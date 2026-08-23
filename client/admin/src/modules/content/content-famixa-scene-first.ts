/** Scene-first / Continuity-first. Script is SoT. Does not invent story. */

import {
  actingOfLines,
  compileShotBlocking,
  inferActingDirection,
  resolveLinePerformance,
  stillAtmosphereFromAction,
  stillFaceFromPerformance,
  stillFaceFromScriptNote,
} from './content-famixa-acting-law';
import { linesForShot } from './content-famixa-dialogue-map';
import { displayCanonName, isOffFrameCanon } from './content-famixa-char-canon';
import { isMetaSpeakerName } from './content-famixa-story-parse';
import { estimateSpokenSec, deriveVoiceScript } from './content-famixa-voice-script';
import type { AssembleTimeline } from './content-famixa-assemble';
import { finalSourceBlockReason, resolveFinalSource } from './content-famixa-final-source';
import {
  effectiveShotAction,
  episodeShots,
  lockFromGraph,
  looksLikePackHeading,
  normCharId,
  shotHasValidAction,
  shotCharacterIds,
  shotRunOf,
  type FamixaCharacter,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { deriveVisualSpec, qaLane, visualQaAllowsApprove, type VisualQa, type VisualSpec } from './content-famixa-visual-spec';

export function visualSpecOf(state: SeriesPilotState, shot: FamixaSeriesShot, prevShot?: FamixaSeriesShot): VisualSpec {
  return shotRunOf(state, shot).visualSpec ?? compileShotSceneCard(state, shot, prevShot).visualSpec;
}

export function visualQaOf(state: SeriesPilotState, shot: FamixaSeriesShot): VisualQa | undefined {
  return shotRunOf(state, shot).visualQa;
}

export type SceneMaster = {
  sceneId: string;
  title: string;
  location: string;
  time: string;
  lighting: string;
  characters: string;
  wardrobe: string;
  props: string;
  environment: string;
  camera: string;
  mood: string;
  continuityRules: string;
  locked: boolean;
  sourceShotId?: string;
  screenDirection?: string;
  coverage?: string;
  lens?: string;
  cameraHeight?: string;
  blocking?: string;
  pacing?: string;
  /** Operator Emotion Arc — KIT warns, does not invent the next beat. */
  emotionPrev?: string;
  emotionNow?: string;
  emotionNext?: string;
};

export type ShotProdStatus =
  | 'HOLD'
  | 'READY'
  | 'KF DRAFT'
  | 'KF APPROVED'
  | 'VIDEO QUEUED'
  | 'VIDEO READY'
  | 'VIDEO APPROVED'
  | 'QA BLOCK'
  | 'QA REVIEW';

export const PROD_GATES = [
  { id: 'script', label: 'Script Approved' },
  { id: 'voice', label: 'Voice Approved' },
  { id: 'shotPlan', label: 'Shot Plan Approved' },
  { id: 'sceneMaster', label: 'Scene Master Approved' },
  { id: 'kf', label: 'KF Approved' },
  { id: 'video', label: 'Video Approved' },
  { id: 'preview', label: 'Preview Approved' },
] as const;

export type ProdGateId = (typeof PROD_GATES)[number]['id'];

export const SCENE_CONTINUITY_RULE =
  'Keep the same people already in the previous still, same faces, hair, wardrobe, age, room, and props. Do not redress anyone from a character sheet. Do not redesign Nam. Classmate An is off-screen. Faces and blocking follow this shot\'s script notes — do not copy a cheerful smile or catalog pose. Lighting may dim when the Action is tense or evening.';

const PROP_RE = /bài kiểm tra|tờ giấy|điện thoại|cơm|nồi|cặp|bàn ăn/i;

export function sceneCodeOf(raw?: string) {
  return raw?.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function sceneIdOfShot(shot: FamixaSeriesShot) {
  return sceneCodeOf(shot.sceneId) || sceneCodeOf(shot.scene) || sceneCodeOf(shot.id) || 'SC';
}

function clip(s: string, n: number) {
  return s.replace(/\s+/g, ' ').trim().slice(0, n);
}

function inferTime(blob: string) {
  if (/buổi tối|đêm|\btối\b/i.test(blob)) return 'evening';
  if (/chiều/i.test(blob)) return 'afternoon';
  if (/sáng|buổi sáng/i.test(blob)) return 'morning';
  return '';
}

function inferLighting(blob: string, time: string) {
  if (/tối|căng|áp lực|sắc lạnh|không vui|thành tích/i.test(blob)) {
    return time === 'evening' ? 'dim warm indoor evening, not bright daylight' : 'dim indoor, not a bright catalog room';
  }
  if (/ấm|warm/i.test(blob)) return time === 'evening' ? 'warm indoor evening' : 'warm indoor';
  if (time === 'evening') return 'warm indoor evening';
  return '';
}

function propsFrom(blob: string) {
  const found = blob.match(new RegExp(PROP_RE.source, 'gi')) ?? [];
  return [...new Set(found.map((s) => s.toLowerCase()))].join(', ');
}

/** Fill Scene Master from Script / graph only. Empty stays empty. */
export function deriveSceneMaster(state: SeriesPilotState, sceneId: string): SceneMaster {
  const sc = sceneCodeOf(sceneId) || sceneId;
  const node = (state.scenes ?? []).find((s) => sceneCodeOf(s.id) === sc);
  const lock = lockFromGraph(
    state,
    episodeShots(state).find((s) => sceneIdOfShot(s) === sc),
  );
  const shots = episodeShots(state).filter((s) => sceneIdOfShot(s) === sc);
  const blob = [node?.environment, node?.content, node?.title, lock.environment, ...shots.map((s) => s.story)]
    .filter(Boolean)
    .join(' ');
  const time = inferTime(blob);
  return {
    sceneId: sc || 'SC',
    title: node?.title || lock.scene.replace(/^SC\d+\s*[—–-]\s*/i, '') || '',
    location: clip(node?.environment || lock.environment || shots[0]?.location || '', 160),
    time,
    lighting: inferLighting(blob, time) || clip(node?.performance ? '' : '', 80),
    characters: clip(lock.characters, 200),
    wardrobe: clip(lock.wardrobe, 200),
    props: propsFrom(blob),
    environment: clip(node?.environment || lock.environment, 200),
    camera: clip(node?.camera || lock.camera, 120),
    mood: clip(node?.performance || lock.performance, 160),
    continuityRules: SCENE_CONTINUITY_RULE,
    locked: Boolean(state.sceneMasters?.[sc]?.locked || (lock.locked && sceneCodeOf(lock.scene) === sc)),
    sourceShotId: state.sceneMasters?.[sc]?.sourceShotId || lock.sourceShotId,
    screenDirection: state.sceneMasters?.[sc]?.screenDirection || '',
    coverage: state.sceneMasters?.[sc]?.coverage || '',
    lens: state.sceneMasters?.[sc]?.lens || '',
    cameraHeight: state.sceneMasters?.[sc]?.cameraHeight || '',
    blocking: state.sceneMasters?.[sc]?.blocking || '',
    pacing: state.sceneMasters?.[sc]?.pacing || '',
    emotionPrev: state.sceneMasters?.[sc]?.emotionPrev || '',
    emotionNow: state.sceneMasters?.[sc]?.emotionNow || '',
    emotionNext: state.sceneMasters?.[sc]?.emotionNext || '',
  };
}

export function sceneMasterOf(state: SeriesPilotState, sceneId: string): SceneMaster {
  const derived = deriveSceneMaster(state, sceneId);
  const stored = state.sceneMasters?.[derived.sceneId];
  if (!stored) return derived;
  return {
    ...derived,
    ...stored,
    sceneId: derived.sceneId,
    continuityRules: stored.continuityRules || SCENE_CONTINUITY_RULE,
    locked: Boolean(stored.locked),
  };
}

export function upsertSceneMaster(state: SeriesPilotState, patch: Partial<SceneMaster> & { sceneId: string }): SeriesPilotState {
  const cur = sceneMasterOf(state, patch.sceneId);
  const next = { ...cur, ...patch, sceneId: cur.sceneId };
  return {
    ...state,
    sceneMasters: { ...state.sceneMasters, [next.sceneId]: next },
  };
}

export function lockSceneMaster(state: SeriesPilotState, sceneId: string): SeriesPilotState {
  const master = sceneMasterOf(state, sceneId);
  return upsertSceneMaster(state, { ...master, locked: true });
}

export function unlockSceneMaster(state: SeriesPilotState, sceneId: string): SeriesPilotState {
  return upsertSceneMaster(state, { sceneId, locked: false });
}

export function sceneMasterLocked(state: SeriesPilotState, sceneId: string) {
  return sceneMasterOf(state, sceneId).locked === true;
}

export function mastersForShots(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const ids = [...new Set(shots.map(sceneIdOfShot))];
  return ids.map((id) => sceneMasterOf(state, id));
}

export function allSelectedMastersLocked(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const masters = mastersForShots(state, shots);
  return masters.length > 0 && masters.every((m) => m.locked);
}

export function normAction(raw?: string) {
  return (raw ?? '').replace(/\s+/g, ' ').trim().toLowerCase();
}

/** Same beat / same pose — REUSE. Different Action — new still. */
export function actionNearlySame(a?: string, b?: string) {
  const x = normAction(a);
  const y = normAction(b);
  if (!x || !y || x.length < 8 || y.length < 8) return false;
  if (x === y) return true;
  const shorter = x.length <= y.length ? x : y;
  const longer = x.length > y.length ? x : y;
  return longer.includes(shorter) && shorter.length / longer.length >= 0.85;
}

export function isCanonSheetName(name?: string) {
  const n = (name || '').toLowerCase();
  return /-canon\.(png|jpe?g|webp)$/.test(n) || /kf-.+-canon/.test(n);
}

export function kfHasPixels(run: SeriesShotRun) {
  return Boolean(run.keyframeDataUrl?.startsWith('data:image'));
}

/** Explicit duyệt + Image QA PASS (hard OK + quality ≥85). Legacy without visualQa still uses kfApproved. */
export function kfIsApprovedStill(run: SeriesShotRun) {
  if (!kfHasPixels(run)) return false;
  if (run.visualQa && !visualQaAllowsApprove(run.visualQa)) return false;
  if (run.kfApproved === true) return true;
  if (run.kfApproved === false) return false;
  return Boolean(run.continuity && Object.values(run.continuity).some(Boolean));
}

/** Immediate previous production still in the same Scene — not a white prompt. */
const VOICE_ONLY = /CHAR-VO|loi binh|narrator|voice.?over/i;

export function isVoiceOnlyChar(id?: string, name?: string) {
  return VOICE_ONLY.test(`${id || ''} ${name || ''}`);
}

/** An / CHAR-004 / extras / VO — named in script, not a body in the still. */
export function isOffFrameChar(id?: string, name?: string) {
  return isOffFrameCanon(id, name);
}

export function displayCharName(id: string, name?: string) {
  return displayCanonName(id, name);
}

/** Parser / insert used to stamp every SH with Minh+Nam+Linh. That is not who is in frame. */
export function isDefaultSceneCast(ids: string[]) {
  const v = [...new Set(ids.map(normCharId).filter((id) => id && !isVoiceOnlyChar(id)))].sort();
  return v.join() === 'CHAR-001,CHAR-002,CHAR-003';
}

function visualChar(c?: FamixaCharacter) {
  if (!c?.id) return false;
  if (c.offFrame || isMetaSpeakerName(c.name) || isMetaSpeakerName(c.id) || isOffFrameChar(c.id, c.name)) return false;
  return !isVoiceOnlyChar(c.id, c.name);
}

function charAliases(c: FamixaCharacter) {
  const out = [c.name, c.role].filter(Boolean).map((s) => s!.trim());
  const hay = `${c.id} ${c.name} ${c.role || ''}`.toLowerCase();
  if (/char-001|\bminh\b/.test(hay)) out.push('Minh');
  if (/char-002|\bnam\b/.test(hay)) out.push('Nam', 'bố', 'ba');
  if (/char-003|\blinh\b/.test(hay)) out.push('Linh', 'mẹ');
  return [...new Set(out.filter((s) => s.length >= 2))];
}

function escapeAlias(s: string) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function mentionedCharIds(text: string, characters: FamixaCharacter[]) {
  const blob = (text || '').replace(/\s+/g, ' ').trim();
  if (!blob) return [] as string[];
  const ids: string[] = [];
  for (const c of characters) {
    if (!visualChar(c)) continue;
    if (/char-004|\ban\b/i.test(`${c.id} ${c.name}`) && !/\ban (bước|đứng|vào|ngồi)/i.test(blob)) continue;
    const hit = charAliases(c).some((alias) => new RegExp(`(?:^|[^\\p{L}])${escapeAlias(alias)}(?:$|[^\\p{L}])`, 'iu').test(blob));
    if (hit) ids.push(normCharId(c.id));
  }
  return [...new Set(ids)];
}

function storedVisualIds(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  if (!shot) return [] as string[];
  const ids = shotCharacterIds(shot).filter((id) => !isVoiceOnlyChar(id) && !isOffFrameChar(id));
  if (isDefaultSceneCast(ids)) {
    const named = namedOnShot(state, shot).named;
    if (kfHasPixels(shotRunOf(state, shot)) && named.includes('CHAR-002')) return ids;
    return [];
  }
  if (kfHasPixels(shotRunOf(state, shot)) && ids.length) return ids;
  return ids;
}

function namedOnShot(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const chars = (state.characters ?? []).filter(visualChar);
  const script = deriveVoiceScript(state);
  const lines = linesForShot(state, shot, script.lines);
  const fromDialogue = [
    ...new Set(
      lines
        .filter((l) => !isVoiceOnlyChar(l.characterId, l.name) && !isOffFrameChar(l.characterId, l.name))
        .map((l) => normCharId(l.characterId))
        .filter(Boolean),
    ),
  ];
  const action = `${shot.story || ''} ${shot.motionPromptVi || ''} ${shot.visual || ''} ${shot.beatText || ''} ${shotRunOf(state, shot).shotAction || ''}`;
  const fromAction = mentionedCharIds(action, chars);
  return { fromDialogue, fromAction, named: [...new Set([...fromDialogue, ...fromAction])] };
}

export type VisibleFrameCast = {
  ids: string[];
  names: string[];
  count: number;
  fromDialogue: string[];
  fromPrevious: string[];
  fromAction: string[];
};

/**
 * People allowed in the still: this shot's dialogue + Action names + previous frame.
 * Scene roster (Minh+Nam+Linh) is not a reason to add a third person.
 */
export function visibleFrameCast(
  state: SeriesPilotState, 
  shot: FamixaSeriesShot,
  prevShot?: FamixaSeriesShot,
): VisibleFrameCast {
  const chars = (state.characters ?? []).filter(visualChar);
  const { fromDialogue, fromAction, named } = namedOnShot(state, shot);
  const prevNamed = prevShot ? namedOnShot(state, prevShot).named : [];
  const prevIds = (prevShot ? [...new Set([...storedVisualIds(state, prevShot), ...prevNamed])] : []).filter(
    (id) => !isOffFrameChar(id),
  );
  const action = `${shot.story || ''} ${shotRunOf(state, shot).shotAction || ''}`;
  const namEntering = /bước vào|vào nhà|về rồi|vào cửa|đứng đối diện/i.test(action) || named.includes('CHAR-002');
  let ids: string[];
  if (named.length) {
    ids = [...named];
    for (const id of prevIds) {
      if (ids.includes(id)) continue;
      if (id === 'CHAR-002' && !namEntering) continue;
      ids.push(id);
    }
  } else {
    ids = prevIds.length ? prevIds : storedVisualIds(state, shot);
  }
  const names = ids.map((id) => displayCharName(id, chars.find((c) => c.id === id)?.name));
  return {
    ids,
    names,
    count: ids.length,
    fromDialogue,
    fromPrevious: prevIds,
    fromAction,
  };
}

/** Script notes + dialogue acting → still mood. Does not invent hug/lesson. */
export function compileShotStillMood(state: SeriesPilotState, shot: FamixaSeriesShot, action: string) {
  const script = deriveVoiceScript(state);
  const lines = linesForShot(state, shot, script.lines);
  const beat = [action, effectiveShotAction(shot, shotRunOf(state, shot)), shot.beatText, shot.story]
    .filter((t) => (t ?? '').trim() && !looksLikePackHeading(t))
    .join(' ');
  const dir = actingOfLines(lines, beat || action);
  const faces: string[] = [];
  const sceneNode = (state.scenes ?? []).find((sc) => sceneCodeOf(sc.id) === sceneIdOfShot(shot));
  for (const c of state.characters ?? []) {
    const note = [c.voiceNote, c.performance, c.line, sceneNode?.performances?.[c.id]].filter(Boolean).join(' ');
    if (!note) continue;
    const face = stillFaceFromScriptNote(c.name || c.id, note);
    if (face) faces.push(face);
  }
  for (const role of state.roles ?? []) {
    const face = stillFaceFromScriptNote(role.name, [role.voiceNote, role.performance, role.line].filter(Boolean).join(' '));
    if (face && !faces.includes(face)) faces.push(face);
  }
  const draft = state.packDraft || '';
  for (const m of draft.matchAll(/^(Minh|Nam|Linh|Mẹ|Bố)\s*[:：]\s*(Giọng[^\n]{8,80})/gim)) {
    const face = stillFaceFromScriptNote(m[1] || '', m[2] || '');
    if (face && !faces.includes(face)) faces.push(face);
  }
  for (const act of sceneNode?.actions ?? []) {
    const face = stillFaceFromScriptNote('', act);
    if (face && !faces.includes(face)) faces.push(face);
  }
  for (const line of lines) {
    const face = stillFaceFromPerformance(line.name, resolveLinePerformance({ ...line, action: beat }));
    if (face && !faces.includes(face)) faces.push(face);
  }
  const loc = shot.location || sceneMasterOf(state, sceneIdOfShot(shot)).environment;
  const sceneNote = [sceneNode?.content, sceneNode?.performance, ...(sceneNode?.actions ?? [])].filter(Boolean).join(' ');
  return [
    stillAtmosphereFromAction(`${beat} ${sceneNote}`, loc),
    stillFaceFromScriptNote('', beat),
    ...faces,
    `Acting ${dir.intensity}/5 ${dir.label}.`,
  ]
    .filter(Boolean)
    .join(' ');
}

export function compileShotSceneCard(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  prevShot?: FamixaSeriesShot,
) {
  const script = deriveVoiceScript(state);
  const lines = linesForShot(state, shot, script.lines).filter((l) => !isVoiceOnlyChar(l.characterId, l.name));
  const master = sceneMasterOf(state, sceneIdOfShot(shot));
  const cast = visibleFrameCast(state, shot, prevShot);
  const action = effectiveShotAction(shot, shotRunOf(state, shot));
  const speakerNames = lines
    .filter((l) => !isOffFrameChar(l.characterId, l.name))
    .map((l) => (l.name || l.characterId || '').trim())
    .filter(Boolean);
  const spoken = lines.map((l) => `${l.name}: ${l.text}`).join(' · ');
  const lighting =
    master.lighting ||
    (master.time === 'evening' ? 'dim warm indoor evening' : '') ||
    'dim warm indoor evening after dinner';
  const namIn = cast.ids.some((id) => /CHAR-002/i.test(id));
  const namWas = cast.fromPrevious.some((id) => /CHAR-002/i.test(id));
  const blocking = compileShotBlocking({
    speakerNames,
    peopleNames: cast.names,
    action: `${action} ${spoken}`,
    namJustEntered: namIn && !namWas,
    namAlreadyIn: namWas,
  });
  const place = master.location || shot.location || master.environment || '';
  const prevAction = prevShot ? effectiveShotAction(prevShot, shotRunOf(state, prevShot)) : undefined;
  const prevFraming = prevShot
    ? (
        shotRunOf(state, prevShot).visualSpec ??
        deriveVisualSpec({
          shotId: prevShot.id,
          action: prevAction,
          names: visibleFrameCast(state, prevShot).names,
          ids: visibleFrameCast(state, prevShot).ids,
        })
      ).framing
    : undefined;
  const visualSpec = deriveVisualSpec({
    shotId: shot.id,
    action,
    spoken,
    location: place,
    lighting,
    names: cast.names,
    ids: cast.ids,
    speakers: speakerNames,
    camera: master.camera,
    lens: master.lens,
    prevAction,
    prevFraming,
  });
  const faceLock =
    visualSpec.framing === 'INSERT'
      ? ''
      : visualSpec.primary
        ? `PRIMARY FACE VISIBLE: ${visualSpec.primary.name} full face, looking at ${visualSpec.gazeTarget || 'the other person'}, never the lens.${
            visualSpec.secondary[0]
              ? ` SECONDARY ${visualSpec.secondary[0].name}: ${visualSpec.secondary[0].body} — face ${visualSpec.secondary[0].face}, not required unless spec says full.`
              : ''
          }`
        : '';
  const stillAction = [
    place ? `Setting: ${place}. ${lighting}.` : lighting,
    speakerNames.length && visualSpec.framing !== 'INSERT'
      ? `SPEAKER FACE VISIBLE: ${[...new Set(speakerNames)].join(', ')}. Looking at the other person, never the lens. Mouth slightly open. Never paint dialogue, subtitles, or letters.`
      : '',
    action && !looksLikePackHeading(action) ? `Action: ${action}` : '',
    blocking,
    faceLock,
  ]
    .filter(Boolean)
    .join(' ');
  const oneLiner = [spoken, action && !looksLikePackHeading(action) ? action : '']
    .filter(Boolean)
    .join(' — ')
    .replace(/\s+/g, ' ')
    .trim();
  return {
    oneLiner: oneLiner ? (oneLiner.length > 88 ? `${oneLiner.slice(0, 88)}…` : oneLiner) : 'Chưa có mô tả shot',
    stillAction,
    spoken,
    speakerNames,
    lighting,
    blocking,
    place,
    cast,
    visualSpec,
  };
}

export function peopleCountLock(cast: VisibleFrameCast, prevCount?: number) {
  const who = cast.names.join(', ') || 'the same people as the previous frame';
  const speakerNames = (cast.fromDialogue ?? [])
    .map((id) => cast.names[cast.ids.indexOf(id)] || id)
    .filter(Boolean)
    .join(', ');
  const prev =
    prevCount && prevCount > 0
      ? `Previous frame has ${prevCount} people. This shot's dialogue/action does not add anyone.`
      : '';
  return [
    `CAST COUNT LOCK: exactly ${cast.count || prevCount || 0} people in the frame: ${who}.`,
    speakerNames
      ? `SPEAKER LOCK: ${speakerNames} face visible, looking at the other person — never the lens. Do not hide a speaking parent.`
      : '',
    `FACE LOCK: primary face complete (eyes, nose, mouth). Secondary may be shoulder-only when Shot Director says so. No faceless primary / speaker torso crop.`,
    prev,
    prevCount && cast.count > prevCount
      ? `CAST PERSIST: keep everyone from the previous still (${prevCount} people) plus any new speaker. Do not drop Minh when Nam enters.`
      : '',
    prevCount && cast.count < prevCount
      ? `CAST SHRINK: only ${who} — do not keep extra people from the previous frame. Do not invent a third person.`
      : '',
    'Do not add another person. Do not add father/Nam/a third adult unless listed. No extras, no crowd, no passer-by. Classmate An is off-screen.',
    'WARDROBE LOCK: same clothes as the previous still. Do not redress Minh from a character sheet.',
    'BLOCKING LOCK: tense, stand apart. No family portrait, no huddle, no affectionate trio.',
  ]
    .filter(Boolean)
    .join(' ');
}

export function applyVisibleCast(state: SeriesPilotState, shotId: string, ids: string[]): SeriesPilotState {
  const ep = state.episode;
  if (!ep) return state;
  const visual = ids.filter((id) => !isVoiceOnlyChar(id));
  return {
    ...state,
    episode: {
      ...ep,
      shots: ep.shots.map((s) => (s.id === shotId ? { ...s, characterIds: visual, characters: visual } : s)),
    },
  };
}

export function previousSceneKf(state: SeriesPilotState, shot: FamixaSeriesShot, queue: FamixaSeriesShot[]) {
  const scene = sceneIdOfShot(shot);
  const i = queue.findIndex((s) => s.id === shot.id);
  for (let k = (i < 0 ? queue.length : i) - 1; k >= 0; k--) {
    const prev = queue[k]!;
    if (sceneIdOfShot(prev) !== scene) continue;
    const run = shotRunOf(state, prev);
    if (run.prodSkip || !shotHasValidAction(prev, run)) continue;
    if (kfHasPixels(run)) return { shot: prev, run };
  }
  return undefined;
}

export function sequentialKfIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots
    .filter((s) => {
      const run = shotRunOf(state, s);
      if (run.prodSkip || !shotHasValidAction(s, run)) return false;
      if (run.status === 'approved' || run.kfApproved === true) return false;
      if (s.voiceChainFrom && !run.kfForceNew) return false;
      return true;
    })
    .map((s) => s.id);
}

export function planEditSeconds(voiceSec: number, pauseSec = 0.2, actionSec = 0) {
  const spoken = voiceSec > 0.2 ? voiceSec + pauseSec : 0;
  const raw = Math.max(spoken, actionSec, spoken > 0 ? 1.2 : 1);
  return Math.round(raw * 10) / 10;
}

/** Runway gen4_turbo only accepts 5 or 10. Assemble trims to edit. */
export function i2vSecondsForEdit(editSec: number): 5 | 10 {
  return editSec > 5.5 ? 10 : 5;
}

export function applyEditDurations(
  state: SeriesPilotState,
  shots = episodeShots(state),
  voiceSecOf?: (lineId: string) => number,
): SeriesPilotState {
  const ep = state.episode;
  if (!ep) return state;
  const script = deriveVoiceScript(state);
  return {
    ...state,
    episode: {
      ...ep,
      shots: ep.shots.map((s) => {
        if (!shots.some((q) => q.id === s.id)) return s;
        const run = shotRunOf(state, s);
        if (!shotHasValidAction(s, run) || run.prodSkip) return s;
        const lines = linesForShot(state, s, script.lines);
        let voice = 0;
        let pause = 0.2;
        for (const line of lines) {
          voice += voiceSecOf?.(line.id) || state.voiceAssets?.[line.id]?.duration || estimateSpokenSec(line.text.replace(/\s+/g, '').length);
          pause = Math.max(pause, inferActingDirection({ text: line.text, name: line.name, action: s.story }).pauseSec);
        }
        const editSeconds = planEditSeconds(voice, pause, lines.length ? 0 : 2);
        const seconds = i2vSecondsForEdit(editSeconds);
        return { ...s, editSeconds, seconds, clock: `${seconds}s` };
      }),
    },
  };
}

export function shotProdStatus(state: SeriesPilotState, shot: FamixaSeriesShot): ShotProdStatus {
  const run = shotRunOf(state, shot);
  if (run.prodSkip) return 'HOLD';
  if (!shotHasValidAction(shot, run)) return 'HOLD';
  if (run.visualQa && !visualQaAllowsApprove(run.visualQa)) {
    const lane = qaLane(run.visualQa, visualSpecOf(state, shot));
    if (lane === 'PENDING' || lane === 'NONE') {
      return kfHasPixels(run) ? 'KF DRAFT' : 'READY';
    }
    return lane === 'REVIEW' ? 'QA REVIEW' : 'QA BLOCK';
  }
  if (run.previewUrl?.trim() && (run.status === 'approved' || state.sceneLocked)) return 'VIDEO APPROVED';
  if (run.turboStatus === 'PENDING' || run.turboStatus === 'RUNNING' || run.turboStatus === 'RETRY') return 'VIDEO QUEUED';
  if (run.previewUrl?.trim()) return 'VIDEO READY';
  if (kfIsApprovedStill(run)) return 'KF APPROVED';
  if (kfHasPixels(run)) return 'KF DRAFT';
  return 'READY';
}

export function pickShots(shots: FamixaSeriesShot[], ids?: string[]) {
  if (!ids?.length) return shots;
  const set = new Set(ids);
  return shots.filter((s) => set.has(s.id));
}

export function prodGateState(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const pack = shots.filter((s) => shotHasValidAction(s, shotRunOf(state, s)) && !shotRunOf(state, s).prodSkip);
  const kfOk = pack.length > 0 && pack.every((s) => kfIsApprovedStill(shotRunOf(state, s)) || s.voiceChainFrom);
  const videoOk = pack.length > 0 && pack.every((s) => Boolean(shotRunOf(state, s).previewUrl?.trim()) || s.voiceChainFrom);
  return {
    script: Boolean(state.scriptLocked),
    voice: Boolean(state.voiceLocked),
    shotPlan: state.shotGraphLocked === true,
    sceneMaster: allSelectedMastersLocked(state, pack),
    kf: kfOk,
    video: videoOk,
    preview: Boolean(state.previewApproved) && videoOk,
  } as Record<ProdGateId, boolean>;
}

/** Full episode Final — all 7 gates + FINAL_SOURCE=FAL for spoken + QA. */
export function fullEpisodeBlockReason(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const pack = shots.filter((s) => shotHasValidAction(s, shotRunOf(state, s)) && !shotRunOf(state, s).prodSkip);
  const g = prodGateState(state, pack);
  const miss = PROD_GATES.filter((row) => !g[row.id]).map((row) => row.label);
  if (miss.length) return `Chưa đủ gate: ${miss.join(' → ')}`;
  const script = deriveVoiceScript(state);
  const items = pack.map((s) => {
    const silent = linesForShot(state, s, script.lines).length === 0;
    const run = shotRunOf(state, s);
    return { code: s.shot || s.id, silent, finalSource: resolveFinalSource(run, silent), lipsynced: run.lipsynced };
  });
  const src = finalSourceBlockReason(items);
  if (src) return src;
  const qaMiss = pack.filter((s) => {
    const qa = shotRunOf(state, s).shotQa;
    const silent = linesForShot(state, s, script.lines).length === 0;
    return !qa?.action || !qa?.continuity || !qa?.motion || (!silent && !qa?.voiceFace);
  });
  if (qaMiss.length) {
    return `QA chưa PASS: ${qaMiss.map((s) => s.shot || s.id).join(', ')} (ACTION + CONTINUITY + MOTION + VOICE/FACE).`;
  }
  const scenes = [...new Set(pack.map(sceneIdOfShot))];
  const sceneMiss = scenes.filter((sc) => !state.sceneApproved?.[sc] && !state.previewApproved);
  if (sceneMiss.length) return `Duyệt Preview từng Scene: ${sceneMiss.join(', ')}.`;
  return undefined;
}

/** Preview range: Script + Voice + Shot Plan. Scene Master before KF. KF+Voice before video. */
export function previewRangeBlockReason(state: SeriesPilotState, shots: FamixaSeriesShot[], kind: 'kf' | 'video' | 'assemble') {
  if (!state.scriptLocked && !state.voiceLocked) return 'Khóa kịch bản trước.';
  if (!state.voiceLocked) return 'Duyệt thoại trước.';
  if (state.shotGraphLocked === false) return 'Duyệt cách chia Shot trước.';
  if (kind === 'kf' && !allSelectedMastersLocked(state, shots)) return 'Khóa Scene Master trước khi tạo hình.';
  if (kind === 'video') {
    const miss = shots.filter((s) => {
      const run = shotRunOf(state, s);
      return shotHasValidAction(s, run) && !run.prodSkip && !kfIsApprovedStill(run);
    });
    if (miss.length) return `Duyệt KF trước I2V: ${miss.length} shot còn DRAFT.`;
  }
  return undefined;
}

export type TimelineLane = {
  id: 'video' | 'voice' | 'sfx' | 'music' | 'subtitle';
  label: string;
  spans: { startSec: number; endSec: number; label: string }[];
};

export function buildTimelineLanes(tl: AssembleTimeline): TimelineLane[] {
  return [
    {
      id: 'video',
      label: 'VIDEO',
      spans: tl.clips.map((c) => ({ startSec: c.startSec, endSec: c.startSec + c.seconds, label: c.code })),
    },
    {
      id: 'voice',
      label: 'VOICE',
      spans: tl.cues.map((c) => ({ startSec: c.startSec, endSec: c.endSec, label: c.name || c.code })),
    },
    {
      id: 'sfx',
      label: 'SFX',
      spans: tl.totalSec > 0 ? [{ startSec: 0, endSec: tl.totalSec, label: 'room tone' }] : [],
    },
    { id: 'music', label: 'MUSIC', spans: [] },
    {
      id: 'subtitle',
      label: 'SUBTITLE',
      spans: tl.cues.map((c) => ({ startSec: c.startSec, endSec: c.endSec, label: c.text.slice(0, 24) })),
    },
  ];
}

export function continueScenePrompt(
  master: SceneMaster,
  prevCode: string | undefined,
  action: string,
  mode: 'same-camera' | 'new-camera' = 'same-camera',
) {
  const who = master.characters || 'the same people';
  const place = master.location || master.environment || 'the same room';
  const light = master.lighting || 'the same lighting';
  const clothes = master.wardrobe || 'the same wardrobe';
  const from = prevCode ? `from ${prevCode}` : 'from the attached previous keyframe';
  if (mode === 'new-camera') {
    return [
      `Same locked scene (${place}, ${light}, ${clothes}, ${who}). NEW CAMERA — do not copy the previous crop, zoom, or camera distance.`,
      `Do not upscale or smear a previous still. Sharp photoreal film grain, not a zoomed JPEG.`,
      `FACE SAFE: forehead, both eyes, nose, mouth, chin, hairline inside the frame. Face visible ≠ look at camera. Forbidden: cut forehead, cut chin, cheek-only, back of head, motion blur, looking into the lens.`,
      master.screenDirection ? `Screen direction lock: ${master.screenDirection}.` : '',
      `Only the Action changes: ${action}.`,
      SCENE_CONTINUITY_RULE,
    ]
      .filter(Boolean)
      .join(' ');
  }
  return [
    `Continue the exact same scene ${from}.`,
    `Preserve character identity (${who}), wardrobe from the previous still (${clothes} — do not redress from Canon), location (${place}), lighting (${light}), props (${master.props || 'same props'}), and time (${master.time || 'same time'}).`,
    master.camera ? `Camera language: ${master.camera}.` : '',
    master.lens || master.cameraHeight
      ? `Lens ${master.lens || '35mm'}, height ${master.cameraHeight || 'eye-level'}.`
      : '',
    master.screenDirection ? `Screen direction lock: ${master.screenDirection}. Do not flip left/right.` : '',
    master.coverage ? `Coverage: ${master.coverage}.` : '',
    `Preserve everything unless Shot Action explicitly requires a change. Only change the action: ${action}.`,
    'Do not copy the previous smile, huddle, or a brightened room. Faces and blocking follow this Action. Restore dim evening if the last still drifted bright.',
    SCENE_CONTINUITY_RULE,
  ]
    .filter(Boolean)
    .join(' ');
}
