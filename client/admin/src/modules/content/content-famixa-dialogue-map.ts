/** Explicit Short ↔ Dialogue map. Script is SoT. Does not dump leftovers onto the last shot. */

import { sceneCodeOfShot } from './content-famixa-batch-plan';
import { estimateSpokenSec, deriveVoiceScript, type FamixaVoiceLine } from './content-famixa-voice-script';
import {
  episodeShots,
  insertSceneShot,
  normCharId,
  shotActionFromPack,
  shotCharacterIds,
  shotHasValidAction,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SeriesPilotState,
} from './content-famixa-series';

function sceneOfLine(line: FamixaVoiceLine) {
  return (line.sceneId ?? '').replace(/\s+/g, '').toUpperCase();
}

function sceneOfShot(shot: FamixaSeriesShot) {
  return (sceneCodeOfShot(shot) || shot.sceneId || shot.scene || 'SC').replace(/\s+/g, '').toUpperCase();
}

function sameScene(line: FamixaVoiceLine, shot: FamixaSeriesShot) {
  const a = sceneOfLine(line);
  const b = sceneOfShot(shot);
  if (!a) return true;
  return a === b || a.includes(b) || b.includes(a);
}

function norm(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

function charsOf(text: string) {
  return norm(text).replace(/\s+/g, '').length;
}

function speakerOnShot(shot: FamixaSeriesShot, characterId: string) {
  const ids = shotCharacterIds(shot);
  if (ids.length === 0) return true;
  return ids.includes(normCharId(characterId));
}

function shotLooksSilent(shot: FamixaSeriesShot) {
  const blob = `${shot.story || ''} ${shot.visual || ''} ${shot.motionPromptVi || ''} ${shot.beatText || ''}`;
  return /câm|không thoại|\bsilent\b|voice:\s*none/i.test(blob);
}

function actionBlob(shot: FamixaSeriesShot) {
  return `${shot.story || ''} ${shot.visual || ''} ${shot.motionPromptVi || ''}`.replace(/\s+/g, ' ');
}

function lineFitsShot(shot: FamixaSeriesShot, line: FamixaVoiceLine) {
  const text = norm(line.text);
  if (text.length < 4) return false;
  return actionBlob(shot).includes(text);
}

export function proposeDialogueMap(state: SeriesPilotState, shots = episodeShots(state)) {
  const script = deriveVoiceScript(state);
  const byShot = new Map<string, string[]>();
  const used = new Set<string>();
  const pack = shots.filter((s) => shotHasValidAction(s, shotRunOf(state, s)));

  for (const shot of pack) {
    if (shotLooksSilent(shot)) {
      byShot.set(shot.id, []);
    }
  }

  for (const line of script.lines) {
    const hit = pack.find((s) => !shotLooksSilent(s) && !used.has(line.id) && sameScene(line, s) && lineFitsShot(s, line));
    if (!hit) continue;
    const cur = byShot.get(hit.id) ?? [];
    cur.push(line.id);
    byShot.set(hit.id, cur);
    used.add(line.id);
  }

  for (const shot of pack) {
    if (shotLooksSilent(shot)) continue;
    if ((byShot.get(shot.id)?.length ?? 0) > 0) continue;
    const line = script.lines.find(
      (l) => !used.has(l.id) && sameScene(l, shot) && speakerOnShot(shot, l.characterId),
    );
    if (!line) {
      byShot.set(shot.id, byShot.get(shot.id) ?? []);
      continue;
    }
    byShot.set(shot.id, [line.id]);
    used.add(line.id);
  }

  for (const line of script.lines) {
    if (used.has(line.id)) continue;
    const beatHost = [...pack]
      .reverse()
      .find(
        (s) =>
          !shotLooksSilent(s) &&
          sameScene(line, s) &&
          s.beatText &&
          norm(s.beatText).includes(norm(line.text)),
      );
    if (!beatHost) continue;
    const cur = byShot.get(beatHost.id) ?? [];
    cur.push(line.id);
    byShot.set(beatHost.id, cur);
    used.add(line.id);
  }

  for (const shot of pack) {
    if (!byShot.has(shot.id)) byShot.set(shot.id, []);
  }

  const extraIds = script.lines.filter((l) => !used.has(l.id)).map((l) => l.id);
  return { byShot, extraIds, lines: script.lines };
}

function lineSecOf(state: SeriesPilotState, line: { id: string; text: string }) {
  const measured = state.voiceAssets?.[line.id]?.duration;
  if (measured && measured > 0.2) return measured;
  return estimateSpokenSec(charsOf(line.text));
}

function packLineIds(state: SeriesPilotState, ids: string[], lines: Map<string, { id: string; text: string }>, cap = 10) {
  const chunks: string[][] = [];
  let cur: string[] = [];
  let acc = 0;
  for (const id of ids) {
    const line = lines.get(id);
    const sec = line ? lineSecOf(state, line) : 0.4;
    if (cur.length && acc + sec > cap + 0.05) {
      chunks.push(cur);
      cur = [id];
      acc = sec;
      continue;
    }
    cur.push(id);
    acc += sec;
  }
  if (cur.length) chunks.push(cur);
  return chunks;
}

function dropVoiceChains(state: SeriesPilotState): SeriesPilotState {
  const ep = state.episode;
  if (!ep?.shots.length) return state;
  const extra = new Map<string, string[]>();
  for (const s of ep.shots) {
    const host = s.voiceChainFrom;
    if (!host) continue;
    extra.set(host, [...(extra.get(host) ?? []), ...(s.dialogueSegmentIds ?? [])]);
  }
  const shots = ep.shots
    .filter((s) => !s.voiceChainFrom)
    .map((s) => {
      const more = extra.get(s.id);
      if (!more?.length) return s;
      return { ...s, dialogueSegmentIds: [...(s.dialogueSegmentIds ?? []), ...more] };
    });
  return { ...state, episode: { ...ep, shots } };
}

function continuationOf(host: FamixaSeriesShot, draft: FamixaSeriesShot, ids: string[], prevId: string, seconds: 5 | 10): FamixaSeriesShot {
  const chars = host.characterIds?.length ? host.characterIds : host.characters;
  return {
    ...draft,
    story: host.story,
    visual: host.visual,
    beatId: host.beatId,
    beatText: host.beatText,
    motionPrompt: host.motionPrompt,
    motionPromptVi: host.motionPromptVi,
    location: host.location,
    characters: [...chars],
    characterIds: [...chars],
    inheritFromShotId: host.id,
    previousShotId: prevId,
    voiceChainFrom: host.id,
    dialogueSegmentIds: ids,
    seconds,
    clock: `${seconds}s`,
  };
}

/** Spoken Short → 10s. Thoại >10s → nối Short cùng Action/KF. Không tách giữa câu. Không bịa beat. */
export function chainOverflowVoiceShots(state: SeriesPilotState): SeriesPilotState {
  const ep = state.episode;
  if (!ep?.shots.length) return state;
  const script = deriveVoiceScript(state);
  const byId = new Map(script.lines.map((l) => [l.id, l]));
  let cur: SeriesPilotState = state;
  const hosts = [...(cur.episode?.shots ?? [])].filter((s) => !s.voiceChainFrom);
  for (const host of hosts) {
    const live = cur.episode?.shots.find((s) => s.id === host.id);
    if (!live) continue;
    const ids = live.dialogueSegmentIds ?? [];
    if (ids.length < 2) {
      const sec = ids.reduce((n, id) => n + (byId.get(id) ? lineSecOf(cur, byId.get(id)!) : 0), 0);
      if (ids.length === 1 && sec > 0.2) {
        cur = {
          ...cur,
          episode: {
            ...cur.episode!,
            shots: cur.episode!.shots.map((s) =>
              s.id === live.id ? { ...s, seconds: 10, clock: '10s' } : s,
            ),
          },
        };
      }
      continue;
    }
    const chunks = packLineIds(cur, ids, byId, 10);
    if (chunks.length <= 1) continue;
    const first = chunks[0] ?? [];
    cur = {
      ...cur,
      episode: {
        ...cur.episode!,
        shots: cur.episode!.shots.map((s) =>
          s.id === live.id ? { ...s, dialogueSegmentIds: first, seconds: 10, clock: '10s' } : s,
        ),
      },
    };
    let afterId = live.id;
    const extras = chunks.slice(1, 6);
    for (const chunk of extras) {
      const inserted = insertSceneShot(cur, { afterId, scene: live.scene || live.sceneId });
      const filled = continuationOf(live, inserted.shot, chunk, afterId, 10);
      cur = {
        ...inserted.state,
        episode: {
          ...inserted.state.episode!,
          shots: inserted.state.episode!.shots.map((s) => (s.id === filled.id ? filled : s)),
        },
      };
      afterId = filled.id;
    }
  }
  return cur;
}

export function applyDialogueMap(state: SeriesPilotState): SeriesPilotState {
  const cleared = dropVoiceChains(state);
  const ep = cleared.episode;
  if (!ep?.shots.length) return cleared;
  const { byShot, lines } = proposeDialogueMap(cleared, ep.shots);
  const shots = ep.shots.map((s) => {
    const ids = s.dialogueSegmentIds ?? byShot.get(s.id) ?? [];
    const voiceSec = ids.reduce((n, id) => {
      const line = lines.find((l) => l.id === id);
      return n + (line ? lineSecOf(cleared, line) : 0);
    }, 0);
    const spoken = ids.length > 0;
    const seconds: 5 | 10 = spoken ? 10 : s.seconds === 10 ? 10 : 5;
    return {
      ...s,
      dialogueSegmentIds: ids,
      seconds,
      clock: `${seconds}s`,
    };
  });
  return chainOverflowVoiceShots({ ...cleared, episode: { ...ep, shots } });
}

export function linesForShot(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  scriptLines?: FamixaVoiceLine[],
): FamixaVoiceLine[] {
  const lines = scriptLines ?? deriveVoiceScript(state).lines;
  const ids = shot.dialogueSegmentIds;
  if (Array.isArray(ids)) return ids.map((id) => lines.find((l) => l.id === id)).filter((l): l is FamixaVoiceLine => Boolean(l));
  const { byShot } = proposeDialogueMap(state);
  const proposed = byShot.get(shot.id) ?? [];
  return proposed.map((id) => lines.find((l) => l.id === id)).filter((l): l is FamixaVoiceLine => Boolean(l));
}

export type CoverageRow = {
  shotId: string;
  code: string;
  action: string;
  dialogueIds: string[];
  silent: boolean;
  hasVoice: boolean;
  hasKf: boolean;
  hasVideo: boolean;
  voiceSec: number;
  seconds: number;
  durationIssue?: string;
  status: 'READY' | 'NONE' | 'NEED_VOICE' | 'NEED_KF' | 'NEED_VIDEO' | 'MISMATCH' | 'UNMAPPED';
};

export function coverageOf(
  state: SeriesPilotState,
  shots: FamixaSeriesShot[],
  opts?: { hasVoiceFile?: (lineId: string) => boolean; voiceSecOf?: (lineId: string) => number },
) {
  const pack = episodeShots(state);
  const script = deriveVoiceScript(state);
  const assigned = new Set<string>();
  const rows: CoverageRow[] = shots.map((shot) => {
    const run = shotRunOf(state, shot);
    const mapped = linesForShot(state, shot, script.lines);
    mapped.forEach((l) => assigned.add(l.id));
    const silent = mapped.length === 0;
    const hasVoice = silent || mapped.every((l) => Boolean(opts?.hasVoiceFile?.(l.id)));
    const voiceSec = mapped.reduce(
      (n, l) => n + (opts?.voiceSecOf?.(l.id) || estimateSpokenSec(charsOf(l.text))),
      0,
    );
    const seconds = shot.seconds === 10 ? 10 : shot.seconds || 5;
    const durationIssue = !silent && voiceSec > seconds + 0.05 ? `VOICE ${voiceSec.toFixed(1)}s / SHOT ${seconds}s` : undefined;
    const hasKf = Boolean(run.keyframeDataUrl);
    const hasVideo = Boolean(run.previewUrl?.trim());
    let status: CoverageRow['status'] = silent ? 'NONE' : 'READY';
    if (durationIssue) status = 'MISMATCH';
    else if (!silent && !hasVoice) status = 'NEED_VOICE';
    else if (!hasKf) status = 'NEED_KF';
    else if (!hasVideo) status = 'NEED_VIDEO';
    return {
      shotId: shot.id,
      code: studioShotCode(shot, pack.length ? pack : shots),
      action: shotActionFromPack(shot).slice(0, 80),
      dialogueIds: mapped.map((l) => l.id),
      silent,
      hasVoice,
      hasKf,
      hasVideo,
      voiceSec,
      seconds,
      durationIssue,
      status,
    };
  });
  const extraUnmapped = script.lines.filter((l) => !assigned.has(l.id));
  const spoken = rows.filter((r) => !r.silent);
  const voiceMissing = spoken.filter((r) => !r.hasVoice);
  const videoMissing = rows.filter((r) => !r.hasVideo);
  const assembleBlocked = Boolean(voiceMissing.length || extraUnmapped.length || rows.some((r) => r.status === 'MISMATCH'));
  const parts = [];
  if (extraUnmapped.length) parts.push(`${extraUnmapped.length} câu thoại chưa gắn Short`);
  if (voiceMissing.length) parts.push(`${voiceMissing.length} Short có thoại thiếu file TTS`);
  if (rows.some((r) => r.status === 'MISMATCH')) parts.push('một câu >10s — tách câu trong kịch bản, không nối giữa chữ');
  return {
    rows,
    spoken: spoken.length,
    silent: rows.length - spoken.length,
    voiceReady: spoken.filter((r) => r.hasVoice).length,
    voiceMissing,
    extraUnmapped,
    videoMissing,
    assembleBlocked,
    message: parts.join(' · ') || undefined,
  };
}
