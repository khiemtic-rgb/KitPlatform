using System.Net;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// Mocked generateContent contract for gemini-3.1-flash-image. No live provider call.
/// </summary>
public sealed class Gemini31FlashImageTransportTests
{
    const string Secret = "tenant-gemini-test-key";
    const string Model = "gemini-3.1-flash-image";
    const string Prompt = "Minh enters, head lowered, two intact people.";

    [Fact]
    public async Task Request_uses_documented_generateContent_url_model_body_and_header()
    {
        var handler = new CaptureHandler(_ => ImageResponse(FinalBytes(), "image/png"));
        var transport = Transport(handler);

        var result = await transport.GenerateOnceAsync(Secret, Model, Prompt, "16:9");

        Assert.True(result.Accepted);
        Assert.Equal(Model, result.Model);
        Assert.Equal("image/png", result.Mime);
        Assert.Equal(FinalBytes(), result.Bytes);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-image:generateContent",
            handler.Url);
        Assert.DoesNotContain("key=", handler.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Secret, handler.ApiKey);
        Assert.Null(handler.Authorization);
        using var body = JsonDocument.Parse(handler.Body);
        var root = body.RootElement;
        Assert.Equal(Prompt, root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
        var config = root.GetProperty("generationConfig");
        Assert.Equal(new[] { "TEXT", "IMAGE" }, config.GetProperty("responseModalities").EnumerateArray().Select(x => x.GetString()).ToArray());
        var image = config.GetProperty("imageConfig");
        Assert.Equal("16:9", image.GetProperty("aspectRatio").GetString());
        Assert.Equal(HttpGeminiTenantImageTransport.ImageSize, image.GetProperty("imageSize").GetString());
        Assert.Equal("512", image.GetProperty("imageSize").GetString());
        Assert.Equal(ProductionQualityTiers.ImageDraft, image.GetProperty("imageSize").GetString());
        Assert.DoesNotContain(Secret, handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_keeps_the_last_non_thought_inline_image_and_its_mime()
    {
        var handler = new CaptureHandler(_ =>
        {
            var interim = Convert.ToBase64String(Encoding.ASCII.GetBytes("thought-image-bytes-not-final!!"));
            var final = Convert.ToBase64String(FinalBytes());
            return "{\"candidates\":[{\"content\":{\"parts\":["
                + "{\"thought\":true,\"inlineData\":{\"mimeType\":\"image/jpeg\",\"data\":\"" + interim + "\"}},"
                + "{\"text\":\"draft\"},"
                + "{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"" + final + "\"}}"
                + "]}}]}";
        });

        var result = await Transport(handler).GenerateOnceAsync(Secret, Model, Prompt, "16:9");

        Assert.True(result.Accepted);
        Assert.Equal(FinalBytes(), result.Bytes);
        Assert.Equal("image/png", result.Mime);
    }

    [Fact]
    public async Task Response_reads_snake_case_inline_data_when_that_is_the_only_image()
    {
        var handler = new CaptureHandler(_ =>
        {
            var data = Convert.ToBase64String(FinalBytes());
            return "{\"candidates\":[{\"content\":{\"parts\":["
                + "{\"inline_data\":{\"mime_type\":\"image/jpeg\",\"data\":\"" + data + "\"}}"
                + "]}}]}";
        });

        var result = await Transport(handler).GenerateOnceAsync(Secret, Model, Prompt, null);

        Assert.Equal(FinalBytes(), result.Bytes);
        Assert.Equal("image/jpeg", result.Mime);
        using var body = JsonDocument.Parse(handler.Body);
        var image = body.RootElement.GetProperty("generationConfig").GetProperty("imageConfig");
        Assert.False(image.TryGetProperty("aspectRatio", out _));
        Assert.Equal("512", image.GetProperty("imageSize").GetString());
        Assert.Equal(ProductionQualityTiers.ImageDraft, image.GetProperty("imageSize").GetString());
    }

    [Fact]
    public async Task Locked_master_bytes_are_sent_before_the_prompt_and_the_key_stays_out_of_the_body()
    {
        var handler = new CaptureHandler(_ => ImageResponse(FinalBytes(), "image/png"));
        var master = Encoding.ASCII.GetBytes("locked-minh-master-bytes-0123456789abcdef");
        var sha = KitVideoIntegrityRules.Sha256Hex(master);
        var references = new[]
        {
            new GeminiStillReference("CHAR-001", "Minh", "image/jpeg", sha, master),
        };

        var result = await Transport(handler).GenerateWithReferencesAsync(Secret, Model, Prompt, "16:9", references);

        Assert.True(result.Accepted);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(Secret, handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("key=", handler.Url, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(handler.Body);
        var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Contains("locked master", parts[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal(Convert.ToBase64String(master), parts[1].GetProperty("inline_data").GetProperty("data").GetString());
        Assert.Equal("image/jpeg", parts[1].GetProperty("inline_data").GetProperty("mime_type").GetString());
        Assert.Equal(Prompt, parts[2].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Selected_image_package_is_sent_and_an_unknown_package_is_not()
    {
        var handler = new CaptureHandler(_ => ImageResponse(FinalBytes(), "image/png"));
        var transport = Transport(handler);
        using (ProductionImageSizeChoice.Use(ProductionQualityTiers.NormalizeImage("1K")))
            await transport.GenerateOnceAsync(Secret, Model, Prompt, "16:9");
        using var body = JsonDocument.Parse(handler.Body);
        Assert.Equal("1K", body.RootElement.GetProperty("generationConfig").GetProperty("imageConfig").GetProperty("imageSize").GetString());
        Assert.Equal("512", ProductionQualityTiers.NormalizeImage("0.5K"));
        Assert.Equal(0.067m, ProductionQualityTiers.ImageUsd("1K"));
        Assert.Equal(0.045m, ProductionQualityTiers.ImageUsd("0.5K"));
        var rejected = Assert.Throws<InvalidOperationException>(() => ProductionQualityTiers.NormalizeImage("9K"));
        Assert.Equal(ProductionQualityTiers.UnsupportedImageSize, rejected.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Retired_model_and_missing_key_do_not_send_http()
    {
        var handler = new CaptureHandler(_ => ImageResponse(FinalBytes(), "image/png"));
        var transport = Transport(handler);

        var retired = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.GenerateOnceAsync(Secret, "gemini-2.5-flash-image", Prompt, "16:9"));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.GenerateOnceAsync("  ", Model, Prompt, "16:9"));

        Assert.Equal(ProductionFailureReasons.ProviderModelMismatch, retired.Message);
        Assert.Equal(ProductionCredentialResolver.Unresolved, missing.Message);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, transport.GenerationCount);
    }

    static HttpGeminiTenantImageTransport Transport(CaptureHandler handler) =>
        new(new HttpClient(handler), NullLogger<HttpGeminiTenantImageTransport>.Instance);

    static byte[] FinalBytes() => Encoding.ASCII.GetBytes("final-inline-image-bytes-0123456789");

    static string ImageResponse(byte[] bytes, string mime) =>
        "{\"candidates\":[{\"content\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"" + mime
        + "\",\"data\":\"" + Convert.ToBase64String(bytes) + "\"}}]}}]}";

    sealed class CaptureHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, string> _response;
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string Url { get; private set; } = "";
        public string? ApiKey { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = "";

        public CaptureHandler(Func<HttpRequestMessage, string> response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Url = request.RequestUri?.ToString() ?? "";
            ApiKey = request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response(request), Encoding.UTF8, "application/json"),
            };
        }
    }
}
