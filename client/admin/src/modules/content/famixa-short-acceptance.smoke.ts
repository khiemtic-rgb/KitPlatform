import { bindShotChecklist, explicitArtifactDecision, playbackReviewTarget, previewBytesMatch, qualityReviewAccepted, reviewTargetForDecision } from './famixa-short-acceptance';
import type { SeriesShotRun } from './content-famixa-series';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const ticks: SeriesShotRun = {
  status: 'reviewed',
  lipsyncUrl: 'https://fal/out.mp4',
  takeUrl: 'https://take/mute.mp4',
  shotQa: { lipsyncQuality: true, finalAv: true },
};
ok(!qualityReviewAccepted(ticks), 'ticks without playback id are not accepted');
ok(playbackReviewTarget(ticks) === 'lipsync:https://fal/out.mp4', 'lipsync url is the review target');

const bound: SeriesShotRun = {
  ...ticks,
  shotQa: {
    action: true,
    continuity: true,
    motion: true,
    voiceFace: true,
    lipsyncQuality: true,
    finalAv: true,
    decision: 'ACCEPT',
    reviewedPlaybackId: 'lipsync:https://fal/out.mp4',
  },
};
ok(qualityReviewAccepted(bound), 'accept binds the lipsync playback');

const otherFile: SeriesShotRun = {
  ...bound,
  shotProduction: { assembleFileName: 'famixa-SH02-01.mp4' },
};
ok(!qualityReviewAccepted(otherFile), 'a new assembly file invalidates the old review');
ok(
  qualityReviewAccepted({
    ...otherFile,
    shotQa: {
      action: true,
      continuity: true,
      motion: true,
      voiceFace: true,
      lipsyncQuality: true,
      finalAv: true,
      decision: 'ACCEPT',
      reviewedPlaybackId: 'assembly:famixa-SH02-01.mp4',
    },
  }),
  'review can bind the assembly file',
);
ok(
  !qualityReviewAccepted({
    ...bound,
    shotQa: { ...bound.shotQa, decision: 'REJECT' },
  }),
  'reject is not accepted',
);

const osRun: SeriesShotRun = {
  ...bound,
  shotQa: {
    ...bound.shotQa!,
    decision: 'ACCEPT',
    reviewedPlaybackId: 'os:mix-1',
    reviewedContentHash: 'abc',
  },
};
ok(
  qualityReviewAccepted(osRun, { artifactId: 'mix-1', contentHash: 'abc' }),
  'an OS review binds the artifact id and hash',
);
ok(
  !qualityReviewAccepted(osRun, { artifactId: 'mix-1', contentHash: 'def' }),
  'a new hash does not inherit the OS review',
);
ok(
  !qualityReviewAccepted(osRun, { artifactId: 'mix-2', contentHash: 'abc' }),
  'a new artifact id does not inherit the OS review',
);
const hash = await previewBytesMatch(new Blob([new TextEncoder().encode('abc')]), 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
ok(hash, 'playback bytes must match the named hash');
ok(!(await previewBytesMatch(new Blob([new TextEncoder().encode('abc')]), 'deadbeef')), 'a different hash is not the named file');

const completeTicks = {
  action: true,
  continuity: true,
  motion: true,
  voiceFace: true,
  lipsyncQuality: true,
  finalAv: true,
};
const checklist = bindShotChecklist(completeTicks, 'os:58297402-e252-45e8-8d33-c7414749a297', 'e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b', true);
ok(checklist.decision === undefined && checklist.reviewedPlaybackId === 'os:58297402-e252-45e8-8d33-c7414749a297', 'completed ticks do not become ACCEPT');
ok(
  !qualityReviewAccepted({ ...osRun, shotQa: checklist }, {
    artifactId: '58297402-e252-45e8-8d33-c7414749a297',
    contentHash: 'e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b',
  }),
  'a checklist bound to the SH02 hash is still not acceptance',
);
const refused = explicitArtifactDecision({
  decision: 'ACCEPT',
  target: reviewTargetForDecision(osRun, null, true),
  hash: '',
  checklist: completeTicks,
});
ok(!refused.ok && refused.blocker === 'REVIEW_TARGET_MISSING', 'an OS shot without a resolved artifact cannot be accepted');
const acceptedFile = explicitArtifactDecision({
  decision: 'ACCEPT',
  target: 'os:58297402-e252-45e8-8d33-c7414749a297',
  hash: 'e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b',
  checklist: completeTicks,
});
ok(acceptedFile.ok && acceptedFile.qa.decision === 'ACCEPT' && acceptedFile.qa.reviewedContentHash === 'e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b', 'ACCEPT is an explicit decision for that artifact and hash');
const rejectedFile = explicitArtifactDecision({
  decision: 'REJECT',
  target: 'os:58297402-e252-45e8-8d33-c7414749a297',
  hash: 'e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b',
  checklist: completeTicks,
});
ok(rejectedFile.ok && rejectedFile.qa.decision === 'REJECT', 'REJECT is an explicit decision for the same artifact');
const missingHash = explicitArtifactDecision({
  decision: 'ACCEPT',
  target: 'os:58297402-e252-45e8-8d33-c7414749a297',
  hash: '',
  checklist: completeTicks,
});
ok(!missingHash.ok && missingHash.blocker === 'REVIEW_HASH_MISSING', 'an OS accept without the hash is refused');

if (fail.length) {
  console.error(`FAMIXA_SHORT_ACCEPTANCE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SHORT_ACCEPTANCE_V1 PASS FAIL=0');
