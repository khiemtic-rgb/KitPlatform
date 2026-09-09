using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterProductionLibraryService : ICharacterProductionLibraryService
{
    private readonly IFamixaCharacterService _characters;
    private readonly KitVideoMasterLockRepository _masters;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly CharacterReferencePackRepository _crp;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly CharacterAuthorityStore _authority;

    public CharacterProductionLibraryService(
        IFamixaCharacterService characters,
        KitVideoMasterLockRepository masters,
        KitVideoCharacterDnaRepository dna,
        KitVideoProductionReferencePackRepository prp,
        CharacterReferencePackRepository crp,
        KitVideoProductionShotRepository shots,
        CharacterAuthorityStore authority)
    {
        _characters = characters;
        _masters = masters;
        _dna = dna;
        _prp = prp;
        _crp = crp;
        _shots = shots;
        _authority = authority;
    }

    public async Task<CharacterLibraryListDto> ListAsync(
        string? query, string? filter, string? sort, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var rows = await ProjectAsync(eraId, cancellationToken);
        var filtered = rows
            .Where(r => CharacterProductionLibraryRules.MatchesSearch(query ?? "", r.View.CharacterId, r.View.Name, r.View.Role))
            .Where(r => CharacterProductionLibraryRules.MatchesFilter(filter ?? "all", r.Ready))
            .ToList();
        var ordered = CharacterProductionLibraryRules.SortRows(
            filtered, sort ?? "series",
            r => r.View.Name, r => r.View.SceneCount, r => r.Ready.Code, r => r.SeriesOrder);
        return new CharacterLibraryListDto(ordered.Select(r => r.View).ToList(), ordered.Count, false);
    }

    public async Task<CharacterLibraryDetailDto> GetAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var id = CharacterProductionLibraryRules.NormalizeCharacterId(characterId);
        var rows = await ProjectAsync(eraId, cancellationToken);
        var hit = rows.FirstOrDefault(r => CharacterProductionLibraryRules.SameTenant(id, r.View.CharacterId));
        if (hit is null)
            throw new InvalidOperationException("LIBRARY_INVALID: nhân vật không thuộc series này.");
        var scenes = (await _shots.ListAsync(id, eraId, cancellationToken))
            .Select(s => new CharacterLibrarySceneDto(s.Id, s.ShotCode, SceneTitle(s), s.ShotSeq))
            .ToList();
        var views = CharacterReferencePackRules.RequiredTypes
            .Select(type =>
            {
                hit.Items.TryGetValue(type, out var item);
                var present = item is not null && !string.IsNullOrWhiteSpace(item.ArtifactPath);
                return new CharacterLibraryViewItemDto(type, present, present ? item!.Id : null);
            })
            .ToList();
        return new CharacterLibraryDetailDto(
            hit.View, views, scenes,
            CharacterProductionLibraryRules.TechnicalSnapshot(hit.Snap, hit.Ready),
            false);
    }

    public IReadOnlyList<string> RunRegression() => CharacterProductionLibraryV1Regression.Run();

    private sealed record Row(
        CharacterLibraryViewDto View,
        CharacterProductionLibraryRules.ReadinessView Ready,
        CharacterProductionLibraryRules.AuthoritySnapshot Snap,
        Dictionary<string, CharacterReferencePackRepository.ItemRow> Items,
        int SeriesOrder);

    private async Task<IReadOnlyList<Row>> ProjectAsync(string eraId, CancellationToken ct)
    {
        var people = await _characters.ListAsync(ct);
        var masters = (await _masters.ListLatestAsync(eraId, ct)).ToDictionary(x => x.CharacterId, StringComparer.OrdinalIgnoreCase);
        var dnas = (await _dna.ListLatestAsync(eraId, ct)).ToDictionary(x => x.CharacterId, StringComparer.OrdinalIgnoreCase);
        var prps = (await _prp.ListLatestAsync(eraId, ct)).ToDictionary(x => x.CharacterId, StringComparer.OrdinalIgnoreCase);
        var packs = (await _crp.ListLatestAsync(eraId, ct)).ToDictionary(x => x.CharacterId, StringComparer.OrdinalIgnoreCase);
        var items = await _crp.ListItemsForPacksAsync(packs.Values.Select(p => p.Id).ToList(), ct);
        var byPack = items.GroupBy(i => i.PackId).ToDictionary(g => g.Key, g => g.ToList());
        var counts = await _shots.CountByCharacterAsync(ct);

        var rows = new List<Row>();
        for (var index = 0; index < people.Count; index++)
        {
            var person = people[index];
            masters.TryGetValue(person.CharacterCode, out var master);
            dnas.TryGetValue(person.CharacterCode, out var dna);
            prps.TryGetValue(person.CharacterCode, out var prp);
            if (master is null && dna is null && prp is null)
            {
                var auth = await _authority.ReadAsync(person.CharacterCode, eraId, ct);
                master = _authority.ToOfficialMaster(auth);
                dna = _authority.ToOfficialDna(auth);
                prp = _authority.ToOfficialPrp(auth);
            }
            packs.TryGetValue(person.CharacterCode, out var pack);
            var packItems = pack is null ? [] : byPack.GetValueOrDefault(pack.Id) ?? [];
            var itemMap = packItems.ToDictionary(i => i.RefType, StringComparer.OrdinalIgnoreCase);
            var missingTypes = CharacterReferencePackRules.RequiredTypes
                .Where(t => !itemMap.TryGetValue(t, out var row) || string.IsNullOrWhiteSpace(row.ArtifactPath))
                .ToList();
            var readyCount = CharacterReferencePackRules.RequiredTypes.Length - missingTypes.Count;
            var identityPass = true;
            var crpStatus = (pack?.Status ?? "").ToUpperInvariant();
            if (dna is not null && packItems.Count > 0
                && crpStatus is "VALIDATED" or "REVIEW" or "APPROVED" or "DIRECTOR_APPROVED" or "LOCKED" or "REJECTED")
            {
                var spec = ReadJson(dna.SpecJson);
                var entries = packItems.Select(i =>
                    new CharacterReferencePackRules.RefEntry(
                        i.RefType, i.ArtifactPath, i.ArtifactSha256, i.ArtifactSha256, ReadJson(i.MetadataJson),
                        CharacterReferencePackRules.RequiredTypes.Contains(i.RefType))).ToList();
                identityPass = !CharacterReferencePackRules.IdentityBlocks(
                    CharacterReferencePackRules.EvaluateIdentity(spec, entries));
            }
            var authority = new CharacterProductionLibraryRules.AuthoritySnapshot(
                master is not null,
                master is not null && CharacterProductionLibraryRules.IsLocked(master.Status),
                dna is not null,
                dna is not null && CharacterProductionLibraryRules.IsLocked(dna.Status),
                prp is not null,
                prp is not null && CharacterProductionLibraryRules.IsLocked(prp.Status),
                pack?.Status,
                pack?.PackVersion,
                readyCount == CharacterReferencePackRules.RequiredTypes.Length,
                identityPass,
                readyCount,
                CharacterReferencePackRules.RequiredTypes.Length,
                counts.GetValueOrDefault(person.CharacterCode),
                missingTypes);
            var ready = CharacterProductionLibraryRules.EvaluateReadiness(authority);
            var front = itemMap.GetValueOrDefault("FRONT");
            var view = new CharacterLibraryViewDto(
                person.CharacterCode,
                person.Name,
                person.Name,
                person.Role,
                ready.Bucket,
                ready.Code,
                ready.Label,
                ready.Reason,
                ready.NextAction,
                ready.CanUse,
                authority.MasterLocked,
                authority.DnaLocked,
                authority.PrpLocked,
                pack?.Status,
                pack?.PackVersion,
                authority.RequiredReady,
                authority.RequiredTotal,
                authority.SceneCount,
                pack?.Id,
                front is not null && !string.IsNullOrWhiteSpace(front.ArtifactPath) ? front.Id : null,
                missingTypes);
            rows.Add(new Row(view, ready, authority, itemMap, index));
        }
        return rows;
    }

    private static JsonElement ReadJson(string? raw)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { });
        }
    }

    private static string SceneTitle(KitVideoProductionShotRepository.ShotRow shot)
    {
        if (!string.IsNullOrWhiteSpace(shot.Note)) return shot.Note.Trim();
        try
        {
            var spec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(shot.SpecJson) ? "{}" : shot.SpecJson);
            if (spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("scene", out var scene)
                && scene.ValueKind == JsonValueKind.Object && scene.TryGetProperty("summary", out var summary)
                && summary.ValueKind == JsonValueKind.String)
            {
                return summary.GetString() ?? shot.ShotCode;
            }
        }
        catch (JsonException)
        {
        }
        return shot.ShotCode;
    }
}
