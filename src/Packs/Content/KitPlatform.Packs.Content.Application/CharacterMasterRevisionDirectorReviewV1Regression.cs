using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterMasterRevisionDirectorReviewV1Regression
{
    public const string SuiteId = CharacterMasterRevisionDirectorReviewV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var master = new string('a', 64);
        var candidate = new string('c', 64);
        var pending = new CharacterMasterRevisionV1Rules.AuthorityState(
            master, candidate, CharacterMasterRevisionV1Rules.StatusPendingReview,
            "LOCKED", "LOCKED", "PENDING");
        var approved = CharacterMasterRevisionDirectorReviewV1Rules.Apply(
            pending, CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove);
        var rejected = CharacterMasterRevisionDirectorReviewV1Rules.Apply(
            pending, CharacterMasterRevisionDirectorReviewV1Rules.ActionReject);
        var locked = CharacterMasterRevisionDirectorReviewV1Rules.Apply(
            approved, CharacterMasterRevisionDirectorReviewV1Rules.ActionLock);
        var styleSha = ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);

        CharacterMasterRevisionDirectorReviewV1Rules.ReviewCommand Cmd(
            string action,
            string status,
            string? actor = "director",
            string? characterId = "CHAR-099",
            string? candidateCharacterId = "CHAR-099",
            string? candidateSha = null,
            string? revisionMaster = null,
            string? liveMaster = null,
            string? reason = null) =>
            new(characterId, candidateCharacterId, candidateSha ?? candidate,
                revisionMaster ?? master, liveMaster ?? master, status, action, actor, null,
                action == CharacterMasterRevisionDirectorReviewV1Rules.ActionReject
                    ? (reason ?? "Vẫn quá già")
                    : reason);

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.MayReview(
                CharacterMasterRevisionV1Rules.StatusPendingReview, true)
            && CharacterMasterRevisionDirectorReviewV1Rules.MayApprove(
                CharacterMasterRevisionV1Rules.StatusPendingReview)
            && CharacterMasterRevisionDirectorReviewV1Rules.MayReject(
                CharacterMasterRevisionV1Rules.StatusPendingReview),
            "01 Pending candidate exists → review available");

        Ok(approved.Status == CharacterMasterRevisionV1Rules.StatusApproved
            && CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusPendingReview)) is null,
            "02 Pending → Approve → APPROVED");

        Ok(rejected.Status == CharacterMasterRevisionV1Rules.StatusRejected
            && CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionReject,
                    CharacterMasterRevisionV1Rules.StatusPendingReview)) is null,
            "03 Pending → Reject → REJECTED");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusPendingReview))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateNotApproved
            && CharacterMasterRevisionDirectorReviewV1Rules.Apply(
                pending, CharacterMasterRevisionDirectorReviewV1Rules.ActionLock).Status
                == CharacterMasterRevisionV1Rules.StatusPendingReview,
            "04 Pending → Lock → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusRejected))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateNotApproved,
            "05 Rejected → Lock → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusRejected))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState,
            "06 Rejected → Approve → BLOCK");

        Ok(locked.Status == CharacterMasterRevisionV1Rules.StatusLocked
            && CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusApproved)) is null,
            "07 Approved → Lock → LOCKED");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionReject,
                    CharacterMasterRevisionV1Rules.StatusApproved))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState,
            "08 Approved → Reject → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusLocked))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState,
            "09 Locked → Lock → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusLocked))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState,
            "10 Locked → Approve → BLOCK");

        Ok(rejected.CurrentMasterSha == master
            && !CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(
                rejected.Status, candidate: true),
            "11 Reject does not change authority");

        Ok(approved.CurrentMasterSha == master
            && CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(approved.Status, false)
            && !CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(approved.Status, true),
            "12 Approve does not change authority");

        Ok(locked.CurrentMasterSha == candidate
            && CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(locked.Status, true),
            "13 Lock changes authority");

        Ok(pending.CurrentMasterSha == master
            && locked.CurrentMasterSha != master
            && master == new string('a', 64),
            "14 Old Master remains immutable");

        Ok(approved.CandidateMasterSha == candidate
            && rejected.CandidateMasterSha == candidate
            && locked.CandidateMasterSha == candidate,
            "15 Candidate SHA remains unchanged");

        Ok(approved.CurrentMasterSha == master
            && locked.CurrentMasterSha == candidate,
            "16 Authority SHA changes only after lock");

        Ok(locked.DnaStatus == CharacterMasterRevisionV1Rules.StaleDna
            && approved.DnaStatus == "LOCKED",
            "17 DNA becomes stale after lock");

        Ok(locked.PrpStatus == CharacterMasterRevisionV1Rules.StalePrp
            && approved.PrpStatus == "LOCKED",
            "18 PRP becomes stale after lock");

        Ok(locked.CrpStatus == CharacterMasterRevisionV1Rules.StaleCrp
            && approved.CrpStatus == "PENDING",
            "19 CRP becomes stale after lock");

        Ok(!CharacterMasterRevisionDirectorReviewV1Rules.AutoGenerateDownstream()
            && !CharacterMasterRevisionV1Rules.AutoGenerateDownstream(),
            "20 No downstream auto-generation");

        Ok(!CharacterMasterRevisionDirectorReviewV1Rules.CallsGeminiOnApprove()
            && !CharacterMasterRevisionDirectorReviewV1Rules.CallsGemini(),
            "21 No Gemini on approve");

        Ok(!CharacterMasterRevisionDirectorReviewV1Rules.CallsGeminiOnReject(),
            "22 No Gemini on reject");

        Ok(!CharacterMasterRevisionDirectorReviewV1Rules.CallsGeminiOnLock(),
            "23 No Gemini on lock");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.ConcurrentLoser(
                CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                CharacterMasterRevisionDirectorReviewV1Rules.ActionReject,
                CharacterMasterRevisionV1Rules.StatusPendingReview)
            == CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState,
            "24 Concurrent approve/reject → one valid transition");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusApproved,
                    liveMaster: new string('f', 64)))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateAuthorityChanged,
            "25 Wrong authority SHA → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusPendingReview,
                    characterId: "CHAR-002", candidateCharacterId: "CHAR-001"))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateCharacterMismatch,
            "26 Wrong character → BLOCK");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusPendingReview,
                    candidateSha: ""))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateCandidateNotFound,
            "27 Missing candidate → CANDIDATE_NOT_FOUND");

        Ok(CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove,
                    CharacterMasterRevisionV1Rules.StatusPendingReview, actor: "system"))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateUnauthorized
            && CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionReject,
                    CharacterMasterRevisionV1Rules.StatusPendingReview, actor: "auto"))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateUnauthorized
            && CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                Cmd(CharacterMasterRevisionDirectorReviewV1Rules.ActionLock,
                    CharacterMasterRevisionV1Rules.StatusApproved, actor: "background"))
            == CharacterMasterRevisionDirectorReviewV1Rules.GateUnauthorized,
            "28 Unauthorized review actor → BLOCK");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1",
            "29 Minh SHA unchanged");

        Ok(styleSha == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"
            && !CharacterMasterRevisionV1Rules.ChangesProjectVisualStyle(),
            "30 Project Visual Style SHA unchanged");

        var cmdType = typeof(CharacterMasterRevisionDirectorReviewV1Rules.ReviewCommand);
        Ok(!CharacterMasterRevisionDirectorReviewV1Rules.UsesCharacterName()
            && !CharacterMasterRevisionDirectorReviewV1Rules.UsesCharacterSpecificBranch()
            && cmdType.GetProperty("Name") is null
            && cmdType.GetProperty("CharacterName") is null
            && !CharacterMasterRevisionDirectorReviewV1Rules.AutoApprove()
            && !CharacterMasterRevisionDirectorReviewV1Rules.AutoLock(),
            "31 no character-name branching");

        return fail;
    }
}
