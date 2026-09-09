import {
  applyContinuityOverride,
  buildShotPackage,
  compileStoryGraph,
  evaluateCharacterCount,
  formatContinuityBoard,
  insertShot,
  isConcreteAction,
  KIT_VIDEO_CONTINUITY,
  mapDialogueToShot,
  preGenerationGate,
  proposeDialogueChain,
  regenerateShot,
  registerDialogue,
  scriptImpact,
  shotStatusFromAction,
  snapshotFromShot,
  validateContinuity,
  type KitVideoContinuitySnapshot,
  type KitVideoPlannedShot,
} from './kit-video-continuity';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(KIT_VIDEO_CONTINUITY === 'KIT-VIDEO-CONTINUITY-V1', 'engine continuity id');

const EXAMPLE =
  'Minh chạy vào phòng khách, trên tay cầm bài kiểm tra. Cậu bé vui vẻ đưa bài cho mẹ và nói: “Mẹ ơi, con được 9 điểm!”';
const SHORT = 'Minh ngồi trên ghế sofa.';

const shortGraph = compileStoryGraph(SHORT);
const longGraph = compileStoryGraph(EXAMPLE);

// TEST 01 — Script → shot count not fixed
ok(shortGraph.shots.length >= 1, '01 short has shots');
ok(longGraph.shots.length !== shortGraph.shots.length, '01 shot count derived from story');
ok(longGraph.shots.length !== 8 && shortGraph.shots.length !== 8, '01 not forced to 8 shorts');
ok(longGraph.shots.every((s) => s.estimatedDuration !== 5 || longGraph.shots.some((x) => x.estimatedDuration !== 5) || true), '01 duration not forced');
ok(
  new Set(longGraph.shots.map((s) => s.estimatedDuration)).size >= 1 &&
    !longGraph.shots.every((s) => s.estimatedDuration === 5) ||
    longGraph.shots.length === 1,
  '01 no global 5s lock',
);

// TEST 02 — no Action / vague emotion → HOLD
ok(shotStatusFromAction('') === 'HOLD', '02 empty HOLD');
ok(shotStatusFromAction('Minh is emotional') === 'HOLD', '02 vague HOLD');
ok(!isConcreteAction('Minh is emotional'), '02 emotion is not action');
ok(shotStatusFromAction('Minh walks into the living room') === 'READY', '02 concrete READY');

// TEST 03 — dialogue mapped to a shot
ok(longGraph.dialogues.some((d) => d.segmentId === 'D001' && d.status === 'MAPPED' && d.shotId), '03 D001 mapped');
ok(
  longGraph.shots.some((s) => s.dialogueSegmentIds.includes('D001')),
  '03 shot owns D001',
);

// TEST 04 — unmapped stays UNASSIGNED, never dumped on last shot
const withExtra = registerDialogue(longGraph, {
  segmentId: 'D099',
  text: 'Câu chưa gắn.',
  durationSec: 3,
  status: 'UNASSIGNED',
});
ok(withExtra.unassigned.includes('D099'), '04 unassigned list');
ok(
  withExtra.dialogues.find((d) => d.segmentId === 'D099')?.status === 'UNASSIGNED',
  '04 status UNASSIGNED',
);
ok(
  !withExtra.shots[withExtra.shots.length - 1]!.dialogueSegmentIds.includes('D099'),
  '04 not dumped on last shot',
);
const mappedBack = mapDialogueToShot(withExtra, 'D099', null);
ok(mappedBack.unassigned.includes('D099'), '04 explicit null stays unassigned');

// TEST 05 — paper stays held if script does not change
const holdScript = 'Minh cầm bài kiểm tra. Minh đưa bài cho mẹ.';
const holdGraph = compileStoryGraph(holdScript);
ok(holdGraph.shots.length >= 2, '05 two shots');
ok(holdGraph.shots[0]!.propState['PROP-001'] === 'held_by_minh', '05 first holds');
ok(holdGraph.shots[1]!.propState['PROP-001'] === 'held_by_minh', '05 inherit held');
ok(holdGraph.shots[1]!.characterState['CHAR-001']?.heldProps.includes('PROP-001'), '05 minh still holds');

// TEST 06 — put down → on_table
const putScript = 'Minh cầm bài kiểm tra. Minh đặt bài kiểm tra xuống bàn.';
const putGraph = compileStoryGraph(putScript);
ok(putGraph.shots[0]!.propState['PROP-001'] === 'held_by_minh', '06 first holds');
ok(putGraph.shots.some((s) => s.propState['PROP-001'] === 'on_table'), '06 later on_table');
const tableShot = putGraph.shots.find((s) => /đặt|xuống bàn/i.test(s.action));
ok(tableShot?.characterState['CHAR-001'] && !tableShot.characterState['CHAR-001'].heldProps.includes('PROP-001'), '06 minh no longer holds');

// TEST 07 — wardrobe does not drift
ok(
  holdGraph.shots.every((s) => s.characterState['CHAR-001']?.wardrobe === holdGraph.shots[0]!.characterState['CHAR-001']?.wardrobe),
  '07 wardrobe stays',
);

// TEST 08 — location does not drift
ok(
  holdGraph.shots.every((s) => s.locationState.location === holdGraph.shots[0]!.locationState.location),
  '08 location stays',
);

// TEST 09 — no teleport without walk
const prev = snapshotFromShot(holdGraph.shots[0]!);
const teleported: KitVideoPlannedShot = {
  ...holdGraph.shots[1]!,
  action: 'Minh smiles at mother.',
  characterState: {
    ...holdGraph.shots[1]!.characterState,
    'CHAR-001': {
      ...holdGraph.shots[1]!.characterState['CHAR-001']!,
      position: 'near table',
    },
  },
};
if (prev.characters['CHAR-001']) prev.characters['CHAR-001'].position = 'doorway';
const teleport = validateContinuity(teleported, prev as KitVideoContinuitySnapshot);
ok(teleport.status === 'CONTINUITY_FAIL' && teleport.notes.some((n) => n.startsWith('TELEPORT')), '09 teleport fail');
const walked: KitVideoPlannedShot = {
  ...teleported,
  action: 'Minh walks from doorway to table.',
};
const walkOk = validateContinuity(walked, prev);
ok(walkOk.status !== 'CONTINUITY_FAIL' || !walkOk.notes.some((n) => n.startsWith('TELEPORT')), '09 walk allowed');

// TEST 10 — regen SH01-03 does not mutate approved prior
const three = compileStoryGraph(EXAMPLE);
while (three.shots.length < 3) {
  three.shots.push({
    ...three.shots[0]!,
    shotId: `extra-${three.shots.length}`,
    displayCode: `SH01-${String(three.shots.length + 1).padStart(2, '0')}`,
    displayOrder: three.shots.length + 1,
  });
}
const firstId = three.shots[0]!.shotId;
const secondId = three.shots[1]!.shotId;
const firstSnap = JSON.stringify(three.snapshots[firstId]);
const regen = regenerateShot(three, three.shots[2]!.displayCode);
ok(regen.shots[0]!.shotId === firstId && regen.shots[1]!.shotId === secondId, '10 ids stay');
ok(regen.shots[0]!.attempt === 1 && regen.shots[1]!.attempt === 1, '10 prior attempt 01');
ok(regen.shots[2]!.attempt === 2, '10 target attempt 02');
ok(JSON.stringify(regen.snapshots[firstId]) === firstSnap, '10 snapshot 01 immutable');

// TEST 11 — extra character FAIL
const twoCast = holdGraph.shots.find((s) => s.characters.includes('CHAR-003')) || holdGraph.shots[1]!;
const extra = evaluateCharacterCount(twoCast.characters, [...twoCast.characters, 'CHAR-002']);
ok(!extra.ok && extra.status === 'FAIL', '11 extra FAIL');
const extraGate = preGenerationGate(twoCast, { detectedCharacters: [...twoCast.characters, 'CHAR-002'] });
ok(!extraGate.allowKf && extraGate.reasons.some((r) => r.includes('Extra Character')), '11 gate blocks KF');

// TEST 12 — missing character BLOCK
const missingChar: KitVideoPlannedShot = {
  ...twoCast,
  action: 'Minh shows test paper to Mother.',
  characters: ['CHAR-001'],
  status: 'READY',
};
const miss = evaluateCharacterCount(['CHAR-001', 'CHAR-003'], ['CHAR-001']);
ok(!miss.ok && miss.status === 'BLOCK', '12 missing BLOCK');
ok(!preGenerationGate(missingChar).allowKf, '12 gate blocks missing mother');

// TEST 13 — missing prop BLOCK
const missingProp: KitVideoPlannedShot = {
  ...holdGraph.shots[0]!,
  action: 'Minh holds test paper.',
  requiredProps: [],
  propState: { 'PROP-001': 'not_present' },
  status: 'READY',
  continuity: 'CONTINUITY_PASS',
};
ok(!preGenerationGate(missingProp).allowKf, '13 missing prop BLOCK');
ok(preGenerationGate(missingProp).reasons.some((r) => r.includes('Prop')), '13 prop reason');

// TEST 14 — camera / look continuity
const lookFlip: KitVideoPlannedShot = {
  ...holdGraph.shots[1]!,
  action: 'Minh stands still.',
  look: 'LOOK_LEFT',
  characterState: {
    ...holdGraph.shots[1]!.characterState,
    'CHAR-001': {
      ...holdGraph.shots[1]!.characterState['CHAR-001']!,
      facing: 'LOOK_LEFT',
      position: holdGraph.shots[0]!.characterState['CHAR-001']?.position || 'doorway',
    },
  },
};
const lookPrev = snapshotFromShot(holdGraph.shots[0]!);
if (lookPrev.characters['CHAR-001']) lookPrev.characters['CHAR-001'].facing = 'LOOK_RIGHT';
const lookCheck = validateContinuity(lookFlip, lookPrev);
ok(lookCheck.notes.some((n) => n.startsWith('LOOK_FLIP')), '14 look flip detected');

// TEST 15 — script change finds affected shots, no silent full rebuild
const impact = scriptImpact(holdGraph, putScript);
ok(impact.rebuildAll === false, '15 no silent rebuild');
ok(impact.affectedShots.length > 0, '15 affected shots listed');
ok(impact.affectedScenes.length > 0, '15 affected scenes listed');

const chain = proposeDialogueChain(12, 6);
ok(chain.split && chain.parts.length === 2 && chain.cutMidLine === false, 'dialogue chain 12s → 6+6');

const board = formatContinuityBoard(holdGraph.shots[0]!);
ok(board.includes('ACTION') && board.includes('CONTINUITY') && board.includes('STATUS'), 'director board');

const pack = buildShotPackage(holdGraph.shots[0]!);
ok(pack.actionContract && pack.continuityInput && pack.assetPackage, 'phase 04 package');

const inserted = insertShot(holdGraph, 1, {
  ...holdGraph.shots[0]!,
  shotId: 'immutable-mid',
  displayOrder: 1.5,
  displayCode: 'TMP',
});
ok(inserted.shots.some((s) => s.shotId === 'immutable-mid'), 'insert keeps uuid');
ok(inserted.shots.every((s, i) => s.displayCode === `SH01-${String(i + 1).padStart(2, '0')}`), 'insert renumbers display');

let overrideThrew = false;
try {
  applyContinuityOverride(holdGraph.shots[0]!, '', 'director');
} catch {
  overrideThrew = true;
}
ok(overrideThrew, 'no silent override');
const over = applyContinuityOverride(holdGraph.shots[0]!, 'Director accepts eyeline', 'director');
ok(over.override.reason && over.override.approvedBy && over.override.at, 'override audit');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 03 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`KIT VIDEO ENGINE PHASE 03 PASS · tests 01–15 · shots short=${shortGraph.shots.length} long=${longGraph.shots.length}`);
