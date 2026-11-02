/** Held EP01 stills must not be shown as the new picture. */
const HELD_KEYFRAME_IDS = new Set([
  '5f9f78c2-e310-4978-a872-f9125c3453a9',
  '27872dda-69f0-4758-b1c9-4fd214b292a5',
]);

export function productionKeyframeIsDisplayable(meta: {
  empty?: boolean;
  artifactId?: string | null;
  byteSha256?: string | null;
  mimeType?: string | null;
}) {
  const id = (meta.artifactId || '').trim().toLowerCase();
  if (meta.empty || !id || !meta.byteSha256) return false;
  if (HELD_KEYFRAME_IDS.has(id)) return false;
  return (meta.mimeType || '').toLowerCase().startsWith('image/');
}

export function directorNoteWithProductionStill(note: string, hasStill: boolean) {
  if (!hasStill) return note;
  const rest = note.replace(/Chưa có hình\. Bấm Tạo hình\.\s*/g, '').trim();
  return rest ? `Hình mới đã có. ${rest}` : 'Hình mới đã có.';
}

export function productionPictureBlockCopy(reason?: string | null) {
  const code = (reason || '').trim();
  if (code.startsWith('LOCKED_CHARACTER_REFERENCE_MISSING'))
    return 'Chưa có ảnh nhân vật đã khóa cho shot này. Mở Nhân vật và khóa master trước khi tạo hình.';
  if (code === 'STALE_PICTURE_REFERENCE')
    return 'Ảnh vừa tạo chưa dùng ảnh nhân vật đã khóa. Không tính là hình hiện tại. Cần giấy phép tạo hình mới trước khi làm lại.';
  if (code === 'EP01_KEYFRAME_QUALITY_BLOCKED')
    return 'Ảnh này đang bị giữ vì chưa đạt. Không tạo video từ ảnh này.';
  if (code === 'EP01_VIDEO_QUALITY_BLOCKED')
    return 'Video hiện tại không đạt. Không đưa sang khớp môi.';
  if (code === 'MISSING_PERFORMANCE_SNAPSHOT')
    return 'Chưa lưu khoảng thoại. Bấm Lưu khoảng thoại — không gọi AI, không tính phí.';
  return '';
}
