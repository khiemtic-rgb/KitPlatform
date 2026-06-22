import { useMemo } from 'react';
import { useAuthStore } from '@/shared/auth/auth.store';

const STAFF_MAX_PERCENT = 10;

export function useSalesDiscountPolicy() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isAdmin = user?.roles.includes('ADMIN') ?? false;
    const unlimited =
      isAdmin || (user?.permissions?.includes('sales.discount.unlimited') ?? false);
    const canDiscount = unlimited || (user?.permissions?.includes('sales.discount') ?? false);
    const maxPercent = unlimited ? 100 : canDiscount ? STAFF_MAX_PERCENT : 0;

    return { canDiscount, unlimited, maxPercent };
  }, [user]);
}
