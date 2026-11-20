using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class FamixaVideoRecovery002Tests
{
    [Fact]
    public void Veo_frozen_credential_authenticator_uses_only_a_valid_owned_secret()
    {
        var fail = FamixaVeoFrozenCredentialAuthenticatorV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Sh02_downstream_readiness_rejects_known_defects()
    {
        var fail = FamixaSh02DownstreamReadinessV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
