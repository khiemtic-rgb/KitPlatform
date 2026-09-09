namespace KitPlatform.Packs.Content;

public static class UnifiedVisualContractV1Regression
{
    public const string SuiteId = UnifiedVisualCompilerV1Rules.ContractSuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var universe = FamixaVisualUniverseAuthorityV1Rules.ToDto(
            FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false);
        var snap = VisualUniverseSnapshotV1Rules.FromAuthorities(
            "FAMIXA", universe, ProjectVisualStyleV2Rules.ProtectedV1Sha);
        var contract = UnifiedVisualCompilerV1Rules.Compile(
            snap,
            new UnifiedVisualIdentityLayer("CHAR-099", null, null, "ChronologicalAge: 38. Adult male."),
            new UnifiedVisualSceneLayer("dinner beat", "looks across the table", "quiet", "dining room", "bowl", "medium two-shot"),
            "FRONT");

        Ok(!string.IsNullOrWhiteSpace(contract.ProjectId)
            && !string.IsNullOrWhiteSpace(contract.VisualUniverseId)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.PvsSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.CdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.CompiledPromptSha)
            && !string.IsNullOrWhiteSpace(contract.CompiledPrompt)
            && !string.IsNullOrWhiteSpace(contract.StyleLayer)
            && contract.PromptVersion == UnifiedVisualCompilerV1Rules.PromptVersion,
            "01 required fields exist");
        var mutated = contract with { SceneLanguage = "hack-style" };
        var snapMutated = snap with { VisualUniverseStatus = "LOCKED" };
        Ok(!ReferenceEquals(contract, mutated)
            && contract.SceneLanguage != "hack-style"
            && !ReferenceEquals(snap, snapMutated)
            && snap.VisualUniverseStatus == FamixaVisualUniverseAuthorityV1Rules.StatusDraft,
            "02 contract and snapshot are immutable records");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(contract.StyleLayer)
            && !contract.StyleLayer.Contains("CHAR-099", StringComparison.Ordinal)
            && !FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(
                FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(contract.CompiledPrompt)),
            "03 no character name in style layer");
        Ok(FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(contract.CompiledPrompt)
            && contract.StyleLayer == FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(contract.CompiledPrompt),
            "04 style prefix is first and extractable");
        Ok(contract.IdentityLayer.Contains("CHAR-099", StringComparison.Ordinal)
            && contract.SceneLanguage.Contains("dinner beat", StringComparison.Ordinal)
            && !contract.StyleLayer.Contains("dinner beat", StringComparison.Ordinal),
            "05 style and scene are separate");
        Ok(contract.VisualUniverseSha == snap.VisualUniverseSha
            && contract.PvsSha == snap.PvsSha
            && contract.CdlSha == snap.CdlSha
            && contract.CalibrationPackSha == snap.CalibrationPackSha
            && contract.StylizationLevel == snap.StylizationLevel
            && contract.PhotorealismCeiling == snap.PhotorealismCeiling,
            "06 SHA tuple and ceilings retained from snapshot");
        Ok((contract with { CompiledPrompt = "raw style" }).CompiledPrompt != contract.CompiledPrompt
            && contract.CompiledPrompt.StartsWith("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal),
            "07 compiled prompt is not publicly mutable");

        return fail;
    }
}
