import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Drawer,
  Input,
  Modal,
  Popover,
  Radio,
  Select,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  EditOutlined,
  HistoryOutlined,
  InfoCircleOutlined,
  MedicineBoxOutlined,
  MinusOutlined,
  PlusOutlined,
  QuestionCircleOutlined,
  RobotOutlined,
  ShoppingCartOutlined,
  ThunderboltOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchCustomer } from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { searchCustomers } from '@/shared/api/sales.api';
import type { CustomerListItem } from '@/shared/api/sales.types';
import {
  confirmConsultation,
  extractConsultation,
  fetchConsultationQuestions,
  fetchConsultationSymptomCatalog,
  fetchRecentConsultationSessions,
  suggestConsultation,
  type ConsultationExtractResult,
  type ConsultationFacts,
  type ConsultationProductSuggestion,
  type ConsultationSession,
  type ConsultationSessionSummary,
  type ConsultationSuggestResult,
  type ConsultationSymptomGroup,
} from '@/shared/api/pharmacy-consultation.api';
import {
  ASSISTANT_DISCLAIMER,
  consultationSafetyHeadline,
  consultationSafetySubtext,
  formatMissingInfoHints,
  hypothesisFitLabel,
  PRELIMINARY_ASSESSMENT_TITLE,
  preliminaryAssessmentBadge,
  productSupportReason,
  WARNING_SIGNS_TITLE,
} from '@/modules/sales/consultation-assistant-copy';
import {
  applyQuestionAnswer,
  durationBucketOptions,
  mergePendingQuestions,
  supplementalOptions,
  yesNoOptions,
  type WizardQuestion,
} from '@/modules/sales/consultation-wizard-questions';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import {
  applyCustomerProfileToFacts,
  buildCustomerProfileSnapshot,
  customerGenderLabelVi,
  getCustomerProfileGaps,
  type ConsultationCustomerProfileSnapshot,
} from '@/modules/sales/consultation-customer-profile';
import { QUICK_SYMPTOM_CODES, QUICK_SYMPTOM_ICONS } from '@/modules/sales/consultation-quick-symptoms';
import {
  suggestRelatedSymptomCodes,
  SYMPTOM_CATEGORY_ICONS,
  SYMPTOM_OVERVIEW_CATEGORY_CODES,
} from '@/modules/sales/consultation-symptom-picker';
import {
  formatDaysAgo,
  symptomLabelsFromCodes,
} from '@/modules/sales/consultation-session-summary';
import {
  formatPosCustomerOptionLabel,
  upsertPosCustomers,
} from '@/modules/sales/pos-customer-option';
import './pos-consultation-studio.css';
import './pos-consultation-symptom-picker.css';

type Props = {
  open: boolean;
  customerId?: string;
  warehouseId?: string;
  onAddToCart?: (lookupCode: string) => Promise<void>;
  onClose: () => void;
  onConfirmed: (session: ConsultationSession) => void;
  /** Đồng bộ khách đã chọn về POS */
  onCustomerChange?: (customerId: string | undefined, option?: CustomerListItem) => void;
  /** Mở form tạo khách nhanh trên POS; truyền query tìm kiếm cuối để prefill */
  onQuickAddCustomer?: (searchQuery?: string) => void;
  /** Cho phép bổ sung hồ sơ (NS, giới tính, địa chỉ, dị ứng) — cần sales.write */
  canPatchCustomer?: boolean;
  /** Đồng bộ hồ sơ sau cập nhật về danh sách POS */
  onCustomerUpdated?: (detail: CustomerDetail) => void;
};

const NL_MAX = 300;

function formatDobVi(iso?: string | null): string | null {
  if (!iso?.trim()) return null;
  const d = new Date(iso.trim());
  if (Number.isNaN(d.getTime())) return null;
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}/${d.getFullYear()}`;
}

function SectionHead({ n, title }: { n: number; title: string }) {
  return (
    <div className="pc-section-head">
      <span className="pc-section-num">{n}</span>
      <span>{title}</span>
    </div>
  );
}

export function PosConsultationModal({
  open,
  customerId,
  warehouseId,
  onAddToCart,
  onClose,
  onConfirmed,
  onCustomerChange,
  onQuickAddCustomer,
  canPatchCustomer,
  onCustomerUpdated,
}: Props) {
  const { message, modal } = App.useApp();
  const [groups, setGroups] = useState<ConsultationSymptomGroup[]>([]);
  const [aliasesByCode, setAliasesByCode] = useState<Record<string, string[]>>({});
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [activeGroupCode, setActiveGroupCode] = useState<string>();
  const [selectedSymptoms, setSelectedSymptoms] = useState<string[]>([]);
  const [naturalLanguage, setNaturalLanguage] = useState('');
  const [extracting, setExtracting] = useState(false);
  const [loadingSupport, setLoadingSupport] = useState(false);
  const [saving, setSaving] = useState(false);
  const [extractResult, setExtractResult] = useState<ConsultationExtractResult | null>(null);
  const [suggestResult, setSuggestResult] = useState<ConsultationSuggestResult | null>(null);
  const [pendingQuestions, setPendingQuestions] = useState<WizardQuestion[]>([]);
  const [questionAnswers, setQuestionAnswers] = useState<Record<string, string>>({});
  const [pickedProducts, setPickedProducts] = useState<Set<string>>(new Set());
  const [addingProductCode, setAddingProductCode] = useState<string | null>(null);
  const [extractError, setExtractError] = useState<string | null>(null);
  const [symptomSearch, setSymptomSearch] = useState('');
  const [facts, setFacts] = useState<ConsultationFacts>({ symptoms: [], redFlags: [] });
  const [customerProfile, setCustomerProfile] = useState<ConsultationCustomerProfileSnapshot | null>(null);
  const [customerDetail, setCustomerDetail] = useState<CustomerDetail | null>(null);
  const [customerProfileLoading, setCustomerProfileLoading] = useState(false);
  const [symptomDrawerOpen, setSymptomDrawerOpen] = useState(false);
  const [recentSessions, setRecentSessions] = useState<ConsultationSessionSummary[]>([]);
  const [recentLoading, setRecentLoading] = useState(false);
  const [previousDetail, setPreviousDetail] = useState<ConsultationSessionSummary | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [customerOptions, setCustomerOptions] = useState<CustomerListItem[]>([]);
  const [customerSearchLoading, setCustomerSearchLoading] = useState(false);
  const [draftSymptoms, setDraftSymptoms] = useState<string[]>([]);
  const [showAllPopular, setShowAllPopular] = useState(false);
  const [symptomGuideOpen, setSymptomGuideOpen] = useState(false);
  const [profilePatchOpen, setProfilePatchOpen] = useState(false);
  const customerSearchSeq = useRef(0);
  const customerSearchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const lastCustomerSearchRef = useRef('');
  const customerIdRef = useRef<string | undefined>(customerId);

  useEffect(() => {
    customerIdRef.current = customerId;
  }, [customerId]);

  const runCustomerSearch = useCallback(async (query: string) => {
    const seq = ++customerSearchSeq.current;
    setCustomerSearchLoading(true);
    try {
      const hits = await searchCustomers(query.trim() || undefined);
      if (seq !== customerSearchSeq.current) return;
      setCustomerOptions((prev) => {
        const selectedId = customerIdRef.current;
        const selected = selectedId ? prev.find((c) => c.id === selectedId) : undefined;
        return upsertPosCustomers(hits, selected);
      });
    } catch {
      if (seq === customerSearchSeq.current) setCustomerOptions((prev) => prev);
    } finally {
      if (seq === customerSearchSeq.current) setCustomerSearchLoading(false);
    }
  }, []);

  const handleCustomerSearch = useCallback(
    (query: string) => {
      if (query.trim()) lastCustomerSearchRef.current = query.trim();
      window.clearTimeout(customerSearchTimer.current);
      customerSearchTimer.current = window.setTimeout(() => {
        void runCustomerSearch(query);
      }, 250);
    },
    [runCustomerSearch],
  );

  useEffect(() => {
    return () => window.clearTimeout(customerSearchTimer.current);
  }, []);

  useEffect(() => {
    if (!open) return;
    void runCustomerSearch('');
  }, [open, runCustomerSearch]);

  useEffect(() => {
    if (!customerDetail) return;
    setCustomerOptions((prev) =>
      upsertPosCustomers(prev, {
        id: customerDetail.id,
        customerCode: customerDetail.customerCode,
        fullName: customerDetail.fullName,
        phone: customerDetail.phone,
        allowCredit: customerDetail.allowCredit,
        creditLimit: customerDetail.creditLimit ?? undefined,
      }),
    );
  }, [customerDetail]);

  const selectCustomer = (nextId: string | undefined) => {
    const option = nextId ? customerOptions.find((c) => c.id === nextId) : undefined;
    onCustomerChange?.(nextId, option);
  };

  const openQuickAdd = () => {
    onQuickAddCustomer?.(lastCustomerSearchRef.current || undefined);
  };

  const handleProfilePatched = (detail: CustomerDetail) => {
    setCustomerDetail(detail);
    setCustomerProfile(buildCustomerProfileSnapshot(detail));
    onCustomerUpdated?.(detail);
    if (extractResult) {
      message.info('Đã cập nhật hồ sơ. Cân nhắc Phân tích lại nếu tuổi/giới tính thay đổi.');
    }
  };

  const profileGaps = customerDetail ? getCustomerProfileGaps(customerDetail) : [];

  const enrichFactsWithProfile = (base: ConsultationFacts): ConsultationFacts =>
    customerProfile ? applyCustomerProfileToFacts(base, customerProfile) : base;

  const resetSession = () => {
    setActiveGroupCode(undefined);
    setSelectedSymptoms([]);
    setNaturalLanguage('');
    setExtractResult(null);
    setSuggestResult(null);
    setPendingQuestions([]);
    setQuestionAnswers({});
    setPickedProducts(new Set());
    setExtractError(null);
    setSymptomSearch('');
    setFacts({ symptoms: [], redFlags: [] });
    setExtracting(false);
    setLoadingSupport(false);
    setSaving(false);
    setCustomerProfile(null);
    setCustomerDetail(null);
    setCustomerProfileLoading(false);
    setSymptomDrawerOpen(false);
    setRecentSessions([]);
    setRecentLoading(false);
    setPreviousDetail(null);
    setHistoryOpen(false);
    setDraftSymptoms([]);
    setShowAllPopular(false);
    setSymptomGuideOpen(false);
    setProfilePatchOpen(false);
  };

  useEffect(() => {
    if (!open || !customerId) {
      setCustomerProfile(null);
      setCustomerDetail(null);
      setCustomerProfileLoading(false);
      return;
    }
    let cancelled = false;
    setCustomerProfileLoading(true);
    void fetchCustomer(customerId)
      .then((detail) => {
        if (cancelled) return;
        setCustomerDetail(detail);
        setCustomerProfile(buildCustomerProfileSnapshot(detail));
      })
      .catch(() => {
        if (!cancelled) {
          setCustomerProfile(null);
          setCustomerDetail(null);
        }
      })
      .finally(() => {
        if (!cancelled) setCustomerProfileLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, customerId]);

  useEffect(() => {
    if (!customerProfile) return;
    setFacts((prev) => applyCustomerProfileToFacts(prev, customerProfile));
  }, [customerProfile]);

  useEffect(() => {
    if (!open || !customerId) {
      setRecentSessions([]);
      setRecentLoading(false);
      return;
    }
    let cancelled = false;
    setRecentLoading(true);
    void fetchRecentConsultationSessions(customerId, 8)
      .then((items) => {
        if (!cancelled) setRecentSessions(items);
      })
      .catch(() => {
        if (!cancelled) setRecentSessions([]);
      })
      .finally(() => {
        if (!cancelled) setRecentLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, customerId]);

  useEffect(() => {
    if (!open) return;
    setCatalogLoading(true);
    setCatalogError(null);
    void fetchConsultationSymptomCatalog()
      .then((catalog) => {
        setGroups(catalog.groups);
        setAliasesByCode(catalog.aliasesByCode ?? {});
        if (catalog.groups.length > 0) setActiveGroupCode(catalog.groups[0].code);
        else setCatalogError('Không tải được danh mục triệu chứng.');
      })
      .catch((err) => {
        setGroups([]);
        setCatalogError(apiErrorMessage(err, 'Không tải được danh mục triệu chứng.'));
      })
      .finally(() => setCatalogLoading(false));
  }, [open]);

  useEffect(() => {
    if (!open) resetSession();
  }, [open]);

  const labelByCode = useMemo(() => {
    const map = new Map<string, string>();
    for (const g of groups) {
      for (const item of g.items) map.set(item.code, item.label);
    }
    return map;
  }, [groups]);

  const normalizedSearch = symptomSearch.trim().toLowerCase();
  const symptomMatchesSearch = (code: string, label: string, search: string) => {
    if (label.toLowerCase().includes(search) || code.toLowerCase().includes(search)) return true;
    return (aliasesByCode[code] ?? []).some((alias) => alias.toLowerCase().includes(search));
  };

  const activeGroup = useMemo(() => {
    return groups.find((g) => g.code === activeGroupCode) ?? groups[0];
  }, [activeGroupCode, groups]);

  useEffect(() => {
    if (groups.length === 0) return;
    if (!groups.some((g) => g.code === activeGroupCode)) {
      setActiveGroupCode(groups[0]?.code);
    }
  }, [activeGroupCode, groups]);

  const overviewGroups = useMemo(() => {
    const ordered: typeof groups = [];
    for (const code of SYMPTOM_OVERVIEW_CATEGORY_CODES) {
      const g = groups.find((x) => x.code === code);
      if (g) ordered.push(g);
    }
    for (const g of groups) {
      if (!ordered.some((x) => x.code === g.code)) ordered.push(g);
    }
    return ordered;
  }, [groups]);

  const popularInActiveGroup = useMemo(() => {
    if (!activeGroup) return [];
    const quickSet = new Set<string>(QUICK_SYMPTOM_CODES);
    const popular = activeGroup.items.filter((i) =>
      quickSet.has(i.code as (typeof QUICK_SYMPTOM_CODES)[number]),
    );
    const rest = activeGroup.items.filter(
      (i) => !quickSet.has(i.code as (typeof QUICK_SYMPTOM_CODES)[number]),
    );
    if (popular.length < 4) {
      const fill = rest.slice(0, Math.max(0, 6 - popular.length));
      return [...popular, ...fill];
    }
    return popular;
  }, [activeGroup]);

  const otherInActiveGroup = useMemo(() => {
    if (!activeGroup) return [];
    const popularCodes = new Set(popularInActiveGroup.map((i) => i.code));
    return activeGroup.items.filter((i) => !popularCodes.has(i.code));
  }, [activeGroup, popularInActiveGroup]);

  const visiblePopular = showAllPopular ? popularInActiveGroup : popularInActiveGroup.slice(0, 8);

  const draftAiSuggestions = useMemo(() => {
    const codes = suggestRelatedSymptomCodes(draftSymptoms, 4);
    return codes
      .map((code) => ({ code, label: labelByCode.get(code) ?? code }))
      .filter((x) => x.label);
  }, [draftSymptoms, labelByCode]);

  const searchHits = useMemo(() => {
    if (!normalizedSearch) return [];
    return groups.flatMap((g) =>
      g.items
        .filter((item) => symptomMatchesSearch(item.code, item.label, normalizedSearch))
        .map((item) => ({ ...item, groupLabel: g.label })),
    );
  }, [groups, normalizedSearch, aliasesByCode]);

  const canAnalyze = selectedSymptoms.length > 0 || naturalLanguage.trim().length >= 3;
  const safetyLevel = extractResult?.safetyLevel ?? 'none';
  const safetyFlags = extractResult?.safetyFlags ?? [];
  const preliminaryAssessment = extractResult?.preliminaryAssessment ?? null;
  const blocksProductSupport =
    safetyLevel === 'stop_sale'
    || safetyLevel === 'refer_medical'
    || preliminaryAssessment?.level === 'needs_evaluation';

  const quickSymptomOptions = useMemo(
    () =>
      QUICK_SYMPTOM_CODES.map((code) => ({
        code,
        label: labelByCode.get(code) ?? code.replace(/_/g, ' '),
        icon: QUICK_SYMPTOM_ICONS[code],
      })).filter((o) => o.label),
    [labelByCode],
  );

  const displaySymptoms = useMemo(() => {
    const pool = [...new Set([...selectedSymptoms, ...facts.symptoms])];
    return pool.map((c) => labelByCode.get(c) ?? c);
  }, [selectedSymptoms, facts.symptoms, labelByCode]);

  const invalidateDownstream = () => {
    setExtractResult(null);
    setSuggestResult(null);
    setExtractError(null);
    setPendingQuestions([]);
    setQuestionAnswers({});
    setPickedProducts(new Set());
  };

  const setSymptomChecked = (code: string, checked: boolean) => {
    setSelectedSymptoms((prev) => {
      if (checked) return prev.includes(code) ? prev : [...prev, code];
      return prev.filter((x) => x !== code);
    });
    invalidateDownstream();
  };

  const applyAnswersToFacts = (base: ConsultationFacts): ConsultationFacts => {
    let working = base;
    for (const q of pendingQuestions) {
      const raw = questionAnswers[q.code];
      if (raw) working = applyQuestionAnswer(working, q.code, raw);
    }
    return working;
  };

  const runExtract = async (nextFacts?: ConsultationFacts) => {
    setExtracting(true);
    setExtractError(null);
    try {
      const workingFacts = enrichFactsWithProfile(nextFacts ?? facts);
      const result = await extractConsultation({
        naturalLanguage: naturalLanguage.trim() || undefined,
        quickSymptoms: selectedSymptoms,
        confirmedFacts: customerProfile || nextFacts ? workingFacts : undefined,
      });
      const mergedFacts = enrichFactsWithProfile(nextFacts ?? result.proposedFacts);
      setExtractResult({ ...result, proposedFacts: mergedFacts });
      setFacts(mergedFacts);

      const symptomPool = [...new Set([...selectedSymptoms, ...mergedFacts.symptoms])];
      const questions = await fetchConsultationQuestions(symptomPool);
      const pending = mergePendingQuestions(questions, symptomPool, mergedFacts);
      setPendingQuestions(pending);
      setSuggestResult(null);
      return { pending, mergedFacts, result: { ...result, proposedFacts: mergedFacts } };
    } catch (error) {
      setExtractError(apiErrorMessage(error, 'Thử lại sau.'));
      throw error;
    } finally {
      setExtracting(false);
    }
  };

  const loadSuggest = async (mergedFacts: ConsultationFacts, evalResult?: ConsultationExtractResult) => {
    const assessment = evalResult?.preliminaryAssessment ?? extractResult?.preliminaryAssessment;
    const level = evalResult?.safetyLevel ?? extractResult?.safetyLevel ?? 'none';
    const blocked =
      level === 'stop_sale'
      || level === 'refer_medical'
      || assessment?.level === 'needs_evaluation';
    if (blocked) {
      setSuggestResult(null);
      return;
    }
    if (!warehouseId) {
      message.warning('Chọn kho trên POS để xem sản phẩm tham khảo.');
      return;
    }
    setLoadingSupport(true);
    try {
      const suggested = await suggestConsultation({
        confirmedFacts: mergedFacts,
        warehouseId,
        limit: 6,
      });
      setSuggestResult(suggested);
    } catch (error) {
      message.warning(apiErrorMessage(error, 'Không tải được hỗ trợ tư vấn.'));
    } finally {
      setLoadingSupport(false);
    }
  };

  const onAnalyzeAndContinue = async () => {
    if (!canAnalyze || extracting) return;
    const hide = message.loading('Đang phân tích…', 0);
    try {
      const { pending, mergedFacts, result } = await runExtract();
      await loadSuggest(mergedFacts, result);
      if (pending.length > 0) {
        message.info('Cần hỏi thêm vài thông tin với khách — trả lời ở mục Phân tích AI.');
      } else {
        message.success('Đã phân tích — xem kết quả bên phải.');
      }
    } catch {
      /* shown */
    } finally {
      hide();
    }
  };

  const onRefreshAfterQuestions = async () => {
    const workingFacts = applyAnswersToFacts(facts);
    setFacts(workingFacts);
    const hide = message.loading('Đang cập nhật phân tích…', 0);
    try {
      const { pending, mergedFacts, result } = await runExtract(workingFacts);
      await loadSuggest(mergedFacts, result);
      if (pending.length > 0) {
        message.warning('Vẫn còn câu hỏi cần trả lời.');
      } else {
        message.success('Đã cập nhật phân tích.');
      }
    } catch {
      /* shown */
    } finally {
      hide();
    }
  };

  const togglePickProduct = (lookupCode: string) => {
    setPickedProducts((prev) => {
      const next = new Set(prev);
      if (next.has(lookupCode)) next.delete(lookupCode);
      else next.add(lookupCode);
      return next;
    });
  };

  const togglePickAll = (checked: boolean) => {
    if (!suggestResult) return;
    if (checked) setPickedProducts(new Set(suggestResult.suggestions.map((s) => s.lookupCode)));
    else setPickedProducts(new Set());
  };

  const addPickedToCart = async () => {
    if (!onAddToCart || !suggestResult) return;
    const codes = suggestResult.suggestions.filter((s) => pickedProducts.has(s.lookupCode)).map((s) => s.lookupCode);
    if (codes.length === 0) {
      message.warning('Chọn ít nhất một sản phẩm.');
      return;
    }
    for (const code of codes) {
      setAddingProductCode(code);
      try {
        await onAddToCart(code);
      } catch (error) {
        Modal.error({ title: 'Không thêm được', content: apiErrorMessage(error, 'Thử lại.') });
        return;
      }
    }
    setAddingProductCode(null);
    message.success(`Đã thêm ${codes.length} sản phẩm vào đơn POS`);
  };

  const save = async () => {
    if (!extractResult) {
      message.warning('Phân tích trước khi lưu phiên tư vấn.');
      return;
    }
    setSaving(true);
    try {
      const session = await confirmConsultation({
        customerId,
        consultationLevel: 1,
        naturalLanguage: naturalLanguage.trim() || undefined,
        quickSymptoms: selectedSymptoms,
        confirmedFacts: enrichFactsWithProfile(facts),
        extractionSource: extractResult.extractionSource,
        aiModel: extractResult.aiModel,
        preliminaryAssessment: extractResult.preliminaryAssessment,
        customerProfileSnapshot: customerProfile ?? undefined,
      });
      onConfirmed(session);
      onClose();
    } catch (error) {
      Modal.error({ title: 'Không lưu được', content: apiErrorMessage(error, 'Thử lại.') });
    } finally {
      setSaving(false);
    }
  };

  const onTransferPharmacist = () => {
    modal.confirm({
      title: 'Chuyển dược sĩ xem lại',
      content:
        'Hàng đợi dược sĩ trên app đang phát triển. Bấm «Ghi vào mô tả» để đánh dấu trong phiên, hoặc lưu phiên tư vấn để dược sĩ tra lại sau.',
      okText: 'Ghi vào mô tả',
      cancelText: 'Đóng',
      onOk: () => {
        setNaturalLanguage((prev) => {
          const tag = '[Cần dược sĩ xem lại]';
          if (prev.includes(tag)) return prev;
          const trimmed = prev.trim();
          return trimmed ? `${trimmed}\n${tag}` : tag;
        });
        message.info('Đã ghi chú — nhớ lưu phiên tư vấn trước khi chốt đơn.');
      },
    });
  };

  const renderQuestionControl = (q: WizardQuestion) => {
    const value = questionAnswers[q.code];
    const supplemental = supplementalOptions(q.code);
    const opts = supplemental ?? (q.answerType === 'boolean' ? yesNoOptions() : null);
    if (opts) {
      return (
        <Radio.Group
          optionType="button"
          buttonStyle="solid"
          size="small"
          value={value}
          onChange={(e) => setQuestionAnswers((prev) => ({ ...prev, [q.code]: e.target.value }))}
          options={opts.map((o) => ({ value: o.value, label: o.label }))}
        />
      );
    }
    if (q.answerType === 'duration_days') {
      return (
        <Radio.Group
          optionType="button"
          buttonStyle="solid"
          size="small"
          value={value}
          onChange={(e) => setQuestionAnswers((prev) => ({ ...prev, [q.code]: e.target.value }))}
          options={durationBucketOptions().map((o) => ({ value: o.value, label: o.label }))}
        />
      );
    }
    return (
      <Input
        size="small"
        value={value ?? ''}
        onChange={(e) => setQuestionAnswers((prev) => ({ ...prev, [q.code]: e.target.value }))}
        placeholder="Nhập câu trả lời từ khách"
      />
    );
  };

  const latestSession = recentSessions[0];

  const renderSectionCustomer = () => (
    <div className="pc-section pc-section--customer">
      <SectionHead n={1} title="Khách hàng" />
      <div className="pc-section-body">
        <div className="pc-customer-picker">
          <Select
            allowClear
            showSearch
            filterOption={false}
            loading={customerSearchLoading}
            onSearch={handleCustomerSearch}
            onDropdownVisibleChange={(visible) => {
              if (visible && customerOptions.length === 0) void runCustomerSearch('');
            }}
            placeholder="Tìm SĐT / mã / tên khách…"
            value={customerId}
            onChange={(id) => selectCustomer(id)}
            notFoundContent={customerSearchLoading ? <Spin size="small" /> : 'Không tìm thấy khách'}
            options={customerOptions.map((c) => ({
              value: c.id,
              label: formatPosCustomerOptionLabel(c),
            }))}
          />
          {onQuickAddCustomer ? (
            <Tooltip title="Thêm khách nhanh">
              <Button icon={<PlusOutlined />} onClick={openQuickAdd} />
            </Tooltip>
          ) : null}
        </div>

        {!customerId ? (
          <Alert
            type="info"
            showIcon
            message="Chọn khách ở trên (hoặc trên POS) để lấy hồ sơ dị ứng / tuổi / tư vấn gần nhất."
          />
        ) : customerProfileLoading ? (
          <Spin size="small" />
        ) : customerProfile && customerDetail ? (
          <>
            <div className="pc-customer-name-row">
              <UserOutlined />
              <Typography.Text strong style={{ fontSize: 14 }}>
                {customerProfile.fullName}
              </Typography.Text>
              {(customerDetail.pharmacyRelation ?? 'member') === 'member' ? (
                <Tag color="blue">Khách quen</Tag>
              ) : null}
              <Typography.Text type="secondary" copyable={{ text: customerProfile.customerCode }}>
                {customerProfile.customerCode}
              </Typography.Text>
            </div>

            <dl className="pc-customer-meta">
              <div>
                <dt>Tuổi</dt>
                <dd>
                  {customerProfile.ageYears != null ? `${customerProfile.ageYears} tuổi` : '—'}
                  {customerProfile.dateOfBirth ? (
                    <Typography.Text type="secondary" style={{ fontSize: 11, marginLeft: 6 }}>
                      (NS: {formatDobVi(customerProfile.dateOfBirth)})
                    </Typography.Text>
                  ) : null}
                </dd>
              </div>
              <div>
                <dt>Giới tính</dt>
                <dd>{customerGenderLabelVi(customerDetail.gender) ?? '—'}</dd>
              </div>
              {customerDetail.addressLine?.trim() ? (
                <div className="pc-customer-meta--full">
                  <dt>Địa chỉ</dt>
                  <dd>{customerDetail.addressLine.trim()}</dd>
                </div>
              ) : null}
            </dl>

            {canPatchCustomer && profileGaps.length > 0 ? (
              <div className="pc-profile-gaps">
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  Thiếu:{' '}
                </Typography.Text>
                {profileGaps.map((gap) => (
                  <Tag key={gap} className="pc-profile-gap-tag">{gap}</Tag>
                ))}
              </div>
            ) : null}

            {canPatchCustomer ? (
              <div className="pc-profile-actions">
                <Button
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() => setProfilePatchOpen(true)}
                >
                  Bổ sung hồ sơ
                </Button>
              </div>
            ) : null}

            <div style={{ fontSize: 12 }}>
              <Typography.Text type="secondary">Dị ứng / ghi chú: </Typography.Text>
              {customerProfile.clinicalNotes ? (
                <Tag color="red" className="pc-allergy-tag">
                  {customerProfile.clinicalNotes}
                </Tag>
              ) : (
                <Typography.Text type="secondary">Chưa ghi nhận</Typography.Text>
              )}
            </div>
            <div style={{ fontSize: 12, marginTop: 4 }}>
              <Typography.Text type="secondary">Bệnh nền: </Typography.Text>
              <Typography.Text type="secondary">Không ghi nhận trên hồ sơ</Typography.Text>
            </div>

            {latestSession ? (
              <div className="pc-prev-box">
                <Typography.Text strong style={{ fontSize: 12 }}>
                  Tư vấn gần nhất: {formatDaysAgo(latestSession.confirmedAt)}
                </Typography.Text>
                <div style={{ marginTop: 4 }}>
                  {symptomLabelsFromCodes(latestSession.symptomCodes, labelByCode) || '—'}
                </div>
                {latestSession.purchasedProductNames.length > 0 ? (
                  <div style={{ marginTop: 4 }}>
                    Sản phẩm đã mua: {latestSession.purchasedProductNames.join(', ')}
                  </div>
                ) : null}
                <Button
                  type="link"
                  size="small"
                  style={{ padding: 0, height: 'auto', marginTop: 4 }}
                  onClick={() => setPreviousDetail(latestSession)}
                >
                  Xem lại tư vấn trước →
                </Button>
              </div>
            ) : null}
          </>
        ) : (
          <Typography.Text type="secondary">Không tải được hồ sơ khách.</Typography.Text>
        )}
      </div>
    </div>
  );

  const renderSectionSymptoms = () => (
    <div className="pc-section pc-section--symptoms">
      <SectionHead n={2} title="Khách đang gặp vấn đề gì?" />
      <div className="pc-section-body">
        {catalogError ? <Alert type="error" showIcon message={catalogError} /> : null}

        <span className="pc-symptom-label">Chọn nhanh (1–3 triệu chứng chính)</span>
        <div className="pc-symptom-grid">
          {quickSymptomOptions.map((opt) => (
            <Tag.CheckableTag
              key={opt.code}
              className="pc-symptom-chip"
              checked={selectedSymptoms.includes(opt.code)}
              onChange={(checked) => setSymptomChecked(opt.code, checked)}
            >
              {opt.icon ? `${opt.icon} ` : ''}
              {opt.label}
            </Tag.CheckableTag>
          ))}
          <Button type="dashed" size="small" icon={<PlusOutlined />} onClick={openSymptomPicker}>
            … Khác
          </Button>
        </div>

        {selectedSymptoms.length > 0 ? (
          <div style={{ marginBottom: 6 }}>
            <Typography.Text type="secondary" style={{ fontSize: 10 }}>
              Đã chọn: {selectedSymptoms.map((c) => labelByCode.get(c) ?? c).join(', ')}
            </Typography.Text>
          </div>
        ) : null}

        <span className="pc-symptom-label">Mô tả thêm (lời khách)</span>
        <Input.TextArea
          rows={2}
          maxLength={NL_MAX}
          showCount
          style={{ marginTop: 2 }}
          value={naturalLanguage}
          onChange={(e) => {
            setNaturalLanguage(e.target.value.slice(0, NL_MAX));
            invalidateDownstream();
          }}
          placeholder="VD: Ho có đờm khoảng 3 ngày, hơi đau họng, sổ mũi."
        />

        {extractError ? (
          <Alert type="error" showIcon message={extractError} style={{ marginTop: 10 }} />
        ) : null}

        <Button
          type="primary"
          className="pc-analyze-btn"
          icon={<ThunderboltOutlined />}
          disabled={!canAnalyze || extracting}
          loading={extracting}
          onClick={() => void onAnalyzeAndContinue()}
        >
          Phân tích &amp; tiếp tục
        </Button>
      </div>
    </div>
  );

  const renderSectionAnalysis = () => (
    <div className="pc-section pc-section--analysis">
      <SectionHead n={3} title="Phân tích bởi AI" />
      <div className="pc-section-body">
        {!extractResult ? (
          <div className="pc-ai-block pc-ai-block--muted">
            Chọn triệu chứng và bấm <strong>Phân tích &amp; tiếp tục</strong> để xem nhận định sơ bộ.
          </div>
        ) : (
          <>
            {preliminaryAssessment ? (
              <div
                className={`pc-ai-block ${
                  preliminaryAssessment.level === 'needs_evaluation'
                    ? 'pc-ai-block--danger'
                    : preliminaryAssessment.level === 'insufficient'
                      ? 'pc-ai-block--warn'
                      : 'pc-ai-block--success'
                }`}
              >
                <Space wrap style={{ marginBottom: 6 }}>
                  <Typography.Text strong>
                    {preliminaryAssessmentBadge(preliminaryAssessment.level)} {PRELIMINARY_ASSESSMENT_TITLE}
                  </Typography.Text>
                  <Popover
                    title="Vì sao nhận định này?"
                    trigger="click"
                    content={
                      <div style={{ maxWidth: 300 }}>
                        <ul style={{ margin: '0 0 8px', paddingLeft: 18 }}>
                          {preliminaryAssessment.supportingFactLines.map((line) => (
                            <li key={line}>{line}</li>
                          ))}
                        </ul>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {preliminaryAssessment.disclaimerVi}
                        </Typography.Text>
                      </div>
                    }
                  >
                    <Button type="link" size="small" icon={<QuestionCircleOutlined />} style={{ padding: 0 }}>
                      Tại sao?
                    </Button>
                  </Popover>
                </Space>
                <div>{preliminaryAssessment.headlineVi}</div>
                {preliminaryAssessment.summaryVi ? (
                  <Typography.Paragraph style={{ margin: '6px 0 0', fontSize: 12 }} type="secondary">
                    {preliminaryAssessment.summaryVi}
                  </Typography.Paragraph>
                ) : null}
                {preliminaryAssessment.hypotheses.map((h, idx) => (
                  <div key={h.code} style={{ fontSize: 12, marginTop: 4 }}>
                    <strong>{hypothesisFitLabel(h.fitLevel, idx)}</strong> — {h.rationaleVi}
                  </div>
                ))}
              </div>
            ) : null}

            <div className="pc-ai-block pc-ai-block--info">
              <Typography.Text strong>
                {safetyLevel === 'none' && safetyFlags.length === 0 ? '🟢 ' : ''}
                {WARNING_SIGNS_TITLE}
              </Typography.Text>
              <div style={{ marginTop: 4, fontSize: 12 }}>
                {consultationSafetyHeadline(safetyLevel)} — {consultationSafetySubtext(safetyLevel, safetyFlags.length > 0)}
              </div>
              {safetyFlags.map((f) => (
                <div key={f.code} style={{ fontSize: 12, color: '#cf1322' }}>
                  • {f.message}
                </div>
              ))}
            </div>

            {pendingQuestions.length > 0 ? (
              <div className="pc-ai-block pc-ai-block--warn">
                <Typography.Text strong>AI cần hỏi thêm</Typography.Text>
                {pendingQuestions.map((q, idx) => (
                  <div key={q.code} className="pc-question-item" style={{ marginTop: idx === 0 ? 10 : 0 }}>
                    <Typography.Text style={{ display: 'block', marginBottom: 6 }}>
                      {idx + 1}. {q.questionVi}
                    </Typography.Text>
                    {renderQuestionControl(q)}
                  </div>
                ))}
                <Button
                  type="primary"
                  size="small"
                  style={{ marginTop: 8 }}
                  loading={extracting || loadingSupport}
                  onClick={() => void onRefreshAfterQuestions()}
                >
                  Cập nhật phân tích
                </Button>
              </div>
            ) : null}
          </>
        )}
      </div>
    </div>
  );

  const renderProductCard = (item: ConsultationProductSuggestion) => {
    const picked = pickedProducts.has(item.lookupCode);
    return (
      <div
        key={item.lookupCode}
        className={`pc-product-card ${picked ? 'pc-product-card--picked' : ''}`}
        onClick={() => togglePickProduct(item.lookupCode)}
        onKeyDown={(e) => e.key === 'Enter' && togglePickProduct(item.lookupCode)}
        role="button"
        tabIndex={0}
      >
        <div className="pc-product-card-check">
          <Checkbox checked={picked} />
        </div>
        <div className="pc-product-card-body">
          <div className="pc-product-name">{item.productName}</div>
          <div className="pc-product-meta">
            {item.productCode}
            {item.genericName ? ` · ${item.genericName}` : ''}
          </div>
          <div className="pc-product-meta">{item.reason}</div>
          <div className="pc-product-price">
            {item.unitPrice.toLocaleString('vi-VN')}đ · Còn {Math.floor(item.stockAvailable)}
          </div>
          <Popover
            title="Vì sao sản phẩm này?"
            trigger="click"
            content={
              <Typography.Paragraph style={{ marginBottom: 0, maxWidth: 260, fontSize: 12 }}>
                {productSupportReason(item.stockAvailable, item.reason)}
              </Typography.Paragraph>
            }
          >
            <Button
              type="link"
              size="small"
              style={{ padding: 0, height: 'auto', fontSize: 11 }}
              onClick={(e) => e.stopPropagation()}
            >
              Tại sao?
            </Button>
          </Popover>
        </div>
      </div>
    );
  };

  const renderSectionProducts = () => (
    <div className="pc-section pc-section--products">
      <SectionHead n={4} title="Hỗ trợ tư vấn sản phẩm" />
      <div className="pc-section-body">
        {!extractResult ? (
          <div className="pc-ai-block pc-ai-block--muted">Phân tích triệu chứng trước để xem sản phẩm tham khảo.</div>
        ) : blocksProductSupport ? (
          <Alert
            type="warning"
            showIcon
            message="Không gợi ý sản phẩm tự động"
            description={
              preliminaryAssessment?.advisoryVi
              || (preliminaryAssessment?.missingInfoHints?.length
                ? formatMissingInfoHints(preliminaryAssessment.missingInfoHints)
                : consultationSafetySubtext(safetyLevel, safetyFlags.length > 0))
            }
          />
        ) : loadingSupport ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : suggestResult && suggestResult.suggestions.length > 0 ? (
          <>
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 10 }}>
              Sản phẩm phù hợp ({suggestResult.suggestions.length}) — tham khảo, dược sĩ quyết định cuối
            </Typography.Text>
            <div className="pc-product-grid">{suggestResult.suggestions.map(renderProductCard)}</div>
            <div className="pc-product-footer">
              <Checkbox
                checked={
                  suggestResult.suggestions.length > 0
                  && pickedProducts.size === suggestResult.suggestions.length
                }
                indeterminate={
                  pickedProducts.size > 0 && pickedProducts.size < suggestResult.suggestions.length
                }
                onChange={(e) => togglePickAll(e.target.checked)}
              >
                Chọn tất cả
              </Checkbox>
              <Button
                type="primary"
                icon={<ShoppingCartOutlined />}
                disabled={pickedProducts.size === 0 || !onAddToCart}
                loading={addingProductCode != null}
                onClick={() => void addPickedToCart()}
              >
                + Thêm vào đơn hàng ({pickedProducts.size})
              </Button>
            </div>
          </>
        ) : (
          <Alert
            type="info"
            showIcon
            message="Chưa có sản phẩm tham khảo"
            description={suggestResult?.blockReason ?? 'Thử hỏi thêm khách hoặc tìm tay trên POS.'}
          />
        )}
      </div>
    </div>
  );

  const renderSummaryBar = () => {
    const enriched = enrichFactsWithProfile(facts);
    return (
      <div className="pc-summary-bar">
        <div className="pc-summary-head">
          <Typography.Text strong style={{ fontSize: 12 }}>5. Tóm tắt phiên tư vấn</Typography.Text>
          <div className="pc-summary-actions">
            <Button size="small" onClick={onClose}>Đóng</Button>
            <Button
              size="small"
              type="primary"
              disabled={!extractResult || saving}
              loading={saving}
              onClick={() => void save()}
            >
              Xác nhận &amp; Lưu tư vấn
            </Button>
          </div>
        </div>
        <div className="pc-summary-grid">
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Khách hàng</span>
            <span className="pc-summary-value" title={customerProfile?.fullName ?? undefined}>
              {customerProfile?.fullName ?? '—'}
            </span>
          </div>
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Triệu chứng chính</span>
            <span className="pc-summary-value" title={displaySymptoms.slice(0, 3).join(', ') || undefined}>
              {displaySymptoms.slice(0, 3).join(', ') || '—'}
            </span>
          </div>
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Thời gian</span>
            <span className="pc-summary-value">
              {enriched.durationDays != null ? `${enriched.durationDays} ngày` : '—'}
            </span>
          </div>
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Dấu hiệu cảnh báo</span>
            <span
              className="pc-summary-value"
              style={{ color: safetyFlags.length ? '#cf1322' : '#389e0d' }}
            >
              {safetyFlags.length ? `${safetyFlags.length} cảnh báo` : 'Không phát hiện'}
            </span>
          </div>
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Nhận định sơ bộ</span>
            <span className="pc-summary-value" title={preliminaryAssessment?.headlineVi ?? undefined}>
              {preliminaryAssessment?.headlineVi ?? '—'}
            </span>
          </div>
          <div className="pc-summary-cell">
            <span className="pc-summary-label">Sản phẩm đã chọn</span>
            <span className="pc-summary-value">{pickedProducts.size} sản phẩm</span>
          </div>
        </div>
      </div>
    );
  };

  const openSymptomPicker = () => {
    setDraftSymptoms([...selectedSymptoms]);
    setShowAllPopular(false);
    setSymptomSearch('');
    const preferred =
      groups.find((g) => g.items.some((i) => selectedSymptoms.includes(i.code)))?.code
      ?? SYMPTOM_OVERVIEW_CATEGORY_CODES.find((code) => groups.some((g) => g.code === code))
      ?? groups[0]?.code;
    if (preferred) setActiveGroupCode(preferred);
    setSymptomDrawerOpen(true);
  };

  const toggleDraftSymptom = (code: string, checked: boolean) => {
    setDraftSymptoms((prev) => {
      if (checked) return prev.includes(code) ? prev : [...prev, code];
      return prev.filter((x) => x !== code);
    });
  };

  const confirmSymptomPicker = () => {
    setSelectedSymptoms(draftSymptoms);
    invalidateDownstream();
    setSymptomDrawerOpen(false);
  };

  const cancelSymptomPicker = () => {
    setSymptomDrawerOpen(false);
    setDraftSymptoms([]);
    setSymptomSearch('');
  };

  const renderSymptomDrawer = () => (
    <Drawer
      className="pc-symptom-picker-drawer"
      rootClassName="pc-symptom-picker-drawer-wrap"
      placement="right"
      open={symptomDrawerOpen}
      onClose={cancelSymptomPicker}
      width="min(680px, 92vw)"
      destroyOnClose
      closable={false}
      mask
      zIndex={1100}
      styles={{
        body: {
          padding: 0,
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        },
      }}
    >
      <div className="pc-sp-shell">
        <div className="pc-sp-header">
          <div className="pc-sp-header-main">
            <h2 className="pc-sp-title">Thêm triệu chứng</h2>
            <p className="pc-sp-subtitle">
              Chọn một hoặc nhiều triệu chứng hoặc tìm nhanh bằng từ khóa
            </p>
          </div>
          <Space>
            <Button
              type="text"
              icon={<QuestionCircleOutlined />}
              onClick={() => setSymptomGuideOpen(true)}
            >
              Hướng dẫn
            </Button>
            <Button type="text" aria-label="Đóng" onClick={cancelSymptomPicker}>
              ✕
            </Button>
          </Space>
        </div>

        <div className="pc-sp-body">
          <div className="pc-sp-main">
            <Input.Search
              className="pc-sp-search"
              allowClear
              size="large"
              placeholder="Tìm triệu chứng (ví dụ: ho, sốt, đau họng…)"
              value={symptomSearch}
              onChange={(e) => setSymptomSearch(e.target.value)}
            />

            {normalizedSearch ? (
              <>
                <span className="pc-sp-section-label">
                  Kết quả tìm kiếm ({searchHits.length})
                </span>
                <div className="pc-sp-chips">
                  {searchHits.length === 0 ? (
                    <Typography.Text type="secondary">Không tìm thấy triệu chứng phù hợp.</Typography.Text>
                  ) : (
                    searchHits.map((opt) => {
                      const on = draftSymptoms.includes(opt.code);
                      return (
                        <button
                          key={opt.code}
                          type="button"
                          className={`pc-sp-chip ${on ? 'pc-sp-chip--on' : ''}`}
                          onClick={() => toggleDraftSymptom(opt.code, !on)}
                        >
                          {on ? <CheckOutlined className="pc-sp-chip-check" /> : null}
                          {opt.label}
                          <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            · {opt.groupLabel}
                          </Typography.Text>
                        </button>
                      );
                    })
                  )}
                </div>
              </>
            ) : (
              <>
                <span className="pc-sp-section-label">Tổng quan</span>
                <div className="pc-sp-categories">
                  {overviewGroups.slice(0, 6).map((group) => {
                    const count = group.items.filter((i) => draftSymptoms.includes(i.code)).length;
                    const active = activeGroupCode === group.code;
                    return (
                      <button
                        key={group.code}
                        type="button"
                        className={`pc-sp-cat ${active ? 'pc-sp-cat--active' : ''}`}
                        onClick={() => setActiveGroupCode(group.code)}
                      >
                        {count > 0 ? <span className="pc-sp-cat-check">✓</span> : null}
                        <span className="pc-sp-cat-icon">
                          {SYMPTOM_CATEGORY_ICONS[group.code] ?? '•'}
                        </span>
                        <span className="pc-sp-cat-label">{group.label}</span>
                        {count > 0 ? (
                          <span className="pc-sp-cat-meta">Đã chọn {count}</span>
                        ) : null}
                      </button>
                    );
                  })}
                </div>

                {overviewGroups.length > 6 ? (
                  <div className="pc-sp-chips" style={{ marginTop: -8 }}>
                    {overviewGroups.slice(6).map((group) => (
                      <button
                        key={group.code}
                        type="button"
                        className={`pc-sp-chip ${activeGroupCode === group.code ? 'pc-sp-chip--on' : ''}`}
                        onClick={() => setActiveGroupCode(group.code)}
                      >
                        {SYMPTOM_CATEGORY_ICONS[group.code] ?? ''} {group.label}
                      </button>
                    ))}
                  </div>
                ) : null}

                <span className="pc-sp-section-label">Triệu chứng phổ biến</span>
                <div className="pc-sp-chips">
                  {visiblePopular.map((opt) => {
                    const on = draftSymptoms.includes(opt.code);
                    const icon = QUICK_SYMPTOM_ICONS[opt.code as keyof typeof QUICK_SYMPTOM_ICONS];
                    return (
                      <button
                        key={opt.code}
                        type="button"
                        className={`pc-sp-chip ${on ? 'pc-sp-chip--on' : ''}`}
                        onClick={() => toggleDraftSymptom(opt.code, !on)}
                      >
                        {on ? <CheckOutlined className="pc-sp-chip-check" /> : null}
                        {icon ? `${icon} ` : ''}
                        {opt.label}
                      </button>
                    );
                  })}
                  {!showAllPopular && popularInActiveGroup.length > 8 ? (
                    <button
                      type="button"
                      className="pc-sp-chip pc-sp-chip--more"
                      onClick={() => setShowAllPopular(true)}
                    >
                      … Xem thêm
                    </button>
                  ) : null}
                </div>

                {otherInActiveGroup.length > 0 ? (
                  <>
                    <span className="pc-sp-section-label">
                      Khác trong nhóm {activeGroup?.label?.toLowerCase() ?? ''}
                    </span>
                    <div className="pc-sp-chips">
                      {otherInActiveGroup.map((opt) => {
                        const on = draftSymptoms.includes(opt.code);
                        return (
                          <button
                            key={opt.code}
                            type="button"
                            className={`pc-sp-chip ${on ? 'pc-sp-chip--on' : ''}`}
                            onClick={() => toggleDraftSymptom(opt.code, !on)}
                          >
                            {on ? <CheckOutlined className="pc-sp-chip-check" /> : null}
                            {opt.label}
                          </button>
                        );
                      })}
                    </div>
                  </>
                ) : null}

                <div className="pc-sp-tip">
                  <InfoCircleOutlined className="pc-sp-tip-icon" />
                  <span>
                    Hãy chọn càng đầy đủ triệu chứng, AI sẽ hỗ trợ tư vấn chính xác hơn
                  </span>
                </div>
              </>
            )}
          </div>

          <div className="pc-sp-side">
            <div className="pc-sp-side-head">
              <h3 className="pc-sp-side-title">
                Triệu chứng đã chọn ({draftSymptoms.length})
              </h3>
              {draftSymptoms.length > 0 ? (
                <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setDraftSymptoms([])}>
                  Xóa tất cả
                </Button>
              ) : null}
            </div>

            {draftSymptoms.length === 0 ? (
              <div className="pc-sp-picked-empty">Chưa chọn triệu chứng nào.</div>
            ) : (
              <div className="pc-sp-picked-list">
                {draftSymptoms.map((code) => (
                  <div key={code} className="pc-sp-picked-item">
                    <span>{labelByCode.get(code) ?? code}</span>
                    <Button
                      type="text"
                      size="small"
                      icon={<MinusOutlined />}
                      aria-label="Bỏ chọn"
                      onClick={() => toggleDraftSymptom(code, false)}
                    />
                  </div>
                ))}
              </div>
            )}

            <div className="pc-sp-ai">
              <div className="pc-sp-ai-head">
                <RobotOutlined />
                Gợi ý từ AI
              </div>
              {draftAiSuggestions.length === 0 ? (
                <div className="pc-sp-ai-text">
                  Chọn vài triệu chứng chính — hệ thống sẽ gợi ý thêm dấu hiệu nên hỏi khách.
                </div>
              ) : (
                <>
                  <div className="pc-sp-ai-text">
                    Có thể hỏi thêm các triệu chứng liên quan để phân tích chính xác hơn:
                  </div>
                  <div className="pc-sp-ai-chips">
                    {draftAiSuggestions.map((s) => (
                      <button
                        key={s.code}
                        type="button"
                        className="pc-sp-chip"
                        onClick={() => toggleDraftSymptom(s.code, true)}
                      >
                        + {s.label}
                      </button>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>

        <div className="pc-sp-footer">
          <Button onClick={cancelSymptomPicker}>Hủy</Button>
          <Button type="primary" onClick={confirmSymptomPicker}>
            Xác nhận ({draftSymptoms.length})
          </Button>
        </div>
      </div>
    </Drawer>
  );

  return (
    <>
      <Modal
        className="pc-studio-modal"
        title={
          <div className="pc-studio-title-row">
            <Space>
              <MedicineBoxOutlined />
              <span>Tư vấn tại quầy</span>
              <Tag color="blue">Beta</Tag>
            </Space>
            <div className="pc-studio-header-actions">
              <Button
                size="small"
                icon={<HistoryOutlined />}
                disabled={!customerId}
                onClick={() => setHistoryOpen(true)}
              >
                Lịch sử tư vấn
              </Button>
              <Button size="small" icon={<UserAddOutlined />} onClick={onTransferPharmacist}>
                Chuyển dược sĩ
              </Button>
            </div>
          </div>
        }
        open={open}
        onCancel={onClose}
        wrapClassName="pc-studio-modal-wrap"
        width="min(1280px, calc(100vw - 48px))"
        styles={{ body: { padding: 0, flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 } }}
        destroyOnClose
        footer={null}
        maskClosable={!extracting && !saving && !loadingSupport}
      >
        <Spin
          spinning={extracting || catalogLoading || loadingSupport}
          wrapperClassName="pc-studio-spin"
        >
          <div className="pc-studio-shell">
            <div className="pc-studio-body">
              <div className="pc-studio-grid">
                <div className="pc-studio-col pc-studio-col--left">
                  {renderSectionCustomer()}
                  {renderSectionSymptoms()}
                </div>
                <div className="pc-studio-col pc-studio-col--right">
                  {renderSectionAnalysis()}
                  {renderSectionProducts()}
                </div>
              </div>
            </div>
            {renderSummaryBar()}
            <div className="pc-disclaimer">{ASSISTANT_DISCLAIMER}</div>
          </div>
        </Spin>
      </Modal>

      {renderSymptomDrawer()}

      <CustomerFormDrawer
        open={profilePatchOpen}
        editing={customerDetail}
        variant="care_patch"
        drawerZIndex={1150}
        onClose={() => setProfilePatchOpen(false)}
        onSaved={handleProfilePatched}
      />

      <Modal
        title="Hướng dẫn chọn triệu chứng"
        open={symptomGuideOpen}
        onCancel={() => setSymptomGuideOpen(false)}
        footer={[
          <Button key="ok" type="primary" onClick={() => setSymptomGuideOpen(false)}>
            Đã hiểu
          </Button>,
        ]}
      >
        <Typography.Paragraph>
          Chọn <strong>1–3 triệu chứng chính</strong> khách đang gặp, rồi bổ sung dấu hiệu liên quan nếu cần.
        </Typography.Paragraph>
        <Typography.Paragraph>
          Dùng ô tìm kiếm khi không thấy trong nhóm. Bấm <strong>Xác nhận</strong> để áp dụng vào phiên tư vấn.
        </Typography.Paragraph>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Đây là hỗ trợ khai thác thông tin — không thay thế đánh giá của dược sĩ.
        </Typography.Paragraph>
      </Modal>

      <Modal
        title="Lịch sử tư vấn"
        open={historyOpen}
        onCancel={() => setHistoryOpen(false)}
        footer={[<Button key="x" onClick={() => setHistoryOpen(false)}>Đóng</Button>]}
        width={560}
      >
        {recentLoading ? (
          <Spin />
        ) : recentSessions.length === 0 ? (
          <Typography.Text type="secondary">Chưa có phiên tư vấn trước.</Typography.Text>
        ) : (
          <Space direction="vertical" style={{ width: '100%' }}>
            {recentSessions.map((s) => (
              <Card
                key={s.id}
                size="small"
                hoverable
                onClick={() => {
                  setPreviousDetail(s);
                  setHistoryOpen(false);
                }}
              >
                <Typography.Text strong>{formatDaysAgo(s.confirmedAt)}</Typography.Text>
                <div style={{ fontSize: 12 }}>
                  {symptomLabelsFromCodes(s.symptomCodes, labelByCode) || '—'}
                </div>
                {s.preliminaryHeadlineVi ? (
                  <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                    {s.preliminaryHeadlineVi}
                  </Typography.Text>
                ) : null}
              </Card>
            ))}
          </Space>
        )}
      </Modal>

      <Modal
        title="Chi tiết tư vấn trước"
        open={previousDetail != null}
        onCancel={() => setPreviousDetail(null)}
        footer={[<Button key="close" onClick={() => setPreviousDetail(null)}>Đóng</Button>]}
        width={520}
      >
        {previousDetail ? (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            <Typography.Text type="secondary">{formatDaysAgo(previousDetail.confirmedAt)}</Typography.Text>
            <div>
              <Typography.Text strong>Triệu chứng: </Typography.Text>
              {symptomLabelsFromCodes(previousDetail.symptomCodes, labelByCode) || '—'}
            </div>
            {previousDetail.naturalLanguageExcerpt ? (
              <div>
                <Typography.Text strong>Khách mô tả: </Typography.Text>
                {previousDetail.naturalLanguageExcerpt}
              </div>
            ) : null}
            {previousDetail.preliminaryHeadlineVi ? (
              <div>
                <Typography.Text strong>Nhận định: </Typography.Text>
                {previousDetail.preliminaryHeadlineVi}
              </div>
            ) : null}
            <div>
              <Typography.Text strong>Kết quả: </Typography.Text>
              {previousDetail.purchasedProductNames.length > 0
                ? `Đã mua — ${previousDetail.purchasedProductNames.join(', ')}`
                : previousDetail.orderLinked
                  ? 'Đã chốt đơn'
                  : 'Chưa liên kết đơn bán'}
            </div>
            <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0 }}>
              Thông tin tham khảo — không tự áp dụng cho lần tư vấn này.
            </Typography.Paragraph>
          </Space>
        ) : null}
      </Modal>
    </>
  );
}
