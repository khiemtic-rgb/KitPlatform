using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyBlueprintRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyBlueprintRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<BlueprintRow?> GetAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<BlueprintRow>(
            """
            SELECT family_id AS FamilyId,
                   layers_json::text AS LayersJson,
                   dna_json::text AS DnaJson,
                   schema_version AS SchemaVersion,
                   hydrated_at AS HydratedAt,
                   updated_at AS UpdatedAt
            FROM pack_family.family_blueprint
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task UpsertAsync(
        Guid familyId,
        string layersJson,
        string dnaJson,
        int schemaVersion,
        DateTimeOffset? hydratedAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_blueprint (
                tenant_id, family_id, layers_json, dna_json, schema_version, hydrated_at
            )
            VALUES (
                @TenantId, @FamilyId,
                CAST(@LayersJson AS jsonb), CAST(@DnaJson AS jsonb),
                @SchemaVersion, @HydratedAt
            )
            ON CONFLICT (tenant_id, family_id)
            DO UPDATE SET
                layers_json = EXCLUDED.layers_json,
                dna_json = EXCLUDED.dna_json,
                schema_version = EXCLUDED.schema_version,
                hydrated_at = COALESCE(EXCLUDED.hydrated_at, pack_family.family_blueprint.hydrated_at),
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                LayersJson = layersJson,
                DnaJson = dnaJson,
                SchemaVersion = schemaVersion,
                HydratedAt = hydratedAt,
            });
    }

    internal sealed class BlueprintRow
    {
        public Guid FamilyId { get; init; }
        public string LayersJson { get; init; } = "{}";
        public string DnaJson { get; init; } = "{}";
        public int SchemaVersion { get; init; }
        public DateTimeOffset? HydratedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}

/// <summary>Maps onboarding answers → sparse Blueprint layers + DNA card (Wave A).</summary>
internal static class FamilyBlueprintHydrator
{
    public static (string LayersJson, string DnaJson) FromOnboardingPayload(
        string payloadJson,
        string? timezone,
        int childMemberCount)
    {
        using var doc = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        var root = doc.RootElement;

        var ageBand = ReadString(root, "ageBand") ?? "7-9";
        var childName = ReadString(root, "childName") ?? "con";
        var shortName = childName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            ?? childName;
        var goal = ReadString(root, "goal") ?? "fewer_nudges";
        var struggles = ReadStringArray(root, "struggles");
        var priorities = ReadStringArray(root, "priorities");
        var childCountPayload = ReadInt(root, "childCount");
        var childCount = childCountPayload ?? Math.Max(1, childMemberCount);

        var (stageCode, stageLabel) = StageFromAgeBand(ageBand);
        var values = BuildValues(priorities, struggles, goal);
        var focus = BuildFocus(goal, priorities);
        var nextStep = BuildNextStep(shortName, goal, struggles, ageBand);

        var layers = new JsonObject
        {
            ["profile"] = new JsonObject
            {
                ["childCount"] = childCount,
                ["primaryAgeBand"] = ageBand,
                ["primaryChildName"] = childName,
                ["timezone"] = timezone ?? "Asia/Ho_Chi_Minh",
            },
            ["stage"] = new JsonObject
            {
                ["code"] = stageCode,
                ["labelVi"] = stageLabel,
            },
            ["values"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v.Code)).ToArray()),
            ["goals"] = new JsonArray(JsonValue.Create(goal)),
            ["focus"] = new JsonArray(focus.Select(f => (JsonNode?)JsonValue.Create(f.Code)).ToArray()),
            ["sources"] = new JsonObject
            {
                ["hydratedFrom"] = "onboarding",
                ["at"] = DateTimeOffset.UtcNow.ToString("O"),
            },
        };

        var dna = new JsonObject
        {
            ["stageLabelVi"] = stageLabel,
            ["valuesLabelsVi"] = new JsonArray(
                values.Select(v => (JsonNode?)JsonValue.Create(v.LabelVi)).ToArray()),
            ["focusLabelsVi"] = new JsonArray(
                focus.Select(f => (JsonNode?)JsonValue.Create(f.LabelVi)).ToArray()),
            ["nextStepVi"] = nextStep,
        };

        return (layers.ToJsonString(), dna.ToJsonString());
    }

    public static FamilyDnaCardDto ToDnaCard(
        Guid familyId,
        string? layersJson,
        string? dnaJson,
        bool hasBlueprint,
        string tierCode,
        bool isTeaser,
        string? upgradeHintVi)
    {
        var composed = ComposeGuidance(layersJson);
        var needsCapture = composed.NeedsCapture || !hasBlueprint;

        if (string.IsNullOrWhiteSpace(dnaJson) || dnaJson is "{}")
        {
            return new FamilyDnaCardDto(
                familyId,
                hasBlueprint,
                isTeaser,
                tierCode,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                isTeaser ? null : composed.NextStepVi,
                isTeaser ? upgradeHintVi : null,
                composed.CalibrationPhaseCode,
                composed.CalibrationLabelVi,
                composed.CoachTipVi,
                needsCapture,
                composed.CareValueVi,
                composed.GrowthBalanceLabelVi,
                composed.PrimaryWorryCode);
        }

        using var doc = JsonDocument.Parse(dnaJson);
        var root = doc.RootElement;
        var stage = ReadString(root, "stageLabelVi");
        var values = ReadStringArray(root, "valuesLabelsVi");
        var focus = ReadStringArray(root, "focusLabelsVi");
        var next = composed.HasPlaybookSignal
            ? composed.NextStepVi
            : ReadString(root, "nextStepVi") ?? composed.NextStepVi;
        var tip = composed.CoachTipVi;
        var care = composed.CareValueVi ?? ReadString(root, "careValueVi");

        if (isTeaser)
        {
            // Free: Stage + values teaser; still show care value + capture CTA.
            return new FamilyDnaCardDto(
                familyId,
                hasBlueprint,
                true,
                tierCode,
                stage,
                values.Take(2).ToArray(),
                Array.Empty<string>(),
                null,
                upgradeHintVi,
                composed.CalibrationPhaseCode,
                composed.CalibrationLabelVi,
                tip,
                needsCapture,
                care,
                composed.GrowthBalanceLabelVi,
                composed.PrimaryWorryCode);
        }

        return new FamilyDnaCardDto(
            familyId,
            hasBlueprint,
            false,
            tierCode,
            stage,
            values,
            focus,
            next,
            null,
            composed.CalibrationPhaseCode,
            composed.CalibrationLabelVi,
            tip,
            needsCapture,
            care,
            composed.GrowthBalanceLabelVi,
            composed.PrimaryWorryCode);
    }

    private sealed record ComposedGuidance(
        string NextStepVi,
        string CoachTipVi,
        string CareValueVi,
        string? CalibrationPhaseCode,
        string? CalibrationLabelVi,
        string? GrowthBalanceLabelVi,
        string? PrimaryWorryCode,
        bool NeedsCapture,
        bool HasPlaybookSignal);

    private static ComposedGuidance ComposeGuidance(string? layersJson, string? childShortName = null)
    {
        var calSignals = ReadCalibrationSignals(layersJson);
        var who = childShortName ?? ReadPrimaryChildShortName(layersJson);
        var cal = FamilySelfCalibration.BuildGuidance(calSignals, who);
        var gbSignals = ReadGrowthBalanceSignals(layersJson, cal.PhaseCode);
        var gb = FamilyGrowthBalance.BuildGuidance(gbSignals, who);

        var worryKnown = gb.WorryCode is not FamilyGrowthBalance.WorryCodes.Unknown;
        // Prefer Growth Balance next/tip when parent named a worry; else calibration.
        var next = worryKnown ? gb.NextStepVi : cal.NextStepVi;
        var tip = worryKnown ? gb.CoachTipVi : cal.CoachTipVi;
        var needsCapture = cal.PhaseCode == FamilySelfCalibration.Phases.NeedsCapture
            || gb.WorryCode is FamilyGrowthBalance.WorryCodes.Unknown;
        var hasSignal = worryKnown
            || calSignals.SelfView != FamilySelfCalibration.SelfViewCodes.Unknown
            || calSignals.SchoolCode != FamilySelfCalibration.SchoolCodes.Unknown
            || calSignals.PeerShock is FamilySelfCalibration.PeerShockCodes.Mild
                or FamilySelfCalibration.PeerShockCodes.Sharp
            || calSignals.IllusionHits7d > 0;

        return new ComposedGuidance(
            next,
            tip,
            gb.CareValueVi,
            cal.PhaseCode,
            cal.PhaseLabelVi,
            gb.WorryLabelVi,
            gb.WorryCode is FamilyGrowthBalance.WorryCodes.Unknown ? null : gb.WorryCode,
            needsCapture,
            hasSignal);
    }

    public static FamilySelfCalibration.Signals ReadCalibrationSignals(string? layersJson)
    {
        var layers = ParseObject(layersJson ?? "{}");
        var school = layers["context"]?["school"]?["code"]?.GetValue<string>()
            ?? FamilySelfCalibration.SchoolCodes.Unknown;
        var cal = layers["child"]?["selfCalibration"];
        var selfView = cal?["selfView"]?.GetValue<string>()
            ?? FamilySelfCalibration.SelfViewCodes.Unknown;
        var peerShock = cal?["peerShock"]?.GetValue<string>()
            ?? FamilySelfCalibration.PeerShockCodes.Unknown;
        var hits = 0;
        if (cal?["illusionHits7d"] is JsonValue jv && jv.TryGetValue<int>(out var n))
            hits = Math.Max(0, n);

        return new FamilySelfCalibration.Signals(school, selfView, peerShock, hits);
    }

    public static FamilyGrowthBalance.Signals ReadGrowthBalanceSignals(
        string? layersJson,
        string? calibrationPhase = null)
    {
        var layers = ParseObject(layersJson ?? "{}");
        var band = layers["resources"]?["band"]?.GetValue<string>()
            ?? FamilyGrowthBalance.ResourceBands.Unknown;
        var worry = layers["growthBalance"]?["primaryWorry"]?.GetValue<string>()
            ?? FamilyGrowthBalance.WorryCodes.Unknown;
        var phase = calibrationPhase
            ?? layers["child"]?["selfCalibration"]?["phase"]?.GetValue<string>();
        return new FamilyGrowthBalance.Signals(band, worry, phase);
    }

    public static string? ReadPrimaryChildShortName(string? layersJson)
    {
        var layers = ParseObject(layersJson ?? "{}");
        var name = layers["profile"]?["primaryChildName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return null;
        return name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? name;
    }

    /// <summary>Merge calibration + growth-balance capture into layers + refresh DNA.</summary>
    public static (string LayersJson, string DnaJson) ApplyCalibrationCapture(
        string? existingLayersJson,
        string? existingDnaJson,
        FamilyCalibrationCaptureRequest request)
    {
        var layers = ParseObject(existingLayersJson ?? "{}");
        var schoolCode = FamilySelfCalibration.Normalize(
            request.SchoolContextCode,
            FamilySelfCalibration.SchoolCodes.All,
            FamilySelfCalibration.SchoolCodes.Unknown);
        var selfView = FamilySelfCalibration.Normalize(
            request.SelfViewCode,
            FamilySelfCalibration.SelfViewCodes.All,
            FamilySelfCalibration.SelfViewCodes.Unknown);
        var peerShock = FamilySelfCalibration.Normalize(
            request.PeerShockCode,
            FamilySelfCalibration.PeerShockCodes.All,
            FamilySelfCalibration.PeerShockCodes.Unknown);
        var resourceBand = FamilyGrowthBalance.Normalize(
            request.ResourceBandCode,
            FamilyGrowthBalance.ResourceBands.All,
            FamilyGrowthBalance.ResourceBands.Unknown);
        var primaryWorry = FamilyGrowthBalance.Normalize(
            request.PrimaryWorryCode,
            FamilyGrowthBalance.WorryCodes.All,
            FamilyGrowthBalance.WorryCodes.Unknown);

        var context = layers["context"] as JsonObject ?? new JsonObject();
        var school = context["school"] as JsonObject ?? new JsonObject();
        school["code"] = schoolCode;
        school["capturedAt"] = DateTimeOffset.UtcNow.ToString("O");
        school["source"] = "parent_capture";
        context["school"] = school;
        layers["context"] = context;

        var resources = layers["resources"] as JsonObject ?? new JsonObject();
        if (resourceBand is not FamilyGrowthBalance.ResourceBands.Unknown)
        {
            resources["band"] = resourceBand;
            resources["capturedAt"] = DateTimeOffset.UtcNow.ToString("O");
            layers["resources"] = resources;
        }

        var child = layers["child"] as JsonObject ?? new JsonObject();
        var cal = child["selfCalibration"] as JsonObject ?? new JsonObject();
        var prevHits = 0;
        if (cal["illusionHits7d"] is JsonValue hv && hv.TryGetValue<int>(out var hn))
            prevHits = Math.Max(0, hn);

        var signals = new FamilySelfCalibration.Signals(schoolCode, selfView, peerShock, prevHits);
        var who = string.IsNullOrWhiteSpace(request.ChildShortName)
            ? ReadPrimaryChildShortName(layers.ToJsonString())
            : request.ChildShortName!.Trim();
        var calGuidance = FamilySelfCalibration.BuildGuidance(signals, who);
        var now = DateTimeOffset.UtcNow.ToString("O");

        var history = cal["history"] as JsonArray ?? new JsonArray();
        history.Insert(0, new JsonObject
        {
            ["at"] = now,
            ["phase"] = calGuidance.PhaseCode,
            ["selfView"] = selfView,
            ["peerShock"] = peerShock,
            ["school"] = schoolCode,
            ["noteVi"] = string.IsNullOrWhiteSpace(request.NoteVi) ? null : request.NoteVi.Trim(),
        });
        while (history.Count > 8) history.RemoveAt(history.Count - 1);

        cal["selfView"] = selfView;
        cal["peerShock"] = peerShock;
        cal["illusionHits7d"] = prevHits;
        cal["phase"] = calGuidance.PhaseCode;
        cal["updatedAt"] = now;
        cal["history"] = history;
        child["selfCalibration"] = cal;
        layers["child"] = child;

        var gb = layers["growthBalance"] as JsonObject ?? new JsonObject();
        if (primaryWorry is not FamilyGrowthBalance.WorryCodes.Unknown)
        {
            var gbHistory = gb["history"] as JsonArray ?? new JsonArray();
            gbHistory.Insert(0, new JsonObject
            {
                ["at"] = now,
                ["worry"] = primaryWorry,
                ["band"] = resourceBand,
                ["phase"] = calGuidance.PhaseCode,
            });
            while (gbHistory.Count > 8) gbHistory.RemoveAt(gbHistory.Count - 1);
            gb["primaryWorry"] = primaryWorry;
            gb["phase"] = primaryWorry;
            gb["updatedAt"] = now;
            gb["history"] = gbHistory;
            layers["growthBalance"] = gb;
        }

        if (calGuidance.PhaseCode is not FamilySelfCalibration.Phases.NeedsCapture
            and not FamilySelfCalibration.Phases.Steady)
        {
            var focus = layers["focus"] as JsonArray ?? new JsonArray();
            var hasConfidence = focus.Any(x => x?.GetValue<string>() == "confidence");
            if (!hasConfidence) focus.Add("confidence");
            layers["focus"] = focus;
        }

        var composed = ComposeGuidance(layers.ToJsonString(), who);
        return (layers.ToJsonString(), WriteComposedDna(existingDnaJson, composed));
    }

    /// <summary>Refresh phase/DNA from existing layers without appending history.</summary>
    public static (string LayersJson, string DnaJson) RefreshCalibrationDna(
        string? existingLayersJson,
        string? existingDnaJson,
        string? childShortName = null)
    {
        var layers = ParseObject(existingLayersJson ?? "{}");
        var signals = ReadCalibrationSignals(layers.ToJsonString());
        var who = string.IsNullOrWhiteSpace(childShortName)
            ? ReadPrimaryChildShortName(layers.ToJsonString())
            : childShortName;
        var guidance = FamilySelfCalibration.BuildGuidance(signals, who);

        var child = layers["child"] as JsonObject ?? new JsonObject();
        var cal = child["selfCalibration"] as JsonObject ?? new JsonObject();
        cal["phase"] = guidance.PhaseCode;
        cal["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        child["selfCalibration"] = cal;
        layers["child"] = child;

        var composed = ComposeGuidance(layers.ToJsonString(), who);
        return (layers.ToJsonString(), WriteComposedDna(existingDnaJson, composed));
    }

    private static string WriteComposedDna(string? existingDnaJson, ComposedGuidance composed)
    {
        var dna = ParseObject(existingDnaJson ?? "{}");
        dna["nextStepVi"] = composed.NextStepVi;
        dna["coachTipVi"] = composed.CoachTipVi;
        dna["careValueVi"] = composed.CareValueVi;
        dna["calibrationPhaseCode"] = composed.CalibrationPhaseCode;
        dna["calibrationLabelVi"] = composed.CalibrationLabelVi;
        dna["growthBalanceLabelVi"] = composed.GrowthBalanceLabelVi;
        dna["primaryWorryCode"] = composed.PrimaryWorryCode;
        if (dna["focusLabelsVi"] is not JsonArray focusLabels)
            focusLabels = new JsonArray();
        if (composed.CalibrationPhaseCode is not FamilySelfCalibration.Phases.NeedsCapture
            and not FamilySelfCalibration.Phases.Steady
            && focusLabels.All(x => x?.GetValue<string>() != "Tự tin"))
            focusLabels.Add("Tự tin");
        dna["focusLabelsVi"] = focusLabels;
        return dna.ToJsonString();
    }

    public static (string LayersJson, string DnaJson) NoteIllusionHit(
        string? existingLayersJson,
        string? existingDnaJson)
    {
        var layers = ParseObject(existingLayersJson ?? "{}");
        var child = layers["child"] as JsonObject ?? new JsonObject();
        var cal = child["selfCalibration"] as JsonObject ?? new JsonObject();
        var hits = 1;
        if (cal["illusionHits7d"] is JsonValue hv && hv.TryGetValue<int>(out var hn))
            hits = Math.Min(20, Math.Max(0, hn) + 1);
        cal["illusionHits7d"] = hits;
        cal["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");

        var signals = ReadCalibrationSignals(layers.ToJsonString()) with { IllusionHits7d = hits };
        var who = ReadPrimaryChildShortName(layers.ToJsonString());
        var guidance = FamilySelfCalibration.BuildGuidance(signals, who);
        cal["phase"] = guidance.PhaseCode;
        child["selfCalibration"] = cal;
        layers["child"] = child;

        var composed = ComposeGuidance(layers.ToJsonString(), who);
        return (layers.ToJsonString(), WriteComposedDna(existingDnaJson, composed));
    }

    public static string MergeJson(string existingJson, string patchJson, bool replace)
    {
        if (replace || string.IsNullOrWhiteSpace(existingJson) || existingJson is "{}")
            return NormalizeObjectJson(patchJson);

        var existing = ParseObject(existingJson);
        var patch = ParseObject(patchJson);
        foreach (var prop in patch)
            existing[prop.Key] = prop.Value?.DeepClone();
        return existing.ToJsonString();
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return node as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string NormalizeObjectJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return (node as JsonObject)?.ToJsonString() ?? "{}";
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Blueprint JSON không hợp lệ.");
        }
    }

    private static (string Code, string LabelVi) StageFromAgeBand(string ageBand) =>
        ageBand switch
        {
            "4-6" => ("preschool", "Mầm non / chuẩn bị lớp 1"),
            "7-9" => ("primary_early", "Tiểu học (đầu cấp)"),
            "10-12" => ("primary_late", "Tiểu học / tiền trung học"),
            "13+" => ("teen", "Gia đình tuổi teen"),
            _ => ("primary_early", "Tiểu học"),
        };

    private static List<(string Code, string LabelVi)> BuildValues(
        IReadOnlyList<string> priorities,
        IReadOnlyList<string> struggles,
        string goal)
    {
        var map = new List<(string Code, string LabelVi)>();
        void Add(string code, string label)
        {
            if (map.Any(x => x.Code == code)) return;
            map.Add((code, label));
        }

        foreach (var p in priorities)
        {
            switch (p)
            {
                case "autonomy": Add("responsibility", "Trách nhiệm"); break;
                case "study": Add("learning", "Học tập"); break;
                case "screen": Add("balance", "Cân bằng"); break;
                case "chores": Add("contribution", "Đóng góp"); break;
            }
        }

        foreach (var s in struggles)
        {
            switch (s)
            {
                case "homework": Add("learning", "Học tập"); break;
                case "screen": Add("balance", "Cân bằng"); break;
                case "sleep": Add("rest", "Nghỉ ngơi"); break;
                case "tidy":
                case "morning_forget": Add("responsibility", "Trách nhiệm"); break;
                case "brush_teeth": Add("health", "Sức khỏe"); break;
            }
        }

        switch (goal)
        {
            case "fewer_nudges":
            case "more_autonomy": Add("responsibility", "Trách nhiệm"); break;
            case "quality_time": Add("connection", "Gắn kết"); break;
            case "bedtime": Add("rest", "Nghỉ ngơi"); break;
        }

        if (map.Count == 0)
        {
            Add("respect", "Tôn trọng");
            Add("learning", "Học tập");
            Add("responsibility", "Trách nhiệm");
        }

        return map.Take(5).ToList();
    }

    private static List<(string Code, string LabelVi)> BuildFocus(
        string goal,
        IReadOnlyList<string> priorities)
    {
        var list = new List<(string Code, string LabelVi)>();
        void Add(string code, string label)
        {
            if (list.Any(x => x.Code == code)) return;
            list.Add((code, label));
        }

        switch (goal)
        {
            case "fewer_nudges": Add("self_discipline", "Tự giác"); break;
            case "more_autonomy": Add("confidence", "Tự tin"); Add("self_discipline", "Tự giác"); break;
            case "quality_time": Add("connection", "Thời gian chất lượng"); break;
            case "bedtime": Add("sleep_rhythm", "Giờ ngủ ổn định"); break;
        }

        if (priorities.Contains("study")) Add("learning", "Học tập");
        if (priorities.Contains("screen")) Add("screen_balance", "Giảm màn hình");

        if (list.Count == 0)
            Add("self_discipline", "Tự giác");

        return list.Take(3).ToList();
    }

    private static string BuildNextStep(
        string shortName,
        string goal,
        IReadOnlyList<string> struggles,
        string ageBand)
    {
        if (struggles.Contains("homework"))
            return $"Hôm nay thử hỏi {shortName} học được gì — thay vì hỏi điểm.";
        if (struggles.Contains("screen"))
            return $"Thỏa thuận 1 khung màn hình rõ với {shortName} — không tranh cãi lúc đang xem.";
        if (struggles.Contains("sleep") || goal == "bedtime")
            return $"Giữ cùng giờ tắt đèn 3 tối liên tiếp — nhẹ tay, khen khi đúng giờ.";
        if (goal == "quality_time")
            return $"Dành 10 phút không điện thoại với {shortName} tối nay — đọc / kể chuyện.";
        if (goal is "fewer_nudges" or "more_autonomy")
            return ageBand is "13+" or "10-12"
                ? $"Hỏi {shortName} chọn 1 việc tự làm hôm nay — bạn chỉ 👍 khi xong."
                : $"Nhắc {shortName} một lần, rồi chờ — đừng nhắc lại trong 10 phút.";
        return $"Hôm nay chú ý 1 việc nhỏ {shortName} làm được — ghi nhận thay vì sửa.";
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out n)) return n;
        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return el.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();
    }
}
