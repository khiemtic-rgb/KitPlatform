export function lookStatusVi(raw?: string) {
  const s = (raw || '').toUpperCase();
  if (s === 'LOCKED') return { label: 'Đã khóa', color: 'green' as const, hint: 'Không sửa trực tiếp. Muốn đổi thì làm bản mới.' };
  if (s === 'APPROVED') return { label: 'Đã duyệt', color: 'blue' as const, hint: 'Director đã chốt. Chưa khóa.' };
  if (s === 'REVIEW') return { label: 'Chờ duyệt', color: 'orange' as const, hint: 'Đã gửi. Director đọc rồi mới duyệt.' };
  if (s === 'PROPOSED' || s === 'CREATING') return { label: 'Đề xuất', color: 'gold' as const, hint: 'Có bản thảo, chưa duyệt.' };
  return { label: 'Bản nháp', color: 'default' as const, hint: 'Đang viết. Chưa dùng để vẽ Minh.' };
}

export function lookStepIndex(input: { style?: string; dna?: string; master?: string }) {
  const done = (s?: string) => ['APPROVED', 'LOCKED'].includes((s || '').toUpperCase());
  if (!done(input.dna)) return 2;
  if (!done(input.master)) return 3;
  return 4;
}
