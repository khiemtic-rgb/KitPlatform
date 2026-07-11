import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { fetchChatThreads } from '@/shared/api/chat.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { salesT } from '@/shared/i18n';
import { buildAdminChatEventsUrl, subscribeChatSse } from '@/shared/utils/chat-sse';
import { showDesktopNotification } from '@/shared/utils/desktop-notification';

const FALLBACK_POLL_MS = 60_000;
const FALLBACK_POLL_HIDDEN_MS = 120_000;
const SSE_POLL_DEBOUNCE_MS = 1_000;

export function useAdminChatUnread(enabled = true) {
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [totalUnread, setTotalUnread] = useState(0);
  const prevUnreadRef = useRef(0);
  const initializedRef = useRef(false);

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;
    let pollTimer: number | undefined;
    let sseTimer: number | undefined;

    const poll = async () => {
      try {
        const threads = await fetchChatThreads();
        if (cancelled) return;

        const total = threads.reduce((sum, thread) => sum + thread.staffUnreadCount, 0);
        const onChatPage = location.pathname.startsWith('/sales/chat');
        const shouldNotify =
          initializedRef.current &&
          total > prevUnreadRef.current &&
          total > 0 &&
          (!onChatPage || document.hidden);

        if (shouldNotify) {
          const t = salesT();
          const latest = threads.find((thread) => thread.staffUnreadCount > 0);
          const preview = latest?.lastMessagePreview?.trim() || t('customerChat.notifications.newMessagePreview');
          showDesktopNotification(
            t('customerChat.notifications.newMessageTitle'),
            latest
              ? `${latest.customerName}: ${preview}`
              : t('customerChat.notifications.unreadBody', { count: total }),
            latest ? `chat-${latest.customerId}` : 'chat-unread',
          );
        }

        initializedRef.current = true;
        prevUnreadRef.current = total;
        setTotalUnread((prev) => (prev === total ? prev : total));
      } catch {
        // im lặng — badge sẽ thử lại ở lần poll sau
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
      ? subscribeChatSse(buildAdminChatEventsUrl(accessToken), () => {
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

  return totalUnread;
}
