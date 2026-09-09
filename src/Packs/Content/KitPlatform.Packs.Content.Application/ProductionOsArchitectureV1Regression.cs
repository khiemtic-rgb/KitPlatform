using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProductionOsArchitectureV1Regression
{
    public const string SuiteId = ProductionOsRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        const string master = "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1";
        const string dna = "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc";
        const string reference = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string contract = "8364a53f00000000000000000000000000000000000000000000000002940e83";

        Ok(ProductionOsRules.ArchitectureId == "PRODUCTION_OS_ARCHITECTURE_V1", "01 architecture id");
        Ok(ProductionOsRules.FamixaLayers.Length == 5 && ProductionOsRules.ReplaceableLayer[0] == "AI_PROVIDERS", "01 Famixa layers vs replaceable providers");
        Ok(ProductionOsRules.AuthorityChain[0] == "MASTER" && ProductionOsRules.AuthorityChain[^1] == "ARTIFACT", "02 authority chain");
        Ok(!ProductionOsRules.ProviderIsAuthority() && !ProductionOsRules.PromptIsAuthority() && !ProductionOsRules.ArtifactIsAuthority(), "02b provider/prompt/artifact are not authority");

        var intent = ProductionOsRules.SampleIntent("CHAR-099", master, dna, reference, contract);
        var canonical = ProductionOsRules.Canonical(intent);
        var sha1 = ProductionOsRules.IntentSha(intent);
        var sha2 = ProductionOsRules.IntentSha(intent);
        var canonical2 = ProductionOsRules.Canonical(intent);
        Ok(sha1 == sha2 && sha1 == canonical.IntentSha256 && sha1.Length == 64, "Intent SHA deterministic");
        Ok(canonical.Text == canonical2.Text && canonical.ShotSize == "medium" && canonical.CameraAngle == "eye_level", "Canonical description deterministic");
        Ok(!ProductionOsRules.ContainsProvider(canonical.Text) && !canonical.Text.Contains("runway_camera", StringComparison.Ordinal), "Canonical has no provider syntax");
        Ok(canonical.Identity == "required" && canonical.Constraints.Contains("no provider rewrite", StringComparison.Ordinal), "Canonical carries identity + constraints");

        var changedAction = intent with { ActionDescription = "Nhân vật ngồi xuống" };
        Ok(ProductionOsRules.IntentSha(changedAction) != sha1, "Intent change changes IntentSha");
        var changedAuthority = intent with { MasterSha256 = "bb439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1" };
        Ok(ProductionOsRules.IntentSha(changedAuthority) != sha1 && ProductionOsRules.SameAuthority(intent, intent), "Authority change changes IntentSha");

        var reqA = new MockProductionProviderA().Compile(intent, canonical);
        var reqB = new MockProductionProviderB().Compile(intent, canonical);
        Ok(reqA.Fields["intentSha"] == sha1 && reqB.Fields["intent_sha256"] == sha1, "Same Intent SHA across adapters");
        Ok(reqA.Fields["kind"] != reqB.Fields["kind"], "Provider request shape may differ");
        Ok(ProductionOsRules.SameAuthority(intent, intent with { ActionDescription = "x" }), "Provider change does not alter authority");

        var video10 = ProductionOsRules.SampleIntent("CHAR-099", master, dna, reference, contract, 10);
        var limited = new ProductionOsRules.ProviderCapabilityProfile(
            "MOCK_SHORT",
            "VIDEO",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VIDEO_GENERATION"] = "SUPPORTED",
                ["IMAGE_TO_VIDEO"] = "SUPPORTED",
                ["DURATION_5S"] = "SUPPORTED",
                ["DURATION_10S"] = "UNSUPPORTED",
            });
        var gate10 = ProductionOsRules.EvaluateCapability(video10, limited, "VIDEO");
        Ok(gate10.Status == "BLOCKED" && gate10.Code == "PROVIDER_CAPABILITY_UNSUPPORTED", "Duration 10 vs 5 → BLOCK");
        Ok(video10.DurationSeconds == 10 && !ProductionOsRules.AutoDowngrade(), "No auto downgrade of Intent");

        var geminiVideo = ProductionOsRules.EvaluateCapability(intent, ProductionOsRules.GeminiImageProfile, "VIDEO");
        Ok(geminiVideo.Status == "BLOCKED", "Gemini image profile cannot satisfy video Intent");
        var runwayVideo = ProductionOsRules.EvaluateCapability(intent, ProductionOsRules.RunwayVideoProfile, "VIDEO");
        Ok(runwayVideo.Status == "READY", "Runway video profile can satisfy 5s I2V");
        var veoVideo = ProductionOsRules.EvaluateCapability(intent, ProductionOsRules.VeoVideoProfile, "VIDEO");
        Ok(veoVideo.Status == "BLOCKED", "Veo catalog is unavailable until wired");

        var geminiImage = ProductionOsRules.EvaluateCapability(intent, ProductionOsRules.GeminiImageProfile, "IMAGE");
        Ok(geminiImage.Status == "NEEDS_PROVIDER_SELECTION", "Gemini CHARACTER_REFERENCE LIMITED → Director selects");
        var geminiImageOpen = ProductionOsRules.EvaluateCapability(intent with { RequiredIdentity = false }, ProductionOsRules.GeminiImageProfile, "IMAGE");
        Ok(geminiImageOpen.Status == "READY", "Gemini IMAGE_GENERATION SUPPORTED when identity not required");
        var runwayImage = ProductionOsRules.EvaluateCapability(intent, ProductionOsRules.RunwayVideoProfile, "IMAGE");
        Ok(runwayImage.Status == "BLOCKED", "Runway IMAGE_GENERATION UNSUPPORTED");

        var limitedRef = new ProductionOsRules.ProviderCapabilityProfile(
            "MOCK_REF",
            "IMAGE",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IMAGE_GENERATION"] = "LIMITED",
            });
        var limitedGate = ProductionOsRules.EvaluateCapability(intent, limitedRef, "IMAGE");
        Ok(limitedGate.Status == "NEEDS_PROVIDER_SELECTION" && limitedGate.Code == "NEEDS_PROVIDER_SELECTION", "LIMITED → NEEDS_PROVIDER_SELECTION");

        var candidates = ProductionOsRules.ListCandidates(intent, "VIDEO", ProductionOsRules.DefaultCatalog);
        Ok(candidates.Any(c => c.ProviderId == "RUNWAY" && c.Status == "READY"), "Selector lists READY Runway");
        Ok(candidates.Any(c => c.ProviderId == "GEMINI" && c.Status == "BLOCKED"), "Selector lists BLOCKED Gemini for video");
        Ok(candidates.Any(c => c.ProviderId == "VEO" && c.Capability == "UNSUPPORTED"), "Selector lists Veo capability");
        Ok(!ProductionOsRules.AutoSelectProvider(), "Selector does not auto-select");

        var undecided = ProductionOsRules.DecideSelection(candidates, null);
        Ok(undecided.Status == "NEEDS_PROVIDER_SELECTION", "One READY provider still needs Director selection");
        var picked = ProductionOsRules.DecideSelection(candidates, "RUNWAY");
        Ok(picked.Status == "READY" && picked.ProviderId == "RUNWAY", "Director may select READY provider");
        var pickedBlocked = ProductionOsRules.DecideSelection(candidates, "VEO");
        Ok(pickedBlocked.Status == "BLOCKED" && pickedBlocked.Code == "PROVIDER_CAPABILITY_UNSUPPORTED", "Director pick of unsupported provider is BLOCKED");
        var missing = ProductionOsRules.DecideSelection(candidates, "UNKNOWN_X");
        Ok(missing.Status == "BLOCKED" && missing.Code == "PROVIDER_UNAVAILABLE", "Unknown provider → BLOCKED");

        var noneReady = ProductionOsRules.ListCandidates(intent, "VIDEO", [ProductionOsRules.GeminiImageProfile, ProductionOsRules.VeoVideoProfile]);
        Ok(ProductionOsRules.DecideSelection(noneReady, null).Code == "PROVIDER_UNAVAILABLE", "No capable provider → BLOCKED");

        Ok(!ProductionOsRules.ContainsProvider("CHAR-001-ERA-01-REF-V1"), "Character Reference id has no provider");
        Ok(!ProductionOsRules.ContainsProvider("CHAR-001-MINH-ERA01-DNA-V1"), "DNA code has no provider");
        Ok(ProductionShotContractRules.ForbiddenModelKeys.Contains("gemini_prompt")
            && ProductionShotContractRules.ForbiddenModelKeys.Contains("runway_prompt"), "Shot Contract forbids provider model keys");
        Ok(!ProductionPromptCompilerRules.ContainsProviderSyntax(canonical.Text), "Prompt Compiler stays provider-neutral");

        var loc = new ProductionOsRules.WorldLocationReference("LOC-001-CLASSROOM-V1", "V1", ["front", "wide", "day"]);
        var obj = new ProductionOsRules.WorldObjectReference("OBJ-001-SCHOOL-BAG-V1", "V1", "canvas bag", "closed");
        Ok(!ProductionOsRules.ContainsProvider(loc.LocationId) && !ProductionOsRules.ContainsProvider(obj.ObjectId), "World refs provider-agnostic");

        Ok(ProductionOsRules.EvaluateAuthority(null, dna, reference, contract, "DIRECTOR_APPROVED").Code == "MASTER_MISSING", "Master missing BLOCK");
        Ok(ProductionOsRules.EvaluateAuthority(master, null, reference, contract, "DIRECTOR_APPROVED").Code == "DNA_MISSING", "DNA missing BLOCK");
        Ok(ProductionOsRules.EvaluateAuthority(master, dna, null, contract, "DIRECTOR_APPROVED").Code == "REFERENCE_MISSING", "Reference missing BLOCK");
        Ok(ProductionOsRules.EvaluateAuthority(master, dna, reference, contract, "PENDING").Code == "DIRECTOR_PENDING", "Director pending BLOCK");
        Ok(ProductionOsRules.EvaluateShaMatch(master, "ffff", "Master").Code == "SHA_MISMATCH", "SHA mismatch BLOCK");
        Ok(ProductionOsRules.EvaluateAuthority(master, dna, reference, contract, "DIRECTOR_APPROVED").Status == "READY", "Authority PASS when complete");

        var fp1 = ProductionOsRules.ExecutionFingerprint(intent, "MOCK_A", "cfg-1");
        var fp2 = ProductionOsRules.ExecutionFingerprint(intent, "MOCK_A", "cfg-1");
        var fpB = ProductionOsRules.ExecutionFingerprint(intent, "MOCK_B", "cfg-1");
        var fpIntent = ProductionOsRules.ExecutionFingerprint(video10, "MOCK_A", "cfg-1");
        Ok(fp1 == fp2 && fp1.Length == 64, "Execution fingerprint deterministic");
        Ok(fp1 != fpB, "Provider change changes fingerprint");
        Ok(fp1 != fpIntent, "Intent V2 changes fingerprint");
        Ok(ProductionOsRules.DuplicatePolicy(fp1, fp1) == "BLOCK_DUPLICATE", "Duplicate fingerprint BLOCK");
        Ok(ProductionOsRules.DuplicatePolicy(fp1, null) == "NEW" && !ProductionOsRules.BlindRetryAllowed(), "No blind retry");

        var provenance = ProductionOsRules.Provenance(intent, promptSha: "p1", generationContractSha: "g1", provider: "MOCK_A", shotId: "66559efe-3021-42f6-89e2-a4d24f83c7f2");
        Ok(provenance.IntentSha256 == sha1 && provenance.MasterSha256 == master && provenance.ShotId is not null, "Provenance traces authority + intent");
        Ok(provenance.ProviderRequestId is null, "Missing providerRequestId stays null");

        var artifact = new ProductionOsRules.ProductionArtifact("art-1", "ex-1", "MOCK_A", null, null, "image/png", 1, "aa", DateTimeOffset.UnixEpoch);
        Ok(artifact.ProviderRequestId is null && !ProductionOsRules.ArtifactMayMutateAuthority() && !ProductionOsRules.ArtifactIsAuthority(), "Artifact is output only");

        Ok(!ProductionOsRules.MayOverwritePayload("LOCKED") && !ProductionOsRules.MayOverwritePayload("DIRECTOR_APPROVED"), "Versioning does not overwrite locked");
        Ok(ProductionOsRules.NextVersion("V1") == "V2" && ProductionOsRules.MayOverwritePayload("DRAFT"), "Change uses V2");

        Ok(!ProductionOsRules.MayCompileRequest(true, true, true, true, true, false), "No compile before Director provider selection");
        Ok(ProductionOsRules.MayCompileRequest(true, true, true, true, true, true), "Compile only after all gates");

        var realBlocked = false;
        try { ProductionOsRules.CompileForProvider(intent, canonical, "RUNWAY"); }
        catch (InvalidOperationException) { realBlocked = true; }
        Ok(realBlocked, "Real provider adapter not invoked");

        Ok(ProductionOsRules.NormalizeQa("UNKNOWN") == "UNKNOWN" && !ProductionOsRules.UnknownBecomesPass("UNKNOWN"), "UNKNOWN does not become PASS");
        Ok(!ProductionOsRules.AutoFix() && !ProductionOsRules.AutoApprove() && !ProductionOsRules.AutoLock(), "No auto-fix/approve/lock");
        Ok(!ProductionOsRules.CreatesPixels("DESCRIBE") && ProductionOsRules.CreatesPixels("GEMINI") && ProductionOsRules.CreatesPixels("RUNWAY") && ProductionOsRules.CreatesPixels("VEO"), "Architecture path does not generate");

        var leaked = false;
        try { ProductionOsRules.EnsureProviderAgnostic("scene", "runway_prompt please"); }
        catch (InvalidOperationException) { leaked = true; }
        Ok(leaked, "Provider leak rejected");

        Ok(typeof(IProductionProvider).Namespace == "KitPlatform.Packs.Content", "IProductionProvider is Application contract");
        Ok(typeof(IImageGenerationProvider).IsAssignableFrom(typeof(IGeminiImageGenerationProvider)), "Gemini image provider is an IImageGenerationProvider");
        Ok(typeof(IVideoGenerationProvider).Name == "IVideoGenerationProvider", "Video execution uses generic provider port");
        Ok(typeof(MockProductionProviderA).IsAssignableTo(typeof(IProductionProvider)), "Mock adapters implement IProductionProvider");

        var appDir = FindSourceDir("src/Packs/Content/KitPlatform.Packs.Content.Application");
        if (appDir is null)
        {
            fail.Add("Application source dir not found for coupling scan");
        }
        else
        {
            var files = Directory.GetFiles(appDir, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Contains("Regression", StringComparison.OrdinalIgnoreCase));
            var leakedClient = files.Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("ContentGeminiClient", StringComparison.Ordinal)
                    || text.Contains("ContentRunwayClient", StringComparison.Ordinal)
                    || text.Contains("Google.Cloud.AI", StringComparison.Ordinal)
                    || text.Contains("Runway.Sdk", StringComparison.Ordinal);
            }).Select(Path.GetFileName).ToList();
            Ok(leakedClient.Count == 0, "Application has no Gemini/Runway client or SDK");

            var os = File.ReadAllText(Path.Combine(appDir, "ProductionOsRules.cs"));
            var intentStart = os.IndexOf("public sealed record ProductionIntent", StringComparison.Ordinal);
            var intentEnd = os.IndexOf("public sealed record CanonicalProductionDescription", StringComparison.Ordinal);
            var intentSlice = intentStart >= 0 && intentEnd > intentStart ? os[intentStart..intentEnd] : "";
            Ok(intentSlice.Length > 0
                && !intentSlice.Contains("Provider", StringComparison.Ordinal)
                && !intentSlice.Contains("gemini", StringComparison.OrdinalIgnoreCase)
                && !intentSlice.Contains("runway", StringComparison.OrdinalIgnoreCase)
                && !intentSlice.Contains("gen4_turbo", StringComparison.OrdinalIgnoreCase),
                "Production Intent schema has no provider");

            var compiler = File.ReadAllText(Path.Combine(appDir, "ProductionPromptCompilerRules.cs"));
            Ok(!compiler.Contains("ContentGeminiClient", StringComparison.Ordinal)
                && !compiler.Contains("ContentRunwayClient", StringComparison.Ordinal)
                && compiler.Contains("ContainsProviderSyntax", StringComparison.Ordinal),
                "Prompt Compiler has no provider client");
        }

        return fail;
    }

    private static string? FindSourceDir(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
