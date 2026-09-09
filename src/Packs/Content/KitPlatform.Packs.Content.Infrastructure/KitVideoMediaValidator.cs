using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMediaValidator : IMediaValidator
{
    private readonly IContentSeriesTakeProxyService _takes;

    public KitVideoMediaValidator(IContentSeriesTakeProxyService takes) => _takes = takes;

    public KitVideoMediaValidation Validate(KitVideoMediaSnapshot snapshot) =>
        KitVideoEngineRules.EvaluateMedia(snapshot);

    public async Task<KitVideoMediaValidation> ValidateUrlAsync(
        KitVideoMediaSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var url = (snapshot.OutputUrl ?? "").Trim();
        if (url.Length == 0 || snapshot.ProbeOk is not null)
            return Validate(snapshot);

        var probe = await _takes.ProbeAsync(url, cancellationToken);
        var mime = probe.Mime ?? "";
        var containerOk = snapshot.ContainerOk
            ?? (probe.Ok && mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase));
        return Validate(snapshot with
        {
            ProbeOk = probe.Ok,
            ProbeError = probe.Error,
            FileExists = probe.Ok,
            Readable = probe.Ok,
            ContainerOk = containerOk,
        });
    }
}
