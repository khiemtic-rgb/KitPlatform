import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { directorPictureVideoSurface, directorPrimaryCta, shotPipelineBusy, shotQaMissing, shotStaleNotes, staffPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';
import { resolveLaneAShotId, sceneShotsOf } from './ContentFamixaShotProduction/ShotProductionNavigator';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { resolveShotPreviewKind, SHOT_PREVIEW_LABEL } from './ContentFamixaShotProduction/ShotProductionPreview';
import { shotProgressRows } from './ContentFamixaShotProduction/ShotProductionProgress';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import { dataUriHash } from './content-famixa-runway-pipe';
import { CAMERA_RETRY_POOL } from './content-runway-prompt-v1';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { DIRECTOR_SHOT_COMMAND_LABEL, directorPictureHoldCopy, mergeCanonicalShotQa } from './kit-video-production-ux';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');

const shotOf = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'SH-03',
  scene: 'SC01 — Buổi tối trong phòng khách',
  shot: '03',
  clock: '',
  seconds: 5,
  story: 'Minh nói chuyện với bố',
  visual: 'phòng khách',
  characters: ['Minh'],
  location: 'nhà',
  motionPrompt: 'slow tilt',
  motionPromptVi: 'nghiêng chậm',
  status: 'story_locked',
  sceneId: 'SC01',
  characterIds: ['CHAR-001'],
  dialogueSegmentIds: ['LINE-003'],
  ...over,
});

const line = (text = 'Con xin lỗi bố.') => ({
  id: 'LINE-003',
  characterId: 'CHAR-001',
  text,
  sceneId: 'SC01',
  voiceId: 'voice-minh',
});

function baseState(run?: SeriesShotRun, extraShots: FamixaSeriesShot[] = []): SeriesPilotState {
  const shot = shotOf();
  const shots = [shot, ...extraShots];
  return {
    roles: [],
    runs: Object.fromEntries(shots.map((s) => [s.id, s.id === shot.id ? run ?? { status: 'story_locked' } : { status: 'story_locked' }])),
    lines: [line()],
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-minh' }],
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'Famixa',
      episode: 'EP01',
      title: 'EP01',
      premise: '',
      moral: '',
      ctaRule: '',
      shots,
    },
    voiceAssets: { 'LINE-003': { lineId: 'LINE-003', duration: 1.2, status: 'ready' } },
  };
}

const tts = { 'LINE-003': { url: 'blob:voice', fileName: 'a.mp3' } };

function snap(run?: SeriesShotRun, extras?: { voiceStale?: boolean }) {
  const state = baseState(run);
  const built = buildShotProductionSnapshot({
    state,
    shot: state.episode!.shots[0]!,
    ttsFiles: tts,
    artifactPresent: Boolean(run?.shotProduction?.assembleFp),
  });
  return extras?.voiceStale ? { ...built, voiceStale: true, voiceReady: false } : built;
}

const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const card = read('ContentFamixaShotProduction/ShotProductionCard.tsx');
const ctaSrc = read('ContentFamixaShotProduction/ShotProductionCta.ts');
const qaSrc = read('ContentFamixaShotProduction/ShotProductionQa.tsx');
const progress = read('ContentFamixaShotProduction/ShotProductionProgress.tsx');
const preview = read('ContentFamixaShotProduction/ShotProductionPreview.tsx');
const nav = read('ContentFamixaShotProduction/ShotProductionNavigator.tsx');
const tech = read('ContentFamixaShotProduction/ShotProductionTechnical.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const shell = read('ContentFamixaSeriesWorkspace.tsx');
const overview = read('ContentFamixaProductionOverview.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const stateFile = read('ContentFamixaShotProduction/ShotProductionState.ts');

ok(ws.includes("mode === 'director'") && ws.includes('ShotProductionPreview') && ws.includes('ShotProductionNavigator'), '01 studio opens focused');
ok(ws.includes('hideIdentity: true') && ws.includes('data-director-bench'), '02 one shot selected in director studio');
ok(ws.includes("action === 'approve-picture'") && ws.includes('onEnsurePicture(shotId)'), '02b empty approve falls through to Tạo hình');
ok(ws.includes('kfPixelsOf(shotId)') && ws.includes('onApprovePicture(shotId, livePixels)'), '02c approve uses desk pixels before remake');
ok(ws.includes('directorPictureVideoSurface') && ws.includes('data-picture-pending'), '02d desk CTAs follow live picture state');
ok(ws.includes('playableMotionTakeOf') && read('content-famixa-series.ts').includes('mergeMotionAttempts'), '02e Wan take on this still becomes current video');
ok(ws.includes("currentMotionArt?.validity === 'CURRENT'") && ws.includes('currentTakeUrl'), '02e2 desk preview uses execution CURRENT motion only');
ok(stateFile.includes('const livePictureHash = picturePixelHashOf(run, opts.shot.id)'), '02e3 snapshot hashes live still before stamp');
ok(ws.includes('!currentTakeUrl') && ws.includes('snap.takeFromOtherPicture'), '02e4 desk does not fall back to old takeUrl after new still');
ok(series.includes('...bound,') && series.includes('withRunPixels(bound)'), '02e5 persist keeps live still pixels, does not restore slim+old IDB');
ok(series.includes('approveShotPicture = (shotId: string, liveStill?: string)') && series.includes('applyPictureApproveGuard'), '02f approve stamps the still shown on the desk');
ok(series.includes('await saveKfPixels(shotId, decided.run.keyframeDataUrl'), '02f2 approve writes live still to IDB before bind');
ok(series.includes('[FAMIXA_APPROVE_BOUNDARY]'), '02f3 approve logs picture/motion boundary without pixels');
const newStillPixels = 'data:image/jpeg;base64,PIXELNEUSTILL999';
const newStillAfterApprove = snap({
  status: 'keyframe_ready',
  keyframeDataUrl: newStillPixels,
  kfApproved: true,
  kfSourceHash: dataUriHash(newStillPixels),
  pictureRevisionId: 'picture:SH-03:002',
  motionNeedsRemake: true,
  takeUrl: 'https://v3b.fal.media/files/b/0aa99696/RYvS1wcKK4hUZ11j0s7Vz_VSlXMNoU.mp4',
  runwayAttempts: [
    {
      n: 44,
      at: '',
      status: 'SUCCEEDED',
      taskId: 'wan-44',
      outputUrl: 'https://v3b.fal.media/files/b/0aa99696/RYvS1wcKK4hUZ11j0s7Vz_VSlXMNoU.mp4',
      kf: { hash: 'hcfbd2a78:140859' },
    },
  ],
});
ok(newStillAfterApprove.takeFromOtherPicture, '02g2 approved new still does not treat take 44 as current');
ok(nextShotProductionCommand(newStillAfterApprove).type === 'CONFIRM_MOTION', '02g3 approved new still opens Tạo video');
ok(
  directorPictureVideoSurface({
    snap: newStillAfterApprove,
    cmd: nextShotProductionCommand(newStillAfterApprove),
    hasLiveStill: true,
    kfApproved: true,
  }).primary?.action === 'motion',
  '02g4 desk primary is Tạo video after Duyệt hình on a new still',
);
const pendingDesk = directorPictureVideoSurface({
  snap: snap({
    status: 'keyframe_ready',
    keyframeDataUrl: 'data:image/png;base64,aa',
    kfApproved: false,
    motionNeedsRemake: true,
    takeUrl: 'https://old/take.mp4',
  }),
  cmd: { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED' },
  hasLiveStill: true,
  kfApproved: false,
});
ok(pendingDesk.primary?.action === 'approve-picture' && pendingDesk.primary?.label === 'Duyệt hình', '02e live still + not approved shows Duyệt hình');
const approvedDesk = directorPictureVideoSurface({
  snap: snap({
    status: 'keyframe_ready',
    keyframeDataUrl: 'data:image/png;base64,aa',
    kfApproved: true,
    motionNeedsRemake: true,
    takeUrl: 'https://old/take.mp4',
  }),
  cmd: { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED' },
  hasLiveStill: true,
  kfApproved: true,
});
ok(approvedDesk.primary?.action === 'motion' && /đã duyệt/i.test(approvedDesk.note), '02g approved still may Tạo video');
ok(pendingDesk.phase === 'picture' && /chưa duyệt/i.test(pendingDesk.note), '02f pending stays on Hình, not Tạo video');
ok(nav.includes('onSelect') && nav.includes('Shot trước') && typeof sceneShotsOf === 'function', '03 navigator changes shot');

const empty = resolveShotPreviewKind({});
const kf = resolveShotPreviewKind({ keyframeUrl: 'data:image/png;base64,aa' });
const take = resolveShotPreviewKind({ takeUrl: 'https://take/mute.mp4', keyframeUrl: 'data:image/png;base64,aa' });
const lip = resolveShotPreviewKind({
  lipsyncUrl: 'https://fal/lip.mp4',
  takeUrl: 'https://take/mute.mp4',
  keyframeUrl: 'data:image/png;base64,aa',
});
const fin = resolveShotPreviewKind({
  finalUrl: 'blob:final',
  lipsyncUrl: 'https://fal/lip.mp4',
  takeUrl: 'https://take/mute.mp4',
  keyframeUrl: 'data:image/png;base64,aa',
});
ok(kf.kind === 'keyframe' && kf.label === 'Hình ảnh' && !kf.isFinal, '04 preview renders KF');
ok(take.kind === 'motion' && take.label === 'Bản chuyển động' && !take.isFinal, '05 preview renders take');
ok(lip.kind === 'lipsync' && lip.label === 'Preview Lip-sync' && !lip.isFinal, '06 preview renders lipsync');
ok(fin.kind === 'final' && fin.label === 'Video hoàn chỉnh' && fin.isFinal, '07 preview renders Final');
ok(fin.src === 'blob:final' && lip.src !== fin.src, '08 Final takes priority');
ok(take.label !== SHOT_PREVIEW_LABEL.final && take.label !== 'Video hoàn chỉnh', '09 take is not labeled Final');
ok(lip.label !== 'Video hoàn chỉnh' && !lip.isFinal, '10 lipsync is not labeled Final');
ok(empty.kind === 'empty' && empty.label === 'Shot chưa có hình ảnh', '04b empty preview');

const voiceSnap = snap();
const voiceCmd = nextShotProductionCommand(voiceSnap);
const cta = directorPrimaryCta(voiceSnap);
ok((card.match(/type="primary"/g) || []).length === 1 && ctaSrc.includes('DIRECTOR_SHOT_COMMAND_LABEL'), '11 one primary CTA');
ok(cta.action === staffPrimaryCta(voiceSnap).action && cta.label === DIRECTOR_SHOT_COMMAND_LABEL[voiceCmd.type], '12 CTA from nextShotProductionCommand');

const kfWait = snap({ status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,aa', kfApproved: false });
ok(nextShotProductionCommand(kfWait).type === 'WAIT_PICTURE_APPROVAL' && directorPrimaryCta(kfWait).action === 'approve-picture', '13 KF approval');
const kfLegacy = { ...kfWait, pictureUnusable: true };
ok(nextShotProductionCommand(kfLegacy).type === 'ENSURE_PICTURE' && directorPrimaryCta(kfLegacy).label === 'Tạo hình', '13b legacy pipeline asks Tạo hình');
const kfStub = snap({ status: 'keyframe_ready', keyframeFileName: 'kf.jpg', kfApproved: false });
ok(nextShotProductionCommand(kfStub).type === 'ENSURE_PICTURE' && directorPrimaryCta(kfStub).label === 'Tạo hình', '13d stub KF without pixels asks Tạo hình');
const leftoverFails = Array.from({ length: CAMERA_RETRY_POOL }, (_, i) => ({
  fingerprint: `old-source-hash|old-prompt-${i}|gen4_turbo|5`,
  status: 'FAILED' as const,
  taskId: `t-fail-${i}`,
  failureCode: 'INTERNAL',
}));
const newKfAfterFail = snap({
  status: 'keyframe_ready',
  keyframeDataUrl: 'data:image/png;base64,newstill',
  kfApproved: true,
  i2vRetry: CAMERA_RETRY_POOL,
  failedKfHash: 'old-source-hash',
  turboError: 'INTERNAL.BAD_OUTPUT',
  runwayAttempts: leftoverFails,
});
ok(!newKfAfterFail.retryLocked && !newKfAfterFail.camerasExhausted, '13e new KF does not inherit old camera exhaustion');
ok(directorPrimaryCta(newKfAfterFail).label === 'Tạo video', '13e new KF after FAIL opens Tạo video');
const sameKfUrl = 'data:image/png;base64,aa';
const sameKfRun: SeriesShotRun = {
  status: 'keyframe_ready',
  keyframeDataUrl: sameKfUrl,
  kfApproved: true,
  i2vRetry: CAMERA_RETRY_POOL,
  failedKfHash: dataUriHash(sameKfUrl),
  turboError: 'INTERNAL.BAD_OUTPUT',
  runwayAttempts: leftoverFails,
};
const sameKfState = baseState(sameKfRun);
const samePromptHash = shotI2vPromptHash(sameKfState, sameKfState.episode!.shots[0]!, sameKfRun);
const sameKfOut = snap({
  ...sameKfRun,
  failedPromptHash: samePromptHash,
  runwayAttempts: leftoverFails.map((row, i) => ({ ...row, promptHash: i === leftoverFails.length - 1 ? samePromptHash : row.fingerprint })),
});
ok(sameKfOut.retryLocked && sameKfOut.camerasExhausted, '13f same KF + same prompt + retry pool stays exhausted');
const newPromptOut = snap({
  ...sameKfRun,
  failedPromptHash: 'h-old-prompt',
  runwayAttempts: leftoverFails,
});
ok(!newPromptOut.retryLocked && !newPromptOut.camerasExhausted, '13g new acting prompt reopens Tạo video');
ok(directorPrimaryCta(newPromptOut).label === 'Tạo video', '13g primary is Tạo video after prompt change');
ok(ws.includes('INVALID_REFERENCE_PIPELINE') && card.includes('pipeline cũ') && ws.includes('directorPreviewStillUrl'), '13c director does not approve legacy still');
const vidWait = snap({
  status: 'reviewed',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: false,
});
ok(nextShotProductionCommand(vidWait).type === 'WAIT_VIDEO_REVIEW' && directorPrimaryCta(vidWait).action === 'approve-video', '14 video approval');
const qaSnap = snap({
  status: 'reviewed',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: true,
});
ok(nextShotProductionCommand(qaSnap).type === 'LIPSYNC_QA_REQUIRED' && card.includes('ShotProductionQa'), '15 QA gate');
ok(
  shotQaMissing({ action: true, continuity: true }, true).join() === 'Giọng / khuôn mặt phù hợp'
    && ws.includes('Chưa đủ kiểm tra Shot'),
  '15b Kiểm tra Shot reports missing ticks',
);
ok(
  shotQaMissing({ action: true, continuity: true, voiceFace: true }, true, { afterLipsync: true }).join() ===
    'Lip-sync đạt,Final A/V đạt',
  '15e after lipsync requires quality ticks',
);
ok(card.includes('afterLipsync') && qaSrc.includes('Môi khớp thoại chưa?'), '15f director sees quality ticks');
ok(series.includes('startSceneTurbo([shotId])') && orch.includes('CONFIRM_MOTION'), '16 Runway confirmation remains');
ok(series.includes('startShotMotion') && series.includes('forceNew: true'), '16b stale motion Studio remake');
ok(series.includes('startLipsync([shotId])') && orch.includes('CONFIRM_LIPSYNC'), '17 Fal confirmation remains');
ok(series.includes('studioLipsyncSendOpts') && series.includes('startLipsync([shotId], opts)'), '17b stale lipsync Studio remake');
const lipReady = snap({
  status: 'reviewed',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: true,
  lipsyncUrl: 'https://fal/lip.mp4',
  lipsynced: true,
  finalSource: 'FAL',
  shotQa: { action: true, continuity: true, voiceFace: true },
});
ok(nextShotProductionCommand(lipReady).type === 'ENSURE_MIX' && directorPrimaryCta(lipReady).action === 'mix', '18 mix command');
ok(directorPrimaryCta(lipReady).label === 'Hoàn thiện Shot', '18b mix label');
const falWipe = mergeCanonicalShotQa(
  { action: true, continuity: true, voiceFace: true },
  { lipsync: true },
);
ok(falWipe.voiceFace === true && falWipe.lipsync === true && falWipe.action === true, '15c Fal patch must keep voiceFace');
const afterVoiceFace = snap({
  status: 'approved',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: true,
  lipsyncUrl: 'https://fal/lip.mp4',
  lipsynced: true,
  finalSource: 'FAL',
  shotQa: falWipe,
});
ok(
  nextShotProductionCommand(afterVoiceFace).type !== 'LIPSYNC_QA_REQUIRED'
    && nextShotProductionCommand(afterVoiceFace).type === 'ENSURE_MIX'
    && directorPrimaryCta(afterVoiceFace).label === 'Hoàn thiện Shot',
  '15d voiceFace=true → Hoàn thiện Shot',
);
ok(DIRECTOR_SHOT_COMMAND_LABEL.READY_FINAL === 'Xem video' && directorPrimaryCta(lipReady).action === 'mix', '19 final command');

const multi = snap();
const blocked = nextShotProductionCommand({
  ...multi,
  intent: { ...multi.intent, productionMode: 'MULTI_SPEAKER_BLOCKED' },
});
ok(blocked.type === 'BLOCK', '20 multi-speaker remains blocked');

const stale = shotStaleNotes({
  ...voiceSnap,
  voiceStale: true,
  keyframeStale: true,
  lipSyncStale: true,
  mixStale: true,
  motionStale: false,
});
ok(stale.some((n) => n.includes('Thoại đã thay đổi')) && stale.some((n) => n.includes('Lip-sync')), '21 stale state reflected');
ok(progress.includes('Thoại') && progress.includes('Hình ảnh') && progress.includes('Hoàn chỉnh') && !progress.includes("'Voice'"), '21b progress Vietnamese');
ok(shotProgressRows(voiceSnap).every((r) => !/VOICE_READY|WAIT_MOTION|MIX_REQUIRED/.test(r.label)), '21c no internal enum');
ok(
  shotProgressRows({ ...lipReady, mixReady: true, finalReady: true, finalArtifactReady: false }).find((r) => r.id === 'final')
    ?.mark === '!',
  '21d Hoàn chỉnh bangs when local final is gone',
);

ok(!ws.includes('openScene') && !ws.includes("setProdView('shot')") && !ws.includes('ContentKitVideoDirectorProductionWorkspace'), '22 no Lane B navigation');
ok(overview.includes('onFocusShot') && series.includes('firstShotId') && series.includes('studioFocusTick'), '22b overview focuses Lane A');
ok(series.includes('directorPictureHold') && series.includes('confirmCreateShotImage') && series.includes('onEnsurePicture={(shotId) =>'), '23 picture preflight does not navigate away');
ok(series.includes('Thoại của Episode chưa được khóa') && series.includes('shotVoiceReadyForPicture') && series.includes('directorShotHasSpokenCopy') && ws.includes('holdReason'), '24 Director picture uses shot voice; staff keeps episode lock');
ok(ws.includes('Nghe trên video đã lồng tiếng') && ws.includes('Nạp thoại'), '24b session TTS miss does not invent new TTS');
ok(ws.includes('ShotProductionTechnical') && ws.includes('!directorDesk'), '25 technical fields hidden Director');
ok(tech.includes('assembleFp') && tech.includes('voiceId') && ws.includes('ShotProductionTechnical'), '26 technical fields available Staff');
ok(series.includes('ContentFamixaBuildVoiceBoard') && series.includes('ContentFamixaBuildImageBoard') && series.includes('ContentFamixaStudioView'), '27 old tabs still mount');

const newUi = [preview, nav, tech, card, ws].join('\n');
ok(
  !newUi.includes('generateContentSeriesStill') &&
    !newUi.includes('startContentSeriesTurbo') &&
    !newUi.includes('generateSceneKf') &&
    !preview.includes('startSceneTurbo') &&
    !preview.includes('startLipsync'),
  '28 no provider calls in new UI',
);

ok(read('ContentFamixaShotProduction/ShotProductionIntent.ts').includes('famixa-shot-orch') && series.includes('ContentFamixaShotProductionPanel'), '48 flag kept');
ok(shell.includes("tab !== 'overview'") && shell.includes('currentTask'), '28b episode CTA does not override overview Shot');
ok(orch.includes('export async function produceShot') && stateFile.includes('buildShotProductionSnapshot'), 'T no new engine');
ok(!card.includes('MUTE_TAKE') && !card.includes('Fal $') && !card.includes('PVS'), 'P director card clean');
ok(directorPictureHoldCopy({ code: 'VOICE_NOT_LOCKED', type: 'voice' }).reason.includes('chưa được khóa'), '26b voice hold copy');
ok(directorPictureHoldCopy({ code: 'SCENE_MASTER_NOT_LOCKED', type: 'scene_master' }).hint.includes('Scene Master'), '26c scene master hold');

const sceneA = [
  shotOf({ id: 'A', sceneId: 'SC01' }),
  shotOf({ id: 'B', sceneId: 'SC01', shot: '04' }),
  shotOf({ id: 'C', scene: 'SC02 — Bếp', sceneId: 'SC02' }),
];
const sceneShots = sceneShotsOf(sceneA, 'A');
ok(sceneShots.length === 2 && sceneShots.every((s) => (s.sceneId || s.scene).startsWith('SC01')), '03b navigator stays in scene');
ok(resolveLaneAShotId(sceneA, 'SC01') === 'A' && resolveLaneAShotId(sceneA, 'B') === 'B', '03c Mở Shot resolves scene to first shot');

const drawing = shotPipelineBusy({ shotId: 'SH04', busyShotId: 'SH04' });
const sending = shotPipelineBusy({ shotId: 'SH04', busyShotId: 'SH04', motionBusyId: 'SH04' });
ok(drawing.pictureBusy && !drawing.motionBusy, '27 picture busy is not motion');
ok(sending.motionBusy && !sending.pictureBusy, '27b motion busy is not picture');
ok(ws.includes('data-picture-busy') && ws.includes('Đợi xong rồi mới Tạo video'), '27c desk blocks video while drawing');
ok(series.includes('Đang vẽ/chấm hình. Đợi xong rồi mới Tạo video.') && series.includes('Đang tạo video. Không vẽ hình mới.'), '27d SeriesTab mutex');

if (fail.length) {
  console.error(`FAMIXA_VIDEO_STUDIO_UX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VIDEO_STUDIO_UX_V1 PASS FAIL=0 (no provider)');
