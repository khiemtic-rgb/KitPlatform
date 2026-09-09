/** KIT Video Engine Phase 05 — real Gemini keyframe + real pixel Vision QA. No Runway. */

import { compileVisualContract, compileVisualPrompt, type KitVideoVisualContract } from './kit-video-vision';

export const KIT_VIDEO_PIXEL = 'KIT-VIDEO-PIXEL-V1';
export const FAMIXA_VISUAL_STYLE = 'FAMIXA_VISUAL_STYLE_V1';

export function famixaGoldenContract(): KitVideoVisualContract {
  return compileVisualContract({
    shotCode: 'SH01-01',
    action: 'Minh is excitedly showing his test paper to his mother.',
    characters: [
      {
        code: 'CHAR-001',
        name: 'Minh',
        version: 'V1',
        era: 'ERA-01',
        reference: '/content/famixa/canon/CHAR-001-minh-master.png',
      },
      {
        code: 'CHAR-003',
        name: 'Linh',
        version: 'V1',
        era: 'ERA-01',
        reference: '/content/famixa/canon/CHAR-003-linh-master.png',
      },
    ],
    props: [{ code: 'PROP-001', name: 'Test paper', state: 'held_by_minh' }],
    location: { code: 'LOC-001', name: 'Living room', time: 'Evening', lighting: 'Warm indoor lighting' },
    wardrobe: { 'CHAR-001': 'WARDROBE-001' },
  });
}

export function jpegMagicOk(bytes: Uint8Array): boolean {
  return bytes.length >= 3 && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff;
}

export function parseVisionJson(raw: string): {
  overall: string;
  expected: number;
  detected: number;
  ids: string[];
  uncertain: boolean;
} {
  const start = raw.indexOf('{');
  const end = raw.lastIndexOf('}');
  const json = JSON.parse(raw.slice(start, end + 1)) as {
    overall?: string;
    uncertain?: boolean;
    characters?: { expected?: number; detected?: number; ids?: string[] };
    requirements?: { status?: string }[];
  };
  const overall = (json.overall || 'FAIL').toUpperCase();
  const uncertain =
    json.uncertain === true ||
    overall === 'UNCERTAIN' ||
    (json.requirements || []).some((r) => (r.status || '').toUpperCase() === 'UNCERTAIN');
  return {
    overall,
    expected: json.characters?.expected ?? 0,
    detected: json.characters?.detected ?? 0,
    ids: json.characters?.ids ?? [],
    uncertain,
  };
}

export function pixelI2vReady(input: {
  attemptStatus: string;
  qaStatus?: string;
  allowI2v?: boolean;
  runwayCalled?: boolean;
}): { ready: boolean; runwaySubmitted: boolean; blocked: string[] } {
  const blocked: string[] = [];
  if (input.attemptStatus !== 'APPROVED') blocked.push('Director not APPROVED');
  if (input.qaStatus !== 'PASS') blocked.push('Vision QA not PASS');
  if (!input.allowI2v) blocked.push('I2V blocked');
  if (input.runwayCalled) blocked.push('Runway must not be called in Phase 05');
  return { ready: blocked.length === 0, runwaySubmitted: false, blocked };
}

export function formatPixelBoard(input: {
  shotCode: string;
  jobState: string;
  provider: string;
  model: string;
  artifactOk: boolean;
  qa?: { status: string; p0Fail: string[]; scores?: Record<string, number> } | null;
  repair?: string | null;
  i2vReady: boolean;
  runwayCalled: boolean;
  imageType?: string;
  artifactHash?: string;
  persistStatus?: string;
}): string {
  const qa = input.qa;
  const ticks = [
    `SH${input.shotCode.replace(/^SH/i, '')}`,
    '',
    'Generation',
    input.jobState !== 'FAILED' && input.jobState !== 'QUEUED' && input.jobState !== 'PERSISTENCE_FAILED' ? '✓ Gemini submitted' : '· Gemini submitted',
    input.artifactOk ? '✓ Image received' : '· Image received',
    input.artifactOk ? '✓ Artifact valid' : '· Artifact valid',
    '',
    'Vision QA',
    input.imageType === 'PRODUCTION_STILL' ? '✓ Image type PRODUCTION_STILL' : `· Image type ${input.imageType || 'UNKNOWN'}`,
    qa?.status === 'PASS' ? '✓ Characters' : '· Characters',
    qa?.status === 'PASS' ? '✓ Location' : '· Location',
    qa?.status === 'PASS' ? '✓ Props' : '· Props',
    qa?.status === 'PASS' ? '✓ Action' : '· Action',
    qa?.status === 'PASS' ? '✓ Continuity' : '· Continuity',
    '',
    `Overall: ${qa?.status || input.jobState}`,
    qa?.p0Fail?.length ? `P0: ${qa.p0Fail.join(' | ')}` : '',
    input.repair ? `Suggested repair: ${input.repair}` : '',
    input.persistStatus === 'PERSISTENCE_FAILED' ? 'PERSISTENCE_FAILED — revalidate, do not regenerate' : '',
    input.artifactHash ? `artifact_sha256 ${input.artifactHash}` : '',
    '',
    `Provider ${input.provider} · ${input.model || '(configured)'}`,
    input.i2vReady ? 'I2V READY' : 'NO I2V',
    input.runwayCalled ? 'RUNWAY CALLED — ILLEGAL IN PHASE 05' : 'RUNWAY CALLED = FALSE',
  ];
  return ticks.filter((line, i, arr) => line.length > 0 || arr[i - 1]?.length).join('\n');
}

export function inferImageType(declared?: string | null, raw?: string | null): string {
  const d = (declared || '').trim().toUpperCase().replace(/\s+/g, '_');
  const blob = `${declared || ''} ${raw || ''}`;
  if (/character[_\s-]*sheet|master reference|wardrobe grid|expression sheet|model sheet|turnaround|locked badge|famixa logo/i.test(blob)) {
    return 'CHARACTER_SHEET';
  }
  if (/reference board/i.test(blob) || d === 'REFERENCE_BOARD') return 'REFERENCE_BOARD';
  if (/storyboard/i.test(blob)) return 'INVALID_COMPOSITION';
  if (/collage/i.test(blob) || d === 'COLLAGE') return 'COLLAGE';
  if (/multi[-_ ]panel|\bpanels\b/i.test(blob) || d === 'MULTI_PANEL') return 'MULTI_PANEL';
  if (/text[-_ ]heavy|unrequested text|watermark|logo overlay/i.test(blob) || d === 'TEXT_HEAVY_IMAGE') return 'TEXT_HEAVY_IMAGE';
  if (['CHARACTER_SHEET', 'COLLAGE', 'MULTI_PANEL', 'REFERENCE_BOARD', 'TEXT_HEAVY_IMAGE', 'INVALID_COMPOSITION'].includes(d)) {
    return d;
  }
  if (d === 'PRODUCTION_STILL' || /production[_\s-]*still|cinematic still|single frame|single scene/i.test(blob)) {
    return 'PRODUCTION_STILL';
  }
  return d || 'UNKNOWN';
}

export function integrityI2vReady(input: {
  attemptStatus: string;
  qaStatus?: string;
  p0Fail?: string[];
  liveHash: string;
  storedHash: string;
  qaHash: string;
  approvedHash: string;
  imageType: string;
  artifactOk: boolean;
}): { ready: boolean; runwaySubmitted: boolean; blocked: string[] } {
  const blocked: string[] = [];
  if (!input.artifactOk) blocked.push('Artifact missing');
  if (!input.liveHash || input.liveHash !== input.storedHash) blocked.push('Artifact hash mismatch');
  if (!input.qaStatus || !input.qaHash) blocked.push('QA result missing');
  if (input.qaHash && input.qaHash !== input.liveHash) blocked.push('QA hash does not belong to artifact');
  if (input.qaStatus !== 'PASS') blocked.push('Vision QA not PASS');
  if (input.p0Fail && input.p0Fail.length) blocked.push('P0 FAIL');
  if (input.imageType !== 'PRODUCTION_STILL') blocked.push(`Image type ${input.imageType} is not PRODUCTION_STILL`);
  if (input.attemptStatus !== 'APPROVED') blocked.push('Director not APPROVED');
  if (!input.approvedHash || input.approvedHash !== input.liveHash) blocked.push('Approved hash mismatch');
  return { ready: blocked.length === 0, runwaySubmitted: false, blocked };
}

export function productionPromptHasDialogue(contract: KitVideoVisualContract, dialogue: string): boolean {
  const req = compileVisualPrompt(contract, { projectStyle: FAMIXA_VISUAL_STYLE });
  return req.hasDialogue || req.prompt.includes(dialogue);
}
