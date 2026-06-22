import { useAuthStore } from '@/shared/auth/auth.store';

export function useHasPermission(permission: string): boolean {
  const user = useAuthStore((s) => s.user);
  if (!user) return false;
  if (user.roles.includes('ADMIN')) return true;
  return user.permissions?.includes(permission) ?? false;
}

export function useProcurementWrite(): boolean {
  return useHasPermission('procurement.write');
}

export function useSystemDeletePermanent(): boolean {
  return useHasPermission('system.delete_permanent');
}
