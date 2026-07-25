import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Button,
  Checkbox,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Switch,
  Table,
  TimePicker,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  StarFilled,
  StarOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  addCommitmentTemplate,
  fetchFamilies,
  fetchFamilyRoutines,
  removeCommitmentTemplate,
  updateCommitmentTemplate,
  updateFamilyRoutine,
  type CommitmentTemplate,
  type FamilyMembership,
  type FamilyRoutine,
  type FamilySummary,
} from '@/shared/api/family-os.api';
import './family-os-routines.css';

const WEEKDAY_OPTIONS = [
  { label: 'T2', value: 1 },
  { label: 'T3', value: 2 },
  { label: 'T4', value: 3 },
  { label: 'T5', value: 4 },
  { label: 'T6', value: 5 },
  { label: 'T7', value: 6 },
  { label: 'CN', value: 7 },
];

const KIND_OPTIONS = [
  { value: 'school_day', label: 'Ngày đi học' },
  { value: 'weekend', label: 'Cuối tuần' },
  { value: 'holiday', label: 'Nghỉ lễ' },
  { value: 'exam', label: 'Thi cử' },
  { value: 'travel', label: 'Du lịch' },
  { value: 'custom', label: 'Tuỳ chỉnh' },
];

const PRIORITY_OPTIONS = [
  { value: 'critical', label: 'Quan trọng' },
  { value: 'normal', label: 'Bình thường' },
  { value: 'optional', label: 'Tuỳ chọn' },
];

const CONTEXT_ANCHOR_OPTIONS = [
  { value: 'after_wake', label: 'Sau khi dậy' },
  { value: 'before_breakfast', label: 'Trước ăn sáng' },
  { value: 'after_breakfast', label: 'Sau ăn sáng' },
  { value: 'before_school', label: 'Trước giờ đi học' },
  { value: 'after_school', label: 'Sau giờ học' },
  { value: 'before_dinner', label: 'Trước ăn tối' },
  { value: 'after_dinner', label: 'Sau ăn tối' },
  { value: 'before_sleep', label: 'Trước khi ngủ' },
];

type RoutineFormValues = {
  displayName: string;
  kind: string;
  weekdays: number[];
  isActive: boolean;
};

type TemplateFormValues = {
  title: string;
  description?: string;
  memberId?: string | null;
  window?: [Dayjs | null, Dayjs | null] | null;
  sortOrder: number;
  isActive: boolean;
  priority: string;
  contextAnchor?: string | null;
  dependsOnTemplateIds?: string[];
  allowEarlyComplete?: boolean;
  earlyLeadMinutes?: number | null;
  onTimeGraceMinutes?: number | null;
  starReward?: number | null;
};

function anchorLabel(code?: string) {
  return CONTEXT_ANCHOR_OPTIONS.find((o) => o.value === code)?.label ?? code ?? '—';
}

function toTimeOnly(value?: string): Dayjs | null {
  if (!value) return null;
  const slice = value.length >= 5 ? value.slice(0, 5) : value;
  const [h, m] = slice.split(':').map((part) => Number(part));
  if (!Number.isFinite(h) || !Number.isFinite(m)) return null;
  return dayjs().hour(h).minute(m).second(0).millisecond(0);
}

function fromTimeOnly(value?: Dayjs | null): string | null {
  if (!value || !value.isValid()) return null;
  return value.format('HH:mm:ss');
}

function formatWindow(start?: string, end?: string): string {
  const clean = (v?: string) => (v ? v.slice(0, 5) : '');
  if (start && end) return `${clean(start)} – ${clean(end)}`;
  return clean(start || end) || '—';
}

function inferStarReward(title: string): number {
  const t = title.trim().toLowerCase();
  if (t.includes('bài') || t.includes('học') || t.includes('toán')) return 20;
  if (t.includes('ngủ') || t.includes('đánh răng')) return 15;
  return 10;
}

function inferAllowEarlyComplete(title: string): boolean {
  const t = title.trim().toLowerCase();
  if (
    t.includes('đọc') ||
    t.includes('sách') ||
    t.includes('kể chuyện') ||
    t.includes('thể dục') ||
    t.includes('vận động') ||
    t.includes('chạy bộ') ||
    t.includes('bơi')
  )
    return true;
  return false;
}

function inferEarlyLeadMinutes(title: string): number {
  const t = title.trim().toLowerCase();
  if (
    t.includes('đọc') ||
    t.includes('sách') ||
    t.includes('kể chuyện') ||
    t.includes('thể dục') ||
    t.includes('vận động')
  )
    return 0;
  return 0;
}

function inferOnTimeGraceMinutes(title: string): number {
  const t = title.trim().toLowerCase();
  if (
    t.includes('đọc') ||
    t.includes('sách') ||
    t.includes('thể dục') ||
    t.includes('vận động') ||
    t.includes('bài') ||
    t.includes('học') ||
    t.includes('toán')
  )
    return 10;
  return 0;
}

function taskIcon(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('dậy') || t.includes('thức')) return '☀️';
  if (t.includes('đánh răng') || t.includes('rang')) return '🪥';
  if (t.includes('ăn') || t.includes('cơm') || t.includes('bữa')) return '🥣';
  if (t.includes('đồng phục') || t.includes('mặc')) return '👕';
  if (t.includes('cặp') || t.includes('balo')) return '🎒';
  if (t.includes('bài tập') || t.includes('học')) return '📘';
  if (t.includes('đọc') || t.includes('sách')) return '📖';
  if (t.includes('tắm') || t.includes('rửa')) return '🚿';
  if (t.includes('ngủ')) return '🌙';
  if (t.includes('dọn') || t.includes('phòng')) return '🧹';
  return '⭐';
}

function priorityStars(priority?: string) {
  const filled = priority === 'critical' ? 3 : priority === 'optional' ? 1 : 2;
  return (
    <span className="fr-stars" title={PRIORITY_OPTIONS.find((p) => p.value === priority)?.label}>
      {[1, 2, 3].map((i) =>
        i <= filled ? (
          <StarFilled key={i} className="is-on" />
        ) : (
          <StarOutlined key={i} className="is-off" />
        ),
      )}
    </span>
  );
}

function statusPill(row: CommitmentTemplate) {
  if (!row.isActive) return <span className="fr-pill is-off">Tắt</span>;
  if (row.priority === 'optional') return <span className="fr-pill is-optional">Tùy chọn</span>;
  return <span className="fr-pill is-required">Bắt buộc</span>;
}

function weekdayLabel(days: number[]) {
  if (!days.length) return 'không cố định';
  return days
    .map((d) => WEEKDAY_OPTIONS.find((w) => w.value === d)?.label ?? d)
    .join(', ');
}

function groupTemplatesByMember(
  templates: CommitmentTemplate[],
  names: Map<string, string>,
): { key: string; label: string; items: CommitmentTemplate[] }[] {
  const sorted = [...templates].sort((a, b) => a.sortOrder - b.sortOrder);
  const order: string[] = [];
  const buckets = new Map<string, CommitmentTemplate[]>();

  for (const t of sorted) {
    const key = t.memberId ?? '__house__';
    if (!buckets.has(key)) {
      buckets.set(key, []);
      order.push(key);
    }
    buckets.get(key)!.push(t);
  }

  order.sort((a, b) => {
    if (a === '__house__') return 1;
    if (b === '__house__') return -1;
    const na = names.get(a) ?? a;
    const nb = names.get(b) ?? b;
    return na.localeCompare(nb, 'vi');
  });

  return order.map((key) => ({
    key,
    label: key === '__house__' ? 'Cả nhà' : names.get(key) ?? 'Thành viên',
    items: buckets.get(key) ?? [],
  }));
}

/** Always list every child on the routine — Team Play / sibling visibility. */
function buildRoutineMemberGroups(
  templates: CommitmentTemplate[],
  children: { id: string; displayName: string }[],
  names: Map<string, string>,
): { key: string; label: string; items: CommitmentTemplate[] }[] {
  const fromFlow = groupTemplatesByMember(templates, names);
  const byKey = new Map(fromFlow.map((g) => [g.key, g]));
  const result = children.map((ch) => {
    const hit = byKey.get(ch.id);
    byKey.delete(ch.id);
    return hit
      ? { ...hit, label: ch.displayName || hit.label }
      : { key: ch.id, label: ch.displayName, items: [] as CommitmentTemplate[] };
  });
  for (const g of byKey.values()) {
    result.push(g);
  }
  return result;
}

function routineStats(templates: CommitmentTemplate[]) {
  const totalMinutes = templates.reduce((sum, t) => sum + (t.expectedDurationMinutes ?? 0), 0);
  const highPriority = templates.filter((t) => t.priority === 'critical').length;
  const studyish = templates.filter((t) => {
    const title = t.title.toLowerCase();
    const start = t.windowStart?.slice(0, 5) ?? '';
    return (
      title.includes('bài') ||
      title.includes('học') ||
      title.includes('đọc') ||
      (start && start >= '16:00')
    );
  });
  const starts = studyish
    .map((t) => t.windowStart?.slice(0, 5))
    .filter(Boolean)
    .sort() as string[];
  const ends = studyish
    .map((t) => t.windowEnd?.slice(0, 5))
    .filter(Boolean)
    .sort() as string[];
  const studyWindow =
    starts.length && ends.length ? `${starts[0]} – ${ends[ends.length - 1]}` : '—';
  return {
    totalMinutes,
    count: templates.length,
    highPriority,
    studyWindow,
  };
}

function memberEmoji(name: string): string {
  const n = name.toLowerCase();
  if (n.includes('nhi') || n.includes('linh') || n === 'mẹ') return '👧';
  if (n.includes('huy') || n.includes('đức') || n === 'bố') return '👦';
  return '🧒';
}

function activeTone(kind: string): string {
  if (kind === 'weekend' || kind === 'holiday') return 'is-warm';
  return 'is-green';
}

export function FamilyOsRoutinesPage() {
  const { modal, message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [routines, setRoutines] = useState<FamilyRoutine[]>([]);
  const [editingRoutine, setEditingRoutine] = useState<FamilyRoutine | null>(null);
  const [routineOpen, setRoutineOpen] = useState(false);
  const [templateOpen, setTemplateOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<CommitmentTemplate | null>(null);
  const [templateRoutine, setTemplateRoutine] = useState<FamilyRoutine | null>(null);
  const [routineForm] = Form.useForm<RoutineFormValues>();
  const [templateForm] = Form.useForm<TemplateFormValues>();
  const templateWindow = Form.useWatch('window', templateForm);
  const templateDurationMinutes = useMemo(() => {
    const [start, end] = templateWindow ?? [];
    if (!start?.isValid() || !end?.isValid()) return null;
    const minutes = end.diff(start, 'minute');
    return minutes > 0 ? minutes : null;
  }, [templateWindow]);
  /** null = cả đội; id = chỉ xem con đó */
  const [focusChildId, setFocusChildId] = useState<string | null>(null);

  const members = family?.members ?? [];
  const childMembers = useMemo(() => {
    const kids = members
      .filter((m) => m.roleCode === 'child' && m.status === 'active')
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
    // Deduplicate by id, then by normalized name (keep first / lower sort).
    const byId = new Map<string, (typeof kids)[0]>();
    for (const k of kids) byId.set(k.id, k);
    const unique = [...byId.values()];
    const seenName = new Set<string>();
    return unique.filter((k) => {
      const key = k.displayName.trim().toLowerCase();
      if (seenName.has(key)) return false;
      seenName.add(key);
      return true;
    });
  }, [members]);
  const memberName = useMemo(() => {
    const map = new Map<string, string>();
    for (const m of members) map.set(m.id, m.displayName);
    return map;
  }, [members]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setRoutines([]);
        return;
      }
      setRoutines(await fetchFamilyRoutines(first.id));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được routine'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openRoutineEdit = (routine: FamilyRoutine) => {
    setEditingRoutine(routine);
    routineForm.setFieldsValue({
      displayName: routine.displayName,
      kind: routine.kind,
      weekdays: routine.weekdays,
      isActive: routine.isActive,
    });
    setRoutineOpen(true);
  };

  const saveRoutine = async () => {
    if (!family || !editingRoutine) return;
    const values = await routineForm.validateFields();
    setSaving(true);
    try {
      await updateFamilyRoutine(family.id, editingRoutine.id, {
        displayName: values.displayName,
        kind: values.kind,
        weekdays: values.weekdays ?? [],
        isActive: values.isActive,
      });
      message.success('Đã lưu routine');
      setRoutineOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được routine'));
    } finally {
      setSaving(false);
    }
  };

  const openTemplateCreate = (routine: FamilyRoutine) => {
    setTemplateRoutine(routine);
    setEditingTemplate(null);
    const nextOrder =
      routine.templates.reduce((max, t) => Math.max(max, t.sortOrder), 0) + 10;
    templateForm.setFieldsValue({
      title: '',
      description: '',
      memberId: undefined,
      window: undefined,
      sortOrder: nextOrder,
      isActive: true,
      priority: 'normal',
      contextAnchor: null,
      dependsOnTemplateIds: [],
      allowEarlyComplete: false,
      earlyLeadMinutes: null,
      onTimeGraceMinutes: null,
      starReward: null,
    });
    setTemplateOpen(true);
  };

  const openTemplateEdit = (routine: FamilyRoutine, template: CommitmentTemplate) => {
    setTemplateRoutine(routine);
    setEditingTemplate(template);
    templateForm.setFieldsValue({
      title: template.title,
      description: template.description ?? '',
      memberId: template.memberId ?? null,
      window: [toTimeOnly(template.windowStart), toTimeOnly(template.windowEnd)],
      sortOrder: template.sortOrder,
      isActive: template.isActive,
      priority: template.priority || 'normal',
      contextAnchor: template.contextAnchor ?? null,
      dependsOnTemplateIds: template.dependsOnTemplateIds ?? [],
      allowEarlyComplete:
        template.allowEarlyComplete ?? inferAllowEarlyComplete(template.title),
      earlyLeadMinutes: template.earlyLeadMinutes ?? inferEarlyLeadMinutes(template.title),
      onTimeGraceMinutes:
        template.onTimeGraceMinutes ?? inferOnTimeGraceMinutes(template.title),
      starReward: template.starReward ?? inferStarReward(template.title),
    });
    setTemplateOpen(true);
  };

  const saveTemplate = async () => {
    if (!family || !templateRoutine) return;
    const values = await templateForm.validateFields();
    const payload = {
      title: values.title.trim(),
      description: values.description?.trim() || null,
      memberId: values.memberId || null,
      windowStart: fromTimeOnly(values.window?.[0]),
      windowEnd: fromTimeOnly(values.window?.[1]),
      sortOrder: values.sortOrder,
      isActive: values.isActive,
      priority: values.priority || 'normal',
      expectedDurationMinutes: null,
      contextAnchor: values.contextAnchor || null,
      dependsOnTemplateIds: values.dependsOnTemplateIds ?? [],
      allowEarlyComplete: values.allowEarlyComplete ?? null,
      earlyLeadMinutes: values.earlyLeadMinutes ?? null,
      onTimeGraceMinutes: values.onTimeGraceMinutes ?? null,
      starReward: values.starReward ?? null,
    };
    setSaving(true);
    try {
      if (editingTemplate) {
        await updateCommitmentTemplate(
          family.id,
          templateRoutine.id,
          editingTemplate.id,
          payload,
        );
        message.success('Đã cập nhật cam kết');
      } else {
        await addCommitmentTemplate(family.id, templateRoutine.id, payload);
        message.success('Đã thêm cam kết');
      }
      setTemplateOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được cam kết'));
    } finally {
      setSaving(false);
    }
  };

  const confirmRemoveTemplate = (routine: FamilyRoutine, template: CommitmentTemplate) => {
    if (!family) {
      message.warning('Chưa chọn gia đình — tải lại trang rồi thử xóa.');
      return;
    }
    modal.confirm({
      title: 'Xóa cam kết này?',
      content: `"${template.title}" sẽ không còn trong các Daily Flow mới. Ngày hôm nay (nếu đã tạo) không bị đổi.`,
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Huỷ',
      centered: true,
      onOk: async () => {
        try {
          await removeCommitmentTemplate(family.id, routine.id, template.id);
          message.success('Đã xóa cam kết');
          await load();
        } catch (error) {
          message.error(apiErrorMessage(error, 'Không xóa được'));
          return Promise.reject(error);
        }
      },
    });
  };

  const templateColumns = (routine: FamilyRoutine): ColumnsType<CommitmentTemplate> => [
    {
      title: 'Cam kết',
      dataIndex: 'title',
      render: (title: string, row) => (
        <div className="fr-commit">
          <span className="fr-commit-icon" aria-hidden>
            {taskIcon(title)}
          </span>
          <div>
            <strong>{title}</strong>
            {row.description ? <span>{row.description}</span> : null}
            {row.dependsOnTemplateIds?.length ? (
              <em>
                Sau:{' '}
                {row.dependsOnTemplateIds
                  .map((id) => routine.templates.find((t) => t.id === id)?.title ?? '…')
                  .join(', ')}
              </em>
            ) : null}
          </div>
        </div>
      ),
    },
    {
      title: 'Quan hệ',
      width: 140,
      render: (_, row) =>
        row.contextAnchor ? (
          <span className="fr-rel">{anchorLabel(row.contextAnchor)}</span>
        ) : (
          '—'
        ),
    },
    {
      title: 'Ưu tiên',
      width: 100,
      render: (_, row) => priorityStars(row.priority),
    },
    {
      title: 'Sao',
      width: 72,
      render: (_, row) => (
        <span title="star_reward khi hoàn thành đúng giờ">
          {row.starReward ?? inferStarReward(row.title)}⭐
        </span>
      ),
    },
    {
      title: 'Timing',
      width: 120,
      render: (_, row) => (
        <span title="allow_early · early_lead · on_time_grace">
          {row.allowEarlyComplete ? 'Sớm ✓' : 'Đúng giờ'}
          {row.earlyLeadMinutes ? ` · −${row.earlyLeadMinutes}′` : ''}
          {row.onTimeGraceMinutes ? ` · +${row.onTimeGraceMinutes}′` : ''}
        </span>
      ),
    },
    {
      title: 'Thời lượng',
      width: 100,
      render: (_, row) =>
        row.expectedDurationMinutes != null ? `${row.expectedDurationMinutes} phút` : '—',
    },
    {
      title: 'Khung giờ gợi ý',
      width: 130,
      render: (_, row) => formatWindow(row.windowStart, row.windowEnd),
    },
    {
      title: 'Thứ tự',
      dataIndex: 'sortOrder',
      width: 80,
    },
    {
      title: 'Trạng thái',
      width: 110,
      render: (_, row) => statusPill(row),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 100,
      align: 'right',
      render: (_, row) => (
        <div className="fr-actions">
          <button
            type="button"
            className="fr-icon-btn"
            aria-label={`Sửa ${row.title}`}
            onClick={() => openTemplateEdit(routine, row)}
          >
            <EditOutlined />
          </button>
          <button
            type="button"
            className="fr-icon-btn is-danger"
            aria-label={`Xóa ${row.title}`}
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              confirmRemoveTemplate(routine, row);
            }}
          >
            <DeleteOutlined />
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className={`fr-page${loading ? ' is-loading' : ''}`}>
      <header className="fr-header">
        <div>
          <h1>
            Routine <span aria-hidden>📅</span>
          </h1>
          <p>
            {family
              ? `${family.displayName} — Sửa mẫu nhịp sống & cam kết`
              : 'Chưa có gia đình'}
          </p>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Làm mới
        </Button>
      </header>

      <section className="fr-banner">
        <div className="fr-banner-copy">
          <span className="fr-banner-i" aria-hidden>
            i
          </span>
          <div>
            <strong>
              Quan hệ (Sau việc…) + ưu tiên + thời lượng là luật nhà. Khung giờ vẫn là gợi ý cho
              Daily Flow — chưa tự dời giờ (R2).
            </strong>
            <p>
              Đổi template chỉ áp dụng cho Daily Flow mới. Ngày hôm nay (nếu đã mở) giữ danh sách
              cũ.
            </p>
          </div>
        </div>
        <div className="fr-banner-art" aria-hidden>
          📋⏰
        </div>
      </section>

      {routines.map((routine) => {
        const groups = buildRoutineMemberGroups(
          routine.templates,
          childMembers.map((m) => ({ id: m.id, displayName: m.displayName })),
          memberName,
        );
        const visibleGroups =
          focusChildId == null
            ? groups
            : groups.filter((g) => g.key === focusChildId);
        const stats = routineStats(routine.templates);
        const tone = activeTone(routine.kind);
        return (
          <section key={routine.id} className="fr-card">
            <div className="fr-card-head">
              <div>
                <div className="fr-card-title">
                  <h2>{routine.displayName}</h2>
                  {routine.isActive ? (
                    <span className={`fr-active ${tone}`}>Active</span>
                  ) : (
                    <span className="fr-active is-off">Inactive</span>
                  )}
                </div>
                <p className="fr-card-meta">
                  code: {routine.code} · Ngày: {weekdayLabel(routine.weekdays)}
                </p>
                {childMembers.length > 0 ? (
                  <div className="fr-children-strip" role="tablist" aria-label="Chọn con để xem">
                    {childMembers.length > 1 ? (
                      <button
                        type="button"
                        role="tab"
                        aria-selected={focusChildId == null}
                        className={`fr-member-chip is-tab${focusChildId == null ? ' is-on' : ''}`}
                        onClick={() => setFocusChildId(null)}
                      >
                        <span className="fr-av" aria-hidden>
                          🏡
                        </span>
                        <div>
                          <strong>Cả đội</strong>
                          <em>{routine.templates.length} cam kết</em>
                        </div>
                      </button>
                    ) : null}
                    {childMembers.map((ch) => {
                      const count = routine.templates.filter((t) => t.memberId === ch.id).length;
                      const on = focusChildId === ch.id;
                      return (
                        <button
                          key={ch.id}
                          type="button"
                          role="tab"
                          aria-selected={on}
                          className={`fr-member-chip is-tab${on ? ' is-on' : ''}${
                            count === 0 ? ' is-empty' : ''
                          }`}
                          onClick={() => setFocusChildId(ch.id)}
                        >
                          <span className="fr-av" aria-hidden>
                            {memberEmoji(ch.displayName)}
                          </span>
                          <div>
                            <strong>{ch.displayName}</strong>
                            <em>
                              {count > 0 ? `${count} cam kết` : 'Chưa có cam kết'}
                            </em>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                ) : null}
                <p className="fr-card-hint">
                  Chạm tên con để xem cam kết của bạn đó
                  {focusChildId
                    ? ` · đang xem ${childMembers.find((c) => c.id === focusChildId)?.displayName ?? ''}`
                    : childMembers.length > 1
                      ? ' · đang xem cả đội'
                      : ''}
                  .
                </p>
              </div>
              <div className="fr-card-actions">
                <Button icon={<EditOutlined />} onClick={() => openRoutineEdit(routine)}>
                  Sửa routine
                </Button>
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  onClick={() => {
                    openTemplateCreate(routine);
                    if (focusChildId) {
                      window.setTimeout(() => {
                        templateForm.setFieldsValue({ memberId: focusChildId });
                      }, 0);
                    }
                  }}
                >
                  Thêm cam kết
                </Button>
              </div>
            </div>

            {visibleGroups.length === 0 ? (
              <p className="fr-empty">Chưa có cam kết — thêm việc đầu tiên cho bé.</p>
            ) : (
              <div className="fr-card-body">
                {visibleGroups.map((group) => {
                  const gStats = routineStats(group.items);
                  const isChild = childMembers.some((c) => c.id === group.key);
                  return (
                    <div key={group.key} className="fr-member-block" id={`fr-child-${group.key}`}>
                      {focusChildId == null && childMembers.length > 1 ? (
                        <div className="fr-member-bar">
                          <h3 className="fr-member-heading">
                            <span className="fr-av is-sm" aria-hidden>
                              {memberEmoji(group.label)}
                            </span>
                            {group.label}
                            <em>
                              {group.items.length > 0
                                ? `${group.items.length} cam kết`
                                : 'Chưa có cam kết'}
                            </em>
                          </h3>
                          {group.items.length > 0 ? (
                            <div className="fr-stat-row">
                              <div className="fr-stat">
                                <span className="fr-stat-ico is-purple">
                                  <ClockCircleOutlined />
                                </span>
                                <div>
                                  <em>Tổng thời lượng</em>
                                  <strong>{gStats.totalMinutes} phút</strong>
                                </div>
                              </div>
                              <div className="fr-stat">
                                <span className="fr-stat-ico is-green">
                                  <CheckCircleOutlined />
                                </span>
                                <div>
                                  <em>Cần hoàn thành</em>
                                  <strong>{gStats.count} cam kết</strong>
                                </div>
                              </div>
                              <div className="fr-stat">
                                <span className="fr-stat-ico is-gold">
                                  <StarFilled />
                                </span>
                                <div>
                                  <em>Ưu tiên cao</em>
                                  <strong>{gStats.highPriority} việc</strong>
                                </div>
                              </div>
                              <div className="fr-stat">
                                <span className="fr-stat-ico is-blue">
                                  <CalendarOutlined />
                                </span>
                                <div>
                                  <em>Thời gian nên học</em>
                                  <strong>{gStats.studyWindow}</strong>
                                </div>
                              </div>
                            </div>
                          ) : null}
                        </div>
                      ) : group.items.length > 0 ? (
                        <div className="fr-stat-row" style={{ marginBottom: 12 }}>
                          <div className="fr-stat">
                            <span className="fr-stat-ico is-purple">
                              <ClockCircleOutlined />
                            </span>
                            <div>
                              <em>Tổng thời lượng</em>
                              <strong>{gStats.totalMinutes} phút</strong>
                            </div>
                          </div>
                          <div className="fr-stat">
                            <span className="fr-stat-ico is-green">
                              <CheckCircleOutlined />
                            </span>
                            <div>
                              <em>Cần hoàn thành</em>
                              <strong>{gStats.count} cam kết</strong>
                            </div>
                          </div>
                          <div className="fr-stat">
                            <span className="fr-stat-ico is-gold">
                              <StarFilled />
                            </span>
                            <div>
                              <em>Ưu tiên cao</em>
                              <strong>{gStats.highPriority} việc</strong>
                            </div>
                          </div>
                          <div className="fr-stat">
                            <span className="fr-stat-ico is-blue">
                              <CalendarOutlined />
                            </span>
                            <div>
                              <em>Thời gian nên học</em>
                              <strong>{gStats.studyWindow}</strong>
                            </div>
                          </div>
                        </div>
                      ) : null}

                      {group.items.length === 0 ? (
                        <div className="fr-empty-member">
                          <p>
                            {isChild
                              ? `${group.label} chưa có Mission trong routine này — gắn việc để cả đội cùng chơi.`
                              : 'Chưa có cam kết trong nhóm này.'}
                          </p>
                          <Button
                            type="link"
                            icon={<PlusOutlined />}
                            onClick={() => {
                              openTemplateCreate(routine);
                              window.setTimeout(() => {
                                templateForm.setFieldsValue({ memberId: group.key });
                              }, 0);
                            }}
                          >
                            Thêm cam kết cho {group.label}
                          </Button>
                        </div>
                      ) : (
                        <Table
                          size="middle"
                          rowKey="id"
                          pagination={false}
                          columns={templateColumns(routine)}
                          dataSource={group.items}
                          className="fr-table"
                        />
                      )}
                    </div>
                  );
                })}

                {childMembers.length > 1 && focusChildId == null ? (
                  <p className="fr-foot-note">
                    Tổng routine: {stats.totalMinutes} phút · {stats.count} cam kết · ưu tiên cao{' '}
                    {stats.highPriority} · {childMembers.length} con trong nhà
                  </p>
                ) : null}
              </div>
            )}
          </section>
        );
      })}

      {!loading && routines.length === 0 ? (
        <section className="fr-card">
          <p className="fr-empty">Chưa có routine. Thêm nhịp sống hoặc chạy seed gia đình mẫu.</p>
        </section>
      ) : null}

      <Drawer
        title="Sửa routine"
        width={420}
        open={routineOpen}
        onClose={() => setRoutineOpen(false)}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void saveRoutine()}>
            Lưu
          </Button>
        }
      >
        <Form form={routineForm} layout="vertical">
          <Form.Item
            name="displayName"
            label="Tên hiển thị"
            rules={[{ required: true, message: 'Nhập tên routine' }]}
          >
            <Input placeholder="Ngày đi học" />
          </Form.Item>
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select options={KIND_OPTIONS} />
          </Form.Item>
          <Form.Item name="weekdays" label="Ngày trong tuần">
            <Checkbox.Group options={WEEKDAY_OPTIONS} />
          </Form.Item>
          <Form.Item name="isActive" label="Đang dùng" valuePropName="checked">
            <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={editingTemplate ? 'Sửa cam kết' : 'Thêm cam kết'}
        width={480}
        open={templateOpen}
        onClose={() => setTemplateOpen(false)}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void saveTemplate()}>
            Lưu
          </Button>
        }
      >
        <Form form={templateForm} layout="vertical">
          <Form.Item
            name="title"
            label="Tên cam kết"
            rules={[{ required: true, message: 'Nhập tên việc' }]}
          >
            <Input placeholder="Đánh răng buổi sáng" />
          </Form.Item>
          <Form.Item name="description" label="Ghi chú">
            <Input.TextArea rows={2} placeholder="Tuỳ chọn" />
          </Form.Item>
          <Form.Item name="memberId" label="Ai làm">
            <Select
              allowClear
              placeholder="Cả nhà / không gán"
              options={[
                ...childMembers.map((m) => ({
                  value: m.id,
                  label: `${m.displayName} (con)`,
                })),
                ...members
                  .filter((m) => m.roleCode !== 'child')
                  .map((m: FamilyMembership) => ({
                    value: m.id,
                    label: `${m.displayName} (${m.roleCode})`,
                  })),
              ]}
            />
          </Form.Item>
          <Form.Item
            name="priority"
            label="Ưu tiên"
            rules={[{ required: true, message: 'Chọn ưu tiên' }]}
          >
            <Select options={PRIORITY_OPTIONS} />
          </Form.Item>
          <Form.Item
            name="window"
            label="Giờ bắt đầu – giờ kết thúc"
            rules={[
              {
                validator: (_, value: TemplateFormValues['window']) => {
                  const [start, end] = value ?? [];
                  if (!start?.isValid() || !end?.isValid()) {
                    return Promise.reject(new Error('Chọn đủ giờ bắt đầu và giờ kết thúc'));
                  }
                  if (!end.isAfter(start)) {
                    return Promise.reject(new Error('Giờ kết thúc phải sau giờ bắt đầu'));
                  }
                  return Promise.resolve();
                },
              },
            ]}
            extra={
              templateDurationMinutes != null
                ? `Thời lượng tự tính: ${templateDurationMinutes} phút`
                : 'Chọn giờ bắt đầu và giờ kết thúc; số phút làm việc sẽ tự tính.'
            }
          >
            <TimePicker.RangePicker format="HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            name="allowEarlyComplete"
            label="Cho phép làm sớm (allow_early_complete)"
            valuePropName="checked"
            extra="Bật cho việc linh hoạt (đọc sách, thể dục): con có thể xong trước window_start. Tắt = chỉ mở khóa từ đầu khung giờ."
          >
            <Switch checkedChildren="Có" unCheckedChildren="Không" />
          </Form.Item>
          <Form.Item
            name="earlyLeadMinutes"
            label="Mở khóa trước window_start (phút)"
            extra="Chỉ khi allow_early bật. 0 = làm sớm không giới hạn. N > 0 = mở khóa trước window_start N phút."
          >
            <InputNumber min={0} max={720} style={{ width: '100%' }} placeholder="VD: 0, 30, 120" />
          </Form.Item>
          <Form.Item
            name="onTimeGraceMinutes"
            label="Đúng giờ — dung sai (phút)"
            extra="Sau window_end bao nhiêu phút vẫn tính đủ sao. Mức muộn T1/T2/T3 của gia đình áp dụng sau dung sai này."
          >
            <InputNumber min={0} max={120} style={{ width: '100%' }} placeholder="VD: 0, 5, 10" />
          </Form.Item>
          <Form.Item
            name="starReward"
            label="Sao thưởng (star_reward)"
            extra="Số sao khi hoàn thành đúng giờ. Để trống = tự đoán theo tên việc."
          >
            <InputNumber min={1} max={999} style={{ width: '100%' }} placeholder="VD: 10, 15, 20" />
          </Form.Item>
          <Form.Item name="contextAnchor" label="Neo ngữ cảnh">
            <Select allowClear placeholder="Không neo" options={CONTEXT_ANCHOR_OPTIONS} />
          </Form.Item>
          <Form.Item
            name="dependsOnTemplateIds"
            label="Sau việc…"
            extra="Chỉ chọn cam kết trong cùng routine; không tạo vòng lặp."
          >
            <Select
              mode="multiple"
              allowClear
              placeholder="Không phụ thuộc"
              options={(templateRoutine?.templates ?? [])
                .filter((t) => t.id !== editingTemplate?.id)
                .map((t) => ({ value: t.id, label: t.title }))}
            />
          </Form.Item>
          <Form.Item
            name="sortOrder"
            label="Thứ tự"
            rules={[{ required: true, message: 'Nhập thứ tự' }]}
          >
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          {editingTemplate ? (
            <Form.Item name="isActive" label="Đang dùng" valuePropName="checked">
              <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
            </Form.Item>
          ) : (
            <Form.Item name="isActive" hidden initialValue={true}>
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </div>
  );
}
