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
const ACTIVE_STATUSES = [CUSTOMER_DRAFT_ORDER_STATUS.Sent, CUSTOMER_DRAFT_ORDER_STATUS.Confirmed];

/** Số đơn tạm cần xử lý + thông báo desktop khi khách xác nhận. */
export function usePendingCustomerDraftCount(enabled = true) {
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [count, setCount] = useState(0);
  const knownConfirmedIdsRef = useRef<Set<string>>(new Set());
  const initializedRef = useRef(false);

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;

    const poll = async () => {
      try {
        const items = await fetchCustomerDraftOrders(undefined, ACTIVE_STATUSES);
        if (cancelled) return;

        setCount(items.length);

        const confirmed = items.filter((item) => item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed);
        const confirmedIds = new Set(confirmed.map((item) => item.id));
        const newConfirmed = confirmed.filter((item) => !knownConfirmedIdsRef.current.has(item.id));

        const onDraftPage = location.pathname.startsWith('/sales/customer-draft-orders');
        const shouldNotify =
          initializedRef.current &&
          newConfirmed.length > 0 &&
          (!onDraftPage || document.hidden);

        if (shouldNotify) {
          for (const order of newConfirmed) {
            showDesktopNotification(
              'Khách xác nhận đơn tạm',
              `${order.draftNumber} — sẵn sàng nạp POS tại quầy.`,
              `draft-confirmed-${order.id}`,
            );
          }
        }

        initializedRef.current = true;
        knownConfirmedIdsRef.current = confirmedIds;
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
