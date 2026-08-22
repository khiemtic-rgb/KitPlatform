import {
  actingI2vBrief,
  actingI2vClause,
  actingTtsPerformText,
  actingTtsVoiceSettings,
  inferActingDirection,
  spokenFromPerformText,
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

const performed = actingTtsPerformText('[sadly] Không có gì.', hide);
if (performed !== 'Không có gì.') fail.push('TTS must send spoken words only — no English tags');
if (spokenFromPerformText('[quietly] Bố.') !== 'Bố.') fail.push('strip tags must restore line');
if (/ôm|xin lỗi|bài học|hug|scream|sadly|quietly/i.test(performed)) fail.push('must not invent hug/lesson or speak tags');

if (fail.length) {
  console.error('ACTING LAW FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('ACTING LAW PASS');
