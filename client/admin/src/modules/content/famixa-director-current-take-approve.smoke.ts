/** FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 — T1–T5. 0 providers. 0 media writes. */

import {
  DIRECTOR_CURRENT_TAKE_NOTE,
  directorExecutionNote,
  directorPictureVideoSurface,
} from './ContentFamixaShotProduction/ShotProductionCta';
import type { ShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import type { ShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

const fail: string[] = [];
const ok = (cond: unknown, name: string) => {
  if (!cond) fail.push(name);
};

const MIX_STALE = 'Bản mix không còn khớp nguồn hiện tại.';
const TAKE = 'https://fal.example/take-53.mp4';

function snapOf(over: Partial<ShotProductionSnapshot> = {}): ShotProductionSnapshot {
  return {
    keyframeReady: true,
    keyframeApproved: true,
    pictureUnusable: false,
    picturePendingApproval: false,
    motionBusy: false,
    videoApproved: false,
    visibleTake: { n: 53, status: 'SUCCESS', url: TAKE },
    execution: {
      currentArtifacts: { motion: { validity: 'CURRENT', url: TAKE } },
      acceptedArtifacts: {},
      visibleArtifacts: { motion: { validity: 'CURRENT', url: TAKE } },
      reason: MIX_STALE,
    },
    ...over,
  } as ShotProductionSnapshot;
}

function deskOf(snap: ShotProductionSnapshot, cmd: ShotProductionCommand) {
  return directorPictureVideoSurface({
    snap,
    cmd,
    hasLiveStill: true,
    kfApproved: true,
  });
}

const waitReview: ShotProductionCommand = { type: 'WAIT_VIDEO_REVIEW', kind: 'APPROVAL_REQUIRED' };
const confirmMotion: ShotProductionCommand = { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED' };
const ensurePicture: ShotProductionCommand = { type: 'ENSURE_PICTURE', kind: 'ENSURE' };

/** T1: CURRENT + !approved → Dùng video này */
const t1Snap = snapOf();
const t1 = deskOf(t1Snap, waitReview);
ok(t1.primary?.label === 'Dùng video này', 'T1 label');
ok(t1.primary?.action === 'use-current-take', 'T1 action is use-current-take not ACCEPT_EXISTING');
ok(t1.secondary?.action === 'picture' && t1.secondary?.label === 'Tạo hình mới', 'T1 secondary Tạo hình mới');
ok(t1.note === DIRECTOR_CURRENT_TAKE_NOTE, 'T1 note');

/** T2: CURRENT + approved → Dùng video này hidden */
const t2Snap = snapOf({ videoApproved: true });
const t2 = deskOf(t2Snap, { type: 'LIPSYNC_QA_REQUIRED', kind: 'APPROVAL_REQUIRED' });
ok(t2.primary?.label !== 'Dùng video này' && t2.primary?.action !== 'use-current-take', 'T2 no Dùng video này');

/** T3: STALE → Tạo video */
const t3Snap = snapOf({
  execution: {
    currentArtifacts: { motion: { validity: 'STALE', url: TAKE } },
    visibleArtifacts: { motion: { validity: 'STALE', url: TAKE } },
    reason: 'Video hiện tại được tạo từ ảnh/diễn xuất cũ.',
  },
});
const t3 = deskOf(t3Snap, confirmMotion);
ok(t3.primary?.label === 'Tạo video' && t3.primary?.action === 'motion', 'T3 Tạo video');

/** T4: no motion → Tạo video */
const t4Snap = snapOf({
  visibleTake: undefined,
  execution: { currentArtifacts: {}, reason: '' },
});
const t4 = deskOf(t4Snap, confirmMotion);
ok(t4.primary?.label === 'Tạo video' && t4.primary?.action === 'motion', 'T4 Tạo video');

/** T5: mix STALE + motion CURRENT → motion note, not mix stale */
const t5Snap = snapOf();
const t5 = deskOf(t5Snap, waitReview);
ok(t5.note === DIRECTOR_CURRENT_TAKE_NOTE, 'T5 desk note is current-take');
ok(t5.note !== MIX_STALE, 'T5 desk note is not mix stale');
ok(
  directorExecutionNote({ snap: t5Snap, cmd: waitReview }) === DIRECTOR_CURRENT_TAKE_NOTE,
  'T5 directorExecutionNote prefers motion',
);
ok(t5Snap.execution?.reason === MIX_STALE, 'T5 execution.reason unchanged');

ok(!t1Snap.videoApproved && t1.primary?.action !== 'accept-existing', 'no ACCEPT_EXISTING action');
ok(!ensurePicture.type || t4.primary?.action !== 'accept-existing', 'no accept on empty motion');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 PASS FAIL=0 (no provider)');
