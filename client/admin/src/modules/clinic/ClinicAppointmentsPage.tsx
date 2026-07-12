import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Badge,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
  Alert,
  message,
} from 'antd';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  ReloadOutlined,
  LoginOutlined,
  PhoneOutlined,
  ProfileOutlined,
  CheckCircleOutlined,
  CloseOutlined,
  UserDeleteOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  UserOutlined,
  FlagOutlined,
  MedicineBoxOutlined,
  ApartmentOutlined,
  SyncOutlined,
  CheckOutlined,
  StopOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchCustomers } from '@/shared/api/customer-admin.api';
import {
  checkInClinicAppointment,
  createClinicAppointment,
  fetchClinicAppointments,
  fetchClinicProviders,
  updateClinicAppointmentStatus,
  type ClinicAppointment,
  type ClinicProvider,
} from '@/shared/api/clinic.api';

const STATUS_COLOR: Record<string, string> = {
  scheduled: 'blue',
  checked_in: 'orange',
  completed: 'green',
  cancelled: 'default',
  no_show: 'red',
};

const ACTIVE_STATUSES = ['scheduled', 'checked_in'] as const;
const DONE_STATUSES = ['completed'] as const;
const OTHER_STATUSES = ['cancelled', 'no_show'] as const;

type ListTab = 'active' | 'done' | 'other';
type TimeSlot = 'all' | 'morning' | 'afternoon' | 'evening' | 'overdue';
type QuickFilter = 'all' | 'needs_checkin' | 'waiting' | 'done_only' | 'cancelled' | 'no_show';

type CreateForm = {
  customerId: string;
  providerId?: string;
  appointmentAt: dayjs.Dayjs;
  durationMinutes: number;
  encounterModality: 'in_person' | 'remote_async';
  reason?: string;
  notes?: string;
};

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

function matchesTimeSlot(
  row: ClinicAppointment,
  slot: TimeSlot,
  now: dayjs.Dayjs,
  selectedDay: dayjs.Dayjs,
): boolean {
  if (slot === 'all') return true;
  const at = dayjs(row.appointmentAt);
  if (!at.isValid()) return false;
  const hour = at.hour();

  if (slot === 'morning') return hour < 12;
  if (slot === 'afternoon') return hour >= 12 && hour < 17;
  if (slot === 'evening') return hour >= 17;

  // overdue: still scheduled but past the appointment time
  if (row.appointmentStatus !== 'scheduled') return false;
  if (selectedDay.isBefore(now, 'day')) return true;
  if (selectedDay.isAfter(now, 'day')) return false;
  return at.isBefore(now);
}

function matchesQuick(row: ClinicAppointment, quick: QuickFilter): boolean {
  if (quick === 'all') return true;
  if (quick === 'needs_checkin') return row.appointmentStatus === 'scheduled';
  if (quick === 'waiting') return row.appointmentStatus === 'checked_in';
  if (quick === 'done_only') return row.appointmentStatus === 'completed';
  if (quick === 'cancelled') return row.appointmentStatus === 'cancelled';
  return row.appointmentStatus === 'no_show';
}

function matchesSearch(row: ClinicAppointment, q: string): boolean {
  if (!q) return true;
  const hay = [
    row.customerName,
    row.customerPhone,
    row.reason,
    row.notes,
    row.providerDisplayName,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return hay.includes(q);
}

export function ClinicAppointmentsPage() {
  const { t } = useTranslation('clinic');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const dayFromQuery = searchParams.get('day');
  const [day, setDay] = useState(() =>
    dayFromQuery && dayjs(dayFromQuery).isValid() ? dayjs(dayFromQuery) : dayjs(),
  );
  const [listTab, setListTab] = useState<ListTab>('active');
  const [items, setItems] = useState<ClinicAppointment[]>([]);
  const [providers, setProviders] = useState<ClinicProvider[]>([]);
  const [customerOptions, setCustomerOptions] = useState<{ value: string; label: string }[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<CreateForm>();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string[]>([]);
  const [providerFilter, setProviderFilter] = useState<string | undefined>();
  const [modalityFilter, setModalityFilter] = useState<string | undefined>();
  const [timeSlot, setTimeSlot] = useState<TimeSlot>('all');
  const [quick, setQuick] = useState<QuickFilter>('all');

  useEffect(() => {
    if (dayFromQuery && dayjs(dayFromQuery).isValid()) {
      setDay(dayjs(dayFromQuery));
    }
  }, [dayFromQuery]);

  const range = useMemo(() => {
    const from = day.startOf('day').toDate().toISOString();
    const to = day.endOf('day').toDate().toISOString();
    return { from, to };
  }, [day]);

  const tabStatuses =
    listTab === 'active' ? ACTIVE_STATUSES : listTab === 'done' ? DONE_STATUSES : OTHER_STATUSES;

  const statusOptions = useMemo(
    () =>
      tabStatuses.map((s) => ({
        value: s,
        label: t(`appointments.status.${s}`, { defaultValue: s }),
      })),
    [tabStatuses, t],
  );

  const quickOptions = useMemo((): Array<{ value: QuickFilter; label: string }> => {
    if (listTab === 'active') {
      return [
        { value: 'all', label: t('appointments.filterQuickAll') },
        { value: 'needs_checkin', label: t('appointments.filterQuickCheckIn') },
        { value: 'waiting', label: t('appointments.filterQuickWaiting') },
      ];
    }
    if (listTab === 'done') {
      return [{ value: 'all', label: t('appointments.filterQuickAll') }];
    }
    return [
      { value: 'all', label: t('appointments.filterQuickAll') },
      { value: 'cancelled', label: t('appointments.filterQuickCancelledOnly') },
      { value: 'no_show', label: t('appointments.filterQuickNoShow') },
    ];
  }, [listTab, t]);

  const onTabChange = (key: string) => {
    const next = key as ListTab;
    setListTab(next);
    const allowed =
      next === 'active' ? ACTIVE_STATUSES : next === 'done' ? DONE_STATUSES : OTHER_STATUSES;
    setStatusFilter((prev) => prev.filter((s) => (allowed as readonly string[]).includes(s)));
    setQuick('all');
    if (timeSlot === 'overdue' && next !== 'active') setTimeSlot('all');
  };

  const activeBase = useMemo(
    () => items.filter((a) => (ACTIVE_STATUSES as readonly string[]).includes(a.appointmentStatus)),
    [items],
  );
  const doneBase = useMemo(
    () => items.filter((a) => (DONE_STATUSES as readonly string[]).includes(a.appointmentStatus)),
    [items],
  );
  const otherBase = useMemo(
    () => items.filter((a) => (OTHER_STATUSES as readonly string[]).includes(a.appointmentStatus)),
    [items],
  );

  const now = useMemo(() => dayjs(), [items, day]);

  const applyFilters = useCallback(
    (list: ClinicAppointment[]) => {
      const q = search.trim().toLowerCase();
      return list.filter((row) => {
        if (statusFilter.length > 0 && !statusFilter.includes(row.appointmentStatus)) return false;
        if (providerFilter && row.providerId !== providerFilter) return false;
        if (modalityFilter && (row.encounterModality || 'in_person') !== modalityFilter) return false;
        if (!matchesTimeSlot(row, timeSlot, now, day)) return false;
        if (!matchesQuick(row, quick)) return false;
        if (!matchesSearch(row, q)) return false;
        return true;
      });
    },
    [search, statusFilter, providerFilter, modalityFilter, timeSlot, quick, now, day],
  );

  const activeItems = useMemo(() => applyFilters(activeBase), [applyFilters, activeBase]);
  const doneItems = useMemo(() => applyFilters(doneBase), [applyFilters, doneBase]);
  const otherItems = useMemo(() => applyFilters(otherBase), [applyFilters, otherBase]);
  const tableItems =
    listTab === 'active' ? activeItems : listTab === 'done' ? doneItems : otherItems;

  const hasFilters =
    search.trim().length > 0 ||
    statusFilter.length > 0 ||
    !!providerFilter ||
    !!modalityFilter ||
    timeSlot !== 'all' ||
    quick !== 'all';

  const clearFilters = () => {
    setSearch('');
    setStatusFilter([]);
    setProviderFilter(undefined);
    setModalityFilter(undefined);
    setTimeSlot('all');
    setQuick('all');
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, prov] = await Promise.all([
        fetchClinicAppointments(range),
        fetchClinicProviders(false).catch(() => [] as ClinicProvider[]),
      ]);
      setItems(list);
      setProviders(prov);
    } catch (error) {
      message.error(apiErrorMessage(error, t('appointments.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [range, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const searchCustomers = async (q: string) => {
    try {
      const result = await fetchCustomers({ search: q || undefined, page: 1, pageSize: 20 });
      setCustomerOptions(
        result.items.map((c) => ({
          value: c.id,
          label: `${c.fullName}${c.phone ? ` · ${c.phone}` : ''}`,
        })),
      );
    } catch {
      setCustomerOptions([]);
    }
  };

  const onCreate = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await createClinicAppointment({
        customerId: values.customerId,
        providerId: values.providerId,
        appointmentAt: values.appointmentAt.toDate().toISOString(),
        durationMinutes: values.durationMinutes,
        reason: values.reason,
        notes: values.notes,
        encounterModality: values.encounterModality || 'in_person',
      });
      message.success(t('appointments.createSuccess'));
      setOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('appointments.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onStatus = async (id: string, appointmentStatus: string) => {
    try {
      await updateClinicAppointmentStatus(id, appointmentStatus);
      message.success(t('appointments.statusSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('appointments.actionFailed')));
    }
  };

  const onCheckIn = async (id: string, remote?: boolean) => {
    try {
      const visit = await checkInClinicAppointment(id);
      message.success(
        remote ? t('appointments.startRemoteSuccess') : t('appointments.checkInSuccess'),
      );
      navigate(`/clinic/visits?open=${visit.id}`);
    } catch (error) {
      message.error(apiErrorMessage(error, t('appointments.actionFailed')));
    }
  };

  const columns: ColumnsType<ClinicAppointment> = [
    {
      title: colTitle(<ClockCircleOutlined />, t('appointments.colWhen')),
      dataIndex: 'appointmentAt',
      width: '8%',
      align: 'center',
      render: (v: string) => (
        <Typography.Text strong>{v ? dayjs(v).format('HH:mm') : '—'}</Typography.Text>
      ),
    },
    {
      title: colTitle(<UserOutlined />, t('appointments.colPatient')),
      key: 'patient',
      width: '26%',
      ellipsis: true,
      render: (_, row) => {
        const name = row.customerName || row.customerId.slice(0, 8);
        const phone = row.customerPhone?.trim();
        const reason = row.reason?.trim();
        return (
          <div style={{ minWidth: 0 }}>
            <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
              {name}
            </Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
              {[phone, reason ? t('appointments.reasonLabel', { reason }) : null]
                .filter(Boolean)
                .join(' · ') || '—'}
            </Typography.Text>
          </div>
        );
      },
    },
    {
      title: colTitle(<FlagOutlined />, t('appointments.colStatus')),
      dataIndex: 'appointmentStatus',
      width: '12%',
      align: 'center',
      render: (s: string) => (
        <Tag color={STATUS_COLOR[s] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`appointments.status.${s}`, { defaultValue: s })}
        </Tag>
      ),
    },
    {
      title: colTitle(<ApartmentOutlined />, t('appointments.colModality')),
      key: 'modality',
      width: '12%',
      align: 'center',
      render: (_, row) =>
        row.encounterModality && row.encounterModality !== 'in_person' ? (
          <Tag color="purple" style={{ marginInlineEnd: 0 }}>
            {t(`appointments.modality.${row.encounterModality}`, {
              defaultValue: row.encounterModality,
            })}
          </Tag>
        ) : (
          <Tag style={{ marginInlineEnd: 0 }}>{t('appointments.modality.in_person')}</Tag>
        ),
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('appointments.colProvider')),
      key: 'provider',
      width: '16%',
      ellipsis: true,
      render: (_, row) =>
        row.providerDisplayName ? (
          <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.providerDisplayName}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: t('appointments.colActions'),
      key: 'actions',
      width: '26%',
      align: 'right',
      render: (_, row) => {
        const remote =
          row.encounterModality === 'remote_async' || row.encounterModality === 'remote_video';
        const actions: ReactNode[] = [];
        if (row.appointmentStatus === 'scheduled') {
          actions.push(
            <Button
              key="checkin"
              size="small"
              type="primary"
              icon={remote ? <PhoneOutlined /> : <LoginOutlined />}
              onClick={() => void onCheckIn(row.id, remote)}
            >
              {remote ? t('appointments.startRemote') : t('appointments.checkIn')}
            </Button>,
            <Tooltip key="cancel" title={t('appointments.cancel')}>
              <Button
                size="small"
                icon={<CloseOutlined />}
                onClick={() => void onStatus(row.id, 'cancelled')}
              />
            </Tooltip>,
            <Tooltip key="noshow" title={t('appointments.noShow')}>
              <Button
                size="small"
                icon={<UserDeleteOutlined />}
                onClick={() => void onStatus(row.id, 'no_show')}
              />
            </Tooltip>,
          );
        }
        if (row.appointmentStatus === 'checked_in') {
          actions.push(
            <Button
              key="open"
              size="small"
              type="primary"
              icon={<ProfileOutlined />}
              onClick={() => void onCheckIn(row.id, remote)}
            >
              {t('appointments.openVisit')}
            </Button>,
            <Tooltip key="complete" title={t('appointments.complete')}>
              <Button
                size="small"
                icon={<CheckCircleOutlined />}
                onClick={() => void onStatus(row.id, 'completed')}
              />
            </Tooltip>,
          );
        }
        if (actions.length === 0) {
          return <Typography.Text type="secondary">—</Typography.Text>;
        }
        return (
          <Space size={4} style={{ justifyContent: 'flex-end' }}>
            {actions}
          </Space>
        );
      },
    },
  ];

  const emptyText = hasFilters
    ? t('appointments.filterEmpty')
    : listTab === 'active'
      ? t('appointments.emptyActive')
      : listTab === 'done'
        ? t('appointments.emptyDone')
        : t('appointments.emptyOther');

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <CalendarOutlined />
              {t('appointments.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('appointments.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <PharmaDatePicker
            value={day.format('YYYY-MM-DD')}
            onChange={(v) => v && setDay(dayjs(v))}
            allowClear={false}
            yearFrom={dayjs().year() - 2}
            yearTo={dayjs().year() + 1}
            style={{ width: 160 }}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('appointments.refresh')}
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              form.setFieldsValue({
                appointmentAt: day.hour(9).minute(0),
                durationMinutes: 30,
                encounterModality: 'in_person',
              });
              void searchCustomers('');
              setOpen(true);
            }}
          >
            {t('appointments.create')}
          </Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        message={t('appointments.connectTip')}
        action={
          <Button size="small" type="link" onClick={() => navigate('/connect/referrals')}>
            {t('appointments.connectTipAction')}
          </Button>
        }
      />

      <Card styles={{ body: { paddingTop: 12 } }}>
        <Tabs
          type="card"
          activeKey={listTab}
          onChange={onTabChange}
          items={[
            {
              key: 'active',
              label: (
                <Space size={8}>
                  <SyncOutlined />
                  <span>{t('appointments.tabActive')}</span>
                  <Badge
                    count={activeItems.length}
                    showZero
                    color={listTab === 'active' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'done',
              label: (
                <Space size={8}>
                  <CheckOutlined />
                  <span>{t('appointments.tabDone')}</span>
                  <Badge
                    count={doneItems.length}
                    showZero
                    color={listTab === 'done' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'other',
              label: (
                <Space size={8}>
                  <StopOutlined />
                  <span>{t('appointments.tabOther')}</span>
                  <Badge
                    count={otherItems.length}
                    showZero
                    color={listTab === 'other' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
          ]}
        />

        <Space wrap style={{ marginBottom: 12, width: '100%' }} size={[8, 8]}>
          <Input.Search
            allowClear
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('appointments.filterSearch')}
            style={{ width: 220 }}
          />
          <Select
            mode="multiple"
            allowClear
            maxTagCount="responsive"
            value={statusFilter}
            onChange={setStatusFilter}
            options={statusOptions}
            placeholder={t('appointments.filterStatus')}
            style={{ minWidth: 180 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            value={providerFilter}
            onChange={(v) => setProviderFilter(v)}
            options={providers.map((p) => ({
              value: p.id,
              label: p.specialty ? `${p.displayName} · ${p.specialty}` : p.displayName,
            }))}
            placeholder={t('appointments.filterProvider')}
            style={{ minWidth: 160 }}
          />
          <Select
            allowClear
            value={modalityFilter}
            onChange={(v) => setModalityFilter(v)}
            options={[
              { value: 'in_person', label: t('appointments.modality.in_person') },
              { value: 'remote_async', label: t('appointments.modality.remote_async') },
            ]}
            placeholder={t('appointments.filterModality')}
            style={{ minWidth: 140 }}
          />
          <Select
            value={timeSlot}
            onChange={(v) => setTimeSlot(v)}
            style={{ minWidth: 180 }}
            options={[
              { value: 'all', label: t('appointments.filterTimeAll') },
              { value: 'morning', label: t('appointments.filterTimeMorning') },
              { value: 'afternoon', label: t('appointments.filterTimeAfternoon') },
              { value: 'evening', label: t('appointments.filterTimeEvening') },
              ...(listTab === 'active'
                ? [{ value: 'overdue' as const, label: t('appointments.filterTimeOverdue') }]
                : []),
            ]}
            placeholder={t('appointments.filterTime')}
          />
          {listTab !== 'done' ? (
            <Select
              value={quick}
              onChange={(v) => setQuick(v)}
              style={{ minWidth: 150 }}
              options={quickOptions}
              placeholder={t('appointments.filterQuick')}
            />
          ) : null}
          {hasFilters ? (
            <Button type="link" onClick={clearFilters}>
              {t('appointments.filterClear')}
            </Button>
          ) : null}
        </Space>

        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={tableItems}
          tableLayout="fixed"
          pagination={{ pageSize: 20 }}
          locale={{ emptyText }}
        />
      </Card>

      <Modal
        title={t('appointments.createTitle')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => void onCreate()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="customerId"
            label={t('appointments.patient')}
            rules={[{ required: true, message: t('appointments.patientRequired') }]}
          >
            <Select
              showSearch
              filterOption={false}
              onSearch={(q) => void searchCustomers(q)}
              options={customerOptions}
              placeholder={t('appointments.patientPlaceholder')}
            />
          </Form.Item>
          <Form.Item name="providerId" label={t('appointments.provider')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              options={providers.map((p) => ({
                value: p.id,
                label: p.specialty ? `${p.displayName} · ${p.specialty}` : p.displayName,
              }))}
              placeholder={
                providers.length === 0
                  ? t('appointments.providerEmpty')
                  : t('appointments.providerPlaceholder')
              }
              notFoundContent={t('appointments.providerEmpty')}
              getPopupContainer={(node) => node.parentElement ?? document.body}
            />
          </Form.Item>
          <Form.Item
            name="encounterModality"
            label={t('appointments.modalityLabel')}
            initialValue="in_person"
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 'in_person', label: t('appointments.modality.in_person') },
                { value: 'remote_async', label: t('appointments.modality.remote_async') },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="appointmentAt"
            label={t('appointments.when')}
            rules={[{ required: true, message: t('appointments.whenRequired') }]}
          >
            <DatePicker
              showTime
              format="DD/MM/YYYY HH:mm"
              style={{ width: '100%' }}
            />
          </Form.Item>
          <Form.Item name="durationMinutes" label={t('appointments.duration')} initialValue={30}>
            <InputNumber min={5} max={480} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="reason" label={t('appointments.reason')}>
            <Input />
          </Form.Item>
          <Form.Item name="notes" label={t('appointments.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
