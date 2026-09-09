/** CHAR-001_MINH_VISUAL_DNA_V1 — ERA-01 / age 11. DRAFT for Director. Not Canon. Not LOCK. */

import { FAMIXA_VISUAL_STYLE_ID, FAMIXA_VISUAL_STYLE_TOKEN } from './content-famixa-visual-style';
import type { FamixaVisualDna } from './content-famixa-character-memory';

export const MINH_DNA_ID = 'CHAR-001_MINH_VISUAL_DNA_V1';
export const MINH_DNA_ID_LEGACY = 'FAMIXA-CHAR-001-VISUAL-DNA-V1';
export const MINH_DNA_ERA = 'ERA-01';
export const MINH_DNA_AGE = 11;
export const MINH_DNA_VERSION = 'V1';

export type FamixaDnaStatus = 'DRAFT' | 'REVIEW' | 'APPROVED';

export type FamixaVisualDnaSpec = FamixaVisualDna & {
  documentId: typeof MINH_DNA_ID;
  status: FamixaDnaStatus;
  characterId: 'CHAR-001';
  era: typeof MINH_DNA_ERA;
  age: typeof MINH_DNA_AGE;
  version: typeof MINH_DNA_VERSION;
  role: string;
  whoVisually: string;
  identityPrinciple: string;
  headShape: string;
  eyeShape: string;
  noseShape: string;
  mouthShape: string;
  hairLock: string;
  posture: string;
  emotionalSignature: string;
  wardrobeBaseline: string;
  colorPersonality: string;
  recognitionTests: string[];
  generationPriority: string[];
  signatureExpression: string;
  headToBody: string;
  silhouette: string;
  immutableTraits: string[];
  forbidden: string[];
};

export const CHAR_001_MINH_VISUAL_DNA_V1: FamixaVisualDnaSpec = {
  documentId: MINH_DNA_ID,
  status: 'DRAFT',
  characterId: 'CHAR-001',
  era: MINH_DNA_ERA,
  age: MINH_DNA_AGE,
  version: MINH_DNA_VERSION,
  role: 'Main Child / Primary Protagonist',
  visualStyle: FAMIXA_VISUAL_STYLE_TOKEN,
  whoVisually:
    'Cậu bé 11 tuổi trong gia đình hiện đại. Đời thường → dễ đồng cảm → có chiều sâu → dễ tổn thương → dễ yêu quý. “Có thể là một đứa trẻ mình từng gặp” nhưng vẫn nhận diện Famixa. Không hot boy, không trẻ QC, không anime, không photoreal, không mặt hoàn hảo.',
  identityPrinciple:
    'Stylized Cinematic Human Character — giữa photoreal và cartoon. Anatomy hợp lý, biểu cảm tự nhiên, ánh sáng điện ảnh; mặt / silhouette / tỷ lệ / rendering stylized. Không photoreal. Không cartoon.',
  headShape:
    'Mặt trẻ, mềm, hơi bầu nhưng không tròn hết. Trán tương đối rộng. Cằm nhỏ mềm. Má đầy tự nhiên tuổi 11. Không góc cạnh teen. Không baby face quá.',
  face: 'soft slightly oval child face, wide-ish forehead, small chin, natural 11yo cheeks — intelligent, sensitive, inward, vulnerable',
  eyeShape:
    'Mắt tương đối lớn nhưng không anime. Ánh nhìn rõ. Mí tự nhiên. Khoảng cách cân. Iris stylized vừa. Không glassy / AI doll. Diễn bằng ánh mắt: tò mò, thất vọng, tổn thương, sợ, giận, né, yêu, hy vọng.',
  eyes: 'relatively large natural eyes, clear gaze, not anime, not doll-glass; acting lives in the eyes',
  eyebrows: 'natural, slightly soft, not thick, not sharp — confused → worried → frustrated → angry → hurt without cartoon brows',
  noseShape: 'Nhỏ, tự nhiên, bridge mềm, không sắc, không mũi người lớn. Ổn định front / 3/4 / side.',
  nose: 'small soft child nose, stable across angles',
  mouthShape:
    'Nhỏ đến trung bình, môi tự nhiên. Không cười QC. Không quá dày/mỏng. Im lặng, mím, do dự, buồn, kìm, cười nhẹ. Subtle > exaggerated.',
  mouth: 'small-to-medium natural mouth; default quiet; no ad smile',
  hairLock:
    'Tóc đen / đen nâu, kiểu trẻ hiện đại, gọn nhưng không hoàn hảo, volume tự nhiên, lệch nhẹ được. Shape mái = Canon. AI không đổi kiểu / độ dài / chân tóc / silhouette. Không anime, idol, undercut mạnh, quá bóng, đổi kiểu giữa shot.',
  hair: 'black or dark-brown modern child hair, neat but not perfect, natural volume, slight asymmetry',
  hairStyle: 'modern child cut; silhouette locked',
  skin: 'natural soft stylized skin — no pores photoreal, no plastic, no glossy beauty filter',
  bodyProportion:
    'Tỷ lệ trẻ rõ: đầu lớn hơn người lớn, vai nhỏ, tay chân trẻ, mảnh vừa. Không cơ. Không thân teen. Không adult body + child face.',
  body: 'clear 11-year-old proportion, small shoulders, slim, not teen, not adult body',
  headToBody: 'Child proportion — larger head than adult scale. Not chibi. Not photoreal adult ratio.',
  height: 'child 11',
  build: 'slim child',
  posture:
    'Hơi khép, tự nhiên, không đứng model. Vai hơi cụp khi buồn, mở hơn khi vui. Thường: reserved. Bị trách: vai hạ, nhìn giảm, thu người. Tổn thương: đóng, ít động, tránh mắt.',
  emotionalSignature:
    'Cố giữ cảm xúc thay vì bộc lộ ngay: bị mắng → không khóc ngay → im → nhìn xuống → cố bình thường → cảm xúc lộ dần. Neutral hơi trầm. Happy nhẹ. Sad/Hurt ở mắt và posture.',
  signatureExpression: 'Holds emotion before showing it. Eyes and posture first. No instant cartoon cry.',
  wardrobeBaseline:
    'School: đồng phục sạch, đơn giản, không fashionized. Home: T-shirt + short/quần dài trẻ, màu giản dị. Đổi đồ = WARDROBE ASSET, không để model tự bịa.',
  defaultClothing: 'HOME',
  colorPersonality: 'Xanh dịu, xanh xám, beige, trắng, màu tự nhiên. Không neon, không chói, không gradient thời trang, không màu QC.',
  color: 'soft blue / grey-blue / beige / white / natural — no neon',
  distinctiveFeatures: 'gaze-acting eyes; quiet mouth; reserved posture; locked hair silhouette; 11yo proportion',
  recognitionTests: [
    'A Front → 3/4 same Minh',
    'B Neutral → Sad same Minh',
    'C School → Home clothes same Minh',
    'D Bright → Dark lighting same Minh',
    'E Close-up → Full body same Minh',
    'F ERA-01 → ERA-02 recognizable grown Minh (later)',
  ],
  generationPriority: [
    'FAMIXA VISUAL STYLE SYSTEM V1',
    'CHAR-001 VISUAL DNA V1',
    'ERA-01',
    'MASTER REFERENCE',
    'SCENE',
    'SHOT',
  ],
  silhouette: 'Đầu + tóc + vai + tỷ lệ + posture — đọc được khi bỏ quần áo. Không phụ thuộc wardrobe.',
  lineStyle: 'Stylized cinematic rendering — not photoreal, not cartoon line.',
  dimension: 'Believable volume, stylized face/identity — not 3D scan, not flat cartoon.',
  texture: 'Stylized skin/cloth. No pore-level photoreal. No plastic gloss.',
  lighting: 'Cinematic, motivated by scene. Not beauty light.',
  realism: 'Stylized cinematic human. Not photoreal child. Not cartoon.',
  emotionRead: 'Eyes first, then brows, mouth, posture, negative space. Hold before release.',
  cuteLevel: 'Everyday, not mascot-cute. Not ad-child. Depth over prettiness.',
  immutableTraits: [
    'CHAR-001 identity persists ERA-01 → ERA-02 → ERA-03',
    'Hair silhouette / hairline logic is Canon',
    'Eye identity and basic face geometry persist across eras',
    'Emotional character: holds feeling before showing it',
    'Child proportion at ERA-01 — never adult body + child face',
    'Stylized cinematic — not photoreal, anime, cartoon, idol, model',
    'Do not use Golden SH01-01 / take-01.mp4 as face lock',
  ],
  forbidden: [
    'photorealistic real child',
    'anime',
    'cartoon',
    'idol / model / hot boy',
    'superhero',
    'generic AI boy',
    'adult body with child face',
    'overly cute mascot',
    'plastic 3D',
    'đổi mặt / đổi tóc / đổi tỷ lệ / đổi tuổi',
    'thêm hoặc xóa đặc điểm nhận diện',
    'Golden Shot / take-01 as Master',
  ],
};

export function isMinhVisualDna(raw?: { documentId?: string; characterId?: string } | null) {
  const id = raw?.documentId || '';
  return (id === MINH_DNA_ID || id === MINH_DNA_ID_LEGACY) && (!raw?.characterId || raw.characterId === 'CHAR-001');
}

export function dnaReadyForMaster(dna?: { status?: string } | null) {
  const s = (dna?.status || '').toUpperCase();
  return s === 'REVIEW' || s === 'APPROVED';
}

export function dnaApproved(dna?: { status?: string } | null) {
  return (dna?.status || '').toUpperCase() === 'APPROVED';
}

export function minhDnaPromptOverride(prompt: string, dna: FamixaVisualDna = CHAR_001_MINH_VISUAL_DNA_V1) {
  const blocked: string[] = [];
  if (/blonde|nhuộm|đổi tóc|new hairstyle|undercut|idol hair/i.test(prompt)) blocked.push('PROMPT_OVERRIDE: hair silhouette is Canon');
  if (/photoreal|người thật|anime|cartoon|hot boy|idol face/i.test(prompt)) blocked.push('PROMPT_OVERRIDE: forbidden Minh look');
  if (/adult body|16 years|đổi tuổi|age 16/i.test(prompt) && dna.era === 'ERA-01') blocked.push('PROMPT_OVERRIDE: ERA-01 age 11 locked in DNA');
  return { ok: blocked.length === 0, blocked };
}

function fillMinhDnaDraft(existing: FamixaVisualDna): FamixaVisualDna {
  const spec = CHAR_001_MINH_VISUAL_DNA_V1;
  const next: FamixaVisualDna = { ...spec, ...existing, documentId: MINH_DNA_ID, status: existing.status || 'DRAFT' };
  for (const [key, value] of Object.entries(spec)) {
    const cur = (existing as Record<string, unknown>)[key];
    if (cur == null || cur === '') (next as Record<string, unknown>)[key] = value;
  }
  return next;
}

export function minhDnaCompilerLines(dna: FamixaVisualDnaSpec = CHAR_001_MINH_VISUAL_DNA_V1): FamixaVisualDna {
  return { ...dna, visualStyle: FAMIXA_VISUAL_STYLE_TOKEN, documentId: MINH_DNA_ID, status: dna.status || 'DRAFT' };
}

/** Attach DNA V1 draft. Upgrade old tuft draft. Keep Director REVIEW/APPROVED. Never LOCK / rewrite Master / Voice / Golden. */
export function mergeMinhVisualDnaDraft(canon: {
  identity: Record<string, unknown>;
  visualDna?: FamixaVisualDna;
  famixaVisualStyle?: { documentId?: string; status?: string; summary?: string };
  references?: unknown;
  voiceDna?: unknown;
  workspace?: { visualProposalStatus?: string };
}) {
  const existing = canon.visualDna;
  const keepDirected = isMinhVisualDna(existing) && existing?.documentId === MINH_DNA_ID && dnaReadyForMaster(existing);
  const keepNewDraft = isMinhVisualDna(existing) && existing?.documentId === MINH_DNA_ID;
  const dna = keepDirected
    ? existing
    : keepNewDraft
      ? fillMinhDnaDraft(existing)
      : { ...CHAR_001_MINH_VISUAL_DNA_V1, status: 'DRAFT' as const };
  return {
    ...canon,
    identity: {
      ...canon.identity,
      characterId: 'CHAR-001',
      name: 'Minh',
      role: existing?.role || CHAR_001_MINH_VISUAL_DNA_V1.role,
      currentAge: MINH_DNA_AGE,
      initialAge: MINH_DNA_AGE,
      currentEra: MINH_DNA_ERA,
    },
    visualDna: dna,
    famixaVisualStyle: {
      documentId: FAMIXA_VISUAL_STYLE_ID,
      status: canon.famixaVisualStyle?.status || 'DRAFT',
      summary: canon.famixaVisualStyle?.summary,
    },
    references: canon.references,
    voiceDna: canon.voiceDna,
    workspace: {
      ...canon.workspace,
      visualProposalStatus: canon.workspace?.visualProposalStatus || 'draft',
    },
  };
}
