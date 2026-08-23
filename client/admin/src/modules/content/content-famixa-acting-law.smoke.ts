import {
  actingI2vBrief,
  actingI2vBriefFromLines,
  actingI2vClause,
  actingTtsPerformText,
  actingTtsVoiceSettings,
  inferActingDirection,
  stripStageDirection,
  lockLinePerformance,
  resolveLinePerformance,
  spokenFromPerformText,
  compileShotBlocking,
  stillAtmosphereFromAction,
  stillFaceFromPerformance,
  stillFaceFromScriptNote,
} from './content-famixa-acting-law';

const fail: string[] = [];
const linhTam = inferActingDirection({ text: 'Tám?', characterId: 'CHAR-003', name: 'Linh' });
if (linhTam.emotion !== 'uneasy' || linhTam.intensity !== 2) fail.push(`Linh Tám? ${linhTam.label}`);
if (linhTam.volume !== 'low' || linhTam.pace !== 'slow') fail.push('Tám? is quiet/slow');

const minh = inferActingDirection({ text: 'Thì sao?', characterId: 'CHAR-001', name: 'Minh' });
if (minh.emotion !== 'annoyed') fail.push(`Minh Thì sao? ${minh.label}`);
if (minh.pace !== 'fast') fail.push('Minh pushback is short/fast');

const hide = inferActingDirection({ text: 'Không có gì.', characterId: 'CHAR-001' });
if (hide.emotion !== 'hurt') fail.push('withhold is hurt, not lecture');

const fromAction = inferActingDirection({ action: 'Linh nhận lấy. Linh: Tám?' });
if (fromAction.emotion !== 'uneasy') fail.push(`action Linh: Tám? ${fromAction.label}`);

const i2v = actingI2vClause(minh);
if (/[àáạảãăâèéêìíòóôơùúưỳýđ]/i.test(i2v)) fail.push('I2V acting must be English');
if (/hug|apology reset|scream/i.test(i2v) === false) fail.push('must forbid hug/scream');
if (/[àáạảãăâèéêìíòóôơùúưỳýđ]/i.test(actingI2vBrief(minh))) fail.push('brief must be English');

const tts = actingTtsVoiceSettings(hide, { speed: 1, stability: 0.4, style: 0.4 });
if (tts.speed > 0.91) fail.push('hurt must slow TTS');
if (tts.style > 0.16) fail.push('hurt must not be theatrical');

const burst = inferActingDirection({ text: 'Mẹ!', characterId: 'CHAR-001' });
if (burst.emotion === 'burst') fail.push('Mẹ! is not a scream beat');
const khoe = inferActingDirection({
  text: 'Mẹ! Con gỡ được điểm Toán rồi! Chín điểm! — KHOE BÀI TRONG VỠ ÒA (0–6s)',
  characterId: 'CHAR-001',
  name: 'Minh',
});
if (khoe.emotion !== 'burst') fail.push(`khoe chín điểm must be burst, got ${khoe.label}`);
if (stripStageDirection('Mẹ! Chín điểm! — KHOE BÀI TRONG VỠ ÒA (0–6s)') !== 'Mẹ! Chín điểm!') {
  fail.push('strip stage cue from TTS');
}
if (actingTtsPerformText('Mẹ! Chín điểm! — KHOE BÀI TRONG VỠ ÒA', khoe) !== 'Mẹ! Chín điểm!') {
  fail.push('TTS must not speak KHOE stage cue');
}

const performed = actingTtsPerformText('[sadly] Không có gì.', hide);
if (performed !== 'Không có gì.') fail.push('TTS must send spoken words only — no English tags');
if (spokenFromPerformText('[quietly] Bố.') !== 'Bố.') fail.push('strip tags must restore line');
if (/ôm|xin lỗi|bài học|hug|scream|sadly|quietly/i.test(performed)) fail.push('must not invent hug/lesson or speak tags');

const phone = stillFaceFromScriptNote('Linh', 'Mẹ đang bận bịu, chăm chăm vào điện thoại.');
if (!/phone|not smiling/i.test(phone)) fail.push(`phone note ${phone}`);
const stern = stillFaceFromScriptNote('Linh', 'mặt nghiêm nghị không vui, không hài lòng');
if (!/stern|unsatisfied/i.test(stern)) fail.push(`stern note ${stern}`);
const dad = stillFaceFromScriptNote('Nam', 'bố chán nản vì mẹ thành tích');
if (!/weary|disappointed/i.test(dad)) fail.push(`dad note ${dad}`);
const air = stillAtmosphereFromAction('Phòng khách tối. Mẹ không vui.', 'Phòng khách tối');
if (/bright sunlit|cheerful stock/i.test(air) === false && !/dim|tense/i.test(air)) fail.push(`atmosphere ${air}`);
if (!/No cheerful stock smiles/i.test(air)) fail.push('must forbid cheerful still');
const block = compileShotBlocking({
  speakerNames: ['Nam'],
  peopleNames: ['Nam', 'Minh', 'Linh'],
  action: 'Nam: Anh về rồi.',
});
if (!/SPEAKER LOCK/i.test(block) || !/Nam/i.test(block)) fail.push(`speaker lock ${block}`);
if (!/family portrait|huddle/i.test(block)) fail.push('blocking must forbid family portrait');
const twoFace = compileShotBlocking({
  speakerNames: ['Minh'],
  peopleNames: ['Minh', 'Linh'],
  action: 'Liếc nhìn con số 9.',
});
if (!/Primary face|shoulder|torso/i.test(twoFace)) fail.push('two-shot blocking must keep speaker face, allow secondary shoulder');
const stayNam = compileShotBlocking({
  speakerNames: ['Linh'],
  peopleNames: ['Nam', 'Minh', 'Linh'],
  action: 'Linh đáp.',
  namAlreadyIn: true,
});
if (!/STAYS|same shirt|same face/i.test(stayNam)) fail.push(`nam persist ${stayNam}`);

const locked = lockLinePerformance({ ...hide, emotion: 'hurt', intensity: 2, label: hide.label });
const afterF5 = resolveLinePerformance({ text: 'Thì sao?', characterId: 'CHAR-001', performance: locked });
if (afterF5.emotion !== 'hurt') fail.push('locked performance must survive different text');
const two = actingI2vBriefFromLines([
  { text: 'Mẹ...', characterId: 'CHAR-001', performance: lockLinePerformance({ ...uneasyMinh(), label: 'khó chịu 2/5' }) },
  { text: 'Con được chín.', characterId: 'CHAR-001', performance: lockLinePerformance({ ...burstMinh(), label: 'bùng 4/5' }) },
]);
if (!/beats/i.test(two) || !/then/i.test(two)) fail.push('I2V must list locked beats, not first line only');
if (!/held face|eyes down/i.test(stillFaceFromPerformance('Minh', hide))) fail.push('KF face from locked hurt');

function uneasyMinh() {
  return inferActingDirection({ text: 'Mẹ xem.', characterId: 'CHAR-001', name: 'Minh' });
}
function burstMinh() {
  return { ...inferActingDirection({ text: 'Mẹ!', characterId: 'CHAR-001' }), emotion: 'burst' as const, intensity: 4 as const, label: 'bùng 4/5' };
}

if (fail.length) {
  console.error('ACTING LAW FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('ACTING LAW PASS');
