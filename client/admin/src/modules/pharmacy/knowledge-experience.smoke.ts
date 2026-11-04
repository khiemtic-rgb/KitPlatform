import {
  allowsPharmacyKnowledgeVertical,
  showOwnedKnowledgeMenu,
  knowledgeMenuGroups,
  showStaffKnowledgeMenu,
  showPharmacyPinMenu,
  showPlatformKnowledgeMenu,
} from './knowledge-experience.ts';

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

assert(allowsPharmacyKnowledgeVertical('pharmacy'), 'pharmacy vertical can manage pharmacy knowledge');
assert(allowsPharmacyKnowledgeVertical('pharmacy_chain'), 'chain vertical stays in pharmacy knowledge');
assert(!allowsPharmacyKnowledgeVertical('marketing'), 'marketing vertical does not see pharmacy knowledge');
assert(!allowsPharmacyKnowledgeVertical('family'), 'family vertical does not see pharmacy knowledge');
assert(!allowsPharmacyKnowledgeVertical('clinic'), 'clinic vertical does not see pharmacy knowledge');
assert(!allowsPharmacyKnowledgeVertical(''), 'unknown vertical fails closed');
assert(!showOwnedKnowledgeMenu(true, 'marketing', true), 'tenant admin of a marketing tenant has no pharmacy knowledge menu');
assert(!showPharmacyPinMenu(true, 'marketing', true), 'tenant admin pin menu is not a platform menu');
assert(showOwnedKnowledgeMenu(true, 'pharmacy', true), 'pharmacy tenant admin sees owned knowledge');
assert(showStaffKnowledgeMenu('pharmacy', true), 'pharmacy staff can open the in-use catalog');
assert(!showStaffKnowledgeMenu('marketing', true), 'marketing staff do not get the pharmacy catalog');
assert(!showStaffKnowledgeMenu('pharmacy', false), 'staff catalog stays hidden until the vertical is known');
assert(!showOwnedKnowledgeMenu(true, 'pharmacy', false), 'menu stays hidden until the tenant vertical is known');
assert(!showPlatformKnowledgeMenu({ review: false, verify: false, publish: false }), 'tenant admin does not gain platform knowledge');
assert(showPlatformKnowledgeMenu({ review: false, verify: false, publish: true }), 'explicit publish permission opens platform knowledge');
assert(!showPlatformKnowledgeMenu({ review: false, verify: false, publish: false }), 'MKT and pharmacy admins are equal without a platform grant');

const labels = (groups: { label: string; children: { label: string }[] }[]) =>
  groups.flatMap((group) => [group.label, ...group.children.map((item) => item.label)]);

const marketingAdmin = knowledgeMenuGroups({
  loaded: true,
  vertical: 'marketing',
  review: false,
  verify: false,
  publish: false,
  canPin: true,
  isTenantAdmin: true,
});
assert(marketingAdmin.length === 0, 'marketing admin has no knowledge area');

const platformOnMarketing = knowledgeMenuGroups({
  loaded: true,
  vertical: 'marketing',
  review: false,
  verify: false,
  publish: true,
  canPin: true,
  isTenantAdmin: true,
});
assert(labels(platformOnMarketing).includes('Kho tri thức nền tảng'), 'explicit publish opens the platform store');
assert(labels(platformOnMarketing).includes('Kiểm duyệt và xuất bản tri thức nền tảng'), 'explicit publish opens governance');
assert(!labels(platformOnMarketing).includes('Tiếp nhận cho nhà thuốc'), 'marketing does not adopt pharmacy knowledge');
assert(!labels(platformOnMarketing).includes('Tài liệu vận hành nội bộ'), 'marketing does not manage pharmacy operating documents');
assert(!labels(platformOnMarketing).includes('Tra cứu tài liệu vận hành'), 'marketing has no staff operating-document lookup');

const pharmacyAdmin = knowledgeMenuGroups({
  loaded: true,
  vertical: 'pharmacy',
  review: false,
  verify: false,
  publish: false,
  canPin: true,
  isTenantAdmin: true,
});
assert(!labels(pharmacyAdmin).includes('Kiểm duyệt và xuất bản tri thức nền tảng'), 'tenant admin does not govern platform knowledge');
assert(labels(pharmacyAdmin).includes('Tiếp nhận cho nhà thuốc'), 'pharmacy admin can adopt published knowledge');
assert(labels(pharmacyAdmin).includes('Kho đơn tham khảo'), 'pharmacy admin manages local regimens');
assert(labels(pharmacyAdmin).includes('Tài liệu vận hành nội bộ'), 'pharmacy admin manages operating documents');
assert(labels(pharmacyAdmin).includes('Tra cứu tài liệu vận hành'), 'pharmacy admin can look up operating documents');
assert(!labels(pharmacyAdmin).includes('Hồ sơ khách hàng'), 'customer records stay outside this document menu');

const staff = knowledgeMenuGroups({
  loaded: true,
  vertical: 'pharmacy',
  review: false,
  verify: false,
  publish: false,
  canPin: false,
  isTenantAdmin: false,
});
assert(!labels(staff).includes('Kho đơn tham khảo'), 'staff does not manage regimens');
assert(staff.length === 1 && staff[0].children.length === 1, 'staff lookup is one route');
assert(staff[0].label === 'Tra cứu tài liệu vận hành' && staff[0].children[0].label === staff[0].label, 'staff lookup has one name');

const hiddenUntilLoaded = knowledgeMenuGroups({
  loaded: false,
  vertical: 'pharmacy',
  review: false,
  verify: false,
  publish: false,
  canPin: true,
  isTenantAdmin: true,
});
assert(hiddenUntilLoaded.length === 0, 'pharmacy menus stay hidden until vertical is known');

console.log('knowledge-experience smoke ok');
