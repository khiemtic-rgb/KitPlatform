import { useCallback, useEffect, useMemo, useState } from 'react';
import { Select, Spin, message } from 'antd';
import { ArrowLeftOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  fetchActiveMedications,
  fetchFamilyMembers,
  fetchRepurchaseSuggestions,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import type { ActiveMedication, FamilyMember, RepurchaseSuggestion } from '@/shared/api/customer-app.types';
import { useAuthStore } from '@/shared/auth/auth.store';
import { RepurchaseSuggestionsPanel } from '@/modules/reminders/RepurchaseSuggestionsPanel';
import { PharmacyLinkSoftBanner } from '@/shared/components/PharmacyLinkGate';
import '@/shared/components/EntryPage.css';
import './MyMedicationPage.css';

type FamilyFilter = 'all' | 'self' | string;

function isVisibleRepurchase(item: RepurchaseSuggestion) {
  if (item.status === 'dismissed' || item.status === 'expired' || item.status === 'converted') return false;
  if (item.status === 'snoozed' && item.snoozedUntil) {
    return dayjs().isAfter(dayjs(item.snoozedUntil));
  }
  return item.status === 'pending' || item.status === 'snoozed';
}

function MedicationCard({
  item,
  personLabel,
}: {
  item: ActiveMedication;
  personLabel: string | null;
}) {
  const { t } = useTranslation();
  const lowSupply = item.daysRemaining != null && item.daysRemaining <= 3;

  const daysLabel =
    item.daysRemaining == null
      ? t('medications.daysUnknown')
      : item.daysRemaining <= 0
        ? t('medications.daysMayRunOut')
        : t('medications.daysRemaining', { days: item.daysRemaining });

  const timeline = item.timeline.slice(-5);

  return (
    <article className="med-card">
      <div className="med-card-head">
        <h2 className="med-card-name">{item.productName}</h2>
        <span className={`med-card-badge${lowSupply ? ' med-card-badge--warn' : ''}`}>{daysLabel}</span>
      </div>

      {personLabel ? <div className="med-card-meta">{t('medications.forPerson', { name: personLabel })}</div> : null}

      {item.remindTime ? (
        <div className="med-card-meta">
          {t('medications.remindAt', { time: item.remindTime })}
          {item.dosageNote ? ` · ${item.dosageNote}` : ''}
        </div>
      ) : null}

      {item.lastOrderNumber ? (
        <div className="med-card-meta">
          {t('medications.purchased', { orderNumber: item.lastOrderNumber })}
          {item.lastOrderDate ? ` · ${dayjs(item.lastOrderDate).format('DD/MM/YYYY')}` : ''}
        </div>
      ) : null}

      {lowSupply ? (
        <div className="med-card-alert">
          <span>{t('medications.lowSupplyAlert')}</span>
          <Link to="/reservations" className="med-card-alert-cta">
            {t('medications.reserveMed')}
          </Link>
        </div>
      ) : null}

      {timeline.length > 0 ? (
        <ol className="med-timeline">
          {timeline.map((ev, index) => (
            <li key={`${ev.occurredAt}-${index}`} className="med-timeline-item">
              <span className="med-timeline-dot" aria-hidden />
              <div className="med-timeline-copy">
                <div className="med-timeline-label">{ev.label}</div>
                <div className="med-timeline-date">{dayjs(ev.occurredAt).format('DD/MM/YYYY')}</div>
              </div>
            </li>
          ))}
        </ol>
      ) : null}

      <Link to={`/ai?productId=${item.productId}`} className="med-card-ai">
        {t('medications.askCopilot')}
      </Link>
    </article>
  );
}

export function MyMedicationPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [items, setItems] = useState<ActiveMedication[]>([]);
  const [repurchase, setRepurchase] = useState<RepurchaseSuggestion[]>([]);
  const [family, setFamily] = useState<FamilyMember[]>([]);
  const [familyFilter, setFamilyFilter] = useState<FamilyFilter>('all');
  const [loading, setLoading] = useState(true);
  const [activeMedsUnavailable, setActiveMedsUnavailable] = useState(false);

  const familyFilterOptions = useMemo(
    () => [
      { value: 'all', label: t('medications.filterAll') },
      { value: 'self', label: t('medications.filterSelf') },
      ...family.map((member) => ({ value: member.id, label: member.fullName })),
    ],
    [family, t],
  );

  const resolvePersonLabel = useCallback(
    (familyMemberId: string | null) => {
      if (!familyMemberId) return t('health.self');
      return family.find((member) => member.id === familyMemberId)?.fullName ?? t('health.familyMember');
    },
    [family, t],
  );

  const load = useCallback(async () => {
    if (!useAuthStore.getState().isAuthenticated()) {
      setLoading(false);
      setItems([]);
      return;
    }
    setLoading(true);
    setActiveMedsUnavailable(false);
    try {
      const medParams =
        familyFilter === 'all'
          ? undefined
          : familyFilter === 'self'
            ? { forSelf: true }
            : { familyMemberId: familyFilter };

      const [medsResult, suggestionsResult, familyResult] = await Promise.allSettled([
        fetchActiveMedications(medParams),
        fetchRepurchaseSuggestions(),
        fetchFamilyMembers(),
      ]);

      if (medsResult.status === 'fulfilled') {
        setItems(medsResult.value);
      } else {
        setItems([]);
        setActiveMedsUnavailable(true);
      }

      if (suggestionsResult.status === 'fulfilled') {
        setRepurchase(suggestionsResult.value.filter(isVisibleRepurchase));
      } else {
        setRepurchase([]);
        message.error(getApiErrorMessage(suggestionsResult.reason, t('medications.loadRepurchaseFailed')));
      }

      if (familyResult.status === 'fulfilled') {
        setFamily(familyResult.value.filter((member) => member.status === 1));
      }
    } finally {
      setLoading(false);
    }
  }, [familyFilter, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const hasRepurchase = repurchase.length > 0;

  return (
    <div className="entry-page med-page">
      <button type="button" className="entry-page-home" onClick={() => navigate('/')}>
        <ArrowLeftOutlined />
        {t('common.backHome')}
      </button>

      <h1 className="entry-page-title">{t('medications.title')}</h1>
      <p className="entry-page-intro">{t('medications.intro')}</p>
      <PharmacyLinkSoftBanner />

      <div className="med-filter">
        <span className="entry-label">{t('medications.filterLabel')}</span>
        <Select
          size="large"
          style={{ width: '100%' }}
          value={familyFilter}
          options={familyFilterOptions}
          onChange={(value) => setFamilyFilter(value as FamilyFilter)}
        />
      </div>

      {loading ? (
        <div className="entry-page-loading">
          <Spin />
        </div>
      ) : (
        <>
          {activeMedsUnavailable ? (
            <div className="med-warn">
              <div className="med-warn-title">{t('medications.apiUnavailableTitle')}</div>
              <div className="med-warn-desc">{t('medications.apiUnavailableDesc')}</div>
            </div>
          ) : null}

          {items.length > 0 ? (
            <div className="med-list">
              {items.map((item) => (
                <MedicationCard
                  key={`${item.productId}-${item.familyMemberId ?? 'self'}`}
                  item={item}
                  personLabel={
                    item.familyMemberId || familyFilter !== 'all'
                      ? resolvePersonLabel(item.familyMemberId)
                      : null
                  }
                />
              ))}
            </div>
          ) : null}

          {items.length === 0 && !hasRepurchase ? (
            <div className="entry-empty" style={{ marginTop: 14 }}>
              <InboxOutlined className="entry-empty-icon" />
              <span>{t('medications.empty')}</span>
              <div className="entry-actions" style={{ width: '100%', marginTop: 8 }}>
                <button
                  type="button"
                  className="entry-btn entry-btn--primary"
                  onClick={() => navigate('/reminders')}
                >
                  {t('medications.addReminder')}
                </button>
                <button type="button" className="entry-btn entry-btn--ghost" onClick={() => navigate('/orders')}>
                  {t('medications.viewOrders')}
                </button>
              </div>
            </div>
          ) : null}

          {hasRepurchase ? (
            <div className="med-repurchase">
              <RepurchaseSuggestionsPanel onAccepted={() => void load()} />
            </div>
          ) : null}
        </>
      )}

      <button
        type="button"
        className="med-fab"
        aria-label={t('medications.addReminder')}
        onClick={() => navigate('/reminders')}
      >
        <PlusOutlined />
      </button>
    </div>
  );
}
