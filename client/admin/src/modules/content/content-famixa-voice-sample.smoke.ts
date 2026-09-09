import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mergeVoiceSamples, voiceLibraryStamp, voiceSampleUrl, voicesNeedSampleRefresh } from './content-famixa-voice-sample';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const rows = [
  { value: 'v1', label: 'A', previewUrl: 'https://cdn.example/a.mp3' },
  { value: 'v2', label: 'B', previewUrl: '  ' },
];

ok(voiceSampleUrl(rows, 'v1') === 'https://cdn.example/a.mp3', '01 sample url');
ok(!voiceSampleUrl(rows, 'v2'), '02 blank preview is missing');
ok(!voiceSampleUrl(rows, 'v3'), '03 unknown voice');
ok(!voiceSampleUrl(rows, ''), '04 empty id');
ok(voicesNeedSampleRefresh([{ value: 'v1', label: 'A' }]) && !voicesNeedSampleRefresh(rows), '05 stale library without preview');
ok(voiceLibraryStamp(rows).includes('v1:https://cdn.example/a.mp3'), '06 stamp includes preview');
ok(
  mergeVoiceSamples(
    [{ value: 'v1', label: 'A' }],
    [{ value: 'v1', label: 'A', previewUrl: 'https://cdn.example/a.mp3' }],
  )[0]?.previewUrl === 'https://cdn.example/a.mp3',
  '07 merge keeps preview',
);

const root = dirname(fileURLToPath(import.meta.url));
const studio = readFileSync(join(root, 'ContentFamixaCharacterStudio.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const controller = readFileSync(join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'), 'utf8');
const eleven = readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentElevenLabsClient.cs'), 'utf8');
const previewFn = eleven.slice(eleven.indexOf('GetLibraryPreviewAsync'), eleven.indexOf('ReadSingleVoicePreviewUrlAsync'));
ok(studio.includes('fetchContentSeriesVoicePreview') && studio.includes('playVoiceBlob') && !studio.includes('previewContentSeriesTts'), '08 studio plays library preview, not TTS');
ok(api.includes("'/content/series/voices/${encodeURIComponent(voiceId)}/preview'") || api.includes('`/content/series/voices/${encodeURIComponent(voiceId)}/preview`'), '09 client hits preview GET');
ok(!api.slice(api.indexOf('export async function fetchContentSeriesVoicePreview'), api.indexOf('function looksLikeScreenplayTts')).includes('series/tts'), '10 preview client is not TTS');
ok(controller.includes('[HttpGet("series/voices/{voiceId}/preview")]') && controller.includes('GetLibraryPreviewAsync'), '11 API streams library preview');
ok(previewFn.includes('preview_url') === false && !previewFn.includes('text-to-speech') && !previewFn.includes('SynthesizeMp3'), '12 preview download is not TTS');
ok(eleven.includes('PreviewUrl = FirstNonEmpty(existing.PreviewUrl) ?? row.PreviewUrl'), '13 list merge keeps library preview');

if (fail.length) {
  console.error(`VOICE_SAMPLE_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('VOICE_SAMPLE_SMOKE PASS FAIL=0');
