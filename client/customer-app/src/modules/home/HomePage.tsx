import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  BellOutlined,
  CalendarOutlined,
  GiftOutlined,
  HeartOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  RobotOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { Badge, Button, Card, Col, Row, Space, Typography } from 'antd';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import {
  fetchDraftOrders,
  fetchHomeSummary,
  fetchLoyaltySummary,
  fetchMedicationAdherenceSummary,
  fetchRepurchaseSuggestions,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import axios from 'axios';
import { CUSTOMER_DRAFT_ORDER_STATUS } from '@/shared/api/customer-app.types';
import { prefetchPrimaryTabOverviews } from '@/shared/api/overview-queries';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { useCustomerNotificationCount } from '@/shared/hooks/useCustomerNotificationCount';
import { DueRemindersPanel, MissedMedicationAlert } from '@/modules/reminders/DueRemindersPanel';
import { FamilyCaregiverDuePanel } from '@/modules/reminders/FamilyCaregiverDuePanel';
import { formatPoints } from '@/shared/utils/points';

type ShortcutKey =
  | 'shortcutHealth'
  | 'shortcutReminders'
  | 'shortcutReservations'
  | 'shortcutAi'
  | 'shortcutFamily'
  | 'shortcutPoints'
  | 'shortcutChat';

type HealthStatProps = {
  label: string;
  value: string;
  valueColor?: string;
  compact?: boolean;
  loading?: boolean;
  onClick?: () => void;
};

function HealthStat({ label, value, valueColor, compact, loading, onClick }: HealthStatProps) {
  const clickable = Boolean(onClick);
  return (
    <div
      className={`home-health-stat${clickable ? ' home-health-stat--clickable' : ''}`}
      role={clickable ? 'button' : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
      onKeyDown={
        clickable
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick?.();
              }
            }
          : undefined
      }
    >
      <span className="home-health-stat-label">{label}</span>
      <span
        className={`home-health-stat-value${compact ? ' home-health-stat-value--compact' : ''}`}
        style={{
          ...(valueColor ? { color: valueColor } : undefined),
          ...(loading ? { opacity: 0.45 } : undefined),
        }}
      >
        {loading ? '…' : value}
      </span>
    </div>
  );
}

export function HomePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const profile = useAuthStore((s) => s.profile);
  const { branding } = useCustomerBranding();
  const notificationCount = useCustomerNotificationCount();

  const shortcuts = useMemo(
    () =>
      [
        { to: '/health', icon: <HeartOutlined />, labelKey: 'shortcutHealth' as ShortcutKey },
        { to: '/reminders', icon: <MedicineBoxOutlined />, labelKey: 'shortcutReminders' as ShortcutKey },
        { to: '/reservations', icon: <CalendarOutlined />, labelKey: 'shortcutReservations' as ShortcutKey },
        { to: '/ai', icon: <RobotOutlined />, labelKey: 'shortcutAi' as ShortcutKey },
        { to: '/family', icon: <TeamOutlined />, labelKey: 'shortcutFamily' as ShortcutKey },
        { to: '/loyalty', icon: <GiftOutlined />, labelKey: 'shortcutPoints' as ShortcutKey },
        { to: '/chat', icon: <MessageOutlined />, labelKey: 'shortcutChat' as ShortcutKey },
      ] as const,
    [],
  );

  const [statsLoading, setStatsLoading] = useState(true);
  const [points, setPoints] = useState(0);
  const [pendingOrders, setPendingOrders] = useState(0);
  const [repurchaseCount, setRepurchaseCount] = useState(0);
  const [adherence, setAdherence] = useState({
    dueCount: 0,
    takenToday: 0,
    scheduledToday: 0,
    missedStreakDays: 0,
    showMissedAlert: false,
  });

  const applyHomeData = useCallback(
    (
      loyalty: { programs: { pointsBalance?: number }[] },
      drafts: { status: number }[],
      repurchase: { status: string }[],
      summary: typeof adherence,
    ) => {
      setPoints(loyalty.programs[0]?.pointsBalance ?? 0);
      setPendingOrders(
        drafts.filter(
          (d) =>
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
        ).length,
      );
      setRepurchaseCount(
        repurchase.filter((r) => r.status === 'pending' || r.status === 'snoozed').length,
      );
      setAdherence(summary);
    },
    [],
  );

  const load = useCallback(async (options?: { silent?: boolean }) => {
    if (!options?.silent) setStatsLoading(true);
    try {
      try {
        const summary = await fetchHomeSummary();
        applyHomeData(
          summary.loyalty,
          summary.draftOrders,
          summary.repurchaseSuggestions,
          summary.adherence,
        );
      } catch (overviewError) {
        if (axios.isAxiosError(overviewError) && overviewError.response?.status === 404) {
          const [loyalty, drafts, repurchase, summary] = await Promise.allSettled([
            fetchLoyaltySummary(),
            fetchDraftOrders(),
            fetchRepurchaseSuggestions(),
            fetchMedicationAdherenceSummary(),
          ]);
          applyHomeData(
            loyalty.status === 'fulfilled' ? loyalty.value : { programs: [] },
            drafts.status === 'fulfilled' ? drafts.value : [],
            repurchase.status === 'fulfilled' ? repurchase.value : [],
            summary.status === 'fulfilled'
              ? summary.value
              : {
                  dueCount: 0,
                  takenToday: 0,
                  scheduledToday: 0,
                  missedStreakDays: 0,
                  showMissedAlert: false,
                },
          );
        } else {
          throw overviewError;
        }
      }
    } catch (error) {
      console.error(getApiErrorMessage(error));
    } finally {
      setStatsLoading(false);
    }
  }, [applyHomeData]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void prefetchPrimaryTabOverviews(queryClient);
  }, [queryClient]);

  const medicationDone =
    !statsLoading &&
    (adherence.scheduledToday > 0
      ? adherence.takenToday >= adherence.scheduledToday && adherence.dueCount === 0
      : adherence.dueCount === 0);

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <Typography.Title level={4} style={{ marginBottom: 2 }}>
            {t('home.greeting', { name: profile?.fullName ?? t('home.guestName') })}
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {branding.appName}
          </Typography.Text>
        </div>
        <Link to="/notifications" aria-label={t('home.notifications')}>
          <Badge count={notificationCount} size="small">
            <Button type="text" icon={<BellOutlined style={{ fontSize: 20 }} />} />
          </Badge>
        </Link>
      </div>

      <Card
        style={{
          borderRadius: 16,
          background: 'linear-gradient(145deg, #ffffff 0%, #ecfdf5 100%)',
          border: '1px solid #99f6e4',
          cursor: 'pointer',
        }}
        styles={{ body: { padding: '16px 18px' } }}
        onClick={() => navigate('/health')}
      >
        <Space align="center" style={{ marginBottom: 12, width: '100%', justifyContent: 'space-between' }}>
          <Space align="center">
            <HeartOutlined style={{ color: branding.primaryColor, fontSize: 18 }} />
            <Typography.Text strong>{t('home.healthToday')}</Typography.Text>
          </Space>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('home.healthWalletLink')}
          </Typography.Text>
        </Space>
        <Row gutter={[10, 10]} onClick={(e) => e.stopPropagation()}>
          <Col span={12}>
            <HealthStat
              label={t('home.medicationTaken')}
              loading={statsLoading}
              value={medicationDone ? '✓' : `${adherence.takenToday}/${Math.max(adherence.scheduledToday, 1)}`}
              valueColor={medicationDone ? '#059669' : '#b45309'}
            />
          </Col>
          <Col span={12}>
            <HealthStat
              label={t('home.points')}
              loading={statsLoading}
              value={formatPoints(points)}
              valueColor={branding.primaryColor}
              compact={formatPoints(points).length > 9}
              onClick={() => navigate('/loyalty')}
            />
          </Col>
          <Col span={12}>
            <HealthStat
              label={t('home.orders')}
              loading={statsLoading}
              value={String(pendingOrders)}
              onClick={() => navigate('/orders')}
            />
          </Col>
          <Col span={12}>
            <HealthStat
              label={t('home.notificationsCount')}
              loading={statsLoading}
              value={String(notificationCount)}
              onClick={() => navigate('/notifications')}
            />
          </Col>
        </Row>
        {repurchaseCount > 0 ? (
          <Button
            type="link"
            className="home-health-footer-link"
            onClick={(e) => {
              e.stopPropagation();
              navigate('/medications');
            }}
          >
            {t('home.repurchaseHint', { count: repurchaseCount })}
          </Button>
        ) : null}
      </Card>

      <MissedMedicationAlert show={adherence.showMissedAlert} streak={adherence.missedStreakDays} />
      <DueRemindersPanel compact onResponded={() => void load({ silent: true })} />
      <FamilyCaregiverDuePanel compact onResponded={() => void load({ silent: true })} />

      <div>
        <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
          {t('home.shortcuts')}
        </Typography.Text>
        <div className="home-shortcut-grid">
          {shortcuts.map((item) => (
            <button key={item.to} type="button" className="home-shortcut-btn" onClick={() => navigate(item.to)}>
              <span className="home-shortcut-icon">{item.icon}</span>
              <span className="home-shortcut-label">{t(`home.${item.labelKey}`)}</span>
            </button>
          ))}
        </div>
      </div>

      <Card
        size="small"
        hoverable
        style={{ borderRadius: 12, cursor: 'pointer' }}
        onClick={() => navigate('/health')}
      >
        <Typography.Text strong>{t('home.healthWalletCardTitle')}</Typography.Text>
        <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginTop: 4 }}>
          {t('home.healthWalletCardDesc')}
        </Typography.Text>
      </Card>

      <Card
        size="small"
        hoverable
        style={{ borderRadius: 12, cursor: 'pointer' }}
        onClick={() => navigate('/medications')}
      >
        <Typography.Text strong>{t('home.medicationsCardTitle')}</Typography.Text>
        <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginTop: 4 }}>
          {t('home.medicationsCardDesc')}
        </Typography.Text>
      </Card>

      <Card size="small" hoverable style={{ borderRadius: 12, cursor: 'pointer' }} onClick={() => navigate('/pharmacy')}>
        <Typography.Text strong>{branding.tenantName}</Typography.Text>
        <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginTop: 4 }}>
          {t('home.pharmacyCardDesc')}
        </Typography.Text>
      </Card>
    </Space>
  );
}
