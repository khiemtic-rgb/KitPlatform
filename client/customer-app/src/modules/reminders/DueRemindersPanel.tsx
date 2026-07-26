import { useCallback, useEffect, useState } from 'react';
import { Alert, Button, Card, Space, Spin, Typography, message } from 'antd';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  fetchDueReminders,
  getApiErrorMessage,
  respondMedicationReminder,
} from '@/shared/api/customer-app.api';
import type { MedicationReminder } from '@/shared/api/customer-app.types';
import type { MedSkipReasonCode } from '@/shared/care/med-skip-reasons';
import { SkipReasonModal } from '@/modules/reminders/SkipReasonModal';

type Props = {
  onResponded?: () => void;
  compact?: boolean;
  /** Không bọc Card — dùng trong Care Decision Inbox. */
  embedded?: boolean;
  dueItems?: MedicationReminder[];
  dueLoading?: boolean;
};

export function DueRemindersPanel({ onResponded, compact, embedded, dueItems, dueLoading }: Props) {
  const { t } = useTranslation();
  const controlled = dueItems !== undefined;
  const [internalItems, setInternalItems] = useState<MedicationReminder[]>([]);
  const [internalLoading, setInternalLoading] = useState(!controlled);
  const [actingId, setActingId] = useState<string | null>(null);
  const [skipTarget, setSkipTarget] = useState<MedicationReminder | null>(null);
  const items = controlled ? dueItems : internalItems;
  const loading = controlled ? Boolean(dueLoading) : internalLoading;

  const load = useCallback(async () => {
    if (controlled) return;
    setInternalLoading(true);
    try {
      const rows = await fetchDueReminders();
      setInternalItems(rows);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('reminders.dueTitle')));
    } finally {
      setInternalLoading(false);
    }
  }, [controlled, t]);

  useEffect(() => {
    if (controlled) return;
    void load();
  }, [controlled, load]);

  const respond = async (
    id: string,
    action: 'taken' | 'skipped' | 'snooze',
    skipReason?: MedSkipReasonCode,
  ) => {
    setActingId(id);
    try {
      await respondMedicationReminder(
        id,
        action,
        action === 'snooze' ? 15 : undefined,
        action === 'skipped' ? skipReason : undefined,
      );
      message.success(
        action === 'taken'
          ? t('reminders.takenRecorded')
          : action === 'skipped'
            ? t('common.skipped')
            : t('common.snooze15'),
      );
      setSkipTarget(null);
      if (!controlled) {
        await load();
      }
      onResponded?.();
    } catch (error) {
      message.error(getApiErrorMessage(error));
    } finally {
      setActingId(null);
    }
  };

  const body =
    loading && !embedded ? (
      <div style={{ textAlign: 'center', padding: 16 }}>
        <Spin size="small" />
      </div>
    ) : items.length === 0 ? null : (
      <Space direction="vertical" style={{ width: '100%' }} size={10}>
        {!embedded ? (
          <Typography.Text strong style={{ display: 'block' }}>
            {t('reminders.dueTitle')}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('reminders.dueTitle')}
          </Typography.Text>
        )}
        {items.map((item) => (
          <div
            key={item.id}
            style={{
              borderTop: items[0].id === item.id ? 'none' : '1px solid #e2e8f0',
              paddingTop: items[0].id === item.id ? 0 : 10,
            }}
          >
            <Typography.Text>
              <strong>{item.remindTime}</strong> — {item.productName}
            </Typography.Text>
            {item.dosageNote ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                {item.dosageNote}
              </Typography.Text>
            ) : null}
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11, marginBottom: 8 }}>
              {t('reminders.dueQuestion')}
            </Typography.Text>
            <Space wrap>
              <Button
                type="primary"
                size="small"
                loading={actingId === item.id}
                onClick={() => void respond(item.id, 'taken')}
              >
                {t('common.taken')}
              </Button>
              <Button
                size="small"
                loading={actingId === item.id}
                onClick={() => setSkipTarget(item)}
              >
                {t('common.skipped')}
              </Button>
              <Button size="small" loading={actingId === item.id} onClick={() => void respond(item.id, 'snooze')}>
                {t('common.snooze15')}
              </Button>
            </Space>
            {item.nextRemindAt ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11, marginTop: 6 }}>
                {t('common.scheduleAt', { time: dayjs(item.nextRemindAt).format('DD/MM HH:mm') })}
              </Typography.Text>
            ) : null}
          </div>
        ))}
      </Space>
    );

  const modal = (
    <SkipReasonModal
      open={Boolean(skipTarget)}
      productName={skipTarget?.productName}
      confirmLoading={skipTarget != null && actingId === skipTarget.id}
      onCancel={() => setSkipTarget(null)}
      onConfirm={(reason) => {
        if (!skipTarget) return;
        void respond(skipTarget.id, 'skipped', reason);
      }}
    />
  );

  if (loading && embedded) return null;
  if (!loading && items.length === 0) return modal;

  if (embedded) {
    return (
      <>
        {body}
        {modal}
      </>
    );
  }

  if (!body) return modal;

  return (
    <>
      <Card
        size="small"
        style={{ borderRadius: 12, borderColor: '#5eead4', marginBottom: compact ? 0 : 12 }}
        styles={{ body: { padding: compact ? '10px 12px' : '12px 16px' } }}
      >
        {body}
      </Card>
      {modal}
    </>
  );
}

export function MissedMedicationAlert({ show, streak }: { show: boolean; streak: number }) {
  const { t } = useTranslation();
  if (!show) return null;
  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 12, borderRadius: 12 }}
      message={t('reminders.missedAlert', { days: streak })}
      description={t('reminders.missedDesc')}
    />
  );
}
