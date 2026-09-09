namespace KitPlatform.Packs.Content;

public static class IdentityConditionedCalibrationLiveV1Regression
{
    public const string SuiteId = IdentityConditionedCalibrationLiveV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var ready = IdentityConditionedCalibrationLiveV1Rules.Readiness(pack);
        var isolated = IdentityConditionedCalibrationLiveV1Rules.Isolate(pack);
        var historical = StripIdentity(VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24));
        var isolatedHistorical = IdentityConditionedCalibrationLiveV1Rules.Isolate(historical);
        var complete = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);

        Ok(ready.Status == IdentityConditionedCalibrationLiveV1Rules.ReadyForDirectorConfirmation
            && ready.GateCode == VisualCalibrationPackV1Rules.GateConfirmation
            && !ready.GeminiCalled
            && !ready.ProviderCalled
            && !ready.GenerationExecuted
            && ready.PixelCreated == 0, "LIVE-01 confirmation boundary; no Gemini");
        Ok(!string.IsNullOrWhiteSpace(ready.CalibrationRunId)
            && ready.CalibrationRunId!.StartsWith("CAL-RUN-", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(isolated.CalibrationRunId), "LIVE-02 CalibrationRunId allocated in memory");
        Ok(ready.Plan.Count == 24
            && ready.Plan.Count(r => IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType)) == 6
            && ready.Plan.Count(r => !IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType)) == 18
            && ready.Logs.Count == 24, "LIVE-03 6 FRONT + 18 conditioned planned");
        Ok(ready.Plan.Where(r => IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType))
                .All(r => r.ReferenceCount == 0 && string.IsNullOrWhiteSpace(r.ReferenceRole))
            && ready.Plan.Where(r => !IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType))
                .All(r => r.ReferenceCount == 1
                    && r.ReferenceRole == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor),
            "LIVE-04 FRONT refs=[] ; downstream refs=[FRONT]");
        Ok(ready.FrontFirstEnforced
            && ready.DownstreamRequiresFront
            && ready.HistoricalOverwriteForbidden
            && ready.UnifiedCompilerBound, "LIVE-05 FRONT-first + unified compiler + no overwrite");
        Ok(ready.AutoApproveForbidden
            && ready.AutoLockForbidden
            && ready.VuaPromotionForbidden
            && !IdentityConditionedCalibrationLiveV1Rules.AutoApprove()
            && !IdentityConditionedCalibrationLiveV1Rules.AutoLock(), "LIVE-06 no auto approve/lock/promote");

        var dryMock = new MockVisualCalibrationGenerationProvider();
        var dry = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, dryMock, false, true).GetAwaiter().GetResult();
        Ok(dry.GateCode == VisualCalibrationPackV1Rules.GateConfirmation
            && dryMock.CallCount == 0
            && !dry.ProviderCalled
            && VisualCalibrationPackV1Rules.GetCoverage(dry.Pack).Valid == 0, "LIVE-07 confirm=false never calls provider");

        Ok(IdentityConditionedCalibrationLiveV1Rules.HasIndependentHistoricalPixels(historical)
            && IdentityConditionedCalibrationV1Rules.GetCoverage(historical).IdentityConditionedValid == 0
            && VisualCalibrationPackV1Rules.EvaluateLiveGenerate(historical, true) is null, "LIVE-08 historical 24 does not count as identity complete");
        Ok(isolatedHistorical.CalibrationRunId != historical.CalibrationRunId
            && isolatedHistorical.HistoricalPixelCount == 24
            && VisualCalibrationPackV1Rules.GetCoverage(isolatedHistorical).Valid == 0
            && isolatedHistorical.Subjects.SelectMany(s => s.Artifacts)
                .All(a => !string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)),
            "LIVE-09 isolate forks a new run and leaves historical pixels out of coverage");
        Ok(IdentityConditionedCalibrationLiveV1Rules.ArtifactFileName(
                isolated.CalibrationRunId!, "CAL-003", "FRONT")
            .Contains(isolated.CalibrationRunId!, StringComparison.Ordinal)
            && IdentityConditionedCalibrationLiveV1Rules.ArtifactRelativeDir(pack.PackId, isolated.CalibrationRunId!)
                .Contains(isolated.CalibrationRunId!, StringComparison.Ordinal),
            "LIVE-10 artifact names are run-scoped and unique");
        Ok(VisualCalibrationPackV1Rules.EvaluateLiveGenerate(complete, true)
                == VisualCalibrationPackV1Rules.GateDuplicate
            && IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(complete),
            "LIVE-11 completed identity-conditioned pack is BLOCK_DUPLICATE");
        Ok(!IdentityConditionedCalibrationLiveV1Rules.SlotReusable(pack, "CAL-001/FRONT")
            && IdentityConditionedCalibrationLiveV1Rules.SlotReusable(
                complete, VisualCalibrationPackV1Rules.SlotIdOf("CAL-001", "FRONT")),
            "LIVE-12 only this-run identity pixels are reusable");

        var failFront = new MockVisualCalibrationGenerationProvider();
        failFront.FailSlots.Add(VisualCalibrationPackV1Rules.SlotIdOf("CAL-001", "FRONT"));
        var failed = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, failFront, true, true).GetAwaiter().GetResult();
        var cal1 = failed.Pack.Subjects.First(s => s.CalibrationSubjectId == "CAL-001");
        Ok(cal1.Status == IdentityConditionedCalibrationLiveV1Rules.SubjectAnchorFailed
            && cal1.Artifacts.Where(a => a.ViewType != "FRONT")
                .All(a => a.GenerationStatus == IdentityConditionedCalibrationV1Rules.GateAnchor)
            && failFront.CallCount == 21, "LIVE-13 FRONT fail blocks downstream and marks IDENTITY_ANCHOR_FAILED");

        Ok(!IdentityConditionedCalibrationLiveV1Rules.CallsGemini()
            && !IdentityConditionedCalibrationLiveV1Rules.CreatesPixels()
            && !IdentityConditionedCalibrationLiveV1Rules.MutatesCharacters()
            && !IdentityConditionedCalibrationLiveV1Rules.MutatesAuthority()
            && !IdentityConditionedCalibrationLiveV1Rules.RequiresMigration()
            && CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha),
            "LIVE-14 no Gemini / mutation / migration; Minh SHA protected");
        Ok(ready.Review.VisualPass == false
            && ready.Review.PixelValid == 0
            && ready.Review.FrontValid == 0
            && ready.Review.ConditionedValid == 0, "LIVE-15 review matrix exists and is not auto PASS");
        Ok(ready.Logs.All(l =>
                !string.IsNullOrWhiteSpace(l.SubjectId)
                && !string.IsNullOrWhiteSpace(l.CompiledPromptSha)
                && !l.CompiledPromptSha.Contains("AIza", StringComparison.OrdinalIgnoreCase)
                && !l.CompiledPromptSha.Contains("key", StringComparison.OrdinalIgnoreCase)),
            "LIVE-16 request logs exist and do not contain secrets");

        return fail;
    }

    private static VisualCalibrationPackV1Rules.PackSnapshot StripIdentity(
        VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack with
        {
            Subjects = pack.Subjects.Select(s => s with
            {
                Artifacts = s.Artifacts.Select(a => a with
                {
                    IdentityAnchorSlot = null,
                    IdentityAnchorSha256 = null,
                    ReferenceRole = null,
                }).ToList(),
            }).ToList(),
        };
}
