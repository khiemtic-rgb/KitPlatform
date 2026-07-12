import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
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
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  ReloadOutlined,
  CheckOutlined,
  CloseOutlined,
  CheckCircleOutlined,
  UserDeleteOutlined,
  SearchOutlined,
  FilterOutlined,
  CalendarOutlined,
  UserOutlined,
  BankOutlined,
  ShopOutlined,
  MedicineBoxOutlined,
  FlagOutlined,
  ApartmentOutlined,
  ClockCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  cancelConnectBooking,
  completeConnectBooking,
  confirmConnectBooking,
  createConnectBooking,
  fetchConnectBookings,
  fetchConnectOrgProfile,
  fetchConnectReferrals,
  noShowConnectBooking,
  type ConnectBooking,
  type ConnectOrgProfile,
  type ConnectReferral,
} from '@/shared/api/connect.api';

const STATUS_COLOR: Record<string, string> = {
  proposed: 'blue',
  confirmed: 'green',
  cancelled: 'default',
  completed: 'cyan',
  no_show: 'orange',
};

const BOOKING_STATUSES = [
  'proposed',
  'confirmed',
  'completed',
  'cancelled',
  'no_show',
] as const;

const BOOKING_MODALITIES = ['in_person', 'remote_async'] as const;

type CreateForm = {
  bookingOrigin: 'internal' | 'referral';
  pharmacyTenantId?: string;
  referralId?: string;
  patientDisplayName: string;
  patientPhone?: string;
  scheduledAt: dayjs.Dayjs;
  durationMinutes: number;
  encounterModality: 'in_person' | 'remote_async';
  notes?: string;
};

const ACTIVE_BOOKING_STATUSES = new Set(['proposed', 'confirmed']);

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

function matchesBookingFilter(
  row: ConnectBooking,
  opts: {
    search: string;
    status?: string;
    modality?: string;
    partnerTenantId?: string;
    isClinic: boolean;
  },
): boolean {
  if (opts.status && row.bookingStatus !== opts.status) return false;
  if (opts.modality && row.encounterModality !== opts.modality) return false;
  if (opts.partnerTenantId) {
    const partnerId = opts.isClinic ? row.pharmacyTenantId : row.clinicTenantId;
    if (partnerId !== opts.partnerTenantId) return false;
  }
  const q = opts.search.trim().toLowerCase();
  if (!q) return true;
  const hay = [
    row.patientDisplayName,
    row.patientPhone,
    row.doctorFullName,
    row.clinicTenantName,
    row.clinicTenantCode,
    row.pharmacyTenantName,
    row.pharmacyTenantCode,
    row.notes,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return hay.includes(q);
}

export function ConnectBookingsPage() {
  const { t } = useTranslation('connect');
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [profile, setProfile] = useState<ConnectOrgProfile | null>(null);
  const [items, setItems] = useState<ConnectBooking[]>([]);
  const [referrals, setReferrals] = useState<ConnectReferral[]>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>();
  const [modalityFilter, setModalityFilter] = useState<string>();
  const [partnerFilter, setPartnerFilter] = useState<string>();
  const [form] = Form.useForm<CreateForm>();
  const isClinic = profile?.orgKind === 'clinic';

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const me = await fetchConnectOrgProfile();
      setProfile(me);
      setItems(await fetchConnectBookings());
      if (me?.orgKind === 'clinic') {
        // Chỉ accepted — completed / đã khám lấy ở Quản lý bệnh nhân, không đưa vào đặt lịch
        const refs = await fetchConnectReferrals();
        setReferrals(refs.filter((r) => r.referralStatus === 'accepted'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('bookings.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  /** Accepted referrals chưa có booking proposed/confirmed — BN đề xuất khám. */
  const suggestedReferrals = useMemo(() => {
    const booked = new Set(
      items
        .filter((b) => b.referralId && ACTIVE_BOOKING_STATUSES.has(b.bookingStatus))
        .map((b) => b.referralId as string),
    );
    return referrals.filter((r) => !booked.has(r.id));
  }, [referrals, items]);

  const pharmacyOptions = useMemo(() => {
    const map = new Map<string, { value: string; label: string }>();
    for (const r of suggestedReferrals) {
      if (map.has(r.pharmacyTenantId)) continue;
      const code = r.pharmacyTenantCode || '';
      const name = r.pharmacyTenantName || code;
      map.set(r.pharmacyTenantId, {
        value: r.pharmacyTenantId,
        label: code && name !== code ? `${name} (${code})` : name || code,
      });
    }
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [suggestedReferrals]);

  const filterOpts = useMemo(
    () => ({
      search,
      status: statusFilter,
      modality: modalityFilter,
      partnerTenantId: partnerFilter,
      isClinic: Boolean(isClinic),
    }),
    [search, statusFilter, modalityFilter, partnerFilter, isClinic],
  );

  const filteredItems = useMemo(
    () => items.filter((row) => matchesBookingFilter(row, filterOpts)),
    [items, filterOpts],
  );

  const partnerOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of items) {
      if (isClinic) {
        if (row.pharmacyTenantId) {
          map.set(
            row.pharmacyTenantId,
            row.pharmacyTenantName || row.pharmacyTenantCode || row.pharmacyTenantId,
          );
        }
      } else {
        map.set(row.clinicTenantId, row.clinicTenantName || row.clinicTenantCode);
      }
    }
    return [...map.entries()]
      .map(([value, label]) => ({ value, label }))
      .sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [items, isClinic]);

  // Deep-link from referral "Đặt lịch" / after Accept
  useEffect(() => {
    if (loading || !isClinic) return;
    const referralId = searchParams.get('referralId');
    if (!referralId) return;
    const ref = referrals.find((r) => r.id === referralId);
    if (!ref) return;
    // Default same-day slot so it appears on Clinic → Lịch hẹn "hôm nay"
    const slot = dayjs().add(30, 'minute').second(0).millisecond(0);
    form.setFieldsValue({
      bookingOrigin: 'referral',
      pharmacyTenantId: ref.pharmacyTenantId,
      referralId: ref.id,
      patientDisplayName: ref.patientDisplayName,
      patientPhone: ref.patientPhone,
      durationMinutes: 30,
      scheduledAt: slot,
      encounterModality: 'remote_async',
      notes: ref.reason ? `Referral: ${ref.reason}` : undefined,
    });
    setCreateOpen(true);
    searchParams.delete('referralId');
    setSearchParams(searchParams, { replace: true });
  }, [loading, isClinic, referrals, searchParams, setSearchParams, form]);

  const onCreate = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const fromReferral = values.bookingOrigin === 'referral' && !!values.referralId;
      const created = await createConnectBooking({
        referralId: fromReferral ? values.referralId : undefined,
        patientDisplayName: values.patientDisplayName,
        patientPhone: values.patientPhone,
        scheduledAt: values.scheduledAt.toDate().toISOString(),
        durationMinutes: values.durationMinutes,
        notes: values.notes,
        encounterModality: values.encounterModality || 'in_person',
      });
      // NT→PK: auto-confirm so bridge creates Clinic → Lịch hẹn (no extra click)
      if (fromReferral && created.id) {
        await confirmConnectBooking(created.id);
        message.success(t('bookings.createConfirmSuccess'));
        setCreateOpen(false);
        form.resetFields();
        const day = values.scheduledAt.format('YYYY-MM-DD');
        navigate(`/clinic/appointments?day=${encodeURIComponent(day)}`);
        return;
      }
      message.success(t('bookings.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('bookings.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  const runAction = async (action: () => Promise<unknown>, successKey: string) => {
    try {
      await action();
      message.success(t(successKey));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('bookings.actionFailed')));
    }
  };

  const columns: ColumnsType<ConnectBooking> = [
    {
      title: colTitle(<ClockCircleOutlined />, t('bookings.colWhen')),
      dataIndex: 'scheduledAt',
      width: '14%',
      render: (v: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: colTitle(<UserOutlined />, t('bookings.colPatient')),
      key: 'patient',
      width: '20%',
      ellipsis: true,
      render: (_, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.patientDisplayName}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
            {row.patientPhone || '—'}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<FlagOutlined />, t('bookings.colStatus')),
      dataIndex: 'bookingStatus',
      width: '12%',
      align: 'center',
      render: (status: string) => (
        <Tag color={STATUS_COLOR[status] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`bookings.status.${status}`, { defaultValue: status })}
        </Tag>
      ),
    },
    {
      title: colTitle(<ApartmentOutlined />, t('bookings.colModality')),
      key: 'modality',
      width: '12%',
      align: 'center',
      render: (_, row) => (
        <Tag
          color={row.encounterModality === 'remote_async' ? 'purple' : 'default'}
          style={{ marginInlineEnd: 0 }}
        >
          {t(`bookings.modality.${row.encounterModality}`, {
            defaultValue: row.encounterModality,
          })}
        </Tag>
      ),
    },
    {
      title: colTitle(
        isClinic ? <ShopOutlined /> : <BankOutlined />,
        isClinic ? t('bookings.colPharmacy') : t('bookings.colClinic'),
      ),
      key: 'partner',
      width: '18%',
      ellipsis: true,
      render: (_, row) => {
        const name = isClinic ? row.pharmacyTenantName : row.clinicTenantName;
        const code = isClinic ? row.pharmacyTenantCode : row.clinicTenantCode;
        if (!name && !code) {
          return <Typography.Text type="secondary">—</Typography.Text>;
        }
        return (
          <Tooltip title={code ? `${name || '—'} (${code})` : name}>
            <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
              {name || code}
            </Typography.Text>
          </Tooltip>
        );
      },
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('bookings.colDoctor')),
      key: 'doctor',
      width: '12%',
      ellipsis: true,
      render: (_, row) =>
        row.doctorFullName ? (
          <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.doctorFullName}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: t('bookings.colActions'),
      key: 'actions',
      width: '12%',
      align: 'right',
      render: (_, row) => {
        if (!isClinic) {
          return <Typography.Text type="secondary">—</Typography.Text>;
        }
        const actions: ReactNode[] = [];
        if (row.bookingStatus === 'proposed') {
          actions.push(
            <Tooltip key="confirm" title={t('bookings.confirm')}>
              <Button
                size="small"
                type="primary"
                icon={<CheckOutlined />}
                onClick={() =>
                  void runAction(() => confirmConnectBooking(row.id), 'bookings.confirmSuccess')
                }
              />
            </Tooltip>,
            <Tooltip key="cancel" title={t('bookings.cancel')}>
              <Button
                size="small"
                danger
                icon={<CloseOutlined />}
                onClick={() =>
                  void runAction(() => cancelConnectBooking(row.id), 'bookings.cancelSuccess')
                }
              />
            </Tooltip>,
          );
        }
        if (row.bookingStatus === 'confirmed') {
          actions.push(
            <Tooltip key="complete" title={t('bookings.complete')}>
              <Button
                size="small"
                type="primary"
                icon={<CheckCircleOutlined />}
                onClick={() =>
                  void runAction(() => completeConnectBooking(row.id), 'bookings.completeSuccess')
                }
              />
            </Tooltip>,
            <Tooltip key="noShow" title={t('bookings.noShow')}>
              <Button
                size="small"
                icon={<UserDeleteOutlined />}
                onClick={() =>
                  void runAction(() => noShowConnectBooking(row.id), 'bookings.noShowSuccess')
                }
              />
            </Tooltip>,
            <Tooltip key="cancel" title={t('bookings.cancel')}>
              <Button
                size="small"
                danger
                icon={<CloseOutlined />}
                onClick={() =>
                  void runAction(() => cancelConnectBooking(row.id), 'bookings.cancelSuccess')
                }
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

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <CalendarOutlined />
              {t('bookings.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('bookings.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('bookings.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: 220 }}
          />
          <Select
            allowClear
            placeholder={t('bookings.filterStatus')}
            suffixIcon={<FilterOutlined />}
            style={{ minWidth: 140 }}
            value={statusFilter}
            onChange={(value) => setStatusFilter(value)}
            options={BOOKING_STATUSES.map((key) => ({
              value: key,
              label: t(`bookings.status.${key}`),
            }))}
          />
          <Select
            allowClear
            placeholder={t('bookings.filterModality')}
            style={{ minWidth: 140 }}
            value={modalityFilter}
            onChange={(value) => setModalityFilter(value)}
            options={BOOKING_MODALITIES.map((key) => ({
              value: key,
              label: t(`bookings.modality.${key}`),
            }))}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('bookings.filterPartner')}
            suffixIcon={isClinic ? <ShopOutlined /> : <BankOutlined />}
            style={{ minWidth: 180 }}
            value={partnerFilter}
            onChange={(value) => setPartnerFilter(value)}
            options={partnerOptions}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('bookings.refresh')}
          </Button>
          {isClinic ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                form.setFieldsValue({
                  bookingOrigin: 'internal',
                  durationMinutes: 30,
                  scheduledAt: dayjs().add(1, 'day').hour(9).minute(0),
                });
                setCreateOpen(true);
              }}
            >
              {t('bookings.create')}
            </Button>
          ) : null}
        </Space>
      </div>

      <Card>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={filteredItems}
          columns={columns}
          tableLayout="fixed"
          pagination={{ pageSize: 20 }}
          locale={{
            emptyText:
              items.length > 0 && filteredItems.length === 0
                ? t('bookings.filterEmpty')
                : t('bookings.empty'),
          }}
        />
      </Card>

      <Modal
        title={t('bookings.createTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void onCreate()}
        confirmLoading={saving}
        okText={t('bookings.create')}
        destroyOnClose
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            durationMinutes: 30,
            bookingOrigin: 'internal',
            encounterModality: 'in_person',
          }}
        >
          <Form.Item
            name="bookingOrigin"
            label={t('bookings.origin')}
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 'internal', label: t('bookings.originInternal') },
                { value: 'referral', label: t('bookings.originReferral') },
              ]}
              onChange={(v) => {
                if (v === 'internal') {
                  form.setFieldsValue({
                    pharmacyTenantId: undefined,
                    referralId: undefined,
                  });
                }
                if (v === 'referral') {
                  const onlyPharmacy =
                    pharmacyOptions.length === 1 ? pharmacyOptions[0].value : undefined;
                  form.setFieldsValue({
                    encounterModality: 'remote_async',
                    pharmacyTenantId: onlyPharmacy,
                    referralId: undefined,
                    patientDisplayName: '',
                    patientPhone: undefined,
                  });
                }
              }}
            />
          </Form.Item>
          <Form.Item
            name="encounterModality"
            label={t('bookings.modalityLabel')}
            rules={[{ required: true }]}
            extra={t('bookings.modalityHint')}
          >
            <Select
              options={[
                { value: 'in_person', label: t('bookings.modality.in_person') },
                { value: 'remote_async', label: t('bookings.modality.remote_async') },
              ]}
            />
          </Form.Item>
          <Form.Item
            noStyle
            shouldUpdate={(prev, cur) =>
              prev.bookingOrigin !== cur.bookingOrigin ||
              prev.pharmacyTenantId !== cur.pharmacyTenantId
            }
          >
            {() => {
              const origin = form.getFieldValue('bookingOrigin') as string;
              if (origin !== 'referral') return null;
              const pharmacyId = form.getFieldValue('pharmacyTenantId') as string | undefined;
              const selectedReferralId = form.getFieldValue('referralId') as string | undefined;
              let patientPool = suggestedReferrals.filter(
                (r) => r.pharmacyTenantId === pharmacyId,
              );
              // Deep-link / đã chọn: giữ option dù referral vừa bị lọc (vd. vừa tạo booking)
              if (
                selectedReferralId &&
                !patientPool.some((r) => r.id === selectedReferralId)
              ) {
                const extra = referrals.find((r) => r.id === selectedReferralId);
                if (extra && (!pharmacyId || extra.pharmacyTenantId === pharmacyId)) {
                  patientPool = [...patientPool, extra];
                }
              }
              const patientOptions = patientPool.map((r) => ({
                value: r.id,
                label: r.patientPhone
                  ? `${r.patientDisplayName} · ${r.patientPhone}`
                  : r.patientDisplayName,
              }));
              // Deep-link pharmacy may be outside suggested list — keep it selectable
              const pharmacySelectOptions = (() => {
                if (
                  pharmacyId &&
                  !pharmacyOptions.some((o) => o.value === pharmacyId)
                ) {
                  const extra = referrals.find((r) => r.pharmacyTenantId === pharmacyId);
                  if (extra) {
                    const code = extra.pharmacyTenantCode || '';
                    const name = extra.pharmacyTenantName || code;
                    return [
                      ...pharmacyOptions,
                      {
                        value: extra.pharmacyTenantId,
                        label: code && name !== code ? `${name} (${code})` : name || code,
                      },
                    ];
                  }
                }
                return pharmacyOptions;
              })();
              return (
                <>
                  <Form.Item
                    name="pharmacyTenantId"
                    label={t('bookings.pharmacy')}
                    rules={[{ required: true, message: t('bookings.pharmacyRequired') }]}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      placeholder={t('bookings.pharmacyPlaceholder')}
                      options={pharmacySelectOptions}
                      notFoundContent={t('bookings.pharmacyEmpty')}
                      onChange={() => {
                        form.setFieldsValue({
                          referralId: undefined,
                          patientDisplayName: '',
                          patientPhone: undefined,
                        });
                      }}
                    />
                  </Form.Item>
                  <Form.Item
                    name="referralId"
                    label={t('bookings.referralPatient')}
                    rules={[{ required: true, message: t('bookings.referralRequired') }]}
                    extra={t('bookings.referralPatientHint')}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      disabled={!pharmacyId}
                      placeholder={
                        pharmacyId
                          ? t('bookings.referralPlaceholder')
                          : t('bookings.referralNeedPharmacy')
                      }
                      options={patientOptions}
                      notFoundContent={t('bookings.referralEmpty')}
                      onChange={(id) => {
                        const ref =
                          patientPool.find((r) => r.id === id) ||
                          suggestedReferrals.find((r) => r.id === id) ||
                          referrals.find((r) => r.id === id);
                        if (ref) {
                          form.setFieldsValue({
                            patientDisplayName: ref.patientDisplayName,
                            patientPhone: ref.patientPhone,
                            notes: ref.reason
                              ? form.getFieldValue('notes') || `Referral: ${ref.reason}`
                              : form.getFieldValue('notes'),
                          });
                        }
                      }}
                    />
                  </Form.Item>
                </>
              );
            }}
          </Form.Item>
          <Form.Item noStyle shouldUpdate={(prev, cur) => prev.bookingOrigin !== cur.bookingOrigin}>
            {() => {
              const fromReferral = form.getFieldValue('bookingOrigin') === 'referral';
              return (
                <>
                  <Form.Item
                    name="patientDisplayName"
                    label={t('bookings.patientName')}
                    rules={[{ required: true, message: t('bookings.patientNameRequired') }]}
                  >
                    <Input disabled={fromReferral} />
                  </Form.Item>
                  <Form.Item name="patientPhone" label={t('bookings.patientPhone')}>
                    <Input disabled={fromReferral} />
                  </Form.Item>
                </>
              );
            }}
          </Form.Item>
          <Form.Item
            name="scheduledAt"
            label={t('bookings.when')}
            rules={[{ required: true, message: t('bookings.whenRequired') }]}
          >
            <DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="durationMinutes" label={t('bookings.duration')}>
            <InputNumber min={5} max={480} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="notes" label={t('bookings.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
