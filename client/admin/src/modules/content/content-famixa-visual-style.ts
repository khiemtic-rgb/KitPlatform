/** Compat view of FAMIXA_VISUAL_STYLE_SYSTEM_V1. System SoT = kit-video-visual-system.ts */

import { FAMIXA_VISUAL_STYLE_SYSTEM_V1 } from './kit-video-visual-system';

export const FAMIXA_VISUAL_STYLE_ID = 'FAMIXA-VISUAL-STYLE-V1';
export const FAMIXA_VISUAL_STYLE_TOKEN = 'FAMIXA_VISUAL_STYLE';

export type FamixaVisualStyleStatus = 'DRAFT' | 'REVIEW' | 'APPROVED';

export type FamixaVisualStyleDoc = {
  documentId: typeof FAMIXA_VISUAL_STYLE_ID;
  status: FamixaVisualStyleStatus;
  summary: string;
  world: string;
  recognition: string;
  line: string;
  dimension: string;
  texture: string;
  lighting: string;
  color: string;
  realism: string;
  emotion: string;
  cuteness: string;
  silhouetteLaw: string;
  notCompetingWith: string[];
  immutable: string[];
  forbidden: string[];
};

export const FAMIXA_VISUAL_STYLE_V1: FamixaVisualStyleDoc = {
  documentId: FAMIXA_VISUAL_STYLE_ID,
  status: 'DRAFT',
  summary: FAMIXA_VISUAL_STYLE_SYSTEM_V1.purpose,
  world:
    'Famixa là một vũ trụ nhân vật riêng — gia đình Việt, đời thường, cảm xúc nhỏ. Mỗi người có hình dạng đồ họa ổn định, đọc được ở thumbnail. Style thuộc studio, không thuộc máy ảnh.',
  recognition:
    'Nhìn 1 giây phải biết đây là Famixa, không phải stock kid / K-drama / catalog. Nhân vật thắng ánh sáng đẹp. Consistency thắng “ảnh đẹp một phát”.',
  line:
    'Đường nét sạch, viền hơi dày, ổn định theo shot. Không sketch lung tung, không nét fashion illustration, không ink manga dày-mỏng cực đoan.',
  dimension:
    '2.5D: có khối ở mặt–tóc–quần áo, mép vẫn graphic. Không 3D scan, không subsurface photoreal, không flat paper doll.',
  texture:
    'Sơn mịn, ít grain. Da không lỗ chân lông. Vải có nếp đơn giản. Không photo texture, không CGI skin.',
  lighting:
    'Đèn gia đình ấm, key mềm, bóng đọc được. Tối = tối phòng khách / hành lang, không beauty dish, không neon MV, không HDR.',
  color:
    'Bảng ấm–trầm: da ấm, tóc đen bóng lạnh, đồ nhà/học muted (teal-grey, cream, than). Không primary kid-bright, không grade teal-orange Hollywood.',
  realism:
    'Stylized-human: vẫn là người, không phải người thật. Cấm photoreal child lookalike. Cấm “AI người đóng”.',
  emotion:
    'Cảm xúc đi bằng mắt, miệng, vai, im lặng. Không meme mặt, không nước mắt manga, không cười quảng cáo.',
  cuteness:
    'Đáng yêu kiềm chế (khoảng 4/10). Thành thật 7/10. Không kawaii, không em bé quảng cáo sữa.',
  silhouetteLaw:
    'Mỗi nhân vật phải đọc được ở hình đen. Đầu, tóc, tỷ lệ, dáng đứng là khóa. Không đổi silhouette giữa shot.',
  notCompetingWith: [
    'photoreal AI live-action',
    'K-drama / idol lighting',
    'kid-model catalog',
    'cartoon comedy',
    'shonen / shoujo anime',
    'pixar-cute',
    'generic any-boy stock',
  ],
  immutable: [
    'Designed character, not a photographed child',
    '2.5D graphic volume',
    'Warm family light, never beauty light',
    'Silhouette-first recognition',
    'Emotion through eyes and posture, not gag faces',
  ],
  forbidden: [
    'photoreal skin / pores / lens',
    'real-child likeness',
    'anime eye sparkle / speed lines',
    'cartoon squash-stretch comedy',
    'text, watermark, character sheet collage',
    'fashion crop, beauty retouch',
    'đổi tỷ lệ đầu-thân giữa các shot',
  ],
};

/** Compat token used by older Canon / compiler fields. */
export const FAMIXA_VISUAL_STYLE = {
  cinematic: true,
  stylizedHuman: true,
  emotional: true,
  naturalProportions: false,
  subtleStylization: true,
  notCartoonComedy: true,
  notAnime: true,
  notPhotoreal: true,
  designedCharacter: true,
  summary: FAMIXA_VISUAL_STYLE_V1.summary,
} as const;

export function isFamixaVisualStyleDoc(raw?: { documentId?: string } | null) {
  return raw?.documentId === FAMIXA_VISUAL_STYLE_ID;
}

export function styleReadyForMaster(style?: { status?: string } | null) {
  const s = (style?.status || '').toUpperCase();
  return s === 'REVIEW' || s === 'APPROVED';
}

export function styleApproved(style?: { status?: string } | null) {
  return (style?.status || '').toUpperCase() === 'APPROVED';
}
