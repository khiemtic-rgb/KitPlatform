using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Production;

public static class PerformanceContractRules
{
    static readonly Regex StageCue = new(
        @"\s*[—–]\s+(?:KHOE|CHẠY|NHÌN|NGẬP|VỠ|BEAT|INSERT|CUT|\().+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex BracketTags = new(@"\[[^\]]*\]\s*", RegexOptions.CultureInvariant);
    static readonly Regex Clock = new(@"\s*\(\d+\s*[–\-]\s*\d+\s*s?\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex LeadingStage = new(
        @"^\(([^)]{1,32})\)\s*",
        RegexOptions.CultureInvariant);

    public static string SpokenText(string? raw)
    {
        var s = BracketTags.Replace(raw ?? "", "");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        s = StageCue.Replace(s, "").Trim();
        s = Clock.Replace(s, "").Trim();
        s = LeadingStage.Replace(s, match =>
        {
            var inner = match.Groups[1].Value;
            if (inner.Contains('?') || inner.Contains('!') || inner.Contains('.') || inner.Contains('…'))
                return match.Value;
            return "";
        }, 1);
        return s.Trim();
    }

    public static PerformanceDraft CreateDraft(PerformanceProjectionSource src)
    {
        if (src.BuildId == Guid.Empty || string.IsNullOrWhiteSpace(src.ShotCode))
            throw new InvalidOperationException(PerformanceContractIds.MissingScope);

        var dialogueIds = src.DialogueSegmentIds?.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToArray()
            ?? [];
        var lineById = (src.Lines ?? []).ToDictionary(l => l.LineId, StringComparer.Ordinal);
        var missing = dialogueIds.Where(id => !lineById.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(PerformanceContractIds.UnresolvedProjection);

        var orderedLines = dialogueIds.Select(id => lineById[id]).ToArray();
        var characterIds = (src.CharacterIds ?? []).Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToArray();
        var offFrame = new HashSet<string>(src.OffFrameCharacterIds ?? [], StringComparer.Ordinal);
        var voiceOnly = new HashSet<string>(src.VoiceOnlyCharacterIds ?? [], StringComparer.Ordinal);
        foreach (var line in orderedLines)
        {
            var id = (line.CharacterId ?? "").Trim();
            if (id.Length == 0) continue;
            if (!characterIds.Contains(id, StringComparer.Ordinal)
                && (voiceOnly.Contains(id) || id.Equals("CHAR-VO", StringComparison.Ordinal)))
            {
                characterIds = [.. characterIds, id];
                voiceOnly.Add(id);
            }
        }

        var shot = MapBeat(src.ActingBeat, src.GazeTarget);
        var characters = characterIds.Select(id => MapCharacter(id, orderedLines, shot, src, offFrame, voiceOnly)).ToArray();
        var lines = orderedLines.Select(line => MapLine(line, characters, shot)).ToArray();

        return new PerformanceDraft(
            PerformanceContractIds.DocumentId,
            PerformanceContractIds.SchemaVersion,
            src.BuildId,
            src.ShotCode.Trim(),
            (src.EpisodeCode ?? "").Trim(),
            (src.SceneCode ?? "").Trim(),
            shot,
            characters,
            lines,
            new PerformanceTimingRef(PerformanceContractIds.TimingDocumentId, EmptyToNull(src.TimingHash)),
            new PerformanceVisualRef(
                characters.Select(c => c.CharacterId).ToArray(),
                EmptyToNull(src.VisualAuthorityId),
                EmptyToNull(src.VisualContractHash)),
            new PerformanceStoryRef(
                dialogueIds,
                EmptyToNull(src.StoryTextHash),
                EmptyToNull(src.BeatId)));
    }

    public static PerformanceSourceRef SourceOf(PerformanceProjectionSource src, PerformanceDraft draft)
    {
        var locked = (src.DialogueSegmentIds ?? []).Select(id =>
        {
            var line = src.Lines.FirstOrDefault(l => l.LineId == id);
            return line?.Locked == true;
        }).ToArray();
        var inferred = draft.Lines.Any(l => !l.Delivery.Locked);
        return new PerformanceSourceRef(
            src.ActingBeat is not null,
            locked,
            inferred,
            PerformanceContractCanon.DraftFingerprintOf(draft));
    }

    public static PerformanceValidation Validate(PerformanceDraft draft, string? extraJson = null)
    {
        var issues = new List<PerformanceIssue>();
        if (draft.DocumentId != PerformanceContractIds.DocumentId)
            issues.Add(new("DOCUMENT", "documentId", "documentId must be FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1."));
        if (draft.SchemaVersion != PerformanceContractIds.SchemaVersion)
            issues.Add(new("SCHEMA", "schemaVersion", "schemaVersion must be 1."));
        if (draft.BuildId == Guid.Empty || string.IsNullOrWhiteSpace(draft.ShotCode))
            issues.Add(new(PerformanceContractIds.MissingScope, "buildId", "buildId and shotCode are required."));
        if (ProductionScopeAnchorRules.IsOfficialFilm(draft.BuildId)
            && !ProductionDirectorTiming.IsExplicitDecision(extraJson))
            issues.Add(new(PerformanceContractIds.OfficialFilmForbidden, "buildId", "Official film is out of Phase A TEST scope."));

        var primaries = draft.Characters.Count(c => c.VisualRole == PerformanceContractIds.Primary);
        if (primaries > 1)
            issues.Add(new(PerformanceContractIds.DuplicatePrimary, "characters", "Duplicate PRIMARY is not allowed."));

        var byId = draft.Characters.ToDictionary(c => c.CharacterId, StringComparer.Ordinal);
        foreach (var line in draft.Lines)
        {
            if (!byId.TryGetValue(line.CharacterId, out var owner))
            {
                issues.Add(new(PerformanceContractIds.InvalidCharacter, "lines." + line.LineId, "line.characterId must exist in characters[]."));
                continue;
            }
            if (owner.Participation != PerformanceContractIds.VoiceOnly
                && owner.Participation != PerformanceContractIds.InFrame
                && owner.Participation != PerformanceContractIds.OffFrame)
            {
                issues.Add(new(PerformanceContractIds.InvalidCharacter, "characters." + owner.CharacterId, "Unknown participation."));
            }
            if (!(owner.LineIds ?? []).Contains(line.LineId, StringComparer.Ordinal))
                issues.Add(new(PerformanceContractIds.LineCharacterMismatch, "characters." + owner.CharacterId + ".lineIds", "lineIds must correspond to the character."));
            if (string.IsNullOrWhiteSpace(line.TextIdentity.Text))
                issues.Add(new(PerformanceContractIds.EmptySpokenText, "lines." + line.LineId, "Spoken text is empty."));
            if (line.TextIdentity.TextHash != PerformanceContractCanon.LineTextHashOf(line.TextIdentity.Text))
                issues.Add(new(PerformanceContractIds.UnresolvedProjection, "lines." + line.LineId + ".textHash", "textHash does not match text."));
            if (!PerformanceEmotions.IsKnown(line.Delivery.Emotion))
                issues.Add(new(PerformanceContractIds.UnresolvedProjection, "lines." + line.LineId + ".emotion", "Unknown emotion."));
        }

        var l1 = draft.ShotPerformance.During?.Emotion;
        if (!string.IsNullOrWhiteSpace(l1) && !PerformanceEmotions.IsKnown(l1))
            issues.Add(new(PerformanceContractIds.UnresolvedProjection, "shotPerformance.during.emotion", "Unknown L1 emotion."));

        issues.AddRange(PerformanceContractCanon.ScanForbidden(extraJson));

        var spoken = draft.Lines.Count > 0;
        var unlocked = spoken && draft.Lines.Any(l => !l.Delivery.Locked);
        if (unlocked)
            issues.Add(new(PerformanceContractIds.SpokenLineUnlocked, "lines", "Spoken snapshot requires every line locked."));

        var blocking = issues.Where(i => i.Code != PerformanceContractIds.SpokenLineUnlocked).ToArray();
        var voiceReady = spoken && !unlocked && blocking.Length == 0;
        return new PerformanceValidation(blocking.Length == 0, voiceReady, issues);
    }

    public static PerformanceValidation ValidateForFreeze(PerformanceDraft draft, string? extraJson = null)
    {
        var result = Validate(draft, extraJson);
        var blocking = result.Issues.Where(i => i.Code != PerformanceContractIds.SpokenLineUnlocked).ToArray();
        var spoken = draft.Lines.Count > 0;
        var unlocked = spoken && draft.Lines.Any(l => !l.Delivery.Locked);
        var issues = result.Issues.ToList();
        if (unlocked && !issues.Any(i => i.Code == PerformanceContractIds.SpokenLineUnlocked))
            issues.Add(new(PerformanceContractIds.SpokenLineUnlocked, "lines", "Spoken snapshot requires every line locked."));
        var ok = blocking.Length == 0 && !unlocked;
        return new PerformanceValidation(ok, ok && spoken, issues);
    }

    static ShotPerformanceSlice MapBeat(ActingBeatSource? beat, string? gazeTarget)
    {
        if (beat is null && string.IsNullOrWhiteSpace(gazeTarget))
            return new ShotPerformanceSlice();
        return new ShotPerformanceSlice(
            new ShotPerformanceBefore(
                EmptyToNull(beat?.BeforeAction),
                EmptyToNull(beat?.BeforeBody),
                EmptyToNull(beat?.BeforeProp),
                EmptyToNull(beat?.BeforeGaze),
                EmptyToNull(beat?.BeforeRoom)),
            new ShotPerformanceDuring(EmptyToNull(beat?.DuringEmotion), beat?.DuringIntensity),
            new ShotPerformanceAfter(EmptyToNull(beat?.AfterAction), EmptyToNull(beat?.AfterGaze)),
            EmptyToNull(gazeTarget));
    }

    static CharacterInShot MapCharacter(
        string id,
        IReadOnlyList<LineProjectionSource> lines,
        ShotPerformanceSlice shot,
        PerformanceProjectionSource src,
        HashSet<string> offFrame,
        HashSet<string> voiceOnly)
    {
        var lineIds = lines.Where(l => l.CharacterId == id).Select(l => l.LineId).ToArray();
        var participation = voiceOnly.Contains(id) || id.Equals("CHAR-VO", StringComparison.Ordinal)
            ? PerformanceContractIds.VoiceOnly
            : offFrame.Contains(id)
                ? PerformanceContractIds.OffFrame
                : PerformanceContractIds.InFrame;
        var visual = VisualRoleOf(id, src);
        var speech = lineIds.Length > 0
            ? PerformanceContractIds.Speaker
            : participation == PerformanceContractIds.InFrame
                ? PerformanceContractIds.Listener
                : PerformanceContractIds.RoleNone;
        string? state = null;
        if (src.CharacterEmotionalState is not null
            && src.CharacterEmotionalState.TryGetValue(id, out var explicitState)
            && !string.IsNullOrWhiteSpace(explicitState))
        {
            state = explicitState.Trim();
        }
        return new CharacterInShot(id, participation, visual, speech, state, null, lineIds);
    }

    static string VisualRoleOf(string id, PerformanceProjectionSource src)
    {
        if ((src.PrimaryCharacterIds ?? []).Contains(id, StringComparer.Ordinal))
            return PerformanceContractIds.Primary;
        if ((src.SecondaryCharacterIds ?? []).Contains(id, StringComparer.Ordinal))
            return PerformanceContractIds.Secondary;
        var inFrame = (src.CharacterIds ?? []).Count(c =>
            !(src.VoiceOnlyCharacterIds ?? []).Contains(c, StringComparer.Ordinal)
            && !(src.OffFrameCharacterIds ?? []).Contains(c, StringComparer.Ordinal));
        if (inFrame == 1 && (src.CharacterIds ?? []).Contains(id, StringComparer.Ordinal)
            && !(src.VoiceOnlyCharacterIds ?? []).Contains(id, StringComparer.Ordinal)
            && !(src.OffFrameCharacterIds ?? []).Contains(id, StringComparer.Ordinal))
            return PerformanceContractIds.Primary;
        return PerformanceContractIds.RoleNone;
    }

    static PerformanceLine MapLine(LineProjectionSource line, IReadOnlyList<CharacterInShot> characters, ShotPerformanceSlice shot)
    {
        var text = SpokenText(line.Text);
        var owner = characters.FirstOrDefault(c => c.CharacterId == line.CharacterId);
        var emotion = EmptyToNull(line.Emotion);
        var intensity = line.Intensity;
        if (emotion is null)
            emotion = EmptyToNull(owner?.EmotionalState) ?? EmptyToNull(shot.During?.Emotion);
        if (intensity is null)
            intensity = owner?.Intensity ?? shot.During?.Intensity;
        var locked = line.Locked;
        if (emotion is null)
            throw new InvalidOperationException(PerformanceContractIds.UnresolvedProjection);
        intensity ??= 2;
        if (intensity is < 1 or > 5) intensity = 2;
        var pace = EmptyToNull(line.Pace) ?? "natural";
        var volume = EmptyToNull(line.Volume) ?? "mid";
        var pause = line.PauseSec ?? 0.15;
        var label = EmptyToNull(line.Label) ?? PerformanceEmotions.LabelOf(emotion, intensity.Value);
        return new PerformanceLine(
            line.LineId,
            line.CharacterId,
            new PerformanceTextIdentity(text, PerformanceContractCanon.LineTextHashOf(text)),
            new PerformanceDelivery(emotion, intensity.Value, pace, volume, pause, label, EmptyToNull(line.Emphasis), locked));
    }

    static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
