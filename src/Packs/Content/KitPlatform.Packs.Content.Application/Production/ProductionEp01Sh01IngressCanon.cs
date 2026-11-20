namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// EP01 SH01 Production Ingress V1 — exact legacy sources. Do not invent.
/// </summary>
public static class ProductionEp01Sh01IngressCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_EP01_SH01_PRODUCTION_INGRESS_V1";
    public const string BuildIdText = "4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f";
    public static readonly Guid BuildId = ProductionScopeAnchorRules.OfficialEp01;
    public const string Episode = "EP01";
    public const string Scene = "SC01";
    public const string Shot = "EP01-SC01-SH01";
    public const string PictureRevisionId = "picture:EP01-SC01-SH01:007";
    public const string KeyframeFileName = "kf-EP01-SC01-SH01-canon.jpg";
    public const string KfSourceHash = "hf82234a0:125347";
    public const string PhysicalSha256 = "7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9";
    public const int PhysicalBytes = 93991;
    public const int Width = 1280;
    public const int Height = 720;
    public const string Mime = "image/jpeg";
    public const string SourceType = "LEGACY_INGRESS";

    public const string Story =
        "(Tiếng cạch cửa. Minh bước vào, đầu hơi cúi, quai cặp kéo lê một bên vai. Người mẹ đang lau dọn bàn ăn, ngẩng lên nhìn dáng đi của con.)";

    public const string ActingBeat =
        "Audience must see Linh entering the room, face toward Minh — not the back of the head, not leaving. Linh looks toward Minh. Face visible. NEVER look into the lens. Mouth mid-speech. Face visible — not a product look into camera. Never paint the line.";

    public const string VisualContract =
        """{"gaze":"Linh looks toward Minh. Face visible. NEVER look into the lens.","lens":"35mm","focus":"Linh","camera":"Eye level","intent":"Audience must see Linh entering the room, face toward Minh — not the back of the head, not leaving.","shotId":"EP01-SC01-SH01","framing":"WIDE","primary":{"id":"CHAR-003","name":"Linh","role":"primary"},"secondary":[{"id":"CHAR-001","name":"Minh","role":"secondary"}],"visualUniverse":"FAMIXA","visualMode":"3D_STYLIZED_REALISM"}""";

    public static string ProvenanceJson() =>
        $$"""{"sourceType":"{{SourceType}}","sourceBuildId":"{{BuildId:D}}","sourceShotId":"{{Shot}}","sourceField":"runs.EP01-SC01-SH01.shotAction|visualSpec","sourceHash":"{{KfSourceHash}}","pictureRevisionId":"{{PictureRevisionId}}"}""";

    public static ProductionScope Scope() => new(BuildId, Episode, Scene, Shot);
}
