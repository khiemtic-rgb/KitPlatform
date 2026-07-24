import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Checkbox,
  Drawer,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Steps,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckOutlined,
  CloseOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  GiftOutlined,
  HeartOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createAccountabilityOption,
  createFamilyAgreement,
  decideFamilyAgreement,
  deleteAccountabilityOption,
  fetchAccountabilityOptions,
  fetchFamilies,
  fetchFamilyAgreements,
  fetchFamilyRoutines,
  updateAccountabilityOption,
  type AccountabilityOption,
  type CommitmentTemplate,
  type FamilyAgreement,
  type FamilyMembership,
  type FamilyRoutine,
  type FamilySummary,
} from '@/shared/api/family-os.api';
import './family-os-agreements.css';

const CATEGORY_META: Array<{
  value: string;
  label: string;
  icon: string;
  tone: string;
}> = [
  { value: 'foundation', label: 'Thỏa thuận nền tảng', icon: '🛡️', tone: 'green' },
  { value: 'routine', label: 'Thói quen & Sinh hoạt', icon: '⏰', tone: 'orange' },
  { value: 'commitment', label: 'Học tập', icon: '📘', tone: 'blue' },
  { value: 'value', label: 'Ứng xử & Giao tiếp', icon: '💬', tone: 'pink' },
  { value: 'accountability', label: 'Trách nhiệm & Việc nhà', icon: '🏠', tone: 'brown' },
  { value: 'reward', label: 'Thiết bị & Giải trí', icon: '🎮', tone: 'purple' },
  { value: 'grace', label: 'Grace (gia hạn)', icon: '⏳', tone: 'blue' },
  { value: 'exception', label: 'Ngoại lệ', icon: '✨', tone: 'orange' },
  { value: 'change', label: 'Điều chỉnh', icon: '🔄', tone: 'purple' },
];

const STATUS_FILTER_OPTIONS = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'accepted', label: 'Đang áp dụng' },
  { value: 'proposed', label: 'Đề xuất' },
  { value: 'discussing', label: 'Đang thảo luận' },
  { value: 'rejected', label: 'Từ chối' },
  { value: 'withdrawn', label: 'Rút lại' },
];

const TOPIC_TEMPLATES = [
  {
    key: 'homework',
    label: 'Làm bài tập',
    title: 'Nếu chưa làm bài tập thì không chơi game tối hôm đó',
    purpose: 'Giúp xây dựng tính tự giác và trách nhiệm với cam kết học tập.',
    value: 'responsibility',
  },
  {
    key: 'sleep',
    label: 'Giờ ngủ',
    title: 'Đi ngủ trước 21:00 vào ngày đi học',
    purpose: 'Giữ sức khỏe và đúng giờ.',
    value: 'punctuality',
  },
  {
    key: 'screen',
    label: 'Thời gian màn hình',
    title: 'Giới hạn màn hình theo khung đã thống nhất',
    purpose: 'Cân bằng giải trí và việc cần làm.',
    value: 'self_discipline',
  },
  {
    key: 'chore',
    label: 'Việc nhà',
    title: 'Hoàn thành việc nhà đã cam kết trong ngày',
    purpose: 'Chia sẻ trách nhiệm trong nhà.',
    value: 'helping',
  },
];

const VALUE_OPTIONS = [
  { value: 'self_discipline', label: 'Tự giác' },
  { value: 'responsibility', label: 'Trách nhiệm' },
  { value: 'punctuality', label: 'Đúng giờ' },
  { value: 'helping', label: 'Biết giúp đỡ' },
  { value: 'honesty', label: 'Trung thực' },
  { value: 'respect', label: 'Tôn trọng' },
];

const EXCEPTION_OPTIONS = [
  { value: 'sick', label: 'Ốm / không khỏe' },
  { value: 'birthday', label: 'Sinh nhật' },
  { value: 'travel', label: 'Du lịch' },
  { value: 'parent_approved', label: 'Được bố mẹ chấp thuận' },
];

const WEEKDAY_OPTIONS = [
  { label: 'T2', value: 1 },
  { label: 'T3', value: 2 },
  { label: 'T4', value: 3 },
  { label: 'T5', value: 4 },
  { label: 'T6', value: 5 },
  { label: 'T7', value: 6 },
  { label: 'CN', value: 7 },
];

const GROUP_OPTIONS = [
  { value: 'screen', label: 'Screen' },
  { value: 'responsibility', label: 'Responsibility' },
  { value: 'learning', label: 'Learning' },
  { value: 'family', label: 'Family' },
  { value: 'experience', label: 'Experience' },
  { value: 'recognition', label: 'Recognition' },
];

const WIZARD_STEPS = [
  'Chủ đề',
  'Mục tiêu',
  'Khi nào',
  'Thưởng',
  'Thỏa thuận',
  'Ngoại lệ',
  'Đồng ý & xem lại',
];

function statusTag(status: string) {
  switch (status) {
    case 'accepted':
      return <span className="fa-status is-on">Đang áp dụng</span>;
    case 'rejected':
      return <span className="fa-status is-off">Từ chối</span>;
    case 'withdrawn':
      return <span className="fa-status is-muted">Rút lại</span>;
    case 'discussing':
      return <span className="fa-status is-talk">Đang thảo luận</span>;
    default:
      return <span className="fa-status is-propose">Đề xuất</span>;
  }
}

function categoryMeta(code: string) {
  return CATEGORY_META.find((t) => t.value === code);
}

function categoryLabel(code: string) {
  return categoryMeta(code)?.label ?? code;
}

function categoryTone(code: string) {
  return categoryMeta(code)?.tone ?? 'blue';
}

function parseAgreementDisplay(row: FamilyAgreement): { code: string; name: string } {
  try {
    const terms = JSON.parse(row.termsJson || '{}') as { code?: string };
    if (terms.code) {
      const stripped = row.title.replace(new RegExp(`^${terms.code}\\s*[·•\\-]\\s*`), '');
      return { code: terms.code, name: stripped || row.title };
    }
  } catch {
    /* ignore */
  }
  const m = row.title.match(/^([A-Z]+\d+)\s*[·•\-]\s*(.+)$/i);
  if (m) return { code: m[1].toUpperCase(), name: m[2] };
  return { code: '—', name: row.title };
}

function formatDateVi(value?: string) {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) {
    if (/^\d{4}-\d{2}-\d{2}/.test(value)) {
      const [y, m, day] = value.slice(0, 10).split('-');
      return `${day}/${m}/${y}`;
    }
    return value;
  }
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const yyyy = d.getFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

function slugifyCode(label: string): string {
  return label
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/đ/g, 'd')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 40);
}

export function FamilyOsAgreementsPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [items, setItems] = useState<FamilyAgreement[]>([]);
  const [options, setOptions] = useState<AccountabilityOption[]>([]);
  const [routines, setRoutines] = useState<FamilyRoutine[]>([]);
  const [open, setOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [catalogKind, setCatalogKind] = useState<'all' | 'consequence' | 'reward'>('all');
  const [catalogStatus, setCatalogStatus] = useState<string>('all');
  const [catalogQuery, setCatalogQuery] = useState('');
  const [viewing, setViewing] = useState<FamilyAgreement | null>(null);
  const [optionOpen, setOptionOpen] = useState(false);
  const [editingOption, setEditingOption] = useState<AccountabilityOption | null>(null);
  const [changeOpen, setChangeOpen] = useState(false);
  const [changeSource, setChangeSource] = useState<FamilyAgreement | null>(null);
  const [form] = Form.useForm();
  const [optionForm] = Form.useForm();
  const [changeForm] = Form.useForm();

  const guardians = (family?.members ?? []).filter((m) => m.roleCode !== 'child');
  const consequences = useMemo(
    () => options.filter((o) => o.kind === 'consequence' && o.status === 'active'),
    [options],
  );
  const rewards = useMemo(
    () => options.filter((o) => o.kind === 'reward' && o.status === 'active'),
    [options],
  );
  const templates = useMemo(() => {
    const rows: Array<CommitmentTemplate & { routineName: string }> = [];
    for (const routine of routines) {
      for (const t of routine.templates) {
        if (t.isActive) rows.push({ ...t, routineName: routine.displayName });
      }
    }
    return rows;
  }, [routines]);
  const children = (family?.members ?? []).filter((m) => m.roleCode === 'child');

  const categoryCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const item of items) {
      map.set(item.targetType, (map.get(item.targetType) ?? 0) + 1);
    }
    return map;
  }, [items]);

  const filteredItems = useMemo(() => {
    return items.filter((i) => {
      if (categoryFilter !== 'all' && i.targetType !== categoryFilter) return false;
      if (statusFilter !== 'all' && i.status !== statusFilter) return false;
      return true;
    });
  }, [items, categoryFilter, statusFilter]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setItems([]);
        setOptions([]);
        setRoutines([]);
        return;
      }
      const [agreements, catalog, routineRows] = await Promise.all([
        fetchFamilyAgreements(first.id),
        fetchAccountabilityOptions(first.id),
        fetchFamilyRoutines(first.id),
      ]);
      setItems(agreements);
      setOptions(catalog);
      setRoutines(routineRows);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được thỏa thuận'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    const topic = TOPIC_TEMPLATES[0];
    form.setFieldsValue({
      topicKey: topic.key,
      proposedBy: guardians[0]?.id,
      title: topic.title,
      purpose: topic.purpose,
      supportsValue: topic.value,
      weekdays: [1, 2, 3, 4, 5],
      rewardCode: undefined,
      includeReward: false,
      consequenceCode: consequences[0]?.code,
      triggerCommitmentTemplateId: templates.find((t) =>
        t.title.toLowerCase().includes('bài tập'),
      )?.id ?? templates[0]?.id,
      appliesToMemberId: children[0]?.id,
      exceptions: ['sick', 'travel', 'parent_approved'],
      reviewAfterDays: 30,
      proposalBody: '',
    });
    setWizardStep(0);
    setOpen(true);
  };

  const applyTopic = (key: string) => {
    const topic = TOPIC_TEMPLATES.find((t) => t.key === key);
    if (!topic) return;
    form.setFieldsValue({
      topicKey: key,
      title: topic.title,
      purpose: topic.purpose,
      supportsValue: topic.value,
    });
  };

  const openCreateOption = (kind: 'consequence' | 'reward') => {
    setEditingOption(null);
    optionForm.setFieldsValue({
      kind,
      code: '',
      optionGroup: kind === 'reward' ? 'family' : 'screen',
      labelVi: '',
      descriptionVi: '',
      sortOrder: 500,
    });
    setOptionOpen(true);
  };

  const openEditOption = (row: AccountabilityOption) => {
    setEditingOption(row);
    optionForm.setFieldsValue({
      kind: row.kind,
      code: row.code,
      optionGroup: row.optionGroup,
      labelVi: row.labelVi,
      descriptionVi: row.descriptionVi,
      sortOrder: row.sortOrder,
    });
    setOptionOpen(true);
  };

  const validateWizardStep = async (step: number) => {
    if (step === 0) await form.validateFields(['topicKey', 'title']);
    if (step === 1) await form.validateFields(['purpose', 'supportsValue']);
    if (step === 2) await form.validateFields(['weekdays']);
    if (step === 3) {
      const include = form.getFieldValue('includeReward');
      if (include) await form.validateFields(['rewardCode']);
    }
    if (step === 4) {
      await form.validateFields([
        'consequenceCode',
        'triggerCommitmentTemplateId',
        'appliesToMemberId',
      ]);
    }
    if (step === 5) await form.validateFields(['exceptions']);
    if (step === 6) await form.validateFields(['proposedBy', 'reviewAfterDays']);
  };

  const nextStep = async () => {
    await validateWizardStep(wizardStep);
    setWizardStep((s) => Math.min(s + 1, WIZARD_STEPS.length - 1));
  };

  const save = async () => {
    if (!family) return;
    await validateWizardStep(6);
    const values = form.getFieldsValue(true);
    setSaving(true);
    try {
      const consequence = consequences.find((c) => c.code === values.consequenceCode);
      const exceptions: string[] = values.exceptions ?? [];
      const weekdays: number[] = values.weekdays ?? [];
      const purpose = String(values.purpose ?? '').trim();
      const title = String(values.title ?? '').trim();
      const bodyParts = [
        `Mục tiêu: ${purpose}`,
        `Thỏa thuận: nếu chưa hoàn thành cam kết đã chọn trong khung giờ thì ${
          consequence?.labelVi ?? values.consequenceCode
        }.`,
        exceptions.length
          ? `Ngoại lệ: ${exceptions
              .map((e) => EXCEPTION_OPTIONS.find((o) => o.value === e)?.label ?? e)
              .join(', ')}.`
          : null,
        values.includeReward && values.rewardCode
          ? `Thưởng khi hoàn thành tốt: ${
              rewards.find((r) => r.code === values.rewardCode)?.labelVi ?? values.rewardCode
            }.`
          : null,
        `Xem lại sau ${values.reviewAfterDays ?? 30} ngày.`,
      ].filter(Boolean);

      const terms = {
        schemaVersion: 2,
        purpose,
        conditions: [
          'Chưa hoàn thành cam kết đã chọn trong khung giờ đã thống nhất',
        ],
        exceptions,
        result: {
          kind: 'consequence',
          code: values.consequenceCode,
          labelVi: consequence?.labelVi ?? values.consequenceCode,
        },
        supportsValues: values.supportsValue ? [values.supportsValue] : [],
        schedule: { weekdays },
        triggerCommitmentTemplateId: values.triggerCommitmentTemplateId,
        consequenceCode: values.consequenceCode,
        appliesToMemberId: values.appliesToMemberId,
        rewardCode: values.includeReward ? values.rewardCode : undefined,
      };

      await createFamilyAgreement(family.id, {
        proposedBy: values.proposedBy,
        title,
        proposalBody: bodyParts.join('\n'),
        targetType: 'accountability',
        targetId: values.triggerCommitmentTemplateId || null,
        termsJson: JSON.stringify(terms),
        purpose,
        reviewAfterDays: values.reviewAfterDays ?? 30,
        appliesToMemberId: values.appliesToMemberId || null,
      });

      if (values.includeReward && values.rewardCode) {
        const reward = rewards.find((r) => r.code === values.rewardCode);
        await createFamilyAgreement(family.id, {
          proposedBy: values.proposedBy,
          title: `Quyền lợi: ${reward?.labelVi ?? values.rewardCode}`,
          proposalBody: `Thưởng đã thống nhất kèm thỏa thuận “${title}”.`,
          targetType: 'reward',
          termsJson: JSON.stringify({
            schemaVersion: 2,
            purpose: 'Động lực tích cực đã đồng thuận.',
            conditions: ['Hoàn thành cam kết theo thỏa thuận liên quan'],
            exceptions,
            result: {
              kind: 'reward',
              code: values.rewardCode,
              labelVi: reward?.labelVi ?? values.rewardCode,
            },
            supportsValues: values.supportsValue ? [values.supportsValue] : [],
            rewardCode: values.rewardCode,
          }),
          purpose: 'Động lực tích cực đã đồng thuận.',
          reviewAfterDays: values.reviewAfterDays ?? 30,
          appliesToMemberId: values.appliesToMemberId || null,
        });
      }

      message.success('Đã tạo đề xuất thỏa thuận');
      setOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được'));
    } finally {
      setSaving(false);
    }
  };

  const saveOption = async () => {
    if (!family) return;
    const values = await optionForm.validateFields();
    setSaving(true);
    try {
      if (editingOption) {
        await updateAccountabilityOption(family.id, editingOption.id, {
          optionGroup: values.optionGroup,
          labelVi: values.labelVi,
          descriptionVi: values.descriptionVi,
          sortOrder: values.sortOrder,
        });
        message.success('Đã cập nhật mục catalog');
      } else {
        const code =
          values.code?.trim() ||
          `${values.kind === 'reward' ? 'reward' : 'custom'}_${slugifyCode(values.labelVi)}`;
        await createAccountabilityOption(family.id, {
          kind: values.kind,
          code,
          optionGroup: values.optionGroup,
          labelVi: values.labelVi,
          descriptionVi: values.descriptionVi,
          sortOrder: values.sortOrder,
        });
        message.success('Đã thêm mục catalog');
      }
      setOptionOpen(false);
      setEditingOption(null);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, editingOption ? 'Không sửa được mục' : 'Không thêm được mục'));
    } finally {
      setSaving(false);
    }
  };

  const archiveOption = async (row: AccountabilityOption) => {
    if (!family) return;
    try {
      await updateAccountabilityOption(family.id, row.id, { status: 'archived' });
      message.success('Đã ẩn mục khỏi catalog active');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ẩn được'));
    }
  };

  const confirmDeleteOption = (row: AccountabilityOption) => {
    if (row.isSystem) {
      message.warning('Mục hệ thống không xóa được — dùng Ẩn hoặc Sửa nhãn.');
      return;
    }
    Modal.confirm({
      title: 'Xóa mục tùy chỉnh?',
      content: `“${row.labelVi}” sẽ bị soft-delete và không còn trong catalog.`,
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Hủy',
      onOk: async () => {
        if (!family) return;
        try {
          await deleteAccountabilityOption(family.id, row.id);
          message.success('Đã xóa mục tùy chỉnh');
          await load();
        } catch (error) {
          message.error(apiErrorMessage(error, 'Không xóa được'));
        }
      },
    });
  };

  const decide = async (
    row: FamilyAgreement,
    status: 'accepted' | 'rejected' | 'withdrawn',
  ) => {
    if (!family) return;
    const decidedBy = guardians[0]?.id ?? row.proposedBy;
    const notes: Record<string, string> = {
      accepted: 'Đồng ý trên Admin',
      rejected: 'Từ chối trên Admin',
      withdrawn: 'Rút lại trên Admin',
    };
    try {
      await decideFamilyAgreement(family.id, row.id, {
        status,
        decidedBy,
        decisionNote: notes[status],
      });
      message.success(
        status === 'accepted' ? 'Đã chấp nhận' : status === 'rejected' ? 'Đã từ chối' : 'Đã rút lại',
      );
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không quyết định được'));
    }
  };

  const confirmWithdraw = (row: FamilyAgreement) => {
    Modal.confirm({
      title: 'Rút lại thỏa thuận?',
      content:
        row.status === 'accepted'
          ? 'Thỏa thuận đang Đồng ý sẽ ngừng thi hành sau khi rút lại. Muốn đổi nội dung thì dùng Đề xuất thay đổi.'
          : 'Đề xuất sẽ chuyển sang trạng thái Rút lại.',
      okText: 'Rút lại',
      okButtonProps: { danger: true },
      cancelText: 'Hủy',
      onOk: () => decide(row, 'withdrawn'),
    });
  };

  const openProposeChange = (row: FamilyAgreement) => {
    let previousTerms: Record<string, unknown> = {};
    try {
      previousTerms = JSON.parse(row.termsJson || '{}') as Record<string, unknown>;
    } catch {
      previousTerms = {};
    }
    setChangeSource(row);
    changeForm.setFieldsValue({
      proposedBy: guardians[0]?.id ?? row.proposedBy,
      title: row.title.startsWith('Đề xuất đổi:')
        ? row.title
        : `Đề xuất đổi: ${row.title}`,
      purpose: row.purpose || 'Điều chỉnh thỏa thuận đã có theo đồng thuận nhà.',
      proposalBody:
        `Tham chiếu thỏa thuận hiện tại:\n${row.proposalBody || row.purpose || row.title}\n\n` +
        `Đề xuất điều chỉnh (sửa phần dưới):\n`,
      reviewAfterDays: row.reviewAfterDays ?? 30,
      previousTermsJson: JSON.stringify(previousTerms),
    });
    setChangeOpen(true);
  };

  const saveProposeChange = async () => {
    if (!family || !changeSource) return;
    const values = await changeForm.validateFields();
    setSaving(true);
    try {
      let previousTerms: Record<string, unknown> = {};
      try {
        previousTerms = JSON.parse(String(values.previousTermsJson || '{}')) as Record<
          string,
          unknown
        >;
      } catch {
        previousTerms = {};
      }
      const purpose = String(values.purpose ?? '').trim();
      const terms = {
        ...previousTerms,
        schemaVersion: 2,
        purpose,
        supersedesAgreementId: changeSource.id,
        sourceCategory: changeSource.targetType,
        sourceTitle: changeSource.title,
      };
      await createFamilyAgreement(family.id, {
        proposedBy: values.proposedBy,
        title: String(values.title ?? '').trim(),
        proposalBody: String(values.proposalBody ?? '').trim(),
        targetType: 'change',
        targetId: changeSource.id,
        termsJson: JSON.stringify(terms),
        purpose,
        reviewAfterDays: values.reviewAfterDays ?? 30,
        appliesToMemberId: changeSource.appliesToMemberId ?? null,
      });
      message.success('Đã tạo đề xuất thay đổi — chờ cả nhà Đồng ý');
      setChangeOpen(false);
      setChangeSource(null);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được đề xuất thay đổi'));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<FamilyAgreement> = [
    {
      title: 'Mã',
      width: 88,
      render: (_, row) => (
        <span className="fa-code">{parseAgreementDisplay(row).code}</span>
      ),
    },
    {
      title: 'Tên thỏa thuận',
      render: (_, row) => {
        const { name } = parseAgreementDisplay(row);
        return (
          <div className="fa-name">
            <strong>{name}</strong>
            <span>{row.purpose || row.proposalBody}</span>
          </div>
        );
      },
    },
    {
      title: 'Loại',
      dataIndex: 'targetType',
      width: 180,
      render: (v: string) => (
        <span className={`fa-type tone-${categoryTone(v)}`}>{categoryLabel(v)}</span>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 140,
      render: statusTag,
    },
    {
      title: 'Được tạo',
      width: 110,
      render: (_, row) => formatDateVi(row.createdAt),
    },
    {
      title: 'Cập nhật',
      width: 110,
      render: (_, row) => formatDateVi(row.decidedAt || row.createdAt),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 130,
      align: 'right',
      render: (_, row) => {
        const pending = row.status === 'proposed' || row.status === 'discussing';
        const accepted = row.status === 'accepted';
        return (
          <div className="fa-row-actions">
            <button
              type="button"
              className="fa-icon-btn"
              title="Xem"
              onClick={() => setViewing(row)}
            >
              <EyeOutlined />
            </button>
            {accepted ? (
              <button
                type="button"
                className="fa-icon-btn"
                title="Đề xuất đổi"
                onClick={() => openProposeChange(row)}
              >
                <EditOutlined />
              </button>
            ) : null}
            {pending ? (
              <>
                <button
                  type="button"
                  className="fa-icon-btn is-ok"
                  title="Đồng ý"
                  onClick={() => void decide(row, 'accepted')}
                >
                  <CheckOutlined />
                </button>
                <button
                  type="button"
                  className="fa-icon-btn is-danger"
                  title="Từ chối"
                  onClick={() => void decide(row, 'rejected')}
                >
                  <CloseOutlined />
                </button>
              </>
            ) : null}
            {(pending || accepted) ? (
              <button
                type="button"
                className="fa-icon-btn is-danger"
                title="Rút lại"
                onClick={() => confirmWithdraw(row)}
              >
                <DeleteOutlined />
              </button>
            ) : (
              <button
                type="button"
                className="fa-icon-btn"
                title="Rút lại"
                onClick={() => confirmWithdraw(row)}
              >
                <UndoOutlined />
              </button>
            )}
          </div>
        );
      },
    },
  ];

  const catalogCounts = useMemo(() => {
    const consequence = options.filter((o) => o.kind === 'consequence').length;
    const reward = options.filter((o) => o.kind === 'reward').length;
    return { all: options.length, consequence, reward };
  }, [options]);

  const filteredOptions = useMemo(() => {
    const q = catalogQuery.trim().toLowerCase();
    return options.filter((o) => {
      if (catalogKind !== 'all' && o.kind !== catalogKind) return false;
      if (catalogStatus !== 'all' && o.status !== catalogStatus) return false;
      if (!q) return true;
      return (
        o.labelVi.toLowerCase().includes(q) ||
        o.code.toLowerCase().includes(q) ||
        o.optionGroup.toLowerCase().includes(q) ||
        o.descriptionVi.toLowerCase().includes(q)
      );
    });
  }, [options, catalogKind, catalogStatus, catalogQuery]);

  const optionColumns: ColumnsType<AccountabilityOption> = [
    {
      title: 'Loại',
      dataIndex: 'kind',
      width: 150,
      render: (kind: string) =>
        kind === 'reward' ? (
          <span className="fa-kind is-reward">
            <GiftOutlined /> Thưởng
          </span>
        ) : (
          <span className="fa-kind is-agree">
            <HeartOutlined /> Thỏa thuận
          </span>
        ),
    },
    {
      title: 'Nội dung',
      render: (_, row) => (
        <div className="fa-opt-body">
          <strong>{row.labelVi}</strong>
          <span>
            {row.code} · {row.optionGroup} · {row.isSystem ? 'hệ thống' : 'tùy chỉnh'}
          </span>
          {row.descriptionVi ? <em>{row.descriptionVi}</em> : null}
        </div>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 120,
      render: (s: string) =>
        s === 'active' ? (
          <span className="fa-opt-status is-active">Active</span>
        ) : (
          <span className="fa-opt-status is-archived">
            {s === 'archived' ? 'Archived' : s}
          </span>
        ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 180,
      render: (_, row) => (
        <div className="fa-opt-actions">
          <button type="button" className="fa-opt-link" onClick={() => openEditOption(row)}>
            <EditOutlined /> Sửa
          </button>
          {row.status === 'active' ? (
            <button
              type="button"
              className="fa-opt-link is-danger"
              onClick={() => void archiveOption(row)}
            >
              <EyeInvisibleOutlined /> Ẩn
            </button>
          ) : (
            <button
              type="button"
              className="fa-opt-link"
              disabled={!family}
              onClick={() =>
                void updateAccountabilityOption(family!.id, row.id, { status: 'active' }).then(
                  () => {
                    message.success('Đã hiện lại');
                    return load();
                  },
                  (error) => message.error(apiErrorMessage(error, 'Không hiện lại được')),
                )
              }
            >
              <EyeOutlined /> Hiện
            </button>
          )}
          {!row.isSystem ? (
            <button
              type="button"
              className="fa-opt-link is-danger"
              onClick={() => confirmDeleteOption(row)}
            >
              <DeleteOutlined /> Xóa
            </button>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div className={`fa-page${loading ? ' is-loading' : ''}`}>
      <header className="fa-header">
        <div>
          <h1>
            Thỏa thuận nhà <span aria-hidden>🛡️</span>
          </h1>
          <p>
            {family
              ? `${family.displayName} — Những nguyên tắc để hiểu nhau & cùng tiến bộ mỗi ngày.`
              : 'Những nguyên tắc để hiểu nhau & cùng tiến bộ mỗi ngày.'}
          </p>
        </div>
        <div className="fa-header-actions">
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Làm mới
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!family}>
            Thêm thỏa thuận
          </Button>
        </div>
      </header>

      <section className="fa-screen-bound">
        <div className="fa-screen-bound-copy">
          <div className="fa-card-label">Screen Boundary · A + B</div>
          <h2 className="fa-card-title" style={{ marginTop: 4 }}>
            App giữ lời hứa · OS giữ ranh giới máy
          </h2>
          <p>
            <strong>A · Soft-lock:</strong> khi nhà Áp dụng thỏa thuận <code>screen_*</code>, app
            con khóa nhẹ — đổi người cần mã bố mẹ. Con vẫn làm Mission được.
          </p>
          <p>
            <strong>B · Screen Agreement:</strong> FamilyOS không chặn web/game giúp. Bố mẹ mở
            Screen Time / Family Link và tick checklist trên Parent Board (family-app).
          </p>
          <div className="fa-screen-bound-links">
            <a
              href="https://support.apple.com/vi-vn/HT208982"
              target="_blank"
              rel="noreferrer"
            >
              Screen Time (iPhone) →
            </a>
            <a
              href="https://families.google.com/familylink/"
              target="_blank"
              rel="noreferrer"
            >
              Family Link (Android) →
            </a>
          </div>
        </div>
        <ol className="fa-screen-bound-steps">
          <li>Thống nhất thỏa thuận màn hình trong catalog / Agreement</li>
          <li>Khi bỏ qua Mission → parent <em>Áp dụng</em> hậu quả screen_*</li>
          <li>Kid app soft-lock (A) · checklist cấu hình máy (B)</li>
          <li>Không dùng FamilyOS như antivirus / firewall</li>
        </ol>
      </section>

      <section className="fa-banner">
        <div className="fa-banner-copy">
          <span className="fa-banner-i" aria-hidden>
            i
          </span>
          <div>
            <strong>
              Thỏa thuận là nền tảng giúp gia đình mình sống cùng nhau vui vẻ — đồng thuận trước,
              thực hiện sau, nhìn lại để tiến bộ.
            </strong>
            <p>
              Chỉ thi hành khi trạng thái Đang áp dụng. Trình bày như quy tắc thống nhất (mục tiêu +
              ngoại lệ), không phải hình phạt.
            </p>
          </div>
        </div>
        <div className="fa-banner-art" aria-hidden>
          📋✨🛡️
        </div>
      </section>

      <div className="fa-layout">
        <aside className="fa-cats">
          <button
            type="button"
            className={`fa-cat${categoryFilter === 'all' ? ' is-on' : ''}`}
            onClick={() => setCategoryFilter('all')}
          >
            <span className="fa-cat-ico" aria-hidden>
              📚
            </span>
            <span className="fa-cat-label">Tất cả</span>
            <em>{items.length}</em>
          </button>
          {CATEGORY_META.map((cat) => (
            <button
              key={cat.value}
              type="button"
              className={`fa-cat tone-${cat.tone}${categoryFilter === cat.value ? ' is-on' : ''}`}
              onClick={() => setCategoryFilter(cat.value)}
            >
              <span className="fa-cat-ico" aria-hidden>
                {cat.icon}
              </span>
              <span className="fa-cat-label">{cat.label}</span>
              <em>{categoryCounts.get(cat.value) ?? 0}</em>
            </button>
          ))}
        </aside>

        <section className="fa-main">
          <div className="fa-toolbar">
            <Select
              style={{ minWidth: 200 }}
              value={statusFilter}
              onChange={setStatusFilter}
              options={STATUS_FILTER_OPTIONS}
            />
          </div>

          <div className="fa-table-wrap">
            <Table
              rowKey="id"
              loading={loading}
              columns={columns}
              dataSource={filteredItems}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showTotal: (total, range) =>
                  `Hiển thị ${range[0]} - ${range[1]} trong tổng số ${total} thỏa thuận`,
              }}
              locale={{ emptyText: 'Chưa có thỏa thuận — mở Wizard đề xuất đầu tiên.' }}
            />
          </div>
        </section>
      </div>

      <section className="fa-catalog">
        <header className="fa-catalog-head">
          <div>
            <h2>Catalog thưởng & thỏa thuận (theo nhà)</h2>
            <p>
              Catalog an toàn theo Constitution v1.0 — chỉ hỗ trợ thỏa thuận tích cực. Mục hệ thống
              được seed; nhà có thể thêm hoặc ẩn.
            </p>
          </div>
          <div className="fa-catalog-actions">
            <Button onClick={() => openCreateOption('consequence')} disabled={!family}>
              + Thỏa thuận
            </Button>
            <Button type="primary" onClick={() => openCreateOption('reward')} disabled={!family}>
              + Thưởng
            </Button>
          </div>
        </header>

        <div className="fa-catalog-banner">
          <span className="fa-banner-i" aria-hidden>
            i
          </span>
          <div>
            <strong>Catalog chỉ hỗ trợ thỏa thuận tích cực.</strong>
            <p>
              Không hỗ trợ cấm ăn / đánh / xúc phạm / phạt tiền. Đây là quy tắc thống nhất để nhà
              cùng tiến bộ — không phải hình phạt.
            </p>
          </div>
          <div className="fa-catalog-banner-art" aria-hidden>
            📋🛡️
          </div>
        </div>

        <div className="fa-catalog-toolbar">
          <div className="fa-catalog-tabs" role="tablist">
            <button
              type="button"
              role="tab"
              className={catalogKind === 'all' ? 'is-on' : ''}
              onClick={() => setCatalogKind('all')}
            >
              Tất cả ({catalogCounts.all})
            </button>
            <button
              type="button"
              role="tab"
              className={catalogKind === 'consequence' ? 'is-on' : ''}
              onClick={() => setCatalogKind('consequence')}
            >
              <HeartOutlined /> Thỏa thuận ({catalogCounts.consequence})
            </button>
            <button
              type="button"
              role="tab"
              className={catalogKind === 'reward' ? 'is-on' : ''}
              onClick={() => setCatalogKind('reward')}
            >
              <GiftOutlined /> Thưởng ({catalogCounts.reward})
            </button>
          </div>
          <div className="fa-catalog-filters">
            <Select
              style={{ minWidth: 160 }}
              value={catalogStatus}
              onChange={setCatalogStatus}
              options={[
                { value: 'all', label: 'Tất cả trạng thái' },
                { value: 'active', label: 'Active' },
                { value: 'archived', label: 'Archived' },
              ]}
            />
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder="Tìm kiếm..."
              value={catalogQuery}
              onChange={(e) => setCatalogQuery(e.target.value)}
              style={{ width: 220 }}
            />
          </div>
        </div>

        <div className="fa-catalog-table">
          <Table
            rowKey="id"
            loading={loading}
            columns={optionColumns}
            dataSource={filteredOptions}
            pagination={{
              pageSize: 8,
              showSizeChanger: true,
              showTotal: (total, range) =>
                `Hiển thị ${range[0]} – ${range[1]} trong tổng số ${total} thỏa thuận`,
            }}
            locale={{ emptyText: 'Chưa có mục catalog.' }}
          />
        </div>
      </section>

      <Drawer
        title={viewing ? parseAgreementDisplay(viewing).name : 'Chi tiết thỏa thuận'}
        width={480}
        open={!!viewing}
        onClose={() => setViewing(null)}
        destroyOnClose
      >
        {viewing ? (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <div>
              <span className="fa-code">{parseAgreementDisplay(viewing).code}</span>{' '}
              <span className={`fa-type tone-${categoryTone(viewing.targetType)}`}>
                {categoryLabel(viewing.targetType)}
              </span>{' '}
              {statusTag(viewing.status)}
            </div>
            <Typography.Paragraph>{viewing.proposalBody}</Typography.Paragraph>
            {viewing.purpose ? (
              <Typography.Text type="secondary">Mục tiêu: {viewing.purpose}</Typography.Text>
            ) : null}
            <Typography.Text type="secondary">
              Đề xuất bởi {viewing.proposedByName ?? '—'} · Tạo {formatDateVi(viewing.createdAt)}
              {viewing.reviewAfterDays ? ` · Xem lại sau ${viewing.reviewAfterDays} ngày` : ''}
            </Typography.Text>
            {viewing.status === 'accepted' ? (
              <Button type="primary" icon={<EditOutlined />} onClick={() => {
                const row = viewing;
                setViewing(null);
                openProposeChange(row);
              }}>
                Đề xuất đổi
              </Button>
            ) : null}
          </Space>
        ) : null}
      </Drawer>

      <Drawer
        title="Wizard thỏa thuận"
        width={560}
        open={open}
        onClose={() => setOpen(false)}
        destroyOnClose
        extra={
          <Space>
            {wizardStep > 0 ? (
              <Button onClick={() => setWizardStep((s) => s - 1)}>Quay lại</Button>
            ) : null}
            {wizardStep < WIZARD_STEPS.length - 1 ? (
              <Button type="primary" onClick={() => void nextStep()}>
                Tiếp
              </Button>
            ) : (
              <Button type="primary" loading={saving} onClick={() => void save()}>
                Tạo đề xuất
              </Button>
            )}
          </Space>
        }
      >
        <Steps
          size="small"
          current={wizardStep}
          items={WIZARD_STEPS.map((title) => ({ title }))}
          style={{ marginBottom: 20 }}
        />
        <Form form={form} layout="vertical">
          {wizardStep === 0 ? (
            <>
              <Form.Item name="topicKey" label="Bạn muốn thống nhất điều gì?" rules={[{ required: true }]}>
                <Select
                  options={TOPIC_TEMPLATES.map((t) => ({ value: t.key, label: t.label }))}
                  onChange={applyTopic}
                />
              </Form.Item>
              <Form.Item name="title" label="Tiêu đề thỏa thuận" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
            </>
          ) : null}

          {wizardStep === 1 ? (
            <>
              <Form.Item name="purpose" label="Mục tiêu của thỏa thuận" rules={[{ required: true }]}>
                <Input.TextArea rows={3} placeholder="Ví dụ: xây tính tự giác…" />
              </Form.Item>
              <Form.Item name="supportsValue" label="Giá trị nhà được hỗ trợ" rules={[{ required: true }]}>
                <Select options={VALUE_OPTIONS} />
              </Form.Item>
            </>
          ) : null}

          {wizardStep === 2 ? (
            <Form.Item name="weekdays" label="Khi nào áp dụng?" rules={[{ required: true }]}>
              <Checkbox.Group options={WEEKDAY_OPTIONS} />
            </Form.Item>
          ) : null}

          {wizardStep === 3 ? (
            <>
              <Form.Item name="includeReward" label="Có quyền lợi khi hoàn thành?" valuePropName="checked">
                <Checkbox>Thêm thỏa thuận thưởng kèm theo</Checkbox>
              </Form.Item>
              <Form.Item noStyle shouldUpdate>
                {() =>
                  form.getFieldValue('includeReward') ? (
                    <Form.Item
                      name="rewardCode"
                      label="Thưởng (catalog)"
                      rules={[{ required: true, message: 'Chọn thưởng' }]}
                    >
                      <Select
                        options={rewards.map((item) => ({
                          value: item.code,
                          label: `${item.labelVi} (${item.optionGroup})`,
                        }))}
                      />
                    </Form.Item>
                  ) : (
                    <Typography.Text type="secondary">
                      Có thể bỏ qua — chỉ tạo thỏa thuận.
                    </Typography.Text>
                  )
                }
              </Form.Item>
            </>
          ) : null}

          {wizardStep === 4 ? (
            <>
              <Form.Item
                name="consequenceCode"
                label="Thỏa thuận khi chưa hoàn thành"
                rules={[{ required: true }]}
                extra="Chọn từ catalog an toàn. Đây là quy tắc đã thống nhất, không phải hình phạt tùy ý."
              >
                <Select
                  options={consequences.map((item) => ({
                    value: item.code,
                    label: `${item.labelVi} (${item.optionGroup})`,
                  }))}
                />
              </Form.Item>
              <Form.Item
                name="triggerCommitmentTemplateId"
                label="Cam kết kích hoạt"
                rules={[{ required: true }]}
              >
                <Select
                  options={templates.map((t) => ({
                    value: t.id,
                    label: `${t.title} (${t.routineName})`,
                  }))}
                />
              </Form.Item>
              <Form.Item name="appliesToMemberId" label="Áp dụng cho" rules={[{ required: true }]}>
                <Select
                  options={(family?.members ?? []).map((m) => ({
                    value: m.id,
                    label: `${m.displayName} (${m.roleCode})`,
                  }))}
                />
              </Form.Item>
            </>
          ) : null}

          {wizardStep === 5 ? (
            <Form.Item name="exceptions" label="Ngoại lệ" rules={[{ required: true }]}>
              <Checkbox.Group
                style={{ display: 'flex', flexDirection: 'column', gap: 8 }}
                options={EXCEPTION_OPTIONS}
              />
            </Form.Item>
          ) : null}

          {wizardStep === 6 ? (
            <>
              <Form.Item name="proposedBy" label="Người đề xuất" rules={[{ required: true }]}>
                <Select
                  options={(family?.members ?? []).map((m: FamilyMembership) => ({
                    value: m.id,
                    label: `${m.displayName} (${m.roleCode})`,
                  }))}
                />
              </Form.Item>
              <Form.Item
                name="reviewAfterDays"
                label="Bao lâu sẽ xem lại?"
                rules={[{ required: true }]}
              >
                <Select
                  options={[
                    { value: 30, label: '30 ngày' },
                    { value: 90, label: '90 ngày' },
                    { value: 180, label: '180 ngày' },
                  ]}
                />
              </Form.Item>
              <Alert
                type="success"
                showIcon
                message="Bước cuối: tạo đề xuất thỏa thuận (và thưởng nếu chọn). Cả nhà Đồng ý trên danh sách trước khi thi hành."
              />
            </>
          ) : null}
        </Form>
      </Drawer>

      <Drawer
        title={editingOption ? 'Sửa mục catalog' : 'Thêm mục catalog'}
        width={420}
        open={optionOpen}
        onClose={() => {
          setOptionOpen(false);
          setEditingOption(null);
        }}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void saveOption()}>
            Lưu
          </Button>
        }
      >
        <Form form={optionForm} layout="vertical">
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select
              disabled={!!editingOption}
              options={[
                { value: 'consequence', label: 'Thỏa thuận' },
                { value: 'reward', label: 'Thưởng' },
              ]}
            />
          </Form.Item>
          <Form.Item name="labelVi" label="Nhãn hiển thị" rules={[{ required: true }]}>
            <Input placeholder="Ví dụ: Giảm 15 phút TikTok" />
          </Form.Item>
          <Form.Item
            name="code"
            label="Mã (snake_case)"
            extra={editingOption ? 'Không đổi mã khi sửa' : 'Để trống sẽ tự tạo từ nhãn'}
          >
            <Input placeholder="custom_reduce_tiktok_15" disabled={!!editingOption} />
          </Form.Item>
          <Form.Item name="optionGroup" label="Nhóm" rules={[{ required: true }]}>
            <Select options={GROUP_OPTIONS} />
          </Form.Item>
          <Form.Item name="descriptionVi" label="Mô tả">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="sortOrder" label="Thứ tự">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={
          changeSource
            ? `Đề xuất thay đổi · ${changeSource.title}`
            : 'Đề xuất thay đổi'
        }
        width={480}
        open={changeOpen}
        onClose={() => {
          setChangeOpen(false);
          setChangeSource(null);
        }}
        destroyOnClose
        extra={
          <Button type="primary" loading={saving} onClick={() => void saveProposeChange()}>
            Tạo đề xuất
          </Button>
        }
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="Tạo đề xuất mới (nhóm Điều chỉnh). Thỏa thuận cũ vẫn giữ đến khi đề xuất được Đồng ý — rồi nhà có thể Rút lại bản cũ."
        />
        <Form form={changeForm} layout="vertical">
          <Form.Item name="previousTermsJson" hidden>
            <Input />
          </Form.Item>
          <Form.Item name="proposedBy" label="Người đề xuất" rules={[{ required: true }]}>
            <Select
              options={(family?.members ?? []).map((m: FamilyMembership) => ({
                value: m.id,
                label: `${m.displayName} (${m.roleCode})`,
              }))}
            />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề đề xuất" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="purpose" label="Mục tiêu điều chỉnh" rules={[{ required: true }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item
            name="proposalBody"
            label="Nội dung đề xuất"
            rules={[{ required: true, message: 'Mô tả điều muốn đổi' }]}
          >
            <Input.TextArea rows={8} />
          </Form.Item>
          <Form.Item name="reviewAfterDays" label="Xem lại sau" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 30, label: '30 ngày' },
                { value: 90, label: '90 ngày' },
                { value: 180, label: '180 ngày' },
              ]}
            />
          </Form.Item>
        </Form>
      </Drawer>
    </div>
  );
}
