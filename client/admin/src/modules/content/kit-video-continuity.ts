/** KIT Video Engine Phase 03 — Scene / Beat / Shot / Continuity. Script is SoT. No Gemini. */

export const KIT_VIDEO_CONTINUITY = 'KIT-VIDEO-CONTINUITY-V1';

const KNOWN = [
  { code: 'CHAR-001', name: 'Minh', aliases: ['minh', 'cậu bé', 'cau be'] },
  { code: 'CHAR-002', name: 'Nam', aliases: ['nam', 'bố'] },
  { code: 'CHAR-003', name: 'Linh', aliases: ['linh', 'mẹ', 'mother'] },
] as const;

const VERB = '(chạy|bước|đi|đưa|giơ|cầm|đặt|ngồi|đứng|mở|nhìn|nói|khoe|raises?|walks?|runs?|shows?|holds?|puts?|sits?|stands?|approaches?)';
const CONCRETE = new RegExp(`(^|[^\\p{L}])${VERB}(?=$|[^\\p{L}])`, 'iu');
const VAGUE = /(^|[^\p{L}])(emotional|cảm xúc|buồn|vui vẻ)\s*$/iu;
const MOVE = /(chạy|bước|đi|walks?|runs?|approaches?|từ .+ đến|from .+ to)/iu;
const PUT_DOWN = /(đặt .+ xuống|xuống bàn|puts? .+ (down|on the table)|on_table)/iu;
const PRIMARY_ACTION = /(chạy|bước|đi vào|đưa|giơ|đặt|nói|walks?|runs?|shows?|raises?|approaches?|puts?)/iu;

export type KitVideoPropState = 'not_present' | 'held_by_minh' | 'held_by_mother' | 'on_table';
export type KitVideoContinuityStatus = 'CONTINUITY_PASS' | 'CONTINUITY_WARNING' | 'CONTINUITY_FAIL';
export type KitVideoShotPurpose = 'ESTABLISH' | 'ACTION' | 'REACTION' | 'DIALOGUE' | 'DETAIL' | 'TRANSITION';
export type KitVideoScreenDir = 'SCREEN_LEFT' | 'SCREEN_RIGHT' | 'CENTER';
export type KitVideoLook = 'LOOK_LEFT' | 'LOOK_RIGHT' | 'LOOK_CENTER';
export type KitVideoTransition = 'CONTINUE' | 'CUT' | 'PUSH_IN' | 'REVERSE' | 'WIDE_TO_CLOSE';

export type KitVideoCharacterState = {
  code: string;
  position: string;
  facing?: KitVideoLook;
  pose?: string;
  emotion?: string;
  action?: string;
  wardrobe: string;
  heldProps: string[];
};

export type KitVideoLocationState = {
  location: string;
  cameraPosition?: string;
  visibleFurniture?: string[];
  importantObjects?: string[];
  lighting?: string;
  time?: string;
};

export type KitVideoContinuitySnapshot = {
  shotId: string;
  immutable: true;
  characters: Record<string, KitVideoCharacterState>;
  props: Record<string, KitVideoPropState>;
  location: KitVideoLocationState;
  lighting?: string;
  camera?: { angle?: string; side?: KitVideoScreenDir; scale?: string };
  facts: string[];
};

export type KitVideoDialogueSegment = {
  segmentId: string;
  text: string;
  durationSec: number;
  shotId?: string | null;
  status: 'MAPPED' | 'UNASSIGNED';
};

export type KitVideoActionContract = {
  action: string;
  requiredVisible: string[];
  blocked: boolean;
};

export type KitVideoPlannedShot = {
  shotId: string;
  displayCode: string;
  displayOrder: number;
  sceneId: string;
  beatId: string;
  attempt: number;
  action: string;
  purpose: KitVideoShotPurpose;
  estimatedDuration: number;
  characters: string[];
  dialogueSegmentIds: string[];
  requiredProps: string[];
  camera?: string;
  composition?: string;
  emotion?: string;
  screen?: KitVideoScreenDir;
  look?: KitVideoLook;
  transitionFromPrevious: KitVideoTransition;
  voiceChainFrom?: string;
  status: 'DRAFT' | 'HOLD' | 'READY' | 'BLOCKED' | 'FAILED' | 'REJECTED';
  continuity: KitVideoContinuityStatus;
  continuityNotes: string[];
  contract: KitVideoActionContract;
  characterState: Record<string, KitVideoCharacterState>;
  locationState: KitVideoLocationState;
  propState: Record<string, KitVideoPropState>;
};

export type KitVideoStoryBeat = {
  beatId: string;
  order: number;
  text: string;
  characters: string[];
  action: string;
  emotion?: string;
  dialogueSegmentIds: string[];
  requiredProps: string[];
};

export type KitVideoStoryScene = {
  sceneId: string;
  sceneOrder: number;
  location: string;
  time: string;
  characters: string[];
  wardrobe: Record<string, string>;
  initialState: Record<string, string>;
  objective: string;
  beats: KitVideoStoryBeat[];
};

export type KitVideoStoryGraph = {
  scenes: KitVideoStoryScene[];
  shots: KitVideoPlannedShot[];
  dialogues: KitVideoDialogueSegment[];
  snapshots: Record<string, KitVideoContinuitySnapshot>;
  unassigned: string[];
};

export function isConcreteAction(action?: string) {
  const t = (action || '').trim();
  if (!t) return false;
  if (VAGUE.test(t) && !CONCRETE.test(t)) return false;
  return CONCRETE.test(t);
}

export function shotStatusFromAction(action?: string): KitVideoPlannedShot['status'] {
  return isConcreteAction(action) ? 'READY' : 'HOLD';
}

export function mentionedCharacters(script: string) {
  const hay = ` ${script.toLowerCase()} `;
  return KNOWN.filter((k) => hay.includes(` ${k.name.toLowerCase()} `) || k.aliases.some((a) => hay.includes(a))).map(
    (k) => k.code,
  );
}

export function mentionedProps(script: string) {
  return /bài kiểm tra|test paper|điểm/i.test(script) ? ['PROP-001'] : [];
}

export function mentionedLocation(script: string) {
  return /phòng khách|living room/i.test(script) ? 'LOC-001' : '';
}

function clauses(script: string) {
  return script
    .split(/(?<=[.!?])\s+/)
    .map((s) => s.trim())
    .filter((s) => s.length > 8);
}

function extractDialogue(script: string): { text: string; durationSec: number }[] {
  const found = [...script.matchAll(/[“"]([^”"]+)[”"]/g)].map((m) => m[1]!.trim());
  return found.map((text) => ({ text, durationSec: Math.max(2, Math.round(text.split(/\s+/).length * 0.45)) }));
}

function purposeOf(action: string, hasDialogue: boolean): KitVideoShotPurpose {
  if (hasDialogue) return 'DIALOGUE';
  if (/chạy vào|bước vào|phòng khách|living room/i.test(action)) return 'ESTABLISH';
  if (/liếc|chi tiết|insert/i.test(action)) return 'DETAIL';
  return 'ACTION';
}

function positionFromAction(action: string, prev?: string) {
  if (/cửa|doorway/i.test(action)) return 'doorway';
  if (/bàn|table|mẹ|mother/i.test(action)) return 'near table';
  return prev || 'doorway';
}

export function proposeBeats(script: string): KitVideoStoryBeat[] {
  const chars = mentionedCharacters(script);
  const props = mentionedProps(script);
  const parts = clauses(script);
  const units = parts.length ? parts : [script.trim()].filter(Boolean);
  return units.map((text, i) => ({
    beatId: `BEAT-${String(i + 1).padStart(2, '0')}`,
    order: i + 1,
    text,
    characters: mentionedCharacters(text).length ? mentionedCharacters(text) : chars,
    action: text.replace(/[“"][^”"]+[”"]/g, '').trim() || text,
    emotion: /vui|excited|hớn hở/i.test(text) ? 'excited' : undefined,
    dialogueSegmentIds: /[“"]/.test(text) || /nói/i.test(text) ? [] : [],
    requiredProps: mentionedProps(text).length ? mentionedProps(text) : /đưa|cầm|giơ|shows?|holds?/i.test(text) ? props : [],
  }));
}

export function proposeSplit(beat: KitVideoStoryBeat): { actions: string[]; reason: string } {
  const text = beat.action;
  const bits = text
    .split(/,\s+| và (?=nói|đưa|giơ|chạy)/i)
    .map((s) => s.trim())
    .filter(Boolean);
  const actionBits = bits.filter((b) => PRIMARY_ACTION.test(b));
  if (actionBits.length >= 2) {
    return { actions: actionBits, reason: 'Action boundary' };
  }
  return { actions: [beat.action], reason: 'ONE SHOT = ONE CLEAR VISUAL ACTION' };
}

function inheritState(
  prev: KitVideoContinuitySnapshot | undefined,
  codes: string[],
  location: string,
  props: string[],
  action: string,
): {
  characters: Record<string, KitVideoCharacterState>;
  propState: Record<string, KitVideoPropState>;
  location: KitVideoLocationState;
} {
  const characters: Record<string, KitVideoCharacterState> = {};
  for (const code of codes) {
    const old = prev?.characters[code];
    const moved = MOVE.test(action);
    characters[code] = {
      code,
      position: moved ? positionFromAction(action, old?.position) : old?.position || positionFromAction(action),
      facing: old?.facing || 'LOOK_RIGHT',
      wardrobe: old?.wardrobe || (code === 'CHAR-001' ? 'WARDROBE-001' : 'WARDROBE-002'),
      heldProps: old?.heldProps ? [...old.heldProps] : [],
      emotion: old?.emotion,
      action,
    };
  }
  const propState: Record<string, KitVideoPropState> = { ...(prev?.props || {}) };
  for (const p of props) {
    if (!(p in propState)) propState[p] = 'not_present';
    if (/cầm|holds?|trên tay|raises?|shows?|đưa/i.test(action) && !PUT_DOWN.test(action)) {
      propState[p] = 'held_by_minh';
      if (characters['CHAR-001'] && !characters['CHAR-001'].heldProps.includes(p)) {
        characters['CHAR-001'].heldProps.push(p);
      }
    }
    if (PUT_DOWN.test(action)) {
      propState[p] = 'on_table';
      if (characters['CHAR-001']) {
        characters['CHAR-001'].heldProps = characters['CHAR-001'].heldProps.filter((x) => x !== p);
      }
    }
  }
  return {
    characters,
    propState,
    location: {
      location: prev?.location.location || location,
      lighting: prev?.location.lighting || 'evening indoor',
      time: prev?.location.time || 'Evening',
      visibleFurniture: prev?.location.visibleFurniture || ['sofa', 'TV cabinet', 'low table'],
    },
  };
}

export function compileStoryGraph(script: string): KitVideoStoryGraph {
  const chars = mentionedCharacters(script);
  const loc = mentionedLocation(script) || 'LOC-001';
  const props = mentionedProps(script);
  const objective = /khoe|điểm|test paper|9 điểm/i.test(script) ? 'Minh muốn khoe điểm với mẹ.' : '';
  const beats = proposeBeats(script);
  const quotes = extractDialogue(script);
  const dialogues: KitVideoDialogueSegment[] = quotes.map((q, i) => ({
    segmentId: `D${String(i + 1).padStart(3, '0')}`,
    text: q.text,
    durationSec: q.durationSec,
    shotId: null,
    status: 'UNASSIGNED',
  }));

  const scene: KitVideoStoryScene = {
    sceneId: 'SC01',
    sceneOrder: 1,
    location: loc,
    time: /tối|evening|đêm/i.test(script) ? 'Evening' : 'Unspecified',
    characters: chars,
    wardrobe: Object.fromEntries(chars.map((c) => [c, c === 'CHAR-001' ? 'WARDROBE-001' : 'WARDROBE-002'])),
    initialState: {},
    objective,
    beats,
  };

  const shots: KitVideoPlannedShot[] = [];
  let prevSnap: KitVideoContinuitySnapshot | undefined;
  const snapshots: Record<string, KitVideoContinuitySnapshot> = {};
  let display = 0;
  let dialogueCursor = 0;

  for (const beat of beats) {
    const split = proposeSplit(beat);
    for (const action of split.actions) {
      display += 1;
      const shotId = cryptoRandom();
      const displayCode = `SH01-${String(display).padStart(2, '0')}`;
      const hasLine =
        /nói|says?/i.test(action) ||
        (dialogues[dialogueCursor] ? action.includes(dialogues[dialogueCursor]!.text) : false);
      const line = hasLine && dialogues[dialogueCursor] ? dialogues[dialogueCursor] : undefined;
      if (line) {
        line.shotId = shotId;
        line.status = 'MAPPED';
        dialogueCursor += 1;
      }
      const inherit = inheritState(prevSnap, beat.characters.length ? beat.characters : chars, loc, beat.requiredProps.length ? beat.requiredProps : props, action);
      const duration = line && line.durationSec > 10 ? 10 : Math.max(3, Math.min(10, Math.round(action.split(/\s+/).length * 0.35 + (line?.durationSec || 0) * 0.5)));
      const chain =
        line && line.durationSec > 10
          ? { voiceChainFrom: shotId, extra: Math.ceil(line.durationSec / 6) }
          : undefined;
      const status = shotStatusFromAction(action);
      const contract = actionVisibilityContract(action, inherit.characters, inherit.propState);
      const shot: KitVideoPlannedShot = {
        shotId,
        displayCode,
        displayOrder: display,
        sceneId: scene.sceneId,
        beatId: beat.beatId,
        attempt: 1,
        action,
        purpose: purposeOf(action, Boolean(line)),
        estimatedDuration: duration,
        characters: Object.keys(inherit.characters),
        dialogueSegmentIds: line ? [line.segmentId] : [],
        requiredProps: Object.entries(inherit.propState)
          .filter(([, st]) => st !== 'not_present')
          .map(([k]) => k),
        emotion: beat.emotion,
        screen: 'SCREEN_LEFT',
        look: inherit.characters['CHAR-001']?.facing || 'LOOK_RIGHT',
        transitionFromPrevious: display === 1 ? 'CUT' : 'CONTINUE',
        status,
        continuity: 'CONTINUITY_PASS',
        continuityNotes: [],
        contract,
        characterState: inherit.characters,
        locationState: inherit.location,
        propState: inherit.propState,
      };
      if (chain && line && line.durationSec > 10) {
        shot.voiceChainFrom = undefined;
        shot.estimatedDuration = 6;
      }
      const validated = validateContinuity(shot, prevSnap);
      shot.continuity = validated.status;
      shot.continuityNotes = validated.notes;
      if (validated.status === 'CONTINUITY_FAIL') shot.status = 'BLOCKED';
      if (contract.blocked) shot.status = 'BLOCKED';
      shots.push(shot);
      const snap = snapshotFromShot(shot);
      snapshots[shot.shotId] = snap;
      prevSnap = snap;

      if (line && line.durationSec > 10) {
        const extra = Math.ceil(line.durationSec / 6) - 1;
        for (let i = 0; i < extra; i += 1) {
          display += 1;
          const childId = cryptoRandom();
          shots.push({
            ...shot,
            shotId: childId,
            displayCode: `SH01-${String(display).padStart(2, '0')}`,
            displayOrder: display,
            voiceChainFrom: shot.shotId,
            dialogueSegmentIds: [line.segmentId],
            estimatedDuration: 6,
            transitionFromPrevious: 'CONTINUE',
            purpose: 'DIALOGUE',
          });
        }
      }
    }
  }

  return {
    scenes: [scene],
    shots,
    dialogues,
    snapshots,
    unassigned: dialogues.filter((d) => d.status === 'UNASSIGNED').map((d) => d.segmentId),
  };
}

export function actionVisibilityContract(
  action: string,
  characters: Record<string, KitVideoCharacterState>,
  props: Record<string, KitVideoPropState>,
): KitVideoActionContract {
  const required: string[] = [];
  if (/minh|CHAR-001/i.test(action) || characters['CHAR-001']) required.push('Minh');
  if (/mẹ|linh|mother|CHAR-003/i.test(action) || (/đưa|cho mẹ|shows?/i.test(action) && characters['CHAR-003'])) {
    required.push('Mother');
  }
  if (/bài|paper|PROP-001/i.test(action) || Object.values(props).some((s) => s !== 'not_present')) {
    required.push('Test paper');
  }
  if (/đưa|shows?|raises?/i.test(action)) {
    required.push('Minh holding paper');
    if (required.includes('Mother')) required.push('Mother able to see paper');
  }
  const blocked = required.length === 0 && !isConcreteAction(action);
  return { action, requiredVisible: [...new Set(required)], blocked };
}

export function validateContinuity(
  shot: KitVideoPlannedShot,
  prev?: KitVideoContinuitySnapshot,
): { status: KitVideoContinuityStatus; notes: string[] } {
  const notes: string[] = [];
  if (!prev) return { status: 'CONTINUITY_PASS', notes };
  for (const [code, st] of Object.entries(shot.characterState)) {
    const before = prev.characters[code];
    if (!before) continue;
    if (st.wardrobe !== before.wardrobe) notes.push(`WARDROBE_DRIFT:${code}`);
    if (st.position !== before.position && !MOVE.test(shot.action)) notes.push(`TELEPORT:${code}`);
    if (before.facing && st.facing && before.facing !== st.facing && !/quay|turns?|looks? (left|right)/i.test(shot.action)) {
      notes.push(`LOOK_FLIP:${code}`);
    }
  }
  if (shot.locationState.location !== prev.location.location && !/đến|khác|leaves?|cut to/i.test(shot.action)) {
    notes.push('LOCATION_DRIFT');
  }
  if (prev.camera?.side && shot.screen && prev.camera.side !== shot.screen && shot.transitionFromPrevious === 'CONTINUE') {
    notes.push('AXIS_JUMP');
  }
  const fail = notes.some((n) => n.startsWith('TELEPORT') || n === 'LOCATION_DRIFT' || n.startsWith('WARDROBE'));
  return {
    status: fail ? 'CONTINUITY_FAIL' : notes.length ? 'CONTINUITY_WARNING' : 'CONTINUITY_PASS',
    notes,
  };
}

export function snapshotFromShot(shot: KitVideoPlannedShot): KitVideoContinuitySnapshot {
  return {
    shotId: shot.shotId,
    immutable: true,
    characters: structuredClone(shot.characterState),
    props: { ...shot.propState },
    location: { ...shot.locationState },
    lighting: shot.locationState.lighting,
    camera: { side: shot.screen, scale: shot.purpose },
    facts: [`action=${shot.action}`],
  };
}

export function regenerateShot(graph: KitVideoStoryGraph, displayCode: string): KitVideoStoryGraph {
  const target = graph.shots.find((s) => s.displayCode === displayCode);
  if (!target) return graph;
  const nextShots = graph.shots.map((s) =>
    s.shotId === target.shotId
      ? { ...s, attempt: s.attempt + 1, status: s.status === 'HOLD' ? s.status : 'DRAFT' }
      : s,
  );
  return { ...graph, shots: nextShots, snapshots: { ...graph.snapshots } };
}

export function insertShot(graph: KitVideoStoryGraph, afterDisplay: number, shot: KitVideoPlannedShot): KitVideoStoryGraph {
  const shots = [...graph.shots, shot].sort((a, b) => a.displayOrder - b.displayOrder);
  const shifted = shots.map((s, i) => ({
    ...s,
    displayOrder: i + 1,
    displayCode: `SH01-${String(i + 1).padStart(2, '0')}`,
  }));
  return { ...graph, shots: shifted };
}

export function scriptImpact(before: KitVideoStoryGraph, afterScript: string) {
  const next = compileStoryGraph(afterScript);
  const affectedScenes = next.scenes
    .filter((s, i) => JSON.stringify(s.beats.map((b) => b.text)) !== JSON.stringify(before.scenes[i]?.beats.map((b) => b.text)))
    .map((s) => s.sceneId);
  const affectedBeats = next.scenes.flatMap((s) => s.beats.map((b) => b.beatId));
  const prevActions = before.shots.map((s) => s.action);
  const affectedShots = next.shots.filter((s) => !prevActions.includes(s.action)).map((s) => s.displayCode);
  return {
    rebuildAll: false,
    affectedScenes: affectedScenes.length ? affectedScenes : next.scenes.map((s) => s.sceneId),
    affectedBeats,
    affectedShots,
    affectedContinuity: affectedShots,
  };
}

export function evaluateCharacterCount(required: string[], detected: string[]) {
  const extra = detected.filter((d) => !required.includes(d));
  const missing = required.filter((r) => !detected.includes(r));
  if (extra.length) return { ok: false, status: 'FAIL' as const, reasons: [`Extra Character: ${extra.join(',')}`] };
  if (missing.length) return { ok: false, status: 'BLOCK' as const, reasons: [`Missing Character: ${missing.join(',')}`] };
  if (detected.length !== required.length) return { ok: false, status: 'FAIL' as const, reasons: ['Detected ≠ Required'] };
  return { ok: true, status: 'PASS' as const, reasons: [] as string[] };
}

export function registerDialogue(graph: KitVideoStoryGraph, segment: KitVideoDialogueSegment): KitVideoStoryGraph {
  return {
    ...graph,
    dialogues: [...graph.dialogues, { ...segment, shotId: null, status: 'UNASSIGNED' }],
    unassigned: [...graph.unassigned, segment.segmentId],
  };
}

export function mapDialogueToShot(graph: KitVideoStoryGraph, segmentId: string, shotId: string | null): KitVideoStoryGraph {
  const dialogues = graph.dialogues.map((d) => {
    if (d.segmentId !== segmentId) return d;
    if (!shotId) return { ...d, shotId: null, status: 'UNASSIGNED' as const };
    return { ...d, shotId, status: 'MAPPED' as const };
  });
  const shots = graph.shots.map((s) => ({
    ...s,
    dialogueSegmentIds: [
      ...s.dialogueSegmentIds.filter((id) => id !== segmentId),
      ...(shotId && s.shotId === shotId ? [segmentId] : []),
    ],
  }));
  return {
    ...graph,
    dialogues,
    shots,
    unassigned: dialogues.filter((d) => d.status === 'UNASSIGNED').map((d) => d.segmentId),
  };
}

export function proposeDialogueChain(dialogueSec: number, shotSec: number) {
  if (dialogueSec <= shotSec) return { split: false as const, parts: [shotSec], voiceChainFrom: undefined };
  const n = Math.ceil(dialogueSec / shotSec);
  return {
    split: true as const,
    parts: Array.from({ length: n }, () => shotSec),
    voiceChainFrom: 'FIRST_SHOT',
    cutMidLine: false,
  };
}

export function preGenerationGate(
  shot: KitVideoPlannedShot,
  extras?: { detectedCharacters?: string[]; resolvedAssets?: string[] },
) {
  const reasons: string[] = [];
  if (shot.status === 'HOLD' || !isConcreteAction(shot.action)) reasons.push('Action missing');
  if (!shot.characters.length) reasons.push('Required Character missing');
  if (/mẹ|mother|linh|CHAR-003/i.test(shot.action) && !shot.characters.includes('CHAR-003')) {
    reasons.push('Required Character missing');
  }
  const needsPaper = /bài|paper|cầm|holds?|đưa|shows?/i.test(shot.action);
  if (needsPaper && ((shot.propState['PROP-001'] || 'not_present') === 'not_present' || !shot.requiredProps.includes('PROP-001'))) {
    reasons.push('Required Prop missing');
  }
  if (shot.continuity === 'CONTINUITY_FAIL') reasons.push('Continuity FAIL');
  if (extras?.resolvedAssets && extras.resolvedAssets.includes('UNRESOLVED')) reasons.push('Asset unresolved');
  if (extras?.detectedCharacters) {
    const count = evaluateCharacterCount(shot.characters, extras.detectedCharacters);
    if (!count.ok) reasons.push(...count.reasons);
  }
  const allowKf =
    reasons.length === 0 && shot.status === 'READY' && shot.continuity !== 'CONTINUITY_FAIL';
  return { ok: allowKf, reasons, allowKf };
}

export function buildShotPackage(shot: KitVideoPlannedShot) {
  return {
    shot,
    assetPackage: {
      characters: shot.characters,
      props: shot.requiredProps,
      location: shot.locationState.location,
    },
    continuityInput: {
      characters: shot.characterState,
      props: shot.propState,
      location: shot.locationState,
      camera: { side: shot.screen, look: shot.look },
    },
    actionContract: shot.contract,
  };
}

export function applyContinuityOverride(
  shot: KitVideoPlannedShot,
  reason: string,
  approvedBy: string,
): { shot: KitVideoPlannedShot; override: { reason: string; approvedBy: string; at: string } } {
  if (!reason.trim() || !approvedBy.trim()) throw new Error('OverrideReason + ApprovedBy bắt buộc.');
  return {
    shot: { ...shot, continuity: 'CONTINUITY_WARNING', continuityNotes: [...shot.continuityNotes, `OVERRIDE:${reason}`] },
    override: { reason: reason.trim(), approvedBy: approvedBy.trim(), at: new Date().toISOString() },
  };
}

export function formatContinuityBoard(shot: KitVideoPlannedShot) {
  const cont = [
    shot.continuityNotes.some((n) => n.startsWith('WARDROBE')) ? '✗ Wardrobe' : '✓ Wardrobe',
    shot.continuityNotes.some((n) => n === 'LOCATION_DRIFT') ? '✗ Location' : '✓ Location',
    shot.continuityNotes.some((n) => n.startsWith('TELEPORT')) ? '✗ Position' : '✓ Position',
    shot.continuityNotes.some((n) => n.includes('PROP')) ? '✗ Props' : '✓ Props',
  ];
  return [
    shot.displayCode,
    '',
    'ACTION',
    shot.action || '(none)',
    '',
    'CHARACTERS',
    shot.characters.join('\n') || '—',
    '',
    'PROPS',
    shot.requiredProps.join('\n') || '—',
    '',
    'DIALOGUE',
    shot.dialogueSegmentIds.join(', ') || 'UNASSIGNED',
    '',
    'CONTINUITY',
    ...cont,
    '',
    'STATUS',
    shot.status,
  ].join('\n');
}

function cryptoRandom() {
  return `shot-${Math.random().toString(36).slice(2, 10)}${Date.now().toString(36)}`;
}
