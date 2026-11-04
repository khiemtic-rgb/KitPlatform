/** Vertical từ settings của tenant. Không dùng mã tenant. */
const PHARMACY_VERTICALS = new Set([
  'pharmacy',
  'pharmacy_chain',
  'supplement_store',
  'medical_equipment_store',
  'hybrid',
]);

export function allowsPharmacyKnowledgeVertical(vertical: string | null | undefined): boolean {
  const value = String(vertical ?? '').trim().toLowerCase();
  return PHARMACY_VERTICALS.has(value);
}

export function showPlatformKnowledgeMenu(input: {
  review: boolean;
  verify: boolean;
  publish: boolean;
}): boolean {
  return input.review || input.verify || input.publish;
}

export function showPharmacyPinMenu(canApprove: boolean, vertical: string | null | undefined, loaded: boolean): boolean {
  return loaded && canApprove && allowsPharmacyKnowledgeVertical(vertical);
}

export function showOwnedKnowledgeMenu(isTenantAdmin: boolean, vertical: string | null | undefined, loaded: boolean): boolean {
  return loaded && isTenantAdmin && allowsPharmacyKnowledgeVertical(vertical);
}

export function showStaffKnowledgeMenu(vertical: string | null | undefined, loaded: boolean): boolean {
  return loaded && allowsPharmacyKnowledgeVertical(vertical);
}

export interface KnowledgeMenuItem {
  key: string;
  label: string;
  path: string;
}

export interface KnowledgeMenuGroup {
  key: string;
  label: string;
  children: KnowledgeMenuItem[];
}

/** Một khu vực Tri thức. Mỗi đường dẫn là một nghiệp vụ khác nhau. Trạng thái không thành menu. */
export function knowledgeMenuGroups(input: {
  loaded: boolean;
  vertical: string | null | undefined;
  review: boolean;
  verify: boolean;
  publish: boolean;
  canPin: boolean;
  isTenantAdmin: boolean;
}): KnowledgeMenuGroup[] {
  const platform = showPlatformKnowledgeMenu({
    review: input.review,
    verify: input.verify,
    publish: input.publish,
  });
  const pharmacy = input.loaded && allowsPharmacyKnowledgeVertical(input.vertical);
  const groups: KnowledgeMenuGroup[] = [];
  const store: KnowledgeMenuItem[] = [];
  if (platform) {
    store.push({
      key: 'platform-knowledge',
      label: 'Danh mục nền tảng',
      path: '/platform/pharmacy-knowledge',
    });
  }
  if (pharmacy && input.canPin) {
    store.push({
      key: 'pharmacy-knowledge',
      label: 'Tiếp nhận cho nhà thuốc',
      path: '/pharmacy/knowledge',
    });
  }
  if (store.length > 0) {
    groups.push({ key: 'knowledge-platform-store', label: 'Kho tri thức nền tảng', children: store });
  }
  if (platform) {
    groups.push({
      key: 'knowledge-platform-governance',
      label: 'Kiểm duyệt và xuất bản tri thức nền tảng',
      children: [{
        key: 'platform-knowledge-governance',
        label: 'Xét duyệt và xuất bản',
        path: '/platform/pharmacy-knowledge/xu-ly',
      }],
    });
  }
  if (pharmacy && input.isTenantAdmin) {
    groups.push({
      key: 'knowledge-owned',
      label: 'Tài liệu vận hành nội bộ',
      children: [{
        key: 'pharmacy-owned-knowledge',
        label: 'Quản lý tài liệu',
        path: '/pharmacy/owned-knowledge',
      }],
    });
  }
  if (pharmacy && input.isTenantAdmin) {
    groups.push({
      key: 'knowledge-regimen',
      label: 'Kho đơn tham khảo',
      children: [{
        key: 'pharmacy-local-regimen',
        label: 'Quản lý mẫu đơn',
        path: '/pharmacy/local-regimens',
      }],
    });
  }
  if (pharmacy) {
    groups.push({
      key: 'knowledge-staff',
      label: 'Tra cứu tài liệu vận hành',
      children: [{
        key: 'pharmacy-knowledge-in-use',
        label: 'Tra cứu tài liệu vận hành',
        path: '/pharmacy/knowledge-in-use',
      }],
    });
  }
  return groups;
}

export const ownedKnowledgeSections: Record<string, { heading: string; required: boolean }[]> = {
  GUIDANCE: [
    { heading: 'Mục đích', required: true },
    { heading: 'Đối tượng áp dụng', required: true },
    { heading: 'Nội dung chính', required: true },
    { heading: 'Giới hạn', required: true },
  ],
  OPERATING_PROCEDURE: [
    { heading: 'Mục đích', required: true },
    { heading: 'Phạm vi áp dụng', required: true },
    { heading: 'Các bước thực hiện', required: true },
    { heading: 'Người chịu trách nhiệm', required: true },
    { heading: 'Trường hợp ngoại lệ', required: false },
  ],
  OPERATIONAL_POLICY: [
    { heading: 'Mục đích', required: true },
    { heading: 'Phạm vi áp dụng', required: true },
    { heading: 'Điều kiện áp dụng', required: true },
    { heading: 'Nội dung chính', required: true },
    { heading: 'Trường hợp ngoại lệ', required: false },
  ],
  PRODUCT_KNOWLEDGE: [
    { heading: 'Phạm vi áp dụng', required: true },
    { heading: 'Nội dung chính', required: true },
    { heading: 'Cảnh báo và giới hạn', required: true },
    { heading: 'Tài liệu tham chiếu', required: false },
  ],
  CLINICAL_KNOWLEDGE: [
    { heading: 'Mục đích', required: true },
    { heading: 'Nội dung chính', required: true },
    { heading: 'Cảnh báo và giới hạn', required: true },
  ],
};

export function parseOwnedKnowledgeBody(body: string): Record<string, string> {
  const sections: Record<string, string> = {};
  let heading: string | null = null;
  const buffer: string[] = [];
  const flush = () => {
    if (heading) sections[heading] = buffer.join('\n').trim();
  };
  for (const raw of body.replace(/\r\n/g, '\n').split('\n')) {
    if (raw.startsWith('## ')) {
      flush();
      heading = raw.slice(3).trim();
      buffer.length = 0;
    } else if (heading) {
      buffer.push(raw);
    }
  }
  flush();
  return sections;
}

export function composeOwnedKnowledgeBody(sections: Record<string, string>): string {
  return Object.entries(sections)
    .filter(([, value]) => value.trim())
    .map(([heading, value]) => `## ${heading}\n${value.trim()}`)
    .join('\n\n');
}
