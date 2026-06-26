import { useCallback, useEffect, useState, type CSSProperties, type ReactNode } from 'react';
import { Alert, Button, Card, Col, Row, Space, Spin, Statistic, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { fetchDraftOrders, fetchLoyaltySummary, fetchReminders, fetchReservations, getApiErrorMessage } from '@/shared/api/customer-app.api';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { CUSTOMER_DRAFT_ORDER_STATUS, CUSTOMER_RESERVATION_STATUS, type LoyaltyProgramSummary } from '@/shared/api/customer-app.types';
import { useAuthStore } from '@/shared/auth/auth.store';
import { formatPoints } from '@/shared/utils/points';

const tappableCardStyle: CSSProperties = {
  borderRadius: 12,
  cursor: 'pointer',
  transition: 'box-shadow 0.15s ease, transform 0.15s ease',
};

function TappableHomeCard({
  title,
  onClick,
  children,
}: {
  title: string;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <Card
      size="small"
      style={tappableCardStyle}
      styles={{ body: { padding: '12px 16px' } }}
      hoverable
      onClick={onClick}
      role="button"
      tabIndex={0}
      aria-label={title}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onClick();
        }
      }}
    >
      {children}
    </Card>
  );
}

export function HomePage() {
  const navigate = useNavigate();
  const profile = useAuthStore((s) => s.profile);
  const { online } = useApiHealth();
  const [initialLoading, setInitialLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [program, setProgram] = useState<LoyaltyProgramSummary | null>(null);
  const [reminderCount, setReminderCount] = useState(0);
  const [nextReminder, setNextReminder] = useState<string | null>(null);
  const [pendingDraftCount, setPendingDraftCount] = useState(0);
  const [activeReservationCount, setActiveReservationCount] = useState(0);

  const loadData = useCallback(async () => {
    setLoadError(null);
    try {
      const [loyaltyResult, remindersResult, draftsResult, reservationsResult] = await Promise.allSettled([
        fetchLoyaltySummary(),
        fetchReminders(),
        fetchDraftOrders(),
        fetchReservations(),
      ]);

      if (loyaltyResult.status === 'rejected') {
        setLoadError(getApiErrorMessage(loyaltyResult.reason, 'Không tải được dữ liệu'));
      } else if (remindersResult.status === 'rejected') {
        setLoadError(getApiErrorMessage(remindersResult.reason, 'Không tải được dữ liệu'));
      }

      if (draftsResult.status === 'rejected') {
        console.error(getApiErrorMessage(draftsResult.reason, 'Không tải được đơn hàng'));
      }

      if (reservationsResult.status === 'rejected') {
        console.error(getApiErrorMessage(reservationsResult.reason, 'Không tải được đặt trước'));
      }

      if (loyaltyResult.status === 'fulfilled') {
        setProgram(loyaltyResult.value?.programs[0] ?? null);
      }

      if (remindersResult.status === 'fulfilled') {
        const active = remindersResult.value.items.filter((r) => r.isActive);
        setReminderCount(active.length);
        const next = active
          .map((r) => r.nextRemindAt)
          .filter(Boolean)
          .sort()[0];
        setNextReminder(next ?? null);
      }

      if (draftsResult.status === 'fulfilled') {
        setPendingDraftCount(
          draftsResult.value.filter(
            (d) =>
              d.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
              d.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
          ).length,
        );
      }

      if (reservationsResult.status === 'fulfilled') {
        setActiveReservationCount(
          reservationsResult.value.filter(
            (r) =>
              r.status === CUSTOMER_RESERVATION_STATUS.Pending ||
              r.status === CUSTOMER_RESERVATION_STATUS.Confirmed ||
              r.status === CUSTOMER_RESERVATION_STATUS.Ready,
          ).length,
        );
      }
    } finally {
      setInitialLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  useRetryWhenApiOnline(() => loadData());

  if (initialLoading) {
    return (
      <div style={{ textAlign: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          Xin chào, {profile?.fullName ?? 'bạn'}!
        </Typography.Title>
        <Typography.Text type="secondary">Chào mừng trở lại nhà thuốc của bạn</Typography.Text>
      </div>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <Alert
          type="warning"
          showIcon
          message="Không tải được dữ liệu"
          description={loadError}
          action={
            <Button size="small" onClick={() => void loadData()}>
              Thử lại
            </Button>
          }
        />
      ) : null}

      {online === false && loadError ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin tip="Đang chờ API — tự tải lại khi kết nối trở lại" />
        </div>
      ) : null}

      {!(online === false && loadError) ? (
        <>
          <Row gutter={[12, 12]}>
            <Col span={12}>
              <TappableHomeCard title="Xem điểm thưởng và voucher" onClick={() => navigate('/loyalty')}>
                <Statistic
                  title="Điểm thưởng"
                  value={formatPoints(program?.pointsBalance ?? 0)}
                  suffix="điểm"
                  valueStyle={{ color: '#0f766e', fontSize: 22 }}
                />
                {program?.currentTier ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Hạng {program.currentTier.tierName}
                  </Typography.Text>
                ) : (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Chạm để xem voucher
                  </Typography.Text>
                )}
              </TappableHomeCard>
            </Col>
            <Col span={12}>
              <TappableHomeCard title="Quản lý nhắc uống thuốc" onClick={() => navigate('/reminders')}>
                <Statistic
                  title="Nhắc uống thuốc"
                  value={reminderCount}
                  suffix="lịch"
                  valueStyle={{ fontSize: 22 }}
                />
                {nextReminder ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Tiếp theo: {dayjs(nextReminder).format('DD/MM HH:mm')}
                  </Typography.Text>
                ) : (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Chạm để thêm lịch nhắc
                  </Typography.Text>
                )}
              </TappableHomeCard>
            </Col>
            <Col span={24}>
              <TappableHomeCard title="Đặt thuốc trước khi có hàng" onClick={() => navigate('/reservations')}>
                <Statistic
                  title="Đặt thuốc trước"
                  value={activeReservationCount}
                  suffix="yêu cầu"
                  valueStyle={{ fontSize: 22, color: '#b45309' }}
                />
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Chạm để gửi danh sách thuốc cần đặt
                </Typography.Text>
              </TappableHomeCard>
            </Col>
          </Row>

          {pendingDraftCount > 0 ? (
            <Alert
              type="info"
              showIcon
              message={`Bạn có ${pendingDraftCount} đơn đang đặt`}
              description="Xem và xác nhận tại tab Đơn hàng bên dưới."
              action={
                <Button size="small" type="primary" onClick={() => navigate('/orders')}>
                  Mở đơn hàng
                </Button>
              }
            />
          ) : null}
        </>
      ) : null}
    </Space>
  );
}
