using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoJobService : IKitVideoJobService
{
    private readonly KitVideoJobRepository _jobs;
    private readonly ContentRepository _repo;
    private readonly IMediaValidator _media;

    public KitVideoJobService(KitVideoJobRepository jobs, ContentRepository repo, IMediaValidator media)
    {
        _jobs = jobs;
        _repo = repo;
        _media = media;
    }

    public async Task<IReadOnlyList<KitVideoGenerationJobDto>> ListAsync(
        Guid productionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _jobs.ListByProductionAsync(productionId, cancellationToken);
        var list = new List<KitVideoGenerationJobDto>(rows.Count);
        foreach (var row in rows)
            list.Add(await ToDto(row, cancellationToken));
        return list;
    }

    public async Task<KitVideoGenerationJobDto> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var row = await _jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("GenerationJob không tồn tại.");
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoGenerationJobDto> EnsureAsync(
        CreateKitVideoJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = (request.IdempotencyKey ?? "").Trim();
        if (key.Length < 8)
            throw new InvalidOperationException("IdempotencyKey bắt buộc (≥ 8 ký tự).");

        var existing = await _jobs.GetByKeyAsync(key, cancellationToken);
        if (existing is not null)
        {
            if (request.Confirmed && existing.Status is "PRECHECK" or "READY" or "CONFIRM_REQUIRED")
                return await ConfirmAsync(existing.Id, cancellationToken);
            return await ToDto(existing, cancellationToken);
        }

        var production = await _repo.GetVideoProductionAsync(request.ProductionId, cancellationToken)
            ?? throw new InvalidOperationException("Production không tồn tại.");
        var shotCode = (request.ShotCode ?? "").Trim().ToUpperInvariant();
        if (shotCode.Length == 0)
            throw new InvalidOperationException("ShotCode bắt buộc.");

        if (request.HasAction == false)
        {
            await _repo.UpsertVideoShotStateAsync(
                production.Id, shotCode, "HOLD", false, cancellationToken,
                KitVideoJobRepository.ShotExtra(null, null));
            KitVideoEngineRules.EnsureNotHold("HOLD");
        }

        var shots = await _repo.ListVideoShotsAsync(production.Id, cancellationToken);
        var shot = shots.FirstOrDefault(s => string.Equals(s.ShotCode, shotCode, StringComparison.OrdinalIgnoreCase));
        var shotState = shot?.State ?? (request.HasAction == false ? "HOLD" : "READY");
        KitVideoEngineRules.EnsureNotHold(shotState);

        var provider = (request.Provider ?? "").Trim().ToUpperInvariant();
        var operation = (request.Operation ?? "").Trim().ToUpperInvariant();
        if (operation is not ("STILL" or "I2V" or "TTS" or "LIPSYNC"))
            throw new InvalidOperationException("Operation không hợp lệ.");

        var confirmed = request.Confirmed;
        var status = confirmed ? "SUBMITTED" : "CONFIRM_REQUIRED";
        if (confirmed)
            KitVideoEngineRules.EnsureCreditGate(true);

        var now = DateTimeOffset.UtcNow;
        var row = await _jobs.InsertAsync(new KitVideoJobRepository.JobRow
        {
            Id = Guid.NewGuid(),
            ProjectId = production.ProjectId,
            ProductionId = production.Id,
            SceneCode = (request.SceneCode ?? "").Trim().ToUpperInvariant(),
            ShotCode = shotCode,
            Provider = provider,
            Operation = operation,
            Status = status,
            IdempotencyKey = key,
            Confirmed = confirmed,
            CreatedAt = now,
            StartedAt = confirmed ? now : null,
        }, cancellationToken);

        if (confirmed && row.Status == "SUBMITTED")
        {
            var attempts = await _jobs.ListAttemptsAsync(row.Id, cancellationToken);
            if (attempts.Count == 0)
                await StartAttemptAsync(row.Id, cancellationToken);
        }

        return await GetAsync(row.Id, cancellationToken);
    }

    public async Task<KitVideoGenerationJobDto> ConfirmAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var row = await _jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("GenerationJob không tồn tại.");
        if (row.Status is "SUBMITTED" or "RUNNING" or "SUCCEEDED" or "FAILED" or "REFUND_PENDING")
            return await ToDto(row, cancellationToken);

        var shots = await _repo.ListVideoShotsAsync(row.ProductionId, cancellationToken);
        var shot = shots.FirstOrDefault(s => string.Equals(s.ShotCode, row.ShotCode, StringComparison.OrdinalIgnoreCase));
        KitVideoEngineRules.EnsureNotHold(shot?.State ?? "DRAFT");
        KitVideoEngineRules.EnsureCreditGate(true);

        var now = DateTimeOffset.UtcNow;
        await _jobs.UpdateJobAsync(row.Id, "SUBMITTED", true, row.StartedAt ?? now, null, cancellationToken);
        var attempts = await _jobs.ListAttemptsAsync(row.Id, cancellationToken);
        if (attempts.Count == 0)
            await StartAttemptAsync(row.Id, cancellationToken);
        return await GetAsync(row.Id, cancellationToken);
    }

    public async Task<KitVideoGenerationJobDto> CompleteAsync(
        Guid jobId,
        CompleteKitVideoJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("GenerationJob không tồn tại.");
        var attempts = await _jobs.ListAttemptsAsync(row.Id, cancellationToken);
        var attempt = attempts.LastOrDefault()
            ?? await StartAttemptAsync(row.Id, cancellationToken);

        var snapshot = new KitVideoMediaSnapshot(
            request.ProviderStatus,
            request.OutputUrl,
            request.HttpStatus,
            request.FileExists,
            request.Readable,
            request.ContainerOk,
            request.ProbeOk,
            request.ProbeError,
            request.DurationSec,
            request.Width,
            request.Height);
        var media = request.ProbeOk is null && !string.IsNullOrWhiteSpace(request.OutputUrl)
            ? await _media.ValidateUrlAsync(snapshot, cancellationToken)
            : _media.Validate(snapshot);

        var providerStatus = (request.ProviderStatus ?? "").Trim().ToUpperInvariant();
        var taskId = (request.ProviderTaskId ?? "").Trim();
        if (taskId.Length == 0)
            throw new InvalidOperationException("ProviderTaskId bắt buộc.");

        await _jobs.InsertProviderTaskAsync(new KitVideoJobRepository.ProviderTaskRow
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            Provider = row.Provider,
            ProviderTaskId = taskId,
            ProviderStatus = providerStatus,
            FailureCode = request.FailureCode,
            OutputUrl = request.OutputUrl,
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        var ok = media.Ok;
        var jobStatus = ok ? "SUCCEEDED" : "FAILED";
        var error = ok ? null : string.Join(" ", media.Reasons);
        await _jobs.UpdateAttemptAsync(attempt.Id, jobStatus, error, cancellationToken);
        await _jobs.UpdateJobAsync(row.Id, jobStatus, true, row.StartedAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, cancellationToken);

        var shotTo = ok ? ShotReadyState(row.Operation) : "FAILED";
        await _repo.UpsertVideoShotStateAsync(
            row.ProductionId,
            row.ShotCode,
            shotTo,
            !ok,
            cancellationToken,
            KitVideoJobRepository.ShotExtra(row.Provider, request.FailureCode));
        await RefreshRunStatusAsync(row.ProductionId, cancellationToken);
        return await GetAsync(row.Id, cancellationToken);
    }

    public async Task<KitVideoGenerationJobDto> RetryAsync(
        Guid jobId,
        RetryKitVideoJobRequest request,
        CancellationToken cancellationToken = default)
    {
        KitVideoEngineRules.EnsureCreditGate(request.Confirmed);
        var row = await _jobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("GenerationJob không tồn tại.");
        if (row.Status is not "FAILED")
            throw new InvalidOperationException("Chỉ retry job FAILED.");

        var shots = await _repo.ListVideoShotsAsync(row.ProductionId, cancellationToken);
        var shot = shots.FirstOrDefault(s => string.Equals(s.ShotCode, row.ShotCode, StringComparison.OrdinalIgnoreCase));
        KitVideoEngineRules.EnsureNotHold(shot?.State ?? "FAILED");

        var now = DateTimeOffset.UtcNow;
        await _jobs.UpdateJobAsync(row.Id, "SUBMITTED", true, now, null, cancellationToken);
        await StartAttemptAsync(row.Id, cancellationToken);
        return await GetAsync(row.Id, cancellationToken);
    }

    private async Task<KitVideoJobRepository.AttemptRow> StartAttemptAsync(Guid jobId, CancellationToken ct)
    {
        var existing = await _jobs.ListAttemptsAsync(jobId, ct);
        var next = existing.Count == 0 ? 1 : existing.Max(a => a.AttemptNo) + 1;
        return await _jobs.InsertAttemptAsync(new KitVideoJobRepository.AttemptRow
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            AttemptNo = next,
            Status = "RUNNING",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);
    }

    private async Task RefreshRunStatusAsync(Guid productionId, CancellationToken ct)
    {
        var shots = await _repo.ListVideoShotsAsync(productionId, ct);
        var run = KitVideoEngineRules.RunStatusAfterShots(
            shots.Select(s => (s.State, s.Failed)).ToList());
        await _repo.UpdateVideoProductionRunStatusAsync(productionId, run, ct);
    }

    private async Task<KitVideoGenerationJobDto> ToDto(KitVideoJobRepository.JobRow row, CancellationToken ct)
    {
        var attempts = await _jobs.ListAttemptsAsync(row.Id, ct);
        var tasks = await _jobs.ListTasksAsync(attempts.Select(a => a.Id).ToList(), ct);
        return new KitVideoGenerationJobDto(
            row.Id,
            row.ProjectId,
            row.ProductionId,
            row.SceneCode,
            row.ShotCode,
            row.Provider,
            row.Operation,
            row.Status,
            row.IdempotencyKey,
            row.Confirmed,
            row.CreatedAt,
            row.StartedAt,
            row.CompletedAt,
            attempts.Select(a => new KitVideoGenerationAttemptDto(
                a.Id,
                a.AttemptNo,
                a.Status,
                a.Error,
                a.CreatedAt,
                tasks.Where(t => t.AttemptId == a.Id).Select(t => new KitVideoProviderTaskDto(
                    t.Id, t.AttemptId, t.Provider, t.ProviderTaskId, t.ProviderStatus,
                    t.FailureCode, t.OutputUrl, t.CreatedAt)).ToList())).ToList());
    }

    private static string ShotReadyState(string operation) =>
        operation.Equals("I2V", StringComparison.OrdinalIgnoreCase) ? "VIDEO_READY"
        : operation.Equals("LIPSYNC", StringComparison.OrdinalIgnoreCase) ? "LIPSYNC_READY"
        : "KF_REVIEW";
}
