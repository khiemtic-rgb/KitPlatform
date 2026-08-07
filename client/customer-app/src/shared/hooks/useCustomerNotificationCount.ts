import { useCallback, useEffect, useState } from 'react';
import { fetchServerNotifications } from '@/shared/api/customer-app.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import {
  subscribeCustomerNotifications,
  unreadCustomerNotificationCount,
} from '@/shared/notifications/customer-notifications';

const SERVER_POLL_MS = 60_000;

export function useCustomerNotificationCount() {
  const isAuthenticated = useAuthStore((s) => Boolean(s.accessToken));
  const [count, setCount] = useState(0);

  const refresh = useCallback(async () => {
    const localUnread = unreadCustomerNotificationCount();
    if (!useAuthStore.getState().accessToken) {
      setCount(localUnread);
      return;
    }
    try {
      const { unreadCount } = await fetchServerNotifications(1);
      setCount(localUnread + unreadCount);
    } catch {
      setCount(localUnread);
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      setCount(unreadCustomerNotificationCount());
      return;
    }
    void refresh();
    const onLocalChange = () => void refresh();
    const onServerChange = () => void refresh();
    const unsubLocal = subscribeCustomerNotifications(onLocalChange);
    window.addEventListener('server-notifications-changed', onServerChange);
    const timer = window.setInterval(() => void refresh(), SERVER_POLL_MS);
    return () => {
      unsubLocal();
      window.removeEventListener('server-notifications-changed', onServerChange);
      window.clearInterval(timer);
    };
  }, [isAuthenticated, refresh]);

  return count;
}

export function notifyServerNotificationsChanged() {
  window.dispatchEvent(new CustomEvent('server-notifications-changed'));
}
