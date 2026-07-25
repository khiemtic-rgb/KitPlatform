import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  App,
  Button,
  DatePicker,
  Dropdown,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Switch,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { MenuProps } from 'antd';
import {
  BookOutlined,
  CalendarOutlined,
  CarOutlined,
  DeleteOutlined,
  DownOutlined,
  EditOutlined,
  FireOutlined,
  PlusOutlined,
  ReadOutlined,
  ReloadOutlined,
  SmileOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createFamilyCalendarPeriod,
  deleteFamilyCalendarPeriod,
  fetchFamilies,
  fetchFamilyCalendarPeriods,
  fetchFamilyRoutines,
  resolveFamilyCalendarRoutine,
  updateFamilyCalendarPeriod,
  type CalendarPeriod,
  type FamilyRoutine,
  type FamilySummary,
  type ResolvedCalendarRoutine,
} from '@/shared/api/family-os.api';
import './family-os-routines.css';

const KIND_OPTIONS = [
  { value: 'school_year', label: 'Năm học' },
  { value: 'summer', label: 'Nghỉ hè' },
  { value: 'exam', label: 'Ôn thi' },
  { value: 'travel', label: 'Du lịch' },
  { value: 'holiday', label: 'Nghỉ lễ' },
  { value: 'custom', label: 'Tùy chỉnh' },
];

const KIND_COLOR: Record<string, string> = {
  school_year: 'blue',
  summer: 'gold',
  exam: 'purple',
  travel: 'cyan',
  holiday: 'magenta',
  custom: 'default',
};

const WEEKDAY_LABELS = ['', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const WEEKDAYS_SCHOOL = [1, 2, 3, 4, 5];
const WEEKDAYS_WEEKEND = [6, 7];

type PeriodTemplate = {
  id: string;
  menuLabel: string;
  icon: React.ReactNode;
  kind: string;
  codeBase: string;
  baseName: string;
  notes: string;
  // Preferred routine codes then kinds (first match wins) for weekday / weekend.
  weekdayRoutineCodes: string[];
  weekdayRoutineKinds: string[];
  weekendRoutineCodes: string[];
  weekendRoutineKinds: string[];
  // Returns [start, end] and a display suffix (e.g. "2026–2027" or "2026").
  build: () => { start: Dayjs; end: Dayjs; suffix: string; codeYear: number };
};

function nextRange(startMonth: number, startDay: number, months: number) {
  const now = dayjs();
  let start = dayjs().month(startMonth).date(startDay).startOf('day');
  // If this year's window already ended, roll to next year.
  if (start.add(months, 'month').isBefore(now)) {
    start = start.add(1, 'year');
  }
  return start;
}

const PERIOD_TEMPLATES: PeriodTemplate[] = [
  {
    id: 'school_year',
    menuLabel: 'Năm học (T2–T6 đi học · T7–CN nghỉ)',
    icon: <BookOutlined />,
    kind: 'school_year',
    codeBase: 'school_year',
    baseName: 'Năm học',
    notes: 'Ngày đi học T2–T6; cuối tuần giữ nhịp nhẹ.',
    weekdayRoutineCodes: ['school_day'],
    weekdayRoutineKinds: ['school_day'],
    weekendRoutineCodes: ['weekend', 'summer_day'],
    weekendRoutineKinds: ['weekend', 'holiday'],
    build: () => {
      const now = dayjs();
      // School year starts ~25/08; if before that, current year already started last Aug.
      const startYear = now.month() >= 7 ? now.year() : now.year() - 1;
      const start = dayjs(`${startYear}-08-25`);
      const end = dayjs(`${startYear + 1}-05-31`);
      return { start, end, suffix: `${startYear}–${startYear + 1}`, codeYear: startYear };
    },
  },
  {
    id: 'summer',
    menuLabel: 'Nghỉ hè (T2–T6 nhịp hè · dậy 8h)',
    icon: <FireOutlined />,
    kind: 'summer',
    codeBase: 'summer',
    baseName: 'Nghỉ hè',
    notes: 'T2–T6 dùng Ngày hè (dậy muộn hơn); T7–CN dùng Cuối tuần.',
    // Prefer summer_day; fall back to weekend so template still works before summer routine exists.
    weekdayRoutineCodes: ['summer_day', 'weekend'],
    weekdayRoutineKinds: ['holiday', 'weekend'],
    weekendRoutineCodes: ['weekend', 'summer_day'],
    weekendRoutineKinds: ['weekend', 'holiday'],
    build: () => {
      const start = nextRange(5, 1, 3); // 01/06
      const end = start.month(7).date(24); // 24/08 same year
      return { start, end, suffix: `${start.year()}`, codeYear: start.year() };
    },
  },
  {
    id: 'exam',
    menuLabel: 'Ôn thi (cả tuần tập trung ôn)',
    icon: <ReadOutlined />,
    kind: 'exam',
    codeBase: 'exam',
    baseName: 'Ôn thi',
    notes: 'Giai đoạn ôn thi — ưu tiên cao hơn năm học/nghỉ hè khi trùng ngày.',
    weekdayRoutineCodes: ['exam', 'school_day'],
    weekdayRoutineKinds: ['exam', 'school_day'],
    weekendRoutineCodes: ['exam', 'weekend'],
    weekendRoutineKinds: ['exam', 'weekend'],
    build: () => {
      const start = dayjs().add(1, 'day').startOf('day');
      const end = start.add(20, 'day');
      return { start, end, suffix: start.format('MM/YYYY'), codeYear: start.year() };
    },
  },
  {
    id: 'travel',
    menuLabel: 'Du lịch (mọi ngày dùng nhịp du lịch)',
    icon: <CarOutlined />,
    kind: 'travel',
    codeBase: 'travel',
    baseName: 'Du lịch',
    notes: 'Cả tuần dùng nhịp du lịch — ưu tiên cao nhất khi trùng ngày.',
    weekdayRoutineCodes: ['travel', 'weekend', 'summer_day'],
    weekdayRoutineKinds: ['travel', 'weekend', 'holiday'],
    weekendRoutineCodes: ['travel', 'weekend', 'summer_day'],
    weekendRoutineKinds: ['travel', 'weekend', 'holiday'],
    build: () => {
      const start = dayjs().add(1, 'day').startOf('day');
      const end = start.add(6, 'day');
      return { start, end, suffix: start.format('DD/MM'), codeYear: start.year() };
    },
  },
  {
    id: 'holiday',
    menuLabel: 'Nghỉ lễ / Tết (mọi ngày nhịp nghỉ)',
    icon: <SmileOutlined />,
    kind: 'holiday',
    codeBase: 'holiday',
    baseName: 'Nghỉ lễ',
    notes: 'Ngày nghỉ lễ — cả tuần dùng nhịp thư giãn.',
    weekdayRoutineCodes: ['weekend', 'summer_day'],
    weekdayRoutineKinds: ['holiday', 'weekend'],
    weekendRoutineCodes: ['weekend', 'summer_day'],
    weekendRoutineKinds: ['weekend', 'holiday'],
    build: () => {
      const start = dayjs().add(1, 'day').startOf('day');
      const end = start.add(3, 'day');
      return { start, end, suffix: start.format('DD/MM'), codeYear: start.year() };
    },
  },
];

type PeriodFormValues = {
  code: string;
  displayName: string;
  kind: string;
  range: [Dayjs, Dayjs];
  priority?: number;
  isActive: boolean;
  notes?: string;
  weekdayRoutineId?: string;
  weekendRoutineId?: string;
};

function kindLabel(kind: string): string {
  return KIND_OPTIONS.find((o) => o.value === kind)?.label ?? kind;
}

function formatWeekdays(days: number[]): string {
  return days
    .slice()
    .sort((a, b) => a - b)
    .map((d) => WEEKDAY_LABELS[d] ?? String(d))
    .join(', ');
}

function slotRoutine(
  period: CalendarPeriod,
  weekdays: number[],
): string | undefined {
  const set = new Set(weekdays);
  const match = period.slots.find(
    (s) => s.weekdays.length === set.size && s.weekdays.every((d) => set.has(d)),
  );
  return match?.routineId;
}

function familyPageSubtitle(displayName: string, suffix: string): string {
  return `${displayName.trim()} — ${suffix}`;
}

export function FamilyOsCalendarPeriodsPage() {
  const { modal } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [periods, setPeriods] = useState<CalendarPeriod[]>([]);
  const [routines, setRoutines] = useState<FamilyRoutine[]>([]);
  const [resolved, setResolved] = useState<ResolvedCalendarRoutine | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<CalendarPeriod | null>(null);
  const formDraftRef = useRef<PeriodFormValues | null>(null);
  const [form] = Form.useForm<PeriodFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setPeriods([]);
        setRoutines([]);
        setResolved(null);
        return;
      }
      // Load independently so a missing calendar-period API doesn't blank Nhịp sống.
      const [periodResult, routineResult, resolveRow] = await Promise.all([
        fetchFamilyCalendarPeriods(first.id)
          .then((rows) => ({ ok: true as const, rows }))
          .catch((error) => ({ ok: false as const, error })),
        fetchFamilyRoutines(first.id)
          .then((rows) => ({ ok: true as const, rows }))
          .catch((error) => ({ ok: false as const, error })),
        resolveFamilyCalendarRoutine(first.id).catch(() => null),
      ]);
      if (periodResult.ok) {
        setPeriods(periodResult.rows);
      } else {
        setPeriods([]);
        message.warning(
          apiErrorMessage(periodResult.error, 'Không tải được kỳ lịch — kiểm tra API đã cập nhật chưa'),
        );
      }
      if (routineResult.ok) {
        setRoutines(routineResult.rows);
      } else {
        setRoutines([]);
        message.warning(apiErrorMessage(routineResult.error, 'Không tải được Nhịp sống'));
      }
      setResolved(resolveRow);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được lịch gia đình'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const activeRoutines = useMemo(
    () => routines.filter((r) => r.isActive !== false),
    [routines],
  );

  const routineOptions = useMemo(
    () =>
      activeRoutines.map((r) => ({
        value: r.id,
        label: `${r.displayName} (${r.code})`,
      })),
    [activeRoutines],
  );

  const pickRoutineId = useCallback(
    (codes: string[], kinds: string[], fallbackAny = true): string | undefined => {
      const pool = activeRoutines;
      for (const code of codes) {
        const byCode = pool.find((r) => r.code.toLowerCase() === code.toLowerCase());
        if (byCode) return byCode.id;
      }
      for (const kind of kinds) {
        const byKind = pool.find((r) => r.kind.toLowerCase() === kind.toLowerCase());
        if (byKind) return byKind.id;
      }
      if (fallbackAny && pool.length > 0) return pool[0].id;
      return undefined;
    },
    [activeRoutines],
  );

  const openDrawerWith = (values: PeriodFormValues, period: CalendarPeriod | null = null) => {
    setEditing(period);
    formDraftRef.current = values;
    setEditorOpen(true);
  };

  const openCreate = () => {
    openDrawerWith({
      code: '',
      displayName: '',
      kind: 'summer',
      range: [dayjs(), dayjs().add(2, 'month')],
      priority: undefined,
      isActive: true,
      notes: '',
      weekdayRoutineId: pickRoutineId(['summer_day', 'weekend'], ['holiday', 'weekend']),
      weekendRoutineId: pickRoutineId(['weekend', 'summer_day'], ['weekend', 'holiday']),
    });
  };

  const existingCodes = useMemo(() => new Set(periods.map((p) => p.code)), [periods]);

  const openFromTemplate = (tpl: PeriodTemplate) => {
    const { start, end, suffix, codeYear } = tpl.build();

    let code = `${tpl.codeBase}_${codeYear}`;
    let bump = 2;
    while (existingCodes.has(code)) {
      code = `${tpl.codeBase}_${codeYear}_${bump}`;
      bump += 1;
    }

    if (activeRoutines.length === 0) {
      message.error(
        'Chưa có Nhịp sống nào — vào tab Nhịp sống tạo “Ngày đi học” / “Cuối tuần” rồi quay lại.',
      );
      return;
    }

    const weekdayRoutineId = pickRoutineId(tpl.weekdayRoutineCodes, tpl.weekdayRoutineKinds);
    const weekendRoutineId = pickRoutineId(tpl.weekendRoutineCodes, tpl.weekendRoutineKinds);

    openDrawerWith({
      code,
      displayName: `${tpl.baseName} ${suffix}`,
      kind: tpl.kind,
      range: [start, end],
      priority: undefined,
      isActive: true,
      notes: tpl.notes,
      weekdayRoutineId,
      weekendRoutineId,
    });

    const usedFallback =
      !activeRoutines.some((r) =>
        tpl.weekdayRoutineCodes.some((c) => c.toLowerCase() === r.code.toLowerCase()),
      ) ||
      !activeRoutines.some((r) =>
        tpl.weekendRoutineCodes.some((c) => c.toLowerCase() === r.code.toLowerCase()),
      );
    if (usedFallback) {
      message.info(
        `Đã gắn nhịp gần nhất có sẵn (${activeRoutines.length} nhịp). Có thể đổi trước khi Lưu.`,
      );
    }
  };

  const templateMenu: MenuProps = {
    items: PERIOD_TEMPLATES.map((tpl) => ({
      key: tpl.id,
      icon: tpl.icon,
      label: tpl.menuLabel,
      onClick: () => openFromTemplate(tpl),
    })),
  };

  const openEdit = (period: CalendarPeriod) => {
    openDrawerWith(
      {
        code: period.code,
        displayName: period.displayName,
        kind: period.kind,
        range: [dayjs(period.startDate), dayjs(period.endDate)],
        priority: period.priority,
        isActive: period.isActive,
        notes: period.notes ?? '',
        weekdayRoutineId: slotRoutine(period, WEEKDAYS_SCHOOL),
        weekendRoutineId: slotRoutine(period, WEEKDAYS_WEEKEND),
      },
      period,
    );
  };

  const buildSlots = (values: PeriodFormValues) => {
    const slots: { weekdays: number[]; routineId: string; sortOrder: number }[] = [];
    if (values.weekdayRoutineId) {
      slots.push({
        weekdays: WEEKDAYS_SCHOOL,
        routineId: values.weekdayRoutineId,
        sortOrder: 1,
      });
    }
    if (values.weekendRoutineId) {
      slots.push({
        weekdays: WEEKDAYS_WEEKEND,
        routineId: values.weekendRoutineId,
        sortOrder: 2,
      });
    }
    return slots;
  };

  const savePeriod = async () => {
    if (!family) return;
    const values = await form.validateFields();
    const slots = buildSlots(values);
    if (slots.length === 0) {
      message.error('Chọn ít nhất một routine cho T2–T6 hoặc T7–CN');
      return;
    }
    setSaving(true);
    try {
      if (editing) {
        await updateFamilyCalendarPeriod(family.id, editing.id, {
          displayName: values.displayName.trim(),
          kind: values.kind,
          startDate: values.range[0].format('YYYY-MM-DD'),
          endDate: values.range[1].format('YYYY-MM-DD'),
          priority: values.priority,
          isActive: values.isActive,
          notes: values.notes?.trim() || null,
          slots,
        });
        message.success('Đã cập nhật kỳ lịch');
      } else {
        await createFamilyCalendarPeriod(family.id, {
          code: values.code.trim().toLowerCase(),
          displayName: values.displayName.trim(),
          kind: values.kind,
          startDate: values.range[0].format('YYYY-MM-DD'),
          endDate: values.range[1].format('YYYY-MM-DD'),
          priority: values.priority,
          isActive: values.isActive,
          notes: values.notes?.trim() || undefined,
          slots,
        });
        message.success('Đã thêm kỳ lịch');
      }
      setEditorOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được kỳ lịch'));
    } finally {
      setSaving(false);
    }
  };

  const removePeriod = (period: CalendarPeriod) => {
    if (!family) return;
    modal.confirm({
      title: `Xóa kỳ “${period.displayName}”?`,
      content: 'Day flow đã mở không đổi; kỳ mới chỉ áp dụng ngày chưa mở.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Huỷ',
      onOk: async () => {
        try {
          await deleteFamilyCalendarPeriod(family.id, period.id);
          message.success('Đã xóa kỳ lịch');
          await load();
        } catch (error) {
          message.error(apiErrorMessage(error, 'Không xóa được kỳ lịch'));
        }
      },
    });
  };

  const columns: ColumnsType<CalendarPeriod> = [
    {
      title: 'Kỳ',
      dataIndex: 'displayName',
      render: (_, row) => (
        <div>
          <div style={{ fontWeight: 600 }}>{row.displayName}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.code}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: 'Loại',
      dataIndex: 'kind',
      width: 120,
      render: (kind: string) => <Tag color={KIND_COLOR[kind] ?? 'default'}>{kindLabel(kind)}</Tag>,
    },
    {
      title: 'Thời gian',
      key: 'range',
      width: 220,
      render: (_, row) => `${row.startDate} → ${row.endDate}`,
    },
    {
      title: 'Nhịp áp dụng',
      key: 'slots',
      render: (_, row) => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {row.slots.map((slot) => (
            <span key={slot.id}>
              <Typography.Text type="secondary">{formatWeekdays(slot.weekdays)}</Typography.Text>
              {' · '}
              {slot.routineDisplayName ?? slot.routineId.slice(0, 8)}
            </span>
          ))}
        </div>
      ),
    },
    {
      title: 'Ưu tiên',
      dataIndex: 'priority',
      width: 90,
      align: 'center',
    },
    {
      title: 'TT',
      dataIndex: 'isActive',
      width: 90,
      render: (active: boolean) =>
        active ? <Tag color="success">Bật</Tag> : <Tag>Tắt</Tag>,
    },
    {
      title: '',
      key: 'actions',
      width: 100,
      render: (_, row) => (
        <div style={{ display: 'flex', gap: 8 }}>
          <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(row)} />
          <Button
            size="small"
            danger
            icon={<DeleteOutlined />}
            onClick={() => removePeriod(row)}
          />
        </div>
      ),
    },
  ];

  return (
    <div className="fos-page">
      <div className="fos-page__header">
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            <CalendarOutlined /> Lịch gia đình
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {family
              ? familyPageSubtitle(
                  family.displayName,
                  'Các kỳ năm học / nghỉ hè / du lịch — gắn routine theo ngày',
                )
              : 'Chưa có gia đình Family OS'}
          </Typography.Paragraph>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} disabled={loading}>
            Tải lại
          </Button>
          <Dropdown menu={templateMenu} disabled={!family} trigger={['click']}>
            <Button icon={<CalendarOutlined />}>
              Tạo từ mẫu <DownOutlined />
            </Button>
          </Dropdown>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!family}>
            Thêm kỳ
          </Button>
        </div>
      </div>

      {resolved && (
        <div
          className="fos-card"
          style={{ marginBottom: 16, padding: '12px 16px' }}
        >
          <Typography.Text strong>Hôm nay ({resolved.flowDate})</Typography.Text>
          <div>
            Áp dụng{' '}
            <Typography.Text strong>{resolved.routineDisplayName}</Typography.Text>
            {resolved.source === 'period' && resolved.periodDisplayName ? (
              <>
                {' '}
                từ kỳ <Tag color={KIND_COLOR[resolved.periodKind ?? ''] ?? 'blue'}>
                  {resolved.periodDisplayName}
                </Tag>
              </>
            ) : (
              <> theo weekday mặc định của Nhịp sống</>
            )}
          </div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Day flow đã mở giữ nguyên; kỳ mới áp dụng khi mở ngày chưa có flow.
            {' '}
            <Link to="/family-os/routines">Sửa nhịp sống →</Link>
          </Typography.Text>
        </div>
      )}

      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={periods}
        pagination={false}
        locale={{ emptyText: 'Chưa có kỳ lịch — thêm Năm học / Nghỉ hè để phụ huynh thấy rõ.' }}
      />

      <Drawer
        title={editing ? `Sửa kỳ · ${editing.displayName}` : 'Thêm kỳ lịch'}
        open={editorOpen}
        onClose={() => {
          setEditorOpen(false);
          formDraftRef.current = null;
        }}
        afterOpenChange={(open) => {
          if (open && formDraftRef.current) {
            form.setFieldsValue(formDraftRef.current);
            formDraftRef.current = null;
          }
        }}
        width={480}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void savePeriod()}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical" requiredMark={false}>
          {!editing && (
            <Form.Item
              name="code"
              label="Mã kỳ"
              rules={[
                { required: true, message: 'Nhập mã' },
                { pattern: /^[a-z0-9_]+$/, message: 'Chỉ a-z, 0-9, _' },
              ]}
            >
              <Input placeholder="summer_2026" />
            </Form.Item>
          )}
          <Form.Item
            name="displayName"
            label="Tên hiển thị"
            rules={[{ required: true, message: 'Nhập tên' }]}
          >
            <Input placeholder="Nghỉ hè 2026" />
          </Form.Item>
          <Form.Item name="kind" label="Loại kỳ" rules={[{ required: true }]}>
            <Select options={KIND_OPTIONS} />
          </Form.Item>
          <Form.Item
            name="range"
            label="Thời gian"
            rules={[{ required: true, message: 'Chọn khoảng ngày' }]}
          >
            <DatePicker.RangePicker style={{ width: '100%' }} format="YYYY-MM-DD" />
          </Form.Item>
          <Form.Item
            name="weekdayRoutineId"
            label="T2–T6 dùng nhịp"
            rules={[{ required: true, message: 'Chọn routine ngày thường' }]}
            extra={
              routineOptions.length === 0 ? (
                <Link to="/family-os/routines">Chưa có nhịp — tạo tại Nhịp sống →</Link>
              ) : undefined
            }
          >
            <Select
              options={routineOptions}
              placeholder={
                routineOptions.length === 0
                  ? 'Chưa có nhịp sống'
                  : 'Ngày đi học / Ngày hè…'
              }
              allowClear
              showSearch
              optionFilterProp="label"
              notFoundContent="Không có nhịp — vào tab Nhịp sống để tạo"
            />
          </Form.Item>
          <Form.Item
            name="weekendRoutineId"
            label="T7–CN dùng nhịp"
            rules={[{ required: true, message: 'Chọn routine cuối tuần' }]}
          >
            <Select
              options={routineOptions}
              placeholder={
                routineOptions.length === 0 ? 'Chưa có nhịp sống' : 'Cuối tuần…'
              }
              allowClear
              showSearch
              optionFilterProp="label"
              notFoundContent="Không có nhịp — vào tab Nhịp sống để tạo"
            />
          </Form.Item>
          <Form.Item
            name="priority"
            label="Ưu tiên (cao hơn thắng khi chồng kỳ)"
            tooltip="Mặc định: Du lịch 100 · Nghỉ lễ 80 · Ôn thi 60 · Nghỉ hè 40 · Năm học 20"
          >
            <InputNumber style={{ width: '100%' }} min={0} max={999} placeholder="Tự theo loại nếu để trống" />
          </Form.Item>
          <Form.Item name="isActive" label="Đang bật" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={3} placeholder="Ghi chú cho bố mẹ…" />
          </Form.Item>
        </Form>
      </Drawer>
    </div>
  );
}
