import { ttsHydrateKeys, ttsLineKey, ttsLookupKeys, ttsTextKey } from './content-famixa-tts-store';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const keys = ttsLookupKeys({ id: 'DIA-02', text: 'Mẹ! Con gỡ được điểm Toán rồi!', voiceId: 'v1' }, ['v2']);
if (!keys.includes('DIA-02')) fail.push('bare line id');
if (!keys.includes(ttsLineKey('DIA-02', 'v1'))) fail.push('line#voice');
if (!keys.includes(ttsLineKey('DIA-02', 'v2'))) fail.push('extra voice');
if (!keys.includes(ttsTextKey('Mẹ! Con gỡ được điểm Toán rồi!', 'v1'))) fail.push('text+voice');
if (!keys.includes(ttsTextKey('Mẹ! Con gỡ được điểm Toán rồi!'))) fail.push('text without voice');
if (ttsLookupKeys({ id: 'DIA-02', text: 'x' }).includes('DIA-02#v1')) fail.push('must not invent voice');

const hydrate = ttsHydrateKeys(
  { id: 'line-SC01-CHAR-001-2', text: 'Dạ... nay con làm bài không tốt ạ', voiceId: 'v1' },
  ['v1'],
);
if (hydrate.some((k) => k === 'line-SC01-CHAR-001-2')) fail.push('hydrate must not use bare line id');
if (!hydrate.includes(ttsTextKey('Dạ... nay con làm bài không tốt ạ', 'v1'))) fail.push('hydrate keeps this-line text');
if (hydrate.includes(ttsTextKey('Dạ... nay con làm bài không tốt ạ'))) fail.push('hydrate must not reuse other-voice same text');
if (hydrate.some((k) => /gỡ được|9 điểm/i.test(k))) fail.push('hydrate must not look up old 9-point line');

const src = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'content-famixa-tts-store.ts'), 'utf8');
if (src.includes('ttsLegacy') || src.includes('famixaLegacyKey')) fail.push('TTS must not read other-build blobs');
const series = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'ContentFamixaSeriesTab.tsx'), 'utf8');
if (!series.includes('ttsHydrateKeys') || series.includes('findTtsBlobForLine')) fail.push('hydrate must stay on this build + text');

if (fail.length) {
  console.error('TTS STORE FAIL');
  fail.forEach((f) => console.error(' -', f));
  process.exit(1);
}
console.log('TTS STORE PASS · lookup keys recover voiceId miss');
