import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  fetchCustomerDraftOrders,
} from '@/shared/api/customer-draft-orders.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildAdminDraftOrderEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';
import { showDesktopNotification } from '@/shared/utils/desktop-notification';

const FALLBACK_POLL_MS = 30_000;
const POLL_STATUSES = [
  CUSTOMER_DRAFT_ORDER_STATUS.Sent,
  CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
  CUSTOMER_DRAFT_ORDER_STATUS.Cancelled,
];

/** Số đơn tạm cần xử lý + thông báo desktop khi khách xác nhận / hủy. */
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

    const poll = async () => {
      try {
        const items = await fetchCustomerDraftOrders(undefined, POLL_STATUSES);
        if (cancelled) return;

        setCount(
          items.filter(
            (item) =>
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
              item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
          ).length,
        );

        const confirmed = items.filter((item) => item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed);
        const cancelledOrders = items.filter((item) => item.status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled);
        const confirmedIds = new Set(confirmed.map((item) => item.id));
        const cancelledIds = new Set(cancelledOrders.map((item) => item.id));
        const newConfirmed = confirmed.filter((item) => !knownConfirmedIdsRef.current.has(item.id));
        const newCancelled = cancelledOrders.filter((item) => !knownCancelledIdsRef.current.has(item.id));

        const onDraftPage = location.pathname.startsWith('/sales/customer-draft-orders');
        const shouldNotify = initializedRef.current && (!onDraftPage || document.hidden);

        if (shouldNotify && newConfirmed.length > 0) {
          for (const order of newConfirmed) {
            showDesktopNotification(
              'Khách xác nhận đơn tạm',
              `${order.draftNumber} — sẵn sàng nạp POS tại quầy.`,
              `draft-confirmed-${order.id}`,
            );
          }
        }

        if (shouldNotify && newCancelled.length > 0) {
          for (const order of newCancelled) {
            showDesktopNotification(
              'Khách hủy đơn tạm',
              `${order.draftNumber} — không cần xử lý tại quầy.`,
              `draft-cancelled-${order.id}`,
            );
          }
        }

        initializedRef.current = true;
        knownConfirmedIdsRef.current = confirmedIds;
        knownCancelledIdsRef.current = cancelledIds;
      } catch {
        // im lặng
      }
    };

    void poll();
    const timer = window.setInterval(() => void poll(), FALLBACK_POLL_MS);
    const unsubscribeSse = accessToken
      ? subscribeChatSse(buildAdminDraftOrderEventsUrl(accessToken), () => void poll())
      : () => undefined;

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      unsubscribeSse();
    };
  }, [enabled, location.pathname, accessToken]);

  return count;
}
