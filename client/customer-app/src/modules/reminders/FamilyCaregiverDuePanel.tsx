import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Space, Spin, Typography, message } from 'antd';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  fetchFamilyDueReminders,
  fetchFamilyMembers,
  getApiErrorMessage,
  respondMedicationReminder,
} from '@/shared/api/customer-app.api';
import type { FamilyMember, MedicationReminder } from '@/shared/api/customer-app.types';
import type { MedSkipReasonCode } from '@/shared/care/med-skip-reasons';
import { SkipReasonModal } from '@/modules/reminders/SkipReasonModal';

type Props = {
  compact?: boolean;
  embedded?: boolean;
  onResponded?: () => void;
  /** Báo số lượng lên Inbox cha. */
  onCountChange?: (count: number) => void;
};

export function FamilyCaregiverDuePanel({ compact, embedded, onResponded, onCountChange }: Props) {
  const { t } = useTranslation();
  const [items, setItems] = useState<MedicationReminder[]>([]);
  const [family, setFamily] = useState<FamilyMember[]>([]);
  const [loading, setLoading] = useState(false);
  const [actingId, setActingId] = useState<string | null>(null);
  const [skipTarget, setSkipTarget] = useState<MedicationReminder | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [due, members] = await Promise.all([fetchFamilyDueReminders(), fetchFamilyMembers()]);
      setItems(due);
      setFamily(members.filter((m) => m.status === 1));
      onCountChange?.(due.length);
    } catch (error) {
      console.error(getApiErrorMessage(error));
      setItems([]);
      onCountChange?.(0);
    } finally {
      setLoading(false);
    }
  }, [onCountChange]);

  useEffect(() => {
    let idleId: number | undefined;
    let timeoutId: number | undefined;
    const run = () => {
      void load();
    };
    if (typeof window.requestIdleCallback === 'function') {
      idleId = window.requestIdleCallback(run, { timeout: 2200 });
    } else {
      timeoutId = window.setTimeout(run, 600);
    }
    return () => {
      if (idleId !== undefined && typeof window.cancelIdleCallback === 'function') {
        window.cancelIdleCallback(idleId);
      }
      if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    };
  }, [load]);

  const familyName = (id: string | null) => {
    if (!id) return t('common.familyMember');
    return family.find((m) => m.id === id)?.fullName ?? t('common.familyMember');
  };

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
          ? t('reminders.familyTakenRecorded')
          : action === 'skipped'
            ? t('common.skipped')
            : t('common.snooze15'),
      );
      setSkipTarget(null);
      await load();
      onResponded?.();
    } catch (error) {
      message.error(getApiErrorMessage(error));
    } finally {
      setActingId(null);
    }
  };

  const modal = (
    <SkipReasonModal
      open={Boolean(skipTarget)}
      productName={
        skipTarget
          ? `${familyName(skipTarget.familyMemberId)} · ${skipTarget.productName}`
          : undefined
      }
      confirmLoading={skipTarget != null && actingId === skipTarget.id}
      onCancel={() => setSkipTarget(null)}
      onConfirm={(reason) => {
        if (!skipTarget) return;
        void respond(skipTarget.id, 'skipped', reason);
      }}
    />
  );

  if (loading) {
    return embedded ? (
      modal
    ) : (
      <>
        <div style={{ textAlign: 'center', padding: 16 }}>
          <Spin size="small" />
        </div>
        {modal}
      </>
    );
  }

  if (items.length === 0) return modal;

  const body = (
    <Space direction="vertical" style={{ width: '100%' }} size={10}>
      {embedded ? (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('reminders.familyDueTitle')}
        </Typography.Text>
      ) : (
        <Typography.Text strong style={{ display: 'block' }}>
          {t('reminders.familyDueTitle')}
        </Typography.Text>
      )}
      {items.map((item, index) => (
        <div
          key={item.id}
          style={{
            borderTop: index === 0 ? 'none' : '1px solid #e2e8f0',
            paddingTop: index === 0 ? 0 : 10,
          }}
        >
          <Typography.Text>
            <strong>{familyName(item.familyMemberId)}</strong> — {item.remindTime} · {item.productName}
          </Typography.Text>
          {item.dosageNote ? (
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              {item.dosageNote}
            </Typography.Text>
          ) : null}
          <Space wrap style={{ marginTop: 8 }}>
            <Button
              size="small"
              type="primary"
              loading={actingId === item.id}
              onClick={() => void respond(item.id, 'taken')}
            >
              {t('common.taken')}
            </Button>
            <Button size="small" loading={actingId === item.id} onClick={() => void respond(item.id, 'snooze')}>
              {t('common.snoozeLater')}
            </Button>
            <Button size="small" loading={actingId === item.id} onClick={() => setSkipTarget(item)}>
              {t('common.skipped')}
            </Button>
          </Space>
        </div>
      ))}
      <Link to="/family" style={{ fontSize: 12, display: 'inline-block' }}>
        {t('reminders.familyManage')}
      </Link>
    </Space>
  );

  if (embedded) {
    return (
      <>
        {body}
        {modal}
      </>
    );
  }

  return (
    <>
      <Card
        size="small"
        style={{ borderRadius: 12, borderColor: '#fcd34d', marginBottom: compact ? 0 : 12 }}
        styles={{ body: { padding: compact ? '10px 12px' : '12px 16px' } }}
      >
        {body}
      </Card>
      {modal}
    </>
  );
}
