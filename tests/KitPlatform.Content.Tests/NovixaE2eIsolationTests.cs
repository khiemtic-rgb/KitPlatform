using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class NovixaE2eIsolationTests
{
    static readonly SemaphoreSlim Gate = new(1, 1);

    [Fact]
    public async Task Disarmed_guard_does_not_touch_a_repository_or_block_a_provider()
    {
        await Gate.WaitAsync();
        try
        {
            using var env = new EnvScope((NovixaE2eIsolation.DatabaseVariable, null));
            Assert.False(NovixaE2eIsolation.IsArmed);
            await NovixaE2eIsolation.AssertAsync(null!, null, CancellationToken.None);
            NovixaE2eIsolation.ThrowIfExternalCall("Creatomate");
        }
        finally
        {
            Gate.Release();
        }
    }

    [Fact]
    public async Task Declared_business_databases_are_refused_before_a_connection()
    {
        string[] names = ["kitplatform", "KITPLATFORM", "kitplatform_test", "pharmacore", "postgres"];
        await Gate.WaitAsync();
        try
        {
            foreach (var name in names)
            {
                using var env = Arm(name, "54329");
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    NovixaE2eIsolation.AssertAsync(null!, UnusedAsset, CancellationToken.None));
                Assert.Contains("database nghiệp vụ", ex.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    [Fact]
    public async Task Business_port_and_invalid_port_are_refused_before_a_connection()
    {
        await Gate.WaitAsync();
        try
        {
            using (var businessPort = Arm("kit_novixa_e2e_declared", "5432"))
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    NovixaE2eIsolation.AssertAsync(null!, UnusedAsset, CancellationToken.None));
                Assert.Contains("5432", ex.Message, StringComparison.Ordinal);
            }

            using (var invalid = Arm("kit_novixa_e2e_declared", "abc"))
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    NovixaE2eIsolation.AssertAsync(null!, UnusedAsset, CancellationToken.None));
                Assert.Contains("port", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    [Fact]
    public async Task Provider_environment_is_refused_before_a_connection()
    {
        await Gate.WaitAsync();
        try
        {
            using var env = Arm("kit_novixa_e2e_declared", "54329");
            env.Set("ELEVENLABS_API_KEY", "not-a-live-key");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NovixaE2eIsolation.AssertAsync(null!, UnusedAsset, CancellationToken.None));
            Assert.Contains("ELEVENLABS_API_KEY", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Gate.Release();
        }
    }

    [Fact]
    public void Armed_guard_blocks_an_external_provider_call_without_opening_a_database()
    {
        Gate.Wait();
        try
        {
            using var env = Arm("kit_novixa_e2e_declared", "54329");
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NovixaE2eIsolation.ThrowIfExternalCall("Creatomate"));
            Assert.Contains("Creatomate", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Gate.Release();
        }
    }

    const string UnusedAsset = @"E:\kit-novixa-e2e-not-used";

    static EnvScope Arm(string database, string port) => new(
        (NovixaE2eIsolation.DatabaseVariable, database),
        (NovixaE2eIsolation.PortVariable, port),
        (NovixaE2eIsolation.AssetVariable, UnusedAsset));

    sealed class EnvScope : IDisposable
    {
        readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvScope(params (string Name, string? Value)[] values)
        {
            foreach (var (name, value) in values)
                Set(name, value);
        }

        public void Set(string name, string? value)
        {
            if (!_previous.ContainsKey(name))
                _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
