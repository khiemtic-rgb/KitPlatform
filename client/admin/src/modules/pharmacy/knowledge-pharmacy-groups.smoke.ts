import {
  canApproveTenantKnowledge,
  compareKnowledgeVersions,
  groupPharmacyVersions,
  pinBlockers,
  pinsMissingPublishedVersion,
  runtimeUsesVersion,
  type PharmacyPinRef,
  type PharmacyVersionRef,
} from './knowledge-pharmacy-groups.ts';
import { knowledgeCatalogActions, publishBlockers } from './knowledge-catalog-actions.ts';

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

const today = '2026-11-01';
const citation = {
  referenceSource: 'synthetic-citation',
  sourceIdentity: 'SYNTH-TEST',
  sourceUrl: 'https://example.test/k2-fixture',
  issuingOrganization: 'synthetic-org',
  evidenceNote: 'fixture section',
};

function version(
  ruleCode: string,
  versionNumber: string,
  sourceVerificationStatus: string,
  effectiveTo: string | null = null,
): PharmacyVersionRef {
  return {
    ruleCode,
    version: versionNumber,
    lifecycleStatus: 'PUBLISHED',
    effectiveFrom: '2020-01-01',
    effectiveTo,
    sourceVerificationStatus,
  };
}

const versions = [
  version('k2syn-used', '1', 'ACCEPTED'),
  version('k2syn-used', '2', 'ACCEPTED'),
  version('k2syn-open', '1', 'ACCEPTED'),
  version('k2syn-stale', '1', 'ACCEPTED', '2020-01-02'),
  version('k2syn-gap', '1', 'UNVERIFIED'),
  version('KR_OTHER', '1', 'ACCEPTED'),
];
const pins: PharmacyPinRef[] = [
  { ruleCode: 'k2syn-used', ruleVersion: '1', status: 'APPROVED' },
  { ruleCode: 'k2syn-stale', ruleVersion: '1', status: 'APPROVED' },
  { ruleCode: 'k2syn-missing', ruleVersion: '1', status: 'APPROVED' },
];

assert(compareKnowledgeVersions('1', '2') < 0, 'version 1 is before 2');
assert(compareKnowledgeVersions('2', '10') < 0, 'version 2 is before 10');

const before = pins.map((pin) => ({ ...pin }));
const groups = groupPharmacyVersions(versions, pins, today);
assert(groups.inUse.map((row) => `${row.ruleCode}:${row.version}`).join() === 'k2syn-used:1', 'only the exact in-effect pin is in use');
assert(groups.newer.map((row) => row.version).join() === '2', 'newer published version stays out of the pin');
assert(groups.notAdopted.map((row) => row.ruleCode).join() === 'k2syn-open,k2syn-gap,KR_OTHER', 'unpublished pins stay unadopted');
assert(groups.inactivePin.map((row) => row.ruleCode).join() === 'k2syn-stale', 'expired pin is not runtime');
assert(pins.every((pin, index) => pin.ruleVersion === before[index]?.ruleVersion), 'grouping does not upgrade a pin');

const used = versions[0];
const newer = versions[1];
assert(used && runtimeUsesVersion(used, pins[0], today), 'runtime uses the pinned version');
assert(newer && !runtimeUsesVersion(newer, pins[0], today), 'runtime does not use the newer version');
assert(versions[5] && !runtimeUsesVersion(versions[5], { ruleCode: 'KR_OTHER', ruleVersion: '1', status: 'APPROVED' }, today), 'KR_OTHER stays out of runtime');

const tenantB: PharmacyPinRef[] = [{ ruleCode: 'k2syn-open', ruleVersion: '1', status: 'APPROVED' }];
const isolated = groupPharmacyVersions(versions, pins, today);
const otherTenant = groupPharmacyVersions(versions, tenantB, today);
assert(isolated.inUse[0]?.ruleCode === 'k2syn-used', 'tenant A pin stays on its rule');
assert(otherTenant.inUse[0]?.ruleCode === 'k2syn-open', 'tenant B pin is a different catalog input');
assert(isolated.inUse[0]?.ruleCode !== otherTenant.inUse[0]?.ruleCode, 'one catalog does not inherit the other pin');

assert(pinBlockers(version('k2syn-gap', '1', 'UNVERIFIED'), today).some((item) => item.includes('ACCEPTED')), 'unverified source cannot be pinned');
assert(pinBlockers(version('k2syn-stale', '1', 'ACCEPTED', '2020-01-02'), today).some((item) => item.includes('hiệu lực')), 'expired version cannot be pinned');
assert(pinBlockers({ ...version('k2syn-open', '1', 'ACCEPTED'), lifecycleStatus: 'IN_REVIEW' }, today).some((item) => item.includes('PUBLISHED')), 'review version cannot be pinned');
assert(pinBlockers(version('KR_OTHER', '1', 'ACCEPTED'), today).some((item) => item.includes('KR_OTHER')), 'KR_OTHER cannot be pinned');
assert(pinBlockers(version('k2syn-open', '1', 'ACCEPTED'), today).length === 0, 'published accepted in-effect version can be pinned');
assert(pinsMissingPublishedVersion(versions, pins).some((pin) => pin.ruleCode === 'k2syn-missing'), 'pin without a published row is visible');

const accepted = { ruleCode: 'k2syn', lifecycleStatus: 'IN_REVIEW', sourceVerificationStatus: 'ACCEPTED', ...citation };
const reviewOnly = { review: true, verifySource: false, publish: false };
const verifyOnly = { review: false, verifySource: true, publish: false };
const publishOnly = { review: false, verifySource: false, publish: true };
const none = { review: false, verifySource: false, publish: false };
assert(knowledgeCatalogActions({ ...accepted, lifecycleStatus: 'DRAFT' }, reviewOnly).editContent, 'review can edit a draft');
assert(!knowledgeCatalogActions(accepted, verifyOnly).editContent, 'verify cannot edit content');
assert(!knowledgeCatalogActions(accepted, publishOnly).editContent, 'publish cannot edit content');
assert(!knowledgeCatalogActions({ ...accepted, lifecycleStatus: 'PUBLISHED' }, reviewOnly).editContent, 'published content is not editable');
assert(!knowledgeCatalogActions({ ...accepted, lifecycleStatus: 'RETIRED' }, reviewOnly).editContent, 'retired content is not editable');
assert(knowledgeCatalogActions(accepted, publishOnly).publish, 'complete accepted review can publish');
assert(!knowledgeCatalogActions({ ...accepted, ruleCode: 'KR_OTHER' }, publishOnly).publish, 'KR_OTHER cannot publish');
assert(!knowledgeCatalogActions({ ...accepted, sourceVerificationStatus: 'UNVERIFIED' }, publishOnly).publish, 'unverified cannot publish');
assert(publishBlockers({ ...accepted, sourceVerificationStatus: 'UNVERIFIED' }).includes('Nguồn chưa ACCEPTED.'), 'missing source is named');
assert(!knowledgeCatalogActions(accepted, none).publish, 'no claim cannot publish');
assert(!knowledgeCatalogActions({ ...accepted, lifecycleStatus: 'DRAFT' }, none).editContent, 'no claim cannot edit');
assert(canApproveTenantKnowledge(['ADMIN'], []), 'tenant admin can pin without a separate grant');
assert(canApproveTenantKnowledge(['STAFF'], ['pharmacy.knowledge.approve']), 'explicit approve claim still works');
assert(!canApproveTenantKnowledge(['STAFF'], []), 'staff cannot pin');
assert(!canApproveTenantKnowledge(['MANAGER'], ['sales.read']), 'manager does not inherit pin');
assert(!canApproveTenantKnowledge(['BRANCH_MANAGER'], []), 'branch manager does not inherit pin');
assert(!canApproveTenantKnowledge([], ['platform.knowledge.publish']), 'platform publish is not a pin grant');

console.log('knowledge pharmacy groups smoke: ok');
