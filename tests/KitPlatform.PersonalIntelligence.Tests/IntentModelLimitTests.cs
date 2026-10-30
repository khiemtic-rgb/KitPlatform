using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intent;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class IntentModelLimitTests
{
    [Fact]
    public void Disabled_model_starts_without_a_credential()
    {
        var options = new BusinessIntentModelOptions();
        options.EnsureStartupSafe();
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Enabled_model_without_a_credential_is_refused_before_startup()
    {
        var options = new BusinessIntentModelOptions { Enabled = true, Model = "local" };
        var error = Assert.Throws<InvalidOperationException>(options.EnsureStartupSafe);
        Assert.Contains("ApiKey", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_model_rejects_an_unbounded_completion()
    {
        var options = Configured();
        options.MaxOutputTokens = 100_000;
        Assert.False(options.IsConfigured);
        Assert.Throws<InvalidOperationException>(options.EnsureStartupSafe);
    }

    [Fact]
    public async Task Configured_request_sends_one_call_with_a_token_cap_and_then_stops()
    {
        var bodies = new List<string>();
        var handler = new CaptureHandler(bodies, """{"choices":[{"message":{"content":"{\"goal\":\"read the publication\",\"bindings\":[]}"}}]}""");
        var options = Configured();
        options.MaxCallsPerProcess = 1;
        var interpreter = new CatalogGroundedIntentInterpreter(new HttpClient(handler), Options.Create(options));
        var catalog = PlatformCapabilityCatalog.All;
        var context = new InterpretationContext(Guid.NewGuid(), false, []);

        var first = await interpreter.InterpretAsync("read", catalog, context, CancellationToken.None);
        var second = await interpreter.InterpretAsync("read again", catalog, context, CancellationToken.None);

        Assert.IsType<InterpretationOutcome.Ready>(first);
        var blocked = Assert.IsType<InterpretationOutcome.Unreadable>(second);
        Assert.Equal("model_call_budget", blocked.Reason);
        Assert.Single(bodies);
        using var sent = JsonDocument.Parse(bodies[0]);
        Assert.Equal(512, sent.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0, sent.RootElement.GetProperty("temperature").GetInt32());
        Assert.DoesNotContain("test-key-not-a-secret", bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_timeout_does_not_run_a_second_attempt()
    {
        var calls = 0;
        var handler = new HoldHandler(() => calls++);
        var options = Configured();
        options.RequestTimeoutSeconds = 5;
        var interpreter = new CatalogGroundedIntentInterpreter(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) }, Options.Create(options));

        var outcome = await interpreter.InterpretAsync("read", PlatformCapabilityCatalog.All, new InterpretationContext(Guid.NewGuid(), false, []), CancellationToken.None);

        var blocked = Assert.IsType<InterpretationOutcome.Unreadable>(outcome);
        Assert.Equal("model_unreachable", blocked.Reason);
        Assert.Equal(1, calls);
    }

    static BusinessIntentModelOptions Configured() => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:9/v1",
        ApiKey = "test-key-not-a-secret",
        Model = "local-test",
    };

    sealed class CaptureHandler(List<string> bodies, string response) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-key-not-a-secret", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }

    sealed class HoldHandler(Action onCall) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onCall();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
