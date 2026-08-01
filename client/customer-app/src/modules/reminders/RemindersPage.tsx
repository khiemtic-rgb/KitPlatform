import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  Alert,
  Button,
  Checkbox,
  Form,
  Input,
  Select,
  Spin,
  Switch,
  TimePicker,
  message,
} from 'antd';
import {
  ArrowLeftOutlined,
  BellOutlined,
  CalendarOutlined,
  CheckCircleOutlined,
  CheckOutlined,
  ClockCircleOutlined,
  FileTextOutlined,
  HeartOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  PlusOutlined,
  SafetyCertificateOutlined,
  ShoppingCartOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  acceptRepurchaseSuggestion,
  createReminder,
  dismissRepurchaseSuggestion,
  getApiErrorMessage,
  reorderRepurchaseSuggestion,
  respondMedicationReminder,
  searchProducts,
  snoozeRepurchaseSuggestion,
  updateReminder,
} from '@/shared/api/customer-app.api';
import {
  CustomerFormModal,
  FormModalFooter,
  FormModalLabel,
  FormModalTip,
} from '@/shared/components/CustomerFormModal';
import { usePharmacyLink } from '@/shared/config/PharmacyLinkProvider';
import type {
  CustomerProductSearchItem,
  FamilyMember,
  MedicationReminder,
  RepurchaseSuggestion,
} from '@/shared/api/customer-app.types';
import { useRemindersOverviewQuery } from '@/shared/api/overview-queries';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { normalizeReminderId } from '@/shared/api/reminder-normalize';
import i18n from '@/shared/i18n';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { ListCardSkeleton } from '@/shared/components/ListCardSkeleton';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import type { MedSkipReasonCode } from '@/shared/care/med-skip-reasons';
import { SkipReasonModal } from '@/modules/reminders/SkipReasonModal';
import { MissedMedicationAlert } from '@/modules/reminders/DueRemindersPanel';
import { RepurchaseCard } from '@/modules/reminders/RepurchaseCard';
import { PharmacyLinkSoftBanner } from '@/shared/components/PharmacyLinkGate';
import { useVerifyAccount } from '@/shared/auth/VerifyAccountProvider';
import { familyRoleLabel } from '@/shared/i18n/family-role-label';
import './RemindersPage.css';

type HubTab = 'all' | 'due' | 'schedule' | 'repurchase';

function useDayOptions() {
  const { day } = useCustomerLabels();
  return [1, 2, 3, 4, 5, 6, 7].map((value) => ({ label: day(value), value }));
}

function formatProductLabel(product: CustomerProductSearchItem) {
  const unit = product.saleUnitName ? ` · ${product.saleUnitName}` : '';
  return `${product.productName} (${product.productCode})${unit}`;
}

function stableReminderOrder(items: MedicationReminder[]): MedicationReminder[] {
  return [...items].sort((a, b) => {
    const createdCmp = a.createdAt.localeCompare(b.createdAt);
    if (createdCmp !== 0) return createdCmp;
    return a.id.localeCompare(b.id);
  });
}

function assertUniqueReminderIds(items: MedicationReminder[]) {
  const seen = new Set<string>();
  for (const item of items) {
    if (seen.has(item.id)) {
      throw new Error(i18n.t('reminders.duplicateIdLocal', { id: item.id }));
    }
    seen.add(item.id);
  }
}

function patchReminderById(
  items: MedicationReminder[],
  reminderId: string,
  patch: Partial<MedicationReminder>,
): MedicationReminder[] {
  const targetId = normalizeReminderId(reminderId);
  let matched = false;
  const next = items.map((row) => {
    if (row.id !== targetId) return row;
    matched = true;
    return { ...row, ...patch };
  });
  if (!matched) {
    throw new Error(i18n.t('reminders.notFound', { id: targetId }));
  }
  return next;
}

function formatFamilyMemberLabel(
  member: FamilyMember,
  relationLabel: (key: string) => string,
  t: (key: string) => string,
) {
  const relation = familyRoleLabel(member.relationship, member.gender, t, relationLabel, member.id);
  return `${member.fullName} (${relation})`;
}

function isVisibleSuggestion(item: RepurchaseSuggestion) {
  if (item.status === 'dismissed' || item.status === 'expired' || item.status === 'converted') return false;
  if (item.status === 'snoozed' && item.snoozedUntil) {
    return dayjs().isAfter(dayjs(item.snoozedUntil));
  }
  return item.status === 'pending' || item.status === 'snoozed';
}

function relativeTime(iso: string | null | undefined, t: (k: string, o?: Record<string, unknown>) => string) {
  if (!iso) return '';
  const d = dayjs(iso);
  if (!d.isValid()) return '';
  if (d.isSame(dayjs(), 'day')) return d.format('HH:mm');
  if (d.isSame(dayjs().subtract(1, 'day'), 'day')) return t('reminders.hubYesterday');
  const days = dayjs().startOf('day').diff(d.startOf('day'), 'day');
  if (days > 1 && days < 7) return t('reminders.hubDaysAgo', { count: days });
  return d.format('DD/MM');
}

export function RemindersPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { branding } = useCustomerBranding();
  const { requireAuth } = useVerifyAccount();
  const { requireLink } = usePharmacyLink();
  const { familyRelationship, day } = useCustomerLabels();
  const dayOptions = useDayOptions();
  const { data: overview, isLoading, error, refetch } = useRemindersOverviewQuery();
  const [items, setItems] = useState<MedicationReminder[]>([]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const initialTab = (searchParams.get('tab') as HubTab | null) ?? 'all';
  const [tab, setTab] = useState<HubTab>(
    initialTab === 'due' ||
      initialTab === 'schedule' ||
      initialTab === 'repurchase' ||
      initialTab === 'all'
      ? initialTab
      : 'all',
  );
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<MedicationReminder | null>(null);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [actingId, setActingId] = useState<string | null>(null);
  const [skipTarget, setSkipTarget] = useState<MedicationReminder | null>(null);
  const [repurchaseActingId, setRepurchaseActingId] = useState<string | null>(null);
  const [productOptions, setProductOptions] = useState<CustomerProductSearchItem[]>([]);
  const [productSearchLoading, setProductSearchLoading] = useState(false);
  const [form] = Form.useForm();
  const searchTimerRef = useRef<number | null>(null);

  const familyMembers = overview?.familyMembers ?? [];
  const adherence = overview?.adherence ?? { showMissedAlert: false, missedStreakDays: 0 };
  const dueItems = overview?.dueReminders ?? [];
  const repurchaseItems = (overview?.repurchaseSuggestions ?? []).filter(isVisibleSuggestion);
  const loading = isLoading && !overview;
  const loadError = error ? getApiErrorMessage(error, t('reminders.listLoadFailed')) : null;

  useEffect(() => {
    if (!overview) return;
    const ordered = stableReminderOrder(overview.reminders);
    assertUniqueReminderIds(ordered);
    setItems(ordered);
  }, [overview]);

  useEffect(() => {
    const next = searchParams.get('tab');
    if (next === 'due' || next === 'schedule' || next === 'repurchase' || next === 'all') {
      setTab(next);
    }
  }, [searchParams]);

  useEffect(() => {
    if (!error) return;
    message.error(getApiErrorMessage(error, t('reminders.listLoadFailed')));
  }, [error, t]);

  useRetryWhenApiOnline(() => void refetch());

  const familyNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const member of familyMembers) {
      map.set(member.id, formatFamilyMemberLabel(member, familyRelationship, t));
    }
    return map;
  }, [familyMembers, familyRelationship, t]);

  const loadProducts = useCallback(
    async (search?: string) => {
      setProductSearchLoading(true);
      try {
        const result = await searchProducts(search, 1, 30);
        setProductOptions(result.items);
      } catch (err) {
        message.error(getApiErrorMessage(err, t('reminders.productLoadFailed')));
      } finally {
        setProductSearchLoading(false);
      }
    },
    [t],
  );

  useEffect(() => {
    if (!modalOpen) return;
    void loadProducts();
  }, [modalOpen, loadProducts]);

  const visibleSchedules = useMemo(() => {
    const ordered = stableReminderOrder(items);
    return includeInactive ? ordered : ordered.filter((item) => item.isActive);
  }, [items, includeInactive]);

  const onProductSearch = (value: string) => {
    if (searchTimerRef.current) {
      window.clearTimeout(searchTimerRef.current);
    }
    searchTimerRef.current = window.setTimeout(() => {
      void loadProducts(value);
    }, 300);
  };

  const openCreate = () => {
    if (!requireAuth(t('verifyAccount.intentSaveSchedule'))) return;
    setEditing(null);
    form.setFieldsValue({
      productId: undefined,
      familyMemberId: undefined,
      dosageNote: '',
      remindTime: dayjs('08:00', 'HH:mm'),
      daysOfWeek: [1, 2, 3, 4, 5, 6, 7],
    });
    setModalOpen(true);
  };

  const openEdit = (item: MedicationReminder) => {
    setEditing(item);
    setProductOptions((prev) => {
      if (prev.some((p) => p.id === item.productId)) return prev;
      return [
        {
          id: item.productId,
          productCode: item.productCode,
          productName: item.productName,
          genericName: null,
          saleUnitName: null,
        },
        ...prev,
      ];
    });
    form.setFieldsValue({
      productId: item.productId,
      familyMemberId: item.familyMemberId ?? undefined,
      dosageNote: item.dosageNote ?? '',
      remindTime: dayjs(item.remindTime, 'HH:mm'),
      daysOfWeek: item.daysOfWeek,
      isActive: item.isActive,
    });
    setModalOpen(true);
  };

  const onSubmit = async () => {
    const values = await form.validateFields();
    const remindTime = (values.remindTime as Dayjs).format('HH:mm');
    try {
      if (editing) {
        const updated = await updateReminder(editing.id, {
          productId: values.productId,
          familyMemberId: values.familyMemberId ?? null,
          dosageNote: values.dosageNote || undefined,
          remindTime,
          daysOfWeek: values.daysOfWeek,
          isActive: values.isActive,
        });
        setItems((prev) => patchReminderById(prev, updated.id, updated));
        message.success(t('reminders.updated'));
      } else {
        const created = await createReminder({
          productId: values.productId,
          familyMemberId: values.familyMemberId ?? undefined,
          dosageNote: values.dosageNote || undefined,
          remindTime,
          daysOfWeek: values.daysOfWeek,
        });
        setItems((prev) => stableReminderOrder([...prev, created]));
        message.success(t('reminders.added'));
      }
      setModalOpen(false);
    } catch (err) {
      message.error(getApiErrorMessage(err, t('reminders.saveFailed')));
    }
  };

  const onToggleActive = async (reminderId: string, isActive: boolean) => {
    const targetId = normalizeReminderId(reminderId);
    const previousItems = items;
    setTogglingId(targetId);
    try {
      const updated = await updateReminder(targetId, { isActive });
      if (normalizeReminderId(updated.id) !== targetId) {
        throw new Error(t('reminders.idMismatch'));
      }
      setItems((prev) => patchReminderById(prev, targetId, updated));
      message.success(isActive ? t('reminders.toggleOn') : t('reminders.toggleOff'));
    } catch (err) {
      setItems(previousItems);
      message.error(getApiErrorMessage(err, t('reminders.toggleFailed')));
    } finally {
      setTogglingId(null);
    }
  };

  const respondDue = async (
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
      void refetch();
    } catch (err) {
      message.error(getApiErrorMessage(err));
    } finally {
      setActingId(null);
    }
  };

  const onRepurchaseDismiss = async (id: string) => {
    setRepurchaseActingId(id);
    try {
      await dismissRepurchaseSuggestion(id);
      message.success(t('repurchase.dismissed'));
      void refetch();
    } catch (err) {
      message.error(getApiErrorMessage(err));
    } finally {
      setRepurchaseActingId(null);
    }
  };

  const onRepurchaseSnooze = async (id: string) => {
    setRepurchaseActingId(id);
    try {
      await snoozeRepurchaseSuggestion(id, dayjs().add(3, 'day').toISOString());
      message.success(t('repurchase.snoozed'));
      void refetch();
    } catch (err) {
      message.error(getApiErrorMessage(err, t('repurchase.snoozeFailed')));
    } finally {
      setRepurchaseActingId(null);
    }
  };

  const onRepurchaseAccept = async (id: string) => {
    setRepurchaseActingId(id);
    try {
      await acceptRepurchaseSuggestion(id, {});
      message.success(t('repurchase.acceptedSelf'));
      void refetch();
    } catch (err) {
      message.error(getApiErrorMessage(err, t('repurchase.createFailed')));
    } finally {
      setRepurchaseActingId(null);
    }
  };

  const onRepurchaseReorder = async (id: string) => {
    if (!requireLink(t('pharmacyLink.intentReserve'))) return;
    setRepurchaseActingId(id);
    try {
      const result = await reorderRepurchaseSuggestion(id);
      message.success(
        t('repurchase.reordered', { number: result.reservationNumber || result.reservationId }),
      );
      void refetch();
      if (result.reservationId) {
        navigate(`/reservations?focus=${encodeURIComponent(result.reservationId)}`);
      } else {
        navigate('/reservations');
      }
    } catch (err) {
      message.error(getApiErrorMessage(err, t('repurchase.reorderFailed')));
    } finally {
      setRepurchaseActingId(null);
    }
  };

  const familySelectOptions = familyMembers.map((member) => ({
    value: member.id,
    label: formatFamilyMemberLabel(member, familyRelationship, t),
  }));

  const selectOptions = productOptions.map((p) => ({
    value: p.id,
    label: formatProductLabel(p),
  }));

  const tabs: Array<{ key: HubTab; label: string; icon: ReactNode }> = [
    { key: 'all', label: t('reminders.hubTabAll'), icon: <MessageOutlined /> },
    { key: 'due', label: t('reminders.hubTabDue'), icon: <BellOutlined /> },
    { key: 'schedule', label: t('reminders.hubTabSchedule'), icon: <FileTextOutlined /> },
    { key: 'repurchase', label: t('reminders.hubTabRepurchase'), icon: <ShoppingCartOutlined /> },
  ];

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  const showDue = tab === 'all' || tab === 'due';
  const showSchedule = tab === 'all' || tab === 'schedule';
  const showRepurchase = tab === 'all' || tab === 'repurchase';

  const empty =
    (!showDue || dueItems.length === 0) &&
    (!showSchedule || visibleSchedules.length === 0) &&
    (!showRepurchase || repurchaseItems.length === 0);

  return (
    <div className="reminders-hub">
      <header className="reminders-hub-header" style={headerStyle}>
        <div className="reminders-hub-header-inner">
          <div className="reminders-hub-brand">
            <button
              type="button"
              className="reminders-hub-back"
              aria-label={t('common.back')}
              onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))}
            >
              <ArrowLeftOutlined />
            </button>
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="reminders-hub-brand-title">
                <span>{branding.appName}</span>
                <CheckCircleOutlined />
              </div>
              <div className="reminders-hub-tagline">
                {branding.tagline || t('reminders.hubTagline')}
              </div>
              <div className="reminders-hub-online">
                <span className="reminders-hub-online-dot" />
                {t('reminders.hubOnline')}
              </div>
            </div>
          </div>
          <div className="reminders-hub-mascot" aria-hidden>
            <img src="/home/ai-robot.jpg" alt="" />
          </div>
        </div>
      </header>

      <div className="reminders-hub-sheet">
        <PharmacyLinkSoftBanner />
        <div className="reminders-hub-banner">
          <div className="reminders-hub-banner-main">
            <span className="reminders-hub-banner-icon">
              <BellOutlined />
            </span>
            <div>
              <div className="reminders-hub-banner-title">{t('reminders.hubBannerTitle')}</div>
              <div className="reminders-hub-banner-sub">{t('reminders.hubBannerSub')}</div>
            </div>
          </div>
          <div className="reminders-hub-banner-feats">
            <div className="reminders-hub-banner-feat">
              <HeartOutlined />
              {t('reminders.hubFeatCare')}
            </div>
            <div className="reminders-hub-banner-feat">
              <SafetyCertificateOutlined />
              {t('reminders.hubFeatSafe')}
            </div>
            <div className="reminders-hub-banner-feat">
              <CheckOutlined />
              {t('reminders.hubFeatFree')}
            </div>
          </div>
        </div>

        <MissedMedicationAlert show={adherence.showMissedAlert} streak={adherence.missedStreakDays} />

        <div className="reminders-hub-tabs" role="tablist">
          {tabs.map((item) => (
            <button
              key={item.key}
              type="button"
              role="tab"
              aria-selected={tab === item.key}
              className={`reminders-hub-tab${tab === item.key ? ' reminders-hub-tab--active' : ''}`}
              onClick={() => setTab(item.key)}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </div>

        {loadError ? (
          <Alert
            type="error"
            showIcon
            style={{ marginBottom: 12 }}
            message={loadError}
            action={
              <Button size="small" onClick={() => void refetch()}>
                {t('common.retry')}
              </Button>
            }
          />
        ) : null}

        {tab === 'schedule' || tab === 'all' ? (
          <div className="reminders-hub-toolbar">
            <Checkbox checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)}>
              {t('reminders.showInactive')}
            </Checkbox>
          </div>
        ) : null}

        {loading ? (
          <div className="reminders-hub-loading">
            <ListCardSkeleton rows={4} />
          </div>
        ) : empty ? (
          <div className="reminders-hub-empty">{t('reminders.empty')}</div>
        ) : (
          <div className="reminders-hub-list">
            {showDue
              ? dueItems.map((item) => {
                  const familyName = item.familyMemberId
                    ? familyNameById.get(item.familyMemberId)
                    : undefined;
                  return (
                    <div key={`due-${item.id}`} className="reminders-hub-card reminders-hub-card--static">
                      <div className="reminders-hub-avatar reminders-hub-avatar--due">
                        <MedicineBoxOutlined />
                        <span className="reminders-hub-online-badge" />
                      </div>
                      <div className="reminders-hub-card-body">
                        <div className="reminders-hub-card-top">
                          <div className="reminders-hub-card-title-row">
                            <span className="reminders-hub-card-title">{item.productName}</span>
                            <span className="reminders-hub-status reminders-hub-status--due">
                              {t('reminders.hubDueNow')}
                            </span>
                          </div>
                          <span className="reminders-hub-time">
                            {item.remindTime || relativeTime(item.nextRemindAt, t)}
                          </span>
                        </div>
                        <div className="reminders-hub-preview">
                          {familyName ? `${familyName} · ` : ''}
                          {item.dosageNote || t('reminders.hubDuePreview')}
                        </div>
                        <div className="reminders-hub-card-foot">
                          <span className="reminders-hub-tag reminders-hub-tag--care">
                            {t('reminders.hubTagDue')}
                          </span>
                        </div>
                        <div className="reminders-hub-actions">
                          <button
                            type="button"
                            className="reminders-hub-action reminders-hub-action--primary"
                            disabled={actingId === item.id}
                            onClick={() => void respondDue(item.id, 'taken')}
                          >
                            {t('common.taken')}
                          </button>
                          <button
                            type="button"
                            className="reminders-hub-action reminders-hub-action--ghost"
                            disabled={actingId === item.id}
                            onClick={() => void respondDue(item.id, 'snooze')}
                          >
                            {t('common.snooze15')}
                          </button>
                          <button
                            type="button"
                            className="reminders-hub-action reminders-hub-action--ghost"
                            disabled={actingId === item.id}
                            onClick={() => setSkipTarget(item)}
                          >
                            {t('common.skipped')}
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })
              : null}

            {showSchedule
              ? visibleSchedules.map((item) => {
                  const familyName = item.familyMemberId
                    ? familyNameById.get(item.familyMemberId)
                    : undefined;
                  return (
                    <div
                      key={`sch-${item.id}`}
                      className={`reminders-hub-card${item.isActive ? '' : ' reminders-hub-card--inactive'}`}
                      role="button"
                      tabIndex={0}
                      onClick={() => openEdit(item)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          openEdit(item);
                        }
                      }}
                    >
                      <div className="reminders-hub-avatar reminders-hub-avatar--schedule">
                        <img src="/home/avatars/adult-female.jpg" alt="" />
                        {item.isActive ? <span className="reminders-hub-online-badge" /> : null}
                      </div>
                      <div className="reminders-hub-card-body">
                        <div className="reminders-hub-card-top">
                          <div className="reminders-hub-card-title-row">
                            <span className="reminders-hub-card-title">{item.productName}</span>
                            <span
                              className={`reminders-hub-status${
                                item.isActive
                                  ? ' reminders-hub-status--online'
                                  : ' reminders-hub-status--off'
                              }`}
                            >
                              {item.isActive ? t('common.active') : t('common.inactive')}
                            </span>
                          </div>
                          <span className="reminders-hub-time">{item.remindTime}</span>
                        </div>
                        <div className="reminders-hub-preview">
                          {familyName ? `${familyName} · ` : ''}
                          {item.dosageNote ? `${item.dosageNote} · ` : ''}
                          {item.daysOfWeek.map((d) => day(d)).join(', ')}
                          {item.nextRemindAt
                            ? ` · ${t('common.nextAt', { time: dayjs(item.nextRemindAt).format('DD/MM HH:mm') })}`
                            : ''}
                        </div>
                        <div className="reminders-hub-card-foot">
                          <span className="reminders-hub-tag reminders-hub-tag--schedule">
                            {t('reminders.hubTagSchedule')}
                          </span>
                          <div
                            className="reminders-hub-card-side"
                            onClick={(e) => e.stopPropagation()}
                            onKeyDown={(e) => e.stopPropagation()}
                          >
                            <Switch
                              size="small"
                              checked={item.isActive}
                              loading={togglingId === item.id}
                              onChange={(checked) => void onToggleActive(item.id, checked)}
                            />
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })
              : null}

            {showRepurchase
              ? repurchaseItems.map((item) => (
                  <RepurchaseCard
                    key={`rep-${item.id}`}
                    item={item}
                    busy={repurchaseActingId === item.id}
                    onReorder={() => void onRepurchaseReorder(item.id)}
                    onCreate={
                      item.drinkRemindersCreatedAt
                        ? undefined
                        : () => void onRepurchaseAccept(item.id)
                    }
                    onSnooze={() => void onRepurchaseSnooze(item.id)}
                    onDismiss={() => void onRepurchaseDismiss(item.id)}
                  />
                ))
              : null}
          </div>
        )}
      </div>

      <button type="button" className="reminders-hub-fab" onClick={openCreate}>
        <span className="reminders-hub-fab-plus">
          <PlusOutlined />
        </span>
        {t('reminders.hubFab')}
      </button>

      <SkipReasonModal
        open={Boolean(skipTarget)}
        productName={skipTarget?.productName}
        confirmLoading={Boolean(skipTarget && actingId === skipTarget.id)}
        onCancel={() => setSkipTarget(null)}
        onConfirm={(reason) => {
          if (!skipTarget) return;
          void respondDue(skipTarget.id, 'skipped', reason);
        }}
      />

      <CustomerFormModal
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        icon={<MedicineBoxOutlined />}
        title={editing ? t('reminders.modalEdit') : t('reminders.modalAdd')}
        subtitle={t('reminders.modalSub')}
        footer={
          <FormModalFooter onCancel={() => setModalOpen(false)} onOk={() => void onSubmit()} />
        }
      >
        <Form form={form} layout="vertical" className="cfm-form" requiredMark={false}>
          <Form.Item
            name="productId"
            label={
              <FormModalLabel icon={<MedicineBoxOutlined />} required>
                {t('reminders.formProduct')}
              </FormModalLabel>
            }
            rules={[{ required: true, message: t('reminders.formProductRequired') }]}
          >
            <Select
              size="large"
              showSearch
              filterOption={false}
              loading={productSearchLoading}
              options={selectOptions}
              placeholder={t('reminders.formProductSearch')}
              onSearch={onProductSearch}
              notFoundContent={
                productSearchLoading ? <Spin size="small" /> : t('reminders.formProductNotFound')
              }
            />
          </Form.Item>
          <Form.Item
            name="familyMemberId"
            label={<FormModalLabel icon={<UserOutlined />}>{t('reminders.formTaker')}</FormModalLabel>}
          >
            <Select
              size="large"
              allowClear
              placeholder={t('reminders.formTakerSelf')}
              options={familySelectOptions}
              notFoundContent={t('reminders.formTakerEmpty')}
            />
          </Form.Item>
          <Form.Item
            name="dosageNote"
            label={<FormModalLabel icon={<FileTextOutlined />}>{t('reminders.formDosage')}</FormModalLabel>}
          >
            <Input size="large" placeholder={t('reminders.formDosagePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="remindTime"
            label={
              <FormModalLabel icon={<ClockCircleOutlined />} required>
                {t('reminders.formRemindTime')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <TimePicker size="large" format="HH:mm" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            name="daysOfWeek"
            label={
              <FormModalLabel icon={<CalendarOutlined />} required>
                {t('reminders.formDaysOfWeek')}
              </FormModalLabel>
            }
            rules={[{ required: true }]}
          >
            <Checkbox.Group options={dayOptions} />
          </Form.Item>
          {editing ? (
            <FormModalTip
              icon={<BellOutlined />}
              title={t('reminders.formActive')}
              subtitle={t('reminders.formActiveHint')}
              action={
                <Form.Item name="isActive" valuePropName="checked" noStyle>
                  <Switch />
                </Form.Item>
              }
            />
          ) : null}
        </Form>
      </CustomerFormModal>
    </div>
  );
}
