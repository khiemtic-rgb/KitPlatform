using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class FirstRealProductionV1Regression
{
    public const string SuiteId = FirstRealProductionRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        FirstRealProductionRules.GateInput Ready(
            bool master = true, bool dna = true, bool prp = true, bool crp = true,
            bool shot = true, bool intent = true, string? provider = "GEMINI",
            bool capability = true, bool confirm = true, bool dup = false,
            string character = "CHAR-001", string requested = "CHAR-001") =>
            new(master, dna, prp, crp, crp ? "LOCKED" : "DRAFT", shot, intent,
                provider, capability, capability ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
                confirm, dup, character, requested);

        var blocked = false;
        var called = false;
        void Attempt(FirstRealProductionRules.GateInput input)
        {
            var gate = FirstRealProductionRules.Evaluate(input);
            called = FirstRealProductionRules.MayCallProvider(gate);
            blocked = !called;
        }

        Attempt(Ready(crp: false));
        Ok(blocked && !called && FirstRealProductionRules.Evaluate(Ready(crp: false)).Code == "CRP_NOT_READY",
            "P0 CRP unusable → generation blocked");
        Attempt(Ready(master: false));
        Ok(blocked && !called, "P0 Master not locked → generation blocked");
        Attempt(Ready(dna: false));
        Ok(blocked && !called, "P0 DNA not locked → generation blocked");
        Attempt(Ready(prp: false));
        Ok(blocked && !called, "P0 PRP not locked → generation blocked");
        Attempt(Ready(shot: false));
        Ok(blocked && !called, "P0 Shot Contract invalid → generation blocked");
        Attempt(Ready(intent: false));
        Ok(blocked && !called, "P0 Intent invalid → generation blocked");
        Attempt(Ready(provider: null));
        Ok(blocked && !called && FirstRealProductionRules.Evaluate(Ready(provider: null)).Status == "NEEDS_PROVIDER_SELECTION",
            "P0 Provider not selected → generation blocked");
        Attempt(Ready(provider: "RUNWAY", capability: false));
        Ok(blocked && !called && !FirstRealProductionRules.AllowsRunway(),
            "P0 Capability unsupported / Runway → generation blocked");
        Attempt(Ready(dup: true));
        Ok(blocked && !called, "P0 Duplicate fingerprint → generation blocked");
        Attempt(Ready());
        Ok(!blocked && called, "P0 Provider selected + all authority valid → generation allowed");

        Ok(!FirstRealProductionRules.SameCharacter("CHAR-001", "CHAR-003")
            && !FirstRealProductionRules.SameCharacter("CHAR-001", ""), "no character fallback");
        Ok(!FirstRealProductionRules.AllowsVeo() && !FirstRealProductionRules.AllowsBatch(), "no Veo/batch");
        Ok(!FirstRealProductionRules.AutoApprove() && !FirstRealProductionRules.AutoLock()
            && !FirstRealProductionRules.AutoSelectProvider(), "no auto approve/lock/select");
        Ok(FirstRealProductionRules.ImageCapabilityReady(ProductionOsRules.GeminiImageProfile)
            && !FirstRealProductionRules.ImageCapabilityReady(ProductionOsRules.RunwayVideoProfile)
            && !FirstRealProductionRules.ImageCapabilityReady(ProductionOsRules.VeoVideoProfile), "Gemini image only");

        var payload = JsonSerializer.SerializeToElement(new
        {
            story = new { action = "Minh đang đọc tờ giấy" },
            scene = new { location = "phòng khách", summary = "Phòng khách buổi tối" },
            character = new { expression = "concerned" },
            composition = new { framing = "medium", angle = "eye_level", movement = "slow_dolly_in" },
            lighting = new { style = "warm_window" },
            motion = new { action = "hold" },
            timing = new { durationSeconds = 5 },
        });
        var intent = FirstRealProductionRules.TryResolveIntent("CHAR-001", payload, sha, sha, sha, sha);
        Ok(intent is not null, "intent resolves from contract");
        var sha1 = ProductionOsRules.IntentSha(intent!);
        var sha2 = ProductionOsRules.IntentSha(intent!);
        Ok(sha1 == sha2 && sha1.Length == 64, "IntentSha deterministic");
        var changed = intent! with { ActionDescription = "Minh đứng dậy" };
        Ok(ProductionOsRules.IntentSha(changed) != sha1, "IntentSha changes with action");
        var canonical = ProductionOsRules.Canonical(intent);
        Ok(canonical.IntentSha256 == sha1 && !ProductionOsRules.ContainsProvider(canonical.Text), "canonical provider-neutral");
        var providerInIntent = FirstRealProductionRules.TryResolveIntent(
            "CHAR-001",
            JsonSerializer.SerializeToElement(new { story = new { action = "gemini_prompt draw" }, scene = new { location = "room" } }),
            sha, sha, sha, sha);
        Ok(providerInIntent is null, "intent rejects provider injection");
        Ok(FirstRealProductionRules.StaffCrpBlock.Contains("Bộ ảnh chuẩn", StringComparison.Ordinal), "staff CRP language");
        return fail;
    }
}
