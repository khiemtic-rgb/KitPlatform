import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { fetchChatThread } from '@/shared/api/customer-app.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { buildCustomerChatEventsUrl, subscribeChatSse } from '@/shared/hooks/chat-sse';

const FALLBACK_POLL_MS = 30_000;

export function useCustomerChatUnread() {
  const location = useLocation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [unreadCount, setUnreadCount] = useState(0);
  const onChatPage = location.pathname.startsWith('/chat');

  useEffect(() => {
    if (!accessToken) {
      setUnreadCount(0);
      return;
    }

    if (onChatPage) {
      setUnreadCount(0);
      return;
    }

    let cancelled = false;

    const poll = async () => {
      try {
        const thread = await fetchChatThread();
        if (cancelled) return;
        setUnreadCount(Math.max(0, thread.unreadCount));
      } catch {
        // badge giữ giá trị cũ
      }
    };

    void poll();
    const timer = window.setInterval(() => void poll(), FALLBACK_POLL_MS);
    const unsubscribeSse = subscribeChatSse(buildCustomerChatEventsUrl(), accessToken, () => void poll());

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      unsubscribeSse();
    };
  }, [accessToken, onChatPage]);

  return unreadCount;
}
