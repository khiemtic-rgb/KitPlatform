import { useEffect, useRef, useState } from 'react';
import { App } from 'antd';
import { ShoppingOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { fetchDraftOrders } from '@/shared/api/customer-app.api';
import { CUSTOMER_DRAFT_ORDER_STATUS, type CustomerDraftOrderListItem } from '@/shared/api/customer-app.types';
import { useApiHealth } from '@/shared/api/useApiHealth';
import { useAuthStore } from '@/shared/auth/auth.store';
import { emitDraftOrderAlerts } from '@/shared/hooks/draft-order-alert-bus';
import {
  addCustomerNotification,
} from '@/shared/notifications/customer-notifications';
import {
  buildCustomerDraftOrderEventsUrl,
  subscribeChatSse,
} from '@/shared/hooks/chat-sse';
import {
  filterUnseenSentDrafts,
  markSentDraftsSeen,
} from '@/shared/hooks/draft-order-seen';

const FALLBACK_POLL_MS = 30_000;

function pendingSentDrafts(items: CustomerDraftOrderListItem[]) {
  return items.filter((o) => o.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent);
}

function formatMoney(value: number) {
  return value.toLocaleString('vi-VN') + 'đ';
}

function notifyNewDrafts(
  drafts: CustomerDraftOrderListItem[],
  notification: ReturnType<typeof App.useApp>['notification'],
  navigate: ReturnType<typeof useNavigate>,
  onOrdersPage: boolean,
) {
  if (drafts.length === 0) return;

  emitDraftOrderAlerts(drafts);

  for (const draft of drafts) {
    addCustomerNotification({
      kind: 'draft_order',
      dedupeKey: `draft-sent-${draft.id}`,
      title: 'Đơn thuốc mới',
      body: `${draft.draftNumber} — tổng tạm tính ${formatMoney(draft.totalAmount)}.`,
      href: '/orders',
    });
  }

  if (onOrdersPage) {
    return;
  }

  for (const draft of drafts) {
    notification.open({
      message: 'Đơn thuốc mới',
      description: `${draft.draftNumber} — tổng tạm tính ${formatMoney(draft.totalAmount)}. Xem và xác nhận (tuỳ chọn).`,
      icon: <ShoppingOutlined style={{ color: '#0f766e' }} />,
      placement: 'top',
      duration: 8,
      onClick: () => navigate('/orders'),
    });
  }
}

/** Badge đơn chưa xem + toast / banner khi dược sĩ gửi đơn mới (SSE + poll). */
export function useCustomerDraftOrderAlerts() {
  const { notification } = App.useApp();
  const navigate = useNavigate();
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const { online } = useApiHealth();
  const [unseenCount, setUnseenCount] = useState(0);
  const announcedIdsRef = useRef<Set<string>>(new Set());

  const onOrdersPage = location.pathname.startsWith('/orders');

  useEffect(() => {
    if (!accessToken || online === false) {
      return;
    }

    let cancelled = false;

    const poll = async () => {
      try {
        const orders = await fetchDraftOrders();
        if (cancelled) return;

        const pending = pendingSentDrafts(orders);
        const unseen = filterUnseenSentDrafts(pending);
        setUnseenCount(unseen.length);

        const toAnnounce = unseen.filter((o) => !announcedIdsRef.current.has(o.id));
        if (toAnnounce.length === 0) {
          return;
        }

        toAnnounce.forEach((o) => announcedIdsRef.current.add(o.id));
        notifyNewDrafts(toAnnounce, notification, navigate, onOrdersPage);
      } catch {
        // giữ badge cũ
      }
    };

    void poll();
    const timer = window.setInterval(() => void poll(), FALLBACK_POLL_MS);
    const unsubscribeSse = subscribeChatSse(
      buildCustomerDraftOrderEventsUrl(accessToken),
      () => void poll(),
    );

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      unsubscribeSse();
    };
  }, [accessToken, online, onOrdersPage, notification, navigate]);

  if (onOrdersPage) {
    return 0;
  }

  return unseenCount;
}

export { filterUnseenSentDrafts, markSentDraftsSeen };
