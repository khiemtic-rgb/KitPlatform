/** FAMIXA_AI_PROVIDER_LIFECYCLE_PARITY_V1 — T1–T22. Fake providers. 0 HTTP. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  freezeMotionInput,
  sameMotionContentFingerprint,
  validateMotionCallback,
  adaptMotionAttempts,
  currentPictureInputOf,
  type ExecutionAwareAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  FAMIXA_PROVIDER_ERROR,
  FakeLipSyncProvider,
  FakeMotionProvider,
  FakePictureProvider,
  FakeVoiceProvider,
  FamixaProviderRegistry,
  resolveFamixaProvider,
  resolveFamixaProviderTask,
} from './famixa-ai-provider-orchestration';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const packInfra = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const packApp = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application');
const turboSrc = readFileSync(join(packInfra, 'ContentSeriesTurboService.cs'), 'utf8');
const adapterSrc = readFileSync(join(packInfra, 'FamixaProviderAdapters.cs'), 'utf8');
const rulesSrc = readFileSync(join(packApp, 'FamixaProviderOrchestrationV1Rules.cs'), 'utf8');

const runway = new FakeMotionProvider('runway');
const wan = new FakeMotionProvider('wan');
const fal = new FakeLipSyncProvider();
const registry = new FamixaProviderRegistry(
  { gemini: new FakePictureProvider() },
  { runway, wan },
  { elevenlabs: new FakeVoiceProvider() },
  { fal },
);

const motReq = { imageDataUrl: 'data:image/jpeg;base64,x', prompt: 'move', seconds: 5 as const, ratio: '1280:720' };
const startedRunway = await registry.getMotion(resolveFamixaProvider('MOTION', { engine: 'turbo' }).providerId).start(motReq);
ok(startedRunway.providerId === 'runway' && runway.calls.some((c) => c.kind === 'start'), 'T1 Runway Start → adapter');

const pollRunway = await registry.getMotionForTask(startedRunway.providerRequestId, 'runway').get(startedRunway.providerRequestId!);
ok(pollRunway.providerId === 'runway' && runway.calls.some((c) => c.kind === 'get'), 'T2 Runway Poll → adapter');
ok(!turboSrc.includes('_runway.GetTaskAsync') && !turboSrc.includes('ContentRunwayClient'), 'T2 TurboService no Runway client');

const startedWan = await registry.getMotion(resolveFamixaProvider('MOTION', { engine: 'wan' }).providerId).start(motReq);
ok(startedWan.providerId === 'wan' && wan.calls.some((c) => c.kind === 'start'), 'T3 Wan Start → adapter');

const wanTask = 'wan_01a08088-04a0-7e81-bc31-c0a786a4c5c2';
const pollWan = await registry.getMotionForTask(wanTask).get(wanTask);
ok(pollWan.providerId === 'wan' && wan.calls.some((c) => c.kind === 'get' && c.taskId === wanTask), 'T4 Wan Poll → Wan adapter');
ok(!runway.calls.some((r) => r.taskId === wanTask), 'T4 Wan poll not Runway');
ok(!turboSrc.includes('_fal.GetTaskAsync') && !turboSrc.includes('IsFalTask'), 'T4 TurboService no Fal poll');

const recoverWan = await registry.getMotionForTask(wanTask).recover(wanTask);
ok(recoverWan.providerId === 'wan' && wan.calls.some((c) => c.kind === 'recover'), 'T5 Wan recover → Wan adapter');
ok(!turboSrc.includes('NormalizeRunwayTaskId') && turboSrc.includes('RecoverAsync'), 'T5 recover not Runway normalize');

const lipStart = await registry.getLipSync('fal').start({ videoUrl: 'https://take.mp4', audioUrl: 'https://v.mp3', modelKind: 'v3' });
ok(lipStart.providerId === 'fal' && fal.calls.some((c) => c.kind === 'start'), 'T6 Fal LipSync Start');

const lipTask = 'lipsync_v3_abc';
const lipPoll = await registry.getLipSyncForTask(lipTask).get(lipTask);
ok(lipPoll.providerId === 'fal' && fal.calls.some((c) => c.kind === 'get'), 'T7 Fal LipSync Poll');
ok(turboSrc.includes('GetLipSync') && turboSrc.includes('.GetAsync'), 'T7 Turbo poll via LipSync adapter');

let unavailable = false;
try {
  resolveFamixaProviderTask('task', 'kling');
} catch (e) {
  unavailable = String((e as Error).message).includes('PROVIDER_UNAVAILABLE')
    || (e as { code?: string }).code === FAMIXA_PROVIDER_ERROR.PROVIDER_UNAVAILABLE
    || String((e as Error).message).includes('UNSUPPORTED');
}
ok(unavailable, 'T8 unknown provider');

const known = resolveFamixaProviderTask('not-a-prefix-task', 'wan');
ok(known.providerId === 'wan' && known.source === 'EXPLICIT', 'T9 attempt provider wins');
ok((await registry.getMotionForTask('not-a-prefix-task', 'wan').get('not-a-prefix-task')).providerId === 'wan', 'T9 poll uses attempt provider');

const legacy = resolveFamixaProviderTask('9ef1c551-aaaa-bbbb-cccc-ddddeeeeffff');
ok(legacy.providerId === 'runway' && legacy.source === 'LEGACY_UNVERIFIED', 'T10 legacy UUID LEGACY_UNVERIFIED');
ok(resolveFamixaProviderTask(wanTask).source === 'LEGACY_PREFIX', 'T10 wan_ is LEGACY prefix not canonical');

const KF = 'data:image/jpeg;base64,LIFEPIXAAA';
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
const otherPic = stateOf(run({
  keyframeDataUrl: 'data:image/jpeg;base64,LIFEPIXBBB',
  kfSourceHash: dataUriHash('data:image/jpeg;base64,LIFEPIXBBB'),
  pictureRevisionId: 'picture:EP01-SC01-SH01:007',
}));
const running = (frozen: typeof frozenRunway, taskId: string): ExecutionAwareAttempt => ({
  n: 1,
  at: '2026-09-09T00:00:00.000Z',
  status: 'RUNNING',
  taskId,
  attemptId: 'mot:EP01-SC01-SH01:1',
  frozenInput: frozen,
  inputFingerprint: frozen.executionFingerprint,
});
ok(
  validateMotionCallback({
    attempts: adaptMotionAttempts(shot.id, [running(frozenRunway, 'rw-late')]),
    current: { ...freezeMotionInput(otherPic, shot), picture: currentPictureInputOf(otherPic, shot) },
    providerTaskId: 'rw-late',
  }).resultClass === 'LATE_RESULT',
  'T11 Runway late → LATE_RESULT',
);
ok(
  validateMotionCallback({
    attempts: adaptMotionAttempts(shot.id, [running(frozenWan, wanTask)]),
    current: { ...freezeMotionInput(otherPic, shot, { provider: 'wan' }), picture: currentPictureInputOf(otherPic, shot) },
    providerTaskId: wanTask,
  }).resultClass === 'LATE_RESULT',
  'T12 Wan late → LATE_RESULT',
);
ok(
  sameMotionContentFingerprint(frozenRunway.executionFingerprint, frozenWan.executionFingerprint),
  'T11/T12 callback WHAT unchanged',
);

ok(!('CURRENT' in pollRunway) && !('APPROVED' in pollRunway) && !('ACCEPTED' in pollWan), 'T13 adapter result has no CURRENT/APPROVED');
ok(rulesSrc.includes('FamixaProviderResult') && !rulesSrc.includes('CURRENT') || rulesSrc.includes('Does not decide CURRENT'), 'T13 contract comment');

ok((await registry.getMotionForTask('rw-resume', 'runway').get('rw-resume')).providerId === 'runway', 'T14 Resume Runway → adapter');
ok((await registry.getMotionForTask(wanTask).get(wanTask)).providerId === 'wan', 'T15 Resume Wan → adapter');
ok((await registry.getLipSyncForTask(lipTask).get(lipTask)).providerId === 'fal', 'T16 LipSync resume → Fal adapter');
ok(turboSrc.includes('DescribeTask') && turboSrc.includes('GetMotion') && turboSrc.includes('GetLipSync'), 'T14–T16 service routes via registry');

const artSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionArtifacts.ts'), 'utf8');
ok(artSrc.includes('samePictureSuccessTakeOf') && artSrc.includes('playableMotionTakeOf'), 'T17 Artifact V2');
const execSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionExecution.ts'), 'utf8');
ok(execSrc.includes('validateMotionCallback') && execSrc.includes('sameMotionContentFingerprint'), 'T18 ShotExecutionState');
ok(readFileSync(join(root, 'famixa-shot-production-timing.ts'), 'utf8').includes('providerDurationOf'), 'T19 Timing V3');
ok(readFileSync(join(root, 'content-famixa-picture-pixel-invariant.ts'), 'utf8').includes('applyPictureGenerationGuard'), 'T20 Picture invariant');
ok(Boolean(acceptExistingTake({ takeUrl: 'https://take/m1.mp4', runwayAttempts: [{ n: 1, status: 'SUCCEEDED', outputUrl: 'https://take/m1.mp4' }] })?.acceptedTake?.url), 'T21 ACCEPT_EXISTING');
ok(!sameFailedInput({ failedKfHash: 'other', failedPromptHash: 'p', runwayAttempts: [] }, HASH, 'p'), 'T22 sameFailedInput');

ok(adapterSrc.includes('GetAsync') && adapterSrc.includes('RecoverAsync') && adapterSrc.includes('CanHandleTask'), 'adapters lifecycle');
ok(!turboSrc.includes('_runway'), 'no _runway field');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_LIFECYCLE_PARITY_V1 T1–T22');
