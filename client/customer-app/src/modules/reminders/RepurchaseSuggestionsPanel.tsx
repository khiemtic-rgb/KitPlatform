import { useCallback, useEffect, useState } from 'react';
import { Form, Select, Spin, TimePicker, message } from 'antd';
import { ClockCircleOutlined, MedicineBoxOutlined, UserOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  acceptRepurchaseSuggestion,
  dismissRepurchaseSuggestion,
  fetchFamilyMembers,
  fetchRepurchaseSuggestions,
  getApiErrorMessage,
  snoozeRepurchaseSuggestion,
} from '@/shared/api/customer-app.api';
import { FAMILY_RELATIONSHIP_LABELS, type FamilyMember, type RepurchaseSuggestion } from '@/shared/api/customer-app.types';
import {
  CustomerFormModal,
  FormModalFooter,
  FormModalLabel,
} from '@/shared/components/CustomerFormModal';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { RepurchaseCard } from '@/modules/reminders/RepurchaseCard';
import './RepurchaseSuggestionsPanel.css';

function isVisibleSuggestion(item: RepurchaseSuggestion) {
  if (item.status === 'dismissed' || item.status === 'expired') return false;
  if (item.status === 'snoozed' && item.snoozedUntil) {
    return dayjs().isAfter(dayjs(item.snoozedUntil));
  }
  return item.status === 'pending';
}

export function RepurchaseSuggestionsPanel({
  onAccepted,
  suggestions,
  familyMembers: externalFamilyMembers,
  suggestionsLoading,
}: {
  onAccepted?: () => void;
  suggestions?: RepurchaseSuggestion[];
  familyMembers?: FamilyMember[];
  suggestionsLoading?: boolean;
}) {
  const { t } = useTranslation();
  const { familyRelationship } = useCustomerLabels();
  const controlled = suggestions !== undefined;
  const [items, setItems] = useState<RepurchaseSuggestion[]>([]);
  const [familyMembers, setFamilyMembers] = useState<FamilyMember[]>([]);
  const [loading, setLoading] = useState(!controlled);
  const [actingId, setActingId] = useState<string | null>(null);
  const [acceptModalOpen, setAcceptModalOpen] = useState(false);
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [form] = Form.useForm();

  const visibleItems = controlled ? suggestions.filter(isVisibleSuggestion) : items;
  const resolvedFamilyMembers = externalFamilyMembers ?? familyMembers;
  const panelLoading = controlled ? Boolean(suggestionsLoading) : loading;

  const load = useCallback(async () => {
    if (controlled) return;
    setLoading(true);
    try {
      const [rows, family] = await Promise.all([fetchRepurchaseSuggestions(), fetchFamilyMembers()]);
      setItems(rows.filter(isVisibleSuggestion));
      setFamilyMembers(family.filter((m) => m.status === 1));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('repurchase.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [controlled, t]);

  useEffect(() => {
    if (controlled) return;
    void load();
  }, [controlled, load]);

  const patchItem = (updated: RepurchaseSuggestion) => {
    setItems((prev) => {
      const next = prev.map((row) => (row.id === updated.id ? updated : row));
      return next.filter(isVisibleSuggestion);
    });
  };

  const openAccept = (id: string) => {
    setAcceptingId(id);
    form.setFieldsValue({
      familyMemberId: undefined,
      remindTime: dayjs('08:00', 'HH:mm'),
    });
    setAcceptModalOpen(true);
  };

  const onAccept = async () => {
    if (!acceptingId) return;
    const values = await form.validateFields();
    setActingId(acceptingId);
    try {
      const updated = await acceptRepurchaseSuggestion(acceptingId, {
        familyMemberId: values.familyMemberId,
        remindTime: (values.remindTime as dayjs.Dayjs).format('HH:mm'),
      });
      patchItem(updated);
      message.success(
        values.familyMemberId ? t('repurchase.acceptedFamily') : t('repurchase.acceptedSelf'),
      );
      setAcceptModalOpen(false);
      setAcceptingId(null);
      onAccepted?.();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('repurchase.createFailed')));
    } finally {
      setActingId(null);
    }
  };

  const onDismiss = async (id: string) => {
    setActingId(id);
    try {
      const updated = await dismissRepurchaseSuggestion(id);
      patchItem(updated);
      message.success(t('repurchase.dismissed'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('repurchase.dismissFailed')));
    } finally {
      setActingId(null);
    }
  };

  const onSnooze = async (id: string) => {
    setActingId(id);
    try {
      const updated = await snoozeRepurchaseSuggestion(id, dayjs().add(3, 'day').toISOString());
      patchItem(updated);
      message.success(t('repurchase.snoozed'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('repurchase.snoozeFailed')));
    } finally {
      setActingId(null);
    }
  };

  const familyOptions = resolvedFamilyMembers.map((member) => ({
    value: member.id,
    label: `${member.fullName} (${familyRelationship(member.relationship) ?? FAMILY_RELATIONSHIP_LABELS[member.relationship]})`,
  }));

  if (panelLoading) {
    return (
      <div className="repurchase-loading">
        <Spin size="small" />
      </div>
    );
  }

  if (visibleItems.length === 0) return null;

  return (
    <>
      <section className="repurchase-panel" aria-label={t('reminders.repurchasePanelTitle')}>
        <div className="repurchase-panel-head">
          <h2 className="repurchase-panel-title">{t('reminders.repurchasePanelTitle')}</h2>
          <p className="repurchase-panel-desc">{t('reminders.repurchasePanelDesc')}</p>
        </div>

        <div className="repurchase-list">
          {visibleItems.map((item) => (
            <RepurchaseCard
              key={item.id}
              item={item}
              busy={actingId === item.id}
              onCreate={
                item.drinkRemindersCreatedAt ? undefined : () => openAccept(item.id)
              }
              onSnooze={() => void onSnooze(item.id)}
              onDismiss={() => void onDismiss(item.id)}
            />
          ))}
        </div>
      </section>

      <CustomerFormModal
        open={acceptModalOpen}
        onCancel={() => {
          setAcceptModalOpen(false);
          setAcceptingId(null);
        }}
        icon={<MedicineBoxOutlined />}
        title={t('repurchase.modalTitle')}
        subtitle={t('repurchase.modalSub')}
        footer={
          <FormModalFooter
            onCancel={() => {
              setAcceptModalOpen(false);
              setAcceptingId(null);
            }}
            onOk={() => void onAccept()}
            okText={t('repurchase.okCreate')}
            confirmLoading={actingId !== null}
          />
        }
      >
        <Form form={form} layout="vertical" className="cfm-form" requiredMark={false}>
          <Form.Item
            name="familyMemberId"
            label={<FormModalLabel icon={<UserOutlined />}>{t('repurchase.forWho')}</FormModalLabel>}
          >
            <Select
              size="large"
              allowClear
              placeholder={t('repurchase.forWhoPlaceholder')}
              options={familyOptions}
              notFoundContent={t('repurchase.noFamily')}
            />
          </Form.Item>
          <Form.Item
            name="remindTime"
            label={
              <FormModalLabel icon={<ClockCircleOutlined />} required>
                {t('repurchase.remindTime')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <TimePicker size="large" format="HH:mm" style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </CustomerFormModal>
    </>
  );
}
