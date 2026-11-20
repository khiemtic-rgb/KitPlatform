/** FAMIXA_VEO_JOIN_CREDENTIALS_V1 — config shape only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { acceptVeoJoinFile, isGeminiApiKeyLookalike } from './famixa-veo-join-credentials';
import { rewriteVeoTakePointers, veoAssembleDurationSec, veoMappedDurationSec, veoSpeechOverflowReason, veoTakePlaybackUrl } from './famixa-veo-take-playback';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const settings = readFileSync(join(root, 'ContentAiSettingsPage.tsx'), 'utf8');
const catalog = readFileSync(join(root, 'content-ai-provider-catalog.ts'), 'utf8');

ok(catalog.includes("title: 'Google Veo 3'"), 'catalog has Google Veo 3');
ok(catalog.includes("title: 'Runway Turbo'"), 'catalog has Runway Turbo');
ok(catalog.includes("id: 'veo'") && catalog.includes("id: 'elevenlabs'"), 'catalog has video and voice rows');
ok(settings.includes('Chọn file Join') && settings.includes('acceptVeoJoinFile'), 'UI picks Join file');
ok(settings.includes('Veo không dùng key Gemini'), 'UI states Veo ≠ Gemini key');
ok(settings.includes('<Table') && settings.includes('Thêm') && settings.includes('Sửa') && settings.includes('Xóa'), 'settings is a grid with add/edit/delete');
ok(settings.includes('<Drawer') && settings.includes('bấm dòng để sửa'), 'row click opens detail drawer');
ok(!settings.includes('i2vChoice'), 'settings is not XOR');
ok(settings.includes('Chọn Runway / Wan / Veo 3 ở bàn shot'), 'settings points at generate-time picker');
ok(catalog.includes('PINNED_CONTENT_AI_PROVIDERS') && catalog.includes("'runway', 'fal', 'veo'"), 'video rows stay on grid');
ok(!/i2vChoice === 'veo'[\s\S]{0,400}geminiApiKey/.test(settings), 'Veo pane does not bind Gemini key');
const desk = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
ok(desk.includes('FamixaMotionEnginePicker'), 'shot desk shows engine picker');
ok(series.includes('refuseUnwiredVeo') && series.includes("engine !== 'veo' || keys.veo"), 'Veo blocked only without Join');
ok(series.includes("engine: 'veo'") || series.includes('motionProviderFromEngine'), 'Veo can send when Join stored');
ok(!series.includes('Máy gửi Veo chưa nối'), 'wired Veo no longer fail-closed after Join');
ok(desk.includes('fetchContentSeriesVeoVideo') && desk.includes('data:video'), 'desk loads Veo file, does not play poll data-URI');
ok(series.includes('veoAssembleDurationSec') && series.includes('dialogueFloorOf') && series.includes('isVeoMotionTaskId'), 'Veo send/assemble uses mapped 4/6/8s');
ok(veoAssembleDurationSec(5, 4.2) === 6 && veoAssembleDurationSec(4, 4.24) === 6, 'assemble keeps 6s when speech floor >4');
ok(veoAssembleDurationSec(5, 0) === 4 && veoAssembleDurationSec(5, 3.5) === 4, 'silent Timing 5 still maps assemble 4');
ok(series.includes('veoSpeechOverflowReason') && series.includes('Không gửi Fal trên take ngắn'), 'lipsync blocks clipped Veo take');
ok(veoMappedDurationSec(5) === 4, 'Timing 5 silent maps Veo 4');
ok(veoMappedDurationSec(5, 4.2) === 6, 'spoken floor >4 maps Veo 6');
ok(veoMappedDurationSec(5, 3.5) === 4, 'short line stays Veo 4');
ok(veoMappedDurationSec(8) === 8, '8 stays 8');
ok(
  Boolean(veoSpeechOverflowReason({ takeSec: 4, dialogueEndSec: 4.3, floorSec: 4.5 })?.includes('6s')),
  'overflow names 6s remake',
);
ok(!veoSpeechOverflowReason({ takeSec: 4, dialogueEndSec: 3.6 }), 'short line does not overflow 4s take');
const veoCs = readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentVeoClient.cs'), 'utf8');
ok(veoCs.includes('FamixaVeoTakePlaybackV1.PublicPath') && !veoCs.includes('data:video/mp4;base64'), 'Veo poll returns short path, not base64');
const turboCs = readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'), 'utf8');
ok(
  turboCs.includes('IsAllowedLipsyncVideoRef') && turboCs.includes('ReadVeoTake') && turboCs.includes('file Veo / Fal đã lưu'),
  'lipsync accepts stored Veo take, not HTTPS-only',
);
ok(
  readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentFalClient.cs'), 'utf8').includes('active_speaker_detection'),
  'Fal v3 can pin one speaker face',
);
ok(series.includes('lipsyncNeedsSpeakerPick') && series.includes('lipsyncSpeaker'), 'desk requires speaker face click');

const missing = acceptVeoJoinFile('');
ok(!missing.ok && missing.code === 'VEO_JOIN_FILE_MISSING', 'missing fail-closed');

ok(isGeminiApiKeyLookalike('AIzaSyDummyGeminiKeyThatLooksReal12'), 'AIza lookalike');
const gemini = acceptVeoJoinFile('AIzaSyDummyGeminiKeyThatLooksReal12', 'key.txt');
ok(!gemini.ok && gemini.code === 'GEMINI_API_KEY_REJECTED', 'Gemini key rejected');

const accepted = acceptVeoJoinFile(
  '{"type":"service_account","project_id":"demo","client_email":"veo@demo.iam.gserviceaccount.com"}',
  'C:\\\\secrets\\\\veo-join.json',
);
ok(accepted.ok && accepted.fileName === 'veo-join.json', 'JSON Join accepted');

const task = 'veo_recoverTake55';
const play = veoTakePlaybackUrl(task);
const recovered = rewriteVeoTakePointers({
  turboTaskId: task,
  takeUrl: 'data:video/mp4;base64,AAAA',
  previewUrl: 'data:video/mp4;base64,AAAA',
  runwayAttempts: [{ taskId: task, outputUrl: 'data:video/mp4;base64,AAAA' }],
});
ok(play && recovered.takeUrl === play && recovered.runwayAttempts?.[0]?.outputUrl === play, 'rewrite drops data:video for Veo path');
ok(!rewriteVeoTakePointers({ takeUrl: 'https://fal.example/take.mp4' }).takeUrl?.includes('veo-video'), 'rewrite does not touch Fal take');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_VEO_JOIN_CREDENTIALS_V1 FAIL=0');
