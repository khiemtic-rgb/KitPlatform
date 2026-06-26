import { useEffect, useState } from 'react';
import {
  subscribeCustomerNotifications,
  unreadCustomerNotificationCount,
} from '@/shared/notifications/customer-notifications';

export function useCustomerNotificationCount() {
  const [count, setCount] = useState(unreadCustomerNotificationCount);

  useEffect(() => {
    const refresh = () => setCount(unreadCustomerNotificationCount());
    refresh();
    return subscribeCustomerNotifications(refresh);
  }, []);

  return count;
}
