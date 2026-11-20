import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Collapse, Input, Radio, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  confirmConsultation,
  consultationSafetyAlertType,
  extractConsultation,
  fetchConsultationQuestions,
  fetchConsultationSymptomCatalog,
  suggestConsultation,
  type ConsultationExtractResult,
  type ConsultationFacts,
  type ConsultationProductSuggestion,
  type ConsultationSession,
} from '@/shared/api/pharmacy-consultation.api';
import { listLocalRegimensInUse, type LocalRegimenInUse } from '@/shared/api/pharmacy-local-regimen.api';
import { listCustomerKnowledge } from '@/shared/api/pharmacy-customer-knowledge.api';
import { REVIEW_OVERDUE_LABEL } from '@/shared/api/pharmacy-owned-knowledge.api';
import type { CustomerKnowledgeEntryDto } from '@/shared/api/pos-customer-context.types';
import { CustomerKnowledgeCard } from '@/modules/sales/CustomerKnowledgeCard';
import { LocalRegimenPosLookup } from '@/modules/sales/LocalRegimenPosLookup';
import { OwnedKnowledgePosLookup } from '@/modules/sales/OwnedKnowledgePosLookup';
import {
  applyQuestionAnswer,
  durationBucketOptions,
  mergePendingQuestions,
  supplementalOptions,
  yesNoOptions,
  type WizardQuestion,
} from '@/modules/sales/consultation-wizard-questions';
import { consultationSafetyHeadline, consultationSafetySubtext } from '@/modules/sales/consultation-assistant-copy';
import {
  codesForSheet,
  counterAsk,
  counterGroupsFor,
  counterNotices,
  counterSymptom,
  matchCounterSheet,
  productsForCounterGroup,
} from '@/modules/sales/consultation-counter-sheet';

const { Text } = Typography;

const GROUP_ORDER = ['Giảm đau', 'Chống viêm', 'Kháng sinh', 'Hoạt huyết', 'Cảm / mũi', 'Mắt', 'Tiêu hóa', 'Dị ứng', 'Vitamin', 'Bổ não', 'An thần', 'Chống nôn'];

const GROUPS_BY_SYMPTOM: Record<string, string[]> = {
  headache: ['Giảm đau', 'Chống viêm', 'Hoạt huyết'],
  fever: ['Giảm đau', 'Chống viêm', 'Kháng sinh', 'Cảm / mũi'],
  body_ache: ['Giảm đau', 'Chống viêm'],
  chills: ['Giảm đau', 'Cảm / mũi'],
  sore_throat: ['Giảm đau', 'Chống viêm', 'Cảm / mũi', 'Kháng sinh'],
  cough: ['Cảm / mũi', 'Kháng sinh'],
  cough_dry: ['Cảm / mũi', 'Kháng sinh'],
  cough_phlegm: ['Cảm / mũi', 'Kháng sinh'],
  runny_nose: ['Cảm / mũi'],
  nasal_congestion: ['Cảm / mũi'],
  sneezing: ['Cảm / mũi'],
  common_cold: ['Cảm / mũi', 'Giảm đau'],
  flu_symptoms: ['Cảm / mũi', 'Giảm đau'],
  diarrhea: ['Tiêu hóa'],
  abdominal_pain: ['Tiêu hóa'],
  bloating: ['Tiêu hóa'],
  constipation: ['Tiêu hóa'],
  heartburn: ['Tiêu hóa'],
  reflux: ['Tiêu hóa'],
  nausea: ['Chống nôn'],
  vomiting: ['Chống nôn', 'Tiêu hóa'],
  red_eye: ['Mắt'],
  itchy_eye: ['Mắt'],
  watery_eye: ['Mắt'],
  dry_eye: ['Mắt'],
  eye_pain: ['Mắt'],
  eye_discharge: ['Mắt'],
  allergy: ['Dị ứng'],
  itchy_skin: ['Dị ứng'],
  rash: ['Dị ứng'],
  hives: ['Dị ứng'],
  toothache: ['Giảm đau'],
  back_pain: ['Giảm đau', 'Chống viêm'],
  joint_pain: ['Giảm đau', 'Chống viêm'],
  dizziness: ['Hoạt huyết', 'Vitamin'],
  fatigue: ['Vitamin'],
  insomnia: ['An thần'],
};

function suggestionGroupName(item: ConsultationProductSuggestion): string {
  const text = `${item.reason} ${item.productName} ${item.genericName ?? ''}`.toLowerCase();
  if (text.includes('kháng sinh') || text.includes('amoxicillin') || text.includes('augmentin') || text.includes('azithromycin') || text.includes('cefixim') || text.includes('cephalexin')) return 'Kháng sinh';
  if (text.includes('nhỏ mắt') || text.includes('nho mat') || text.includes('eyes drop') || text.includes('dưỡng mắt') || text.includes('duong mat') || text.includes('mắt —')) return 'Mắt';
  if (text.includes('tiêu chảy') || text.includes('táo bón') || text.includes('đầy bụng') || text.includes('ợ nóng') || text.includes('trào ngược') || text.includes('smecta') || text.includes('gastropulgite') || text.includes('diosmectite') || text.includes('oresol') || text.includes('enterogermina') || text.includes('loperamide') || text.includes('omeprazole') || text.includes('gaviscon') || text.includes('phosphalugel') || text.includes('duphalac') || text.includes('bisacodyl') || text.includes('simethicone') || text.includes('cầm tiêu') || text.includes('bù nước') || text.includes('nhuận tràng')) return 'Tiêu hóa';
  if (text.includes('dị ứng') || text.includes('ngứa da') || text.includes('phát ban') || text.includes('mề đay') || text.includes('loratadine') || text.includes('desloratadine') || text.includes('cetirizine') || text.includes('clarityne') || text.includes('zyrtec') || text.includes('kháng histamin')) return 'Dị ứng';
  if (text.includes('hoạt huyết') || text.includes('ginkgo') || text.includes('piracetam') || text.includes('cerebro')) return 'Hoạt huyết';
  if (text.includes('chống viêm') || text.includes('ibuprofen') || text.includes('brufen') || text.includes('diclofenac') || text.includes('meloxicam') || text.includes('chymotrypsin')) return 'Chống viêm';
  if (text.includes('melatonin') || text.includes('mất ngủ')) return 'An thần';
  if (text.includes('bổ não')) return 'Bổ não';
  if (text.includes('vitamin') || text.includes('berocca')) return 'Vitamin';
  if (text.includes('chống nôn') || text.includes('motilium') || text.includes('domperidon')) return 'Chống nôn';
  if (text.includes('decolgen') || text.includes('prospan') || text.includes('acetylcysteine') || text.includes('nghẹt mũi') || text.includes('sổ mũi') || text.includes('ho khan') || text.includes('ho đờm') || text.includes('long đờm')) return 'Cảm / mũi';
  if (text.includes('paracetamol') || text.includes('panadol') || text.includes('efferalgan') || text.includes('tatanol')) return 'Giảm đau';
  if (text.includes('giảm đau') || text.includes('hạ sốt')) return 'Giảm đau';
  if (text.includes('cảm') || text.includes('mũi') || text.includes('hắt') || text.includes('ho -') || text.startsWith('ho ')) return 'Cảm / mũi';
  const head = item.reason.split(' - ')[0]?.trim();
  return head || 'Khác';
}

const GROUP_HINT: Record<string, string> = {
  'Giảm đau': 'Hạ sốt, giảm đau',
  'Chống viêm': 'Viêm, đau nhức',
  'Kháng sinh': 'Thuốc kê đơn — khi khách có đơn, thêm qua ô tìm hàng',
  'Hoạt huyết': 'Tuần hoàn não, chóng mặt',
  'Cảm / mũi': 'Cảm, sổ mũi, nghẹt mũi',
  'Mắt': 'Nhỏ mắt, rửa mắt',
  'Tiêu hóa': 'Cầm tiêu, bù nước, dạ dày',
  'Dị ứng': 'Kháng histamin',
  Vitamin: 'Bổ sung',
  'Bổ não': 'Bổ não',
  'An thần': 'Hỗ trợ ngủ',
  'Chống nôn': 'Buồn nôn',
};

function expectedGroupNames(symptoms: string[]) {
  return [...new Set(symptoms.flatMap((code) => GROUPS_BY_SYMPTOM[code] ?? []))];
}

function groupSuggestions(items: ConsultationProductSuggestion[]) {
  const map = new Map<string, ConsultationProductSuggestion[]>();
  for (const item of items) {
    const name = suggestionGroupName(item);
    const list = map.get(name) ?? [];
    list.push(item);
    map.set(name, list);
  }
  return [...map.entries()]
    .sort((left, right) => {
      const leftIndex = GROUP_ORDER.indexOf(left[0]);
      const rightIndex = GROUP_ORDER.indexOf(right[0]);
      return (leftIndex === -1 ? 99 : leftIndex) - (rightIndex === -1 ? 99 : rightIndex);
    })
    .map(([name, groupItems]) => ({ name, items: groupItems }));
}

function emptyFacts(): ConsultationFacts {
  return { symptoms: [], redFlags: [] };
}

function blocksSale(extract: ConsultationExtractResult | null) {
  return extract?.safetyLevel === 'stop_sale';
}

function adviceText(level: string) {
  if (level === 'stop_sale') return 'Dấu hiệu cần cấp cứu. Dừng bán và hướng khách đi cấp cứu.';
  if (level === 'refer_medical' || level === 'refer_pharmacist') {
    return 'Vẫn lập đơn thuốc cho khách. Nhắc thêm nên đi khám để bác sĩ xem.';
  }
  return null;
}

export function PosSupportSale({
  customerId,
  warehouseId,
  onAddProduct,
  onSaved,
}: {
  customerId?: string;
  warehouseId?: string;
  onAddProduct: (lookupCode: string) => Promise<void>;
  onSaved: (session: ConsultationSession) => void;
}) {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<LocalRegimenInUse[] | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [extract, setExtract] = useState<ConsultationExtractResult | null>(null);
  const [facts, setFacts] = useState<ConsultationFacts>(emptyFacts);
  const [pending, setPending] = useState<WizardQuestion[]>([]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [safetyNote, setSafetyNote] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<ConsultationProductSuggestion[]>([]);
  const [suggestNote, setSuggestNote] = useState<string | null>(null);
  const [addingCode, setAddingCode] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [labels, setLabels] = useState<Record<string, string>>({});
  const [sheetIds, setSheetIds] = useState<string[]>([]);
  const [customerNotes, setCustomerNotes] = useState<{ entries: CustomerKnowledgeEntryDto[]; storeReady: boolean } | null>(null);
  const labelsRef = useRef(labels);
  labelsRef.current = labels;

  const blocked = blocksSale(extract);
  const usableRegimens = (rows ?? []).filter((row) => !row.title.toLowerCase().includes('không dùng để tư vấn'));
  const open = usableRegimens.find((row) => row.id === openId) ?? null;
  const knownSymptoms = facts.symptoms ?? [];
  const suggestionGroups = groupSuggestions(suggestions);
  const sheetGroups = counterGroupsFor(sheetIds);
  const sheetNotices = counterNotices(sheetIds);
  const sheetAsk = counterAsk(sheetIds);
  const expectedGroups = expectedGroupNames(knownSymptoms);
  const boardGroups = (expectedGroups.length > 0 ? expectedGroups : suggestionGroups.map((group) => group.name))
    .sort((left, right) => {
      const leftIndex = GROUP_ORDER.indexOf(left);
      const rightIndex = GROUP_ORDER.indexOf(right);
      return (leftIndex === -1 ? 99 : leftIndex) - (rightIndex === -1 ? 99 : rightIndex);
    })
    .map((name) => ({
      name,
      items: suggestionGroups.find((group) => group.name === name)?.items ?? [],
    }));

  useEffect(() => {
    let cancelled = false;
    fetchConsultationSymptomCatalog()
      .then((catalog) => {
        if (cancelled) return;
        const next: Record<string, string> = {};
        for (const item of catalog.flat) next[item.code] = item.label;
        setLabels(next);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  const loadCustomerNotes = useCallback(async () => {
    if (!customerId) {
      setCustomerNotes(null);
      return;
    }
    try {
      setCustomerNotes(await listCustomerKnowledge(customerId));
    } catch {
      setCustomerNotes(null);
    }
  }, [customerId]);

  useEffect(() => {
    void loadCustomerNotes();
  }, [loadCustomerNotes]);

  async function loadRegimens(text: string, symptomLabels: string[]) {
    const queries = [...new Set([text, ...symptomLabels].map((item) => item.trim()).filter(Boolean))];
    const batches = await Promise.all(queries.map((item) => listLocalRegimensInUse(item).catch(() => null)));
    const merged = new Map<string, LocalRegimenInUse>();
    for (const batch of batches) {
      for (const item of batch?.items ?? []) merged.set(item.id, item);
    }
    const items = [...merged.values()];
    setRows(items);
    setOpenId(items.length === 1 ? items[0].id : null);
  }

  async function loadSafety(text: string, nextFacts?: ConsultationFacts, sheetCodes: string[] = []) {
    const result = await extractConsultation({
      naturalLanguage: text,
      confirmedFacts: nextFacts,
      quickSymptoms: nextFacts?.symptoms ?? (sheetCodes.length > 0 ? sheetCodes : undefined),
    });
    const proposed = result.proposedFacts ?? emptyFacts();
    const symptomPool = [...new Set(nextFacts
      ? (nextFacts.symptoms ?? [])
      : [...(proposed.symptoms ?? []), ...sheetCodes])];
    const merged = { ...(nextFacts ?? proposed), symptoms: symptomPool };
    const symptomLabels = symptomPool
      .map((code) => labelsRef.current[code])
      .filter((label): label is string => Boolean(label));
    await loadRegimens(text, symptomLabels);
    const questions = await fetchConsultationQuestions(symptomPool);
    setExtract(result);
    setFacts(merged);
    setPending(mergePendingQuestions(questions, symptomPool, merged));
    setSafetyNote(null);
    setSuggestNote(null);
    if (!warehouseId) {
      setSuggestions([]);
      return;
    }
    try {
      const suggested = await suggestConsultation({
        confirmedFacts: merged,
        warehouseId,
        limit: 8,
        keepOrderWithReferral: true,
      });
      setSuggestions(suggested.suggestions);
      setSuggestNote(suggested.suggestions.length === 0 ? suggested.blockReason ?? null : null);
    } catch {
      setSuggestions([]);
    }
  }

  async function search() {
    const text = query.trim();
    if (!text) return;
    setLoading(true);
    setAnswers({});
    setExtract(null);
    setPending([]);
    setSuggestions([]);
    setSuggestNote(null);
    try {
      try {
        const ids = matchCounterSheet(text);
        setSheetIds(ids);
        await loadSafety(text, undefined, codesForSheet(ids));
      } catch (error) {
        setExtract(null);
        setPending([]);
        setSuggestions([]);
        setFacts(emptyFacts());
        setSafetyNote(apiErrorMessage(error, 'Chưa kiểm tra được dấu hiệu cảnh báo. Vẫn xem được thuốc nhà thuốc đã gắn.'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tra được đơn mẫu'));
    } finally {
      setLoading(false);
    }
  }

  async function toggleSheet(id: string) {
    const next = sheetIds.includes(id) ? sheetIds.filter((item) => item !== id) : [...sheetIds, id];
    setSheetIds(next);
    const text = query.trim() || counterSymptom(id)?.label || id;
    setLoading(true);
    try {
      await loadSafety(text, { ...facts, symptoms: codesForSheet(next) }, codesForSheet(next));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Chưa cập nhật được triệu chứng'));
    } finally {
      setLoading(false);
    }
  }

  async function refreshAfterAnswers() {
    let next = facts;
    for (const question of pending) {
      const answer = answers[question.code];
      if (answer) next = applyQuestionAnswer(next, question.code, answer);
    }
    setLoading(true);
    try {
      await loadSafety(query.trim(), next);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Chưa cập nhật được phần hỏi thêm'));
    } finally {
      setLoading(false);
    }
  }

  async function addProduct(code: string) {
    setAddingCode(code);
    try {
      await onAddProduct(code);
    } finally {
      setAddingCode(null);
    }
  }

  async function save() {
    if (!extract) return;
    setSaving(true);
    try {
      const session = await confirmConsultation({
        customerId,
        consultationLevel: 1,
        naturalLanguage: query.trim(),
        quickSymptoms: facts.symptoms,
        confirmedFacts: facts,
        extractionSource: extract.extractionSource,
        aiModel: extract.aiModel,
        preliminaryAssessment: extract.preliminaryAssessment,
      });
      onSaved(session);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được tư vấn'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <div className="pos-support-kicker">Triệu chứng</div>
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <Input
          value={query}
          placeholder="Khách đang nói gì? Ví dụ: đau đầu"
          onChange={(event) => setQuery(event.target.value)}
          onPressEnter={() => void search()}
        />
        <Button type="primary" loading={loading} onClick={() => void search()}>Xem đơn</Button>
      </div>

      {safetyNote ? <Alert style={{ marginTop: 8 }} type="warning" showIcon message={safetyNote} /> : null}

      {extract && extract.safetyLevel === 'none' && extract.safetyFlags.length === 0 ? (
        <div className="pos-support-empty" style={{ paddingLeft: 0 }}>Chưa thấy dấu hiệu cần chuyển khám.</div>
      ) : null}

      {extract && (extract.safetyLevel !== 'none' || extract.safetyFlags.length > 0) ? (
        <Alert
          style={{ marginTop: 8 }}
          type={extract.safetyLevel === 'stop_sale' ? 'error' : consultationSafetyAlertType(extract.safetyLevel)}
          showIcon
          message={adviceText(extract.safetyLevel) ?? consultationSafetyHeadline(extract.safetyLevel)}
          description={adviceText(extract.safetyLevel) ? undefined : consultationSafetySubtext(extract.safetyLevel, extract.safetyFlags.length > 0)}
        />
      ) : null}

      {extract?.safetyFlags.map((flag) => (
        <Text key={flag.code} type="danger" style={{ display: 'block', fontSize: 12, marginTop: 4 }}>
          {flag.message}
        </Text>
      ))}

      {suggestNote ? (
        <Alert style={{ marginTop: 8 }} type="info" showIcon message="Không gợi ý thuốc" description={suggestNote} />
      ) : null}

      {sheetNotices.map((notice) => (
        <Alert key={notice.text} style={{ marginTop: 8 }} type={notice.tone === 'danger' ? 'error' : 'warning'} showIcon message={notice.text} />
      ))}

      {extract && sheetIds.length === 0 && knownSymptoms.length === 0 ? (
        <div className="pos-support-empty">Chưa nhận ra triệu chứng trong câu này.</div>
      ) : null}

      {sheetIds.length > 0 ? (
        <div style={{ marginTop: 12 }}>
          <div className="pos-support-kicker">Đang tính vào đơn</div>
          <div className="pos-support-chips" style={{ marginTop: 6 }}>
            {sheetIds.map((id) => (
              <Button key={id} size="small" type="primary" onClick={() => void toggleSheet(id)}>
                {counterSymptom(id)?.label ?? id}
              </Button>
            ))}
          </div>
          {sheetAsk.length > 0 ? (
            <>
              <div className="pos-support-kicker" style={{ marginTop: 10 }}>Hỏi thêm</div>
              <div className="pos-support-chips" style={{ marginTop: 6 }}>
                {sheetAsk.map((id) => (
                  <Button key={id} size="small" disabled={loading} onClick={() => void toggleSheet(id)}>
                    {counterSymptom(id)?.label ?? id}
                  </Button>
                ))}
              </div>
            </>
          ) : null}
        </div>
      ) : null}

      {extract ? (
        <div style={{ marginTop: 14 }}>
          <div className="pos-support-kicker">Đơn mẫu nhà thuốc</div>
          {usableRegimens.length === 0 ? (
            <div className="pos-support-empty">Chưa có đơn mẫu nhà thuốc cho triệu chứng này.</div>
          ) : usableRegimens.map((row) => (
            <div key={row.id}>
              <Button type="link" style={{ paddingLeft: 0 }} onClick={() => setOpenId(openId === row.id ? null : row.id)}>
                {row.title}
              </Button>
              <Text type="secondary">{row.conditionGroup}</Text>
              {row.reviewOverdue ? <Tag color="orange" style={{ marginLeft: 6 }}>{REVIEW_OVERDUE_LABEL}</Tag> : null}
            </div>
          ))}
        </div>
      ) : null}

      {open ? (
        <div style={{ marginTop: 8 }}>
          <Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {open.audience} · {open.limits}
          </Text>
          {open.items.map((item) => {
            const code = item.catalogProductCode?.trim();
            const canAdd = Boolean(item.validChoice && code && !blocked && warehouseId);
            return (
              <div key={item.id} className="pos-support-line">
                <div>
                  <div className="pos-support-line-name">{item.catalogProductName || item.productLabel || item.ingredient}</div>
                  {!item.validChoice ? <div className="pos-support-line-code">Chưa gắn sản phẩm để bán</div> : null}
                </div>
                {item.validChoice ? (
                  <Button size="small" type="primary" disabled={!canAdd} loading={addingCode === code} onClick={() => { if (code) void addProduct(code); }}>
                    Thêm
                  </Button>
                ) : null}
              </div>
            );
          })}
        </div>
      ) : null}

      {extract && sheetGroups.length > 0 ? (
        <div style={{ marginTop: 14 }}>
          <div className="pos-support-kicker">Đơn theo nhóm</div>
          <div className="pos-support-board">
            {sheetGroups.map(({ group, labels: forLabels }) => {
              const products = productsForCounterGroup(group, suggestions);
              const badge = products.length > 0 ? null : group.status === 'het' ? 'Chưa có hàng' : group.status === 'nhap' ? 'Cần nhập' : null;
              return (
                <section key={group.id} className={group.status === 'het' && products.length === 0 ? 'pos-support-group pos-support-group--rx' : 'pos-support-group'}>
                  <div className="pos-support-group-head">
                    <div>
                      <div className="pos-support-group-title">{group.name}</div>
                      <div className="pos-support-group-hint">Cho: {forLabels.join(', ')}</div>
                    </div>
                    {badge ? <span className={group.status === 'het' ? 'pos-support-badge pos-support-badge--out' : 'pos-support-badge pos-support-badge--add'}>{badge}</span> : null}
                  </div>
                  {products.length > 0 ? products.map((item) => (
                    <div key={item.productId} className="pos-support-line">
                      <div>
                        <div className="pos-support-line-name">{item.productName}</div>
                        <div className="pos-support-line-code">{item.productCode}</div>
                      </div>
                      <Button
                        size="small"
                        type="primary"
                        disabled={!item.lookupCode || blocked || !warehouseId}
                        loading={addingCode === item.lookupCode}
                        onClick={() => void addProduct(item.lookupCode)}
                      >
                        Thêm
                      </Button>
                    </div>
                  )) : (
                    <div className="pos-support-empty">
                      {group.examples.length > 0 ? `Ví dụ: ${group.examples.join(', ')}` : badge === 'Chưa có hàng' ? 'Kho chưa có hàng trong nhóm này.' : 'Chưa có hàng trong nhóm này.'}
                      {group.note ? ` ${group.note}` : ''}
                    </div>
                  )}
                  {products.length > 0 && group.note ? <div className="pos-support-empty">{group.note}</div> : null}
                </section>
              );
            })}
          </div>
        </div>
      ) : extract ? (
        <div style={{ marginTop: 14 }}>
          <div className="pos-support-kicker">Đơn theo nhóm</div>
          {rows && boardGroups.length === 0 ? (
            <div className="pos-support-empty">Chưa có thuốc phù hợp cho triệu chứng này.</div>
          ) : (
            <div className="pos-support-board">
              {boardGroups.map((group) => (
                <section key={group.name} className={group.name === 'Kháng sinh' ? 'pos-support-group pos-support-group--rx' : 'pos-support-group'}>
                  <div className="pos-support-group-head">
                    <div>
                      <div className="pos-support-group-title">{group.name}</div>
                      <div className="pos-support-group-hint">{GROUP_HINT[group.name]}</div>
                    </div>
                    <div className="pos-support-group-hint">{group.items.length}</div>
                  </div>
                  {group.items.length === 0 ? (
                    <div className="pos-support-empty">Chưa có hàng trong nhóm này.</div>
                  ) : group.items.map((item) => (
                    <div key={item.productId} className="pos-support-line">
                      <div>
                        <div className="pos-support-line-name">{item.productName}</div>
                        <div className="pos-support-line-code">{item.productCode}</div>
                      </div>
                      {group.name === 'Kháng sinh' ? (
                        <div className="pos-support-group-hint">Cần đơn bác sĩ</div>
                      ) : (
                        <Button
                          size="small"
                          type="primary"
                          disabled={!item.lookupCode || blocked || !warehouseId}
                          loading={addingCode === item.lookupCode}
                          onClick={() => void addProduct(item.lookupCode)}
                        >
                          Thêm
                        </Button>
                      )}
                    </div>
                  ))}
                </section>
              ))}
            </div>
          )}
        </div>
      ) : null}

      {pending.length > 0 ? (
        <Collapse
          ghost
          style={{ marginTop: 8 }}
          items={[{
            key: 'questions',
            label: 'Hỏi thêm chi tiết nếu cần',
            children: (
              <div>
                {pending.map((question) => {
                  const supplemental = supplementalOptions(question.code);
                  const options = supplemental ?? (question.answerType === 'boolean' ? yesNoOptions() : null);
                  const duration = question.answerType === 'duration_days' ? durationBucketOptions() : null;
                  return (
                    <div key={question.code} style={{ marginTop: 8 }}>
                      <Text style={{ display: 'block', marginBottom: 4 }}>{question.questionVi}</Text>
                      {options || duration ? (
                        <Radio.Group
                          size="small"
                          optionType="button"
                          value={answers[question.code]}
                          onChange={(event) => setAnswers((current) => ({ ...current, [question.code]: event.target.value }))}
                          options={(options ?? duration ?? []).map((option) => ({ value: option.value, label: option.label }))}
                        />
                      ) : (
                        <Input
                          size="small"
                          value={answers[question.code] ?? ''}
                          onChange={(event) => setAnswers((current) => ({ ...current, [question.code]: event.target.value }))}
                        />
                      )}
                    </div>
                  );
                })}
                <Button style={{ marginTop: 8 }} onClick={() => void refreshAfterAnswers()}>Cập nhật</Button>
              </div>
            ),
          }]}
        />
      ) : null}

      <div style={{ marginTop: 12 }}>
        <Button disabled={!customerId || !extract} loading={saving} onClick={() => void save()}>
          Lưu tư vấn
        </Button>
        {!customerId ? <Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>Chọn khách để lưu lần tư vấn này.</Text> : null}
      </div>

      {customerId && customerNotes ? (
        <div style={{ marginTop: 14 }}>
          <CustomerKnowledgeCard
            customerId={customerId}
            entries={customerNotes.entries}
            storeReady={customerNotes.storeReady}
            onChanged={() => void loadCustomerNotes()}
          />
        </div>
      ) : null}

      <Collapse
        ghost
        style={{ marginTop: 8 }}
        items={[
          { key: 'owned', label: 'Tra tài liệu nhà thuốc', children: <OwnedKnowledgePosLookup initialQuery={query} /> },
          { key: 'regimen', label: 'Tra kho đơn tham khảo', children: <LocalRegimenPosLookup /> },
        ]}
      />
    </div>
  );
}
