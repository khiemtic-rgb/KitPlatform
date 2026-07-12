import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Drawer,
  Form,
  Input,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  MedicineBoxOutlined,
  IdcardOutlined,
  FlagOutlined,
  EditOutlined,
  CheckOutlined,
  StopOutlined,
  TeamOutlined,
  NumberOutlined,
  ApartmentOutlined,
  FilterOutlined,
  CloseOutlined,
  LinkOutlined,
  SaveOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createClinicProvider,
  fetchClinicProviders,
  updateClinicProvider,
  upsertClinicProviderFromConnect,
  type ClinicProvider,
} from '@/shared/api/clinic.api';
import { fetchClinicMemberships, type ConnectDoctorMembership } from '@/shared/api/connect.api';
import { clinicSpecialtySelectOptions, normalizeClinicSpecialty } from '@/modules/clinic/clinic-specialties';
import { connectBadgeForProvider } from '@/modules/clinic/provider-connect-match';

type FormValues = {
  providerCode: string;
  displayName: string;
  specialty?: string;
  licenseNo?: string;
  status: number;
  connectDoctorId?: string;
  phone?: string;
  email?: string;
  title?: string;
  notes?: string;
};

const PROVIDER_TITLE_OPTIONS = [
  { value: 'BS', label: 'BS' },
  { value: 'ThS.BS', label: 'ThS.BS' },
  { value: 'TS.BS', label: 'TS.BS' },
  { value: 'PGS.TS.BS', label: 'PGS.TS.BS' },
  { value: 'GS.TS.BS', label: 'GS.TS.BS' },
];

type StatusTab = 'active' | 'inactive' | 'all';

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

export function ClinicProvidersPage() {
  const { t } = useTranslation('clinic');
  const [items, setItems] = useState<ClinicProvider[]>([]);
  const [memberships, setMemberships] = useState<ConnectDoctorMembership[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusTab, setStatusTab] = useState<StatusTab>('active');
  const [search, setSearch] = useState('');
  const [specialtyFilter, setSpecialtyFilter] = useState<string>();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ClinicProvider | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<FormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [providers, mem] = await Promise.all([
        fetchClinicProviders(true),
        fetchClinicMemberships('active').catch(() => [] as ConnectDoctorMembership[]),
      ]);
      setItems(providers);
      setMemberships(mem);
    } catch (error) {
      message.error(apiErrorMessage(error, t('providers.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const specialtyOptions = useMemo(
    () => clinicSpecialtySelectOptions(items.map((r) => r.specialty)),
    [items],
  );

  const formSpecialtyOptions = useMemo(
    () => clinicSpecialtySelectOptions([editing?.specialty, ...items.map((r) => r.specialty)]),
    [items, editing],
  );

  const activeBase = useMemo(() => items.filter((r) => r.status === 1), [items]);
  const inactiveBase = useMemo(() => items.filter((r) => r.status !== 1), [items]);

  const applyFilters = useCallback(
    (list: ClinicProvider[]) => {
      const q = search.trim().toLowerCase();
      return list.filter((row) => {
        if (specialtyFilter && (row.specialty?.trim() || '') !== specialtyFilter) return false;
        if (!q) return true;
        const hay = [row.providerCode, row.displayName, row.specialty, row.licenseNo]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return hay.includes(q);
      });
    },
    [search, specialtyFilter],
  );

  const activeItems = useMemo(() => applyFilters(activeBase), [applyFilters, activeBase]);
  const inactiveItems = useMemo(() => applyFilters(inactiveBase), [applyFilters, inactiveBase]);
  const allItems = useMemo(() => applyFilters(items), [applyFilters, items]);
  const tableItems =
    statusTab === 'active' ? activeItems : statusTab === 'inactive' ? inactiveItems : allItems;

  const hasFilters = search.trim().length > 0 || !!specialtyFilter;

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      providerCode: '',
      displayName: '',
      specialty: undefined,
      licenseNo: undefined,
      status: 1,
      connectDoctorId: undefined,
      phone: undefined,
      email: undefined,
      title: undefined,
      notes: undefined,
    });
    setOpen(true);
  };

  const openEdit = (row: ClinicProvider) => {
    setEditing(row);
    form.setFieldsValue({
      providerCode: row.providerCode,
      displayName: row.displayName,
      specialty: normalizeClinicSpecialty(row.specialty) ?? row.specialty,
      licenseNo: row.licenseNo,
      status: row.status,
      connectDoctorId: row.connectDoctorId,
      phone: row.phone,
      email: row.email,
      title: row.title,
      notes: row.notes,
    });
    setOpen(true);
  };

  const onImportConnect = async (doctorId: string) => {
    setSaving(true);
    try {
      await upsertClinicProviderFromConnect({ connectDoctorId: doctorId });
      message.success(t('providers.fromConnectSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('providers.fromConnectFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      if (editing) {
        await updateClinicProvider(editing.id, {
          displayName: values.displayName,
          specialty: values.specialty,
          licenseNo: values.licenseNo,
          status: values.status,
          connectDoctorId: values.connectDoctorId || undefined,
          clearConnectDoctorId: !values.connectDoctorId,
          phone: values.phone,
          email: values.email,
          title: values.title,
          notes: values.notes,
        });
        message.success(t('providers.updateSuccess'));
      } else {
        await createClinicProvider({
          providerCode: values.providerCode,
          displayName: values.displayName,
          specialty: values.specialty,
          licenseNo: values.licenseNo,
          status: values.status,
          connectDoctorId: values.connectDoctorId,
          phone: values.phone,
          email: values.email,
          title: values.title,
          notes: values.notes,
        });
        message.success(t('providers.createSuccess'));
      }
      setOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('providers.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ClinicProvider> = [
    {
      title: colTitle(<NumberOutlined />, t('providers.colCode')),
      dataIndex: 'providerCode',
      width: '12%',
      ellipsis: true,
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('providers.colName')),
      dataIndex: 'displayName',
      width: '26%',
      ellipsis: true,
      render: (v: string, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.title ? `${row.title} ${v}` : v}
          </Typography.Text>
          {row.phone ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.phone}
            </Typography.Text>
          ) : null}
          {connectBadgeForProvider(row, memberships) ? (
            <Tag color="blue" style={{ marginTop: 4, marginInlineEnd: 0 }}>
              {connectBadgeForProvider(row, memberships)}
            </Tag>
          ) : null}
        </div>
      ),
    },
    {
      title: colTitle(<ApartmentOutlined />, t('providers.colSpecialty')),
      dataIndex: 'specialty',
      width: '18%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: colTitle(<IdcardOutlined />, t('providers.colLicense')),
      dataIndex: 'licenseNo',
      width: '18%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: colTitle(<FlagOutlined />, t('providers.colStatus')),
      dataIndex: 'status',
      width: '14%',
      align: 'center',
      render: (s: number) => (
        <Tag color={s === 1 ? 'green' : 'default'} style={{ marginInlineEnd: 0 }}>
          {s === 1 ? t('providers.active') : t('providers.inactive')}
        </Tag>
      ),
    },
    {
      title: t('providers.colActions'),
      key: 'actions',
      width: '12%',
      align: 'right',
      render: (_, row) => (
        <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
          {t('providers.edit')}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <TeamOutlined />
              {t('providers.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('providers.subtitle')}</Typography.Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('providers.refresh')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('providers.create')}
          </Button>
        </Space>
      </div>

      {memberships.length > 0 ? (
        <Alert
          type="info"
          showIcon
          message={t('providers.connectHint')}
          action={
            <Select
              placeholder={t('providers.fromConnect')}
              style={{ minWidth: 220 }}
              options={memberships
                .filter((m) => m.membershipStatus === 'active')
                .filter((m) => !items.some((p) => p.connectDoctorId === m.doctorId))
                .map((m) => ({
                  value: m.doctorId,
                  label: m.doctorSpecialty
                    ? `${m.doctorFullName} · ${m.doctorSpecialty}`
                    : m.doctorFullName,
                }))}
              onChange={(id) => void onImportConnect(id)}
              suffixIcon={<LinkOutlined />}
            />
          }
        />
      ) : null}

      <Card styles={{ body: { paddingTop: 12 } }}>
        <Tabs
          type="card"
          activeKey={statusTab}
          onChange={(key) => setStatusTab(key as StatusTab)}
          items={[
            {
              key: 'active',
              label: (
                <Space size={8}>
                  <CheckOutlined />
                  <span>{t('providers.tabActive')}</span>
                  <Badge
                    count={activeItems.length}
                    showZero
                    color={statusTab === 'active' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'inactive',
              label: (
                <Space size={8}>
                  <StopOutlined />
                  <span>{t('providers.tabInactive')}</span>
                  <Badge
                    count={inactiveItems.length}
                    showZero
                    color={statusTab === 'inactive' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
            {
              key: 'all',
              label: (
                <Space size={8}>
                  <TeamOutlined />
                  <span>{t('providers.tabAll')}</span>
                  <Badge
                    count={allItems.length}
                    showZero
                    color={statusTab === 'all' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
            },
          ]}
        />

        <Space wrap style={{ marginBottom: 12, width: '100%' }}>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('providers.searchPlaceholder')}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: 260 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('providers.filterSpecialty')}
            suffixIcon={<FilterOutlined />}
            style={{ minWidth: 180 }}
            value={specialtyFilter}
            onChange={(value) => setSpecialtyFilter(value)}
            options={specialtyOptions}
          />
          {hasFilters ? (
            <Button
              type="link"
              onClick={() => {
                setSearch('');
                setSpecialtyFilter(undefined);
              }}
            >
              {t('providers.filterClear')}
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
          locale={{
            emptyText: hasFilters ? t('providers.filterEmpty') : t('providers.empty'),
          }}
        />
      </Card>

      <Drawer
        title={editing ? t('providers.editTitle') : t('providers.createTitle')}
        width={760}
        open={open}
        onClose={() => setOpen(false)}
        destroyOnClose
        extra={
          <Space>
            <Button icon={<CloseOutlined />} onClick={() => setOpen(false)}>
              {t('providers.cancel')}
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={saving}
              onClick={() => void onSave()}
            >
              {editing ? t('providers.save') : t('providers.create')}
            </Button>
          </Space>
        }
      >
        <Form
          form={form}
          layout="vertical"
          style={{
            display: 'grid',
            gridTemplateColumns: '1fr 1fr',
            columnGap: 16,
            rowGap: 0,
          }}
        >
          <Form.Item
            name="providerCode"
            label={t('providers.code')}
            rules={[{ required: true, message: t('providers.codeRequired') }]}
          >
            <Input disabled={!!editing} placeholder="BS01" />
          </Form.Item>
          <Form.Item
            name="displayName"
            label={t('providers.name')}
            rules={[{ required: true, message: t('providers.nameRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="title" label={t('providers.degreeTitle')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('providers.degreeTitlePlaceholder')}
              options={PROVIDER_TITLE_OPTIONS}
            />
          </Form.Item>
          <Form.Item name="specialty" label={t('providers.specialty')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('providers.specialtyPlaceholder')}
              options={formSpecialtyOptions}
            />
          </Form.Item>
          <Form.Item name="phone" label={t('providers.phone')}>
            <Input placeholder={t('providers.phonePlaceholder')} />
          </Form.Item>
          <Form.Item name="email" label={t('providers.email')}>
            <Input placeholder={t('providers.emailPlaceholder')} />
          </Form.Item>
          <Form.Item name="licenseNo" label={t('providers.license')}>
            <Input placeholder="CCHN..." />
          </Form.Item>
          <Form.Item name="status" label={t('providers.status')} initialValue={1}>
            <Select
              options={[
                { value: 1, label: t('providers.active') },
                { value: 0, label: t('providers.inactive') },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="connectDoctorId"
            label={t('providers.connectDoctor')}
            style={{ gridColumn: '1 / -1' }}
          >
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('providers.connectDoctorPlaceholder')}
              options={memberships
                .filter((m) => m.membershipStatus === 'active')
                .map((m) => ({
                  value: m.doctorId,
                  label: [
                    m.doctorFullName,
                    m.doctorPhone,
                    m.doctorLicenseNumber,
                    m.doctorSpecialty,
                  ]
                    .filter(Boolean)
                    .join(' · '),
                }))}
            />
          </Form.Item>
          <Form.Item
            name="notes"
            label={t('providers.notes')}
            style={{ gridColumn: '1 / -1' }}
          >
            <Input.TextArea rows={2} placeholder={t('providers.notesPlaceholder')} />
          </Form.Item>
        </Form>
      </Drawer>
    </Space>
  );
}
