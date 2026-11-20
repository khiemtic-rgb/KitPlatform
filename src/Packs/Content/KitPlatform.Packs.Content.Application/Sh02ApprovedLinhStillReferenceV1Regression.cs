namespace KitPlatform.Packs.Content;

public static class Sh02ApprovedLinhStillReferenceV1Regression
{
    public const string SuiteId = Sh02ApprovedLinhStillReferenceV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var mode = ProjectVisualModeAuthorityV1Rules.FamixaCurrent();
        var linh = new ContentSeriesStillRefDto(
            "Linh",
            "data:image/png;base64,AAAA",
            Role: "identity",
            AuthorityStatus: "LOCKED",
            CharacterId: Sh02ApprovedLinhStillReferenceV1.LinhCharacterId,
            ReferenceRole: ProjectVisualModeAuthorityV1Rules.RoleIdentityAnchor);
        var minh = new ContentSeriesStillRefDto(
            "Minh",
            "data:image/jpeg;base64,AAAA",
            Role: "identity",
            CharacterId: Sh02ApprovedLinhStillReferenceV1.MinhCharacterId);

        var approvedLinh = Sh02ApprovedLinhStillReferenceV1.Classify(
            mode, linh, Sh02ApprovedLinhStillReferenceV1.LinhSha256, approvedScope: true);
        var approvedMinh = Sh02ApprovedLinhStillReferenceV1.Classify(
            mode, minh, Sh02ApprovedLinhStillReferenceV1.MinhMasterSha256, approvedScope: true);
        Ok(approvedLinh.AuthorityStatus == Sh02ApprovedLinhStillReferenceV1.Authority, "linh authority is one still");
        Ok(approvedLinh.ReferenceRole == ProjectVisualModeAuthorityV1Rules.RoleScene, "linh is not an identity anchor");
        Ok(approvedLinh.Source == Sh02ApprovedLinhStillReferenceV1.Source, "linh source is the approved still");
        Ok(!string.Equals(approvedLinh.AuthorityStatus, "LOCKED", StringComparison.Ordinal), "client LOCKED is not kept");
        Ok(approvedMinh.AuthorityStatus == "LOCKED"
            && approvedMinh.ArtifactSha256 == Sh02ApprovedLinhStillReferenceV1.MinhMasterSha256, "minh bytes match the locked master");
        Ok(Sh02ApprovedLinhStillReferenceV1.HasRequiredPair([approvedLinh, approvedMinh]), "approved pair");

        var preflight = ProjectVisualModeAuthorityV1Rules.Preflight(
            new VisualGenerationPreflightInput(
                mode,
                mode.VisualUniverse,
                mode.VisualMode,
                true,
                [approvedLinh, approvedMinh],
                false,
                mode.VisualMode,
                mode.VisualMode));
        Ok(preflight.Allowed, "approved references pass visual preflight");

        var threwScope = false;
        try
        {
            Sh02ApprovedLinhStillReferenceV1.Classify(
                mode, linh, Sh02ApprovedLinhStillReferenceV1.LinhSha256, approvedScope: false);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SH02_LINH_REFERENCE_OUT_OF_SCOPE")
        {
            threwScope = true;
        }
        Ok(threwScope, "linh file is refused outside the approved shot");

        var threwOtherShot = false;
        try
        {
            Ok(!Sh02ApprovedLinhStillReferenceV1.IsApprovedScope(
                Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"),
                Sh02ApprovedLinhStillReferenceV1.ShotCode), "official build is outside the allowance");
            Sh02ApprovedLinhStillReferenceV1.Classify(
                mode, linh, Sh02ApprovedLinhStillReferenceV1.LinhSha256, approvedScope: false);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SH02_LINH_REFERENCE_OUT_OF_SCOPE")
        {
            threwOtherShot = true;
        }
        Ok(threwOtherShot, "same shot on another build is refused");

        var threwHash = false;
        try
        {
            Sh02ApprovedLinhStillReferenceV1.Classify(
                mode, linh, new string('a', 64), approvedScope: true);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SH02_LINH_REFERENCE_HASH_MISMATCH")
        {
            threwHash = true;
        }
        Ok(threwHash, "another linh image is refused");

        var threwCharacter = false;
        try
        {
            Sh02ApprovedLinhStillReferenceV1.Classify(
                mode, minh, Sh02ApprovedLinhStillReferenceV1.LinhSha256, approvedScope: true);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SH02_LINH_REFERENCE_CHARACTER")
        {
            threwCharacter = true;
        }
        Ok(threwCharacter, "linh bytes cannot be labeled as minh");

        var threwMinh = false;
        try
        {
            Sh02ApprovedLinhStillReferenceV1.Classify(
                mode, minh, new string('b', 64), approvedScope: true);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SH02_MINH_MASTER_HASH_MISMATCH")
        {
            threwMinh = true;
        }
        Ok(threwMinh, "a different minh image is refused");
        Ok(!Sh02ApprovedLinhStillReferenceV1.HasRequiredPair([approvedLinh]), "linh alone is not the approved pair");
        Ok(Sh02ApprovedLinhStillReferenceV1.ModelId == "gemini-3.1-flash-lite-image", "one live image model");
        return fail;
    }
}
