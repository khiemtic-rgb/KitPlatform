/** FAMIXA_PICTURE_NEW_PIXEL_INVARIANT_V1 — T1–T8. 0 providers. 0 media writes. */

import { dataUriHash } from './content-famixa-runway-pipe';
import {
  applyPictureApproveGuard,
  applyPictureGenerationGuard,
  classifyGeneratedPicture,
  classifyPictureApprove,
  currentApprovedPictureHash,
  PICTURE_DUPLICATE_COPY,
} from './content-famixa-picture-pixel-invariant';
import { directorPictureVideoSurface } from './ContentFamixaShotProduction/ShotProductionCta';
import { nextPictureRevisionId } from './ContentFamixaShotProduction/ShotProductionExecution';
import type { ShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

const fail: string[] = [];
const ok = (cond: unknown, name: string) => {
  if (!cond) fail.push(name);
};

const PIX_A = 'data:image/jpeg;base64,PIXELINVAAA111';
const PIX_B = 'data:image/jpeg;base64,PIXELINVBBB222';
const HASH_A = dataUriHash(PIX_A);
const HASH_B = dataUriHash(PIX_B);
ok(HASH_A !== HASH_B, 'fixture hashes differ');
ok(HASH_A === dataUriHash(PIX_A) && HASH_B === dataUriHash(PIX_B), 'dataUriHash algorithm unchanged');

const SHOT_ID = 'EP01-SC01-SH01';
const REV_001 = nextPictureRevisionId(SHOT_ID);
const REV_002 = nextPictureRevisionId(SHOT_ID, REV_001);
ok(REV_001 === `picture:${SHOT_ID}:001` && REV_002 === `picture:${SHOT_ID}:002`, 'revision counter helper');

const SAME_NAME = 'kf-EP01-SC01-SH01-canon.jpg';
const OTHER_NAME = 'kf-EP01-SC01-SH01-remake.jpg';
const TAKE_URL = 'https://fal.example/take-44.mp4';

function runOf(over: Record<string, unknown> = {}) {
  return {
    status: 'keyframe_ready' as const,
    kfApproved: true,
    keyframeDataUrl: PIX_A,
    keyframeFileName: SAME_NAME,
    kfSourceHash: HASH_A,
    pictureRevisionId: REV_001,
    takeUrl: TAKE_URL,
    previewUrl: TAKE_URL,
    motionNeedsRemake: false,
    ...over,
  };
}

const approvedA = runOf();
ok(currentApprovedPictureHash(approvedA) === HASH_A, 'approved identity is pixel hash');
ok(classifyGeneratedPicture(HASH_A, HASH_A) === 'DUPLICATE_PIXEL', 'classify gen A→A');
ok(classifyGeneratedPicture(HASH_B, HASH_A) === 'NEW_PENDING_PICTURE', 'classify gen A→B');
ok(classifyGeneratedPicture('', HASH_A) === 'GENERATION_FAILED', 'classify gen empty');
ok(classifyPictureApprove(HASH_A, HASH_A) === 'DUPLICATE_PIXEL', 'classify approve A→A');
ok(classifyPictureApprove(HASH_B, HASH_A) === 'APPROVE_NEW', 'classify approve A→B');

/** T1: current A, generated A → DUPLICATE, revision/motion unchanged */
const t1 = applyPictureGenerationGuard(approvedA, PIX_A);
ok(t1.kind === 'DUPLICATE_PIXEL', 'T1 DUPLICATE_PIXEL');
ok(t1.run === approvedA, 'T1 run object unchanged');
ok(t1.run.pictureRevisionId === REV_001, 'T1 revision unchanged');
ok(t1.run.kfSourceHash === HASH_A, 'T1 hash unchanged');
ok(t1.run.kfApproved === true, 'T1 stays approved');
ok(t1.run.takeUrl === TAKE_URL && t1.run.motionNeedsRemake === false, 'T1 motion unchanged');

/** T2: current A, generated B → NEW_PENDING, revision not minted */
const t2 = applyPictureGenerationGuard(approvedA, PIX_B);
ok(t2.kind === 'NEW_PENDING_PICTURE', 'T2 NEW_PENDING_PICTURE');
ok(t2.run.pictureRevisionId === REV_001, 'T2 revision not minted');
ok(t2.run.kfApproved === false, 'T2 pending');
ok(t2.run.keyframeDataUrl === PIX_B, 'T2 pending pixels B');
ok(t2.run.kfSourceHash === HASH_A, 'T2 keeps last approved hash A');
ok(t2.run.takeUrl === undefined, 'T2 new pixels detach motion');

/** T3: approve pending B → revision +1, hash B, old motion stale */
const pendingB = runOf({
  kfApproved: false,
  keyframeDataUrl: PIX_B,
  kfSourceHash: HASH_A,
  takeUrl: TAKE_URL,
  previewUrl: TAKE_URL,
  motionNeedsRemake: false,
});
const t3 = applyPictureApproveGuard(pendingB, SHOT_ID, PIX_B);
ok(t3.kind === 'APPROVE_NEW', 'T3 APPROVE_NEW');
ok(t3.run.pictureRevisionId === REV_002, 'T3 revision +1');
ok(t3.run.kfSourceHash === HASH_B, 'T3 kfSourceHash = B');
ok(t3.run.kfApproved === true, 'T3 approved');
ok(t3.run.takeUrl === undefined && t3.run.motionNeedsRemake === true, 'T3 old motion stale');
const t3fromGen = applyPictureApproveGuard(t2.run, SHOT_ID, PIX_B);
ok(t3fromGen.kind === 'APPROVE_NEW' && t3fromGen.run.pictureRevisionId === REV_002 && t3fromGen.run.kfSourceHash === HASH_B, 'T3 approve after generate still mints');

/** T4: approve current A again → revision/motion unchanged */
const t4 = applyPictureApproveGuard(approvedA, SHOT_ID, PIX_A);
ok(t4.kind === 'DUPLICATE_PIXEL', 'T4 DUPLICATE_PIXEL');
ok(t4.run === approvedA, 'T4 run object unchanged');
ok(t4.run.pictureRevisionId === REV_001, 'T4 revision unchanged');
ok(t4.run.takeUrl === TAKE_URL && t4.run.motionNeedsRemake === false, 'T4 motion unchanged');

/** T5: same filename, pixel B → NEW */
const t5 = applyPictureGenerationGuard(runOf({ keyframeFileName: SAME_NAME }), PIX_B);
ok(t5.kind === 'NEW_PENDING_PICTURE', 'T5 same filename + pixel B is NEW');
ok(t5.run.keyframeDataUrl === PIX_B, 'T5 pending B');

/** T6: different filename, pixel A → DUPLICATE */
const t6 = applyPictureGenerationGuard(runOf({ keyframeFileName: OTHER_NAME }), PIX_A);
ok(t6.kind === 'DUPLICATE_PIXEL', 'T6 different filename + pixel A is DUPLICATE');
ok(t6.run.pictureRevisionId === REV_001 && t6.run.takeUrl === TAKE_URL, 'T6 identity + motion unchanged');

/** T7: generation failure / no image → current picture unchanged */
const t7a = applyPictureGenerationGuard(approvedA, undefined);
const t7b = applyPictureGenerationGuard(approvedA, 'not-an-image');
ok(t7a.kind === 'GENERATION_FAILED' && t7a.run === approvedA, 'T7 no image');
ok(t7b.kind === 'GENERATION_FAILED' && t7b.run === approvedA, 'T7 invalid image');
ok(t7a.run.keyframeDataUrl === PIX_A && t7a.run.pictureRevisionId === REV_001, 'T7 picture unchanged');

/** T8: existing motion is not reset by duplicate pixel */
const t8 = applyPictureGenerationGuard(
  runOf({ takeUrl: TAKE_URL, previewUrl: TAKE_URL, motionNeedsRemake: false }),
  PIX_A,
);
ok(t8.kind === 'DUPLICATE_PIXEL', 'T8 DUPLICATE_PIXEL');
ok(t8.run.takeUrl === TAKE_URL && t8.run.previewUrl === TAKE_URL, 'T8 takeUrl kept');
ok(t8.run.motionNeedsRemake === false, 'T8 remake flag not reset');
ok(t8.run.pictureRevisionId === REV_001 && t8.run.kfApproved === true, 'T8 picture identity kept');

const dupDesk = directorPictureVideoSurface({
  snap: { keyframeApproved: false, pictureUnusable: false } as ShotProductionSnapshot,
  cmd: { type: 'WAIT_PICTURE_APPROVAL', kind: 'WAIT' },
  hasLiveStill: true,
  kfApproved: false,
  duplicatePixel: true,
});
ok(dupDesk.pendingApproval === false, 'UI duplicate is not pending approval');
ok(dupDesk.primary?.action !== 'approve-picture', 'UI duplicate hides Duyệt hình');
ok(dupDesk.primary?.action === 'picture' && dupDesk.note === PICTURE_DUPLICATE_COPY, 'UI duplicate asks remake');

const pendingDesk = directorPictureVideoSurface({
  snap: { keyframeApproved: false, pictureUnusable: false } as ShotProductionSnapshot,
  cmd: { type: 'WAIT_PICTURE_APPROVAL', kind: 'WAIT' },
  hasLiveStill: true,
  kfApproved: false,
});
ok(pendingDesk.pendingApproval && pendingDesk.primary?.action === 'approve-picture', 'UI new pending shows Duyệt hình');

if (fail.length) {
  console.error(`FAMIXA_PICTURE_NEW_PIXEL_INVARIANT_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PICTURE_NEW_PIXEL_INVARIANT_V1 PASS FAIL=0 (no provider)');
