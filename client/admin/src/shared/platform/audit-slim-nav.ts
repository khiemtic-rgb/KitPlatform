import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';

/**
 * DEMO_PHARMACY audit handoff: slim sidebar/tabs so thẩm định không lạc sang
 * People / Cockpit / App KH / pack thương mại / gộp SP.
 * Opt-in via settings.platform.features.audit_slim_nav === true (chỉ DEMO).
 */
export function isAuditSlimNav(): boolean {
  const { loaded, isFeatureEnabled } = useTenantPlatformStore.getState();
  return loaded && isFeatureEnabled('audit_slim_nav');
}

export function useAuditSlimNav(): boolean {
  return useTenantPlatformStore((s) => s.loaded && s.isFeatureEnabled('audit_slim_nav'));
}
