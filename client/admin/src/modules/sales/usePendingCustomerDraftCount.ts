import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  fetchCustomerDraftOrders,
} from '@/shared/api/customer-draft-orders.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { salesT } from '@/shared/i18n';
import { buildAdminDraftOrderEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';
import { showDesktopNotification } from '@/shared/utils/desktop-notification';

const FALLBACK_POLL_MS = 60_000;
const FALLBACK_POLL_HIDDEN_MS = 120_000;
const SSE_POLL_DEBOUNCE_MS = 1_000;
const POLL_STATUSES = [
  CUSTOMER_DRAFT_ORDER_STATUS.Sent,
  CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
  CUSTOMER_DRAFT_ORDER_STATUS.Cancelled,
];

/** Số đơn từ app cần xử lý + thông báo desktop khi khách xác nhận / hủy. */
export function usePendingCustomerDraftCount(enabled = true) {
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [count, setCount] = useState(0);
  const knownConfirmedIdsRef = useRef<Set<string>>(new Set());
  const knownCancelledIdsRef = useRef<Set<string>>(new Set());
  const initializedRef = useRef(false);

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;
    let pollTimer: number | undefined;
    let sseTimer: number | undefined;

    const poll = async () => {
      try {
        const items = await fetchCustomerDraftOrders(undefined, POLL_STATUSES);
        if (cancelled) return;

        const nextCount = items.filter(
          (item) =>
            item.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
            item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
        ).length;

        const confirmed = items.filter((item) => item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed);
        const cancelledOrders = items.filter((item) => item.status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled);
        const confirmedIds = new Set(confirmed.map((item) => item.id));
        const cancelledIds = new Set(cancelledOrders.map((item) => item.id));
        const newConfirmed = confirmed.filter((item) => !knownConfirmedIdsRef.current.has(item.id));
        const newCancelled = cancelledOrders.filter((item) => !knownCancelledIdsRef.current.has(item.id));

        const onDraftPage = location.pathname.startsWith('/sales/app-orders/drafts');
        const shouldNotify = initializedRef.current && (!onDraftPage || document.hidden);

        if (shouldNotify && newConfirmed.length > 0) {
          const t = salesT();
          for (const order of newConfirmed) {
            showDesktopNotification(
              t('customerDrafts.notifications.confirmedTitle'),
              t('customerDrafts.notifications.confirmedBody', { draftNumber: order.draftNumber }),
              `draft-confirmed-${order.id}`,
            );
          }
        }

        if (shouldNotify && newCancelled.length > 0) {
          const t = salesT();
          for (const order of newCancelled) {
            showDesktopNotification(
              t('customerDrafts.notifications.cancelledTitle'),
              t('customerDrafts.notifications.cancelledBody', { draftNumber: order.draftNumber }),
              `draft-cancelled-${order.id}`,
            );
          }
        }

        initializedRef.current = true;
        knownConfirmedIdsRef.current = confirmedIds;
        knownCancelledIdsRef.current = cancelledIds;
        setCount((prev) => (prev === nextCount ? prev : nextCount));
      } catch {
        // im lặng
      }
    };

    const schedulePoll = (delayMs: number) => {
      if (pollTimer) window.clearTimeout(pollTimer);
      pollTimer = window.setTimeout(() => {
        pollTimer = undefined;
        void poll();
      }, delayMs);
    };

    void poll();
    let timer = window.setInterval(() => schedulePoll(0), FALLBACK_POLL_MS);
    const scheduleFallback = () => {
      const delay = document.hidden ? FALLBACK_POLL_HIDDEN_MS : FALLBACK_POLL_MS;
      window.clearInterval(timer);
      timer = window.setInterval(() => schedulePoll(0), delay);
    };
    const onVisibility = () => scheduleFallback();
    document.addEventListener('visibilitychange', onVisibility);
    const unsubscribeSse = accessToken
      ? subscribeChatSse(buildAdminDraftOrderEventsUrl(accessToken), () => {
          if (sseTimer) return;
          sseTimer = window.setTimeout(() => {
            sseTimer = undefined;
            schedulePoll(0);
          }, SSE_POLL_DEBOUNCE_MS);
        })
      : () => undefined;

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      if (pollTimer) window.clearTimeout(pollTimer);
      if (sseTimer) window.clearTimeout(sseTimer);
      document.removeEventListener('visibilitychange', onVisibility);
      unsubscribeSse();
    };
  }, [enabled, location.pathname, accessToken]);

  return count;
}
