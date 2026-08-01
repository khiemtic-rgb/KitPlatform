import { BellOutlined, CalendarOutlined, DeleteOutlined, ShoppingCartOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { RepurchaseSuggestion } from '@/shared/api/customer-app.types';
import { repurchaseCardTitle, repurchaseDatedTitle } from '@/modules/reminders/repurchase-card-title';
import './RepurchaseCard.css';

function stampDate(item: RepurchaseSuggestion): string {
  const source = item.suggestedForDate || item.orderDate;
  if (!source) return '—';
  const d = dayjs(source);
  return d.isValid() ? d.format('DD/MM') : '—';
}

function remainingSupply(item: RepurchaseSuggestion): { remaining: number; supply: number } | null {
  const supply = item.reminderDaysSupply;
  if (supply == null || supply <= 0) return null;

  if (item.suggestedForDate) {
    const remaining = Math.max(
      0,
      dayjs(item.suggestedForDate).startOf('day').diff(dayjs().startOf('day'), 'day'),
    );
    return { remaining, supply };
  }

  if (item.orderDate) {
    const elapsed = Math.max(0, dayjs().startOf('day').diff(dayjs(item.orderDate).startOf('day'), 'day'));
    return { remaining: Math.max(0, supply - elapsed), supply };
  }

  return { remaining: supply, supply };
}

function isSupplyExpired(item: RepurchaseSuggestion, remain: { remaining: number; supply: number } | null): boolean {
  if (remain) return remain.remaining <= 0;
  if (!item.suggestedForDate) return false;
  return !dayjs(item.suggestedForDate).startOf('day').isAfter(dayjs().startOf('day'));
}

export function RepurchaseCard({
  item,
  busy,
  onReorder,
  onCreate,
  onSnooze,
  onDismiss,
}: {
  item: RepurchaseSuggestion;
  busy?: boolean;
  onReorder?: () => void;
  onCreate?: () => void;
  onSnooze: () => void;
  onDismiss: () => void;
}) {
  const { t } = useTranslation();
  const reminderCreated = Boolean(item.drinkRemindersCreatedAt);
  const showCreate = !reminderCreated && Boolean(onCreate);
  const showReorder = Boolean(onReorder) && item.status !== 'converted';
  const orderLine = t('repurchase.orderLine', { orderNumber: item.orderNumber });
  const datedTitle = repurchaseDatedTitle(item, (date) => t('repurchase.orderDatedTitle', { date }));
  const title = repurchaseCardTitle(item, { orderLine, datedTitle });
  const remain = remainingSupply(item);
  const expired = isSupplyExpired(item, remain);
  const stamp = stampDate(item);

  const secondaryClass = showCreate
    ? 'rp-card-actions-row rp-card-actions-row--three'
    : 'rp-card-actions-row rp-card-actions-row--two';

  return (
    <article className="rp-card">
      <div className="rp-card-main">
        <span className="rp-card-icon" aria-hidden>
          <ShoppingCartOutlined />
          <span className="rp-card-icon-plus">+</span>
        </span>

        <div className="rp-card-body">
          <div className="rp-card-top">
            <h3 className="rp-card-title">{title}</h3>
            {expired ? (
              <span className="rp-card-status rp-card-status--expired">
                <span className="rp-card-status-dot" aria-hidden />
                {t('repurchase.expiredBadge')}
              </span>
            ) : remain ? (
              <span className="rp-card-status rp-card-status--remain">
                <CalendarOutlined />
                {t('repurchase.remainBadge', {
                  remaining: remain.remaining,
                  supply: remain.supply,
                })}
              </span>
            ) : reminderCreated ? (
              <span className="rp-card-status rp-card-status--remain">
                <BellOutlined />
                {t('repurchase.reminderCreatedTag')}
              </span>
            ) : null}
            <span className="rp-card-stamp">
              <CalendarOutlined />
              {stamp}
            </span>
          </div>

          <div className="rp-card-meta">
            <span>{orderLine}</span>
            {item.suggestedForDate ? (
              <>
                <span className="rp-card-sep" aria-hidden>
                  |
                </span>
                <span>
                  {t('repurchase.expectedRunOutPrefix')}{' '}
                  <em>{dayjs(item.suggestedForDate).format('DD/MM/YYYY')}</em>
                </span>
              </>
            ) : null}
          </div>

          <span className="rp-card-tag">
            <BellOutlined />
            {t('repurchase.medRemindTag')}
          </span>
        </div>
      </div>

      <div className="rp-card-actions">
        {showReorder ? (
          <button
            type="button"
            className="rp-card-btn rp-card-btn--reorder"
            disabled={busy}
            onClick={onReorder}
          >
            <ShoppingCartOutlined />
            {t('repurchase.reorder')}
          </button>
        ) : null}
        <div className={secondaryClass}>
          {showCreate ? (
            <button
              type="button"
              className="rp-card-btn rp-card-btn--primary"
              disabled={busy}
              onClick={onCreate}
            >
              <BellOutlined />
              {t('repurchase.createReminder')}
            </button>
          ) : null}
          <button
            type="button"
            className="rp-card-btn rp-card-btn--snooze"
            disabled={busy}
            onClick={onSnooze}
          >
            <CalendarOutlined />
            {t('repurchase.snooze3Days')}
          </button>
          <button
            type="button"
            className="rp-card-btn rp-card-btn--dismiss"
            disabled={busy}
            onClick={onDismiss}
          >
            <DeleteOutlined />
            {t('repurchase.dismiss')}
          </button>
        </div>
      </div>
    </article>
  );
}
