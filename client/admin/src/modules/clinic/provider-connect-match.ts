import type { ClinicProvider } from '@/shared/api/clinic.api';
import type { ConnectDoctorMembership } from '@/shared/api/connect.api';

function norm(s?: string | null) {
  return (s ?? '').trim().toLowerCase().replace(/\s+/g, ' ');
}

/** Soft-match Connect membership → local clinic_provider (license then name). */
export function suggestProviderIdFromConnect(
  providers: ClinicProvider[],
  memberships: ConnectDoctorMembership[],
): string | undefined {
  const active = memberships.filter((m) => m.membershipStatus === 'active');
  for (const m of active) {
    const linked = providers.find((p) => p.connectDoctorId === m.doctorId && p.status === 1);
    if (linked) return linked.id;
  }
  for (const m of active) {
    const lic = norm(m.doctorLicenseNumber);
    if (!lic) continue;
    const byLic = providers.find((p) => p.status === 1 && norm(p.licenseNo) === lic);
    if (byLic) return byLic.id;
  }
  for (const m of active) {
    const name = norm(m.doctorFullName);
    if (!name) continue;
    const hits = providers.filter((p) => p.status === 1 && norm(p.displayName) === name);
    if (hits.length === 1) return hits[0].id;
  }
  return undefined;
}

export function connectBadgeForProvider(
  provider: ClinicProvider,
  memberships: ConnectDoctorMembership[],
): string | undefined {
  if (provider.connectDoctorId) {
    const m = memberships.find((x) => x.doctorId === provider.connectDoctorId);
    return m ? `Connect · ${m.doctorFullName}` : 'Connect';
  }
  const lic = norm(provider.licenseNo);
  if (lic) {
    const m = memberships.find(
      (x) => x.membershipStatus === 'active' && norm(x.doctorLicenseNumber) === lic,
    );
    if (m) return `Gợi ý Connect · ${m.doctorFullName}`;
  }
  const name = norm(provider.displayName);
  const hits = memberships.filter(
    (x) => x.membershipStatus === 'active' && norm(x.doctorFullName) === name,
  );
  if (hits.length === 1) return `Gợi ý Connect · ${hits[0].doctorFullName}`;
  return undefined;
}
