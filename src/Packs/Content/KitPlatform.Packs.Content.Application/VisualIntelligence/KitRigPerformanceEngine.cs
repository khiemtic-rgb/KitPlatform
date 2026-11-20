using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Samples a clip and poses the shared rig. Brand plates and colors arrive as data.
/// </summary>
public sealed class KitRigPerformanceEngine : IPerformanceEngine
{
    public const int Width = 1280;
    public const int Height = 720;

    public string EngineId => "kit-rig-v1";

    public byte[] Render(PerformanceRequest request)
    {
        if (request.Clip.RigId != request.Rig.Id)
            throw new InvalidOperationException("Clip " + request.Clip.Id + " does not fit rig " + request.Rig.Id);
        if (request.Timeline.Cue.ClipId != request.Clip.Id)
            throw new InvalidOperationException("Timeline cue does not name this clip.");
        var character = request.Character ?? CharacterCatalog.Lan;
        if (character.RigId != request.Rig.Id)
            throw new InvalidOperationException("Nhân vật " + character.Id + " không dùng rig " + request.Rig.Id + ".");
        if (!string.Equals(character.MasterFile, CharacterCatalog.Lan.MasterFile, StringComparison.Ordinal))
            throw new InvalidOperationException("Chưa có lớp cắt cho " + character.MasterFile + ". Không dùng tranh Lan để thế.");

        _ = PerformanceSampling.At(request.Clip, request.Timeline.Cue, request.TimeSeconds);
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        var plate = StudioScenePainter.BorrowMaster(request.Timeline.PlateFile);
        canvas.DrawBitmap(plate, new SKRect(0, 0, Width, Height));
        _ = request.Look;
        LanVector.Draw(
            canvas,
            request.TimeSeconds,
            request.Timeline.Cue.Duration,
            request.Clip.Id,
            request.Timeline.AnchorX,
            request.Timeline.FootY);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }
}
