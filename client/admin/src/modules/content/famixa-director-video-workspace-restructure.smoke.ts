import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { DIRECTOR_COPY, directorRecoveryOf } from './ContentFamixaShotProduction/ShotProductionDirector';
import { directorPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { directorPreviewStillUrl, directorShowsStillPreview, resolveShotPreviewKind } from './ContentFamixaShotProduction/ShotProductionPreview';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { takeFromOtherPicture } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import { DIRECTOR_SHOT_COMMAND_LABEL, SERIES_STAFF_TABS } from './kit-video-production-ux';
import {
  DIRECTOR_HIDDEN_PRIMARY_TABS,
  DIRECTOR_VIDEO_WORKSPACE_ID,
  SERIES_DIRECTOR_TABS,
  directorShotAttention,
  directorShotLaneMarks,
  directorShotOpenLabel,
  directorTabOf,
  episodeNavTabs,
  nextDirectorStoryAction,
} from './kit-video-director-nav';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');

const shell = read('ContentFamixaSeriesWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const shortDesk = read('ContentFamixaDirectorShortDesk.tsx');
const storyDesk = read('ContentFamixaDirectorStoryDesk.tsx');
const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const card = read('ContentFamixaShotProduction/ShotProductionCard.tsx');
const ctaSrc = read('ContentFamixaShotProduction/ShotProductionCta.ts');
const preview = read('ContentFamixaShotProduction/ShotProductionPreview.tsx');
const kfStore = read('content-famixa-kf-store.ts');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const stateSrc = read('ContentFamixaShotProduction/ShotProductionState.ts');

ok(DIRECTOR_VIDEO_WORKSPACE_ID === 'FAMIXA_DIRECTOR_VIDEO_WORKSPACE_RESTRUCTURE_V1', 'id');

ok(
  SERIES_DIRECTOR_TABS.map((t) => t.label).join('/') === 'Chuyện/Người/Short',
  'T1 director nav labels',
);
ok(episodeNavTabs('director').every((t) => ['script', 'characters', 'overview'].includes(t.id)), 'T1 director tab ids');
ok(shell.includes('data-director-nav') && shell.includes('Đạo diễn') && shell.includes('episodeNavTabs'), 'T1 shell uses director nav');
ok(
  DIRECTOR_HIDDEN_PRIMARY_TABS.join('|') === 'scenes|voice|images|video|finish|publish',
  'T1 no parallel production desks',
);

const shotOf = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'SH-02',
  scene: 'SC01 — Nhà',
  shot: '02',
  clock: '',
  seconds: 5,
  story: 'Minh nhìn mẹ',
  visual: 'phòng khách',
  characters: ['Minh'],
  location: 'nhà',
  motionPrompt: 'slow push',
  motionPromptVi: 'đẩy chậm',
  status: 'story_locked',
  sceneId: 'SC01',
  characterIds: ['CHAR-001'],
  dialogueSegmentIds: ['LINE-002'],
  ...over,
});

function baseState(run?: SeriesShotRun): SeriesPilotState {
  const shot = shotOf();
  return {
    roles: [],
    runs: { [shot.id]: run ?? { status: 'story_locked' } },
    lines: [{ id: 'LINE-002', characterId: 'CHAR-001', text: 'Con về rồi.', sceneId: 'SC01', voiceId: 'voice-minh' }],
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-minh' }],
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'Famixa',
      episode: 'EP100',
      title: 'Phía Sau Điểm Số',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
    voiceAssets: { 'LINE-002': { lineId: 'LINE-002', duration: 1.1, status: 'ready' } },
  };
}

const tts = { 'LINE-002': { url: 'blob:voice', fileName: 'a.mp3' } };

function snap(run?: SeriesShotRun) {
  const state = baseState(run);
  return buildShotProductionSnapshot({
    state,
    shot: state.episode!.shots[0]!,
    ttsFiles: tts,
    artifactPresent: Boolean(run?.shotProduction?.assembleFp),
  });
}

const emptySnap = snap();
const emptyCmd = nextShotProductionCommand(emptySnap);
const emptyRow = {
  ...directorShotLaneMarks(emptySnap),
  ...directorShotAttention(emptyCmd, emptySnap),
  openLabel: directorShotOpenLabel(1),
};
ok(shortDesk.includes('data-director-short-list') && shortDesk.includes('Hình') && shortDesk.includes('Video'), 'T2 short list markup');
ok(emptyRow.picture === '○' && emptyRow.label === 'Chưa tạo hình' && emptyRow.openLabel === 'Mở Shot 02', 'T2 per-shot state from snapshot');
ok(series.includes('ContentFamixaDirectorShortDesk') && series.includes('directorShotAttention'), 'T2 series wires Short');

ok(ws.includes('data-director-bench') && ws.includes('BÀN SHOT'), 'T3 one canonical Bàn Shot');
ok(ws.includes('episodeLabel') && shortDesk.includes('title ?') && series.includes('Chưa đặt tên tập'), 'T3e episode title on Short and Shot');
ok(series.includes('sameRequestBlocked') && series.includes('cameraShift'), 'T3b SAME REQUEST shifts camera until fingerprint opens');
ok(series.includes('directorI2v') && series.includes('shotVoiceReadyForPicture'), 'T3c Director one-shot I2V does not require episode voiceLocked');
ok(series.includes('directorMuteKf') && series.includes('directorI2v'), 'T3d mute I2V does not require session TTS blob');
ok(series.includes('benchOpen={directorBench}') && series.includes('onOpenShot'), 'T3 click shot opens bench');
ok(!series.includes("setProdView('shot')") || series.includes("prodMode === 'staff' && prodView === 'shot'"), 'T3 scene workspace not director path');

const motionSnap = snap({
  status: 'keyframe_ready',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
});
ok(nextShotProductionCommand(motionSnap).type === 'CONFIRM_MOTION', 'T4 command is CONFIRM_MOTION');
ok(directorPrimaryCta(motionSnap).label === 'Tạo video' && directorPrimaryCta(motionSnap).action === 'motion', 'T4 one primary CTA');
ok((card.match(/type="primary"/g) || []).length === 1, 'T4 card has one primary button');

const take = resolveShotPreviewKind({
  takeUrl: 'https://take/mute.mp4',
  keyframeUrl: 'data:image/png;base64,aa',
});
ok(take.kind === 'motion' && take.src === 'https://take/mute.mp4', 'T5 preview becomes take');
const pendingStill = resolveShotPreviewKind({
  takeUrl: 'https://take/mute.mp4',
  keyframeUrl: 'data:image/png;base64,aa',
  preferKeyframe: true,
});
ok(pendingStill.kind === 'keyframe' && pendingStill.src === 'data:image/png;base64,aa', 'T5b pending picture shows still not old take');
const pendingOverFinal = resolveShotPreviewKind({
  finalUrl: 'https://take/mix.mp4',
  takeUrl: 'https://take/mute.mp4',
  keyframeUrl: 'data:image/png;base64,aa',
  preferKeyframe: true,
});
ok(pendingOverFinal.kind === 'keyframe', 'T5b2 pending picture beats mix/take');
ok(series.includes('remakeStillCorrection') && series.includes('failedKfUrl: remake'), 'T5e remake does not attach rejected still');
ok(series.includes('onlyIds?.length ? []') && series.includes('remake ? undefined'), 'T5e remake skips REUSE and prev KF');
ok(ws.includes('desk.phase') && ws.includes('preferKeyframe: showStill') && ws.includes('directorShowsStillPreview'), 'T5c Hình phase shows still not take');
ok(!directorShowsStillPreview({ phase: 'finish', hasUsableVideo: true }), 'T5c2 finished short keeps video over leftover still');
ok(!directorShowsStillPreview({ phase: 'picture', hasUsableVideo: true }), 'T5c3 leftover Hình does not hide a playable short');
ok(directorShowsStillPreview({ phase: 'finish', picturePendingApproval: true, hasUsableVideo: true }), 'T5c4 pending picture still beats old take');
ok(directorShowsStillPreview({ phase: 'motion', takeFromOtherPicture: true, hasUsableVideo: true }), 'T5c4b approved new picture keeps still over old take');
ok(!directorPreviewStillUrl({ pictureUnusable: true, rawKf: 'data:image/png;base64,school' }), 'T5c5 old-pipeline still stays off hero');
ok(directorPreviewStillUrl({ picturePendingApproval: true, rawKf: 'data:image/png;base64,new' }) === 'data:image/png;base64,new', 'T5c6 pending new still stays visible');
ok(directorPreviewStillUrl({ keyframeApproved: true, rawKf: 'data:image/png;base64,sh04', allowedStill: '' }) === 'data:image/png;base64,sh04', 'T5c8 approve keeps this-shot still');
ok(ws.includes('keyframeApproved: snap.keyframeApproved'), 'T5c8 workspace passes approved still');
ok(ws.includes('directorPreviewStillUrl') && kfStore.includes('await read(kfKey(clipId))') && !kfStore.includes('kfLegacy'), 'T5c7 scoped KF does not reuse other-build still');
ok(ws.includes('data-director-picture-cta') && ws.includes('Tạo hình mới'), 'T5d Tạo hình mới sits under preview');
ok(ctaSrc.includes("cmd.type === 'CONFIRM_MOTION'") && ws.includes('Tạo video'), 'T5f motion phase keeps Tạo video under preview');
ok(series.includes('remakeStill') && series.includes('shotVoiceReadyForPicture') && series.includes('directorShotHasSpokenCopy'), 'T5f remake still is not blocked by missing session TTS');
ok(series.includes('detachAvAfterPictureChange') && series.includes('kfSourceHash') && ws.includes('takeFromOtherPicture'), 'T6e new still detaches old Fal/take');
ok(series.includes('loadKfPixels(shot.id)') && series.includes('startShotMotion'), 'T6e2 Tạo video hydrates new KF before send');
ok(series.includes('Video cũ không khớp ảnh mới') && series.includes('bindPictureHashOnRuns'), 'T6e3 Xem video does not open old final after remake');
ok(
  takeFromOtherPicture({
    kfSourceHash: 'still-new',
    hasTake: true,
    bindUnstampedTake: true,
  }),
  'T6e4 unbound old take is stale vs new still hash',
);
ok(
  !takeFromOtherPicture({
    keyframeDataUrl: 'data:image/png;base64,aa',
    hasTake: true,
  }),
  'T6e5 dataUrl-only + unstamped take stays compatible',
);
ok(
  !takeFromOtherPicture({
    kfSourceHash: 'still-new',
    takeKfHash: 'still-new',
    motionNeedsRemake: true,
    hasTake: true,
  }),
  'T6e6 remake flag does not hide take frozen on this still',
);
ok(series.includes('explicitOne && ready.length === 0'), 'T6f one-shot remake is not skipped as already I2V');
ok(series.includes('!replaceTake') && series.includes("retry.kind === 'recover'"), 'T6j picture remake does not recover old take');
ok(series.includes('resolveTakeUrl(shotRunOf(stateRef.current, shot)) && !remakeTake'), 'T6g Confirm does not skip existing take on remake');
ok(ctaSrc.includes("validity === 'CURRENT'") && ctaSrc.includes('remake: true'), 'T6f remake opts follow execution, not takeFromOtherPicture');
ok(series.includes('opts?.remake && (onlyIds?.length ?? 0) !== 1'), 'T6h one-shot Tạo video skips READY-take remake gate');
ok(series.includes('!remakeTake') && series.includes('circuit — bỏ qua'), 'T6i Confirm remake is not skipped by circuit');

const failKeep = {
  ...motionSnap,
  motionReady: false,
  motionFailed: true,
  videoApproved: false,
  visibleTake: { url: 'https://take/old.mp4', n: 2, status: 'SUCCEEDED' as const },
  currentAttempt: { url: undefined, n: 3, status: 'FAILED' as const },
  acceptedTake: undefined,
};
const failCmd = nextShotProductionCommand(failKeep);
ok(failCmd.type === 'ACCEPT_EXISTING' && directorPrimaryCta(failKeep).action === 'accept-existing', 'T6 ACCEPT_EXISTING when previous take exists');
ok(DIRECTOR_SHOT_COMMAND_LABEL.ACCEPT_EXISTING === 'Dùng video này', 'T6 Dùng video này');
ok(DIRECTOR_SHOT_COMMAND_LABEL.EDIT_INPUT === 'Đổi diễn', 'T6 Đổi diễn');
ok(
  DIRECTOR_COPY.SUCCESS_THEN_FAIL.includes('Video trước đó vẫn sẵn sàng') &&
    directorRecoveryOf(failKeep).actions.some((a) => a.label === 'Dùng video này') &&
    directorRecoveryOf(failKeep).actions.some((a) => a.label === 'Đổi diễn' || a.label === 'Tạo lại') &&
    directorRecoveryOf(failKeep).actions.some((a) => a.label === 'Tạo hình mới'),
  'T6 recovery actions',
);

const takeKeep = {
  ...motionSnap,
  motionReady: true,
  motionUsable: true,
  videoApproved: false,
  visibleTake: { url: 'https://take/mute.mp4', n: 1, status: 'SUCCEEDED' as const },
  acceptedTake: undefined,
};
ok(directorRecoveryOf(takeKeep).actions.some((a) => a.label === 'Tạo hình mới'), 'T6c take present still offers Tạo hình mới');
ok(
  directorRecoveryOf({
    ...takeKeep,
    isSilent: false,
    voiceReady: false,
    voiceDurationSec: 3.3,
  }).actions.some((a) => a.label === 'Tạo hình mới'),
  'T6c2 Nạp thoại still offers Tạo hình mới',
);
ok(ws.includes('data-director-remake-picture') && ws.includes('Tạo hình mới'), 'T6c3 remake picture stays under preview when take exists');

const newPicKeep = {
  ...failKeep,
  keyframeApproved: true,
  takeFromOtherPicture: true,
  motionStale: true,
};
ok(nextShotProductionCommand(newPicKeep).type === 'CONFIRM_MOTION', 'T6e approved new still + old take is Tạo video');
ok(directorPrimaryCta(newPicKeep).label === 'Tạo video' && directorPrimaryCta(newPicKeep).action === 'motion', 'T6e primary Tạo video');
ok(directorRecoveryOf(newPicKeep).message.includes('Hình mới đã duyệt'), 'T6e picture-changed copy');
const newPicLocked = {
  ...newPicKeep,
  retryLocked: true,
  motionFailed: true,
  currentAttempt: { url: undefined, n: 4, status: 'FAILED' as const },
};
ok(directorRecoveryOf(newPicLocked).status === 'RETRY_LOCKED', 'T6k locked after new-picture fail is camera retry');
ok(directorPrimaryCta(newPicLocked).action === 'motion' && directorPrimaryCta(newPicLocked).label === 'Tạo video', 'T6k locked primary is Tạo video');
ok(ws.includes('desk.primary.action') && ws.includes('Tạo video'), 'T6k preview Tạo video always sends motion');
ok(ws.includes('data-motion-busy') && ctaSrc.includes('Video hiện tại không còn khớp hình') && ws.includes('data-picture-pending'), 'T6k3 preview explains missing / hidden take');
ok(series.includes('startShotMotion') && series.includes('forceNew: true') && series.includes('CONFIRM & GENERATE') && series.includes('Job mới FAIL'), 'T6k2 Tạo video Confirms a new job, does not resume/recover');
ok(stateSrc.includes('promptUsedOnCurrentKf') && series.includes('promptUsedOnCurrentKf'), 'T6l used prompt hashes skip SAME REQUEST on current KF only');
ok(series.includes('Hết lệch camera trên ảnh này') && series.includes('startShotMotion'), 'T6l exhausted cameras ask for new still');
const camerasOut = { ...newPicLocked, camerasExhausted: true };
ok(directorRecoveryOf(camerasOut).status === 'PICTURE_REQUIRED', 'T6m exhausted cameras require new still');
ok(directorPrimaryCta(camerasOut).action === 'picture', 'T6m primary is Tạo hình mới');
ok(!directorRecoveryOf(camerasOut).actions.some((a) => a.label === 'Gửi lại với camera khác'), 'T6m camera retry hidden when exhausted');
ok(stateSrc.includes('failOnThisKf') && stateSrc.includes('failedJobsOnThisKf'), 'T6n exhausted cameras count submitted FAIL jobs on this KF');

const lockedNoTake = {
  ...motionSnap,
  motionReady: false,
  motionFailed: true,
  retryLocked: true,
  videoApproved: false,
  visibleTake: undefined,
  acceptedTake: undefined,
  currentAttempt: { url: undefined, n: 1, status: 'FAILED' as const },
};
ok(directorRecoveryOf(lockedNoTake).actions.some((a) => a.label === 'Tạo video'), 'T6b locked no-take offers Tạo video');
ok(directorPrimaryCta(lockedNoTake).label === 'Tạo video' && directorPrimaryCta(lockedNoTake).action === 'motion', 'T6b primary is Tạo video');
ok(directorRecoveryOf(lockedNoTake).actions.some((a) => a.label === 'Tạo hình mới'), 'T6b Tạo hình mới stays secondary');
ok(directorRecoveryOf(lockedNoTake).actions.some((a) => a.label === 'Đổi diễn'), 'T6b Đổi diễn stays secondary');

const qaReady = snap({
  status: 'reviewed',
  keyframeDataUrl: 'data:image/png;base64,aa',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: true,
  shotQa: { action: true, continuity: true, voiceFace: true },
});
ok(nextShotProductionCommand(qaReady).type === 'CONFIRM_LIPSYNC', 'T7 lipsync command');
ok(directorPrimaryCta(qaReady).label === 'Lồng tiếng', 'T7 Lồng tiếng');

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
ok(nextShotProductionCommand(lipReady).type === 'ENSURE_MIX', 'T8 mix is ENSURE_MIX');
const remadePic = snap({
  status: 'reviewed',
  keyframeDataUrl: 'data:image/png;base64,newpic',
  kfApproved: true,
  previewUrl: 'https://take/old.mp4',
  takeUrl: 'https://take/old.mp4',
  lipsyncUrl: 'https://fal/old-lip.mp4',
  lipsynced: true,
  videoApproved: true,
  motionNeedsRemake: true,
  shotProduction: { assembleFp: 'old-mix' },
  shotQa: { action: true, continuity: true, voiceFace: true, lipsyncQuality: true, finalAv: true },
});
ok(nextShotProductionCommand(remadePic).type === 'CONFIRM_MOTION', 'T8b new picture beats old lipsync / Xem video');
ok(directorPrimaryCta(remadePic).action === 'motion' && directorPrimaryCta(remadePic).label === 'Tạo video', 'T8b CTA Tạo video');
ok(directorRecoveryOf(remadePic).message.includes('Hình mới đã duyệt'), 'T8b picture-changed copy');
ok(directorPrimaryCta(lipReady).label === 'Hoàn thiện Shot', 'T8 Hoàn thiện is not a desk');
ok(ws.includes("cmd.type !== 'ENSURE_MIX'") && ws.includes('onEnsureMix'), 'T8 mix is internal on Bàn Shot');
ok(!SERIES_DIRECTOR_TABS.some((t) => t.label === 'Hoàn thiện'), 'T8 no Hoàn thiện tab');

const mixReadyNoQuality = {
  ...lipReady,
  mixReady: true,
  qualityPassed: false,
  finalReady: false,
};
ok(nextShotProductionCommand(mixReadyNoQuality).type === 'LIPSYNC_QA_REQUIRED', 'T8b mix ready still needs quality ticks');
ok(directorRecoveryOf(mixReadyNoQuality).status === 'QUALITY_QA', 'T8b director quality QA after lipsync');

const finalSnap = {
  ...lipReady,
  mixReady: true,
  qualityPassed: true,
  finalReady: true,
  finalArtifactReady: true,
};
ok(nextShotProductionCommand(finalSnap).type === 'READY_FINAL', 'T9 final ready command');
ok(DIRECTOR_SHOT_COMMAND_LABEL.READY_FINAL === 'Xem video', 'T9 Xem video');

ok(
  shortDesk.includes('Xem preview') &&
    shortDesk.includes('Xuất preview') &&
    shortDesk.includes('Xuất cắt thoại') &&
    shortDesk.includes('data-director-editorial'),
  'T10 editorial preview before allFinal',
);
ok(shortDesk.includes('disabled={!canExport') && shortDesk.includes('onExportFinal'), 'T10 Xuất gated on editorial source');
ok(shortDesk.includes('Có video để dựng') && !shortDesk.includes('Preview = shot đã Hoàn thiện'), 'T10 editorial copy ≠ Hoàn thiện');
ok(
  series.includes('onPreviewWatch') &&
    series.includes("exportEditorialCut('watch')") &&
    series.includes("exportEditorialCut('preview')") &&
    series.includes("exportEditorialCut('final')") &&
    series.includes("exportEditorialCut('final', 'SPEECH_CUT')"),
  'T10 preview vs Xuất / Xuất cắt thoại',
);
ok(series.includes('usableStart') && series.includes('resolveEditorialWindow'), 'T10 trim uses UsableStart');
ok(series.includes('compileMixCueSheet') && series.includes('editorialCutOf'), 'T10 mix from editorial overlay');
ok(series.includes('editorialFileStem') && series.includes('editorialSourceOf') && series.includes('editorialSourceReady'), 'T10 EP-edit + canonical source');

ok(
  episodeNavTabs('director').every((t) => !['images', 'video', 'voice', 'scenes', 'finish', 'publish'].includes(t.id)),
  'T11 director never primary-navigates Hình/Thoại/Video',
);
ok(directorTabOf('images') === 'overview' && directorTabOf('voice') === 'characters' && directorTabOf('scenes') === 'script', 'T11 remap hidden desks');
ok(!episodeNavTabs('director').some((t) => t.label === 'Hình ảnh' || t.label === 'Video' || t.label === 'Thoại'), 'T11 no Hình ảnh tab');

ok(
  SERIES_STAFF_TABS.map((t) => t.label).join('|') ===
    'Tổng quan|Kịch bản|Chia cảnh|Nhân vật|Thoại|Hình ảnh|Video|Hoàn thiện|Xuất bản',
  'T12 staff tabs remain',
);
ok(
  series.includes('ContentFamixaBuildImageBoard') &&
    series.includes('ContentFamixaBuildVideoBoard') &&
    series.includes('ContentFamixaBuildVoiceBoard') &&
    series.includes('ContentFamixaBuildFinishBoard') &&
    series.includes("prodMode === 'staff'"),
  'T12 staff boards still mounted',
);
ok(episodeNavTabs('staff').length === 9, 'T12 staff nav length');

ok(nextDirectorStoryAction({
  body: 'Minh về nhà.',
  sceneCount: 1,
  shotCount: 3,
  storyReviewed: true,
  needsInheritReview: false,
  scriptLocked: false,
  shotGraphLocked: true,
  aspectOk: true,
}).cta === 'Khóa chuyện', 'story lock consolidates');
ok(nextDirectorStoryAction({
  body: 'Minh về nhà.',
  sceneCount: 1,
  shotCount: 3,
  storyReviewed: true,
  needsInheritReview: false,
  scriptLocked: true,
  shotGraphLocked: false,
  aspectOk: true,
}).missing === 'Duyệt cách chia shot.', 'story lock one missing action');

ok(storyDesk.includes('Khóa chuyện') || storyDesk.includes('storyAction.cta'), 'story desk one CTA');
ok(orch.includes('export function nextShotProductionCommand'), 'no new orchestrator');
ok(!ws.includes('generateSceneKf') && !preview.includes('startSceneTurbo'), 'no provider calls in shot UI');
ok(DIRECTOR_SHOT_COMMAND_LABEL.CONFIRM_MOTION === 'Tạo video', 'CTA map motion');
ok(DIRECTOR_SHOT_COMMAND_LABEL.CONFIRM_LIPSYNC === 'Lồng tiếng', 'CTA map lipsync');
ok(DIRECTOR_SHOT_COMMAND_LABEL.ENSURE_MIX === 'Hoàn thiện Shot', 'CTA map mix');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_VIDEO_WORKSPACE_RESTRUCTURE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_VIDEO_WORKSPACE_RESTRUCTURE_V1 PASS FAIL=0 (no provider)');
