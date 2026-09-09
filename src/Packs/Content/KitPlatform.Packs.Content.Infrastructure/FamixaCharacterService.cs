using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class FamixaCharacterService : IFamixaCharacterService
{
    public const string CompilerVersion = "FAMIXA-CHAR-PROMPT-V1";

    private static readonly HashSet<string> Roster = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHAR-001", "CHAR-002", "CHAR-003", "CHAR-004", "CHAR-VO",
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly ContentRepository _repo;

    public FamixaCharacterService(ContentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<FamixaCharacterDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListFamixaCharactersAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FamixaCharacterDto> GetAsync(string characterCode, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetFamixaCharacterAsync(NormCode(characterCode), cancellationToken)
            ?? throw new InvalidOperationException($"Character Registry không có {NormCode(characterCode)}.");
        return ToDto(row);
    }

    public async Task<FamixaCharacterDto> CreateDraftAsync(
        CreateFamixaCharacterRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length < 1) throw new InvalidOperationException("Tên nhân vật bắt buộc.");
        var all = await _repo.ListFamixaCharactersAsync(cancellationToken);
        if (!request.ForceCreate)
        {
            var hits = all.Where(r => string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count > 0)
            {
                throw new InvalidOperationException(
                    $"DUPLICATE: A character with this identity already exists ({string.Join(", ", hits.Select(h => h.CharacterCode))}). USE EXISTING CHARACTER.");
            }
        }
        var code = string.IsNullOrWhiteSpace(request.CharacterCode)
            ? NextCode(all)
            : NormCode(request.CharacterCode);
        if (!Roster.Contains(code) && !request.AllowNew && !request.ForceCreate)
        {
            throw new InvalidOperationException(
                "REQUEST CREATION: Character Registry không có mã này. Không tự tạo CHAR-005+.");
        }
        var existing = await _repo.GetFamixaCharacterAsync(code, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"{code} đã có trong Registry. Không tạo trùng.");
        var visual = request.Visual is "mention" or "voice" ? request.Visual : "frame";
        var universe = string.IsNullOrWhiteSpace(request.Universe) ? "CORE" : request.Universe.Trim().ToUpperInvariant();
        if (universe is not ("CORE" or "RECURRING" or "SUPPORTING" or "BACKGROUND" or "VOICE"))
            universe = "CORE";
        var age = request.InitialAge is > 0 ? request.InitialAge.Value : 11;
        var era = $"A{age}";
        var id = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var canon = SeedCanon(code, name, request.Role ?? "", visual, request.Gender, age, era, universe, request.Description, request.FamilyRole);
        var row = await _repo.InsertFamixaCharacterAsync(
            id,
            versionId,
            code,
            name,
            request.Role ?? "",
            universe,
            visual,
            canon,
            era,
            cancellationToken);
        await Audit(row, "lifecycle", null, "draft", request.Name, "create", null, cancellationToken);
        return ToDto(row);
    }

    public async Task<FamixaCharacterDto> PutCanonAsync(
        string characterCode,
        UpsertFamixaCharacterCanonRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOCKED: UNLOCK REQUEST → CREATE NEW VERSION. Không silent update.");
        if (string.Equals(row.Lifecycle, "archived", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCHIVED: không sửa Canon. Chỉ đọc lịch sử.");
        var json = StripDataUrls(request.Canon.GetRawText());
        var life = row.Lifecycle is "draft" or "designing" or "review" ? "designing" : row.Lifecycle;
        var updated = await _repo.UpdateFamixaCanonAsync(
            row.CharacterCode, json, life, "review", row.ApprovedAt, row.ApprovedBy ?? actor, cancellationToken);
        await Audit(updated, "canon", null, "updated", actor, request.Unlock ? "unlock-edit" : "edit", null, cancellationToken);
        return ToDto(updated);
    }

    public async Task<FamixaCharacterDto> ApproveAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOCKED: UNLOCK + version mới trước khi duyệt lại.");
        var qa = ReadQaStatus(ParseCanon(row.CanonJson));
        if (qa == "FAIL")
            throw new InvalidOperationException("QA FAIL — không APPROVE.");
        var dto = await SetLifecycle(characterCode, "approved", "approved", actor, cancellationToken);
        await Audit(row, "lifecycle", row.Lifecycle, "approved", actor, "approve", "approved", cancellationToken);
        return dto;
    }

    public async Task<FamixaCharacterDto> LockAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Đã LOCK.");
        if (!string.Equals(row.Lifecycle, "approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("APPROVE trước. APPROVE ≠ LOCK.");
        var canon = ParseCanon(row.CanonJson);
        var refs = ReadRefs(canon);
        var front = refs.Any(r => r.Kind.Equals("FRONT", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.Path));
        var frame = row.Visual.Equals("frame", StringComparison.OrdinalIgnoreCase);
        if (frame && !front)
            throw new InvalidOperationException("QA FAIL: thiếu Reference FRONT — không LOCK.");
        if (ReadQaStatus(canon) == "FAIL")
            throw new InvalidOperationException("QA FAIL — không LOCK.");
        var dto = await SetLifecycle(characterCode, "locked", "locked", actor, cancellationToken);
        await Audit(row, "lifecycle", row.Lifecycle, "locked", actor, "lock", "locked", cancellationToken);
        return dto;
    }

    public async Task<FamixaCharacterDto> UnlockAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (!string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ UNLOCK khi đang LOCKED.");
        return await CreateVersionAsync(characterCode, actor, "unlock-request", cancellationToken);
    }

    public async Task<FamixaCharacterDto> PutReferencesAsync(
        string characterCode,
        IReadOnlyList<FamixaCharacterRefDto> references,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOCKED: UNLOCK trước khi đổi Reference. Không silent update.");
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.CanonJson) ? "{}" : row.CanonJson);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText(), JsonOpts)
            ?? new Dictionary<string, JsonElement>();
        var clean = references
            .Where(r => !string.IsNullOrWhiteSpace(r.Kind) && !string.IsNullOrWhiteSpace(r.Path) && !r.Path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Select(r => new { kind = r.Kind.Trim().ToUpperInvariant(), path = r.Path.Trim(), label = r.Label })
            .ToList();
        map["references"] = JsonSerializer.SerializeToElement(clean);
        return ToDto(await _repo.UpdateFamixaCanonAsync(
            row.CharacterCode,
            JsonSerializer.Serialize(map),
            row.Lifecycle,
            row.Lifecycle,
            row.ApprovedAt,
            row.ApprovedBy,
            cancellationToken));
    }

    public async Task<FamixaCharacterGuardDto> GuardAsync(
        FamixaCharacterGuardRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListFamixaCharactersAsync(cancellationToken);
        var byCode = rows.ToDictionary(r => r.CharacterCode, StringComparer.OrdinalIgnoreCase);
        var blocked = new List<string>();
        var resolved = new List<FamixaCharacterGuardItemDto>();
        var extraIds = new List<string>();
        foreach (var raw in request.UnknownNames ?? Array.Empty<string>())
        {
            var name = (raw ?? "").Trim();
            if (name.Length == 0) continue;
            var hit = rows.FirstOrDefault(r =>
                string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.CharacterCode, NormCode(name), StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                extraIds.Add(hit.CharacterCode);
                continue;
            }
            blocked.Add($"REQUEST CREATION: «{name}» chưa có trong Character Registry. Không tự tạo Canon.");
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (request.CharacterIds ?? Array.Empty<string>()).Concat(extraIds))
        {
            var code = NormCode(raw);
            if (code.Length == 0 || !seen.Add(code)) continue;
            if (!byCode.TryGetValue(code, out var row))
            {
                blocked.Add($"REQUEST CREATION: {code} chưa có. Không tự invent nhân vật.");
                continue;
            }
            var canon = ParseCanon(row.CanonJson);
            var refs = ReadRefs(canon);
            var front = refs.Any(r => r.Kind.Equals("FRONT", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.Path));
            var life = row.Lifecycle;
            var frame = row.Visual.Equals("frame", StringComparison.OrdinalIgnoreCase);
            if (frame && life is not "locked")
                blocked.Add($"{code}: Character chưa LOCK — không vào production ({life}).");
            else if (!frame && life is not ("approved" or "locked"))
                blocked.Add($"{code}: Canon chưa APPROVE/LOCK ({life}).");
            if (frame && !front)
                blocked.Add($"{code}: thiếu Reference FRONT — BLOCK.");
            var needVoice = request.HasDialogue is not null
                && request.HasDialogue.TryGetValue(code, out var talk) && talk;
            var voiceId = ReadVoiceId(canon);
            if (string.IsNullOrWhiteSpace(voiceId) && request.VoiceIds is not null)
                request.VoiceIds.TryGetValue(code, out voiceId);
            if (needVoice && string.IsNullOrWhiteSpace(voiceId))
                blocked.Add($"{code}: có thoại nhưng chưa Voice ID.");
            var wardrobe = ReadWardrobeId(canon);
            resolved.Add(new FamixaCharacterGuardItemDto(
                code,
                life,
                row.CurrentEra,
                row.Version,
                wardrobe,
                refs.Select(r => $"{r.Kind}:{r.Path}").ToList(),
                front || !frame));
        }
        return new FamixaCharacterGuardDto(blocked.Count == 0, blocked, resolved, CompilerVersion);
    }

    public async Task<FamixaCharacterDuplicateDto> DetectDuplicateAsync(
        FamixaCharacterDuplicateRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        var rows = await _repo.ListFamixaCharactersAsync(cancellationToken);
        var hits = rows
            .Where(r => string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            .Select(r => new FamixaCharacterDuplicateHitDto(r.CharacterCode, r.Name, r.Role, r.Lifecycle))
            .ToList();
        return hits.Count == 0
            ? new FamixaCharacterDuplicateDto(false, "CREATE_NEW", "", hits)
            : new FamixaCharacterDuplicateDto(
                true,
                "USE_EXISTING",
                $"A character with this identity already exists ({string.Join(", ", hits.Select(h => h.CharacterCode))}).",
                hits);
    }

    public async Task<FamixaCharacterDto> CreateVersionAsync(
        string characterCode,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(characterCode, cancellationToken);
        if (string.Equals(row.Lifecycle, "archived", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCHIVED: không tạo version mới.");
        var next = NextVersion(row.Version);
        var updated = await _repo.InsertFamixaVersionAsync(
            Guid.NewGuid(),
            row.CharacterCode,
            next,
            row.CurrentEra,
            StripDataUrls(row.CanonJson),
            cancellationToken);
        await Audit(row, "version", row.Version, next, actor, reason ?? "new-version", null, cancellationToken);
        return ToDto(updated);
    }

    public async Task<IReadOnlyList<FamixaCharacterAuditDto>> ListAuditAsync(
        string characterCode,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListFamixaAuditAsync(NormCode(characterCode), cancellationToken);
        return rows.Select(r => new FamixaCharacterAuditDto(
            r.Id, r.CharacterCode, r.Version, r.FieldChanged, r.OldValue, r.NewValue,
            r.ChangedBy, r.ChangedAt, r.Reason, r.Approval)).ToList();
    }

    public async Task<IReadOnlyList<FamixaCharacterVersionDto>> ListVersionsAsync(
        string characterCode,
        CancellationToken cancellationToken = default)
    {
        await Require(characterCode, cancellationToken);
        var rows = await _repo.ListFamixaVersionsAsync(NormCode(characterCode), cancellationToken);
        return rows.Select(r => new FamixaCharacterVersionDto(
            r.Id, r.Version, r.Era, r.Status, r.IsCurrentCanon, r.CreatedAt)).ToList();
    }

    private async Task Audit(
        ContentRepository.FamixaCharacterRow row,
        string field,
        string? oldValue,
        string? newValue,
        string? actor,
        string? reason,
        string? approval,
        CancellationToken ct)
    {
        try
        {
            await _repo.InsertFamixaAuditAsync(
                Guid.NewGuid(),
                row.Id,
                row.CharacterCode,
                row.Version,
                field,
                oldValue,
                newValue,
                actor ?? "operator",
                reason,
                approval,
                ct);
        }
        catch (Exception)
        {
            // Audit table arrives with mig 331. Character writes must not fail if it is missing.
        }
    }

    private async Task<FamixaCharacterDto> SetLifecycle(
        string characterCode,
        string lifecycle,
        string versionStatus,
        string? actor,
        CancellationToken ct,
        bool unlock = false)
    {
        var row = await Require(characterCode, ct);
        if (string.Equals(row.Lifecycle, "archived", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCHIVED: không đổi lifecycle.");
        if (string.Equals(row.Lifecycle, "locked", StringComparison.OrdinalIgnoreCase) && !unlock
            && lifecycle is not "locked")
        {
            throw new InvalidOperationException("LOCKED: UNLOCK trước khi đổi Canon.");
        }
        var approvedAt = lifecycle is "approved" or "locked" ? DateTimeOffset.UtcNow : row.ApprovedAt;
        var approvedBy = actor ?? row.ApprovedBy ?? "operator";
        return ToDto(await _repo.UpdateFamixaCanonAsync(
            row.CharacterCode,
            StripDataUrls(row.CanonJson),
            lifecycle,
            versionStatus,
            approvedAt,
            approvedBy,
            ct));
    }

    private async Task<ContentRepository.FamixaCharacterRow> Require(string code, CancellationToken ct) =>
        await _repo.GetFamixaCharacterAsync(NormCode(code), ct)
        ?? throw new InvalidOperationException($"Character Registry không có {NormCode(code)}.");

    private static FamixaCharacterDto ToDto(ContentRepository.FamixaCharacterRow row)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.CanonJson) ? "{}" : row.CanonJson);
        var canon = doc.RootElement.Clone();
        return new FamixaCharacterDto(
            row.Id,
            row.CharacterCode,
            row.Name,
            row.Role,
            row.Universe,
            row.Visual,
            row.Lifecycle,
            row.CurrentVersionId,
            row.CurrentEra,
            row.Version,
            row.IsCurrentCanon,
            row.ApprovedAt,
            row.ApprovedBy,
            canon,
            ReadRefs(canon),
            row.UpdatedAt);
    }

    private static List<FamixaCharacterRefDto> ReadRefs(JsonElement canon)
    {
        if (canon.ValueKind != JsonValueKind.Object || !canon.TryGetProperty("references", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var rows = new List<FamixaCharacterRefDto>();
        foreach (var el in arr.EnumerateArray())
        {
            var kind = el.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
            var path = el.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            var label = el.TryGetProperty("label", out var l) ? l.GetString() : null;
            if (kind.Length == 0 || path.Length == 0 || path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            rows.Add(new FamixaCharacterRefDto(kind, path, label));
        }
        return rows;
    }

    private static JsonElement ParseCanon(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }

    private static string? ReadVoiceId(JsonElement canon)
    {
        if (canon.ValueKind != JsonValueKind.Object || !canon.TryGetProperty("voiceDna", out var v))
            return null;
        return v.TryGetProperty("voiceId", out var id) ? id.GetString() : null;
    }

    private static string? ReadWardrobeId(JsonElement canon)
    {
        if (canon.ValueKind != JsonValueKind.Object || !canon.TryGetProperty("wardrobe", out var w)
            || w.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var el in w.EnumerateArray())
        {
            var set = el.TryGetProperty("set", out var s) ? s.GetString() : null;
            var id = el.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (string.Equals(set, "HOME", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(id))
                return id;
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        return null;
    }

    private static string SeedCanon(
        string code,
        string name,
        string role,
        string visual,
        string? gender,
        int age,
        string era,
        string characterType,
        string? description,
        string? familyRole) =>
        JsonSerializer.Serialize(new
        {
            identity = new
            {
                characterId = code,
                name,
                role,
                gender = gender ?? "",
                currentAge = age,
                currentEra = era,
                familyRole = familyRole ?? "",
                biography = description ?? "",
                characterType,
                initialAge = age,
            },
            visualDna = new { },
            personality = new { },
            personalityDna = Array.Empty<string>(),
            behaviorDna = Array.Empty<object>(),
            voiceDna = new { voiceId = "", language = "vi", provider = "" },
            wardrobe = Array.Empty<object>(),
            relationships = Array.Empty<object>(),
            evolution = new
            {
                initialEra = era,
                eras = new object[]
                {
                    new { era, status = "active" },
                    new { era = "A16", status = "planned" },
                    new { era = "A23", status = "planned" },
                },
            },
            continuityRules = Array.Empty<string>(),
            famixaVisualStyle = new
            {
                summary = "Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait.",
            },
            references = Array.Empty<object>(),
            workspace = new { completed = new { identity = true }, visualProposalStatus = "draft" },
            visual,
        });

    private static string? ReadQaStatus(JsonElement canon)
    {
        if (canon.ValueKind != JsonValueKind.Object) return null;
        if (canon.TryGetProperty("workspace", out var ws)
            && ws.ValueKind == JsonValueKind.Object
            && ws.TryGetProperty("qa", out var qa)
            && qa.TryGetProperty("status", out var st))
        {
            return st.GetString();
        }
        if (canon.TryGetProperty("qa", out var top) && top.TryGetProperty("status", out var topSt))
            return topSt.GetString();
        return null;
    }

    private static string NextCode(IReadOnlyList<ContentRepository.FamixaCharacterRow> rows)
    {
        var max = 0;
        foreach (var row in rows)
        {
            var m = Regex.Match(row.CharacterCode, @"CHAR-(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) max = Math.Max(max, int.Parse(m.Groups[1].Value));
        }
        return $"CHAR-{max + 1:D3}";
    }

    private static string NextVersion(string current)
    {
        var m = Regex.Match(current ?? "V1", @"^V(\d+)$", RegexOptions.IgnoreCase);
        return m.Success ? $"V{int.Parse(m.Groups[1].Value) + 1}" : "V2";
    }

    internal static string StripDataUrls(string json) =>
        Regex.Replace(json ?? "{}", @"data:image[^""']+", "", RegexOptions.IgnoreCase);

    private static string NormCode(string raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        if (t is "CHAR-VO") return t;
        var m = Regex.Match(t, @"CHAR\s*-?\s*(\d+)");
        return m.Success ? $"CHAR-{int.Parse(m.Groups[1].Value):D3}" : t;
    }
}
