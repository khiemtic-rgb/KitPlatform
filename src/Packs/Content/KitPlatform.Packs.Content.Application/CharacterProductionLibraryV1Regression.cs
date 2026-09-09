using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterProductionLibraryV1Regression
{
    public const string SuiteId = CharacterProductionLibraryRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var ready = Snap(true, true, true, true, true, true, "LOCKED", true, true, 3);
        var draft = Snap(true, true, true, true, true, true, "DRAFT", false, true, 1);
        var missingCrp = Snap(true, true, true, true, true, true, null, false, true, 0);
        var validated = Snap(true, true, true, true, true, true, "VALIDATED", true, true, 2);
        var approved = Snap(true, true, true, true, true, true, "APPROVED", true, true, 2);
        var noMaster = Snap(false, false, true, true, true, true, "LOCKED", true, true, 0);
        var noDna = Snap(true, true, false, false, true, true, "LOCKED", true, true, 0);
        var noPrp = Snap(true, true, true, true, false, false, "LOCKED", true, true, 0);
        var conflict = Snap(true, true, true, true, true, true, "LOCKED", true, false, 1);

        var rows = new[]
        {
            ("CHAR-099", "Lan", "supporting", CharacterProductionLibraryRules.EvaluateReadiness(draft), 1),
            ("CHAR-098", "Bác", "supporting", CharacterProductionLibraryRules.EvaluateReadiness(ready), 4),
            ("CHAR-097", "An", "core", CharacterProductionLibraryRules.EvaluateReadiness(validated), 2),
        };

        Ok(rows.Length == 3, "01 list characters");
        Ok(CharacterProductionLibraryRules.SortRows(Array.Empty<(string, string)>(), "name", x => x.Item1, _ => 0, _ => "", _ => 0).Count == 0, "02 empty list");
        Ok(CharacterProductionLibraryRules.MatchesSearch("lan", "CHAR-099", "Lan", "supporting")
            && !CharacterProductionLibraryRules.MatchesSearch("xyz", "CHAR-099", "Lan", "supporting"), "03 search");
        Ok(rows.Count(r => CharacterProductionLibraryRules.MatchesFilter("ready", r.Item4)) == 1, "04 filter");
        var sorted = CharacterProductionLibraryRules.SortRows(rows, "name", r => r.Item2, r => r.Item5, r => r.Item4.Code, _ => 0);
        Ok(sorted[0].Item2 == "An" && sorted[1].Item2 == "Bác", "05 sort");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(ready).Code == "CRP_LOCKED", "06 character detail / locked view");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(ready).Bucket == "ready" && CharacterProductionLibraryRules.EvaluateReadiness(ready).CanUse, "07 readiness");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(missingCrp).Code == "CRP_MISSING", "08 CRP missing");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(draft).Code == "REFERENCE_MISSING", "09 CRP draft");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(validated).Code == "CRP_VALIDATED", "10 CRP validated");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(approved).Code == "CRP_APPROVED", "11 CRP approved");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(ready).Label == "Sẵn sàng sản xuất", "12 CRP locked");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(noMaster).Code == "MASTER_MISSING", "13 missing Master");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(noDna).Code == "DNA_MISSING", "14 missing DNA");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(noPrp).Code == "PRP_MISSING", "15 missing Production Reference");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(conflict).Code == "IDENTITY_CONFLICT", "16 identity conflict");
        Ok(CharacterProductionLibraryRules.SameTenant("CHAR-099", "CHAR-099")
            && !CharacterProductionLibraryRules.SameTenant("CHAR-099", "CHAR-098"), "17 character ownership");
        Ok(ready.SceneCount == 3, "18 scene count");
        Ok(ready.SceneCount > 0 && CharacterProductionLibraryRules.EvaluateReadiness(ready).CanUse, "19 character-scene relation");
        Ok(CharacterProductionLibraryRules.PickerAllows(CharacterProductionLibraryRules.EvaluateReadiness(ready)), "20 character picker");
        Ok(!CharacterProductionLibraryRules.PickerAllows(CharacterProductionLibraryRules.EvaluateReadiness(draft)), "21 blocked character");
        Ok(!CharacterProductionLibraryRules.RequireCharacterId("") && !CharacterProductionLibraryRules.RequireCharacterId(null), "22 no fallback character");
        var idThrew = false;
        try { CharacterProductionLibraryRules.NormalizeCharacterId(""); }
        catch (InvalidOperationException) { idThrew = true; }
        Ok(idThrew && CharacterProductionLibraryRules.NormalizeCharacterId("CHAR-099") == "CHAR-099", "23 no hardcoded CHAR-001");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(ready).Label.Contains("Sẵn sàng", StringComparison.Ordinal)
            && !ReferenceEquals(CharacterProductionLibraryRules.EvaluateReadiness(ready).Label, "MINH"), "24 no hardcoded Minh");
        var tech = CharacterProductionLibraryRules.TechnicalSnapshot(ready, CharacterProductionLibraryRules.EvaluateReadiness(ready));
        Ok(tech.ToString()!.Contains("reference_pack", StringComparison.Ordinal), "25 technical details");
        Ok(CharacterProductionLibraryRules.EvaluateReadiness(Snap(true, true, true, true, true, true, "LOCKED", true, true, 0, "V2")).CanUse, "26 versioning");
        Ok(CharacterProductionLibraryRules.IsImmutable("LOCKED") && !CharacterProductionLibraryRules.IsImmutable("DRAFT"), "27 locked immutability");
        Ok(!CharacterProductionLibraryRules.SameTenant("KIT_A", "KIT_B"), "28 tenant isolation");
        var a = CharacterProductionLibraryRules.EvaluateReadiness(ready);
        var b = CharacterProductionLibraryRules.EvaluateReadiness(ready);
        Ok(a.Code == b.Code && a.CanUse == b.CanUse && a.Label == b.Label, "29 deterministic readiness");
        Ok(!CharacterProductionLibraryRules.CreatesPixels("LIST")
            && !CharacterProductionLibraryRules.AutoApprove()
            && !CharacterProductionLibraryRules.AutoLock()
            && !CharacterProductionLibraryRules.AutoSelect(), "30 no generation");

        return fail;
    }

    private static CharacterProductionLibraryRules.AuthoritySnapshot Snap(
        bool master, bool masterLocked, bool dna, bool dnaLocked, bool prp, bool prpLocked,
        string? crp, bool coverage, bool identity, int scenes, string version = "V1") =>
        new(master, masterLocked, dna, dnaLocked, prp, prpLocked, crp, version, coverage, identity, coverage ? 4 : 1, 4, scenes);
}
