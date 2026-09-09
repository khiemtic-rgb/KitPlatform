/** FAMIXA_AI_PROVIDER_ORCHESTRATION_CORE_V1 — T1–T25. 0 HTTP. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import { directorPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';
import {
  freezeMotionInput,
  motionContentFingerprintOf,
  motionLegacyExecutionFingerprintOf,
  motionProviderStampOf,
  sameMotionContentFingerprint,
  validateMotionCallback,
  adaptMotionAttempts,
  currentPictureInputOf,
  type ExecutionAwareAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  FAMIXA_PROVIDER_CATALOG,
  FAMIXA_PROVIDER_ERROR,
  FAMIXA_PROVIDER_ORCHESTRATION_ID,
  FakeLipSyncProvider,
  FakeMotionProvider,
  FakePictureProvider,
  FakeVoiceProvider,
  describeFamixaProvider,
  estimateFamixaProviderCost,
  famixaProviderHasCapability,
  famixaProviderRegistryForTests,
  lipSyncInputIsContractOnly,
  mapFamixaProviderPolicy,
  motionInputIsContractOnly,
  pictureInputIsContractOnly,
  resolveFamixaProvider,
  voiceInputIsContractOnly,
} from './famixa-ai-provider-orchestration';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');

ok(FAMIXA_PROVIDER_ORCHESTRATION_ID === 'FAMIXA_AI_PROVIDER_ORCHESTRATION_CORE_V1', 'suite id');

const registry = famixaProviderRegistryForTests();

ok(registry.describe('gemini').providerId === 'gemini', 'T1 known provider');
ok(FAMIXA_PROVIDER_CATALOG.length === 5, 'T1 catalog live set');

let unknown = false;
try {
  describeFamixaProvider('kling');
} catch (e) {
  unknown = String((e as Error).message).includes('PROVIDER_UNAVAILABLE');
}
ok(unknown, 'T2 unknown provider');

ok(famixaProviderHasCapability('gemini', 'PICTURE'), 'T3 gemini picture');
ok(!famixaProviderHasCapability('gemini', 'MOTION'), 'T3 capability mismatch');
let mismatch = false;
try {
  resolveFamixaProvider('MOTION', { explicitProviderId: 'gemini' });
} catch (e) {
  mismatch = String((e as Error).message).includes('UNSUPPORTED');
}
ok(mismatch, 'T3 explicit capability mismatch');

ok(resolveFamixaProvider('PICTURE').providerId === 'gemini', 'T4 STANDARD picture');
ok(resolveFamixaProvider('MOTION').providerId === 'runway', 'T4 STANDARD motion');
ok(resolveFamixaProvider('VOICE').providerId === 'elevenlabs', 'T4 STANDARD voice');
ok(resolveFamixaProvider('LIPSYNC').modelId === '1.9', 'T4 STANDARD lipsync');

ok(mapFamixaProviderPolicy('MOTION', 'ECONOMY').providerId === 'wan', 'T5 ECONOMY motion');
ok(mapFamixaProviderPolicy('LIPSYNC', 'ECONOMY').modelId === 'ls', 'T5 ECONOMY lipsync');
ok(resolveFamixaProvider('MOTION', { engine: 'wan' }).providerId === 'wan', 'T5 engine wan');
ok(resolveFamixaProvider('MOTION', { engine: 'turbo' }).providerId === 'runway', 'T5 engine turbo');

let unavailable = false;
try {
  resolveFamixaProvider('VOICE', { explicitProviderId: 'f5' });
} catch (e) {
  unavailable = (e as { code?: string }).code === FAMIXA_PROVIDER_ERROR.PROVIDER_UNAVAILABLE
    || String((e as Error).message).includes('PROVIDER_UNAVAILABLE')
    || String((e as Error).message).includes('UNSUPPORTED');
}
ok(unavailable, 'T6 unavailable provider');

const picReq = { prompt: 'still', references: [{ mime: 'image/jpeg', base64: 'abc', label: 'Canon' }] };
const motReq = { imageDataUrl: 'data:image/jpeg;base64,x', prompt: 'move', seconds: 5 as const, ratio: '1280:720' };
const voiReq = { voiceId: 'kOIUKJ9E0DPndqTtvJm5', text: 'Về rồi hả con?' };
const lipReq = { videoUrl: 'https://take.mp4', audioUrl: 'https://voice.mp3', modelKind: '1.9', syncMode: 'silence' };
ok(pictureInputIsContractOnly(picReq), 'T7 picture contract');
ok(motionInputIsContractOnly(motReq), 'T8 motion contract');
ok(voiceInputIsContractOnly(voiReq), 'T9 voice contract');
ok(lipSyncInputIsContractOnly(lipReq), 'T10 lipsync contract');

await new FakePictureProvider().generate(picReq);
await new FakeMotionProvider('runway').start(motReq);
await new FakeVoiceProvider().synthesize(voiReq);
await new FakeLipSyncProvider().start(lipReq);

const KF = 'data:image/jpeg;base64,ORCHPIXAAA';
const HASH = dataUriHash(KF);
const shot: FamixaSeriesShot = {
  id: 'EP01-SC01-SH01',
  shot: 'SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  story: 'Minh bước vào nhà',
  seconds: 5,
  characters: ['CHAR-003'],
  characterIds: ['CHAR-003'],
  dialogueSegmentIds: ['line-1'],
  actingBeat: { before: { action: 'stand', holdSec: 0.2 }, during: { speech: true, emotion: 'neutral' }, after: { holdSec: 0.22 } },
};
const run = (extra?: Partial<SeriesShotRun>): SeriesShotRun => ({
  kfApproved: true,
  kfSourceHash: HASH,
  keyframeDataUrl: KF,
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  ...extra,
});
const stateOf = (r: SeriesShotRun): SeriesPilotState => ({
  schemaVersion: 4,
  roles: [{ id: 'role-CHAR-003', title: 'Mẹ', name: 'Linh', characterId: 'CHAR-003', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  episode: { episode: 'EP01', title: 'Tập 01', shots: [shot] },
  lines: [{ id: 'line-1', text: 'Về rồi hả con?', characterId: 'CHAR-003', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  characters: [{ id: 'CHAR-003', name: 'Linh', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.5, status: 'ready', characterId: 'CHAR-003' } },
  runs: { [shot.id]: r },
});

const frozenRunway = freezeMotionInput(stateOf(run()), shot, { provider: 'runway' });
const frozenWan = freezeMotionInput(stateOf(run()), shot, { provider: 'wan' });
ok(frozenRunway.executionFingerprint === frozenWan.executionFingerprint, 'T11/T12 same WHAT');
ok(frozenRunway.providerStamp !== frozenWan.providerStamp, 'T11/T12 HOW stamp differs');
ok(
  sameMotionContentFingerprint(frozenRunway.executionFingerprint, frozenWan.executionFingerprint),
  'T11/T12 sameMotionContentFingerprint',
);
const what = {
  baseMotionFingerprint: frozenRunway.baseMotionFingerprint,
  actingBeatFingerprint: frozenRunway.actingBeatFingerprint,
  promptHash: frozenRunway.promptHash,
  pixelHash: frozenRunway.keyframePixelHash,
  providerDuration: frozenRunway.providerDuration,
  timingHash: frozenRunway.timingHash,
};
ok(motionContentFingerprintOf(what) === frozenRunway.executionFingerprint, 'T11 content hash is executionFingerprint');
ok(
  motionLegacyExecutionFingerprintOf({ ...what, provider: 'runway' }) !== frozenRunway.executionFingerprint,
  'T11 legacy HOW hash is not WHAT',
);
ok(motionProviderStampOf('runway', 5) !== motionProviderStampOf('wan', 5), 'T12 provider stamps');

const runningRow = (frozen: typeof frozenRunway): ExecutionAwareAttempt => ({
  n: 1,
  at: '2026-09-09T00:00:00.000Z',
  status: 'RUNNING',
  taskId: 'task-run-1',
  attemptId: 'mot:EP01-SC01-SH01:1',
  frozenInput: frozen,
  inputFingerprint: frozen.executionFingerprint,
});
const liveState = stateOf(run({ runwayAttempts: [runningRow(frozenWan)] }));
const decision13 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, liveState.runs![shot.id]!.runwayAttempts),
  current: { ...frozenRunway, picture: currentPictureInputOf(liveState, shot) },
  providerTaskId: 'task-run-1',
});
ok(decision13.promote && decision13.resultClass === 'CURRENT', 'T13 correct attempt accepted');

const lateDecision = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, [runningRow(frozenRunway)]),
  current: {
    ...freezeMotionInput(
      stateOf(run({ keyframeDataUrl: 'data:image/jpeg;base64,ORCHPIXBBB', kfSourceHash: dataUriHash('data:image/jpeg;base64,ORCHPIXBBB'), pictureRevisionId: 'picture:EP01-SC01-SH01:007' })),
      shot,
    ),
    picture: currentPictureInputOf(
      stateOf(run({ keyframeDataUrl: 'data:image/jpeg;base64,ORCHPIXBBB', kfSourceHash: dataUriHash('data:image/jpeg;base64,ORCHPIXBBB'), pictureRevisionId: 'picture:EP01-SC01-SH01:007' })),
      shot,
    ),
  },
  providerTaskId: 'task-run-1',
});
ok(!lateDecision.promote && lateDecision.resultClass === 'LATE_RESULT', 'T14 late attempt LATE_RESULT');

ok(validateMotionCallback({
  attempts: [],
  current: { ...frozenRunway, picture: currentPictureInputOf(stateOf(run()), shot) },
  providerTaskId: 'missing',
}).resultClass === 'LATE_RESULT', 'T15 mismatched frozen / unknown rejected');

ok(estimateFamixaProviderCost('runway', 'gen4_turbo', 5).amount === 25, 'T16 known model estimate');
ok(estimateFamixaProviderCost('fal', '1.9', 10).amount === 0.12, 'T16 fal 1.9');
ok(estimateFamixaProviderCost('gemini').kind === 'UNKNOWN', 'T17 unknown price');
ok(estimateFamixaProviderCost('wan').kind === 'FAL_BILLED_ESTIMATE', 'T17 wan not fake 0 USD');

const execSrc = read('ContentFamixaShotProduction/ShotProductionExecution.ts');
ok(execSrc.includes('motionContentFingerprintOf') && execSrc.includes('sameMotionContentFingerprint'), 'T18 execution fingerprint split');
ok(execSrc.includes("provider: 'runway' | 'wan'"), 'T18 provider metadata kept');

const artSrc = read('ContentFamixaShotProduction/ShotProductionArtifacts.ts');
ok(artSrc.includes('samePictureSuccessTakeOf') && artSrc.includes('playableMotionTakeOf'), 'T19 Artifact V2');

const timingSrc = read('famixa-shot-production-timing.ts');
ok(timingSrc.includes('performanceDurationOf') && timingSrc.includes('providerDurationOf'), 'T20 Timing V3');

const pixSrc = read('content-famixa-picture-pixel-invariant.ts');
ok(pixSrc.includes('PICTURE_DUPLICATE_COPY') && pixSrc.includes('applyPictureGenerationGuard'), 'T21 Picture invariant');

const accept = acceptExistingTake({
  takeUrl: 'https://take/m1.mp4',
  previewUrl: 'https://take/m1.mp4',
  kfSourceHash: HASH,
  runwayAttempts: [{ n: 1, status: 'SUCCEEDED', outputUrl: 'https://take/m1.mp4' }],
});
ok(Boolean(accept?.acceptedTake?.url), 'T22 ACCEPT_EXISTING helper');

ok(
  !sameFailedInput(
    { failedKfHash: 'other', failedPromptHash: 'p', runwayAttempts: [] },
    HASH,
    'p',
  ),
  'T23 sameFailedInput still hash-based',
);

const ctaSrc = read('ContentFamixaShotProduction/ShotProductionCta.ts');
ok(ctaSrc.includes('directorPictureVideoSurface') && ctaSrc.includes('directorPrimaryCta'), 'T24 Director CTA');
ok(typeof directorPrimaryCta === 'function', 'T24 CTA export');
ok(typeof nextShotProductionCommand === 'function', 'T24 orchestrator');

const studioSrc = read('famixa-video-studio-ux.smoke.ts');
ok(studioSrc.includes('no provider') || studioSrc.includes('startContentSeriesTurbo') || studioSrc.includes('Fal confirmation'), 'T25 studio smoke present');

const packInfra = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const stillSrc = readFileSync(join(packInfra, 'ContentSeriesStillService.cs'), 'utf8');
const turboSrc = readFileSync(join(packInfra, 'ContentSeriesTurboService.cs'), 'utf8');
const pilotSrc = readFileSync(join(packInfra, 'ContentSeriesPilotService.cs'), 'utf8');
ok(stillSrc.includes('IFamixaProviderRegistry') && stillSrc.includes('GetPicture'), 'wire still');
ok(turboSrc.includes('GetMotion') && turboSrc.includes('GetLipSync'), 'wire turbo');
ok(pilotSrc.includes('GetVoice'), 'wire voice');
ok(!turboSrc.includes('_fal.CreateImageToVideoAsync'), 'turbo start no direct fal i2v');
ok(!turboSrc.includes('_runway.CreateImageToVideoAsync'), 'turbo start no direct runway i2v');
ok(!stillSrc.includes('GenerateImageWithRefsAsync'), 'still generate via adapter');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_ORCHESTRATION_CORE_V1 T1–T25');
