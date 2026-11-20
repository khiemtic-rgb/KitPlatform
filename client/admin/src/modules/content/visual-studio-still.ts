/**
 * Video Studio → shared visual intelligence.
 * Search never picks an ineligible candidate. Reload is a GET, not a resolve.
 * GENERATION_REQUIRED is the only handoff to the existing paid CREATE_IMAGE confirm.
 */

export const VISUAL_PROVIDER_HANDOFF = 'GENERATION_REQUIRED';

export type VisualStudioPhase =
  | 'idle'
  | 'searching'
  | 'missing'
  | 'generating'
  | 'needs-provider'
  | 'ready'
  | 'approved'
  | 'variant';

export type VisualDeskView = {
  phase: VisualStudioPhase;
  detail: string;
  assetId?: string;
  approved?: boolean;
  undrawable?: string;
  keepPicture?: boolean;
  offerPaid?: boolean;
  decision?: string;
  searchDecision?: string;
  variantUrl?: string;
};

export type VisualShotDemand = {
  shotId: string;
  filmId: string;
  brandCode: string;
  subjectKey: string;
  action: string;
  setting: string;
  narration: string;
  pose: string;
  expression: string;
  camera: string;
  medium: 'auto' | 'photo';
  characterId?: string;
  prop: string;
  requiredLabels?: string[];
  tokens?: string[];
};

export type VisualResolvePayload = {
  decision?: string;
  assetId?: string | null;
  variantAssetId?: string | null;
  reason?: string;
  reasonCodes?: string[];
  hasImage?: boolean;
  paidProviderCalls?: number;
  providerCallAuthorized?: boolean;
  undrawable?: string;
  candidates?: { assetId?: string; eligible?: boolean; reasonCode?: string }[];
};

export type VisualShotInput = {
  id: string;
  story?: string;
  visual?: string;
  location?: string;
  characters?: string[];
  characterIds?: string[];
  actingBeat?: { before?: { action?: string; body?: string } };
};

const LIBRARY_SCENES: {
  brand: string;
  test: RegExp;
  subjectKey: string;
  action: string;
  setting: string;
  pose: string;
  expression: string;
  camera: string;
}[] = [
  {
    brand: 'novixa',
    test: /quay thuoc|pharmacy-counter|pharmacy counter|tu van thuoc/,
    subjectKey: 'pharmacy-consult',
    action: 'explain',
    setting: 'pharmacy-counter',
    pose: 'stand',
    expression: 'calm',
    camera: 'medium',
  },
  {
    brand: 'famixa',
    test: /bua sang|breakfast|kitchen/,
    subjectKey: 'family-breakfast',
    action: 'share-meal',
    setting: 'kitchen',
    pose: 'sit',
    expression: 'smile',
    camera: 'medium',
  },
  {
    brand: 'trade',
    test: /trading-desk|trading desk|ban giao dich|candle-axis/,
    subjectKey: 'trade-review',
    action: 'review',
    setting: 'trading-desk',
    pose: 'stand',
    expression: 'calm',
    camera: 'wide',
  },
];

export function normalizeFilmBrand(code: string | null | undefined): string {
  const raw = (code || '').trim().toLowerCase();
  if (!raw) return '';
  const folded = fold(raw);
  if (folded === 'hanh trinh trade' || folded === 'trade') return 'trade';
  return raw;
}

export function buildShotVisualDemand(input: {
  filmId: string;
  brandCode: string;
  shot: VisualShotInput;
  medium?: 'auto' | 'photo';
}): VisualShotDemand {
  const brandCode = normalizeFilmBrand(input.brandCode);
  const shot = input.shot;
  const actionText = [shot.actingBeat?.before?.action, shot.actingBeat?.before?.body]
    .map((part) => (part || '').trim())
    .filter(Boolean)
    .join(' ');
  const sceneText = fold([shot.location, shot.visual, actionText].filter(Boolean).join(' '));
  const profile = LIBRARY_SCENES.find((row) => row.brand === brandCode && row.test.test(sceneText));
  const setting = profile?.setting || slug(shot.location || '') || 'room';
  const action = profile?.action || slug(actionText || shot.visual || '') || 'act';
  const subjectKey = profile?.subjectKey || slug(`${setting}-${action}`) || 'scene';
  const characterId = referenceCharacter(shot);
  return {
    shotId: shot.id,
    filmId: input.filmId,
    brandCode,
    subjectKey,
    action,
    setting,
    narration: (shot.story || '').trim(),
    pose: profile?.pose || poseOf(sceneText),
    expression: profile?.expression || expressionOf(sceneText),
    camera: profile?.camera || cameraOf(sceneText),
    medium: input.medium === 'photo' ? 'photo' : 'auto',
    characterId,
    prop: '',
    requiredLabels: controlledLabels(shot.visual, shot.story),
    tokens: tokensOf(sceneText),
  };
}

function controlledLabels(visual?: string, story?: string) {
  const source = visual || '';
  const spoken = (story || '').trim();
  const found = new Set<string>();
  for (const match of source.matchAll(/[«""]([^»""\n]{1,40})[»""]/g)) {
    const label = (match[1] || '').trim();
    if (!label || label === spoken) continue;
    found.add(label);
  }
  return [...found];
}

/** Search result. NO_FIT never selects candidates[0]. */
export function afterVisualSearch(result: VisualResolvePayload): 'bind' | 'create' | 'stop' {
  if ((result.paidProviderCalls ?? 0) > 0 || result.providerCallAuthorized) return 'stop';
  const decision = result.decision || '';
  if (decision === 'REUSE' || decision === 'BOUND' || decision === 'GENERATE_LOCAL') {
    return result.hasImage && result.assetId ? 'bind' : 'stop';
  }
  if (decision === 'NO_FIT') return 'create';
  return 'stop';
}

export function interpretVisualResolve(
  result: VisualResolvePayload,
  intent: 'search' | 'create' | 'variant',
): VisualDeskView {
  const undrawable = (result.undrawable || '').trim() || undefined;
  if ((result.paidProviderCalls ?? 0) > 0) {
    return {
      phase: 'needs-provider',
      detail: 'Có cuộc gọi provider trả phí. Dừng, không lấy ảnh khác thay.',
      offerPaid: false,
      keepPicture: true,
      undrawable,
      decision: result.decision || '',
    };
  }
  const decision = result.decision || '';
  const approved = (result.reasonCodes || []).includes('APPROVED_LOCK');
  if (decision === 'REUSE' || decision === 'BOUND' || decision === 'GENERATE_LOCAL') {
    return {
      phase: approved ? 'approved' : 'ready',
      detail: result.reason || 'Đã gắn ảnh đúng cảnh với shot.',
      assetId: result.assetId || undefined,
      approved,
      undrawable,
      keepPicture: false,
      offerPaid: false,
      decision,
    };
  }
  if (decision === 'VARIANT_STORED') {
    return {
      phase: 'variant',
      detail: result.reason || 'Đã lưu biến thể mới. Ảnh đã duyệt của shot vẫn giữ nguyên.',
      assetId: result.assetId || undefined,
      approved: true,
      undrawable,
      keepPicture: true,
      offerPaid: false,
      decision,
    };
  }
  if (decision === VISUAL_PROVIDER_HANDOFF) {
    return {
      phase: 'needs-provider',
      detail: result.reason || 'Cần model ảnh chất lượng cao. Điểm gọi nằm ở đây; chưa gửi provider.',
      undrawable,
      keepPicture: true,
      offerPaid: intent !== 'search',
      decision,
    };
  }
  return {
    phase: 'missing',
    detail: result.reason || 'Không có tài nguyên đúng cảnh. Không lấy ảnh khác chủ đề.',
    undrawable,
    keepPicture: true,
    offerPaid: false,
    decision,
  };
}

function referenceCharacter(shot: VisualShotInput): string | undefined {
  const ids = (shot.characterIds || []).map((id) => id.trim()).filter(Boolean);
  if (ids.includes('CHAR-VIS-LAN')) return 'CHAR-VIS-LAN';
  if (ids.includes('CHAR-VIS-KOI')) return 'CHAR-VIS-KOI';
  if (ids.length > 0) return ids[0];
  const names = (shot.characters || []).map((name) => fold(name));
  if (names.includes('lan')) return 'CHAR-VIS-LAN';
  if (names.includes('koi')) return 'CHAR-VIS-KOI';
  return undefined;
}

function poseOf(text: string) {
  if (/ngoi|sit|viet|write/.test(text)) return /viet|write/.test(text) ? 'write' : 'sit';
  if (/buoc|walk/.test(text)) return 'walk';
  if (/\bquy\b|kneel/.test(text)) return 'kneel';
  if (/\bchi\b|point/.test(text)) return 'point';
  if (/dien thoai|phone|nghe may/.test(text)) return 'phone';
  if (/suy nghi|think/.test(text)) return 'think';
  if (/voi|reach/.test(text)) return 'reach';
  if (/tua|lean/.test(text)) return 'lean';
  return 'stand';
}

function expressionOf(text: string) {
  if (/nhay mat|wink/.test(text)) return 'wink';
  if (/ngac nhien|surprise/.test(text)) return 'surprise';
  if (/vui ve|happy/.test(text)) return 'happy';
  if (/suy nghi|think/.test(text)) return 'think';
  if (/lo lang|buon|concern/.test(text)) return 'concern';
  if (/tap trung|focus/.test(text)) return 'focus';
  if (/cuoi tuoi/.test(text)) return 'happy';
  if (/cuoi|smile/.test(text)) return 'smile';
  return 'calm';
}

function tokensOf(text: string) {
  const tokens: string[] = [];
  if (/blouse|ao blouse|ao khoac/.test(text)) tokens.push('garment:coat');
  if (/the thao|hoodie/.test(text)) tokens.push('garment:sport');
  if (/tap de|overall/.test(text)) tokens.push('garment:apron');
  if (/ngoai troi|khan/.test(text)) tokens.push('garment:outdoor');
  if (/ao gia dinh|ao do/.test(text)) tokens.push('garment:family');
  if (/sau lung|tu sau/.test(text)) tokens.push('view:back');
  else if (/nghieng|profile/.test(text)) tokens.push('view:profile');
  else if (/ba phan tu|3\/4|three-quarter/.test(text)) tokens.push('view:three-quarter');
  return tokens.length > 0 ? tokens : undefined;
}

function cameraOf(text: string) {
  if (/rong|wide/.test(text)) return 'wide';
  if (/can canh|close/.test(text)) return 'close';
  return 'medium';
}

function fold(value: string) {
  return value.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
}

function slug(value: string) {
  const folded = fold(value);
  const buffer: string[] = [];
  for (const ch of folded) {
    if (/[a-z0-9]/.test(ch)) buffer.push(ch);
    else if (buffer.length > 0 && buffer[buffer.length - 1] !== '-') buffer.push('-');
  }
  return buffer.join('').replace(/-+$/g, '').slice(0, 48);
}
