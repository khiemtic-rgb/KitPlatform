/**
 * Spoken pauses for a brand that turns them on (Người Giữ Vốn).
 * Tags go only into the ElevenLabs text. Subtitles stay the original line.
 * Novixa does not call this.
 */
import { scriptScenes } from './novixa-shorts';

const BREAK_COMMA = '<break time="0.3s" />';
const BREAK_STOP = '<break time="0.6s" />';
const BREAK_SCENE = '<break time="1.2s" />';
const DECIMAL = /\d+[.,]\d+/g;

function ensureEnd(body: string, tag: string): string {
  const trimmed = body.replace(/\s+$/, '');
  const match = trimmed.match(/<break time="([0-9.]+)s" \/>$/);
  if (!match || match.index == null) return `${trimmed} ${tag}`;
  const have = Number(match[1]);
  const want = tag === BREAK_SCENE ? 1.2 : 0.6;
  if (have + 1e-9 >= want) return trimmed;
  return `${trimmed.slice(0, match.index).replace(/\s+$/, '')} ${tag}`;
}

/** One pause per punctuation. A later pause at the same spot replaces the shorter one. */
export function insertSpokenBreaks(text: string, sceneEnd: boolean, commaBreaks: boolean): string {
  const source = text.normalize('NFC').trim();
  if (!source) return source;
  const shields: string[] = [];
  const protectedText = source.replace(DECIMAL, (token) => {
    shields.push(token);
    return `\uE000${shields.length - 1}\uE001`;
  });
  let body = '';
  for (let i = 0; i < protectedText.length;) {
    if (protectedText.startsWith('<break', i)) {
      const end = protectedText.indexOf('/>', i);
      if (end < 0) {
        body += protectedText.slice(i);
        break;
      }
      body += protectedText.slice(i, end + 2);
      i = end + 2;
      continue;
    }
    const ch = protectedText[i];
    if (ch === ',' && commaBreaks) {
      body += `${ch} ${BREAK_COMMA}`;
      i += 1;
      continue;
    }
    if (ch === '.' || ch === '?' || ch === '!') {
      let j = i + 1;
      while (j < protectedText.length && (protectedText[j] === '.' || protectedText[j] === '?' || protectedText[j] === '!')) j += 1;
      body += `${protectedText.slice(i, j)} ${BREAK_STOP}`;
      i = j;
      continue;
    }
    body += ch;
    i += 1;
  }
  shields.forEach((token, index) => {
    body = body.replace(`\uE000${index}\uE001`, token);
  });
  body = body.replace(/(?:<break time="[0-9.]+s" \/>\s*){2,}/g, (block) => {
    const times = [...block.matchAll(/time="([0-9.]+)s"/g)].map((item) => Number(item[1]));
    const best = Math.max(...times);
    const tag = best >= 1.15 ? BREAK_SCENE : best >= 0.5 ? BREAK_STOP : BREAK_COMMA;
    return `${tag} `;
  });
  return ensureEnd(body, sceneEnd ? BREAK_SCENE : BREAK_STOP);
}

/** Indexes into `lines` whose sentence closes a "Cảnh N" block. */
export function sceneEndsForLines(script: string, lines: string[], spoken: (raw: string) => string): number[] {
  const scenes = scriptScenes(script.split(/\r?\n/), spoken);
  if (!scenes.length) return [];
  const full: string[] = [];
  for (const raw of script.split(/\r?\n/)) {
    const text = spoken(raw);
    if (text) full.push(text);
  }
  const ends = new Set(scenes.map((scene) => scene.to));
  const found: number[] = [];
  let cursor = 0;
  lines.forEach((line, index) => {
    while (cursor < full.length && full[cursor] !== line) cursor += 1;
    if (cursor < full.length && ends.has(cursor)) found.push(index);
    if (cursor < full.length) cursor += 1;
  });
  return found;
}
