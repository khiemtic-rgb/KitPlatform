using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class GeminiPictureProvider : IFamixaPictureProvider
{
    private readonly ContentGeminiClient _gemini;

    public GeminiPictureProvider(ContentGeminiClient gemini) => _gemini = gemini;

    public string ProviderId => FamixaProviderIds.Gemini;

    public async Task<FamixaProviderResult> GenerateAsync(
        FamixaPictureProviderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var labeled = request.References.Select(r => (r.Mime, r.Base64, r.Label)).ToList();
            var (bytes, model) = await _gemini.GenerateImageWithRefsAsync(
                request.Prompt, labeled, request.AspectRatio, cancellationToken);
            var mime = bytes.Length >= 8 && bytes[0] == 0x89 ? "image/png" : "image/jpeg";
            return new FamixaProviderResult(
                ProviderId,
                model,
                FamixaProviderStatus.Succeeded,
                MimeType: mime,
                Bytes: bytes,
                CostKind: FamixaCostKind.Unknown);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }
}

internal sealed class RunwayMotionProvider : IFamixaMotionProvider
{
    private readonly ContentRunwayClient _runway;

    public RunwayMotionProvider(ContentRunwayClient runway) => _runway = runway;

    public string ProviderId => FamixaProviderIds.Runway;

    public bool CanHandleTask(string? taskId) =>
        FamixaProviderTaskIdentity.CanHandle(ProviderId, FamixaProviderCapability.Motion, taskId);

    public async Task<FamixaProviderResult> StartAsync(
        FamixaMotionStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var seconds = request.Seconds >= 8 ? 10 : 5;
            var taskId = await _runway.CreateImageToVideoAsync(
                request.ImageDataUrl,
                request.Prompt,
                seconds,
                request.Ratio,
                cancellationToken,
                request.LastFrameDataUrl);
            var cost = FamixaProviderCostCatalog.Estimate(ProviderId, ContentRunwayClient.TurboModel, seconds);
            return new FamixaProviderResult(
                ProviderId,
                ContentRunwayClient.TurboModel,
                FamixaProviderStatus.Pending,
                ProviderRequestId: taskId,
                CostKind: cost.Kind,
                CostEstimate: cost.Amount,
                CostUnit: cost.Unit);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }

    public Task<FamixaProviderResult> GetAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    public Task<FamixaProviderResult> RecoverAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    private async Task<FamixaProviderResult> PollAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var id = NormalizeTaskId(taskId);
            var (status, video, error, failureCode) = await _runway.GetTaskAsync(id, cancellationToken);
            return new FamixaProviderResult(
                ProviderId,
                ContentRunwayClient.TurboModel,
                FamixaProviderTaskIdentity.ToProviderStatus(status),
                ProviderRequestId: id,
                OutputUrl: video,
                ErrorCode: failureCode,
                Error: error);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }

    private static string NormalizeTaskId(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.StartsWith("rw_", StringComparison.OrdinalIgnoreCase)) s = s[3..];
        return s;
    }
}

internal sealed class WanMotionProvider : IFamixaMotionProvider
{
    private readonly ContentFalClient _fal;

    public WanMotionProvider(ContentFalClient fal) => _fal = fal;

    public string ProviderId => FamixaProviderIds.Wan;

    public bool CanHandleTask(string? taskId) =>
        FamixaProviderTaskIdentity.CanHandle(ProviderId, FamixaProviderCapability.Motion, taskId);

    public async Task<FamixaProviderResult> StartAsync(
        FamixaMotionStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Legacy Wan DTO: 5s / ~6s. Fal frames = seconds >= 8 ? 100 : 81 — keep 81.
            var seconds = request.Seconds >= 8 ? 6 : 5;
            var wanId = await _fal.CreateImageToVideoAsync(
                request.ImageDataUrl,
                request.Prompt,
                request.NegativePrompt,
                seconds,
                request.Ratio,
                cancellationToken);
            var cost = FamixaProviderCostCatalog.Estimate(ProviderId, ContentFalClient.WanModel, seconds);
            return new FamixaProviderResult(
                ProviderId,
                ContentFalClient.WanModel,
                FamixaProviderStatus.Pending,
                ProviderRequestId: wanId,
                CostKind: cost.Kind,
                CostEstimate: cost.Amount,
                CostUnit: cost.Unit);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }

    public Task<FamixaProviderResult> GetAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    public Task<FamixaProviderResult> RecoverAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    private async Task<FamixaProviderResult> PollAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var (status, video, error) = await _fal.GetTaskAsync(taskId, cancellationToken);
            return new FamixaProviderResult(
                ProviderId,
                ContentFalClient.WanModel,
                FamixaProviderTaskIdentity.ToProviderStatus(status),
                ProviderRequestId: taskId,
                OutputUrl: video,
                Error: error);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }
}

internal sealed class ElevenLabsVoiceProvider : IFamixaVoiceProvider
{
    private readonly ContentElevenLabsClient _elevenLabs;

    public ElevenLabsVoiceProvider(ContentElevenLabsClient elevenLabs) => _elevenLabs = elevenLabs;

    public string ProviderId => FamixaProviderIds.ElevenLabs;

    public async Task<FamixaProviderResult> SynthesizeAsync(
        FamixaVoiceSynthesizeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var voice = (request.VoiceId ?? "").Trim();
            if (voice.Length is < 8 or > 64)
                throw new InvalidOperationException("Voice ID ElevenLabs không hợp lệ.");
            foreach (var ch in voice)
            {
                if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-')
                    continue;
                throw new InvalidOperationException("Voice ID ElevenLabs không hợp lệ.");
            }
            if (!await _elevenLabs.IsConfiguredAsync(cancellationToken))
                throw new InvalidOperationException("Chưa có key ElevenLabs — Cấu hình AI.");
            var bytes = await _elevenLabs.SynthesizeMp3Async(
                request.Text,
                voice,
                request.PublicOwnerId,
                request.VoiceName,
                request.VoiceSettings,
                cancellationToken,
                request.Accent);
            return new FamixaProviderResult(
                ProviderId,
                "eleven_v3",
                FamixaProviderStatus.Succeeded,
                MimeType: "audio/mpeg",
                Bytes: bytes,
                CostKind: FamixaCostKind.Unknown);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }
}

internal sealed class FalLipSyncProvider : IFamixaLipSyncProvider
{
    private readonly ContentFalClient _fal;

    public FalLipSyncProvider(ContentFalClient fal) => _fal = fal;

    public string ProviderId => FamixaProviderIds.Fal;

    public bool CanHandleTask(string? taskId) =>
        FamixaProviderTaskIdentity.CanHandle(ProviderId, FamixaProviderCapability.LipSync, taskId);

    public async Task<FamixaProviderResult> StartAsync(
        FamixaLipSyncStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _fal.CreateLipsyncAsync(
                request.VideoUrl,
                request.AudioUrl,
                request.SyncMode,
                request.ModelKind,
                cancellationToken);
            var cost = FamixaProviderCostCatalog.Estimate(ProviderId, request.ModelKind, 5);
            return new FamixaProviderResult(
                ProviderId,
                created.Model,
                FamixaProviderStatus.Pending,
                ProviderRequestId: created.TaskId,
                OutputUrl: created.VideoUrl,
                CostKind: cost.Kind,
                CostEstimate: cost.Amount,
                CostUnit: cost.Unit);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }

    public Task<FamixaProviderResult> GetAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    public Task<FamixaProviderResult> RecoverAsync(string taskId, CancellationToken cancellationToken) =>
        PollAsync(taskId, cancellationToken);

    private async Task<FamixaProviderResult> PollAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var (status, video, error) = await _fal.GetTaskAsync(taskId, cancellationToken);
            var model = FamixaProviderTaskIdentity.LipsyncModelOf(taskId);
            return new FamixaProviderResult(
                ProviderId,
                model == "v3" ? "sync-lipsync-v3" : model == "ls" ? "latentsync" : ContentFalClient.LipsyncModel,
                FamixaProviderTaskIdentity.ToProviderStatus(status),
                ProviderRequestId: taskId,
                OutputUrl: video,
                Error: error);
        }
        catch (Exception ex)
        {
            throw FamixaProviderErrors.Wrap(ProviderId, ex);
        }
    }
}

internal sealed class FamixaProviderRegistry : IFamixaProviderRegistry
{
    private readonly IFamixaPictureProvider _gemini;
    private readonly IFamixaMotionProvider _runway;
    private readonly IFamixaMotionProvider _wan;
    private readonly IFamixaVoiceProvider _eleven;
    private readonly IFamixaLipSyncProvider _fal;

    public FamixaProviderRegistry(
        GeminiPictureProvider gemini,
        RunwayMotionProvider runway,
        WanMotionProvider wan,
        ElevenLabsVoiceProvider eleven,
        FalLipSyncProvider fal)
    {
        _gemini = gemini;
        _runway = runway;
        _wan = wan;
        _eleven = eleven;
        _fal = fal;
    }

    public IReadOnlyList<FamixaProviderDescriptor> List() => FamixaProviderCatalog.All;

    public FamixaProviderDescriptor Describe(string providerId) =>
        FamixaProviderCatalog.Find(providerId)
        ?? throw new FamixaProviderException(
            FamixaProviderErrorCodes.Unsupported,
            providerId ?? "",
            $"PROVIDER_UNAVAILABLE: {providerId}.");

    public bool HasCapability(string providerId, FamixaProviderCapability capability)
    {
        var desc = FamixaProviderCatalog.Find(providerId);
        return desc is not null && desc.Capabilities.Contains(capability);
    }

    public IFamixaPictureProvider GetPicture(string providerId)
    {
        if (Is(providerId, FamixaProviderIds.Gemini)) return _gemini;
        throw Missing(providerId, FamixaProviderCapability.Picture);
    }

    public IFamixaMotionProvider GetMotion(string providerId)
    {
        if (Is(providerId, FamixaProviderIds.Runway)) return _runway;
        if (Is(providerId, FamixaProviderIds.Wan)) return _wan;
        throw Missing(providerId, FamixaProviderCapability.Motion);
    }

    public IFamixaVoiceProvider GetVoice(string providerId)
    {
        if (Is(providerId, FamixaProviderIds.ElevenLabs)) return _eleven;
        throw Missing(providerId, FamixaProviderCapability.Voice);
    }

    public IFamixaLipSyncProvider GetLipSync(string providerId)
    {
        if (Is(providerId, FamixaProviderIds.Fal)) return _fal;
        throw Missing(providerId, FamixaProviderCapability.LipSync);
    }

    public FamixaProviderTaskRef DescribeTask(
        string? taskId,
        string? providerId = null,
        FamixaProviderCapability? capability = null) =>
        FamixaProviderTaskIdentity.Resolve(taskId, providerId, capability);

    public IFamixaMotionProvider GetMotionForTask(string? taskId, string? providerId = null)
    {
        var identity = FamixaProviderTaskIdentity.Resolve(taskId, providerId, FamixaProviderCapability.Motion);
        return GetMotion(identity.ProviderId);
    }

    public IFamixaLipSyncProvider GetLipSyncForTask(string? taskId, string? providerId = null)
    {
        var identity = FamixaProviderTaskIdentity.Resolve(taskId, providerId, FamixaProviderCapability.LipSync);
        return GetLipSync(identity.ProviderId);
    }

    private static bool Is(string? raw, string id) =>
        string.Equals((raw ?? "").Trim(), id, StringComparison.OrdinalIgnoreCase);

    private static FamixaProviderException Missing(string? providerId, FamixaProviderCapability capability) =>
        new(
            FamixaProviderErrorCodes.Unsupported,
            providerId ?? "",
            $"UNSUPPORTED: {providerId} không có {capability}.");
}
