namespace KitPlatform.Packs.Content;

public static class FamixaExecutionProvenanceV1Regression
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var decision = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Runway,
            DurationSec: 5));
        var snap = FamixaExecutionProvenanceRules.Snapshot(decision);
        Ok(snap.DecisionId == decision.DecisionId && snap.ProviderId == FamixaProviderIds.Runway, "T1 snapshot from decision");

        var attemptId = "mot:EP01-SC01-SH01:1";
        var prov = FamixaExecutionProvenanceRules.Bind(
            attemptId,
            FamixaProviderCapability.Motion,
            decision,
            shotId: "EP01-SC01-SH01",
            pictureRevisionId: "picture:EP01-SC01-SH01:006",
            picturePixelHash: "pix-a",
            motionContentFingerprint: "what-a",
            actingBeatFingerprint: "act-a",
            timingHash: "time-a",
            providerRequestId: "runway-task-1",
            artifactId: "mot:EP01-SC01-SH01:1");
        Ok(prov.DecisionId == decision.DecisionId, "T2 DecisionId survives bind");
        Ok(prov.ProviderId == FamixaProviderIds.Runway, "T3 ProviderId survives");
        Ok(prov.ModelId == decision.ModelId, "T4 ModelId survives");
        Ok(prov.AttemptId == attemptId, "T5 AttemptId preserved");
        Ok(prov.ProviderRequestId == "runway-task-1", "T6 vendor request persisted");
        Ok(prov.Class == FamixaProvenanceClass.Verified, "T1 verified when decision+attempt+provider");

        var noReq = FamixaExecutionProvenanceRules.Bind(
            "mot:EP01-SC01-SH01:2",
            FamixaProviderCapability.Picture,
            FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Picture)));
        Ok(noReq.ProviderRequestId is null, "T7 missing request id stays null");
        Ok(FamixaExecutionProvenanceRules.VendorRequestId("") is null
           && FamixaExecutionProvenanceRules.VendorRequestId("  ") is null, "T19 empty is not invented");

        Ok(prov.ProviderTaskId == "runway-task-1" && prov.AttemptId != prov.ProviderRequestId, "T8 task id ≠ local attempt id");

        var lineage = FamixaExecutionProvenanceRules.Reconstruct(prov);
        Ok(lineage.Decision?.DecisionId == decision.DecisionId
           && lineage.HowProviderId == FamixaProviderIds.Runway
           && lineage.WhatFingerprint == "what-a"
           && lineage.Provenance.ArtifactId == "mot:EP01-SC01-SH01:1", "T9–T11 artifact→attempt→decision→HOW→WHAT");

        var wanDecision = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Wan,
            DurationSec: 5));
        var wanProv = FamixaExecutionProvenanceRules.Bind(
            "mot:EP01-SC01-SH01:2",
            FamixaProviderCapability.Motion,
            wanDecision,
            shotId: "EP01-SC01-SH01",
            pictureRevisionId: "picture:EP01-SC01-SH01:006",
            motionContentFingerprint: "what-a",
            providerRequestId: "wan_abc");
        Ok(FamixaExecutionProvenanceRules.SameWhat(prov, wanProv)
           && !FamixaExecutionProvenanceRules.SameHow(prov, wanProv)
           && wanProv.AttemptId != prov.AttemptId
           && wanProv.DecisionId != prov.DecisionId, "T12 switch same WHAT new HOW/attempt/decision");

        var retry = FamixaExecutionProvenanceRules.Bind(
            "mot:EP01-SC01-SH01:3",
            FamixaProviderCapability.Motion,
            FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
                FamixaProviderCapability.Motion,
                ExplicitProviderId: FamixaProviderIds.Runway,
                DurationSec: 5)),
            pictureRevisionId: "picture:EP01-SC01-SH01:006",
            motionContentFingerprint: "what-a",
            providerRequestId: "runway-task-2");
        Ok(retry.AttemptId != attemptId && FamixaExecutionProvenanceRules.SameWhat(prov, retry), "T14–T15 retry new attempt same WHAT");

        var regen = FamixaExecutionProvenanceRules.Bind(
            "mot:EP01-SC01-SH01:4",
            FamixaProviderCapability.Motion,
            decision,
            pictureRevisionId: "picture:EP01-SC01-SH01:007",
            motionContentFingerprint: "what-b");
        Ok(!FamixaExecutionProvenanceRules.SameWhat(prov, regen), "T17 regenerate new WHAT");

        var legacy = FamixaExecutionProvenanceRules.Bind("mot:old:1", FamixaProviderCapability.Motion);
        Ok(legacy.Class == FamixaProvenanceClass.LegacyUnverified && legacy.DecisionId is null, "T18 LEGACY_UNVERIFIED");

        Ok(prov.Class != FamixaProvenanceClass.LegacyUnverified && decision.EstimatedCost == 25, "T20 cost snapshot from decision");
        Ok(wanDecision.EstimatedCost is null && wanDecision.CostKind == FamixaCostKind.FalBilledEstimate, "T21 Wan cost null");

        var lip = FamixaExecutionProvenanceRules.Bind(
            "lip:SH01:1",
            FamixaProviderCapability.LipSync,
            FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.LipSync)),
            providerRequestId: "lipsync_fake");
        Ok(lip.ProviderId == FamixaProviderIds.Fal && lip.ProviderRequestId == "lipsync_fake", "T22 LipSync provenance");

        var voice = FamixaExecutionProvenanceRules.Bind(
            "voi:SH01:1",
            FamixaProviderCapability.Voice,
            FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Voice)));
        Ok(voice.ProviderId == FamixaProviderIds.ElevenLabs && voice.ProviderRequestId is null, "T23 Voice request id null if vendor omitted");

        var pic = FamixaExecutionProvenanceRules.Bind(
            "pic:SH01:1",
            FamixaProviderCapability.Picture,
            FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Picture)),
            pictureRevisionId: "picture:SH01:006",
            picturePixelHash: "pix-a");
        Ok(pic.ProviderId == FamixaProviderIds.Gemini && pic.PicturePixelHash == "pix-a", "T24 Picture provenance");

        var dto = FamixaExecutionProvenanceRules.StampTurbo(
            new ContentSeriesTurboTaskDto("runway-task-1", "PENDING", null, null, false, "gen4_turbo", 5),
            decision);
        Ok(dto.DecisionId == decision.DecisionId && dto.ProviderId == FamixaProviderIds.Runway, "Start DTO keeps DecisionId");

        var voiceDecision = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Voice));
        var voiceDto = new ContentSeriesTtsPreviewDto(Array.Empty<byte>(), voiceDecision.DecisionId, voiceDecision.ProviderId, voiceDecision.ModelId, null);
        Ok(voiceDto.DecisionId == voiceDecision.DecisionId && voiceDto.ProviderRequestId is null, "Voice preview keeps DecisionId and null request id");

        return fail;
    }
}
