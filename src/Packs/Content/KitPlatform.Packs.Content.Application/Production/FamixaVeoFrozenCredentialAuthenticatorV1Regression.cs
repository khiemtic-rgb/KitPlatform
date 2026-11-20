using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Controlled Veo authenticator. Fake service-account keys stay inside the test.
/// No Join file, environment credential, or provider socket.
/// </summary>
public static class FamixaVeoFrozenCredentialAuthenticatorV1Regression
{
    public const string SuiteId = "FAMIXA_VIDEO_RECOVERY_002_VEO_AUTHENTICATOR";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            RunAsync(Ok).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var secret = ServiceAccountJson("recovery-project", "veo@recovery.iam.gserviceaccount.com", pem);
        Ok(!secret.Contains("\nBEGIN", StringComparison.Ordinal) || secret.Contains("\\n", StringComparison.Ordinal),
            "service-account JSON keeps the PEM inside the secret document");

        var closed = new ProductionVeoHttpAuthenticator(tokens: new ScriptedTokens());
        var unscoped = false;
        try
        {
            await closed.GetAccessTokenAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            unscoped = true;
        }
        Ok(unscoped && closed.GetAccessTokenCalls == 1, "unscoped GetAccessToken stays closed");
        var scriptedClosed = new ScriptedTokens();
        var authClosed = new ProductionVeoHttpAuthenticator(null, scriptedClosed);
        var stillClosed = false;
        try { await authClosed.GetAccessTokenAsync(); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            stillClosed = true;
        }
        Ok(stillClosed && scriptedClosed.Calls == 0, "a bound token client is not used without a frozen credential");

        var noClient = new ProductionVeoHttpAuthenticator();
        var material = Material(secret);
        var blocked = false;
        try { await noClient.AuthorizeFrozenAsync(material); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            blocked = true;
        }
        Ok(blocked
           && noClient.AcceptedCredentialId == material.CredentialId
           && noClient.AcceptedCredentialVersionId == material.CredentialVersionId
           && noClient.GetAccessTokenCalls == 1,
            "valid service account without a token client is accepted and still closed");

        var badPem = new ProductionVeoHttpAuthenticator(tokens: new ScriptedTokens());
        var bad = false;
        try
        {
            await badPem.AuthorizeFrozenAsync(Material(
                ServiceAccountJson("recovery-project", "veo@recovery.iam.gserviceaccount.com",
                    "-----BEGIN PRIVATE KEY-----\nNOT-A-KEY\n-----END PRIVATE KEY-----\n")));
        }
        catch (InvalidOperationException ex) when (ex.Message == VeoHttpServiceAccountCodes.PrivateKeyInvalid)
        {
            bad = !ex.ToString().Contains("NOT-A-KEY", StringComparison.Ordinal);
        }
        Ok(bad && badPem.AcceptedCredentialId is null && badPem.GetAccessTokenCalls == 0,
            "invalid PEM is rejected before accept and before token exchange");

        var mismatched = new ProductionVeoHttpAuthenticator(
            new VeoHttpCredentialContract("other-project", "other@recovery.iam.gserviceaccount.com", VeoHttpAuthContract.DefaultTokenUri, true),
            new ScriptedTokens());
        var mismatch = false;
        try { await mismatched.AuthorizeFrozenAsync(material); }
        catch (InvalidOperationException ex) when (ex.Message == VeoFrozenCredentialCodes.ContractMismatch)
        {
            mismatch = true;
        }
        Ok(mismatch && mismatched.AcceptedCredentialId is null, "bound contract project and email must match the secret");

        var wrongProvider = false;
        try
        {
            await new ProductionVeoHttpAuthenticator(tokens: new ScriptedTokens()).AuthorizeFrozenAsync(
                new ProductionCredentialMaterial(Guid.NewGuid(), Guid.NewGuid(), "runway", "veo-3.1", secret));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.ProviderMismatch)
        {
            wrongProvider = true;
        }
        Ok(wrongProvider, "non-google material is rejected");

        var missing = false;
        try
        {
            await new ProductionVeoHttpAuthenticator(tokens: new ScriptedTokens()).AuthorizeFrozenAsync(
                new ProductionCredentialMaterial(Guid.Empty, Guid.NewGuid(), "google", "veo-3.1", secret));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missing = true;
        }
        Ok(missing, "missing credential id is rejected");

        await T_SuccessPath(Ok, secret, pem);
        await T_TokenFailure(Ok, secret);
        await T_NetworkFailure(Ok, secret);
        await T_MissingFrozenCredential(Ok, secret);
        await T_WrongTenant(Ok, secret);
        await T_RevokedDoesNotOpen(Ok, secret);
        await T_SupersededPinStillOpens(Ok, secret);

        var source = File.ReadAllText(Path.Combine(RepoRoot(),
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers",
            "ProductionVeoHttpAuthenticator.cs"));
        Ok(!source.Contains("GetEnvironmentVariable", StringComparison.Ordinal)
           && !source.Contains("OfficialVeoJoinFileHttpAuthenticator", StringComparison.Ordinal)
           && !source.Contains("GOOGLE_APPLICATION_CREDENTIALS", StringComparison.Ordinal),
            "production authenticator does not read env, Join, or ADC");
    }

    static async Task T_SuccessPath(Action<bool, string> Ok, string secret, string pem)
    {
        var tokens = new FakeVeoHttpMessageHandler
        {
            OnSend = capture =>
            {
                Ok(capture.Url == VeoHttpAuthContract.DefaultTokenUri, "token exchange uses the service-account token URI");
                Ok(capture.Body.Contains("grant_type=" + Uri.EscapeDataString(VeoHttpAuthContract.GrantType), StringComparison.Ordinal)
                   || capture.Body.Contains("urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", StringComparison.Ordinal),
                    "token exchange uses the JWT-bearer grant");
                Ok(!capture.Body.Contains(pem, StringComparison.Ordinal)
                   && !capture.Body.Contains("PRIVATE KEY", StringComparison.Ordinal),
                    "token request does not carry the PEM");
                return FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"ya29.recovery-test-token","expires_in":3600}""");
            },
        };
        var vertex = new FakeVeoHttpMessageHandler();
        var (adapter, auth) = Wire(secret, new HttpClient(tokens), new HttpClient(vertex));
        var started = await adapter.StartFrozenAsync(Snap(auth.CredentialId, auth.VersionId), new MotionStartContext(Keyframe(), "image/png"), default);
        Ok(started.ProviderJobId.Length > 0
           && tokens.Requests.Count == 1
           && vertex.Requests.Count == 1
           && vertex.Last!.Scheme == "Bearer"
           && vertex.Last.Token == "ya29.recovery-test-token"
           && !vertex.Last.Body.Contains("PRIVATE KEY", StringComparison.Ordinal)
           && !vertex.Last.Body.Contains("ya29.recovery-test-token", StringComparison.Ordinal),
            "valid frozen credential authorizes the Vertex request and does not put the token in the body");
        Ok(auth.Authenticator.AcceptedCredentialId == auth.CredentialId
           && auth.Authenticator.AcceptedCredentialVersionId == auth.VersionId,
            "the frozen credential version is the one that was used");
    }

    static async Task T_TokenFailure(Action<bool, string> Ok, string secret)
    {
        var tokens = new FakeVeoHttpMessageHandler
        {
            OnSend = _ => FakeVeoHttpMessageHandler.Json(HttpStatusCode.Unauthorized, """{"error":"invalid_grant"}"""),
        };
        var vertex = new FakeVeoHttpMessageHandler();
        var (adapter, ids) = Wire(secret, new HttpClient(tokens), new HttpClient(vertex));
        var failed = false;
        try
        {
            await adapter.StartFrozenAsync(Snap(ids.CredentialId, ids.VersionId), new MotionStartContext(Keyframe(), "image/png"), default);
        }
        catch (InvalidOperationException ex) when (ex.Message == FamixaProviderErrorCodes.AuthFailed)
        {
            failed = !ex.ToString().Contains("PRIVATE KEY", StringComparison.Ordinal);
        }
        Ok(failed && tokens.Requests.Count == 1 && vertex.Requests.Count == 0,
            "token rejection stops before the Vertex request");
    }

    static async Task T_NetworkFailure(Action<bool, string> Ok, string secret)
    {
        var tokens = new FakeVeoHttpMessageHandler { ThrowBeforeCapture = true };
        var vertex = new FakeVeoHttpMessageHandler();
        var (adapter, ids) = Wire(secret, new HttpClient(tokens), new HttpClient(vertex));
        var failed = false;
        try
        {
            await adapter.StartFrozenAsync(Snap(ids.CredentialId, ids.VersionId), new MotionStartContext(Keyframe(), "image/png"), default);
        }
        catch (InvalidOperationException ex) when (ex.Message == FamixaProviderErrorCodes.Timeout)
        {
            failed = true;
        }
        Ok(failed && vertex.Requests.Count == 0 && vertex.PredictCalls == 0,
            "token network failure does not start a Vertex job");
    }

    static async Task T_MissingFrozenCredential(Action<bool, string> Ok, string secret)
    {
        var tokens = new FakeVeoHttpMessageHandler();
        var vertex = new FakeVeoHttpMessageHandler();
        var (adapter, auth) = Wire(secret, new HttpClient(tokens), new HttpClient(vertex));
        var missing = false;
        try
        {
            await adapter.StartFrozenAsync(Snap(null, null), new MotionStartContext(Keyframe(), "image/png"), default);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missing = true;
        }
        Ok(missing && tokens.Requests.Count == 0 && vertex.Requests.Count == 0 && auth.Authenticator.GetAccessTokenCalls == 0,
            "missing frozen credential fails before token exchange");
    }

    static async Task T_WrongTenant(Action<bool, string> Ok, string secret)
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new Caller(tenantA);
        var opens = 0;
        var storedCredential = await Store(caller, secret, () => opens++);
        caller.Id = tenantB;
        var wrong = false;
        try
        {
            await storedCredential.Resolver.ResolveAsync(
                storedCredential.CredentialId, storedCredential.VersionId, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.SecretUnavailable)
        {
            wrong = true;
        }
        Ok(wrong && opens == 0, "another tenant cannot open the credential ciphertext");
    }

    static async Task T_RevokedDoesNotOpen(Action<bool, string> Ok, string secret)
    {
        var caller = new Caller(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"));
        var opens = 0;
        var storedCredential = await Store(caller, secret, () => opens++);
        await storedCredential.Identities.RevokeVersionAsync(storedCredential.VersionId);
        var revoked = false;
        try
        {
            await storedCredential.Resolver.ResolveAsync(
                storedCredential.CredentialId, storedCredential.VersionId, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Revoked)
        {
            revoked = true;
        }
        Ok(revoked && opens == 0, "REVOKED version does not open ciphertext");
    }

    static async Task T_SupersededPinStillOpens(Action<bool, string> Ok, string secret)
    {
        var caller = new Caller(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"));
        var storedCredential = await Store(caller, secret, null);
        storedCredential.Identities.SupersedeActiveVersions(storedCredential.CredentialId);
        var material = await storedCredential.Resolver.ResolveAsync(
            storedCredential.CredentialId, storedCredential.VersionId, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId);
        Ok(material.CredentialVersionId == storedCredential.VersionId && material.Secret == secret,
            "an explicit SUPERSEDED pin still resolves that version");
        var tokens = new ScriptedTokens();
        var auth = new ProductionVeoHttpAuthenticator(null, tokens);
        var token = await auth.AuthorizeFrozenAsync(material);
        Ok(token == tokens.Token && tokens.Calls == 1 && auth.AcceptedCredentialVersionId == storedCredential.VersionId,
            "a superseded pin can still mint a token for that version");
    }

    static (VeoMotionExecutionAdapter Adapter, WiredIds Ids) Wire(string secret, HttpClient tokens, HttpClient vertex)
    {
        var caller = new Caller(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"));
        var built = Store(caller, secret, null).GetAwaiter().GetResult();
        var auth = new ProductionVeoHttpAuthenticator(null, new HttpVeoJwtBearerTokenClient(tokens));
        var transport = new HttpVeoMotionTransport(
            vertex, auth, new VeoHttpSettings("recovery-project"), built.Resolver);
        return (
            new VeoMotionExecutionAdapter(transport),
            new WiredIds(built.CredentialId, built.VersionId, auth));
    }

    static async Task<WiredStore> Store(Caller caller, string secret, Action? opened)
    {
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        IProductionCredentialSecretStore store = opened is null ? secrets : new CountingSecrets(secrets, opened);
        var credentialId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await identities.CreateAsync(ProductionCredentialOwnership.Create(credentialId, caller.Id, "recovery-veo"));
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            versionId, credentialId, caller.Id, 1, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId));
        await store.PutAsync(versionId, secret);
        return new WiredStore(
            new ProductionCredentialResolver(identities, store, caller),
            identities,
            credentialId,
            versionId);
    }

    static ProductionCredentialMaterial Material(string secret) =>
        new(Guid.NewGuid(), Guid.NewGuid(), FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId, secret);

    static string ServiceAccountJson(string project, string email, string pem) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = project,
            ["client_email"] = email,
            ["private_key"] = pem,
            ["token_uri"] = VeoHttpAuthContract.DefaultTokenUri,
        });

    static byte[] Keyframe()
    {
        var bytes = new byte[32];
        bytes[0] = 9;
        return bytes;
    }

    static ProductionTaskInputSnapshot Snap(Guid? credentialId, Guid? versionId)
    {
        var bytes = Keyframe();
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var text = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            text,
            "He stays in place.",
            "sofa 16:9",
            "",
            text,
            text,
            ShotExecutionCompiler.Hash(text),
            ShotExecutionCompiler.Hash("He stays in place."),
            ShotExecutionCompiler.Hash("sofa 16:9"),
            ShotExecutionCompiler.Hash(text),
            hash,
            "",
            "",
            "",
            ShotExecutionCompiler.Hash("8.0s"),
            FamixaProviderIds.Google,
            ProductionMotionCatalog.Veo31ModelId,
            "",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "16:9",
            8,
            ShotExecutionCompiler.Version,
            null,
            CredentialId: credentialId,
            CredentialVersionId: versionId);
        return snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\KitPlatform";
    }

    sealed record WiredIds(Guid CredentialId, Guid VersionId, ProductionVeoHttpAuthenticator Authenticator);

    sealed record WiredStore(
        ProductionCredentialResolver Resolver,
        InMemoryProductionCredentialStore Identities,
        Guid CredentialId,
        Guid VersionId);

    sealed class Caller(Guid id) : IProductionCallerTenant
    {
        public Guid Id { get; set; } = id;
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class ScriptedTokens : IVeoJwtBearerTokenClient
    {
        public int Calls { get; private set; }
        public string Token { get; } = "ya29.scripted-token";

        public Task<string> ExchangeAsync(string tokenUri, string assertion, CancellationToken ct)
        {
            Calls++;
            if (string.IsNullOrWhiteSpace(assertion) || assertion.Split('.').Length != 3)
                throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
            return Task.FromResult(Token);
        }
    }

    sealed class CountingSecrets(IProductionCredentialSecretStore inner, Action opened) : IProductionCredentialSecretStore
    {
        public Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default) =>
            inner.PutAsync(credentialVersionId, secret, ct);

        public Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default)
        {
            opened();
            return inner.OpenAsync(credentialVersionId, ct);
        }
    }
}
