/** FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 — T1–T5. 0 providers. 0 media writes. */

import {
  DIRECTOR_CURRENT_TAKE_NOTE,
  DIRECTOR_LIPSYNC_LABEL,
  DIRECTOR_LIPSYNC_NEW_LABEL,
  DIRECTOR_LIPSYNC_RESUME_LABEL,
  DIRECTOR_REMAKE_MOTION_LABEL,
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
ok(t1.secondary?.action === 'motion' && t1.secondary?.label === DIRECTOR_REMAKE_MOTION_LABEL, 'T1 secondary Tạo lại video');
ok(t1.tertiary?.action === 'picture' && t1.tertiary?.label === 'Tạo hình mới', 'T1 tertiary Tạo hình mới');
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

const t6 = deskOf(t2Snap, { type: 'CONFIRM_LIPSYNC', kind: 'CONFIRM_REQUIRED' });
ok(t6.primary?.action === 'lipsync' && t6.primary?.label === DIRECTOR_LIPSYNC_LABEL, 'T6 Khớp môi');
ok(t6.secondary?.action === 'motion', 'T6 approved take can remake before lipsync');
const t7 = deskOf(t2Snap, { type: 'ENSURE_MIX', kind: 'ENSURE' });
ok(t7.primary?.action === 'mix' && t7.secondary?.action === 'motion', 'T7 mix-stale still offers Tạo lại video');
const t8 = deskOf({ ...t2Snap, mixStale: true, finalArtifactReady: false }, { type: 'READY_FINAL', kind: 'READY' });
ok(t8.primary?.action === 'mix' && t8.primary?.label === 'Hoàn thiện Shot', 'T8 READY_FINAL + mix stale → Hoàn thiện');
const t9 = deskOf({ ...t2Snap, mixStale: false, finalArtifactReady: true }, { type: 'READY_FINAL', kind: 'READY' });
ok(t9.primary?.action === 'watch' && t9.primary?.label === 'Xem video', 'T9 READY_FINAL → Xem video');
ok(t9.secondary?.action === 'motion', 'T9 no-voice still Tạo lại video');

/** T12: READY_FINAL + thoại still shows Khớp môi — Lip-sync ✓ is not Fal-proven. */
const t12 = deskOf(
  snapOf({
    videoApproved: true,
    isSilent: false,
    voiceReady: true,
    takeFromOtherPicture: false,
    lipSyncReady: true,
    mixStale: false,
    finalArtifactReady: true,
  }),
  { type: 'READY_FINAL', kind: 'READY' },
);
ok(t12.primary?.action === 'watch', 'T12 still Xem video');
ok(t12.secondary?.action === 'lipsync' && t12.secondary?.label === DIRECTOR_LIPSYNC_LABEL, 'T12 Khớp môi on finish');
ok(t12.tertiary?.action === 'motion', 'T12 remake stays');

/** T10: STALE take + voice still offers Khớp môi — do not hide behind Tạo video only. */
const t10Snap = snapOf({
  isSilent: false,
  voiceReady: true,
  takeFromOtherPicture: false,
  execution: {
    currentArtifacts: { motion: { validity: 'STALE', url: TAKE } },
    visibleArtifacts: { motion: { validity: 'STALE', url: TAKE } },
    reason: 'Video hiện tại được tạo từ ảnh/diễn xuất cũ.',
  },
});
const t10 = deskOf(t10Snap, confirmMotion);
ok(t10.primary?.action === 'motion' && t10.primary?.label === 'Tạo video', 'T10 still Tạo video');
ok(t10.secondary?.action === 'lipsync' && t10.secondary?.label === DIRECTOR_LIPSYNC_LABEL, 'T10 Khớp môi visible');
ok(t10.tertiary?.action === 'picture', 'T10 Tạo hình mới stays tertiary');

const t11 = deskOf(
  snapOf({ isSilent: false, voiceReady: true, takeFromOtherPicture: false }),
  waitReview,
);
ok(t11.primary?.action === 'use-current-take', 'T11 CURRENT still Dùng video này');
ok(t11.tertiary?.action === 'lipsync' && t11.tertiary?.label === DIRECTOR_LIPSYNC_LABEL, 'T11 Khớp môi after Dùng video này');

/** T13: mix-stale fallback (ACCEPT_EXISTING) still offers Hỏi lại môi — do not leave only Tạo hình mới. */
const t13 = deskOf(
  snapOf({
    isSilent: false,
    voiceReady: true,
    takeFromOtherPicture: false,
    lipsyncFailed: true,
    videoApproved: true,
    execution: {
      currentArtifacts: { motion: { validity: 'STALE', url: TAKE } },
      visibleArtifacts: { motion: { validity: 'STALE', url: TAKE } },
      reason: MIX_STALE,
    },
  }),
  { type: 'ACCEPT_EXISTING', kind: 'CONFIRM_REQUIRED' },
);
ok(t13.primary?.action === 'motion' && t13.primary?.label === 'Tạo video', 'T13 stale take offers Tạo video');
ok(t13.secondary?.action === 'accept-existing', 'T13 Dùng video này stays available');

const t14 = directorPictureVideoSurface({
  snap: snapOf({
    isSilent: false,
    voiceReady: true,
    takeFromOtherPicture: false,
    lipsyncFailed: true,
    videoApproved: true,
  }),
  cmd: { type: 'CONFIRM_LIPSYNC', kind: 'CONFIRM_REQUIRED' },
  hasLiveStill: true,
  kfApproved: true,
  forceNewLipsync: true,
});
ok(t14.primary?.action === 'lipsync' && t14.primary?.label === DIRECTOR_LIPSYNC_NEW_LABEL, 'T14 dead job shows Khớp môi mới');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_EXISTING_CURRENT_TAKE_ACTION_V1 PASS FAIL=0 (no provider)');
