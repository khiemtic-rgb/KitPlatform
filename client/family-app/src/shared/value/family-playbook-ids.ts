/**
 * Famixa Playbook starter IDs — Wave B stub (not a CMS).
 * Map gradually onto CoachInsight proposalCode / AFE proposals.
 */
export const FAMILY_PLAYBOOK_STARTER = [
  { id: 'PB0001', trigger: 'Con quên đánh răng', domain: 'habit_brush' },
  { id: 'PB0002', trigger: 'Quên chuẩn bị cặp sáng', domain: 'habit_pack' },
  { id: 'PB0003', trigger: 'Ngủ muộn lặp', domain: 'sleep' },
  { id: 'PB0004', trigger: 'Bỏ qua đọc sách', domain: 'learning' },
  { id: 'PB0005', trigger: 'Quá nhiều lần nhắc phụ huynh', domain: 'peace' },
  { id: 'PB0006', trigger: 'Việc quá giờ buổi tối', domain: 'twin_evening' },
  { id: 'PB0007', trigger: 'Con báo xong — chờ duyệt', domain: 'awaiting' },
  { id: 'PB0008', trigger: 'Pattern quên ≥3/7 ngày', domain: 'coach_insight' },
  { id: 'PB0009', trigger: 'Stage teen — tránh giọng trẻ nhỏ', domain: 'stage' },
  { id: 'PB0010', trigger: 'Values Reading → ưu tiên đọc', domain: 'values' },
  { id: 'PB0011', trigger: 'Focus tự lập / ít nhắc', domain: 'goals' },
  { id: 'PB0012', trigger: 'School bubble competitive', domain: 'context' },
  { id: 'PB0013', trigger: 'Self-view lệch peer shock', domain: 'calibration' },
  { id: 'PB0014', trigger: 'Growth Balance — tránh dễ hư', domain: 'growth_balance' },
  { id: 'PB0015', trigger: 'Growth Balance — thiếu phấn đấu', domain: 'growth_balance' },
  { id: 'PB0016', trigger: 'Movie Night / team incomplete', domain: 'team' },
  { id: 'PB0017', trigger: 'Skip lý do forgot', domain: 'skip' },
  { id: 'PB0018', trigger: 'Optional task done đúng giờ', domain: 'strength' },
  { id: 'PB0019', trigger: 'Illusion risk hit', domain: 'illusion' },
  { id: 'PB0020', trigger: 'Chưa hydrate Blueprint', domain: 'sparse' },
] as const;

export type FamilyPlaybookId = (typeof FAMILY_PLAYBOOK_STARTER)[number]['id'];

const PROPOSAL = {
  suggest_move_after_dinner: 'suggest_move_after_dinner',
  suggest_move_after_school: 'suggest_move_after_school',
  open_today: 'open_today',
  support_overdue: 'support_overdue',
} as const;

function looksBrush(title?: string | null): boolean {
  const t = (title ?? '').toLowerCase();
  return t.includes('đánh răng') || t.includes('danh rang') || t.includes('brush');
}

function looksPack(title?: string | null): boolean {
  const t = (title ?? '').toLowerCase();
  return (
    t.includes('cặp') ||
    t.includes('balo') ||
    t.includes('chuẩn bị') ||
    t.includes('chuan bi') ||
    t.includes('đồng phục')
  );
}

function looksSleep(title?: string | null): boolean {
  const t = (title ?? '').toLowerCase();
  return t.includes('ngủ') || t.includes('ngu ');
}

function looksRead(title?: string | null): boolean {
  const t = (title ?? '').toLowerCase();
  return t.includes('đọc') || t.includes('sách');
}

/**
 * Resolve stable Playbook ID from CoachInsight / Brief signals.
 * Habit/pattern wins over sparse; sparse (PB0020) only when no other signal.
 */
export function resolvePlaybookId(input: {
  blueprintSparse?: boolean;
  proposalCode?: string | null;
  focusTitle?: string | null;
  patternForgotCount?: number;
  skipReasonForgot?: boolean;
  awaitingCheck?: boolean;
}): FamilyPlaybookId | null {
  if (input.awaitingCheck) return 'PB0007';

  const title = input.focusTitle;
  const forgot = (input.patternForgotCount ?? 0) >= 3;
  if (forgot) {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
    if (looksSleep(title)) return 'PB0003';
    if (looksRead(title)) return 'PB0004';
    return 'PB0008';
  }

  if (input.skipReasonForgot) {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
    return 'PB0017';
  }

  const code = (input.proposalCode ?? '').trim();
  if (
    code === PROPOSAL.suggest_move_after_dinner ||
    code === PROPOSAL.suggest_move_after_school
  ) {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
    return 'PB0008';
  }
  if (code === PROPOSAL.support_overdue) return 'PB0006';
  if (code === PROPOSAL.open_today) {
    if (looksBrush(title)) return 'PB0001';
    if (looksPack(title)) return 'PB0002';
  }

  if (looksBrush(title)) return 'PB0001';
  if (looksPack(title)) return 'PB0002';
  if (looksSleep(title)) return 'PB0003';
  if (looksRead(title)) return 'PB0004';

  if (input.blueprintSparse) return 'PB0020';
  return null;
}
