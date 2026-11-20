using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Worker riêng của M1 (bảng <c>linesheet_render_run</c>), không dùng <c>work_job</c>: một tập dài nhiều phút
/// không chặn hàng đợi Content chung. Mỗi vòng làm đúng một bước (một shot), lưu tiến độ rồi nhận bước tiếp,
/// nên tắt máy giữa chừng chỉ mất bước đang làm.
/// </summary>
internal sealed class FamixaLinesheetRenderWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly IOptions<FamixaLinesheetOptions> _options;
    readonly IOptions<ContentOptions> _content;
    readonly ILogger<FamixaLinesheetRenderWorker> _log;

    public FamixaLinesheetRenderWorker(
        IServiceScopeFactory scopes,
        IOptions<FamixaLinesheetOptions> options,
        IOptions<ContentOptions> content,
        ILogger<FamixaLinesheetRenderWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _content = content;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!(_options.Value.WorkerEnabled ?? _content.Value.WorkerEnabled))
        {
            _log.LogInformation("Famixa linesheet render worker disabled.");
            return;
        }

        if (!await DelayAsync(TimeSpan.FromSeconds(12), stoppingToken)) return;
        _log.LogInformation("Famixa linesheet render worker started.");

        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            string? done = null;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var renderer = scope.ServiceProvider.GetRequiredService<IFamixaLinesheetRenderer>();
                done = await renderer.RunNextStepAsync(stoppingToken);
                failures = 0;
                if (done is not null)
                    _log.LogInformation("Linesheet render: {Step}", done);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // ví dụ: chưa chạy migration 430. Lùi dần để không đầy log.
                failures++;
                if (failures <= 3 || failures % 20 == 0)
                    _log.LogWarning(ex, "Famixa linesheet render worker loop failed ({Count}).", failures);
            }

            if (done is not null) continue;
            var wait = failures > 3
                ? TimeSpan.FromMinutes(2)
                : TimeSpan.FromSeconds(Math.Clamp(_options.Value.WorkerPollSeconds, 1, 60));
            if (!await DelayAsync(wait, stoppingToken)) break;
        }
    }

    static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
