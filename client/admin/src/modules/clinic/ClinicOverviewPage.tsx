import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Col,
  Row,
  Space,
  Statistic,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  FileTextOutlined,
  LoginOutlined,
  MedicineBoxOutlined,
  PhoneOutlined,
  ReloadOutlined,
  SendOutlined,
  SolutionOutlined,
  SettingOutlined,
  TeamOutlined,
  UserDeleteOutlined,
  UserOutlined,
  ApartmentOutlined,
  LinkOutlined,
  RocketOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchClinicDaySummary, type ClinicDaySummary } from '@/shared/api/clinic.api';

type StatTone = 'default' | 'blue' | 'orange' | 'green' | 'purple' | 'red';

const TONE: Record<
  StatTone,
  { iconBg: string; iconColor: string; valueColor?: string }
> = {
  default: { iconBg: 'rgba(0,0,0,0.04)', iconColor: 'rgba(0,0,0,0.45)' },
  blue: { iconBg: 'rgba(22,119,255,0.08)', iconColor: '#1677ff', valueColor: '#1677ff' },
  orange: { iconBg: 'rgba(250,140,22,0.1)', iconColor: '#fa8c16', valueColor: '#d46b08' },
  green: { iconBg: 'rgba(82,196,26,0.1)', iconColor: '#52c41a', valueColor: '#389e0d' },
  purple: { iconBg: 'rgba(114,46,209,0.08)', iconColor: '#722ed1', valueColor: '#531dab' },
  red: { iconBg: 'rgba(255,77,79,0.08)', iconColor: '#ff4d4f', valueColor: '#cf1322' },
};

function StatTile({
  title,
  value,
  icon,
  tone = 'default',
  onClick,
}: {
  title: string;
  value: number;
  icon: ReactNode;
  tone?: StatTone;
  onClick?: () => void;
}) {
  const palette = TONE[tone];
  return (
    <div
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      onClick={onClick}
      onKeyDown={
        onClick
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      style={{
        display: 'flex',
        gap: 12,
        alignItems: 'flex-start',
        padding: '14px 16px',
        borderRadius: 10,
        border: '1px solid rgba(0,0,0,0.06)',
        background: '#fff',
        cursor: onClick ? 'pointer' : 'default',
        height: '100%',
        transition: 'border-color 0.15s, box-shadow 0.15s',
      }}
      onMouseEnter={(e) => {
        if (!onClick) return;
        e.currentTarget.style.borderColor = 'rgba(22,119,255,0.35)';
        e.currentTarget.style.boxShadow = '0 2px 8px rgba(22,119,255,0.08)';
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.borderColor = 'rgba(0,0,0,0.06)';
        e.currentTarget.style.boxShadow = 'none';
      }}
    >
      <span
        style={{
          width: 36,
          height: 36,
          borderRadius: 8,
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: palette.iconBg,
          color: palette.iconColor,
          fontSize: 16,
          flexShrink: 0,
        }}
      >
        {icon}
      </span>
      <Statistic
        title={<span style={{ fontSize: 13 }}>{title}</span>}
        value={value}
        valueStyle={{ fontSize: 24, fontWeight: 600, color: palette.valueColor, lineHeight: 1.2 }}
      />
    </div>
  );
}

export function ClinicOverviewPage() {
  const { t } = useTranslation('clinic', { keyPrefix: 'overview' });
  const navigate = useNavigate();
  const [summary, setSummary] = useState<ClinicDaySummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState('appointments');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSummary(await fetchClinicDaySummary());
    } catch (error) {
      message.error(apiErrorMessage(error, t('summaryFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const dateLabel = useMemo(() => {
    const raw = summary?.date;
    const d = raw && dayjs(raw).isValid() ? dayjs(raw) : dayjs();
    return d.format('DD/MM/YYYY');
  }, [summary?.date]);

  const apptTotal =
    (summary?.appointmentsToday ?? 0) +
    (summary?.appointmentsRemoteToday ?? 0) +
    (summary?.appointmentsCheckedIn ?? 0) +
    (summary?.appointmentsNoShow ?? 0);
  const visitTotal = (summary?.visitsOpen ?? 0) + (summary?.visitsClosed ?? 0);
  const rxTotal =
    (summary?.prescriptionsDraft ?? 0) +
    (summary?.prescriptionsFinalized ?? 0) +
    (summary?.prescriptionsSentToPharmacy ?? 0);

  const quickLinks = [
    { to: '/clinic/patients', label: t('linkPatients'), icon: <UserOutlined /> },
    { to: '/clinic/providers', label: t('linkProviders'), icon: <MedicineBoxOutlined /> },
    { to: '/clinic/appointments', label: t('linkAppointments'), icon: <CalendarOutlined /> },
    { to: '/clinic/visits', label: t('linkVisits'), icon: <SolutionOutlined /> },
    { to: '/clinic/settings', label: t('linkSettings'), icon: <SettingOutlined /> },
    { to: '/connect/bookings', label: t('linkBookings'), icon: <LinkOutlined /> },
    { to: '/connect/referrals', label: t('linkReferrals'), icon: <ApartmentOutlined /> },
    { to: '/connect/status', label: t('linkStatus'), icon: <SendOutlined /> },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <MedicineBoxOutlined />
              {t('title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('subtitle')}</Typography.Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('refresh')}
        </Button>
      </div>

      <Alert type="info" showIcon message={t('banner')} />

      <Card
        loading={loading && !summary}
        styles={{ body: { paddingTop: 12 } }}
        title={
          <Space size={8}>
            <CalendarOutlined />
            <span>{t('todayCard')}</span>
            <Tag style={{ marginInlineEnd: 0 }}>{dateLabel}</Tag>
          </Space>
        }
      >
        <Tabs
          type="card"
          activeKey={tab}
          onChange={setTab}
          items={[
            {
              key: 'appointments',
              label: (
                <Space size={8}>
                  <CalendarOutlined />
                  <span>{t('groupAppointments')}</span>
                  <Badge
                    count={apptTotal}
                    showZero
                    color={tab === 'appointments' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Row gutter={[12, 12]}>
                  <Col xs={12} sm={12} md={6}>
                    <StatTile
                      title={t('statAppointments')}
                      value={summary?.appointmentsToday ?? 0}
                      icon={<CalendarOutlined />}
                      tone="blue"
                      onClick={() => navigate('/clinic/appointments')}
                    />
                  </Col>
                  <Col xs={12} sm={12} md={6}>
                    <StatTile
                      title={t('statRemote')}
                      value={summary?.appointmentsRemoteToday ?? 0}
                      icon={<PhoneOutlined />}
                      tone="purple"
                      onClick={() => navigate('/clinic/appointments')}
                    />
                  </Col>
                  <Col xs={12} sm={12} md={6}>
                    <StatTile
                      title={t('statCheckedIn')}
                      value={summary?.appointmentsCheckedIn ?? 0}
                      icon={<LoginOutlined />}
                      tone="orange"
                      onClick={() => navigate('/clinic/appointments')}
                    />
                  </Col>
                  <Col xs={12} sm={12} md={6}>
                    <StatTile
                      title={t('statNoShow')}
                      value={summary?.appointmentsNoShow ?? 0}
                      icon={<UserDeleteOutlined />}
                      tone="red"
                      onClick={() => navigate('/clinic/appointments')}
                    />
                  </Col>
                </Row>
              ),
            },
            {
              key: 'visits',
              label: (
                <Space size={8}>
                  <SolutionOutlined />
                  <span>{t('groupVisits')}</span>
                  <Badge
                    count={visitTotal}
                    showZero
                    color={tab === 'visits' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Row gutter={[12, 12]}>
                  <Col xs={12} md={12}>
                    <StatTile
                      title={t('statVisitsOpen')}
                      value={summary?.visitsOpen ?? 0}
                      icon={<MedicineBoxOutlined />}
                      tone="orange"
                      onClick={() => navigate('/clinic/visits')}
                    />
                  </Col>
                  <Col xs={12} md={12}>
                    <StatTile
                      title={t('statVisitsClosed')}
                      value={summary?.visitsClosed ?? 0}
                      icon={<CheckCircleOutlined />}
                      tone="green"
                      onClick={() => navigate('/clinic/visits')}
                    />
                  </Col>
                </Row>
              ),
            },
            {
              key: 'rx',
              label: (
                <Space size={8}>
                  <FileTextOutlined />
                  <span>{t('groupRx')}</span>
                  <Badge
                    count={rxTotal}
                    showZero
                    color={tab === 'rx' ? '#1677ff' : '#8c8c8c'}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Row gutter={[12, 12]}>
                  <Col xs={12} sm={12} md={8}>
                    <StatTile
                      title={t('statRxDraft')}
                      value={summary?.prescriptionsDraft ?? 0}
                      icon={<FileTextOutlined />}
                      tone="blue"
                      onClick={() => navigate('/clinic/visits')}
                    />
                  </Col>
                  <Col xs={12} sm={12} md={8}>
                    <StatTile
                      title={t('statRxFinal')}
                      value={summary?.prescriptionsFinalized ?? 0}
                      icon={<CheckCircleOutlined />}
                      tone="green"
                      onClick={() => navigate('/clinic/visits')}
                    />
                  </Col>
                  <Col xs={24} sm={12} md={8}>
                    <StatTile
                      title={t('statRxSent')}
                      value={summary?.prescriptionsSentToPharmacy ?? 0}
                      icon={<SendOutlined />}
                      tone="purple"
                      onClick={() => navigate('/connect/status')}
                    />
                  </Col>
                </Row>
              ),
            },
          ]}
        />
      </Card>

      <Row gutter={[12, 12]}>
        <Col xs={24} lg={16}>
          <Card
            title={
              <Space size={8}>
                <RocketOutlined />
                {t('nextCard')}
              </Space>
            }
          >
            <Space wrap size={[8, 8]} style={{ marginBottom: 12 }}>
              {quickLinks.map((item) => (
                <Link key={item.to} to={item.to}>
                  <Button icon={item.icon}>{item.label}</Button>
                </Link>
              ))}
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {t('nextBody')}
            </Typography.Paragraph>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card
            title={
              <Space size={8}>
                <TeamOutlined />
                {t('phaseCard')}
              </Space>
            }
          >
            <Tag color="green" style={{ marginBottom: 12 }}>
              {t('phase')}
            </Tag>
            <Typography.Paragraph style={{ marginBottom: 0 }}>{t('phaseBody')}</Typography.Paragraph>
          </Card>
        </Col>
      </Row>
    </Space>
  );
}
