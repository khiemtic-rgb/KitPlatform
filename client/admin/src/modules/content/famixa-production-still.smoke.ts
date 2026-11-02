import { directorNoteWithProductionStill, productionKeyframeIsDisplayable, productionPictureBlockCopy } from './famixa-production-still';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(productionKeyframeIsDisplayable({
  empty: false,
  artifactId: 'd3409bf8-07d0-4298-bdcc-f81d7985c4f0',
  byteSha256: 'abc',
  mimeType: 'image/png',
}), 'new still is displayable');
ok(!productionKeyframeIsDisplayable({
  empty: false,
  artifactId: '5f9f78c2-e310-4978-a872-f9125c3453a9',
  byteSha256: 'abc',
  mimeType: 'image/png',
}), 'held SH01 still stays hidden');
ok(!productionKeyframeIsDisplayable({
  empty: false,
  artifactId: '27872dda-69f0-4758-b1c9-4fd214b292a5',
  byteSha256: 'abc',
  mimeType: 'image/jpeg',
}), 'held SH02 still stays hidden');
ok(directorNoteWithProductionStill('Chưa có hình. Bấm Tạo hình. CREATE_VIDEO · MISSING', true)
  .startsWith('Hình mới đã có.'), 'missing-picture note is replaced');
ok(productionPictureBlockCopy('STALE_PICTURE_REFERENCE').includes('chưa dùng ảnh nhân vật đã khóa'), 'stale still is not current');
ok(productionPictureBlockCopy('LOCKED_CHARACTER_REFERENCE_MISSING').includes('khóa master'), 'missing lock names the next action');
ok(productionPictureBlockCopy('EP01_KEYFRAME_QUALITY_BLOCKED').includes('Không tạo video'), 'held still is not a video source');
ok(productionPictureBlockCopy('EP01_VIDEO_QUALITY_BLOCKED').includes('Không đưa sang khớp môi'), 'held video is not a lipsync source');
ok(productionPictureBlockCopy('MISSING_PERFORMANCE_SNAPSHOT').includes('Lưu khoảng thoại'), 'missing timing names the free action');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('famixa-production-still smoke PASS');
