namespace KitPlatform.Packs.Content.Production;

/// <summary>TEST-only PerformanceSnapshot seed. Not EP01/EP99.</summary>
public static class ProductionTestPerformanceFixture
{
    public const string LineId = "L1";
    public const string CharacterId = "CHAR-001";
    public const string LineText = "Thì sao?";
    public const string VoiceId = "VOICE-TEST";

    public static PerformanceProjectionSource Source(Guid buildId, string shotCode, string? action = null) => new(
        buildId,
        shotCode,
        "TEST",
        "SC01",
        [CharacterId],
        [LineId],
        [
            new LineProjectionSource(LineId, CharacterId, LineText, "tense", 3, "fast", "mid", 0.12, PerformanceEmotions.LabelOf("tense", 3), Locked: true),
        ],
        new ActingBeatSource(action ?? "turns", "stands", null, "window", "kitchen", "uneasy", 2, null, "down"));

    public static async Task<PerformanceSnapshot> FreezeAsync(
        IPerformanceSnapshotStore store,
        Guid buildId,
        string shotCode,
        string? action = null,
        string createdBy = "regression")
    {
        var svc = new PerformanceContractService(store);
        return await svc.FreezeAsync(svc.CreateDraft(Source(buildId, shotCode, action)), createdBy);
    }

    public static ShotExecutionInput Attach(ShotExecutionInput input, PerformanceSnapshot snap) =>
        input with
        {
            PerformanceSnapshotId = snap.PerformanceSnapshotId,
            PerformanceHash = snap.PerformanceHash,
            PerformancePayloadJson = snap.PayloadJson,
        };

    public static CreateVideoReadinessClaim Attach(CreateVideoReadinessClaim claim, PerformanceSnapshot snap) =>
        claim with
        {
            PerformanceSnapshotId = snap.PerformanceSnapshotId,
            PerformanceHash = snap.PerformanceHash,
            LineId = LineId,
            CharacterId = CharacterId,
            LineText = LineText,
        };

    public static ShotExecutionInput VoiceInput(ShotExecutionInput basis, PerformanceSnapshot snap, string voiceId = VoiceId) =>
        Attach(basis, snap) with
        {
            LineId = LineId,
            SpeakerCharacterId = CharacterId,
            Story = LineText,
            VoiceId = voiceId,
        };
}
