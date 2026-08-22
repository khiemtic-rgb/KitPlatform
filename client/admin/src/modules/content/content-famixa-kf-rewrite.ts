/** User complaint → continuity instruction. Does not rewrite Action / dialogue / story. */

export type KfRewriteFlags = {
  place: boolean;
  lighting: boolean;
  wardrobe: boolean;
  camera: boolean;
  inherit: boolean;
};

export type KfRewrite = KfRewriteFlags & {
  instruction: string;
  source: 'local' | 'ai';
};

const BANNED =
  /ôm|xin lỗi|bài học|hạnh phúc|tha thứ|đỏ mặt|chạy sang|phòng khách|phòng ngủ|nhà bếp khác|thêm nhân vật|đổi thoại/i;

function clip(s: string, n: number) {
  return s.replace(/\s+/g, ' ').trim().slice(0, n);
}

export function emptyKfRewrite(): KfRewrite {
  return { place: false, lighting: false, wardrobe: false, camera: false, inherit: true, instruction: '', source: 'local' };
}

export function compileKfRewrite(userNote: string, ctx?: { action?: string; location?: string }): KfRewrite {
  const raw = clip(userNote, 400);
  const flags: KfRewriteFlags = {
    place: /phòng|chỗ|bối cảnh|bàn|cửa sổ|nội thất|khác nơi|sai chỗ|ban ngày|ngoài|trong nhà/i.test(raw),
    lighting: /sáng|đèn|ban ngày|ban đêm|tối|chiều|ấm|lạnh|nắng|đêm/i.test(raw),
    wardrobe: /áo|quần|tóc|mặt|quần áo|đồ/i.test(raw),
    camera: /góc|camera|máy|khung|crop|cận|rộng/i.test(raw),
    inherit: !/không kế thừa|bỏ shot trước|cảnh mới hoàn toàn/i.test(raw),
  };
  if (!raw) {
    return { ...emptyKfRewrite(), inherit: true, instruction: 'Giữ KF shot trước. Cùng chỗ, áo, đèn, camera. Chỉ đổi Action của Short này.' };
  }
  const bits: string[] = [];
  if (flags.inherit) bits.push('Kế thừa khung cuối shot trước.');
  if (flags.place) bits.push(`Cùng chỗ${ctx?.location ? ` (${clip(ctx.location, 60)})` : ''}. Không đổi phòng nếu script không nói.`);
  if (flags.lighting) bits.push(/đêm|tối|ấm/i.test(raw) ? 'Ánh sáng đêm / tone ấm. Không ban ngày.' : 'Giữ đúng đèn của shot trước.');
  if (flags.wardrobe) bits.push('Giữ mặt/tóc/áo Canon. Không đổi đồ.');
  if (flags.camera) bits.push('Giữ hướng máy; chỉ crop nếu user nói cận/rộng.');
  bits.push('Chỉ áp Action đã khóa. Không thêm sự kiện, thoại, nhân vật.');
  if (/vẽ cái gì|vẽ gì|sai cảnh|không đúng|không làm theo|khoe điểm|sheet|canon|bảng mặt|turnaround/i.test(raw)) {
    bits.push('Một khung phim live-action của Action. Không vẽ Character Canon, turnaround, expression grid, title card.');
  }
  if (ctx?.action) bits.push(`Action giữ: ${clip(ctx.action, 80)}.`);
  return { ...flags, instruction: clip(bits.join(' '), 280), source: 'local' };
}

export function sanitizeKfRewrite(raw: Partial<KfRewrite> | null | undefined, fallback: KfRewrite): KfRewrite {
  const instruction = clip(String(raw?.instruction ?? fallback.instruction), 280);
  const safe = BANNED.test(instruction) ? fallback.instruction : instruction;
  return {
    place: Boolean(raw?.place ?? fallback.place),
    lighting: Boolean(raw?.lighting ?? fallback.lighting),
    wardrobe: Boolean(raw?.wardrobe ?? fallback.wardrobe),
    camera: Boolean(raw?.camera ?? fallback.camera),
    inherit: raw?.inherit === false ? false : true,
    instruction: safe || fallback.instruction,
    source: raw?.source === 'ai' ? 'ai' : fallback.source,
  };
}

export function parseKfRewriteJson(text: string, fallback: KfRewrite): KfRewrite {
  const t = (text ?? '').trim().replace(/^```(?:json)?\s*|\s*```$/g, '');
  try {
    const row = JSON.parse(t) as Partial<KfRewrite>;
    return sanitizeKfRewrite({ ...row, source: 'ai' }, fallback);
  } catch {
    return fallback;
  }
}

export function stillContinuityLine(note?: KfRewrite) {
  const line = clip(note?.instruction ?? '', 280);
  if (!line) return '';
  return `Continuity lock (operator): ${line} Do not change the story action or add people.`;
}
