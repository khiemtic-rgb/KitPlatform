namespace KitPlatform.Packs.Content;

public static class ProjectVisualModeAuthorityV1Regression
{
    public const string SuiteId = ProjectVisualModeAuthorityV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pvsBefore = ProjectVisualStyleV2Rules.ProtectedV1Sha;
        var vuaBefore = FamixaVisualUniverseAuthorityV1Rules.Sha();
        var cdlBefore = CharacterDesignLanguageV2Rules.Sha();

        Ok(ProjectVisualModeAuthorityV1Rules.RequiresVisualMode()
            && ProjectVisualModeAuthorityV1Rules.ValidateMode(null) == ProjectVisualModeAuthorityV1Rules.GateMissing
            && ProjectVisualModeAuthorityV1Rules.ValidateMode("3D_STYLIZED_REALISM") is null,
            "VM-01 Project requires VisualMode");

        var famixa = ProjectVisualModeAuthorityV1Rules.FamixaCurrent();
        Ok(famixa.VisualMode == ProjectVisualModeAuthorityV1Rules.Mode3dStylized
            && ProjectVisualModeAuthorityV1Rules.Inherit(famixa.VisualMode, "3D_STYLIZED_REALISM") is null
            && ProjectVisualModeAuthorityV1Rules.CharacterMayChooseMode() == false,
            "VM-02 3D project propagates to Character");

        Ok(ProjectVisualModeAuthorityV1Rules.CalibrationMayChooseMode() == false
            && ProjectVisualModeAuthorityV1Rules.Inherit(famixa.VisualMode, "3D_STYLIZED_REALISM") is null
            && ProjectVisualModeAuthorityV1Rules.Inherit(famixa.VisualMode, "PHOTOREALISTIC")
                == ProjectVisualModeAuthorityV1Rules.GateConflict,
            "VM-03 3D project propagates to Calibration");

        var studio = ProjectVisualModeAuthorityV1Rules.LockedStudioAnchor("CHAR-001", "Minh", "ab");
        var scene = ProjectVisualModeAuthorityV1Rules.ResolveSceneReferences(
            famixa.VisualMode, [studio]);
        Ok(scene.Allowed && scene.Attachable.Count == 1
            && ProjectVisualModeAuthorityV1Rules.SceneMayChooseMode() == false,
            "VM-04 3D project propagates to Scene");

        Ok(ProjectVisualModeAuthorityV1Rules.VideoMayChooseMode() == false
            && famixa.VideoRenderingMode == ProjectVisualModeAuthorityV1Rules.VideoRender3d
            && ProjectVisualModeAuthorityV1Rules.Inherit(famixa.VisualMode, "3D_STYLIZED_REALISM") is null,
            "VM-05 3D project propagates to Video");

        var photoreal = ProjectVisualModeAuthorityV1Rules.LockedStudioAnchor("CHAR-001", "Minh")
            with { VisualMode = ProjectVisualModeAuthorityV1Rules.ModePhotoreal };
        var rejectPhoto = ProjectVisualModeAuthorityV1Rules.ClassifyReference(photoreal, famixa.VisualMode);
        Ok(!rejectPhoto.Eligible && rejectPhoto.Code == ProjectVisualModeAuthorityV1Rules.GateRefIneligible,
            "VM-06 Photoreal reference rejected by 3D project");

        var legacy = ProjectVisualModeAuthorityV1Rules.LegacyPhotorealCanon("CHAR-001", "Minh");
        var rejectLegacy = ProjectVisualModeAuthorityV1Rules.ClassifyReference(legacy, famixa.VisualMode);
        Ok(!rejectLegacy.Eligible
            && rejectLegacy.ReferenceStatus == ProjectVisualModeAuthorityV1Rules.StatusIneligibleForProject,
            "VM-07 Legacy photoreal reference rejected");

        Ok(ProjectVisualModeAuthorityV1Rules.ClassifyReference(studio, famixa.VisualMode).Eligible,
            "VM-08 Locked Character Studio reference accepted");

        var blocked = ProjectVisualModeAuthorityV1Rules.ResolveSceneReferences(
            famixa.VisualMode, [legacy, photoreal]);
        Ok(!blocked.Allowed
            && blocked.GeminiCalled == false
            && blocked.Code == ProjectVisualModeAuthorityV1Rules.GateLegacy,
            "VM-09 Visual Mode conflict blocks provider");

        Ok(ProjectVisualModeAuthorityV1Rules.CallsGemini() == false
            && ProjectVisualModeAuthorityV1Rules.ProviderDecidesMode() == false
            && blocked.Generation == false,
            "VM-10 Visual Mode conflict blocks Gemini");

        Ok(ProjectVisualModeAuthorityV1Rules.SilentFallback() == false
            && ProjectVisualModeAuthorityV1Rules.AutoSelectLegacyCanon() == false
            && blocked.Attachable.Count == 0,
            "VM-11 No silent fallback");

        Ok(ProjectVisualModeAuthorityV1Rules.MutatesAuthorities() == false
            && ProjectVisualModeAuthorityV1Rules.MayChangeModeDirectly() == false
            && ProjectVisualModeAuthorityV1Rules.AutoApprove() == false
            && ProjectVisualModeAuthorityV1Rules.AutoLock() == false,
            "VM-12 No authority mutation");

        Ok(ProjectVisualModeAuthorityV1Rules.RequiresMigration() == false,
            "VM-13 No database migration unless absolutely required");

        Ok(ProjectVisualStyleV2Rules.ProtectedV1Sha == pvsBefore
            && FamixaVisualUniverseAuthorityV1Rules.Sha() == vuaBefore
            && CharacterDesignLanguageV2Rules.Sha() == cdlBefore
            && ProjectVisualModeAuthorityV1Rules.AuthoritiesUnchanged(pvsBefore, vuaBefore, cdlBefore),
            "VM-14 Existing PVS/VUA/CDL SHA unchanged");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && pvsBefore == ProjectVisualStyleV2Rules.ProtectedV1Sha,
            "VM-15 Existing Character Master SHA unchanged");

        var scene01 = ProjectVisualModeAuthorityV1Rules.ClassifyExistingScene(
            "SH01-01", [legacy], famixa.VisualMode);
        Ok(scene01 == ProjectVisualModeAuthorityV1Rules.StatusInvalidPipeline,
            "VM-16 Scene 01 preflight rejects old photoreal reference");

        var scene01Studio = ProjectVisualModeAuthorityV1Rules.Preflight(
            new VisualGenerationPreflightInput(
                famixa,
                famixa.VisualUniverse,
                famixa.VisualMode,
                true,
                [studio],
                false,
                famixa.VisualMode,
                famixa.VisualMode));
        Ok(scene01Studio.Allowed
            && scene01Studio.GeminiCalled == false
            && scene01Studio.Generation == false
            && scene01Studio.ProviderCalled == false,
            "VM-17 Scene 01 accepts new Character Studio reference");

        Ok(ProjectVisualModeAuthorityV1Rules.CreatesPixels() == false
            && ProjectVisualModeAuthorityV1Rules.CallsGemini() == false,
            "VM-18 no pixels / no Gemini");

        return fail;
    }
}
