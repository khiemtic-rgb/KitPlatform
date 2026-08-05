import type { CommitmentTemplateDto } from '@/shared/api/family-os.api';

export type RoutineFocusKey = 'all' | string;

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

/** Cả nhà = tất cả; từng con = việc của con + việc chung (null). */
export function templateVisibleForFocus(
  t: CommitmentTemplateDto,
  focus: RoutineFocusKey,
): boolean {
  if (focus === 'all') return true;
  return !t.memberId || t.memberId === focus;
}

export function countActiveTemplatesForFocus(
  templates: CommitmentTemplateDto[],
  focus: RoutineFocusKey,
): number {
  return templates.filter((t) => t.isActive && templateVisibleForFocus(t, focus)).length;
}

/** Hydrate focus from storage / query — chỉ giữ `all` hoặc id có trong danh sách con. */
export function sanitizeRoutineFocus(
  raw: string | null | undefined,
  childIds: readonly string[],
): RoutineFocusKey {
  if (!raw || raw === 'all') return 'all';
  if (!UUID_RE.test(raw)) return 'all';
  if (childIds.length === 0) return raw;
  return childIds.includes(raw) ? raw : 'all';
}
