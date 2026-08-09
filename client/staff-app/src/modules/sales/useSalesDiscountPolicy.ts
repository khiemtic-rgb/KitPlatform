import { useMemo } from 'react';
import { useAuthStore } from '@/shared/auth/auth.store';

const STAFF_MAX_PERCENT = 10;

export function useSalesDiscountPolicy() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const roles = user?.roles ?? [];
    const perms = user?.permissions ?? [];
    const isAdmin = roles.includes('ADMIN');
    const isManager = roles.includes('MANAGER');
    const unlimited =
      isAdmin || perms.includes('sales.discount.unlimited');
    const canDiscount = unlimited || perms.includes('sales.discount');
    const maxPercent = unlimited ? 100 : canDiscount ? STAFF_MAX_PERCENT : 0;
    const canPriceOverride =
      isAdmin ||
      isManager ||
      perms.includes('sales.price.override') ||
      perms.includes('sales.discount.unlimited');

    return { canDiscount, unlimited, maxPercent, canPriceOverride };
  }, [user]);
}
