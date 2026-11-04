export interface PharmacyVersionRef {
  ruleCode: string;
  version: string;
  lifecycleStatus: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  sourceVerificationStatus?: string | null;
}

export interface PharmacyPinRef {
  ruleCode: string;
  ruleVersion: string;
  status: string;
}

export type PharmacyListGroup = 'inUse' | 'inactivePin' | 'notAdopted' | 'newer' | 'older';

export function localKnowledgeToday(now = new Date()) {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

export function compareKnowledgeVersions(left: string, right: string) {
  const leftNumber = Number(left);
  const rightNumber = Number(right);
  const leftExact = Number.isInteger(leftNumber) && String(leftNumber) === left;
  const rightExact = Number.isInteger(rightNumber) && String(rightNumber) === right;
  if (leftExact && rightExact) return leftNumber - rightNumber;
  return left.localeCompare(right, 'en');
}

export function knowledgeVersionInEffect(
  effectiveFrom: string,
  effectiveTo: string | null | undefined,
  today: string,
) {
  const from = effectiveFrom.slice(0, 10);
  const to = effectiveTo?.slice(0, 10);
  if (from > today) return false;
  if (to && to < today) return false;
  return true;
}

export function activePin<T extends PharmacyPinRef>(pins: T[], ruleCode: string) {
  return pins.find((pin) => pin.ruleCode === ruleCode && pin.status === 'APPROVED');
}

export function pharmacyListGroup(
  row: PharmacyVersionRef,
  pin: PharmacyPinRef | undefined,
  today: string,
): PharmacyListGroup {
  if (!pin || pin.status !== 'APPROVED' || pin.ruleCode !== row.ruleCode) return 'notAdopted';
  const order = compareKnowledgeVersions(row.version, pin.ruleVersion);
  if (order === 0) {
    return row.lifecycleStatus === 'PUBLISHED'
      && knowledgeVersionInEffect(row.effectiveFrom, row.effectiveTo, today)
      ? 'inUse'
      : 'inactivePin';
  }
  if (order > 0 && row.lifecycleStatus === 'PUBLISHED') return 'newer';
  return 'older';
}

/** Matches the current Copilot read: exact APPROVED pin, PUBLISHED, in effect, not KR_OTHER. */
export function runtimeUsesVersion(row: PharmacyVersionRef, pin: PharmacyPinRef | undefined, today: string) {
  return row.ruleCode !== 'KR_OTHER' && pharmacyListGroup(row, pin, today) === 'inUse';
}

/** Pin của nhà thuốc: claim tường minh, hoặc vai trò ADMIN của tenant trên session. */
export function canApproveTenantKnowledge(
  roles: readonly string[] | undefined,
  permissions: readonly string[] | undefined,
) {
  return Boolean(
    permissions?.includes('pharmacy.knowledge.approve')
    || roles?.includes('ADMIN'),
  );
}

export function pinBlockers(row: PharmacyVersionRef, today: string): string[] {
  const blockers: string[] = [];
  if (row.lifecycleStatus !== 'PUBLISHED') blockers.push('Chỉ ghim phiên bản PUBLISHED.');
  if (row.ruleCode === 'KR_OTHER') blockers.push('KR_OTHER không dùng tại quầy.');
  if (row.sourceVerificationStatus !== 'ACCEPTED') blockers.push('Phiên bản chưa có nguồn ACCEPTED.');
  if (!knowledgeVersionInEffect(row.effectiveFrom, row.effectiveTo, today)) {
    blockers.push('Phiên bản chưa tới ngày hoặc đã hết hiệu lực.');
  }
  return blockers;
}

export function pinsMissingPublishedVersion<T extends PharmacyPinRef>(versions: PharmacyVersionRef[], pins: T[]) {
  return pins.filter((pin) =>
    pin.status === 'APPROVED'
    && !versions.some((row) =>
      row.ruleCode === pin.ruleCode
      && row.version === pin.ruleVersion
      && row.lifecycleStatus === 'PUBLISHED'));
}

export function groupPharmacyVersions<T extends PharmacyVersionRef>(
  versions: T[],
  pins: PharmacyPinRef[],
  today: string,
) {
  const groups: Record<PharmacyListGroup, T[]> = {
    inUse: [],
    inactivePin: [],
    notAdopted: [],
    newer: [],
    older: [],
  };
  for (const row of versions) {
    groups[pharmacyListGroup(row, activePin(pins, row.ruleCode), today)].push(row);
  }
  return groups;
}
