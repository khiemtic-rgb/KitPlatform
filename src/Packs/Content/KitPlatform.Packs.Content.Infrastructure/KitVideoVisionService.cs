using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoVisionService : IKitVideoVisionService
{
    private readonly KitVideoVisionRepository _repo;
    private readonly IVisualPromptCompiler _compiler;
    private readonly IVisionQa _qa;

    public KitVideoVisionService(KitVideoVisionRepository repo, IVisualPromptCompiler compiler, IVisionQa qa)
    {
        _repo = repo;
        _compiler = compiler;
        _qa = qa;
    }

    public KitVideoGenerationRequestDto Compile(KitVideoVisualCompileRequest request) =>
        _compiler.Compile(request.Contract, request.ProjectStyle, request.Dialogue);

    public KitVideoVisionQaDto Evaluate(KitVideoVisionEvaluateRequest request) =>
        _qa.Evaluate(request.Contract, request.Observation);

    public KitVideoRepairDto Diagnose(KitVideoVisionQaDto qa) => KitVideoVisionRules.Diagnose(qa);

    public string Revision(string note) => KitVideoVisionRules.DirectorRevision(note);

    public async Task<KitVideoKeyframeAttemptDto> RecordAttemptAsync(
        KitVideoRecordAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        var shot = (request.ShotCode ?? "").Trim().ToUpperInvariant();
        var existing = await _repo.ListAttemptsAsync(request.ProductionId, shot, cancellationToken);
        var lastFail = existing.LastOrDefault(a => a.Status is "QA_FAIL" or "REVIEW_REQUIRED");
        if (lastFail is not null)
            KitVideoVisionRules.EnsureNotBlindRetry(lastFail.Fingerprint, request.Fingerprint, request.StrategyChanged);
        var no = existing.Count + 1;
        var qa = request.Qa;
        var status = no > KitVideoVisionRules.MaxAutoAttempts
            ? "REVIEW_REQUIRED"
            : qa?.Status == "FAIL" ? "QA_FAIL" : qa?.Status == "PASS" ? "QA_PASS" : "GENERATED";
        var repair = qa?.Status == "FAIL" ? KitVideoVisionRules.Diagnose(qa) : null;
        var row = new KitVideoVisionRepository.AttemptRow
        {
            Id = Guid.NewGuid(),
            ProductionId = request.ProductionId,
            ShotCode = shot,
            AttemptNo = no,
            Status = status,
            Fingerprint = request.Fingerprint,
            QaJson = qa is null ? "{}" : JsonSerializer.Serialize(qa),
            RepairJson = repair is null ? "{}" : JsonSerializer.Serialize(repair),
        };
        await _repo.InsertAttemptAsync(row, request.PromptJson ?? "{}", cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ContractJson) && !string.IsNullOrWhiteSpace(request.PromptJson))
        {
            await _repo.UpsertContractAsync(
                request.ProductionId, shot, request.ContractJson, request.PromptJson, request.Fingerprint, cancellationToken);
        }
        return Map(row, qa, repair);
    }

    public async Task<IReadOnlyList<KitVideoKeyframeAttemptDto>> ListAttemptsAsync(
        Guid productionId,
        string shotCode,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListAttemptsAsync(productionId, (shotCode ?? "").Trim().ToUpperInvariant(), cancellationToken);
        return rows.Select(r => Map(r, ParseQa(r.QaJson), ParseRepair(r.RepairJson))).ToList();
    }

    public async Task<KitVideoKeyframeAttemptDto> DecideAsync(
        KitVideoDirectorDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListAttemptsAsync(request.ProductionId, request.ShotCode.Trim().ToUpperInvariant(), cancellationToken);
        var row = rows.FirstOrDefault(r => r.Id == request.AttemptId)
            ?? throw new InvalidOperationException("Attempt không tồn tại.");
        var qa = ParseQa(row.QaJson);
        if (string.Equals(request.Decision, "APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            if (qa?.Status != "PASS")
                throw new InvalidOperationException("Chỉ APPROVE khi Vision QA PASS.");
            await _repo.SetAttemptStatusAsync(row.Id, "APPROVED", row.QaJson, cancellationToken);
            row.Status = "APPROVED";
        }
        else
        {
            if (qa is not null)
                qa = qa with { AllowI2v = false, CanBeReference = false };
            var json = qa is null ? row.QaJson : JsonSerializer.Serialize(qa);
            await _repo.SetAttemptStatusAsync(row.Id, "REJECTED", json, cancellationToken);
            row.Status = "REJECTED";
            row.QaJson = json;
        }
        return Map(row, qa, ParseRepair(row.RepairJson));
    }

    public KitVideoI2vReadyPackageDto I2vPackage(KitVideoKeyframeAttemptDto attempt) =>
        KitVideoVisionRules.I2vPackage(attempt.ShotCode, attempt.AttemptId.ToString(), attempt.Status, attempt.Qa?.Status, attempt.Qa?.AllowI2v == true);

    private static KitVideoKeyframeAttemptDto Map(
        KitVideoVisionRepository.AttemptRow row,
        KitVideoVisionQaDto? qa,
        KitVideoRepairDto? repair) =>
        new(row.Id, row.ProductionId, row.ShotCode, row.AttemptNo, row.Status, row.Fingerprint, qa, repair, row.ImagePath, row.CreatedAt);

    private static KitVideoVisionQaDto? ParseQa(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}") return null;
        return JsonSerializer.Deserialize<KitVideoVisionQaDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static KitVideoRepairDto? ParseRepair(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}") return null;
        return JsonSerializer.Deserialize<KitVideoRepairDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
