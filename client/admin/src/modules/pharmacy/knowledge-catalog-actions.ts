export interface KnowledgeCatalogAccess {
  review: boolean;
  verifySource: boolean;
  publish: boolean;
}

export interface KnowledgeCatalogRow {
  ruleCode: string;
  lifecycleStatus: string;
  sourceVerificationStatus?: string | null;
}

export interface SourceVerificationInput {
  referenceSource?: string | null;
  sourceIdentity?: string | null;
  sourceUrl?: string | null;
  documentRef?: string | null;
  issuingOrganization?: string | null;
  evidenceNote?: string | null;
}

export function canOpenKnowledgeCatalog(access: KnowledgeCatalogAccess) {
  return access.review || access.verifySource || access.publish;
}

export function publishBlockers(row: KnowledgeCatalogRow & Partial<SourceVerificationInput>): string[] {
  const blockers: string[] = [];
  if (row.lifecycleStatus !== 'IN_REVIEW') blockers.push('Chỉ xuất bản từ IN_REVIEW.');
  if (row.ruleCode === 'KR_OTHER') blockers.push('KR_OTHER không được xuất bản.');
  if (row.sourceVerificationStatus !== 'ACCEPTED') blockers.push('Nguồn chưa ACCEPTED.');
  else {
    const missing = sourceAcceptanceError(row);
    if (missing) blockers.push(missing);
  }
  return blockers;
}

export function knowledgeCatalogActions(row: KnowledgeCatalogRow & Partial<SourceVerificationInput>, access: KnowledgeCatalogAccess) {
  const lifecycle = row.lifecycleStatus;
  const editable = lifecycle === 'DRAFT' || lifecycle === 'IN_REVIEW';
  return {
    submitReview: access.review && lifecycle === 'DRAFT',
    returnToDraft: access.review && lifecycle === 'IN_REVIEW',
    editContent: access.review && editable,
    verifySource: access.verifySource && editable,
    publish: access.publish && publishBlockers(row).length === 0,
    retire: access.publish && lifecycle === 'PUBLISHED',
  };
}

export function splitKnowledgeList(value: string): string[] {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}

export function sameKnowledgeList(left: string[], right: string[]) {
  return left.length === right.length && left.every((item, index) => item === right[index]);
}

/** Client check before ACCEPT. Does not invent a citation and does not treat the catalog label as a source. */
export function sourceAcceptanceError(input: SourceVerificationInput): string | null {
  const citation = input.referenceSource?.trim() ?? '';
  const identity = input.sourceIdentity?.trim() ?? '';
  const url = input.sourceUrl?.trim() ?? '';
  const documentRef = input.documentRef?.trim() ?? '';
  const organization = input.issuingOrganization?.trim() ?? '';
  const evidence = input.evidenceNote?.trim() ?? '';
  if (!citation) return 'Thiếu câu trích dẫn.';
  if (!identity) return 'Thiếu định danh nguồn.';
  if (citation.toLowerCase() === 'novixa' || identity.toLowerCase() === 'novixa')
    return 'Nhãn novixa không phải nguồn chuyên môn.';
  if (!url && !documentRef) return 'Cần URL hoặc mã hồ sơ của nguồn.';
  if (!organization) return 'Thiếu cơ quan ban hành.';
  if (!evidence) return 'Thiếu vị trí bằng chứng.';
  return null;
}
