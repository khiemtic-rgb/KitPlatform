import { ttsLineKey, ttsLookupKeys, ttsTextKey } from './content-famixa-tts-store';

const fail: string[] = [];
const keys = ttsLookupKeys({ id: 'DIA-02', text: 'Mẹ! Con gỡ được điểm Toán rồi!', voiceId: 'v1' }, ['v2']);
if (!keys.includes('DIA-02')) fail.push('bare line id');
if (!keys.includes(ttsLineKey('DIA-02', 'v1'))) fail.push('line#voice');
if (!keys.includes(ttsLineKey('DIA-02', 'v2'))) fail.push('extra voice');
if (!keys.includes(ttsTextKey('Mẹ! Con gỡ được điểm Toán rồi!', 'v1'))) fail.push('text+voice');
if (!keys.includes(ttsTextKey('Mẹ! Con gỡ được điểm Toán rồi!'))) fail.push('text without voice');
if (ttsLookupKeys({ id: 'DIA-02', text: 'x' }).includes('DIA-02#v1')) fail.push('must not invent voice');

if (fail.length) {
  console.error('TTS STORE FAIL');
  fail.forEach((f) => console.error(' -', f));
  process.exit(1);
}
console.log('TTS STORE PASS · lookup keys recover voiceId miss');
