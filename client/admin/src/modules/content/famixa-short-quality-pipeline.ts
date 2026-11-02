/**
 * Desk-side future input for the short quality pipeline.
 * Historical ingress strings stay in the canon modules. This only rewrites the
 * next request when that request would repeat a confirmed contradiction.
 */

export const SH01 = 'EP01-SC01-SH01';
export const SH02 = 'EP01-SC01-SH02';
export const BROKEN_TAKE_PAPER =
  '(Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.';
export const COMPLETE_TAKE_PAPER =
  'Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.';
export const SH01_FUTURE_ACTING =
  "Exactly two people. No third person, no ghost, no duplicate body. Both of Linh's eyes stay visible for the whole shot. She does not turn into profile. Her mouth stays slightly open, a visible three-quarter shape toward Minh, not a closed line. Minh's head stays lowered. He never turns his back to the camera and his head does not spin or snap. Linh's face stays the large face in a medium two-shot. Minh stays in the doorway, bag strap on one shoulder, bag hanging. Audience must see Minh entering the room, head lowered, while Linh wipes the dining table and looks toward him. NEVER look into the lens. Exactly two intact people. No extra duplicates, no severe deformation.";
export const SH02_FUTURE_ACTING =
  'Audience must see Minh hesitate, set the bag down on the chair, slowly unzip it, take out the folded test paper, hand it to Linh with both hands, then lower his gaze. Face visible. Gaze toward Linh, not the camera. NEVER look into the lens. Never paint the line. Continuity: Minh is already in the room. Prop: bag on the chair, folded test with a red score.';
export const SH03_SH04_FUTURE_ACTING =
  `${COMPLETE_TAKE_PAPER} Continuity: this is the same folded test from the previous shot. Prop: the score on the paper is red.`;
export const SH05_FUTURE_ACTING =
  'Audience must see Minh exhale, sit on the sofa edge, and glance at Linh. He is not already seated at the start. Face visible. NEVER look into the lens. Never paint the line. Continuity: Minh moves from standing to the sofa edge. Prop: sofa.';
export const SH06_FUTURE_ACTING =
  'Audience must see Linh smile and pat the cushion beside Minh. Face visible. NEVER look into the lens. Never paint the line. Continuity: Minh is seated from the previous shot. Prop: cushion and pen.';
export const SH03_RECORDED_LINE = 'Điểm thấp thì lần sau gỡ được mà.';
export const SH01_SPOKEN_LINE = 'Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?';
export const SH02_SPOKEN_LINE = 'Dạ… nay con làm bài không tốt.';
export const SH05_SPOKEN_LINE = 'Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?';
export const SH06_SPOKEN_LINE = 'Ngồi xích lại đây, lấy bút ra mẹ xem nào.';
const EP01_LOCKED_LINES: Record<string, string> = {
  'EP01-SC01-SH01': SH01_SPOKEN_LINE,
  'EP01-SC01-SH02': SH02_SPOKEN_LINE,
  'EP01-SC01-SH03': SH03_RECORDED_LINE,
  'EP01-SC01-SH05': SH05_SPOKEN_LINE,
  'EP01-SC01-SH06': SH06_SPOKEN_LINE,
};
export const EP01_QUALITY_ORDER = [
  'EP01-SC01-SH01',
  'EP01-SC01-SH02',
  'EP01-SC01-SH03',
  'EP01-SC01-SH04',
  'EP01-SC01-SH05',
  'EP01-SC01-SH06',
] as const;
export const CLOCK_TOLERANCE_SEC = 0.75;
export const DIRECTOR_BLOCKER_COPY: Record<string, string> = {
  SPEECH_DOES_NOT_COVER_PLAYBACK: 'Thoại ngắn hơn hình đang phát.',
  MOUTH_CLOCK_MISMATCH: 'Miệng và thoại không cùng một nhịp.',
  MIX_CLOCK_SPLIT: 'Khớp môi và hình gốc không cùng một đồng hồ.',
  NOT_IN_PRODUCTION_OS: 'Tệp cũ chưa vào Production OS. Chưa được duyệt.',
  LEGACY_CANDIDATE: 'Tệp cũ chưa vào Production OS. Chưa được duyệt.',
  ACTING_SUBJECT_CONTRADICTS_STORY: 'Diễn đang trái với chuyện đã khóa.',
  ACTION_SENTENCE_INCOMPLETE: 'Câu hành động bị cắt.',
  SPOKEN_LINE_INCOMPLETE: 'Câu thoại bị cắt hoặc chưa gắn.',
  REQUIRED_PROP_MISSING: 'Đạo cụ bắt buộc không có trên hình.',
  PROP_NOT_ATTESTED: 'Đạo cụ bắt buộc không có trên hình.',
  MOUTH_DURING_INTENDED_SILENCE: 'Miệng cử động trong khoảng im lặng.',
  DIALOGUE_WINDOW_MISMATCH: 'Khoảng thoại không khớp đồng hồ đã khóa.',
  REVIEW_HASH_MISMATCH: 'Duyệt không gắn đúng tệp và mã băm đang phát.',
  REVIEW_HASH_MISSING: 'Duyệt không gắn đúng tệp và mã băm đang phát.',
  LIPSYNC_DURATION_MISSING: 'Chưa đo được thời lượng khớp môi của tệp hiện tại.',
};
const EP01_RECORDED_BLOCKERS: Record<string, readonly string[]> = {
  'EP01-SC01-SH01': ['ACTING_SUBJECT_CONTRADICTS_STORY', 'MOUTH_CLOCK_MISMATCH'],
  'EP01-SC01-SH02': ['SPEECH_DOES_NOT_COVER_PLAYBACK', 'MOUTH_CLOCK_MISMATCH', 'MIX_CLOCK_SPLIT', 'REQUIRED_PROP_MISSING'],
  'EP01-SC01-SH03': ['NOT_IN_PRODUCTION_OS', 'REQUIRED_PROP_MISSING'],
  'EP01-SC01-SH04': ['NOT_IN_PRODUCTION_OS', 'ACTION_SENTENCE_INCOMPLETE', 'REQUIRED_PROP_MISSING'],
  'EP01-SC01-SH05': ['NOT_IN_PRODUCTION_OS'],
  'EP01-SC01-SH06': ['NOT_IN_PRODUCTION_OS'],
};
export const SH01_HISTORICAL_ACTING_MARK = 'Linh entering the room';
export const SH02_TRUNCATION_MARK = 'đưa hai  ';

const EP01_APPROVED_ACTING: Record<string, string> = {
  [SH01]: SH01_FUTURE_ACTING,
  [SH02]: SH02_FUTURE_ACTING,
  'EP01-SC01-SH03': SH03_SH04_FUTURE_ACTING,
  'EP01-SC01-SH04': SH03_SH04_FUTURE_ACTING,
  'EP01-SC01-SH05': SH05_FUTURE_ACTING,
  'EP01-SC01-SH06': SH06_FUTURE_ACTING,
};

export type DialogueInterval = { startSec: number; endSec: number };
export type MixSpeechSpan = { startSec: number; endSec: number; carriesSpeech: boolean };

export function spokenLineForTts(raw: string) {
  let text = (raw ?? '').replace(/\s+/g, ' ').trim();
  text = text.replace(/\[[^\]]*\]\s*/g, '').trim();
  const leading = text.match(/^\(([^)]{1,32})\)\s*/);
  if (leading && !/[?!.…]/.test(leading[1] || '')) text = text.slice(leading[0].length).trim();
  return text;
}

export function brokenTakePaper(text: string) {
  return text.includes(BROKEN_TAKE_PAPER) || (text.includes('con số đỏ chót') && text.includes('(') && !text.includes(')'));
}

export function futureDeskStory(shotCode: string, story: string) {
  void shotCode;
  return brokenTakePaper(story) ? COMPLETE_TAKE_PAPER : story;
}

export function futureDeskActing(shotCode: string, acting: string) {
  const approved = EP01_APPROVED_ACTING[shotCode];
  if (approved) return approved;
  return brokenTakePaper(acting) ? SH03_SH04_FUTURE_ACTING : acting;
}

/** Next voice request. A truncated line is refused. Empty EP01 lines receive the locked text. */
export function voiceDraftForRequest(shotCode: string, raw: string) {
  const stripped = spokenLineForTts(raw);
  if ((EP01_QUALITY_ORDER as readonly string[]).includes(shotCode) && stripped.endsWith('...')) {
    return { permitted: false, text: '', blocker: 'SPOKEN_LINE_INCOMPLETE' };
  }
  const locked = EP01_LOCKED_LINES[shotCode];
  if (!stripped && locked) return { permitted: true, text: locked, blocker: '' };
  if (locked && stripped === locked) return { permitted: true, text: locked, blocker: '' };
  if (locked && stripped !== locked) return { permitted: false, text: '', blocker: 'SPOKEN_LINE_INCOMPLETE' };
  return { permitted: true, text: stripped, blocker: '' };
}

/**
 * A dialogue window exists only when a director locked the timing.
 * A computed lead-in or a measured voice length is not that window.
 */
export function approvedDialogueIntervals(
  timing?: { source?: string; speechStartSec?: number; speechEndSec?: number } | null,
): DialogueInterval[] {
  if (timing?.source !== 'DIRECTOR') return [];
  const start = Number(timing.speechStartSec);
  const end = Number(timing.speechEndSec);
  if (!(end > start) || start < 0) return [];
  return [{ startSec: start, endSec: end }];
}

/** Picture, dialogue intervals, lip-sync, audio, and mix placement stay separate clocks. */
export function productionClockBlockers(input: {
  directDialogue: boolean;
  audioSec: number;
  pictureSec: number;
  lipsyncSec?: number;
  dialogueIntervals?: readonly DialogueInterval[];
  mix?: readonly MixSpeechSpan[];
  /**
   * Outcome checks require the mix spans that were actually composed.
   * A click before that mix exists passes false. It still checks the window
   * against the voice, the measured lip-sync, and the picture. It does not invent spans.
   */
  enforceMixSpans?: boolean;
  mouthDuringSilence?: boolean;
  mouthDuringSpeech?: boolean | null;
}) {
  const codes: string[] = [];
  if (!input.directDialogue) return codes;
  const intervals = input.dialogueIntervals ?? [];
  const audio = input.audioSec;
  const picture = input.pictureSec;
  const lipsync = input.lipsyncSec ?? 0;
  if (!intervals.length) {
    if (audio > 0 && picture > 0 && picture - audio > CLOCK_TOLERANCE_SEC) codes.push('SPEECH_DOES_NOT_COVER_PLAYBACK');
    if (lipsync > 0 && picture > 0 && picture - lipsync > CLOCK_TOLERANCE_SEC) codes.push('MIX_CLOCK_SPLIT');
  } else {
    const span = intervals.reduce((sum, row) => sum + (row.endSec - row.startSec), 0);
    if (span <= 0 || intervals.some((row) => row.endSec <= row.startSec || row.startSec < 0)) {
      codes.push('DIALOGUE_WINDOW_MISMATCH');
    } else if (span - audio > CLOCK_TOLERANCE_SEC) codes.push('SPOKEN_LINE_INCOMPLETE');
    else if (audio - span > CLOCK_TOLERANCE_SEC) codes.push('DIALOGUE_WINDOW_MISMATCH');
    if (lipsync > 0 && span - lipsync > CLOCK_TOLERANCE_SEC) codes.push('MIX_CLOCK_SPLIT');
    const mix = input.mix ?? [];
    const enforceMixSpans = input.enforceMixSpans !== false || mix.length > 0;
    if (enforceMixSpans) {
      if (!mix.length) codes.push('DIALOGUE_WINDOW_MISMATCH');
      else {
        for (const interval of intervals) {
          const covered = mix.some(
            (row) => row.carriesSpeech && row.startSec <= interval.startSec + CLOCK_TOLERANCE_SEC && row.endSec + CLOCK_TOLERANCE_SEC >= interval.endSec,
          );
          if (!covered) codes.push('DIALOGUE_WINDOW_MISMATCH');
        }
      }
    } else if (picture > 0) {
      for (const interval of intervals) {
        if (interval.endSec > picture || (lipsync > 0 && interval.startSec + lipsync > picture)) {
          codes.push('DIALOGUE_WINDOW_MISMATCH');
        }
      }
    }
  }
  if (input.mouthDuringSilence) codes.push('MOUTH_DURING_INTENDED_SILENCE');
  if (input.mouthDuringSpeech === false) codes.push('MOUTH_CLOCK_MISMATCH');
  return [...new Set(codes)];
}

export const LIPSYNC_DURATION_MISSING = 'LIPSYNC_DURATION_MISSING';

/** A measured lip-sync length. Zero and any non-finite value are missing, not a duration. */
export function measuredLipsyncDurationSec(durationSec?: number | null) {
  const n = typeof durationSec === 'number' ? durationSec : Number(durationSec);
  if (!Number.isFinite(n) || n <= 0) return null;
  return n;
}

/**
 * Duration of the current LIPSYNC artifact, only when the loaded preview is that artifact.
 * A mix or video-take duration is not reused. A missing measurement stays missing.
 */
export function currentLipsyncMeasurement(input: {
  lipsyncArtifactId?: string | null;
  previewArtifactId?: string | null;
  previewRole?: string | null;
  previewDurationSec?: number | null;
}) {
  const id = (input.lipsyncArtifactId || '').trim();
  if (!id) return { hasArtifact: false as const };
  const previewId = (input.previewArtifactId || '').trim();
  const role = (input.previewRole || '').trim().toUpperCase();
  const duration = measuredLipsyncDurationSec(input.previewDurationSec);
  if (previewId === id && role === 'LIPSYNC' && duration !== null) {
    return { hasArtifact: true as const, durationSec: duration };
  }
  return { hasArtifact: true as const };
}

/**
 * Click-time clock. A mix, or a lipsync that already exists, uses that artifact's duration.
 * A missing duration is LIPSYNC_DURATION_MISSING. It is never sent as zero.
 * A first lipsync has no output yet, so the split check is not given a fabricated length.
 */
export function clickTimeLipsyncClock(input: {
  action: 'CREATE_LIPSYNC' | 'MIX_VIDEO';
  hasCurrentLipsyncArtifact: boolean;
  currentLipsyncDurationSec?: number | null;
  directDialogue: boolean;
  audioSec: number;
  pictureSec: number;
  dialogueIntervals?: readonly DialogueInterval[];
  mix?: readonly MixSpeechSpan[];
  mouthDuringSilence?: boolean;
  mouthDuringSpeech?: boolean | null;
}) {
  const needsMeasuredLipsync = input.action === 'MIX_VIDEO' || input.hasCurrentLipsyncArtifact;
  const measured = measuredLipsyncDurationSec(input.currentLipsyncDurationSec);
  if (needsMeasuredLipsync && measured === null) return [LIPSYNC_DURATION_MISSING];
  const intervals = input.dialogueIntervals ?? [];
  const mix = input.mix ?? [];
  const beforeMix = intervals.length > 0 && mix.length === 0;
  return productionClockBlockers({
    directDialogue: input.directDialogue,
    audioSec: input.audioSec,
    pictureSec: input.pictureSec,
    lipsyncSec: measured ?? undefined,
    dialogueIntervals: input.dialogueIntervals,
    mix: input.mix,
    enforceMixSpans: beforeMix ? false : undefined,
    mouthDuringSilence: input.mouthDuringSilence,
    mouthDuringSpeech: input.mouthDuringSpeech,
  });
}

export function futureClockBlocker(directDialogue: boolean, speechSec: number, pictureSec: number, lipsyncSec = 0) {
  return productionClockBlockers({
    directDialogue,
    audioSec: speechSec,
    pictureSec,
    lipsyncSec,
  })[0] || '';
}

export function shotQualityBlockers(shotId: string, qualityPassed: boolean) {
  if (qualityPassed) return [];
  return [...(EP01_RECORDED_BLOCKERS[shotId] ?? [])];
}

export function directorBlockerLines(codes?: readonly string[]) {
  const lines: string[] = [];
  for (const code of codes ?? []) {
    const text = DIRECTOR_BLOCKER_COPY[code] || DIRECTOR_BLOCKER_COPY[code.split(':')[0]];
    if (text && !lines.includes(text)) lines.push(text);
  }
  return lines;
}

/** A technical concat order is not episode acceptance. The quality order is all six shots. */
export function episodeAssemblyNotice(technicalOrder: readonly string[]) {
  const missing = EP01_QUALITY_ORDER.filter((shot) => !technicalOrder.includes(shot));
  if (missing.length) {
    return `Thứ tự kỹ thuật không phải duyệt tập. Còn thiếu ${missing.join(', ')}.`;
  }
  return 'File ghép chỉ được duyệt khi từng shot đã duyệt và đúng file lắp được xem.';
}

/** A verified file is not a visual accept. The review has to name this playback. */
export function technicalFileIsVisualAccept() {
  return false;
}
