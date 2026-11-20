using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Cầu nối "Đồng bộ lên máy chủ": Studio trên máy dựng phim (local) gửi thành phẩm lên máy chủ đăng bài
/// (Content:RemotePublishBaseUrl). Trình duyệt gọi API local; API local chuyển tiếp sang máy chủ, nên không cần CORS
/// và không lưu mật khẩu máy chủ ở đâu cả: người dùng tự đăng nhập máy chủ một lần, token máy chủ nằm trong trình duyệt
/// và đi kèm header <c>X-Remote-Authorization</c>.
/// Chỉ chuyển tiếp đúng các đường cần cho việc đưa phim vào lịch đăng (danh sách cố định bên dưới).
/// Máy chủ B để trống RemotePublishBaseUrl → mọi lệnh ở đây trả 404.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content-remote")]
public sealed class ContentRemoteRelayController : ControllerBase
{
    private const long MaxBody = 600L * 1024 * 1024;
    private const string RemoteAuthHeader = "X-Remote-Authorization";

    /// <summary>Đường được chuyển tiếp (sau /api/). Mọi đường khác bị chặn.</summary>
    private static readonly (string Method, Regex Path)[] Allowed =
    [
        ("GET", Rx(@"^content/brands$")),
        ("GET", Rx(@"^content/brands/{id}/channels$")),
        ("GET", Rx(@"^content/topics/{id}$")),
        ("POST", Rx(@"^content/topics$")),
        ("POST", Rx(@"^content/topics/{id}/variants/import$")),
        ("POST", Rx(@"^content/topics/{id}/video$")),
        ("POST", Rx(@"^content/topics/{id}/publish-extras$")),
        ("POST", Rx(@"^content/agent/schedule-topics$")),
        ("GET", Rx(@"^content/agent/schedule-settings$")),
        ("GET", Rx(@"^auth/me$")),
    ];

    private static readonly string[] AuthActions = ["login", "select-workspace", "refresh"];

    private readonly IHttpClientFactory _http;
    private readonly IOptions<ContentOptions> _options;

    public ContentRemoteRelayController(IHttpClientFactory http, IOptions<ContentOptions> options)
    {
        _http = http;
        _options = options;
    }

    private static Regex Rx(string pattern) =>
        new(pattern.Replace("{id}", "[0-9a-fA-F-]{36}"), RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string? BaseUrl
    {
        get
        {
            var url = (_options.Value.RemotePublishBaseUrl ?? "").Trim().TrimEnd('/');
            return Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http") ? url : null;
        }
    }

    /// <summary>GET api/content-remote/status — máy này có bật đồng bộ không, đồng bộ lên đâu.</summary>
    [HttpGet("status")]
    public ActionResult<object> Status() => Ok(new { enabled = BaseUrl is not null, baseUrl = BaseUrl });

    /// <summary>POST api/content-remote/auth/{login|select-workspace|refresh} — đăng nhập máy chủ (người dùng tự gõ).</summary>
    [HttpPost("auth/{op}")]
    public Task Auth(string op, CancellationToken ct)
    {
        if (!AuthActions.Contains(op, StringComparer.Ordinal))
            return WriteError(404, "Không hỗ trợ lệnh đăng nhập này.");
        return ForwardAsync(HttpMethod.Post, $"auth/{op}", withToken: false, ct);
    }

    /// <summary>GET/POST api/content-remote/api/{path} — chuyển tiếp sang {máy chủ}/api/{path} nếu path nằm trong danh sách cho phép.</summary>
    [AcceptVerbs("GET", "POST")]
    [Route("api/{**path}")]
    [RequestSizeLimit(MaxBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBody)]
    public Task Relay(string path, CancellationToken ct)
    {
        path = (path ?? "").Trim('/');
        var method = Request.Method.ToUpperInvariant();
        if (path.Contains("..", StringComparison.Ordinal) || !Allowed.Any(a => a.Method == method && a.Path.IsMatch(path)))
            return WriteError(403, "Đường này không được đồng bộ lên máy chủ.");
        return ForwardAsync(new HttpMethod(method), path, withToken: true, ct);
    }

    private async Task WriteError(int status, string message)
    {
        Response.StatusCode = status;
        await Response.WriteAsJsonAsync(new { message });
    }

    private async Task ForwardAsync(HttpMethod method, string path, bool withToken, CancellationToken ct)
    {
        var baseUrl = BaseUrl;
        if (baseUrl is null)
        {
            await WriteError(404, "Máy này chưa cấu hình máy chủ đăng bài (Content:RemotePublishBaseUrl).");
            return;
        }

        using var request = new HttpRequestMessage(method, $"{baseUrl}/api/{path}{Request.QueryString}");
        if (withToken)
        {
            var token = Request.Headers[RemoteAuthHeader].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                await WriteError(401, "Chưa đăng nhập máy chủ đăng bài.");
                return;
            }
            request.Headers.TryAddWithoutValidation("Authorization", token);
        }

        if (method != HttpMethod.Get && (Request.ContentLength is > 0 || Request.Headers.ContainsKey("Transfer-Encoding")))
        {
            var body = new StreamContent(Request.Body);
            if (!string.IsNullOrWhiteSpace(Request.ContentType))
                body.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
            if (Request.ContentLength is long len)
                body.Headers.ContentLength = len;
            request.Content = body;
        }

        HttpResponseMessage response;
        try
        {
            var client = _http.CreateClient("content-remote");
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            await WriteError(502, "Không kết nối được máy chủ đăng bài: " + ex.Message);
            return;
        }

        using (response)
        {
            Response.StatusCode = (int)response.StatusCode;
            var type = response.Content.Headers.ContentType?.ToString();
            if (!string.IsNullOrWhiteSpace(type)) Response.ContentType = type;
            await response.Content.CopyToAsync(Response.Body, ct);
        }
    }
}
