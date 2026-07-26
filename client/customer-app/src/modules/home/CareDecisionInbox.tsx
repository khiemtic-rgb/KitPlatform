import { useCallback, useEffect, useMemo, useState } from 'react';
import { Badge, Button, Card, Space, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  fetchDueReminders,
  getApiErrorMessage,
  type CustomerConnectInbox,
} from '@/shared/api/customer-app.api';
import type { MedicationReminder } from '@/shared/api/customer-app.types';
import { DueRemindersPanel, MissedMedicationAlert } from '@/modules/reminders/DueRemindersPanel';
import { FamilyCaregiverDuePanel } from '@/modules/reminders/FamilyCaregiverDuePanel';
import { ConnectCarePanel } from '@/modules/home/ConnectCarePanel';

type AdherenceSnapshot = {
  dueCount: number;
  takenToday: number;
  skippedToday: number;
  scheduledToday: number;
  missedStreakDays: number;
  showMissedAlert: boolean;
};

type Props = {
  adherence: AdherenceSnapshot;
  pendingOrders: number;
  repurchaseCount: number;
  connectInbox: CustomerConnectInbox | null;
  homeLoading?: boolean;
  onChanged?: () => void;
};

/**
 * Hộp quyết định care ~1 phút — vay layout Inbox Family OS, nội dung thuốc/đơn/caregiver.
 */
export function CareDecisionInbox({
  adherence,
  pendingOrders,
  repurchaseCount,
  connectInbox,
  homeLoading,
  onChanged,
}: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [dueItems, setDueItems] = useState<MedicationReminder[]>([]);
  const [dueLoading, setDueLoading] = useState(false);
  const [familyDueCount, setFamilyDueCount] = useState(0);

  const reloadDue = useCallback(async () => {
    setDueLoading(true);
    try {
      setDueItems(await fetchDueReminders());
    } catch (error) {
      console.error(getApiErrorMessage(error));
      setDueItems([]);
    } finally {
      setDueLoading(false);
    }
  }, []);

  // Due list sau idle / ngắn delay — ưu tiên home-summary + paint trước.
  useEffect(() => {
    let idleId: number | undefined;
    let timeoutId: number | undefined;
    const run = () => {
      void reloadDue();
    };
    if (typeof window.requestIdleCallback === 'function') {
      idleId = window.requestIdleCallback(run, { timeout: 1800 });
    } else {
      timeoutId = window.setTimeout(run, 400);
    }
    return () => {
      if (idleId !== undefined && typeof window.cancelIdleCallback === 'function') {
        window.cancelIdleCallback(idleId);
      }
      if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    };
  }, [reloadDue]);

  const handleResponded = useCallback(() => {
    void reloadDue();
    onChanged?.();
  }, [onChanged, reloadDue]);

  const linkCount = (pendingOrders > 0 ? 1 : 0) + (repurchaseCount > 0 ? 1 : 0);
  const connectCount = connectInbox?.connectEnabled ? connectInbox.items.length : 0;
  const actionCount = dueItems.length + familyDueCount + linkCount + Math.min(connectCount, 3);
  // dueCount từ home-summary — hiện inbox sớm trước khi fetchDue xong.
  const showInboxCard = actionCount > 0 || dueLoading || adherence.dueCount > 0;

  const headline = useMemo(() => {
    if (actionCount === 0) {
      if (adherence.takenToday > 0 && adherence.dueCount === 0) return t('home.careInbox.emptyWin');
      return t('home.careInbox.empty');
    }
    if (actionCount === 1) return t('home.careInbox.headlineOne');
    return t('home.careInbox.headlineMany', { count: actionCount });
  }, [actionCount, adherence.dueCount, adherence.takenToday, t]);

  const showWin =
    actionCount === 0 &&
    !adherence.showMissedAlert &&
    adherence.takenToday > 0 &&
    adherence.dueCount === 0;

  const showInsight =
    actionCount === 0 &&
    !showWin &&
    adherence.skippedToday > 0 &&
    adherence.takenToday === 0;

  if (dueLoading && homeLoading && actionCount === 0 && !showWin && !adherence.showMissedAlert) {
    return null;
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <MissedMedicationAlert show={adherence.showMissedAlert} streak={adherence.missedStreakDays} />

      {showWin ? (
        <Card
          size="small"
          style={{
            borderRadius: 12,
            borderColor: '#6ee7b7',
            background: 'linear-gradient(145deg, #ffffff 0%, #ecfdf5 100%)',
          }}
          styles={{ body: { padding: '12px 14px' } }}
        >
          <Typography.Text strong style={{ display: 'block' }}>
            {t('home.careWin.title')}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {t('home.careWin.body', { taken: adherence.takenToday })}
          </Typography.Text>
        </Card>
      ) : null}

      {showInsight ? (
        <Card size="small" style={{ borderRadius: 12 }} styles={{ body: { padding: '12px 14px' } }}>
          <Typography.Text strong style={{ display: 'block' }}>
            {t('home.careInsight.title')}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {t('home.careInsight.skippedToday', { count: adherence.skippedToday })}
          </Typography.Text>
        </Card>
      ) : null}

      {showInboxCard ? (
        <Card
          size="small"
          style={{ borderRadius: 12, border: '1px solid #99f6e4' }}
          styles={{ body: { padding: '12px 14px' } }}
        >
          <Space align="center" style={{ width: '100%', justifyContent: 'space-between', marginBottom: 10 }}>
            <Typography.Text strong>
              {t('home.careInbox.title')}
              {actionCount > 0 ? (
                <Badge
                  count={Math.min(actionCount, 9)}
                  style={{ marginLeft: 8, backgroundColor: '#0f766e' }}
                />
              ) : null}
            </Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {headline}
            </Typography.Text>
          </Space>

          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            {pendingOrders > 0 ? (
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center' }}>
                <div>
                  <Typography.Text strong style={{ display: 'block', fontSize: 13 }}>
                    {t('home.careInbox.draftOrders', { count: pendingOrders })}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('home.careInbox.draftOrdersHint')}
                  </Typography.Text>
                </div>
                <Button size="small" type="primary" onClick={() => navigate('/orders')}>
                  {t('home.careInbox.openOrders')}
                </Button>
              </div>
            ) : null}

            {repurchaseCount > 0 ? (
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center' }}>
                <div>
                  <Typography.Text strong style={{ display: 'block', fontSize: 13 }}>
                    {t('home.careInbox.repurchase', { count: repurchaseCount })}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('home.careInbox.repurchaseHint')}
                  </Typography.Text>
                </div>
                <Button size="small" onClick={() => navigate('/medications')}>
                  {t('home.careInbox.openMeds')}
                </Button>
              </div>
            ) : null}

            <DueRemindersPanel
              embedded
              compact
              dueItems={dueItems}
              dueLoading={dueLoading}
              onResponded={handleResponded}
            />
            <FamilyCaregiverDuePanel
              embedded
              compact
              onResponded={handleResponded}
              onCountChange={setFamilyDueCount}
            />
          </Space>
        </Card>
      ) : null}

      <ConnectCarePanel inbox={connectInbox} loading={Boolean(homeLoading) && !connectInbox} />
    </Space>
  );
}
