using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class ProductionVideoContractV1Regression
{
    public const string SuiteId = "PRODUCTION_VIDEO_CONTRACT_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var masterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dnaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var prpId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var shotId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var contractId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var stillId = Guid.Parse("7ed003d7-789a-41ef-bcdf-a88637ff14d2");
        var sha = new string('a', 64);
        var shot = ProductionShotContractRules.BuildValidFixture("CHAR-099", "FAMIXA", "ERA-01", masterId, sha, dnaId, sha, prpId, sha);
        var contractSha = ProductionShotContractRules.HashCanonical(shot);
        var payload = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot);
        var dna = CharacterIdentityGovernanceRules.BuildGenericDna("9", ["different hair identity", "long", "shoulder_length"]);
        var locked = new CharacterIdentityGovernanceRules.AuthorityState("CHAR-099", true, true, true, true, true, true, "LOCKED", "LOCKED", "LOCKED", masterId, dnaId, prpId);
        var govPass = CharacterIdentityGovernanceRules.Evaluate(dna, null, ProductionShotContractRules.ToGovernanceShotSpec(shot), null, ProductionShotContractRules.ToGovernancePrompt(shot), locked);

        ProductionVideoContractRules.GateOutput Ready(
            string exec = "IMAGE_APPROVED",
            string review = "APPROVED",
            bool master = true, bool dnaOk = true, bool prp = true, bool govOk = true,
            IReadOnlyList<CharacterIdentityGovernanceRules.Conflict>? conflicts = null,
            string? liveMaster = null, string? liveDna = null, string? livePrp = null,
            string? liveContract = null, string? liveStill = null,
            bool artifact = true, bool readable = true,
            JsonElement? body = null,
            string shotStatus = "DIRECTOR_APPROVED",
            Guid? requestedStill = null) =>
            ProductionVideoContractRules.Evaluate(new ProductionVideoContractRules.GateInput(
                "CHAR-099", master, dnaOk, prp, govOk, conflicts ?? [],
                shotStatus, exec, review, artifact, readable,
                sha, liveMaster ?? sha, sha, liveDna ?? sha, sha, livePrp ?? sha,
                contractSha, liveContract ?? contractSha, sha, liveStill ?? sha,
                requestedStill, stillId, body ?? payload));

        Ok(Ready().Status == "PASS" && Ready().Generation == false, "01 authority + approved still → PASS");
        Ok(!Ready(exec: "READY_FOR_DIRECTOR", review: "PENDING").CanPass(), "02 READY_FOR_DIRECTOR still → BLOCK");
        Ok(!Ready(exec: "IMAGE_REJECTED", review: "REJECTED").CanPass(), "03 rejected still → BLOCK");
        Ok(!Ready(review: "SUPERSEDED").CanPass(), "04 superseded still → BLOCK");
        var other = new string('b', 64);
        Ok(Ready(liveMaster: other).Blocks.Any(b => b.Source == "MASTER"), "05 Master SHA mismatch → BLOCK");
        Ok(Ready(liveDna: other).Blocks.Any(b => b.Source == "CHARACTER_DNA"), "06 DNA SHA mismatch → BLOCK");
        Ok(Ready(livePrp: other).Blocks.Any(b => b.Source == "PRODUCTION_REFERENCE_PACK"), "07 PRP SHA mismatch → BLOCK");
        Ok(Ready(liveContract: other).Blocks.Any(b => b.Source == "SHOT_CONTRACT"), "08 Shot Contract SHA mismatch → BLOCK");
        Ok(Ready(liveStill: other).Blocks.Any(b => b.Source == "APPROVED_STILL"), "09 Still artifact SHA mismatch → BLOCK");
        Ok(Ready(master: false).Blocks.Any(b => b.Attribute == "master"), "10 Master not LOCKED → BLOCK");
        Ok(Ready(dnaOk: false).Blocks.Any(b => b.Attribute == "dna"), "11 DNA not LOCKED → BLOCK");
        Ok(Ready(prp: false).Blocks.Any(b => b.Attribute == "prp"), "12 PRP not LOCKED → BLOCK");
        Ok(Ready(shotStatus: "DRAFT").Status == "BLOCKED", "13 Shot Contract not approved → BLOCK");

        var age = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(HairMotion: "make him older"));
        Ok(Ready(body: age).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_IDENTITY_CONFLICT"), "14 age change → BLOCK");
        var hair = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(HairMotion: "change hairstyle"));
        Ok(Ready(body: hair).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_IDENTITY_CONFLICT"), "15 hair change → BLOCK");
        var face = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(HairMotion: "different face"));
        Ok(Ready(body: face).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_IDENTITY_CONFLICT"), "16 face change → BLOCK");
        var eyes = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(ClothMotion: "change eye structure"));
        Ok(Ready(body: eyes).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_IDENTITY_CONFLICT"), "17 eye structure change → BLOCK");
        var wardrobe = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(ClothMotion: "change wardrobe identity"));
        Ok(Ready(body: wardrobe).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_IDENTITY_CONFLICT"), "18 wardrobe identity change → BLOCK");

        Ok(Ready().Status == "PASS", "19 allowed slow push / head turn / breathing / cloth / hair → PASS");
        var mixed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
        mixed["camera"] = JsonSerializer.SerializeToElement(new { framing = "medium", cameraMovement = "camera moves forward while Minh walks" });
        Ok(Ready(body: JsonSerializer.SerializeToElement(mixed.ToDictionary(p => p.Key, p => (object)p.Value))).Blocks.Any(b => b.Code == "VIDEO_CONTRACT_CAMERA_ACTION_CONFLICT"),
            "20 camera/action mix → BLOCK");

        var zero = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(DurationSeconds: 0));
        Ok(Ready(body: zero).Blocks.Any(b => b.Attribute == "duration"), "21 duration 0 → BLOCK");
        var neg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
        neg["timing"] = JsonSerializer.SerializeToElement(new { duration = -1, fps = "24", beats = Array.Empty<object>() });
        Ok(Ready(body: JsonSerializer.SerializeToElement(neg.ToDictionary(p => p.Key, p => (object)p.Value))).Blocks.Any(b => b.Attribute == "duration"), "22 negative duration → BLOCK");
        var beat = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
        beat["timing"] = JsonSerializer.SerializeToElement(new { duration = 5, fps = "24", beats = new object[] { new { at = 9, label = "late" } } });
        Ok(Ready(body: JsonSerializer.SerializeToElement(beat.ToDictionary(p => p.Key, p => (object)p.Value))).Blocks.Any(b => b.Attribute == "duration"), "23 beat > duration → BLOCK");

        var runway = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(HairMotion: "Runway gen4_turbo"));
        Ok(Ready(body: runway).Status == "BLOCKED", "24 Runway injection → BLOCK");
        var gemini = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(ClothMotion: "call Gemini now"));
        Ok(Ready(body: gemini).Status == "BLOCKED", "25 Gemini injection → BLOCK");
        Ok(ProductionVideoContractRules.ContainsPromptLeak("FINAL_PROMPT + model_prompt"), "26 model/prompt leak → BLOCK");

        var h1 = ProductionVideoContractRules.HashCanonical(payload);
        var h2 = ProductionVideoContractRules.HashCanonical(JsonSerializer.Deserialize<JsonElement>(payload.GetRawText()));
        Ok(h1.Length == 64 && h1 == h2 && Ready().ContractSha256 == h1, "27 deterministic SHA");
        var changed = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(DurationSeconds: 8));
        Ok(ProductionVideoContractRules.HashCanonical(changed) != h1, "28 changed payload → new SHA");
        Ok(ProductionVideoContractRules.IsApproved("DIRECTOR_APPROVED") && !ProductionVideoContractRules.CanEdit("DIRECTOR_APPROVED"), "29 approved immutable");
        Ok(ProductionVideoContractRules.NextVersion("V1") == "V2", "30 versioning V1 → V2");
        Ok(Ready(requestedStill: Guid.Parse("11111111-1111-1111-1111-111111111111")).Blocks.Any(b => b.Attribute == "approvedStillExecutionId"),
            "31 still swap same version → BLOCK");
        Ok(!ProductionVideoContractRules.AutoApprove() && !ProductionVideoContractRules.AutoFix() && !ProductionVideoContractRules.CallsProvider(), "32 no auto / no provider");
        Ok(!ProductionVideoContractRules.AllowsGemini() && !ProductionVideoContractRules.AllowsRunway() && !ProductionVideoContractRules.CreatesPixels("SPEC"), "33 Gemini/Runway/generation FALSE");
        Ok(!ProductionVideoContractRules.TouchesGolden("production-video-contract") && ProductionVideoContractRules.TouchesGolden("GOLDEN-SH01-01"), "34 Golden protected");
        Ok(ProductionVideoContractRules.RejectReasonRequired("Camera too fast.") && !ProductionVideoContractRules.RejectReasonRequired("  "), "35 reject reason required");
        Ok(govPass.Count == 0, "36 fixture governance clean");
        Ok(Ready(exec: "SUCCEEDED", review: "PENDING").Status == "BLOCKED", "37 SUCCEEDED without Director APPROVED → BLOCK");
        Ok(Ready(artifact: false).Status == "BLOCKED", "38 artifact missing → BLOCK");
        var providerWord = ProductionVideoContractRules.BuildPayload(
            "CHAR-099", shotId, masterId, sha, dnaId, sha, prpId, sha, contractId, contractSha, stillId, sha, shot,
            new ProductionVideoContractRules.WriteOverlay(HairMotion: "ask the provider"));
        Ok(Ready(body: providerWord).Status == "BLOCKED", "39 provider word in motion → BLOCK");
        return fail;
    }

    private static bool CanPass(this ProductionVideoContractRules.GateOutput o) => o.Status == "PASS";
}
