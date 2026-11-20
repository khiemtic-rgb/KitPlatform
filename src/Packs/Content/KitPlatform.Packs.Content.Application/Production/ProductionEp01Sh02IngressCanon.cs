namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// EP01 SH02 Existing Canonical Ingress V1 — exact legacy sources. Do not invent.
/// Reuses SH01 RegisterKeyframe / import / existing-pixels pattern.
/// </summary>
public static class ProductionEp01Sh02IngressCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_EP01_SH02_EXISTING_CANONICAL_INGRESS_V1";
    public const string BuildIdText = "4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f";
    public static readonly Guid BuildId = ProductionScopeAnchorRules.OfficialEp01;
    public const string Episode = "EP01";
    public const string Scene = "SC01";
    public const string Shot = "EP01-SC01-SH02";
    public const string PictureRevisionId = "picture:EP01-SC01-SH02:001";
    public const string KeyframeFileName = "kf-EP01-SC01-SH02-canon.jpg";
    public const string KfSourceHash = "h5778a617:151347";
    public const string PhysicalSha256 = "9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57";
    public const int PhysicalBytes = 113493;
    public const int Width = 1280;
    public const int Height = 720;
    public const string Mime = "image/jpeg";
    public const string SourceType = "LEGACY_INGRESS";

    public const string Story =
        "(Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa. Cậu rút ra tờ bài kiểm tra đã bị gấp góc, đưa hai tay cho mẹ rồi cúi gằm mặt.)";

    public const string ActingBeat =
        "Audience must see (Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa. Cậu rút ra tờ bài kiểm tra đã bị gấp góc, đưa hai  Face visible. Gaze toward Linh, not the camera. Minh looks toward Linh. Face visible. NEVER look into the lens. Mouth mid-speech. Face visible — not a product look into camera. Never paint the line.";

    public const string VisualContract =
        """{"gaze":"Minh looks toward Linh. Face visible. NEVER look into the lens.","lens":"40mm","focus":"Minh","camera":"Eye level","intent":"Audience must see (Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa. Cậu rút ra tờ bài kiểm tra đã bị gấp góc, đưa hai  Face visible. Gaze toward Linh, not the camera.","shotId":"EP01-SC01-SH02","framing":"MEDIUM","primary":{"id":"CHAR-001","name":"Minh","role":"primary"},"secondary":[{"id":"CHAR-003","name":"Linh","role":"secondary"}]}""";

    public static string ProvenanceJson() =>
        $$"""{"sourceType":"{{SourceType}}","sourceBuildId":"{{BuildId:D}}","sourceShotId":"{{Shot}}","sourceField":"runs.EP01-SC01-SH02.shotAction|visualSpec","sourceHash":"{{KfSourceHash}}","pictureRevisionId":"{{PictureRevisionId}}"}""";

    public static ProductionScope Scope() => new(BuildId, Episode, Scene, Shot);
}
