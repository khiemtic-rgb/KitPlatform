import { Card, Col, Row, Statistic, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ReactNode } from 'react';
import { fetchPendingInvites, fetchPrescriberDashboard } from '@/shared/api/prescriber-portal.api';
import type { PortalPrescriberDashboard } from '@/shared/api/prescriber-portal.types';
import { useAuthStore } from '@/shared/auth/auth.store';

export function HomePage() {
  const { t } = useTranslation();
  const profile = useAuthStore((s) => s.profile);

  const invitesQuery = useQuery({
    queryKey: ['prescriber', 'invites'],
    queryFn: fetchPendingInvites,
  });

  const dashboardQuery = useQuery({
    queryKey: ['prescriber', 'dashboard'],
    queryFn: fetchPrescriberDashboard,
  });

  return (
    <SpacePage title={t('home.title')} greeting={profile?.fullName}>
      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('home.signedThisMonth')}
              value={dashboardQuery.data?.signedThisMonth ?? 0}
              loading={dashboardQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('home.signedTotal')}
              value={dashboardQuery.data?.signedTotal ?? 0}
              loading={dashboardQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('home.linkedPharmacies')}
              value={dashboardQuery.data?.activePharmacyCount ?? 0}
              loading={dashboardQuery.isLoading}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('home.pendingInvites')}
              value={invitesQuery.data?.length ?? 0}
              loading={invitesQuery.isLoading}
            />
          </Card>
        </Col>
      </Row>
      {dashboardQuery.data?.byTenant?.length ? (
        <Card title={t('home.byPharmacy')}>
          {dashboardQuery.data.byTenant.map((row: PortalPrescriberDashboard['byTenant'][number]) => (
            <Typography.Paragraph key={row.tenantId} style={{ marginBottom: 8 }}>
              <Typography.Text strong>{row.tenantName}</Typography.Text>
              {' · '}
              {t('home.signedThisMonth')}: {row.signedThisMonth} · {t('home.signedTotal')}: {row.signedTotal}
            </Typography.Paragraph>
          ))}
        </Card>
      ) : null}
      <Card>
        <Typography.Paragraph style={{ marginBottom: 0 }}>{t('home.tip')}</Typography.Paragraph>
      </Card>
    </SpacePage>
  );
}

function SpacePage({
  title,
  greeting,
  children,
}: {
  title: string;
  greeting?: string;
  children: ReactNode;
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {title}
        </Typography.Title>
        {greeting ? <Typography.Text type="secondary">{greeting}</Typography.Text> : null}
      </div>
      {children}
    </div>
  );
}
