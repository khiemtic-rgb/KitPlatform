import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Input,
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
  UserAddOutlined,
  CloseOutlined,
  CalendarOutlined,
  CheckCircleOutlined,
  StopOutlined,
  SearchOutlined,
  UserOutlined,
  BankOutlined,
  ShopOutlined,
  MedicineBoxOutlined,
  FlagOutlined,
  FilterOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchCustomers } from '@/shared/api/customer-admin.api';
import {
  acceptConnectReferral,
  cancelConnectReferral,
  completeConnectReferral,
  createConnectReferral,
  fetchConnectOrgLinks,
  fetchConnectOrgProfile,
  fetchConnectReferralInbox,
  fetchConnectReferrals,
  fetchPartnerClinicDoctors,
  rejectConnectReferral,
  type ConnectDoctor,
  type ConnectOrgLink,
  type ConnectOrgProfile,
  type ConnectReferral,
} from '@/shared/api/connect.api';

const STATUS_COLOR: Record<string, string> = {
  pending_clinic_accept: 'blue',
  accepted: 'green',
  rejected: 'default',
  completed: 'cyan',
  cancelled: 'red',
};

const REFERRAL_STATUSES = [
  'pending_clinic_accept',
  'accepted',
  'rejected',
  'completed',
  'cancelled',
] as const;

type CreateForm = {
  clinicTenantId: string;
  pharmacyCustomerId: string;
  patientDisplayName: string;
  patientPhone?: string;
  reason?: string;
  notes?: string;
  doctorId?: string;
};

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

function matchesReferralFilter(
  row: ConnectReferral,
  opts: {
    search: string;
    status?: string;
    partnerTenantId?: string;
    isPharmacy: boolean;
  },
): boolean {
  if (opts.status && row.referralStatus !== opts.status) return false;
  if (opts.partnerTenantId) {
    const partnerId = opts.isPharmacy ? row.clinicTenantId : row.pharmacyTenantId;
    if (partnerId !== opts.partnerTenantId) return false;
  }
  const q = opts.search.trim().toLowerCase();
  if (!q) return true;
  const hay = [
    row.patientDisplayName,
    row.patientPhone,
    row.reason,
    row.notes,
    row.doctorFullName,
    row.clinicTenantName,
    row.clinicTenantCode,
    row.pharmacyTenantName,
    row.pharmacyTenantCode,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return hay.includes(q);
}

export function ConnectReferralsPage() {
  const { t } = useTranslation('connect');
  const navigate = useNavigate();
  const [profile, setProfile] = useState<ConnectOrgProfile | null>(null);
  const [items, setItems] = useState<ConnectReferral[]>([]);
  const [inbox, setInbox] = useState<ConnectReferral[]>([]);
  const [clinics, setClinics] = useState<ConnectOrgLink[]>([]);
  const [doctors, setDoctors] = useState<ConnectDoctor[]>([]);
  const [customerOptions, setCustomerOptions] = useState<
    { value: string; label: string; name: string; phone?: string }[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>();
  const [partnerFilter, setPartnerFilter] = useState<string>();
  const [form] = Form.useForm<CreateForm>();
  const clinicId = Form.useWatch('clinicTenantId', form);
  const isPharmacy = profile?.orgKind === 'pharmacy';
  const isClinic = profile?.orgKind === 'clinic';

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const me = await fetchConnectOrgProfile();
      setProfile(me);
      const list = await fetchConnectReferrals();
      setItems(list);
      if (me?.orgKind === 'clinic') {
        setInbox(await fetchConnectReferralInbox());
      } else {
        setInbox([]);
      }
      if (me?.orgKind === 'pharmacy') {
        const links = await fetchConnectOrgLinks('active');
        setClinics(links.filter((l) => l.partnerOrgRole === 'clinic'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('referrals.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!clinicId || !isPharmacy) {
      setDoctors([]);
      return;
    }
    void fetchPartnerClinicDoctors(clinicId)
      .then(setDoctors)
      .catch(() => setDoctors([]));
  }, [clinicId, isPharmacy]);

  const filterOpts = useMemo(
    () => ({
      search,
      status: statusFilter,
      partnerTenantId: partnerFilter,
      isPharmacy: Boolean(isPharmacy),
    }),
    [search, statusFilter, partnerFilter, isPharmacy],
  );

  const filteredItems = useMemo(
    () => items.filter((row) => matchesReferralFilter(row, filterOpts)),
    [items, filterOpts],
  );

  const filteredInbox = useMemo(
    () => inbox.filter((row) => matchesReferralFilter(row, filterOpts)),
    [inbox, filterOpts],
  );

  const partnerOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of items) {
      if (isPharmacy) {
        map.set(row.clinicTenantId, `${row.clinicTenantName} (${row.clinicTenantCode})`);
      } else {
        map.set(row.pharmacyTenantId, `${row.pharmacyTenantName} (${row.pharmacyTenantCode})`);
      }
    }
    return [...map.entries()]
      .map(([value, label]) => ({ value, label }))
      .sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [items, isPharmacy]);

  const searchCustomers = async (q: string) => {
    try {
      const result = await fetchCustomers({ search: q || undefined, page: 1, pageSize: 20 });
      setCustomerOptions(
        result.items.map((c) => ({
          value: c.id,
          label: `${c.fullName}${c.phone ? ` · ${c.phone}` : ''}${c.customerCode ? ` (${c.customerCode})` : ''}`,
          name: c.fullName,
          phone: c.phone,
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
      await createConnectReferral({
        clinicTenantId: values.clinicTenantId,
        pharmacyCustomerId: values.pharmacyCustomerId,
        patientDisplayName: values.patientDisplayName,
        patientPhone: values.patientPhone,
        reason: values.reason,
        notes: values.notes,
        doctorId: values.doctorId || undefined,
      });
      message.success(t('referrals.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('referrals.createFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onAccept = async (referralId: string) => {
    try {
      await acceptConnectReferral(referralId);
      message.success(t('referrals.acceptSuccess'));
      await load();
      // Continuity: BN đã vào CRM — mở đặt lịch để hiện trên Clinic → Lịch hẹn
      navigate(`/connect/bookings?referralId=${encodeURIComponent(referralId)}`);
    } catch (error) {
      message.error(apiErrorMessage(error, t('referrals.actionFailed')));
    }
  };

  const runAction = async (action: () => Promise<unknown>, successKey: string) => {
    try {
      await action();
      message.success(t(successKey));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('referrals.actionFailed')));
    }
  };

  const columns: ColumnsType<ConnectReferral> = [
    {
      title: colTitle(<UserOutlined />, t('referrals.colPatient')),
      key: 'patient',
      width: '28%',
      ellipsis: true,
      render: (_, row) => {
        const reason = row.reason?.trim();
        return (
          <div style={{ minWidth: 0 }}>
            <Space size={6} wrap={false} style={{ maxWidth: '100%' }}>
              <Typography.Text strong ellipsis style={{ maxWidth: 160 }}>
                {row.patientDisplayName}
              </Typography.Text>
              {row.pharmacyCustomerId || row.clinicCustomerId ? (
                <Tag style={{ marginInlineEnd: 0, fontSize: 11, lineHeight: '18px' }}>
                  {row.clinicCustomerId ? 'BN PK' : 'KH NT'}
                </Tag>
              ) : null}
            </Space>
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }} ellipsis>
              {[row.patientPhone, reason ? `${t('referrals.reasonLabel')}: ${reason}` : null]
                .filter(Boolean)
                .join(' · ') || '—'}
            </Typography.Text>
          </div>
        );
      },
    },
    {
      title: colTitle(<FlagOutlined />, t('referrals.colStatus')),
      dataIndex: 'referralStatus',
      width: '12%',
      align: 'center',
      render: (s: string) => (
        <Tag color={STATUS_COLOR[s] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`referrals.status.${s}`, { defaultValue: s })}
        </Tag>
      ),
    },
    {
      title: colTitle(
        isPharmacy ? <BankOutlined /> : <ShopOutlined />,
        isPharmacy ? t('referrals.colClinic') : t('referrals.colPharmacy'),
      ),
      key: 'partner',
      width: '26%',
      ellipsis: true,
      render: (_, row) => {
        const name = isPharmacy ? row.clinicTenantName : row.pharmacyTenantName;
        const code = isPharmacy ? row.clinicTenantCode : row.pharmacyTenantCode;
        return (
          <Tooltip title={`${name} (${code})`}>
            <div style={{ minWidth: 0 }}>
              <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
                {name}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
                {code}
              </Typography.Text>
            </div>
          </Tooltip>
        );
      },
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('referrals.colDoctor')),
      key: 'doctor',
      width: '18%',
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
      title: t('referrals.colActions'),
      key: 'actions',
      width: '16%',
      align: 'right',
      render: (_, row) => {
        const actions: ReactNode[] = [];
        if (isClinic && row.referralStatus === 'pending_clinic_accept') {
          actions.push(
            <Tooltip key="accept" title={t('referrals.accept')}>
              <Button
                size="small"
                type="primary"
                icon={<UserAddOutlined />}
                onClick={() => void onAccept(row.id)}
              >
                {t('referrals.accept')}
              </Button>
            </Tooltip>,
            <Tooltip key="reject" title={t('referrals.reject')}>
              <Button
                size="small"
                icon={<CloseOutlined />}
                onClick={() =>
                  void runAction(() => rejectConnectReferral(row.id), 'referrals.rejectSuccess')
                }
              />
            </Tooltip>,
          );
        }
        if (isClinic && row.referralStatus === 'accepted') {
          actions.push(
            <Tooltip key="book" title={t('referrals.book')}>
              <Button
                size="small"
                type="primary"
                icon={<CalendarOutlined />}
                onClick={() =>
                  navigate(`/connect/bookings?referralId=${encodeURIComponent(row.id)}`)
                }
              >
                {t('referrals.book')}
              </Button>
            </Tooltip>,
            <Tooltip key="complete" title={t('referrals.complete')}>
              <Button
                size="small"
                icon={<CheckCircleOutlined />}
                onClick={() =>
                  void runAction(
                    () => completeConnectReferral(row.id),
                    'referrals.completeSuccess',
                  )
                }
              />
            </Tooltip>,
          );
        }
        if (isPharmacy && row.referralStatus === 'pending_clinic_accept') {
          actions.push(
            <Tooltip key="cancel" title={t('referrals.cancel')}>
              <Button
                size="small"
                danger
                icon={<StopOutlined />}
                onClick={() =>
                  void runAction(() => cancelConnectReferral(row.id), 'referrals.cancelSuccess')
                }
              >
                {t('referrals.cancel')}
              </Button>
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
              <UserAddOutlined />
              {t('referrals.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('referrals.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('referrals.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: 240 }}
          />
          <Select
            allowClear
            placeholder={t('referrals.filterStatus')}
            suffixIcon={<FilterOutlined />}
            style={{ minWidth: 150 }}
            value={statusFilter}
            onChange={(value) => setStatusFilter(value)}
            options={REFERRAL_STATUSES.map((key) => ({
              value: key,
              label: t(`referrals.status.${key}`),
            }))}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('referrals.filterPartner')}
            suffixIcon={isPharmacy ? <BankOutlined /> : <ShopOutlined />}
            style={{ minWidth: 200 }}
            value={partnerFilter}
            onChange={(value) => setPartnerFilter(value)}
            options={partnerOptions}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('referrals.refresh')}
          </Button>
          {isPharmacy ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                form.resetFields();
                void searchCustomers('');
                setCreateOpen(true);
              }}
            >
              {t('referrals.create')}
            </Button>
          ) : null}
        </Space>
      </div>

      {isClinic && inbox.length > 0 ? (
        <Card title={t('referrals.inboxTitle')} size="small">
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={filteredInbox}
            columns={columns}
            tableLayout="fixed"
            locale={{ emptyText: t('referrals.filterEmpty') }}
          />
        </Card>
      ) : null}

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
                ? t('referrals.filterEmpty')
                : t('referrals.empty'),
          }}
        />
      </Card>

      <Modal
        title={
          <Space>
            <PlusOutlined />
            {t('referrals.createTitle')}
          </Space>
        }
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        confirmLoading={saving}
        destroyOnClose
        footer={[
          <Button key="cancel" icon={<CloseOutlined />} onClick={() => setCreateOpen(false)}>
            {t('referrals.cancel')}
          </Button>,
          <Button
            key="ok"
            type="primary"
            icon={<PlusOutlined />}
            loading={saving}
            onClick={() => void onCreate()}
          >
            {t('referrals.create')}
          </Button>,
        ]}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="clinicTenantId"
            label={t('referrals.clinic')}
            rules={[{ required: true, message: t('referrals.clinicRequired') }]}
          >
            <Select
              showSearch
              options={clinics.map((c) => ({
                value: c.partnerTenantId,
                label: `${c.partnerTenantName} (${c.partnerTenantCode})`,
              }))}
              filterOption={(input, option) =>
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
            />
          </Form.Item>
          <Form.Item
            name="pharmacyCustomerId"
            label={t('referrals.customer')}
            rules={[{ required: true, message: t('referrals.customerRequired') }]}
            extra={t('referrals.customerHint')}
          >
            <Select
              showSearch
              filterOption={false}
              onSearch={(q) => void searchCustomers(q)}
              options={customerOptions}
              placeholder={t('referrals.customerPlaceholder')}
              onChange={(id) => {
                const hit = customerOptions.find((c) => c.value === id);
                if (hit) {
                  form.setFieldsValue({
                    patientDisplayName: hit.name,
                    patientPhone: hit.phone,
                  });
                }
              }}
            />
          </Form.Item>
          <Form.Item
            name="patientDisplayName"
            label={t('referrals.patientName')}
            rules={[{ required: true, message: t('referrals.patientNameRequired') }]}
          >
            <Input disabled />
          </Form.Item>
          <Form.Item name="patientPhone" label={t('referrals.patientPhone')}>
            <Input disabled />
          </Form.Item>
          <Form.Item name="doctorId" label={t('referrals.doctor')}>
            <Select
              allowClear
              placeholder={t('referrals.doctorOptional')}
              options={doctors.map((d) => ({
                value: d.id,
                label: `${d.fullName} (${d.phone})`,
              }))}
            />
          </Form.Item>
          <Form.Item name="reason" label={t('referrals.reason')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="notes" label={t('referrals.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
