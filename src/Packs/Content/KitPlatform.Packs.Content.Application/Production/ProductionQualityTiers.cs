namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Draft is the default. A higher package is used only when the user selects it on that click.
/// </summary>
public static class ProductionQualityTiers
{
    public const string ImageDraft = "512";
    public const string UnsupportedImageSize = "IMAGE_SIZE_UNSUPPORTED";

    public static string NormalizeImage(string? size)
    {
        var value = string.IsNullOrWhiteSpace(size) ? ImageDraft : size.Trim();
        return value switch
        {
            "0.5K" or "512" or "512P" or "512PX" => "512",
            "1K" or "2K" or "4K" => value,
            _ => throw new InvalidOperationException(UnsupportedImageSize),
        };
    }

    public static decimal ImageUsd(string? size) => NormalizeImage(size) switch
    {
        "512" => 0.045m,
        "1K" => 0.067m,
        "2K" => 0.101m,
        "4K" => 0.151m,
        _ => throw new InvalidOperationException(UnsupportedImageSize),
    };
}

/// <summary>The image size for the create that is running on this call. Empty means the draft size.</summary>
public static class ProductionImageSizeChoice
{
    static readonly AsyncLocal<string?> Current = new();

    public static string CurrentOrDraft =>
        string.IsNullOrWhiteSpace(Current.Value) ? ProductionQualityTiers.ImageDraft : Current.Value;

    public static IDisposable Use(string size)
    {
        var previous = Current.Value;
        Current.Value = size;
        return new Restore(previous);
    }

    sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
