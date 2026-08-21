/** Voice Script = dialogue-only production artifact. Screenplay stays Story SoT. */

import { isNonStoryLine, looksLikeSpokenLine } from './content-famixa-story-parse';
import type { FamixaListenCue, FamixaSceneNode, SeriesPilotState } from './content-famixa-series';

export type VoicePreviewStatus = 'idle' | 'incomplete' | 'complete';

export type FamixaVoicePreview = {
  sourceLineCount: number;
  sourceCharCount: number;
  speakerCount: number;
  estimatedSec: number;
  generatedLineCount: number;
  generatedCharCount: number;
  generatedSec?: number;
  status: VoicePreviewStatus;
  issues: string[];
  /** Fingerprints of lines already sent to TTS — not audio. Survives F5. */
  generated?: { id: string; text: string }[];
  /** Operator xác nhận đã TTS — không gọi lại ElevenLabs. */
  operatorConfirmed?: boolean;
};

export type FamixaVoiceLine = FamixaListenCue & {
  sceneId?: string;
  order: number;
};

export type FamixaVoiceScript = {
  lines: FamixaVoiceLine[];
  sourceLineCount: number;
  sourceCharCount: number;
  speakerCount: number;
  estimatedSec: number;
  droppedNonSpeechCount: number;
};

function voiceOf(state: SeriesPilotState, characterId: string) {
  const ch = (state.characters ?? []).find((c) => c.id === characterId);
  const role = state.roles.find((r) => r.characterId === characterId);
  return (ch?.voiceId || role?.voiceId || '').trim() || undefined;
}

function nameOf(state: SeriesPilotState, characterId: string) {
  const ch = (state.characters ?? []).find((c) => c.id === characterId);
  const role = state.roles.find((r) => r.characterId === characterId);
  return (ch?.name || role?.name || characterId).trim();
}

/** True when the string is heading/action/meta/SMS/memory — not spoken dialogue. */
export function isNotSpokenDialogue(text: string, speaker?: string) {
  const s = text.trim();
  if (!s || s.length < 1) return true;
  if (!looksLikeSpokenLine(s, speaker)) return true;
  if (isNonStoryLine(s)) return true;
  if (/^(?:SC|SCENE|SHOT|SH)\s*0*\d+\b/i.test(s)) return true;
  if (/^(?:INT|EXT)\.?\s/i.test(s)) return true;
  if (/^(?:CUT TO BLACK|CUT TO|FADE OUT|FADE IN|KẾT THÚC|END)\.?\s*$/i.test(s)) return true;
  if (/^(VIDEO[ _]?ID|VIDEO[ _]?TITLE|TARGET DURATION|FORMAT|CONTINUITY)\s*:/i.test(s)) return true;
  return false;
}

/** Whole payload looks like a screenplay dump, not one spoken line. */
export function looksLikeScreenplayDump(text: string) {
  const s = text.trim();
  if (s.split(/\r?\n/).filter(Boolean).length >= 4) return true;
  if (/(?:^|\n)(?:SC|SCENE)\s*0*\d+\b/i.test(s) && s.length > 40) return true;
  if (/\n(?:MINH|NAM|LINH|BỐ|MẸ)\s*:/i.test(s) && s.length > 60) return true;
  if (/VIDEO ID:|07\.\s*SCRIPT/i.test(s)) return true;
  return false;
}

function spokenText(raw: string) {
  return raw
    .replace(/^["“”']+|["“”']+$/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function charsOf(text: string) {
  return spokenText(text).replace(/\s+/g, '').length;
}

export function estimateSpokenSec(charCount: number) {
  return Math.max(1, Math.round(charCount / 12));
}

function linesFromScenes(state: SeriesPilotState, scenes: FamixaSceneNode[]): FamixaVoiceLine[] {
  const out: FamixaVoiceLine[] = [];
  let order = 0;
  for (const sc of [...scenes].sort((a, b) => a.id.localeCompare(b.id, undefined, { numeric: true }))) {
    for (const d of sc.dialogue ?? []) {
      const text = spokenText(d.text);
      const who = nameOf(state, d.characterId);
      if (!text || isNotSpokenDialogue(text, who) || looksLikeScreenplayDump(text)) continue;
      order += 1;
      out.push({
        id: d.id || `voice-${sc.id}-${order}`,
        characterId: d.characterId,
        name: nameOf(state, d.characterId),
        text,
        voiceId: voiceOf(state, d.characterId),
        sceneId: sc.id,
        order,
      });
    }
  }
  return out;
}

/** Derive Voice Script from approved graph dialogue. Never uses packDraft as TTS. */
export function deriveVoiceScript(state: SeriesPilotState): FamixaVoiceScript {
  let lines = linesFromScenes(state, state.scenes ?? []);
  if (lines.length === 0 && (state.lines?.length ?? 0) > 0) {
    const byScene = new Map<string, typeof lines>();
    for (const l of state.lines ?? []) {
      const text = spokenText(l.text);
      if (!text || isNotSpokenDialogue(text, l.characterId) || looksLikeScreenplayDump(text)) continue;
      const sceneId = l.sceneId || 'SC';
      const bucket = byScene.get(sceneId) ?? [];
      bucket.push({
        id: l.id,
        characterId: l.characterId,
        name: nameOf(state, l.characterId),
        text,
        voiceId: voiceOf(state, l.characterId),
        sceneId: l.sceneId,
        order: 0,
      });
      byScene.set(sceneId, bucket);
    }
    lines = [...byScene.keys()]
      .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
      .flatMap((k) => byScene.get(k) ?? []);
    lines = lines.map((l, i) => ({ ...l, order: i + 1 }));
  }
  const sourceCharCount = lines.reduce((n, l) => n + charsOf(l.text), 0);
  const speakerCount = new Set(lines.map((l) => l.characterId)).size;
  const graphDialogue =
    (state.scenes ?? []).reduce((n, sc) => n + (sc.dialogue?.length ?? 0), 0) || (state.lines?.length ?? 0);
  return {
    lines,
    sourceLineCount: lines.length,
    sourceCharCount,
    speakerCount,
    estimatedSec: estimateSpokenSec(sourceCharCount),
    droppedNonSpeechCount: Math.max(0, graphDialogue - lines.length),
  };
}

export function formatVoiceScriptPreview(script: FamixaVoiceScript) {
  return script.lines.map((l) => `[${(l.name || l.characterId).toUpperCase()}]\n${l.text}`).join('\n\n');
}

export function mergeVoiceGenerated(
  prev: { id: string; text: string }[] | undefined,
  next: { id: string; text: string }[],
) {
  const map = new Map<string, string>();
  for (const g of [...(prev ?? []), ...next]) {
    const t = spokenText(g.text);
    if (g.id && t) map.set(g.id, t);
  }
  return [...map.entries()].map(([id, text]) => ({ id, text }));
}

export function verifyVoiceGeneration(
  script: FamixaVoiceScript,
  generated: { id: string; text: string }[],
): FamixaVoicePreview {
  const issues: string[] = [];
  const genMap = new Map(generated.map((g) => [g.id, spokenText(g.text)]));
  const genTexts = new Set(generated.map((g) => spokenText(g.text)).filter(Boolean));
  script.lines.forEach((line) => {
    const got = genMap.get(line.id);
    if (got === line.text || genTexts.has(line.text)) return;
    issues.push(`Thiếu #${line.order} ${line.name}: ${line.text.slice(0, 40)}`);
  });
  const generatedCharCount = generated.reduce((n, g) => n + charsOf(g.text), 0);
  const complete = issues.length === 0 && script.lines.length > 0;
  return {
    sourceLineCount: script.sourceLineCount,
    sourceCharCount: script.sourceCharCount,
    speakerCount: script.speakerCount,
    estimatedSec: script.estimatedSec,
    generatedLineCount: generated.length,
    generatedCharCount,
    generatedSec: estimateSpokenSec(generatedCharCount),
    generated: generated.map((g) => ({ id: g.id, text: spokenText(g.text) })),
    status: script.lines.length === 0 ? 'idle' : complete ? 'complete' : 'incomplete',
    issues,
  };
}

/** Current Voice Script is covered by a prior TTS run. Extra old lines (action later dropped) are OK. */
export function voicePreviewCoversScript(script: FamixaVoiceScript, preview?: FamixaVoicePreview) {
  if (!preview || script.lines.length === 0) return false;
  if (preview.generated?.length) {
    return verifyVoiceGeneration(script, preview.generated).status === 'complete';
  }
  if (preview.operatorConfirmed) return true;
  return preview.status === 'complete' && preview.generatedLineCount >= script.lines.length;
}

export function emptyVoicePreview(script: FamixaVoiceScript): FamixaVoicePreview {
  return {
    sourceLineCount: script.sourceLineCount,
    sourceCharCount: script.sourceCharCount,
    speakerCount: script.speakerCount,
    estimatedSec: script.estimatedSec,
    generatedLineCount: 0,
    generatedCharCount: 0,
    status: script.lines.length ? 'incomplete' : 'idle',
    issues: script.lines.length ? ['Chưa TTS đủ mọi câu thoại.'] : ['Chưa có thoại — Nhận pack kịch bản.'],
  };
}

export function canLockVoice(state: SeriesPilotState) {
  return !voiceLockBlockReason(state);
}

export function voiceLockBlockReason(state: SeriesPilotState): string | undefined {
  if ((state.scenes?.length ?? 0) > 0 && !state.storyReviewed) {
    return 'Duyệt Parsed Story (nút «Parsed Story đúng») trước.';
  }
  const script = deriveVoiceScript(state);
  if (script.lines.length === 0) return 'Chưa có thoại trong Voice Script — Nhận pack kịch bản.';
  const missing = script.lines.filter((l) => !l.voiceId);
  if (missing.length) {
    const names = [...new Set(missing.map((l) => l.name || l.characterId))].slice(0, 4).join(', ');
    return `Gán Voice Canon cho: ${names}.`;
  }
  if (voicePreviewCoversScript(script, state.voicePreview)) return undefined;
  const issue = (state.voicePreview?.issues ?? [])[0];
  return issue
    ? `Bấm «Tạo Full Voice» đến Status COMPLETE. ${issue}`
    : 'Bấm «Tạo Full Voice» (ElevenLabs) đến Status COMPLETE rồi mới khóa.';
}

export function voiceProductionReady(state: SeriesPilotState) {
  if (!state.voiceLocked) return false;
  if (state.voicePreview?.status === 'complete' || state.voicePreview?.operatorConfirmed) return true;
  return voicePreviewCoversScript(deriveVoiceScript(state), state.voicePreview);
}
