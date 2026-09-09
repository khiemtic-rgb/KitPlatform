using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterMasterRevisionV1Regression
{
    public const string SuiteId = CharacterMasterRevisionV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var style = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var styleSha = ProjectVisualStyleV1Rules.Sha(style);
        var masterSha = new string('a', 64);
        var identitySha = new string('b', 64);
        var appearance = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "Bố", "warm", null, null, styleSha));
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");

        CharacterMasterRevisionV1Rules.RevisionSource Base(
            bool exists = true,
            bool master = true,
            bool confirm = true,
            string? provider = "GEMINI",
            string? reason = CharacterMasterRevisionV1Rules.ReasonAge,
            bool official = false,
            bool ready = false,
            CharacterAppearanceProfile? app = null,
            AgeExpressionTarget? ageTarget = null,
            bool hasApp = true,
            bool hasAge = true,
            string? idSha = null,
            string? brief = null,
            string? pvs = null,
            bool pvsReady = true,
            string? lastOk = null,
            string? characterId = "CHAR-099") =>
            new(exists, characterId, master, "MASTER-V1", masterSha,
                hasApp ? (app ?? appearance) : null, hasAge ? (ageTarget ?? age) : null, idSha ?? identitySha, brief ?? identity,
                pvs ?? styleSha, pvsReady, reason,
                "Character is 38 but current Master appears approximately 55–65 years old.",
                confirm, provider, official, ready, lastOk);

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(exists: false))
            == CharacterMasterRevisionV1Rules.GateCharacterNotFound,
            "01 missing Character → CHARACTER_NOT_FOUND");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(master: false))
            == CharacterMasterRevisionV1Rules.GateMasterNotReady,
            "02 missing Master → MASTER_NOT_READY");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(hasApp: false))
            == CharacterAppearanceProfileV1Rules.GateNotReady,
            "03 missing Appearance Profile → APPEARANCE_PROFILE_NOT_READY");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(hasAge: false))
            == CharacterAgeConsistencyV1Rules.GateNotReady,
            "04 missing Age Profile → AGE_PROFILE_NOT_READY");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(idSha: "", brief: ""))
            == CharacterMasterRevisionV1Rules.GateIdentityNotReady,
            "05 missing Identity → IDENTITY_NOT_READY");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(pvs: "", pvsReady: false))
            == ProjectVisualStyleV1Rules.GateNotReady,
            "06 missing Project Visual Style → PROJECT_VISUAL_STYLE_NOT_READY");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(confirm: false))
            == CharacterMasterRevisionV1Rules.GateConfirmation
            && CharacterMasterRevisionV1Rules.ToGate(CharacterMasterRevisionV1Rules.GateConfirmation).ProviderCalled == false,
            "07 confirm=false → CONFIRMATION_REQUIRED, Gemini not called");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(provider: "RUNWAY"))
            == CharacterMasterRevisionV1Rules.GateProviderUnsupported
            && CharacterMasterRevisionV1Rules.Evaluate(Base(provider: "VEO"))
                == CharacterMasterRevisionV1Rules.GateProviderUnsupported,
            "08 unsupported provider → PROVIDER_CAPABILITY_UNSUPPORTED");

        var ageRev = Base(reason: CharacterMasterRevisionV1Rules.ReasonAge);
        var ageReq = CharacterMasterRevisionV1Rules.BuildGenerationRequest(ageRev);
        Ok(CharacterMasterRevisionV1Rules.Evaluate(ageRev) is null
            && ageReq.TargetAppearanceAgeMin == 35
            && ageReq.TargetAppearanceAgeMax == 41
            && ageReq.CurrentMasterIsContinuityOnly
            && CharacterMasterRevisionV1Rules.PromptForbidsExactMasterFace(ageReq.CanonicalText, ageRev.RevisionReason)
            && ageReq.CanonicalText.Contains("AgeAppearanceProfile:", StringComparison.Ordinal)
            && ageReq.CanonicalText.Contains("FacialMaturityProfile:", StringComparison.Ordinal),
            "09 AGE_MISMATCH → valid revision");

        var appRev = Base(reason: CharacterMasterRevisionV1Rules.ReasonAppearance);
        var appReq = CharacterMasterRevisionV1Rules.BuildGenerationRequest(appRev);
        Ok(CharacterMasterRevisionV1Rules.Evaluate(appRev) is null
            && appReq.CanonicalText.Contains("LifestyleProfile:", StringComparison.Ordinal)
            && appReq.CanonicalText.Contains("Role is a narrative function", StringComparison.Ordinal)
            && CharacterMasterRevisionV1Rules.MayReviseAppearance(appRev.RevisionReason),
            "10 APPEARANCE_MISMATCH → valid revision");

        var fp = CharacterMasterRevisionV1Rules.Fingerprint(ageRev);
        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(lastOk: fp))
            == CharacterMasterRevisionV1Rules.GateDuplicate
            && fp == CharacterMasterRevisionV1Rules.Fingerprint(ageRev),
            "11 same fingerprint successful → BLOCK_DUPLICATE");

        Ok(!CharacterMasterRevisionV1Rules.AutoRetry()
            && CharacterMasterRevisionV1Rules.Evaluate(ageRev) is null,
            "12 failed previous generation → no automatic retry");

        var pending = new CharacterMasterRevisionV1Rules.AuthorityState(
            masterSha, new string('c', 64), CharacterMasterRevisionV1Rules.StatusPendingReview, "LOCKED", "LOCKED", "PENDING");
        Ok(pending.CurrentMasterSha == masterSha
            && pending.Status == CharacterMasterRevisionV1Rules.StatusPendingReview,
            "13 Current Master SHA preserved during pending review");

        var historySha = pending.CurrentMasterSha;
        var locked = CharacterMasterRevisionV1Rules.ApplyLock(
            CharacterMasterRevisionV1Rules.ApplyApprove(pending));
        Ok(historySha == masterSha
            && !string.Equals(locked.CurrentMasterSha, historySha, StringComparison.OrdinalIgnoreCase),
            "14 old Master remains readable after promote");

        Ok(!string.IsNullOrWhiteSpace(pending.CandidateMasterSha)
            && pending.CandidateMasterSha != pending.CurrentMasterSha,
            "15 candidate Master has new SHA");

        Ok(CharacterMasterRevisionV1Rules.ApplyApprove(pending).CurrentMasterSha == masterSha
            && locked.CurrentMasterSha == pending.CandidateMasterSha
            && locked.Status == CharacterMasterRevisionV1Rules.StatusLocked,
            "16 Approval + Lock changes authority");

        var rejected = CharacterMasterRevisionV1Rules.ApplyReject(pending);
        Ok(rejected.CurrentMasterSha == masterSha
            && rejected.Status == CharacterMasterRevisionV1Rules.StatusRejected,
            "17 Reject does not change authority");

        Ok(CharacterMasterRevisionV1Rules.Evaluate(Base(official: true, ready: true))
            == CharacterMasterRevisionV1Rules.GateLockedSilent
            && !CharacterMasterRevisionV1Rules.SilentMutate()
            && !CharacterMasterRevisionV1Rules.OverwritesLockedMaster()
            && !CharacterMasterRevisionV1Rules.MayRequest(true, true),
            "18 locked character cannot silently mutate");

        Ok(styleSha == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"
            && !CharacterMasterRevisionV1Rules.ChangesProjectVisualStyle()
            && ageReq.ProjectVisualStyleSha == styleSha,
            "19 Project Visual Style SHA unchanged");

        var sourceType = typeof(CharacterMasterRevisionV1Rules.RevisionSource);
        Ok(!CharacterMasterRevisionV1Rules.UsesCharacterName()
            && !CharacterMasterRevisionV1Rules.UsesCharacterSpecificBranch()
            && sourceType.GetProperty("Name") is null
            && sourceType.GetProperty("CharacterName") is null,
            "20 no character-name branching");

        Ok(appearance.TargetAppearanceAgeMin == 35
            && appearance.TargetAppearanceAgeMax == 41
            && appearance.LifestyleProfile.Contains("Contemporary Vietnamese", StringComparison.Ordinal)
            && appearance.EnergyProfile.Contains("warm", StringComparison.OrdinalIgnoreCase)
            && appearance.EnergyProfile.Contains("active", StringComparison.OrdinalIgnoreCase)
            && appearance.EnergyProfile.Contains("approachable", StringComparison.OrdinalIgnoreCase)
            && appearance.GroomingProfile.Contains("contemporary", StringComparison.OrdinalIgnoreCase)
            && appearance.PositiveAppearanceConstraints.Any(p =>
                p.Contains("contemporary Vietnamese adult man", StringComparison.OrdinalIgnoreCase))
            && appearance.NegativeAppearanceConstraints.Any(n => n.Contains("elderly", StringComparison.OrdinalIgnoreCase))
            && appearance.NegativeAppearanceConstraints.Any(n => n.Contains("55–65", StringComparison.Ordinal))
            && appearance.NegativeAppearanceConstraints.Any(n => n.Contains("rural elderly", StringComparison.OrdinalIgnoreCase))
            && appearance.NegativeAppearanceConstraints.Any(n => n.Contains("farmer", StringComparison.OrdinalIgnoreCase))
            && !CharacterAppearanceProfileV1Rules.UsesCharacterName(),
            "21 Nam fixture compiles from structured profile, not name");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && !CharacterMasterRevisionV1Rules.AutoApprove()
            && !CharacterMasterRevisionV1Rules.AutoLock()
            && !CharacterMasterRevisionV1Rules.AutoGenerateDownstream()
            && !CharacterMasterRevisionV1Rules.CrpRegenerationRevisesMaster()
            && !CharacterMasterRevisionV1Rules.CallsGemini()
            && !CharacterMasterRevisionV1Rules.GeneratesVideo()
            && !CharacterMasterRevisionV1Rules.ReadyAllowed(
                true, true, true, true, true, true, CharacterMasterRevisionV1Rules.StatusPendingReview)
            && CharacterMasterRevisionV1Rules.OverlayStudioStatus(
                CharacterStudioV1Rules.CharacterReady, CharacterMasterRevisionV1Rules.StatusPendingReview)
                == CharacterMasterRevisionV1Rules.StatusPendingReview
            && locked.DnaStatus == CharacterMasterRevisionV1Rules.StaleDna
            && locked.PrpStatus == CharacterMasterRevisionV1Rules.StalePrp
            && locked.CrpStatus == CharacterMasterRevisionV1Rules.StaleCrp
            && CharacterMasterRevisionV1Rules.NextVersion("V1") == "V2",
            "22 Minh protected / no auto / pending not READY / lock marks stale");

        var oldMaster = new string('1', 64);
        var newMaster = new string('2', 64);
        Ok(CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(oldMaster, newMaster)
            && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(null, newMaster, 4)
            && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(null, newMaster)
            && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(newMaster, newMaster)
            && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(oldMaster, null)
            && CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.MasterReady, crpStale: true)
            && !CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.MasterReady),
            "23 CRP bound to previous Master is stale and may regenerate");

        Ok(CharacterMasterRevisionV1Rules.HistoricalAuthoritySha(
                CharacterMasterRevisionV1Rules.StatusLocked, newMaster, newMaster, newMaster, oldMaster) == oldMaster
            && CharacterMasterRevisionV1Rules.LiveAuthoritySha(
                CharacterMasterRevisionV1Rules.StatusLocked, newMaster, oldMaster) == newMaster
            && CharacterMasterRevisionV1Rules.LiveAuthorityPath(
                CharacterMasterRevisionV1Rules.StatusLocked, "candidate.jpg", "v1.jpg") == "candidate.jpg"
            && CharacterMasterRevisionV1Rules.LiveAuthorityPath(
                CharacterMasterRevisionV1Rules.StatusLocked, null, "v1.jpg") is null
            && CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(
                CharacterMasterRevisionV1Rules.StatusLocked)
            && CharacterMasterRevisionV1Rules.OverlayCrpStale(
                CharacterMasterRevisionV1Rules.StatusLocked,
                CharacterMasterRevisionV1Rules.StaleCrp, oldMaster, newMaster)
            && !CharacterMasterRevisionV1Rules.OverlayCrpStale(
                CharacterMasterRevisionV1Rules.StatusLocked,
                CharacterMasterRevisionV1Rules.StaleCrp, newMaster, newMaster)
            && CharacterMasterRevisionV1Rules.GateAuthorityUnreadable
                == "MASTER_REVISION_AUTHORITY_UNREADABLE",
            "24 after lock, historical V1 stays visible; generate binds candidate; CRP stale lifts when rebound");

        return fail;
    }
}
