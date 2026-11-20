namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Shared rig and the first two productions. Novixa is the film. Famixa reuses the same rig and engine.
/// </summary>
public static class PerformanceCatalog
{
    public static CharacterRig Human { get; } = new(
        "rig-human", 78, 72, 96, 90, 118, 46);

    public static JointPose Rest { get; } = new(12, 16, 14, 12, 0, 0);

    public static JointPose Anticipate { get; } = new(12, 16, 24, 6, 0.4f, 0);

    public static JointPose Point { get; } = new(12, 16, -42, 58, -0.6f, 0);

    public static JointPose Attention { get; } = new(12, 16, 14, 12, 1.4f, 0);

    public static JointPose WaveUp { get; } = new(12, 16, -40, 24, 0.5f, 0);

    public static JointPose WaveDown { get; } = new(12, 16, -26, 40, 0.2f, 0);

    public static JointPose Reach { get; } = new(12, 16, -50, 28, -0.4f, 0);

    public static ActionClip Ready { get; } = new(
        "act-ready",
        Human.Id,
        [new PoseKey(0, Rest, "rest"), new PoseKey(0.5f, Attention, "rest"), new PoseKey(1, Rest, "rest")]);

    public static ActionClip Explain { get; } = new(
        "act-explain",
        Human.Id,
        [
            new PoseKey(0, Rest, "rest"),
            new PoseKey(0.14f, Anticipate, "point"),
            new PoseKey(0.48f, Point, "point"),
            new PoseKey(1, Point, "point"),
        ]);

    public static ActionClip Return { get; } = new(
        "act-return",
        Human.Id,
        [new PoseKey(0, Point, "point"), new PoseKey(0.72f, Rest, "rest"), new PoseKey(1, Rest, "rest")]);

    public static ActionClip Wave { get; } = new(
        "act-wave",
        Human.Id,
        [
            new PoseKey(0, Rest, "wave"),
            new PoseKey(0.28f, WaveUp, "wave"),
            new PoseKey(0.55f, WaveDown, "wave"),
            new PoseKey(0.78f, WaveUp, "wave"),
            new PoseKey(1, WaveUp, "wave"),
        ]);

    public static ActionClip Settle { get; } = new(
        "act-settle",
        Human.Id,
        [new PoseKey(0, WaveUp, "wave"), new PoseKey(0.7f, Rest, "rest"), new PoseKey(1, Rest, "rest")]);

    public static ActionClip Grasp { get; } = new(
        "act-reach",
        Human.Id,
        [
            new PoseKey(0, Rest, "grasp"),
            new PoseKey(0.16f, Anticipate, "grasp"),
            new PoseKey(0.72f, Reach, "grasp"),
            new PoseKey(1, Reach, "grasp"),
        ]);

    public static ActionClip Walk { get; } = new(
        "act-walk",
        Human.Id,
        [new PoseKey(0, Rest, "rest"), new PoseKey(1, Rest, "rest")]);

    public static ActionClip Hold { get; } = new(
        "act-hold",
        Human.Id,
        [new PoseKey(0, Rest, "rest"), new PoseKey(1, Rest, "rest")]);

    public static ActionClip ReturnReach { get; } = new(
        "act-return-reach",
        Human.Id,
        [new PoseKey(0, Reach, "grasp"), new PoseKey(0.7f, Rest, "rest"), new PoseKey(1, Rest, "rest")]);

    public static IReadOnlyList<FilmBeat> NovixaShort { get; } =
    [
        new(Ready.Id, 0f, 1.5f, "Lan đứng sau quầy."),
        new(Explain.Id, 1.5f, 4.1f, "Nhìn kìa, cái lọ trên quầy!"),
        new(Wave.Id, 5.6f, 4.2f, "Chào bạn!"),
        new(Walk.Id, 9.8f, 1.8f, "Cô đi tới quầy."),
        new(Grasp.Id, 11.6f, 3.0f, "Mình với tới rồi."),
        new(Hold.Id, 14.6f, 0.4f, "Lan đứng sau quầy."),
    ];

    public static ShotTimeline NovixaCounter { get; } = new(
        "film-novixa-counter",
        "novixa",
        "pharmacy-counter",
        "plate-pharmacy.jpg",
        860,
        650,
        new TimelineCue(Explain.Id, 0, 1));

    public static ShotTimeline FamixaDoorway { get; } = new(
        "film-famixa-wave",
        "famixa",
        "doorway",
        "plate-home.jpg",
        460,
        650,
        new TimelineCue(Wave.Id, 0, 1));

    public static CastLook LookOf(VisualBrandStyle brand) => new(
        brand.Skin, brand.Ink, brand.Accent, brand.Ink);

    public static PerformanceRequest Request(ShotTimeline timeline, ActionClip clip, float timeSeconds) 
    {
        var brand = BrandSeed.All.First(b => b.Code == timeline.BrandCode);
        return new PerformanceRequest(Human, clip, timeline, LookOf(brand), timeSeconds, CharacterCatalog.Lan);
    }

    public static ActionClip FindClip(string clipId) =>
        clipId == Wave.Id ? Wave
        : clipId == Explain.Id ? Explain
        : clipId == Ready.Id ? Ready
        :         clipId == Return.Id ? Return
        : clipId == Settle.Id ? Settle
        : clipId == Grasp.Id ? Grasp
        : clipId == ReturnReach.Id ? ReturnReach
        : clipId == Walk.Id ? Walk
        : clipId == Hold.Id ? Hold
        : throw new InvalidOperationException("Không có clip " + clipId);

    public static ShotTimeline FindTimeline(string brandCode) =>
        brandCode == FamixaDoorway.BrandCode ? FamixaDoorway
        : brandCode == NovixaCounter.BrandCode ? NovixaCounter
        : throw new InvalidOperationException("Không có timeline cho " + brandCode);
}
