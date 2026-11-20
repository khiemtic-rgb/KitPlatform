namespace KitPlatform.Packs.Content;

/// <summary>
/// Fal sync-lipsync/v3 ASD coordinates are integer pixels. Floats → pydantic int_type, no mp4.
/// </summary>
public static class FamixaFalLipsyncSpeakerPinV1
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_SPEAKER_PIN_V1";

    public static int[] PixelCoordinates(double x01, double y01, int? width = null, int? height = null)
    {
        var w = width is > 0 ? width.Value : 1280;
        var h = height is > 0 ? height.Value : 720;
        var px = (int)Math.Round(Math.Clamp(x01, 0, 1) * w, MidpointRounding.AwayFromZero);
        var py = (int)Math.Round(Math.Clamp(y01, 0, 1) * h, MidpointRounding.AwayFromZero);
        return [Math.Clamp(px, 0, Math.Max(0, w - 1)), Math.Clamp(py, 0, Math.Max(0, h - 1))];
    }

    public static bool TryActiveSpeakerDetection(
        double? x01,
        double? y01,
        int? width,
        int? height,
        int? frameNumber,
        out int frame,
        out int[] coordinates)
    {
        frame = 0;
        coordinates = [];
        if (x01 is not (>= 0 and <= 1) || y01 is not (>= 0 and <= 1)) return false;
        frame = frameNumber is >= 0 ? frameNumber.Value : 0;
        coordinates = PixelCoordinates(x01.Value, y01.Value, width, height);
        return true;
    }

    /** Face box around the pin. CGI two-shot: a point is rejected; a box is the speaker lock. */
    public static int[] FaceBoundingBox(double x01, double y01, int? width = null, int? height = null)
    {
        var w = width is > 0 ? width.Value : 1280;
        var h = height is > 0 ? height.Value : 720;
        var pin = PixelCoordinates(x01, y01, w, h);
        var bw = Math.Max(140, w / 8);
        var bh = Math.Max(170, h / 4);
        var x1 = Math.Clamp(pin[0] - bw / 2, 0, Math.Max(0, w - 1));
        var y1 = Math.Clamp(pin[1] - bh / 2, 0, Math.Max(0, h - 1));
        var x2 = Math.Clamp(pin[0] + bw / 2, x1 + 1, w);
        var y2 = Math.Clamp(pin[1] + bh / 2, y1 + 1, h);
        return [x1, y1, x2, y2];
    }

    public static int FrameCountOf(double? durationSec, int fps = 24)
    {
        var sec = durationSec is > 0 ? durationSec.Value : 4;
        var n = (int)Math.Round(sec * Math.Max(1, fps), MidpointRounding.AwayFromZero);
        return Math.Clamp(n, 1, 400);
    }

    /** Prefer encoded video samples. Shot production 5s must not invent 120 boxes for a 96-frame take. */
    public static int FrameCountFromTake(byte[]? takeBytes, double? durationFallback)
    {
        if (takeBytes is { Length: >= 32 })
        {
            var samples = KitVideoMotionRules.ReadMp4VideoSampleCount(takeBytes);
            if (samples is >= 1 and <= 400) return samples;
            var fileSec = KitVideoMotionRules.ReadMp4DurationSec(takeBytes);
            if (fileSec > 0) return FrameCountOf(fileSec);
        }
        return FrameCountOf(durationFallback);
    }

    public static int[][] RepeatFaceBox(int[] box, int frames)
    {
        var n = Math.Clamp(frames, 1, 400);
        var rows = new int[n][];
        for (var i = 0; i < n; i++) rows[i] = box;
        return rows;
    }

    /** No usable pin → Fal auto. Do not invent coordinates. */
    public static bool UseAutoDetect(
        double? x01,
        double? y01,
        int? width,
        int? height,
        int? frameNumber)
    {
        return !TryActiveSpeakerDetection(x01, y01, width, height, frameNumber, out _, out _);
    }
}
