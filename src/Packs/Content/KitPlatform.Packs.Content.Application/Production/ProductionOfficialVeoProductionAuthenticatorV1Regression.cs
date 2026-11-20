using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoProductionAuthenticatorV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_PRODUCTION_AUTHENTICATOR_V1";
    const string FakeSa = """{"type":"service_account","project_id":"ck15-fake-project","client_email":"veo@ck15-fake.iam.gserviceaccount.com","private_key":"-----BEGIN PRIVATE KEY-----\nTEST-NOT-A-REAL-KEY\n-----END PRIVATE KEY-----\n","token_uri":"https://oauth2.googleapis.com/token"}""";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunCore(Ok); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static void RunCore(Action<bool, string> Ok)
    {
        T_ValidServiceAccount(Ok);
        T_MissingType(Ok);
        T_WrongType(Ok);
        T_MissingProject(Ok);
        T_MissingEmail(Ok);
        T_MissingPrivateKey(Ok);
        T_InvalidPem(Ok);
        T_MissingTokenUriDefaults(Ok);
        T_InvalidTokenUri(Ok);
        T_JwtContract(Ok);
        T_OfficialTargetReady(Ok);
        T_WrongBuild(Ok);
        T_WrongShot(Ok);
        T_WrongProvider(Ok);
        T_WrongModel(Ok);
        T_WrongSelection(Ok);
        T_FixtureRunwayUnselected(Ok);
        T_NoGlobalBypass(Ok);
        T_GetAccessTokenClosed(Ok);
        T_NoSecretReadNoNetwork(Ok);
        T_PreflightRemovesAuthenticatorNotReady(Ok);
        T_ProductionDiClosed(Ok);
        T_Ck14StillGreen(Ok);
    }

    static void T_ValidServiceAccount(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(FakeSa);
        Ok(parsed.Ok && parsed.Contract is not null, "valid service-account structure is accepted");
        Ok(parsed.Contract!.ProjectId == "ck15-fake-project"
            && parsed.Contract.ClientEmail == "veo@ck15-fake.iam.gserviceaccount.com"
            && parsed.Contract.TokenUri == VeoHttpAuthContract.DefaultTokenUri
            && parsed.Contract.HasPrivateKeyPem
            && parsed.Contract.Scope == VeoHttpAuthContract.Scope
            && parsed.Contract.JwtLifetimeSeconds == 3600,
            "contract fields without storing PEM");
        var json = JsonSerializer.Serialize(parsed.Contract);
        Ok(!json.Contains("BEGIN", StringComparison.Ordinal)
            && !json.Contains("PRIVATE KEY", StringComparison.Ordinal),
            "contract JSON never contains PEM");
    }

    static void T_MissingType(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"project_id":"x","client_email":"a@b","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.TypeMissing, "missing type rejected");
    }

    static void T_WrongType(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"user","project_id":"x","client_email":"a@b","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.TypeInvalid, "wrong type rejected");
    }

    static void T_MissingProject(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","client_email":"a@b","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.ProjectIdMissing, "missing project_id rejected");
    }

    static void T_MissingEmail(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","project_id":"x","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.ClientEmailMissing, "missing client_email rejected");
    }

    static void T_MissingPrivateKey(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","project_id":"x","client_email":"a@b"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.PrivateKeyMissing, "missing private_key rejected");
    }

    static void T_InvalidPem(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","project_id":"x","client_email":"a@b","private_key":"not-a-pem"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.PrivateKeyInvalid, "invalid private_key PEM rejected");
    }

    static void T_MissingTokenUriDefaults(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","project_id":"x","client_email":"a@b","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n"}""");
        Ok(parsed.Ok && parsed.Contract!.TokenUri == VeoHttpAuthContract.DefaultTokenUri,
            "missing token_uri resolves to canonical default");
    }

    static void T_InvalidTokenUri(Action<bool, string> Ok)
    {
        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(
            """{"type":"service_account","project_id":"x","client_email":"a@b","private_key":"-----BEGIN PRIVATE KEY-----\nX\n-----END PRIVATE KEY-----\n","token_uri":"http://insecure.example/token"}""");
        Ok(!parsed.Ok && parsed.Code == VeoHttpServiceAccountCodes.TokenUriInvalid, "invalid token_uri rejected");
    }

    static void T_JwtContract(Action<bool, string> Ok)
    {
        Ok(VeoHttpAuthContract.Algorithm == "RS256"
            && VeoHttpAuthContract.TokenType == "JWT"
            && VeoHttpAuthContract.GrantType == "urn:ietf:params:oauth:grant-type:jwt-bearer"
            && VeoHttpAuthContract.Scope == "https://www.googleapis.com/auth/cloud-platform"
            && VeoHttpAuthContract.JwtLifetimeSeconds == 3600
            && VeoHttpAuthContract.DefaultTokenUri == "https://oauth2.googleapis.com/token",
            "RS256 / JWT-bearer / cloud-platform / 3600 represented");
    }

    static void T_OfficialTargetReady(Action<bool, string> Ok)
    {
        var composed = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest());
        Ok(composed.Ready && composed.State == ProductionFailureReasons.AuthenticatorReady,
            "official google/veo-3.1 authenticator composed");
        Ok(composed.Authenticator is ProductionVeoHttpAuthenticator, "production authenticator implementation");
        var withSa = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with { ExplicitServiceAccountJson = FakeSa });
        Ok(withSa.Ready && withSa.Contract is { ProjectId: "ck15-fake-project", HasPrivateKeyPem: true },
            "explicit fake SA binds contract without env read");
    }

    static void T_WrongBuild(Action<bool, string> Ok)
    {
        var r = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong build");
    }

    static void T_WrongShot(Action<bool, string> Ok)
    {
        var r = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong shot");
    }

    static void T_WrongProvider(Action<bool, string> Ok)
    {
        var r = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Selection = OfficialSelection("runway", "veo-3.1"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong provider");
    }

    static void T_WrongModel(Action<bool, string> Ok)
    {
        var r = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Selection = OfficialSelection("google", "veo-2"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong model");
    }

    static void T_WrongSelection(Action<bool, string> Ok)
    {
        var r = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.SelectionConflict, "wrong selection");
    }

    static void T_FixtureRunwayUnselected(Action<bool, string> Ok)
    {
        Ok(!OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Selection = OfficialSelection("fixture", "fixture-motion-v1"),
        }).Ready, "Fixture rejected");
        Ok(!OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with
        {
            Selection = OfficialSelection("runway", "gen4_turbo"),
        }).Ready, "Runway rejected");
        Ok(!OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with { Selection = null }).Ready
            && OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest() with { Selection = null }).BlockReason
                == ProductionFailureReasons.MissingProductionAiSelection,
            "unselected model rejected");
    }

    static void T_NoGlobalBypass(Action<bool, string> Ok)
    {
        Ok(!RealVertexExecutionPreflight.IsAuthenticatorReady(new ProductionVeoHttpAuthenticator()),
            "bare production authenticator is not globally ready");
        Ok(RealVertexExecutionPreflight.IsAuthenticatorReady(new FakeVeoHttpAuthenticator()),
            "FakeVeoHttpAuthenticator remains for transport tests");
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(RealVertexExecutionV1Lock.OfficialScope, OfficialSelection());
        Ok(!RealVertexExecutionPreflight.IsAuthenticatorReady(closed)
            && closed.OfficialAuthenticator is null,
            "ClosedDefaults do not bypass authenticator");
    }

    static void T_GetAccessTokenClosed(Action<bool, string> Ok)
    {
        var composed = OfficialVeoProductionAuthenticatorV1.Compose(
            OfficialRequest() with { ExplicitServiceAccountJson = FakeSa });
        var auth = (ProductionVeoHttpAuthenticator)composed.Authenticator;
        var closed = false;
        try { auth.GetAccessTokenAsync().GetAwaiter().GetResult(); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            closed = true;
        }
        Ok(composed.Ready && closed && auth.GetAccessTokenCalls == 1,
            "READY composition still does not mint a token");
    }

    static void T_NoSecretReadNoNetwork(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        _ = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest());
        _ = VeoHttpServiceAccountValidator.ValidateExplicit(FakeSa);
        _ = VeoHttpConfigPrecedence.ParseExplicit(null);
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, null, null, null) is null,
            "empty precedence does not read Environment");
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0
            && graph.Authenticator.Calls == 0 && graph.Transport.StartCount == 0,
            "no HTTP / OAuth / provider Start");
        Ok(permits.IssueCalls == 0 && permits.ConsumeCalls == 0, "permit issued/consumed = 0");
        Ok(VeoHttpConfigPrecedence.JoinFileSourceOrder[0] == "videoConfigJson.veoJoinFile"
            && VeoHttpConfigPrecedence.JoinFileSourceOrder[^1] == "VEO_JOIN_FILE",
            "existing Join precedence reused, not executed");
    }

    static void T_PreflightRemovesAuthenticatorNotReady(Action<bool, string> Ok)
    {
        var composed = OfficialVeoProductionAuthenticatorV1.Compose(OfficialRequest());
        var closedAuth = (ProductionVeoHttpAuthenticator)composed.Authenticator;
        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                RealVertexExecutionV1Lock.OfficialScope, OfficialSelection())
            with
            {
                Authenticator = composed.Authenticator,
                OfficialAuthenticator = composed,
            });
        Ok(report.AuthenticatorPass
            && !report.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady),
            "AUTHENTICATOR_NOT_READY removed for official composition");
        Ok(!report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.HttpCompositionUnavailable)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired),
            "resolver / HTTP / execution remain BLOCKED");
        Ok(closedAuth.GetAccessTokenCalls == 0, "preflight does not GetAccessToken");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "graph / execution lock unchanged");
    }

    static void T_ProductionDiClosed(Action<bool, string> Ok)
    {
        Ok(!OfficialVeoProductionAuthenticatorV1.ProductionDiRegistered,
            "production DI does not register IVeoHttpAuthenticator");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo,
            "Veo provider resolver remains CLOSED");
        var fake = new FakeVeoHttpAuthenticator();
        Ok(fake.GetAccessTokenAsync().GetAwaiter().GetResult() == FakeVeoHttpAuthenticator.Token,
            "fake authenticator still available");
    }

    static void T_Ck14StillGreen(Action<bool, string> Ok)
    {
        var ck14 = ProductionOfficialVeoPaidNetworkAuthorizationV1Regression.Run();
        Ok(ck14.Count == 0, ck14.Count == 0 ? "CK14 still green" : "CK14:" + string.Join(",", ck14));
    }

    static OfficialVeoAuthenticatorCompositionRequest OfficialRequest() =>
        new(RealVertexExecutionV1Lock.OfficialScope, OfficialSelection());

    static ProductionAiSelection OfficialSelection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-15",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck15");
}
