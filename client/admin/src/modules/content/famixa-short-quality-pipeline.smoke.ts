import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  COMPLETE_TAKE_PAPER,
  SH01_FUTURE_ACTING,
  SH02_FUTURE_ACTING,
  brokenTakePaper,
  BROKEN_TAKE_PAPER,
  SH03_SH04_FUTURE_ACTING,
  SH05_FUTURE_ACTING,
  directorBlockerLines,
  episodeAssemblyNotice,
  approvedDialogueIntervals,
  futureClockBlocker,
  futureDeskActing,
  clickTimeLipsyncClock,
  currentLipsyncMeasurement,
  productionClockBlockers,
  futureDeskStory,
  shotQualityBlockers,
  spokenLineForTts,
  technicalFileIsVisualAccept,
  voiceDraftForRequest,
} from './famixa-short-quality-pipeline';
import { explicitDirectorSpeechInterval, DIRECTOR_PRODUCTION_TIMING_OPTIONS } from './famixa-director-timing';
import { computeShotTiming } from './famixa-shot-production-timing';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const root = dirname(fileURLToPath(import.meta.url));
const pipeline = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ShortProductionQualityPipelineV1.cs'),
  'utf8',
);
const canon = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01IngressCanon.cs'),
  'utf8',
);

ok(canon.includes('Linh entering the room'), 'historical SH01 acting still says Linh enters');
ok(pipeline.includes(SH01_FUTURE_ACTING), 'desk SH01 acting matches the server future spec');
ok(pipeline.includes(SH02_FUTURE_ACTING), 'desk SH02 acting matches the server future spec');
ok(pipeline.includes('PilotExecuted => false'), 'pilot call is not executed');
ok(!technicalFileIsVisualAccept(), 'a verified file is not visual acceptance');

ok(
  futureDeskActing('EP01-SC01-SH01', 'Audience must see Linh entering the room, face toward Minh.').includes('Minh entering'),
  'next SH01 request names Minh',
);
ok(
  futureDeskActing('EP01-SC01-SH02', 'Cậu rút ra tờ bài kiểm tra đã bị gấp góc, đưa hai  Face visible.').includes('hand it to Linh'),
  'next SH02 request completes the cut-off action',
);
ok(futureDeskStory('EP01-SC01-SH04', '(Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.') === COMPLETE_TAKE_PAPER, 'SH04 story is one sentence');
ok(!brokenTakePaper(COMPLETE_TAKE_PAPER), 'completed sentence is closed');
ok(spokenLineForTts('(lý nhí) Dạ… nay con làm bài không tốt.') === 'Dạ… nay con làm bài không tốt.', 'stage direction is not spoken');
ok(spokenLineForTts('Về rồi hả con?') === 'Về rồi hả con?', 'a line without a cue is unchanged');
ok(futureDeskActing('GEN-SC01-SH01', 'An stands at the door.') === 'An stands at the door.', 'another script is left as written');
ok(futureDeskActing('EP01-SC01-SH04', BROKEN_TAKE_PAPER) === SH03_SH04_FUTURE_ACTING, 'SH04 next acting keeps the red-score prop');
ok(futureDeskActing('EP01-SC01-SH05', 'Minh is already seated.').includes('not already seated'), 'SH05 next acting does not start seated');
ok(futureClockBlocker(true, 2.534, 8, 2.5) === 'SPEECH_DOES_NOT_COVER_PLAYBACK', 'a short line blocks the next clock');
ok(futureClockBlocker(true, 4, 4.4) === '', 'a line inside the tolerance is allowed');
ok(
  futureDeskActing('EP01-SC01-SH02', 'Minh đứng yên và nhìn tờ giấy trắng.').includes('red score'),
  'a stale SH02 sentence does not have to contain the truncated phrase',
);
ok(
  futureDeskActing('EP01-SC01-SH01', 'Linh walks in from the door.').includes('Minh entering'),
  'SH01 correction does not depend on the historical English phrase',
);
const partial = productionClockBlockers({
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  lipsyncSec: 2.5,
  dialogueIntervals: [{ startSec: 0, endSec: 2.5 }],
  mix: [
    { startSec: 0, endSec: 2.5, carriesSpeech: true },
    { startSec: 2.5, endSec: 8, carriesSpeech: false },
  ],
  mouthDuringSilence: false,
  mouthDuringSpeech: true,
});
ok(partial.length === 0, 'dialogue may cover only part of a longer picture');
ok(approvedDialogueIntervals({ source: 'DEFAULT_TAKE', speechStartSec: 0, speechEndSec: 2.5 }).length === 0, 'a computed timing is not a dialogue window');
ok(approvedDialogueIntervals(undefined).length === 0, 'a missing timing is not a dialogue window');
const directorWindow = approvedDialogueIntervals({ source: 'DIRECTOR', speechStartSec: 0.2, speechEndSec: 2.73 });
ok(directorWindow.length === 1 && directorWindow[0].startSec === 0.2 && directorWindow[0].endSec === 2.73, 'only a director-locked timing is a dialogue window');
const splitMouth = productionClockBlockers({
  directDialogue: true,
  audioSec: 2.534,
  pictureSec: 8,
  lipsyncSec: 2.5,
  mouthDuringSilence: true,
  mouthDuringSpeech: false,
});
ok(splitMouth.includes('SPEECH_DOES_NOT_COVER_PLAYBACK') && splitMouth.includes('MIX_CLOCK_SPLIT') && splitMouth.includes('MOUTH_DURING_INTENDED_SILENCE'), 'recorded SH02 keeps the three distinct clock failures');
ok(shotQualityBlockers('EP01-SC01-SH02', false).includes('MIX_CLOCK_SPLIT'), 'SH02 records the split mix clock');
ok(shotQualityBlockers('EP01-SC01-SH02', true).length === 0, 'an accepted shot does not keep the old blockers');
ok(shotQualityBlockers('EP01-SC01-SH06', false).includes('NOT_IN_PRODUCTION_OS'), 'SH06 stays outside Production OS');
ok(directorBlockerLines(['SPEECH_DOES_NOT_COVER_PLAYBACK'])[0] === 'Thoại ngắn hơn hình đang phát.', 'director names the speech clock');
ok(pipeline.includes('Thoại ngắn hơn hình đang phát.'), 'server director copy matches the desk');
ok(episodeAssemblyNotice(['EP01-SC01-SH01', 'EP01-SC01-SH02']).includes('EP01-SC01-SH06'), 'two-shot concat is not the six-shot episode');
ok(SH05_FUTURE_ACTING.includes('sofa'), 'SH05 contract names the sofa');
const sh03 = voiceDraftForRequest('EP01-SC01-SH03', 'Điểm thấp thì lần sau gỡ được mà.');
ok(sh03.permitted && sh03.text === 'Điểm thấp thì lần sau gỡ được mà.', 'SH03 voice binds the approved line');
const sh03Cut = voiceDraftForRequest('EP01-SC01-SH03', 'Điểm thấp thì lần sau gỡ...');
ok(!sh03Cut.permitted && sh03Cut.blocker === 'SPOKEN_LINE_INCOMPLETE', 'a truncated SH03 line stays blocked');
const sh05 = voiceDraftForRequest('EP01-SC01-SH05', '');
ok(sh05.permitted && sh05.text === 'Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?', 'SH05 empty request binds the locked line');
const sh02 = voiceDraftForRequest('EP01-SC01-SH02', '(lý nhí) Dạ… nay con làm bài không tốt.');
ok(sh02.permitted && sh02.text === 'Dạ… nay con làm bài không tốt.', 'SH02 voice keeps the locked line');
ok(voiceDraftForRequest('GEN-SC01-SH01', 'Khoan đã...').permitted, 'another script may end with an ellipsis');
ok(pipeline.includes('PrepareVoiceDraft'), 'server voice draft gate is present');
const measuredLip = currentLipsyncMeasurement({
  lipsyncArtifactId: 'lip-1',
  previewArtifactId: 'lip-1',
  previewRole: 'LIPSYNC',
  previewDurationSec: 2.5,
});
ok(measuredLip.hasArtifact && measuredLip.durationSec === 2.5, 'the current lipsync artifact duration is used');
const mixDuration = currentLipsyncMeasurement({
  lipsyncArtifactId: 'lip-1',
  previewArtifactId: 'mix-1',
  previewRole: 'MIX',
  previewDurationSec: 8,
});
ok(mixDuration.hasArtifact && mixDuration.durationSec === undefined, 'a mix duration is not the lipsync duration');
ok(currentLipsyncMeasurement({ previewRole: 'LIPSYNC', previewDurationSec: 2.5 }).hasArtifact === false, 'no lipsync artifact means no measured output');
const missingMix = clickTimeLipsyncClock({
  action: 'MIX_VIDEO',
  hasCurrentLipsyncArtifact: true,
  currentLipsyncDurationSec: 0,
  directDialogue: true,
  audioSec: 2.534,
  pictureSec: 8,
});
ok(missingMix.length === 1 && missingMix[0] === 'LIPSYNC_DURATION_MISSING', 'a missing lipsync duration blocks the click and is not zero');
const knownSplit = clickTimeLipsyncClock({
  action: 'MIX_VIDEO',
  hasCurrentLipsyncArtifact: true,
  currentLipsyncDurationSec: measuredLip.durationSec,
  directDialogue: true,
  audioSec: 2.534,
  pictureSec: 8,
});
ok(knownSplit.includes('SPEECH_DOES_NOT_COVER_PLAYBACK') && knownSplit.includes('MIX_CLOCK_SPLIT'), 'the measured 2.5s lipsync keeps the split');
const firstLipsync = clickTimeLipsyncClock({
  action: 'CREATE_LIPSYNC',
  hasCurrentLipsyncArtifact: false,
  directDialogue: true,
  audioSec: 2.534,
  pictureSec: 8,
});
ok(firstLipsync.includes('SPEECH_DOES_NOT_COVER_PLAYBACK') && !firstLipsync.includes('MIX_CLOCK_SPLIT') && !firstLipsync.includes('LIPSYNC_DURATION_MISSING'), 'a first lipsync is not given a fabricated zero length');
const computedLead = computeShotTiming({ voiceDurations: [2.5] });
ok(computedLead.speechStartSec === 0.2 && approvedDialogueIntervals(computedLead).length === 0, 'a computed 0.2s lead-in is not a director interval');
ok(explicitDirectorSpeechInterval(computedLead) === null, 'a computed lead-in is not stored as director speech');
for (const sec of DIRECTOR_PRODUCTION_TIMING_OPTIONS) {
  ok(approvedDialogueIntervals({ source: 'DIRECTOR', durationSec: sec }).length === 0, `${sec}s picture duration does not create a dialogue window`);
}
const covered = productionClockBlockers({
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  lipsyncSec: 2.5,
  dialogueIntervals: [{ startSec: 2, endSec: 4.5 }],
  mix: [
    { startSec: 0, endSec: 2, carriesSpeech: false },
    { startSec: 2, endSec: 4.5, carriesSpeech: true },
    { startSec: 4.5, endSec: 8, carriesSpeech: false },
  ],
  mouthDuringSilence: false,
  mouthDuringSpeech: true,
});
ok(!covered.includes('DIALOGUE_WINDOW_MISMATCH') && !covered.includes('SPEECH_DOES_NOT_COVER_PLAYBACK'), 'a mix span that covers the director interval has no clock mismatch');
const shiftedToZero = productionClockBlockers({
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  lipsyncSec: 2.5,
  dialogueIntervals: [{ startSec: 2, endSec: 4.5 }],
  mix: [
    { startSec: 0, endSec: 2.5, carriesSpeech: true },
    { startSec: 2.5, endSec: 8, carriesSpeech: false },
  ],
});
ok(shiftedToZero.includes('DIALOGUE_WINDOW_MISMATCH'), 'speech left at zero does not cover a later interval');
const missingSpan = productionClockBlockers({
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  lipsyncSec: 2.5,
  dialogueIntervals: [{ startSec: 2, endSec: 4.5 }],
});
ok(missingSpan.includes('DIALOGUE_WINDOW_MISMATCH'), 'a missing mix span stays blocked');
const placedWindow = [{ startSec: 2, endSec: 4.5 }];
const firstWithWindow = clickTimeLipsyncClock({
  action: 'CREATE_LIPSYNC',
  hasCurrentLipsyncArtifact: false,
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  dialogueIntervals: placedWindow,
});
ok(
  !firstWithWindow.includes('DIALOGUE_WINDOW_MISMATCH')
    && !firstWithWindow.includes('SPEECH_DOES_NOT_COVER_PLAYBACK')
    && !firstWithWindow.includes('LIPSYNC_DURATION_MISSING'),
  'a first lipsync uses the director window and does not demand a mix that does not exist',
);
const mixWithWindow = clickTimeLipsyncClock({
  action: 'MIX_VIDEO',
  hasCurrentLipsyncArtifact: true,
  currentLipsyncDurationSec: 2.5,
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  dialogueIntervals: placedWindow,
});
ok(
  !mixWithWindow.includes('DIALOGUE_WINDOW_MISMATCH')
    && !mixWithWindow.includes('SPEECH_DOES_NOT_COVER_PLAYBACK')
    && !mixWithWindow.includes('MIX_CLOCK_SPLIT'),
  'the first mix may place the director window',
);
const pastPicture = clickTimeLipsyncClock({
  action: 'MIX_VIDEO',
  hasCurrentLipsyncArtifact: true,
  currentLipsyncDurationSec: 2.5,
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  dialogueIntervals: [{ startSec: 6, endSec: 8.5 }],
});
ok(pastPicture.includes('DIALOGUE_WINDOW_MISMATCH'), 'a window past the picture stays blocked');
const knownBadPlacement = clickTimeLipsyncClock({
  action: 'MIX_VIDEO',
  hasCurrentLipsyncArtifact: true,
  currentLipsyncDurationSec: 2.5,
  directDialogue: true,
  audioSec: 2.5,
  pictureSec: 8,
  dialogueIntervals: placedWindow,
  mix: [
    { startSec: 0, endSec: 2.5, carriesSpeech: true },
    { startSec: 2.5, endSec: 8, carriesSpeech: false },
  ],
});
ok(knownBadPlacement.includes('DIALOGUE_WINDOW_MISMATCH'), 'a supplied mix that leaves speech at zero stays blocked');
ok(directorBlockerLines(['LIPSYNC_DURATION_MISSING'])[0] === 'Chưa đo được thời lượng khớp môi của tệp hiện tại.', 'the desk names the missing lipsync duration');

if (fail.length) {
  console.error(`FAMIXA_SHORT_QUALITY_PIPELINE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SHORT_QUALITY_PIPELINE_V1 PASS FAIL=0 (no provider)');
