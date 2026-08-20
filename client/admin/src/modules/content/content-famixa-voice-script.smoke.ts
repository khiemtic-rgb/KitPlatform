import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseEpisodeStory } from './content-famixa-story-parse';
import { deriveVoiceScript, looksLikeScreenplayDump, verifyVoiceGeneration } from './content-famixa-voice-script';
import type { SeriesPilotState } from './content-famixa-series';

const here = dirname(fileURLToPath(import.meta.url));
const golden = readFileSync(join(here, 'content-famixa-ep01-golden.txt'), 'utf8');
const parsed = parseEpisodeStory(golden);
if (!parsed) throw new Error('golden parse failed');

const state = {
  roles: parsed.roles,
  runs: {},
  episode: parsed.episode,
  characters: parsed.characters,
  scenes: parsed.scenes,
  lines: parsed.lines,
} as SeriesPilotState;

const script = deriveVoiceScript(state);
const fail: string[] = [];
if (script.lines.length !== 71) fail.push(`expected 71 dialogue lines, got ${script.lines.length}`);
if (script.lines.some((l) => looksLikeScreenplayDump(l.text))) fail.push('a TTS line looks like a screenplay dump');
if (script.lines.some((l) => /^(SC\d+|LỜI HỨA|TIN NHẮN|CUT TO)/i.test(l.text))) {
  fail.push('heading leaked into Voice Script');
}
if (script.lines.some((l) => /buổi tối\. cả gia đình|minh vừa ăn vừa nhìn/i.test(l.text))) {
  fail.push('action leaked into Voice Script');
}
if (!script.lines.some((l) => l.text === 'Ngoắc tay.')) fail.push('lost Ngoắc tay');
if (!looksLikeScreenplayDump(golden)) fail.push('full screenplay should be rejected as TTS dump');
if (looksLikeScreenplayDump('Mẹ!')) fail.push('short dialogue rejected as dump');

const complete = verifyVoiceGeneration(
  script,
  script.lines.map((l) => ({ id: l.id, text: l.text })),
);
if (complete.status !== 'complete') fail.push(`self-verify not complete: ${complete.issues.join('; ')}`);
const extra = verifyVoiceGeneration(script, [
  ...script.lines.map((l) => ({ id: l.id, text: l.text })),
  { id: 'legacy-action', text: 'Minh chạy về.' },
]);
if (extra.status !== 'complete') fail.push('extra old TTS lines should still be COMPLETE');
const missing = verifyVoiceGeneration(script, script.lines.slice(0, 10).map((l) => ({ id: l.id, text: l.text })));
if (missing.status !== 'incomplete') fail.push('missing lines not flagged INCOMPLETE');

if (fail.length) {
  console.error('VOICE SCRIPT FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(
  `VOICE SCRIPT PASS · ${script.sourceLineCount} lines · ${script.sourceCharCount} chars · ~${script.estimatedSec}s`,
);
