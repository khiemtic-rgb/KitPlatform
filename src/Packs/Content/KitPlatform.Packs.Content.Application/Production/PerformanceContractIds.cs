namespace KitPlatform.Packs.Content.Production;

public static class PerformanceContractIds
{
    public const string DocumentId = "FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1";
    public const string CanonId = "PERFORMANCE_CANON_V1";
    public const string HashPrefix = "PERFORMANCE_HASH_V1:";
    public const string LineTextPrefix = "LINE_TEXT_V1:";
    public const string DraftPrefix = "PERFORMANCE_DRAFT_V1:";
    public const string TimingDocumentId = "FAMIXA_SHOT_TIMING_CONTRACT_V3";
    public const int SchemaVersion = 1;
    public const string SuiteId = "FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1_PHASE_A";

    public const string InFrame = "IN_FRAME";
    public const string OffFrame = "OFF_FRAME";
    public const string VoiceOnly = "VOICE_ONLY";

    public const string Primary = "PRIMARY";
    public const string Secondary = "SECONDARY";
    public const string RoleNone = "NONE";

    public const string Speaker = "SPEAKER";
    public const string Listener = "LISTENER";

    public const string SpokenLineUnlocked = "SPOKEN_LINE_UNLOCKED";
    public const string InvalidCharacter = "INVALID_CHARACTER";
    public const string DuplicatePrimary = "DUPLICATE_PRIMARY";
    public const string LineCharacterMismatch = "LINE_CHARACTER_MISMATCH";
    public const string OfficialFilmForbidden = "OFFICIAL_FILM_FORBIDDEN";
    public const string CrossShotForbidden = "CROSS_SHOT_FORBIDDEN";
    public const string CrossScopeForbidden = "CROSS_SCOPE_FORBIDDEN";
    public const string ProviderFieldForbidden = "PROVIDER_FIELD_FORBIDDEN";
    public const string VoiceIdForbidden = "VOICE_ID_FORBIDDEN";
    public const string ProviderDurationForbidden = "PROVIDER_DURATION_FORBIDDEN";
    public const string InvalidPerformanceHash = "INVALID_PERFORMANCE_HASH";
    public const string UnresolvedProjection = "UNRESOLVED_PROJECTION";
    public const string EmptySpokenText = "EMPTY_SPOKEN_TEXT";
    public const string MissingScope = "MISSING_SCOPE";
    public const string SnapshotNotFound = "SNAPSHOT_NOT_FOUND";
    public const string SnapshotImmutable = "SNAPSHOT_IMMUTABLE";
}

public static class PerformanceEmotions
{
    public static readonly IReadOnlyList<string> All =
    [
        "neutral", "uneasy", "tense", "annoyed", "burst", "silent", "hurt", "soft", "aftertaste",
    ];

    public static bool IsKnown(string? emotion) =>
        All.Contains((emotion ?? "").Trim(), StringComparer.Ordinal);

    public static string LabelOf(string emotion, int intensity)
    {
        var vi = emotion switch
        {
            "uneasy" => "mong manh",
            "tense" => "căng",
            "annoyed" => "bực",
            "burst" => "bùng",
            "silent" => "im",
            "hurt" => "đau",
            "soft" => "dịu",
            "aftertaste" => "dư âm",
            _ => "bình thường",
        };
        return $"{vi} {intensity}/5";
    }
}
