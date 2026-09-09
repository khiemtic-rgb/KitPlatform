namespace KitPlatform.Packs.Content;

public static class VisualUniverseSnapshotResolverV1Regression
{
    public const string SuiteId = VisualUniverseSnapshotV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var universe = FamixaVisualUniverseAuthorityV1Rules.ToDto(
            FamixaVisualUniverseAuthorityV1Rules.StatusDraft, currentAuthority: false);
        var snap = VisualUniverseSnapshotV1Rules.FromAuthorities(
            "FAMIXA", universe, ProjectVisualStyleV2Rules.ProtectedV1Sha);

        Ok(snap.VisualUniverseSha == FamixaVisualUniverseAuthorityV1Rules.Sha()
            && snap.VisualUniverseSha == "4e9c4bad9ee0d241d1896846a574828865dcdd9e96e18751c251b3c77ea76726",
            "01 current VUA SHA");
        Ok(snap.PvsSha == ProjectVisualStyleV2Rules.ProtectedV1Sha
            && snap.PvsSha == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58",
            "02 current PVS V1 SHA");
        Ok(snap.CdlSha == CharacterDesignLanguageV2Rules.Sha()
            && snap.CdlSha == "683ce6bd64b1588c38db325cf4ce724d4f66be48b581b7f044ab25a065c88153",
            "03 current CDL V2 SHA");
        Ok(snap.VisualUniverseStatus == FamixaVisualUniverseAuthorityV1Rules.StatusDraft
            && !snap.VuaIsCurrentAuthority
            && snap.PvsIsCurrentAuthority
            && FamixaVisualUniverseAuthorityV1Rules.CurrentPvsRemainsAuthority(snap.VisualUniverseStatus),
            "04 DRAFT VUA — PVS remains current authority");
        Ok(!VisualUniverseSnapshotV1Rules.PromotesVua()
            && !VisualUniverseSnapshotV1Rules.LocksVua()
            && !VisualUniverseSnapshotV1Rules.MutatesRulesJson()
            && !VisualUniverseSnapshotV1Rules.MutatesCharacters()
            && !VisualUniverseSnapshotV1Rules.CallsGemini()
            && !VisualUniverseSnapshotV1Rules.CreatesPixels(),
            "05 does not promote / mutate / generate");
        Ok(snap.SnapshotVersion == VisualUniverseSnapshotV1Rules.SnapshotVersion
            && snap.VisualUniverseId == FamixaVisualUniverseAuthorityV1Rules.AuthorityId
            && snap.StylizationLevel == FamixaVisualUniverseAuthorityV1Rules.StylizationLevel
            && snap.PhotorealismCeiling == FamixaVisualUniverseAuthorityV1Rules.PhotorealismCeiling
            && snap.CalibrationPackSha is null
            && snap.CalibrationPackId is null,
            "06 snapshot fields from existing authorities");
        Ok(VisualUniverseSnapshotV1Rules.CurrentPvsSha(ProjectVisualStyleV2Rules.Sha(), FamixaVisualUniverseAuthorityV1Rules.StatusDraft)
                == ProjectVisualStyleV2Rules.ProtectedV1Sha
            && !VisualUniverseSnapshotV1Rules.UsesPvsV2AsLiveAuthority(),
            "07 PVS V2 is not live authority");

        var locked = VisualUniverseSnapshotV1Rules.FromAuthorities(
            "FAMIXA",
            FamixaVisualUniverseAuthorityV1Rules.ToDto(FamixaVisualUniverseAuthorityV1Rules.StatusLocked, true),
            ProjectVisualStyleV2Rules.ProtectedV1Sha);
        Ok(locked.VuaIsCurrentAuthority && !locked.PvsIsCurrentAuthority,
            "08 LOCKED VUA would become current authority — mapping only");

        var persisted = VisualUniverseSnapshotV1Rules.FromAuthorities(
            "FAMIXA", universe, ProjectVisualStyleV2Rules.ProtectedV1Sha,
            VisualCalibrationPackV1Rules.DefaultPackId,
            VisualCalibrationPackV1Rules.PackSha(VisualCalibrationPackV1Rules.CompilePack()));
        Ok(persisted.CalibrationPackId == VisualCalibrationPackV1Rules.DefaultPackId
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(persisted.CalibrationPackSha),
            "09 calibration pack binding when persisted");

        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "10 Minh protected SHAs unchanged");

        return fail;
    }
}
