namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Existing SH01 two-shot pin: Linh / CHAR-003 is the right face.
/// Coordinates come from famixa-lipsync-speaker-target (0.63, 0.25 @ 1280x720, frame 0).
/// </summary>
public static class ProductionLipsyncSpeakerTarget
{
    public const string SpeakerCharacterId = "CHAR-003";
    public const double X01 = 0.63;
    public const double Y01 = 0.25;
    /// <summary>Current SH01 take: Linh stands at the table on the left. The old right-face pin is Minh.</summary>
    /// <summary>Medium SH01 still b9ff68dc. Linh is the large right face. Mouth is open.</summary>
    public const double TenantSh01LinhX01 = 0.64;
    public const double TenantSh01LinhY01 = 0.39;
    /// <summary>Head on the medium still, scaled to 1280x720. Covers the hair, both eyes, and the open mouth.</summary>
    public static readonly int[] TenantSh01HeadBox = [660, 40, 1100, 400];
    /// <summary>Current SH02 take 82bf78e6 at 0.4s. Minh is the left face. Mouth is inside the head box.</summary>
    public const double TenantSh02MinhX01 = 0.40;
    public const double TenantSh02MinhY01 = 0.56;
    /// <summary>Head on take 82bf78e6 frame 0.4s. Covers the hair, both eyes, and the open mouth.</summary>
    public static readonly int[] TenantSh02HeadBox = [300, 180, 580, 480];
    /// <summary>
    /// Head-sized box around a mouth pin. The passing Fal run used a box that covered the whole head.
    /// The pin sits on the mouth, so the box extends farther above the pin than below it.
    /// </summary>
    public static int[] HeadBoxAroundMouth(double x01, double y01)
    {
        var pin = FamixaFalLipsyncSpeakerPinV1.PixelCoordinates(x01, y01, Width, Height);
        var x1 = Math.Clamp(pin[0] - 140, 0, Width - 2);
        var x2 = Math.Clamp(pin[0] + 140, x1 + 1, Width);
        var y1 = Math.Clamp(pin[1] - 220, 0, Height - 2);
        var y2 = Math.Clamp(pin[1] + 90, y1 + 1, Height);
        return [x1, y1, x2, y2];
    }
    public const int Width = 1280;
    public const int Height = 720;
    public const int FrameNumber = 0;

    public static bool TryResolve(string? speakerCharacterId, out ProductionLipsyncSpeakerPin pin)
    {
        pin = default;
        if (!string.Equals((speakerCharacterId ?? "").Trim(), SpeakerCharacterId, StringComparison.Ordinal))
            return false;
        pin = new ProductionLipsyncSpeakerPin(SpeakerCharacterId, X01, Y01, Width, Height, FrameNumber);
        return FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(
            pin.X01, pin.Y01, pin.Width, pin.Height, pin.FrameNumber, out _, out _);
    }

    /// <summary>
    /// CHAR-003 global pin, or official SH02 CHAR-001 freeze (canonical line required).
    /// CHAR-001 stays unresolved unless the SH02 line is present.
    /// </summary>
    public static bool TryResolveFrozen(string? lineId, string? speakerCharacterId, out ProductionLipsyncSpeakerPin pin)
    {
        if (TryResolve(speakerCharacterId, out pin)) return true;
        return ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker(lineId, speakerCharacterId, out pin);
    }

    public static bool TryResolveTenantSh01Linh(out ProductionLipsyncSpeakerPin pin)
    {
        pin = new ProductionLipsyncSpeakerPin(
            SpeakerCharacterId, TenantSh01LinhX01, TenantSh01LinhY01, Width, Height, FrameNumber);
        return FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(
            pin.X01, pin.Y01, pin.Width, pin.Height, pin.FrameNumber, out _, out _);
    }

    public static bool TryResolveTenantSh02Minh(out ProductionLipsyncSpeakerPin pin)
    {
        pin = new ProductionLipsyncSpeakerPin(
            ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
            TenantSh02MinhX01, TenantSh02MinhY01, Width, Height, FrameNumber);
        return FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(
            pin.X01, pin.Y01, pin.Width, pin.Height, pin.FrameNumber, out _, out _);
    }

    /// <summary>
    /// Transport may only send the current SH01 Linh pin, a catalog CHAR-003 pin, or the official SH02 CHAR-001 pin.
    /// Does not invent a speaker. Does not copy SH01 CHAR-003 onto SH02.
    /// </summary>
    public static bool TryAcceptTransportPin(
        ProductionScope? scope,
        ProductionLipsyncSpeakerPin requested,
        out ProductionLipsyncSpeakerPin pin)
    {
        pin = default;
        if (scope is not null && TenantSh01(scope))
        {
            return string.Equals(requested.CharacterId, SpeakerCharacterId, StringComparison.Ordinal)
                && TryResolveTenantSh01Linh(out pin)
                && pin.X01 == requested.X01
                && pin.Y01 == requested.Y01;
        }
        if (scope is not null && ProductionEp01Sh02LipsyncCanon.MatchesScope(scope))
        {
            return ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker(
                    ProductionEp01Sh02LipsyncCanon.LineId, requested.CharacterId, out pin)
                && pin.X01 == requested.X01
                && pin.Y01 == requested.Y01;
        }
        if (scope is not null && TenantSh02(scope))
        {
            return string.Equals(
                    requested.CharacterId, ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId, StringComparison.Ordinal)
                && TryResolveTenantSh02Minh(out pin)
                && pin.X01 == requested.X01
                && pin.Y01 == requested.Y01;
        }
        if (TryResolve(requested.CharacterId, out pin)
            && pin.X01 == requested.X01
            && pin.Y01 == requested.Y01)
            return true;
        pin = default;
        return false;
    }

    static bool TenantSh01(ProductionScope scope) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode)
        && string.Equals(scope.ShotCode, "EP01-SC01-SH01", StringComparison.Ordinal);

    static bool TenantSh02(ProductionScope scope) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode)
        && string.Equals(scope.ShotCode, "EP01-SC01-SH02", StringComparison.Ordinal);
}

public readonly record struct ProductionLipsyncSpeakerPin(
    string CharacterId,
    double X01,
    double Y01,
    int Width,
    int Height,
    int FrameNumber);
