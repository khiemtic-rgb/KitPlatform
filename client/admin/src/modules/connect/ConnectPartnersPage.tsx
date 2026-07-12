import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
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
  BankOutlined,
  FilterOutlined,
  MedicineBoxOutlined,
  PhoneOutlined,
  ReloadOutlined,
  SearchOutlined,
  ShopOutlined,
  TeamOutlined,
  IdcardOutlined,
  ApartmentOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchConnectOrgLinks,
  fetchConnectOrgProfile,
  fetchPartnerClinicDoctors,
  type ConnectDoctor,
  type ConnectOrgLink,
  type ConnectOrgProfile,
} from '@/shared/api/connect.api';

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

export function ConnectPartnersPage() {
  const { t } = useTranslation('connect');
  const [items, setItems] = useState<ConnectOrgLink[]>([]);
  const [profile, setProfile] = useState<ConnectOrgProfile | null>(null);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<ConnectOrgLink | null>(null);
  const [doctors, setDoctors] = useState<ConnectDoctor[]>([]);
  const [doctorsLoading, setDoctorsLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('partners');
  const [partnerSearch, setPartnerSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>();
  const [doctorSearch, setDoctorSearch] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [links, me] = await Promise.all([
        fetchConnectOrgLinks('active'),
        fetchConnectOrgProfile(),
      ]);
      setItems(links);
      setProfile(me);
    } catch (error) {
      message.error(apiErrorMessage(error, t('partners.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  const isPharmacy = profile?.orgKind === 'pharmacy';

  useEffect(() => {
    void load();
  }, [load]);

  const loadDoctors = useCallback(
    async (partner: ConnectOrgLink) => {
      if (partner.partnerOrgRole !== 'clinic') {
        message.warning(t('partners.clinicOnlyHint'));
        return;
      }
      setSelected(partner);
      setActiveTab('doctors');
      setDoctorSearch('');
      setDoctorsLoading(true);
      try {
        setDoctors(await fetchPartnerClinicDoctors(partner.partnerTenantId));
      } catch (error) {
        setDoctors([]);
        message.error(apiErrorMessage(error, t('partners.doctorsLoadFailed')));
      } finally {
        setDoctorsLoading(false);
      }
    },
    [t],
  );

  const clinicPartners = useMemo(
    () =>
      items
        .filter((row) => row.partnerOrgRole === 'clinic')
        .slice()
        .sort((a, b) =>
          (a.partnerTenantName || a.partnerTenantCode).localeCompare(
            b.partnerTenantName || b.partnerTenantCode,
            'vi',
          ),
        ),
    [items],
  );

  const onClinicFilterChange = (partnerId?: string) => {
    if (!partnerId) {
      setSelected(null);
      setDoctors([]);
      setDoctorSearch('');
      return;
    }
    const partner = clinicPartners.find((p) => p.partnerTenantId === partnerId);
    if (partner) void loadDoctors(partner);
  };

  const filteredPartners = useMemo(() => {
    const q = partnerSearch.trim().toLowerCase();
    return items.filter((row) => {
      if (roleFilter && row.partnerOrgRole !== roleFilter) return false;
      if (!q) return true;
      const hay = [row.partnerTenantName, row.partnerTenantCode, row.ourOrgRole, row.partnerOrgRole]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return hay.includes(q);
    });
  }, [items, partnerSearch, roleFilter]);

  const filteredDoctors = useMemo(() => {
    const q = doctorSearch.trim().toLowerCase();
    if (!q) return doctors;
    return doctors.filter((d) =>
      [d.fullName, d.phone, d.licenseNumber, d.specialty]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()
        .includes(q),
    );
  }, [doctors, doctorSearch]);

  const partnerColumns: ColumnsType<ConnectOrgLink> = [
    {
      title: colTitle(<ApartmentOutlined />, t('partners.colPartner')),
      key: 'partner',
      width: '42%',
      ellipsis: true,
      render: (_, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.partnerTenantName}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.partnerTenantCode}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<TeamOutlined />, t('partners.colRoles')),
      key: 'roles',
      width: '38%',
      render: (_, row) => (
        <Space size={6} wrap>
          <Tag icon={row.ourOrgRole === 'pharmacy' ? <ShopOutlined /> : <BankOutlined />}>
            {t(`network.roles.${row.ourOrgRole}`, { defaultValue: row.ourOrgRole })}
          </Tag>
          <Typography.Text type="secondary">→</Typography.Text>
          <Tag
            color={row.partnerOrgRole === 'clinic' ? 'blue' : 'default'}
            icon={row.partnerOrgRole === 'pharmacy' ? <ShopOutlined /> : <BankOutlined />}
          >
            {t(`network.roles.${row.partnerOrgRole}`, { defaultValue: row.partnerOrgRole })}
          </Tag>
        </Space>
      ),
    },
    {
      title: t('partners.colActions'),
      key: 'actions',
      width: '20%',
      align: 'right',
      render: (_, row) =>
        row.partnerOrgRole === 'clinic' ? (
          <Button
            size="small"
            type={selected?.id === row.id ? 'primary' : 'default'}
            icon={<EyeOutlined />}
            onClick={() => void loadDoctors(row)}
          >
            {t('partners.viewDoctors')}
          </Button>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
  ];

  const doctorColumns: ColumnsType<ConnectDoctor> = [
    {
      title: colTitle(<MedicineBoxOutlined />, t('partners.colDoctor')),
      dataIndex: 'fullName',
      width: '32%',
      ellipsis: true,
      render: (v: string) => (
        <Typography.Text strong ellipsis style={{ display: 'block', maxWidth: '100%' }}>
          {v}
        </Typography.Text>
      ),
    },
    {
      title: colTitle(<PhoneOutlined />, t('partners.colPhone')),
      dataIndex: 'phone',
      width: '22%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: colTitle(<IdcardOutlined />, t('partners.colLicense')),
      dataIndex: 'licenseNumber',
      width: '22%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: colTitle(<ApartmentOutlined />, t('partners.colSpecialty')),
      dataIndex: 'specialty',
      width: '24%',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <TeamOutlined />
              {t('partners.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('partners.subtitle')}</Typography.Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('partners.refresh')}
        </Button>
      </div>

      {isPharmacy ? (
        <Alert type="info" showIcon message={t('partners.pharmacyDoctorsHint')} />
      ) : null}

      <Card styles={{ body: { paddingTop: 12 } }}>
        <Tabs
          type="card"
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: 'partners',
              label: (
                <Space size={8}>
                  <ApartmentOutlined />
                  <span>{t('partners.tabPartners')}</span>
                  <Badge
                    count={filteredPartners.length}
                    showZero
                    color={activeTab === 'partners' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Space direction="vertical" style={{ width: '100%' }} size={12}>
                  <Space wrap>
                    <Input
                      allowClear
                      prefix={<SearchOutlined />}
                      placeholder={t('partners.searchPlaceholder')}
                      value={partnerSearch}
                      onChange={(e) => setPartnerSearch(e.target.value)}
                      style={{ width: 260 }}
                    />
                    <Select
                      allowClear
                      placeholder={t('partners.filterRole')}
                      suffixIcon={<FilterOutlined />}
                      style={{ minWidth: 160 }}
                      value={roleFilter}
                      onChange={(value) => setRoleFilter(value)}
                      options={[
                        { value: 'clinic', label: t('network.roles.clinic') },
                        { value: 'pharmacy', label: t('network.roles.pharmacy') },
                      ]}
                    />
                  </Space>
                  <Table
                    rowKey="id"
                    loading={loading}
                    dataSource={filteredPartners}
                    columns={partnerColumns}
                    tableLayout="fixed"
                    pagination={{ pageSize: 20 }}
                    rowClassName={(row) => (selected?.id === row.id ? 'ant-table-row-selected' : '')}
                    locale={{
                      emptyText:
                        items.length > 0 && filteredPartners.length === 0
                          ? t('partners.filterEmpty')
                          : t('partners.empty'),
                    }}
                  />
                </Space>
              ),
            },
            {
              key: 'doctors',
              label: (
                <Space size={8}>
                  <MedicineBoxOutlined />
                  <span>{t('partners.tabDoctors')}</span>
                  <Badge
                    count={selected ? filteredDoctors.length : 0}
                    showZero
                    color={activeTab === 'doctors' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Space direction="vertical" style={{ width: '100%' }} size={12}>
                  <Space wrap>
                    <Select
                      showSearch
                      allowClear
                      optionFilterProp="label"
                      placeholder={t('partners.filterClinic')}
                      suffixIcon={<BankOutlined />}
                      style={{ minWidth: 280 }}
                      value={
                        selected?.partnerOrgRole === 'clinic'
                          ? selected.partnerTenantId
                          : undefined
                      }
                      onChange={(value) => onClinicFilterChange(value)}
                      options={clinicPartners.map((p) => ({
                        value: p.partnerTenantId,
                        label: `${p.partnerTenantName} (${p.partnerTenantCode})`,
                      }))}
                      notFoundContent={t('partners.noClinicPartners')}
                    />
                    <Input
                      allowClear
                      prefix={<SearchOutlined />}
                      placeholder={t('partners.searchDoctorPlaceholder')}
                      value={doctorSearch}
                      onChange={(e) => setDoctorSearch(e.target.value)}
                      style={{ width: 260 }}
                      disabled={!selected || selected.partnerOrgRole !== 'clinic'}
                    />
                  </Space>
                  {selected?.partnerOrgRole === 'clinic' ? (
                    <Table
                      rowKey="id"
                      loading={doctorsLoading}
                      dataSource={filteredDoctors}
                      columns={doctorColumns}
                      tableLayout="fixed"
                      pagination={{ pageSize: 20 }}
                      locale={{
                        emptyText:
                          doctors.length > 0 && filteredDoctors.length === 0
                            ? t('partners.filterEmpty')
                            : t('partners.doctorsEmpty'),
                      }}
                    />
                  ) : (
                    <Typography.Text type="secondary">
                      {clinicPartners.length === 0
                        ? t('partners.noClinicPartners')
                        : t('partners.selectClinicHint')}
                    </Typography.Text>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>
    </Space>
  );
}
