using System.Text.Json.Nodes;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public sealed class BusinessIntentSession : IBusinessIntentSession
{
    public const int MaxUtteranceLength = 4000;

    readonly IBusinessIntentInterpreter _interpreter;
    readonly IReadOnlyDictionary<string, IBusinessCapabilityHandler> _handlers;
    readonly IPaperDecisionRecorder? _decisions;
    readonly IPaperTaskStore? _tasks;

    public BusinessIntentSession(
        IBusinessIntentInterpreter interpreter,
        IEnumerable<IBusinessCapabilityHandler> handlers,
        IPaperDecisionRecorder? decisions = null,
        IPaperTaskStore? tasks = null)
    {
        _interpreter = interpreter;
        _decisions = decisions;
        _tasks = tasks;
        var map = new Dictionary<string, IBusinessCapabilityHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            if (!map.TryAdd(handler.CapabilityId, handler))
                throw new InvalidOperationException("Duplicate capability handler: " + handler.CapabilityId);
        }

        _handlers = map;
    }

    public async Task<BusinessIntentEvidence> HandleAsync(
        string? utterance,
        BusinessCaller caller,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var catalog = PlatformCapabilityCatalog.All;
        var catalogHash = PlatformCapabilityCatalog.Fingerprint(catalog);
        var catalogIds = catalog.Select(item => item.Id).ToArray();
        var text = utterance ?? "";
        var utteranceHash = BusinessIntentHash.Sha256(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            var blocked = BusinessIntentPlanner.Blocked("not_called", IntentDisposition.EmptyRequest, "Empty request.");
            return Evidence(correlationId, utteranceHash, catalogHash, catalogIds, blocked, [], "blocked",
                "Yêu cầu trống. Mô hình không được gọi và không có handler nào chạy.");
        }

        if (text.Length > MaxUtteranceLength)
        {
            var blocked = BusinessIntentPlanner.Blocked("not_called", IntentDisposition.TooLong, "Request is longer than 4000 characters.");
            return Evidence(correlationId, utteranceHash, catalogHash, catalogIds, blocked, [], "blocked",
                "Yêu cầu dài hơn 4000 ký tự. Mô hình không được gọi và không có handler nào chạy.");
        }

        var outcome = await _interpreter.InterpretAsync(
            text,
            catalog,
            new InterpretationContext(caller.TenantId, caller.IsAdmin, caller.Permissions.ToArray()),
            cancellationToken);

        switch (outcome)
        {
            case InterpretationOutcome.Unavailable:
                {
                    var blocked = BusinessIntentPlanner.Blocked("unavailable", IntentDisposition.NoCapability, "Language model is not configured.");
                    return Evidence(correlationId, utteranceHash, catalogHash, catalogIds, blocked, [], "unavailable",
                        "Chưa cấu hình mô hình hiểu ngôn ngữ. Hệ thống không đoán bằng từ khóa và không gọi handler.");
                }
            case InterpretationOutcome.Unreadable unreadable:
                {
                    var blocked = BusinessIntentPlanner.Blocked("unreadable", IntentDisposition.NoCapability, unreadable.Reason);
                    return Evidence(correlationId, utteranceHash, catalogHash, catalogIds, blocked, [], "unreadable",
                        UnreadableNextStep(unreadable.Reason));
                }
            case InterpretationOutcome.Ready ready:
                {
                    var plan = BusinessIntentPlanner.Plan("model", ready.Interpretation, catalog, caller);
                    var execution = new List<StepEvidence>();
                    foreach (var step in plan.Steps)
                    {
                        if (!BusinessIntentPlanner.MayExecute(step, ready.Interpretation, catalog, caller))
                            continue;
                        execution.Add(await RunAsync(step, caller, cancellationToken));
                    }

                    var recordedNote = await RecordProposedPaperAsync(plan, execution, caller, cancellationToken);
                    var callerMet = execution.Any(item => item.CallerOutcomeMet);
                    var outcomeName = callerMet
                        ? "completed"
                        : execution.Count > 0 ? "ran_incomplete" : "blocked";
                    var next = NextStep(plan, execution);
                    if (!string.IsNullOrWhiteSpace(recordedNote))
                        next = next + " " + recordedNote;
                    return Evidence(
                        correlationId,
                        utteranceHash,
                        catalogHash,
                        catalogIds,
                        plan,
                        execution,
                        outcomeName,
                        next);
                }
            default:
                {
                    var blocked = BusinessIntentPlanner.Blocked("unreadable", IntentDisposition.NoCapability, "Unknown interpreter result.");
                    return Evidence(correlationId, utteranceHash, catalogHash, catalogIds, blocked, [], "unreadable",
                        "Mô hình không trả về JSON hợp lệ. Không có handler nào chạy.");
                }
        }
    }

    async Task<StepEvidence> RunAsync(PlanStep step, BusinessCaller caller, CancellationToken cancellationToken)
    {
        var capabilityId = step.CapabilityId ?? "";
        var descriptor = PlatformCapabilityCatalog.All.First(item => item.Id == capabilityId);
        if (!_handlers.TryGetValue(capabilityId, out var handler))
        {
            var missing = BusinessIntentJson.Serialize(new { status = "handler_missing", wroteData = false, externalCall = false });
            return new StepEvidence(capabilityId, step.Disposition, "handler_missing", false, false, false, false, BusinessIntentHash.Sha256(missing), missing);
        }

        CapabilityExecution result;
        try
        {
            result = await handler.ExecuteAsync(caller, step.Slots, cancellationToken);
        }
        catch (Exception ex)
        {
            var failed = BusinessIntentJson.Serialize(new { status = "handler_failed", exception = ex.GetType().Name, wroteData = false, externalCall = false });
            return new StepEvidence(capabilityId, step.Disposition, "handler_failed", false, false, false, false, BusinessIntentHash.Sha256(failed), failed);
        }

        var json = string.IsNullOrWhiteSpace(result.EvidenceJson) ? "{}" : result.EvidenceJson;
        var callerMet = descriptor.CanSatisfyCallerGoal
            && result.CapabilityCompletionMet
            && step.Disposition == IntentDisposition.Run
            && !result.WroteData
            && !result.ExternalCall;
        return new StepEvidence(
            capabilityId,
            step.Disposition,
            result.Status,
            result.CapabilityCompletionMet,
            callerMet,
            result.WroteData,
            result.ExternalCall,
            BusinessIntentHash.Sha256(json),
            json);
    }

    async Task<string?> RecordProposedPaperAsync(
        BusinessPlan plan,
        List<StepEvidence> execution,
        BusinessCaller caller,
        CancellationToken cancellationToken)
    {
        if (_decisions is null)
            return null;
        var index = execution.FindIndex(step =>
            step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate
            && step.Status == "approval_required");
        if (index < 0)
            return null;
        var planStep = plan.Steps.FirstOrDefault(step =>
            step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate
            && step.RunHandler
            && step.Disposition == IntentDisposition.Evaluate);
        if (planStep is null)
            return null;
        if (JsonNode.Parse(execution[index].EvidenceJson) is not JsonObject body)
            return "Kết quả cổng paper không đọc được. Decision không được lưu.";
        if (body["riskAllowed"] is not JsonValue risk || !risk.TryGetValue<bool>(out var allowed) || !allowed)
            return null;
        if (body["fillRecorded"] is JsonValue filled && filled.TryGetValue<bool>(out var wasFilled) && wasFilled)
            return "Cổng báo đã ghi khớp lệnh trước phê duyệt. Decision không được lưu thêm.";

        RecordedPaperDecision recorded;
        try
        {
            recorded = await _decisions.RecordProposedAsync(caller, planStep.Slots, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TradingOperationConflictException)
        {
            body["decisionPersisted"] = false;
            body["decisionError"] = ex.Message;
            ReplaceEvidence(execution, index, body);
            return ex.Message;
        }
        catch (Exception ex)
        {
            body["decisionPersisted"] = false;
            body["decisionError"] = ex.GetType().Name;
            ReplaceEvidence(execution, index, body);
            return ex.GetType().Name;
        }

        body["decisionId"] = recorded.DecisionId.ToString("D");
        body["decisionStatus"] = recorded.Status;
        body["operationKey"] = recorded.OperationKey;
        body["decisionPersisted"] = true;
        if (_tasks is not null)
        {
            try
            {
                var evidenceJson = body.ToJsonString();
                var task = await _tasks.ContinueProposedAsync(
                    caller,
                    recorded.DecisionId,
                    recorded.OperationKey,
                    planStep.Slots,
                    evidenceJson,
                    BusinessIntentHash.Sha256(evidenceJson),
                    cancellationToken);
                body["taskId"] = task.TaskId.ToString("D");
                body["taskStatus"] = task.Status;
                body["taskGoal"] = task.Goal;
            }
            catch (Exception ex) when (ex is InvalidOperationException or TradingOperationConflictException)
            {
                body["taskError"] = ex.Message;
            }
        }
        ReplaceEvidence(execution, index, body);
        return "Đã lưu decision " + recorded.DecisionId.ToString("D")
            + " trạng thái " + recorded.Status
            + ". Chưa có paper execution. Phê duyệt hoặc từ chối là bước riêng trên quyết định này.";
    }

    static void ReplaceEvidence(List<StepEvidence> execution, int index, JsonObject body)
    {
        var json = body.ToJsonString();
        execution[index] = execution[index] with
        {
            EvidenceJson = json,
            EvidenceSha256 = BusinessIntentHash.Sha256(json),
        };
    }

    static BusinessIntentEvidence Evidence(
        Guid correlationId,
        string utteranceHash,
        string catalogHash,
        IReadOnlyList<string> catalogIds,
        BusinessPlan plan,
        IReadOnlyList<StepEvidence> execution,
        string outcome,
        string nextStep)
    {
        var interpretation = plan.Interpretation;
        return new BusinessIntentEvidence(
            correlationId,
            outcome,
            utteranceHash,
            catalogHash,
            catalogIds,
            plan.Understanding,
            interpretation?.Goal,
            interpretation?.CompletionConditions ?? [],
            interpretation?.ContextUsed ?? [],
            interpretation?.MissingInformation ?? [],
            plan.HumanDecisions,
            interpretation?.Ambiguous ?? false,
            interpretation?.Limit,
            interpretation?.NextStep,
            interpretation?.RequestedTenant,
            plan.Rejected,
            plan.Steps,
            execution,
            execution.Any(item => item.CallerOutcomeMet),
            nextStep);
    }

    static string UnreadableNextStep(string reason)
    {
        if (reason == "model_unreachable")
            return "Không gọi được mô hình trong thời gian chờ. Không có handler nào chạy.";
        if (reason.StartsWith("model_http_status_", StringComparison.Ordinal))
            return "Mô hình trả lỗi HTTP. Không có handler nào chạy.";
        return "Mô hình không trả về JSON hợp lệ. Không có handler nào chạy.";
    }

    static string NextStep(BusinessPlan plan, IReadOnlyList<StepEvidence> execution)
    {
        var lines = new List<string>();
        if (plan.Steps.Any(step => step.Disposition == IntentDisposition.TenantConflict))
            lines.Add("Yêu cầu nhắm tới tenant khác với tenant của phiên đăng nhập. Không có handler nào chạy.");
        if (plan.Interpretation?.Ambiguous == true || plan.Steps.Any(step => step.Disposition == IntentDisposition.Ambiguous))
        {
            lines.Add("Chưa đủ căn cứ để chọn hành động. Hãy nói rõ đối tượng và việc cần làm. Không có handler nào chạy.");
            var unconfirmed = plan.Interpretation?.MissingInformation ?? [];
            if (unconfirmed.Count > 0)
                lines.Add("Model có ghi mục còn thiếu, nhưng đó chưa phải ý định đã xác nhận: " + string.Join(", ", unconfirmed.Select(item => item.Slot)) + ".");
        }

        if (plan.Steps.Any(step => step.Slots.TryGetValue("mode", out var mode) && mode.Equals("live", StringComparison.OrdinalIgnoreCase)))
            lines.Add("Lệnh live không được gửi. Không có giá trị còn thiếu nào được bịa để chạy cổng.");
        foreach (var step in plan.Steps.Where(step => step.Disposition == IntentDisposition.PermissionDenied))
            lines.Add("Thiếu quyền cho " + step.CapabilityId + ". Handler không được gọi.");
        foreach (var step in plan.Steps.Where(step => step.Disposition == IntentDisposition.MissingInformation))
            lines.Add("Thiếu thông tin (" + string.Join(", ", step.MissingSlots) + ") cho " + step.CapabilityId + ". Không có giá trị được điền thay và handler không chạy.");
        foreach (var step in plan.Steps.Where(step => step.Disposition == IntentDisposition.UnknownSideEffect))
            lines.Add("Capability " + step.CapabilityId + " có side effect chưa xác định. Handler không được gọi.");
        if (plan.Rejected.Count > 0 || plan.Steps.Any(step => step.Disposition == IntentDisposition.NotInCatalog))
            lines.Add("Có capability không nằm trong catalog. Capability đó không được tạo mới và không được gọi.");
        if (plan.Steps.Any(step => step.Disposition == IntentDisposition.NoCapability) && execution.Count == 0 && lines.Count == 0)
        {
            var missing = plan.Interpretation?.MissingInformation ?? [];
            lines.Add(missing.Count > 0
                ? "Yêu cầu chưa xác định capability trong catalog. Không chọn domain thay người dùng và không gọi handler. Model ghi thiếu: " + string.Join(", ", missing.Select(item => item.Slot)) + "."
                : "Không có capability trong catalog khớp yêu cầu. Không tạo capability mới và không gọi handler.");
        }

        foreach (var step in execution)
        {
            if (step.CapabilityId == PlatformCapabilityCatalog.ResearchPublicationRead)
            {
                lines.Add(step.CapabilityCompletionMet
                    ? "Đã đọc publication theo catalog. Mã băm nằm trong evidence. Không có file nào bị sửa."
                    : "Đã gọi trình đọc publication. Kết quả không đủ nội dung đã xuất bản. Không có nội dung được bịa.");
            }
            else if (step.CapabilityId == PlatformCapabilityCatalog.TradingDeskSummaryRead)
            {
                if (step.Status == "desk_summary" && !step.CapabilityCompletionMet)
                    lines.Add("Handler sổ cái đã chạy. Payload của store đánh dấu thiếu dữ liệu hoặc chỉ là fixture, nên các số không phải giao dịch đã xác minh.");
                else if (step.CapabilityCompletionMet)
                    lines.Add("Đã đọc sổ cái của tenant trong phiên. Số liệu là kết quả store trả về.");
                else
                    lines.Add("Store sổ cái không trả được dữ liệu. Không có số liệu được bịa.");
            }
            else if (step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate)
            {
                if (step.Status == "risk_policy_absent")
                    lines.Add("Hạn mức rủi ro không có trong yêu cầu nên không được bịa. Phê duyệt trong câu nói không được chấp nhận. Khớp lệnh giấy cần phê duyệt riêng trên một decision đã tồn tại. Không ghi sổ và không gửi lệnh ra sàn.");
                else
                    lines.Add("Đã chạy cổng rủi ro hoặc cổng paper hiện có. Câu nói không phải phê duyệt. Không ghi sổ và không gửi lệnh ra sàn. Khớp lệnh giấy cần phê duyệt riêng trên một decision đã tồn tại.");
            }
        }

        if (lines.Count == 0)
            lines.Add("Không có bước nào hoàn thành mục tiêu của người gọi.");
        return string.Join(" ", lines);
    }
}
