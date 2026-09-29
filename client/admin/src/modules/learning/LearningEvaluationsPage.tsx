import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Avatar,
  Button,
  Card,
  Checkbox,
  Col,
  Descriptions,
  Drawer,
  Empty,
  Form,
  Input,
  Modal,
  Progress,
  Radio,
  Rate,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  EyeOutlined,
  PlayCircleOutlined,
  QuestionCircleOutlined,
  RightOutlined,
  RobotOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchCareerRoster,
  fetchEmployeeEvidence,
  fetchLearningEvaluations,
  upsertLearningEvaluation,
  type LearningCareerRosterItem,
  type LearningEmployeeEvidence,
  type LearningEvaluation,
} from '@/shared/api/learning.api';
import { fetchEmployees } from '@/shared/api/identity-admin.api';
import type { EmployeeLookup } from '@/shared/api/identity-admin.types';
import { useCanLearningWrite } from '@/shared/auth/usePermission';
import { PeoplePageHint } from '@/modules/learning/PeopleModuleIntro';
import { humanizeMissingReason } from '@/modules/learning/competency-labels';

const now = new Date();

const MONTH_OPTIONS = [
  { value: 1, label: 'Tháng 1' },
  { value: 2, label: 'Tháng 2' },
  { value: 3, label: 'Tháng 3' },
  { value: 4, label: 'Tháng 4' },
  { value: 5, label: 'Tháng 5' },
  { value: 6, label: 'Tháng 6' },
  { value: 7, label: 'Tháng 7' },
  { value: 8, label: 'Tháng 8' },
  { value: 9, label: 'Tháng 9' },
  { value: 10, label: 'Tháng 10' },
  { value: 11, label: 'Tháng 11' },
  { value: 12, label: 'Tháng 12' },
];

/** Thang chấm cố định — không nhập số bừa. */
const SCORE_BANDS = [
  {
    value: 55,
    label: 'Chưa đạt',
    short: '55',
    hint: 'Thiếu học / hay sai quy trình / cần kèm sát',
    color: '#ff4d4f',
  },
  {
    value: 70,
    label: 'Đạt',
    short: '70',
    hint: 'Làm đúng khi có hướng dẫn, ít chủ động',
    color: '#faad14',
  },
  {
    value: 80,
    label: 'Tốt',
    short: '80',
    hint: 'Ổn định, đúng SOP, ít cần nhắc',
    color: '#1677ff',
  },
  {
    value: 90,
    label: 'Xuất sắc',
    short: '90',
    hint: 'Chủ động, đúng mực, có thể kèm người khác',
    color: '#52c41a',
  },
] as const;

const CRITERIA = [
  {
    name: 'scoreKnowledge' as const,
    label: 'Kiến thức',
    ask: 'Nhân viên nắm bán hàng tại quầy, xuất hàng gần hết hạn trước, quy trình trên hệ thống thế nào?',
    tone: '#1677ff',
  },
  {
    name: 'scoreAttitude' as const,
    label: 'Thái độ',
    ask: 'Tác phong quầy, hợp tác, sẵn sàng học?',
    tone: '#722ed1',
  },
  {
    name: 'scoreCare' as const,
    label: 'Chăm sóc khách',
    ask: 'Đón khách, gắn điểm, ranh giới tư vấn / mời dược sĩ?',
    tone: '#eb2f96',
  },
  {
    name: 'scoreStock' as const,
    label: 'Kho / hết hạn',
    ask: 'Xuất trước hàng gần hết hạn, báo hết hàng, nhận hàng đúng lô?',
    tone: '#13c2c2',
  },
  {
    name: 'scoreDiscipline' as const,
    label: 'Kỷ luật ca',
    ask: 'Mở–đóng ca, đối quỹ, báo cáo sự cố đúng?',
    tone: '#fa8c16',
  },
] as const;

const SMART_GOALS = [
  { key: 'l3', label: 'Hoàn thành bài / bậc tiếp theo (vd. bậc 3)' },
  { key: 'crm', label: 'Chăm sóc khách ổn định (gắn khách tốt hơn)' },
  { key: 'checklist', label: 'Không quên checklist đóng ca' },
  { key: 'fefo', label: 'Xuất trước hàng gần hết hạn khi lấy hàng' },
  { key: 'mentor', label: 'Kèm được 1 đồng nghiệp mới' },
] as const;

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

function scoreToStars(score: number | null | undefined) {
  if (score == null) return 0;
  return Math.max(0, Math.min(5, Math.round(score / 20)));
}

function bandLabel(score: number) {
  const b = [...SCORE_BANDS].reverse().find((x) => score >= x.value) ?? SCORE_BANDS[0];
  return `${b.short} ${b.label}`;
}

function competencyLabel(score: number) {
  const b = [...SCORE_BANDS].reverse().find((x) => score >= x.value) ?? SCORE_BANDS[0];
  return b.label;
}

function snapToBand(n: number | null | undefined) {
  if (n == null) return 70;
  const sorted = [...SCORE_BANDS].sort(
    (a, b) => Math.abs(a.value - n) - Math.abs(b.value - n),
  );
  return sorted[0].value;
}

function pctTrain(ev: LearningEmployeeEvidence) {
  if (!ev.modulesTotal) return 0;
  return Math.round((100 * ev.modulesPassed) / ev.modulesTotal);
}

function pctChecklist(ev: LearningEmployeeEvidence) {
  const days = ev.closeChecklistDaysThisMonth ?? 0;
  const streak = ev.closeStreakDays ?? 0;
  return Math.min(100, Math.round(days * 4 + Math.min(28, streak * 4)));
}

function pctCrm(ev: LearningEmployeeEvidence) {
  const care = ev.suggestedCare ?? 70;
  return snapToBand(care);
}

function buildAiSummary(ev: LearningEmployeeEvidence) {
  const dims = [
    { key: 'Kiến thức / quầy', score: snapToBand(ev.suggestedKnowledge), label: 'Kiến thức' },
    { key: 'Thái độ', score: snapToBand(ev.suggestedAttitude), label: 'Thái độ' },
    { key: 'Chăm sóc khách', score: snapToBand(ev.suggestedCare), label: 'Chăm sóc khách' },
    { key: 'Kho / hết hạn', score: snapToBand(ev.suggestedStock), label: 'Kho / hết hạn' },
    { key: 'Checklist / kỷ luật', score: snapToBand(ev.suggestedDiscipline), label: 'Checklist' },
  ];
  const strengths = dims.filter((d) => d.score >= 80).map((d) => d.label);
  const weak = dims.filter((d) => d.score < 80).sort((a, b) => a.score - b.score).map((d) => d.label);
  const proposals: string[] = [];
  if (!ev.hasPosBasic) proposals.push('Ưu tiên hoàn thành bán hàng tại quầy cơ bản');
  if (weak.includes('Chăm sóc khách')) proposals.push('Theo dõi chăm sóc khách / gắn điểm 30 ngày');
  if (weak.includes('Checklist')) proposals.push('Nhắc checklist đóng ca mỗi ngày');
  if (weak.includes('Kho / hết hạn')) proposals.push('Ôn lại bài xuất hàng gần hết hạn / kiểm hạn dùng');
  if (ev.nextLevelTitle && !ev.eligibleForNext) {
    proposals.push(`Bổ sung điều kiện lên ${ev.nextLevelTitle}`);
  } else if (ev.nextLevelTitle && ev.eligibleForNext) {
    proposals.push(`Xem xét duyệt lên ${ev.nextLevelTitle}`);
  }
  if (!proposals.length) proposals.push('Giữ nhịp hiện tại · ghi nhận điểm mạnh');
  return {
    strengths: strengths.length ? strengths : ['Đang tích lũy dữ liệu'],
    weak: weak.length ? weak.slice(0, 3) : [],
    proposals: proposals.slice(0, 3),
    overall: Math.round(dims.reduce((s, d) => s + d.score, 0) / dims.length),
  };
}

function buildAiComment(ev: LearningEmployeeEvidence, summary: ReturnType<typeof buildAiSummary>) {
  const strong = summary.strengths.filter((s) => s !== 'Đang tích lũy dữ liệu').slice(0, 2);
  const weak = summary.weak.slice(0, 2);
  const parts: string[] = [];
  if (strong.length) {
    parts.push(`Thực hiện tốt: ${strong.join(', ')}.`);
  }
  if (weak.length) {
    parts.push(`Cần cải thiện: ${weak.join(', ')}.`);
  }
  parts.push(humanizeMissingReason(ev.suggestionNote));
  if (summary.proposals[0]) {
    parts.push(`Đề xuất tháng sau: ${summary.proposals[0]}.`);
  }
  return parts.join(' ').slice(0, 1000);
}

export function LearningEvaluationsPage() {
  const { message } = App.useApp();
  const canWrite = useCanLearningWrite();
  const [form] = Form.useForm();
  const [rows, setRows] = useState<LearningEvaluation[]>([]);
  const [employees, setEmployees] = useState<EmployeeLookup[]>([]);
  const [roster, setRoster] = useState<LearningCareerRosterItem[]>([]);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [evidence, setEvidence] = useState<LearningEmployeeEvidence | null>(null);
  const [evidenceLoading, setEvidenceLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [evalStarted, setEvalStarted] = useState(false);
  const [usedSuggestion, setUsedSuggestion] = useState(false);
  const [smartGoals, setSmartGoals] = useState<string[]>([]);
  const [employeeFilter, setEmployeeFilter] = useState('');
  const [detail, setDetail] = useState<LearningEvaluation | null>(null);

  const reload = async () => {
    if (!canWrite) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const [list, emp, rost] = await Promise.all([
        fetchLearningEvaluations(year, month),
        fetchEmployees(),
        fetchCareerRoster().catch(() => [] as LearningCareerRosterItem[]),
      ]);
      setRows(list);
      setEmployees(emp);
      setRoster(rost);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải đánh giá'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canWrite, year, month]);

  const loadEvidence = async (employeeId: string) => {
    setUsedSuggestion(false);
    setEvalStarted(false);
    setSmartGoals([]);
    form.setFieldsValue({
      scoreKnowledge: undefined,
      scoreAttitude: undefined,
      scoreCare: undefined,
      scoreStock: undefined,
      scoreDiscipline: undefined,
      comment: undefined,
    });
    setSelectedId(employeeId);
    setEvidenceLoading(true);
    try {
      const ev = await fetchEmployeeEvidence(employeeId);
      setEvidence(ev);
    } catch (e) {
      setEvidence(null);
      message.error(apiErrorMessage(e, 'Không tải bằng chứng'));
    } finally {
      setEvidenceLoading(false);
    }
  };

  const aiSummary = useMemo(
    () => (evidence ? buildAiSummary(evidence) : null),
    [evidence],
  );

  const suggestionSnapshot = useMemo(() => {
    if (!evidence) return null;
    return {
      scoreKnowledge: snapToBand(evidence.suggestedKnowledge),
      scoreAttitude: snapToBand(evidence.suggestedAttitude),
      scoreCare: snapToBand(evidence.suggestedCare),
      scoreStock: snapToBand(evidence.suggestedStock),
      scoreDiscipline: snapToBand(evidence.suggestedDiscipline),
    };
  }, [evidence]);

  const applyAiScores = () => {
    if (!evidence || !aiSummary) return;
    const comment = buildAiComment(evidence, aiSummary);
    form.setFieldsValue({
      ...suggestionSnapshot,
      comment,
    });
    setUsedSuggestion(true);
    setEvalStarted(true);
    message.success(
      'Đã điền sẵn 5 mức điểm + nhận xét theo bằng chứng ca. Cuộn xuống chỉnh nếu cần, rồi bấm Lưu đánh giá.',
    );
  };

  const applyAiCommentOnly = () => {
    if (!evidence || !aiSummary) return;
    form.setFieldsValue({ comment: buildAiComment(evidence, aiSummary) });
    setEvalStarted(true);
    message.success(
      'Đã điền sẵn ô nhận xét. Bạn vẫn tự chọn mức điểm bên dưới, rồi Lưu đánh giá.',
    );
  };

  const employeeCards = useMemo(() => {
    const q = employeeFilter.trim().toLowerCase();
    const evaluatedIds = new Set(rows.map((r) => r.employeeId));
    const seen = new Set<string>();
    return employees
      .filter((e) => {
        if (seen.has(e.id)) return false;
        seen.add(e.id);
        return !q || e.fullName.toLowerCase().includes(q);
      })
      .map((e) => {
        const r = roster.find((x) => x.employeeId === e.id);
        const prior = rows.find((x) => x.employeeId === e.id);
        return {
          id: e.id,
          name: e.fullName,
          level: r?.currentLevelTitle ?? r?.currentLevelCode ?? '—',
          levelCode: r?.currentLevelCode ?? '',
          score: prior?.averageScore ?? r?.latestAvgEvaluate ?? null,
          evaluated: evaluatedIds.has(e.id),
          eligible: r?.eligibleForNext ?? false,
        };
      })
      .sort((a, b) => Number(a.evaluated) - Number(b.evaluated) || a.name.localeCompare(b.name, 'vi'));
  }, [employees, roster, rows, employeeFilter]);

  if (!canWrite) {
    return (
      <Typography.Text type="secondary">
        Trang này dành cho chủ / quản lý. Bạn có thể học bài và phản hồi đánh giá của chính mình ở «Học
        bài».
      </Typography.Text>
    );
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          gap: 12,
          flexWrap: 'wrap',
          alignItems: 'flex-start',
        }}
      >
        <div>
          <Space align="center" size={10} style={{ marginBottom: 4 }}>
            <UserOutlined style={{ fontSize: 24, color: '#1677ff' }} />
            <Typography.Title level={3} style={{ margin: 0, color: '#1d39c4' }}>
              Đánh giá tháng
            </Typography.Title>
          </Space>
          <PeoplePageHint>
            Quan sát bằng chứng → gợi ý thông minh tổng hợp → bạn xác nhận mức thang → trao đổi với
            nhân viên → thống nhất mục tiêu. Không chấm số cảm tính (55 / 70 / 80 / 90).
          </PeoplePageHint>
        </div>
      </div>

      <Alert
        type="info"
        showIcon
        style={{ borderRadius: 10, border: '1px solid #91caff', background: '#e6f4ff' }}
        message={
          <Typography.Text>
            <Typography.Text strong>Chuẩn hóa thang điểm.</Typography.Text>{' '}
            55 Chưa đạt · 70 Đạt · 80 Tốt · 90 Xuất sắc. Giữ lâu dài — không nhập 81–85 tùy hứng.
          </Typography.Text>
        }
      />

      <Card
        size="small"
        styles={{ body: { padding: '12px 16px' } }}
        style={{ borderRadius: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.04)' }}
      >
        <Space wrap align="center" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space wrap align="center">
            <CalendarOutlined style={{ color: '#1677ff' }} />
            <Typography.Text strong>Kỳ đánh giá:</Typography.Text>
            <Select
              style={{ width: 100 }}
              value={year}
              options={Array.from({ length: 8 }, (_, i) => {
                const y = new Date().getFullYear() - 2 + i;
                return { value: y, label: String(y) };
              })}
              onChange={(v) => setYear(v)}
            />
            <Select
              style={{ width: 130 }}
              value={month}
              options={MONTH_OPTIONS}
              onChange={(v) => setMonth(v)}
            />
          </Space>
          <Button
            type="link"
            icon={<QuestionCircleOutlined />}
            onClick={() =>
              Modal.info({
                title: 'Hướng dẫn đánh giá',
                width: 520,
                content: (
                  <Space direction="vertical" size={8}>
                    <Typography.Text>
                      1. Chọn nhân viên → xem bằng chứng tháng này.
                    </Typography.Text>
                    <Typography.Text>
                      2. Dùng gợi ý thông minh (nếu có) rồi xác nhận mức 55 / 70 / 80 / 90.
                    </Typography.Text>
                    <Typography.Text>
                      3. Lưu → nhân viên trao đổi hai chiều và cam kết tháng sau.
                    </Typography.Text>
                    <Typography.Text type="secondary">
                      Không chấm số cảm tính ngoài thang chuẩn.
                    </Typography.Text>
                  </Space>
                ),
              })
            }
          >
            Hướng dẫn đánh giá
          </Button>
        </Space>
      </Card>

      <Card
        title={<Typography.Text strong style={{ fontSize: 15 }}>1. Chọn nhân viên</Typography.Text>}
        size="small"
        style={{ borderRadius: 12 }}
        extra={
          <Input.Search
            allowClear
            placeholder="Tìm nhân viên…"
            style={{ width: 220 }}
            onSearch={setEmployeeFilter}
            onChange={(e) => setEmployeeFilter(e.target.value)}
          />
        }
      >
        <Row gutter={[12, 12]}>
          {employeeCards.map((c) => {
            const selected = c.id === selectedId;
            return (
              <Col xs={24} sm={12} md={8} lg={6} key={c.id}>
                <button
                  type="button"
                  onClick={() => void loadEvidence(c.id)}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 10,
                    width: '100%',
                    height: '100%',
                    minHeight: 148,
                    textAlign: 'left',
                    padding: '14px',
                    borderRadius: 12,
                    border: selected ? '2px solid #1677ff' : '1px solid #f0f0f0',
                    background: '#fff',
                    boxShadow: selected ? '0 0 0 2px #1677ff22' : '0 1px 2px rgba(0,0,0,0.03)',
                    cursor: 'pointer',
                    boxSizing: 'border-box',
                    overflow: 'hidden',
                  }}
                >
                  <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', minWidth: 0 }}>
                    <Avatar
                      size={42}
                      style={{ background: selected ? '#1677ff' : '#722ed1', flexShrink: 0 }}
                    >
                      {initials(c.name)}
                    </Avatar>
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <Typography.Text
                        strong
                        ellipsis={{ tooltip: c.name }}
                        style={{ display: 'block', lineHeight: 1.3 }}
                      >
                        {c.name}
                      </Typography.Text>
                      <Typography.Text
                        type="secondary"
                        ellipsis
                        style={{ display: 'block', fontSize: 12, marginTop: 2 }}
                      >
                        {c.level}
                      </Typography.Text>
                    </div>
                  </div>

                  <div>
                    <Rate
                      disabled
                      allowHalf
                      value={scoreToStars(c.score)}
                      style={{ fontSize: 13, lineHeight: 1, color: '#faad14' }}
                    />
                    <div style={{ marginTop: 4 }}>
                      {c.score != null ? (
                        <Typography.Text style={{ fontSize: 13 }}>
                          {c.score} · {competencyLabel(c.score)}
                        </Typography.Text>
                      ) : (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Chưa có điểm
                        </Typography.Text>
                      )}
                    </div>
                  </div>

                  <div style={{ marginTop: 'auto' }}>
                    {c.evaluated ? (
                      <Button size="small" type="default" icon={<CheckCircleOutlined />}>
                        Đã chấm
                      </Button>
                    ) : (
                      <Button size="small">Chưa chấm</Button>
                    )}
                  </div>
                </button>
              </Col>
            );
          })}
        </Row>
      </Card>

      {/* Hồ sơ đánh giá — bức tranh tổng thể trước */}
      {!evidence && !evidenceLoading ? (
        <Card
          size="small"
          hoverable
          style={{ borderRadius: 12, cursor: selectedId ? 'pointer' : 'default' }}
          styles={{ body: { padding: '14px 18px' } }}
          onClick={() => {
            if (selectedId) void loadEvidence(selectedId);
          }}
        >
          <Space style={{ width: '100%', justifyContent: 'space-between' }} align="center">
            <Space align="start" size={12}>
              <UserOutlined style={{ fontSize: 20, color: '#1677ff', marginTop: 2 }} />
              <div>
                <Typography.Text strong style={{ fontSize: 15 }}>
                  2. Hồ sơ đánh giá
                </Typography.Text>
                <div>
                  <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                    Chọn nhân viên để xem bằng chứng
                  </Typography.Text>
                </div>
              </div>
            </Space>
            <RightOutlined style={{ color: '#1677ff' }} />
          </Space>
        </Card>
      ) : (
      <Card
        title={
          <Space>
            <UserOutlined style={{ color: '#1677ff' }} />
            2. Hồ sơ đánh giá
          </Space>
        }
        size="small"
        loading={evidenceLoading}
        style={{ borderRadius: 12 }}
      >
        {!evidence ? (
          <Typography.Text type="secondary">Chọn nhân viên để xem bằng chứng.</Typography.Text>
        ) : (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div
              style={{
                padding: 16,
                borderRadius: 12,
                background: 'linear-gradient(135deg, #1677ff0c 0%, #722ed10a 100%)',
                border: '1px solid #1677ff22',
              }}
            >
              <Space align="start" size={16}>
                <Avatar size={56} style={{ background: '#1677ff', fontSize: 20 }}>
                  {initials(evidence.employeeName)}
                </Avatar>
                <div>
                  <Typography.Title level={4} style={{ margin: 0 }}>
                    {evidence.employeeName}
                  </Typography.Title>
                  <Space wrap size={8} style={{ marginTop: 4 }}>
                    <Tag color="purple">
                      Bậc: {evidence.currentLevelTitle ?? '—'}
                    </Tag>
                    <Tag color="blue">
                      Điểm năng lực gợi ý: {aiSummary?.overall ?? '—'} (
                      {aiSummary ? competencyLabel(aiSummary.overall) : '—'})
                    </Tag>
                    {evidence.eligibleForNext ? (
                      <Tag color="success">Đủ điều kiện lên {evidence.nextLevelTitle}</Tag>
                    ) : null}
                  </Space>
                  {aiSummary ? (
                    <Rate
                      disabled
                      allowHalf
                      value={scoreToStars(aiSummary.overall)}
                      style={{ marginTop: 4, fontSize: 16 }}
                    />
                  ) : null}
                </div>
              </Space>
            </div>

            {/* Evidence mini-dashboard */}
            <Typography.Text strong>Bằng chứng tháng này</Typography.Text>
            <Row gutter={[10, 10]}>
              {(
                [
                  {
                    label: 'Đào tạo',
                    value: `${pctTrain(evidence)}%`,
                    sub: `${evidence.modulesPassed}/${evidence.modulesTotal} bài`,
                    pct: pctTrain(evidence),
                    color: '#1677ff',
                  },
                  {
                    label: 'Checklist',
                    value: `${pctChecklist(evidence)}%`,
                    sub: `${evidence.closeChecklistDaysThisMonth ?? 0} ngày · chuỗi ${evidence.closeStreakDays ?? 0}`,
                    pct: pctChecklist(evidence),
                    color: '#faad14',
                  },
                  {
                    label: 'Chăm sóc khách',
                    value: `${pctCrm(evidence)}`,
                    sub: competencyLabel(pctCrm(evidence)),
                    pct: pctCrm(evidence),
                    color: '#eb2f96',
                  },
                  {
                    label: 'Đi làm / đóng ca',
                    value: `${evidence.closeChecklistDaysThisMonth ?? 0}`,
                    sub: 'ngày có đóng ca',
                    pct: Math.min(100, (evidence.closeChecklistDaysThisMonth ?? 0) * 4),
                    color: '#52c41a',
                  },
                  {
                    label: 'Bán hàng',
                    value: `${evidence.orderCountThisMonth ?? 0}`,
                    sub:
                      evidence.salesNetThisMonth != null
                        ? `${Math.round(Number(evidence.salesNetThisMonth)).toLocaleString('vi-VN')}₫`
                        : 'đơn tháng',
                    pct: Math.min(100, (evidence.orderCountThisMonth ?? 0) * 5),
                    color: '#13c2c2',
                  },
                  {
                    label: 'Cảm nhận gần nhất',
                    value:
                      evidence.latestEngagementPulse != null
                        ? `${evidence.latestEngagementPulse}/5`
                        : '—',
                    sub:
                      evidence.latestEngagementPulse != null
                        ? evidence.latestEngagementPulse >= 4
                          ? '😊 Ổn'
                          : evidence.latestEngagementPulse >= 3
                            ? '😐 Bình thường'
                            : '☹ Cần quan tâm'
                        : 'Chưa có',
                    pct: (evidence.latestEngagementPulse ?? 0) * 20,
                    color: '#ff4d4f',
                  },
                ] as const
              ).map((tile) => (
                <Col xs={12} sm={8} md={4} key={tile.label}>
                  <Card size="small" styles={{ body: { padding: 12 } }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {tile.label}
                    </Typography.Text>
                    <Typography.Title level={3} style={{ margin: '4px 0', color: tile.color }}>
                      {tile.value}
                    </Typography.Title>
                    <Progress percent={tile.pct} showInfo={false} strokeColor={tile.color} size="small" />
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                      {tile.sub}
                    </Typography.Text>
                  </Card>
                </Col>
              ))}
            </Row>

            {evidence.careerMissingReasons?.length ? (
              <Typography.Text type="secondary">
                Thiếu để lên bậc:{' '}
                {evidence.careerMissingReasons.map(humanizeMissingReason).join('; ')}
              </Typography.Text>
            ) : null}

            {/* AI Evaluation Assistant */}
            {aiSummary ? (
              <Card
                size="small"
                title={
                  <Space>
                    <RobotOutlined style={{ color: '#722ed1' }} />
                    Gợi ý thông minh (từ bằng chứng ca)
                  </Space>
                }
                style={{ borderColor: '#722ed133' }}
              >
                <Row gutter={[16, 12]}>
                  <Col xs={24} md={8}>
                    <Typography.Text type="secondary">Điểm mạnh</Typography.Text>
                    {aiSummary.strengths.map((s) => (
                      <div key={s}>
                        <Typography.Text style={{ color: '#52c41a' }}>✓ {s}</Typography.Text>
                      </div>
                    ))}
                  </Col>
                  <Col xs={24} md={8}>
                    <Typography.Text type="secondary">Điểm cần cải thiện</Typography.Text>
                    {aiSummary.weak.length ? (
                      aiSummary.weak.map((s) => (
                        <div key={s}>
                          <Typography.Text style={{ color: '#faad14' }}>• {s}</Typography.Text>
                        </div>
                      ))
                    ) : (
                      <Typography.Text type="secondary">Không điểm yếu rõ</Typography.Text>
                    )}
                  </Col>
                  <Col xs={24} md={8}>
                    <Typography.Text type="secondary">Đề xuất</Typography.Text>
                    {aiSummary.proposals.map((s) => (
                      <div key={s}>
                        <Typography.Text>• {s}</Typography.Text>
                      </div>
                    ))}
                  </Col>
                </Row>
                <Space wrap style={{ marginTop: 12 }}>
                  <Button type="primary" icon={<CheckCircleOutlined />} onClick={applyAiScores}>
                    Dùng gợi ý: điền điểm + nhận xét
                  </Button>
                  <Button onClick={applyAiCommentOnly}>Chỉ điền nhận xét (tự chấm điểm)</Button>
                </Space>
                <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
                  Chỉ điền sẵn vào form bên dưới — chưa lưu. Bạn vẫn chỉnh rồi mới bấm Lưu đánh giá.
                </Typography.Paragraph>
              </Card>
            ) : null}

            {!evalStarted ? (
              <Button
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={() => setEvalStarted(true)}
              >
                Bắt đầu đánh giá
              </Button>
            ) : null}
          </Space>
        )}
      </Card>
      )}

      {/* Form chấm — chỉ sau khi bắt đầu */}
      {evidence && evalStarted ? (
        <Card title="3. Chọn mức theo tiêu chí" size="small" style={{ borderRadius: 12 }}>
          <Form
            form={form}
            layout="vertical"
            onFinish={async (v) => {
              const scores = [
                v.scoreKnowledge,
                v.scoreAttitude,
                v.scoreCare,
                v.scoreStock,
                v.scoreDiscipline,
              ];
              if (scores.some((s) => s == null)) {
                message.warning('Chọn đủ 5 mức thang');
                return;
              }
              let comment = (v.comment as string | undefined)?.trim() ?? '';
              if (smartGoals.length) {
                const goalText = SMART_GOALS.filter((g) => smartGoals.includes(g.key))
                  .map((g) => g.label)
                  .join('; ');
                comment = `${comment}\n\nMục tiêu đề xuất tháng sau: ${goalText}`.trim();
              }
              if (comment.length < 20) {
                message.warning('Nhận xét tối thiểu 20 ký tự');
                return;
              }
              if (suggestionSnapshot) {
                const drifted = CRITERIA.some(
                  (c) => Math.abs(Number(v[c.name]) - suggestionSnapshot[c.name]) >= 20,
                );
                if (drifted && !comment.toLowerCase().includes('thực tế')) {
                  message.warning(
                    'Bạn chấm lệch gợi ý ≥20 điểm: trong nhận xét hãy ghi rõ «thực tế ca…»',
                  );
                  return;
                }
              }
              setSaving(true);
              try {
                await upsertLearningEvaluation({
                  employeeId: selectedId!,
                  periodYear: year,
                  periodMonth: month,
                  scoreKnowledge: v.scoreKnowledge,
                  scoreAttitude: v.scoreAttitude,
                  scoreCare: v.scoreCare,
                  scoreStock: v.scoreStock,
                  scoreDiscipline: v.scoreDiscipline,
                  comment,
                });
                message.success('Đã lưu đánh giá — chờ nhân viên trao đổi hai chiều');
                setEvalStarted(false);
                setUsedSuggestion(false);
                setSmartGoals([]);
                await reload();
              } catch (e) {
                message.error(apiErrorMessage(e, 'Lưu thất bại'));
              } finally {
                setSaving(false);
              }
            }}
          >
            <Row gutter={[12, 12]}>
              {CRITERIA.map((c) => (
                <Col xs={24} lg={12} key={c.name}>
                  <Card
                    size="small"
                    styles={{
                      body: { borderLeft: `4px solid ${c.tone}` },
                    }}
                  >
                    <Typography.Text strong style={{ color: c.tone }}>
                      {c.label}
                    </Typography.Text>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                      {c.ask}
                    </Typography.Paragraph>
                    <Form.Item
                      name={c.name}
                      rules={[{ required: true, message: 'Chọn mức' }]}
                      style={{ marginBottom: 0 }}
                    >
                      <Radio.Group style={{ width: '100%' }}>
                        <Space direction="vertical" style={{ width: '100%' }}>
                          {SCORE_BANDS.map((b) => (
                            <Radio key={b.value} value={b.value} style={{ width: '100%' }}>
                              <span style={{ color: b.color, fontWeight: 600 }}>
                                {b.short} {b.label}
                              </span>
                              <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                                {b.hint}
                              </Typography.Text>
                            </Radio>
                          ))}
                        </Space>
                      </Radio.Group>
                    </Form.Item>
                  </Card>
                </Col>
              ))}
            </Row>

            <Card
              size="small"
              style={{ marginTop: 16 }}
              title={
                <Space>
                  <RobotOutlined />
                  Nhận xét quản lý
                </Space>
              }
              extra={
                <Button type="link" size="small" onClick={applyAiCommentOnly}>
                  AI gợi ý lại
                </Button>
              }
            >
              <Form.Item
                name="comment"
                extra={
                  usedSuggestion
                    ? 'Đã dùng gợi ý từ bằng chứng — chỉnh nếu thực tế ca khác.'
                    : 'Có thể bấm AI gợi ý rồi sửa.'
                }
                rules={[
                  { required: true, message: 'Bắt buộc nhận xét' },
                  { min: 20, message: 'Tối thiểu 20 ký tự' },
                ]}
                style={{ marginBottom: 12 }}
              >
                <Input.TextArea
                  rows={4}
                  maxLength={1000}
                  showCount
                  placeholder="Vd: Thực hiện tốt quy trình xuất hàng gần hết hạn trước. Cần cải thiện kỹ năng chăm sóc khách."
                />
              </Form.Item>

              <Typography.Text strong>Mục tiêu đề xuất tháng sau (cụ thể · đo được)</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 8 }}>
                Chọn vài mục — sẽ gắn vào nhận xét để nhân viên cam kết khi trao đổi hai chiều.
              </Typography.Paragraph>
              <Checkbox.Group
                style={{ width: '100%' }}
                value={smartGoals}
                onChange={(v) => setSmartGoals(v as string[])}
              >
                <Row gutter={[8, 8]}>
                  {SMART_GOALS.map((g) => (
                    <Col xs={24} sm={12} key={g.key}>
                      <Checkbox value={g.key}>{g.label}</Checkbox>
                    </Col>
                  ))}
                </Row>
              </Checkbox.Group>
            </Card>

            <Alert
              style={{ marginTop: 16 }}
              type="success"
              showIcon
              message="Sau khi lưu: Trao đổi hai chiều"
              description="Nhân viên sẽ thấy nhận xét → phản hồi ý kiến → chọn cam kết tháng sau → cảm nhận công việc (vài giây). Đó là phát triển nhân viên, không chỉ chấm điểm."
            />

            <Button
              type="primary"
              htmlType="submit"
              loading={saving}
              size="large"
              style={{ marginTop: 16 }}
              block
            >
              Lưu đánh giá tháng
            </Button>
          </Form>
        </Card>
      ) : null}

      <Card
        title={
          <Space>
            <CalendarOutlined style={{ color: '#1677ff' }} />
            <span>Lịch sử kỳ {month}/{year}</span>
          </Space>
        }
        style={{ borderRadius: 12 }}
      >
        <Table
          rowKey="id"
          loading={loading}
          dataSource={rows}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          scroll={{ x: 780 }}
          locale={{
            emptyText: (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="Chưa có đánh giá trong kỳ này"
              />
            ),
          }}
          onRow={(r) => ({
            onClick: () => setDetail(r),
            style: { cursor: 'pointer' },
          })}
          columns={[
            {
              title: 'Nhân viên',
              dataIndex: 'employeeName',
              fixed: 'left',
              width: 160,
              render: (name: string) => <Typography.Text strong>{name}</Typography.Text>,
            },
            {
              title: 'Điểm năng lực',
              dataIndex: 'averageScore',
              width: 150,
              render: (v: number) => (
                <Space direction="vertical" size={0}>
                  <Typography.Text strong>
                    {v} · {competencyLabel(v)}
                  </Typography.Text>
                  <Rate disabled allowHalf value={scoreToStars(v)} style={{ fontSize: 12 }} />
                </Space>
              ),
            },
            {
              title: 'Trạng thái trao đổi',
              key: 'dialogue',
              width: 150,
              render: (_, r) =>
                r.employeeRespondedAt ? (
                  <Tag color="success">Đã phản hồi</Tag>
                ) : (
                  <Tag color="orange">Chờ nhân viên</Tag>
                ),
            },
            {
              title: 'Cảm nhận',
              dataIndex: 'engagementPulse',
              width: 110,
              render: (v: number | null | undefined) => {
                if (v == null) return <Typography.Text type="secondary">Chưa có</Typography.Text>;
                if (v >= 4) return `😊 ${v}/5`;
                if (v >= 3) return `😐 ${v}/5`;
                return `☹ ${v}/5`;
              },
            },
            {
              title: 'Ngày chấm',
              dataIndex: 'reviewedAt',
              width: 120,
              render: (v: string) =>
                new Date(v).toLocaleDateString('vi-VN', {
                  day: '2-digit',
                  month: '2-digit',
                  year: 'numeric',
                }),
            },
            {
              title: 'Thao tác',
              key: 'actions',
              fixed: 'right',
              width: 140,
              render: (_, r) => (
                <Button
                  size="small"
                  icon={<EyeOutlined />}
                  onClick={(e) => {
                    e.stopPropagation();
                    setDetail(r);
                  }}
                >
                  Xem chi tiết
                </Button>
              ),
            },
          ]}
        />
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
          Bấm hàng hoặc «Xem chi tiết» để đọc đủ nhận xét, phản hồi và cam kết tháng sau.
        </Typography.Paragraph>
      </Card>

      <Drawer
        title={detail ? `Chi tiết đánh giá — ${detail.employeeName}` : 'Chi tiết đánh giá'}
        open={Boolean(detail)}
        onClose={() => setDetail(null)}
        width={Math.min(560, typeof window !== 'undefined' ? window.innerWidth - 24 : 560)}
        destroyOnClose
      >
        {detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary">
                Kỳ {detail.periodMonth}/{detail.periodYear} · Chấm ngày{' '}
                {new Date(detail.reviewedAt).toLocaleString('vi-VN')}
              </Typography.Text>
              <div style={{ marginTop: 8 }}>
                <Typography.Title level={4} style={{ margin: 0 }}>
                  {detail.averageScore} · {competencyLabel(detail.averageScore)}
                </Typography.Title>
                <Rate
                  disabled
                  allowHalf
                  value={scoreToStars(detail.averageScore)}
                  style={{ fontSize: 16 }}
                />
              </div>
            </div>

            <Card size="small" title="Thang điểm từng tiêu chí">
              <Descriptions column={1} size="small">
                <Descriptions.Item label="Kiến thức">
                  {bandLabel(detail.scoreKnowledge)}
                </Descriptions.Item>
                <Descriptions.Item label="Thái độ">
                  {bandLabel(detail.scoreAttitude)}
                </Descriptions.Item>
                <Descriptions.Item label="Chăm sóc khách">
                  {bandLabel(detail.scoreCare)}
                </Descriptions.Item>
                <Descriptions.Item label="Kho / hết hạn">
                  {bandLabel(detail.scoreStock)}
                </Descriptions.Item>
                <Descriptions.Item label="Kỷ luật ca">
                  {bandLabel(detail.scoreDiscipline)}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="Nhận xét quản lý">
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                {detail.comment?.trim() || (
                  <Typography.Text type="secondary">Chưa có nhận xét</Typography.Text>
                )}
              </Typography.Paragraph>
            </Card>

            <Card
              size="small"
              title="Trao đổi hai chiều"
              extra={
                detail.employeeRespondedAt ? (
                  <Tag color="success">Đã phản hồi</Tag>
                ) : (
                  <Tag color="orange">Chờ nhân viên</Tag>
                )
              }
            >
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                {detail.employeeRespondedAt
                  ? detail.employeeFeedback?.trim() || 'Đã phản hồi (không ghi nội dung)'
                  : 'Nhân viên chưa phản hồi.'}
              </Typography.Paragraph>
              {detail.employeeRespondedAt ? (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Phản hồi lúc {new Date(detail.employeeRespondedAt).toLocaleString('vi-VN')}
                </Typography.Text>
              ) : null}
            </Card>

            <Card size="small" title="Cam kết tháng sau">
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                {detail.nextMonthGoal?.trim() || (
                  <Typography.Text type="secondary">Chưa có cam kết</Typography.Text>
                )}
              </Typography.Paragraph>
            </Card>

            <Card size="small" title="Cảm nhận công việc">
              {detail.engagementPulse == null ? (
                <Typography.Text type="secondary">Chưa chọn</Typography.Text>
              ) : detail.engagementPulse >= 4 ? (
                <Typography.Text>😊 Rất tốt ({detail.engagementPulse}/5)</Typography.Text>
              ) : detail.engagementPulse >= 3 ? (
                <Typography.Text>😐 Bình thường ({detail.engagementPulse}/5)</Typography.Text>
              ) : (
                <Typography.Text>☹ Khó khăn ({detail.engagementPulse}/5)</Typography.Text>
              )}
            </Card>

            <Button
              type="primary"
              block
              onClick={() => {
                const id = detail.employeeId;
                setDetail(null);
                void loadEvidence(id);
                window.scrollTo({ top: 0, behavior: 'smooth' });
              }}
            >
              Đánh giá lại nhân viên này
            </Button>
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
