import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  App,
  Button,
  Card,
  Col,
  Collapse,
  Drawer,
  Form,
  Input,
  Modal,
  Pagination,
  Progress,
  Row,
  Segmented,
  Select,
  Space,
  Tabs,
  Tag,
  Timeline,
  Typography,
  Rate,
} from 'antd';
import {
  HistoryOutlined,
  PlusOutlined,
  ReadOutlined,
  RiseOutlined,
  RobotOutlined,
  StarOutlined,
  TrophyOutlined,
  AimOutlined,
  CommentOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createLearningRecognition,
  fetchLearningRecognitions,
  fetchMyLearning,
  fetchMyLearningBadges,
  fetchRecentCustomerFeedback,
  type LearningBadge,
  type LearningCustomerFeedback,
  type LearningModuleProgress,
  type LearningRecognition,
} from '@/shared/api/learning.api';
import { fetchEmployees } from '@/shared/api/identity-admin.api';
import type { EmployeeLookup } from '@/shared/api/identity-admin.types';
import { useCanLearningWrite } from '@/shared/auth/usePermission';
import { PeoplePageHint } from '@/modules/learning/PeopleModuleIntro';
import { CompetencyProfilePanel } from '@/modules/learning/CompetencyProfilePanel';
import { NovixaPeopleCycle } from '@/modules/learning/NovixaPeopleCycle';
import {
  classifyFeedChannel,
  daysAgo,
  humanizeRecognitionTitle,
  inCurrentMonth,
  inPreviousMonth,
  recognitionIcon,
  recognitionSource,
  parseCustomerPraiseRating,
  customerPraiseComment,
  isCustomerFeedbackKind,
  type FeedChannel,
} from '@/modules/learning/recognition-display';
function CustomerStars({ rating, size = 16 }: { rating: number; size?: number }) {
  return (
    <Space size={4} align="center">
      <Rate disabled value={rating} style={{ fontSize: size, color: '#faad14' }} />
      <Typography.Text strong style={{ color: '#d48806', fontSize: Math.max(12, size - 2) }}>
        {rating}/5
      </Typography.Text>
    </Space>
  );
}

type QuickAward = {
  key: string;
  kind: string;
  title: string;
  body: string;
  badgeCode: string | null;
  badgeTitle: string;
  emoji: string;
  /** khen = động viên; coach = góp ý / hỗ trợ cải thiện */
  tone: 'praise' | 'coach';
  /** Mở modal chọn sao khách (praise ≥4 / feedback ≤3) */
  needsStars?: boolean;
  /** Mở modal nhập nội dung góp ý */
  needsNote?: boolean;
};

/** Ghi nhận nhanh — khen + góp ý (không phải nhật ký phạt). */
const QUICK_AWARDS: QuickAward[] = [
  {
    key: 'customer_praise',
    kind: 'customer_praise',
    title: 'Khách hàng khen',
    body: 'Được khách khen thái độ / tư vấn tại quầy. (+ động lực chăm sóc khách)',
    badgeCode: 'customer_praise',
    badgeTitle: 'Được khách hàng khen',
    emoji: '❤️',
    tone: 'praise',
    needsStars: true,
  },
  {
    key: 'mentor',
    kind: 'badge_award',
    title: 'Kèm đồng nghiệp',
    body: 'Hỗ trợ / kèm nhân viên mới trong ca.',
    badgeCode: 'mentor',
    badgeTitle: 'Kèm đồng nghiệp',
    emoji: '👏',
    tone: 'praise',
  },
  {
    key: 'zero_error',
    kind: 'badge_award',
    title: 'Không sai sót trong ca',
    body: 'Ca bán không lệch quỹ / không sự cố nghiêm trọng.',
    badgeCode: 'zero_error_shift',
    badgeTitle: 'Không sai sót',
    emoji: '🛡',
    tone: 'praise',
  },
  {
    key: 'crm_pro',
    kind: 'badge_award',
    title: 'Chuyên gia chăm sóc khách quầy',
    body: 'Gắn khách / điểm thưởng tốt, chăm sóc khách đúng mực.',
    badgeCode: 'complete_l2',
    badgeTitle: 'Chuyên gia chăm sóc khách',
    emoji: '⭐',
    tone: 'praise',
  },
  {
    key: 'tenure_12',
    kind: 'work_anniversary',
    title: '12 tháng gắn bó',
    body: 'Đủ 12 tháng đồng hành cùng nhà thuốc.',
    badgeCode: 'tenure_12m',
    badgeTitle: 'Gắn bó 1 năm',
    emoji: '📅',
    tone: 'praise',
  },
  {
    key: 'close_streak_7',
    kind: 'badge_award',
    title: '7 ngày đóng ca liên tục',
    body: 'Giữ nhịp checklist đóng ca 7 ngày liên tiếp.',
    badgeCode: 'close_streak_7',
    badgeTitle: '7 ngày đóng ca',
    emoji: '✅',
    tone: 'praise',
  },
  {
    key: 'customer_feedback',
    kind: 'customer_feedback',
    title: 'Khách góp ý',
    body: 'Khách góp ý cần cải thiện tại quầy — hỗ trợ NV kịp thời.',
    badgeCode: null,
    badgeTitle: 'Khách góp ý',
    emoji: '💬',
    tone: 'coach',
    needsStars: true,
  },
  {
    key: 'coach_attitude',
    kind: 'custom',
    title: 'Góp ý — thái độ / giao tiếp',
    body: 'Nhắc nhẹ về thái độ hoặc giao tiếp với khách tại quầy. Mục tiêu: cải thiện, không phạt.',
    badgeCode: null,
    badgeTitle: 'Thái độ / giao tiếp',
    emoji: '🗣️',
    tone: 'coach',
    needsNote: true,
  },
  {
    key: 'coach_process',
    kind: 'custom',
    title: 'Góp ý — quy trình / checklist',
    body: 'Nhắc lại bước làm đúng (POS, checklist, FEFO…). Mục tiêu: làm đúng hơn ca sau.',
    badgeCode: null,
    badgeTitle: 'Quy trình / checklist',
    emoji: '📋',
    tone: 'coach',
    needsNote: true,
  },
  {
    key: 'coach_general',
    kind: 'custom',
    title: 'Góp ý — đóng góp cải thiện',
    body: 'Góp ý cụ thể để NV cải thiện (viết rõ việc cần làm).',
    badgeCode: null,
    badgeTitle: 'Đóng góp cải thiện',
    emoji: '✍️',
    tone: 'coach',
    needsNote: true,
  },
];

const KIND_OPTIONS = [
  { value: 'custom', label: 'Ghi nhận / góp ý tùy chọn' },
  { value: 'customer_praise', label: 'Khách khen (≥4★)' },
  { value: 'customer_feedback', label: 'Khách góp ý (1–3★)' },
  { value: 'birthday', label: 'Sinh nhật' },
  { value: 'work_anniversary', label: 'Kỷ niệm công tác' },
  { value: 'badge_award', label: 'Trao thành tích' },
];

const MONTHLY_PRAISE_GOAL = 20;

function FeedItem({
  r,
  onPerson,
  hidePersonName,
}: {
  r: LearningRecognition;
  onPerson: (name: string, employeeId: string) => void;
  hidePersonName?: boolean;
}) {
  return (
    <div
      style={{
        padding: '10px 0',
        borderBottom: '1px solid #f0f0f0',
        display: 'flex',
        gap: 12,
      }}
    >
      <div style={{ fontSize: 22, lineHeight: 1 }}>{recognitionIcon(r)}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        {hidePersonName ? (
          <Typography.Text strong>{humanizeRecognitionTitle(r)}</Typography.Text>
        ) : (
          <>
            <button
              type="button"
              onClick={() => onPerson(r.employeeName, r.employeeId)}
              style={{
                border: 'none',
                background: 'none',
                padding: 0,
                cursor: 'pointer',
                fontWeight: 600,
                color: '#1677ff',
              }}
            >
              {r.employeeName}
            </button>
            <Typography.Text> — {humanizeRecognitionTitle(r)}</Typography.Text>
          </>
        )}
        {isCustomerFeedbackKind(r) ? (
          <div style={{ marginTop: 4 }}>
            {parseCustomerPraiseRating(r) != null ? (
              <CustomerStars rating={parseCustomerPraiseRating(r)!} size={15} />
            ) : (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Ghi nhận tay (chưa có sao từ app khách)
              </Typography.Text>
            )}
            {customerPraiseComment(r) ? (
              <Typography.Paragraph
                style={{
                  margin: '6px 0 0',
                  fontSize: 13,
                  fontStyle: 'italic',
                  color: '#434343',
                  background: '#fff0f6',
                  borderLeft: '3px solid #eb2f96',
                  padding: '6px 10px',
                  borderRadius: 4,
                }}
              >
                «{customerPraiseComment(r)}»
              </Typography.Paragraph>
            ) : null}
          </div>
        ) : r.body && !/complete_|perfect_/i.test(r.body) ? (
          <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
            {r.body
              .replace(/\bL(\d)\b/gi, 'Bậc $1')
              .replace(/\bĐạt 100%\b/gi, 'Đạt điểm tuyệt đối')}
          </Typography.Paragraph>
        ) : null}
        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
          {new Date(r.createdAt).toLocaleString('vi-VN')}
        </Typography.Text>
      </div>
    </div>
  );
}

function FeedChannelList({
  items,
  empty,
  onPerson,
  hidePersonName,
}: {
  items: LearningRecognition[];
  empty: string;
  onPerson: (name: string, employeeId: string) => void;
  hidePersonName?: boolean;
}) {
  const [page, setPage] = useState(1);
  const pageSize = 8;

  useEffect(() => {
    setPage(1);
  }, [items]);

  if (!items.length) {
    return <Typography.Text type="secondary">{empty}</Typography.Text>;
  }

  const start = (page - 1) * pageSize;
  const slice = items.slice(start, start + pageSize);

  return (
    <div>
      {slice.map((r) => (
        <FeedItem key={r.id} r={r} onPerson={onPerson} hidePersonName={hidePersonName} />
      ))}
      {items.length > pageSize ? (
        <div style={{ display: 'flex', justifyContent: 'center', marginTop: 12 }}>
          <Pagination
            size="small"
            current={page}
            pageSize={pageSize}
            total={items.length}
            onChange={setPage}
            showSizeChanger={false}
            showTotal={(total) => `${total} sự kiện`}
          />
        </div>
      ) : null}
    </div>
  );
}

export function LearningRecognizePage() {
  const { message } = App.useApp();
  const canWrite = useCanLearningWrite();
  const [feedScope, setFeedScope] = useState<'me' | 'team'>('me');
  const [rows, setRows] = useState<LearningRecognition[]>([]);
  const [myRows, setMyRows] = useState<LearningRecognition[]>([]);
  const [recentFeedback, setRecentFeedback] = useState<LearningCustomerFeedback[]>([]);
  const [employees, setEmployees] = useState<EmployeeLookup[]>([]);
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [myBadges, setMyBadges] = useState<LearningBadge[]>([]);
  const [myModules, setMyModules] = useState<LearningModuleProgress[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [quickBusy, setQuickBusy] = useState<string | null>(null);
  const [timeline, setTimeline] = useState<{
    name: string;
    employeeId: string;
    items: LearningRecognition[];
  } | null>(null);
  const [praiseModalOpen, setPraiseModalOpen] = useState(false);
  const [praiseStars, setPraiseStars] = useState(5);
  const [praiseNote, setPraiseNote] = useState('');
  const [starModalAward, setStarModalAward] = useState<QuickAward | null>(null);
  const [coachModalOpen, setCoachModalOpen] = useState(false);
  const [coachAward, setCoachAward] = useState<QuickAward | null>(null);
  const [coachNote, setCoachNote] = useState('');
  const [customKind, setCustomKind] = useState('custom');
  const [customStars, setCustomStars] = useState(5);

  const reload = async () => {
    setLoading(true);
    try {
      const tasks: Promise<unknown>[] = [
        fetchLearningRecognitions(80, { scope: 'me' }),
        fetchMyLearningBadges().catch(() => [] as LearningBadge[]),
        fetchMyLearning().catch(() => null),
      ];
      if (canWrite || feedScope === 'team') {
        tasks.push(fetchLearningRecognitions(80));
      }
      if (canWrite) {
        tasks.push(fetchEmployees());
        tasks.push(fetchRecentCustomerFeedback(48, 10).catch(() => [] as LearningCustomerFeedback[]));
      }
      const results = await Promise.all(tasks);
      const mineList = results[0] as LearningRecognition[];
      const badges = results[1] as LearningBadge[];
      const mineLearning = results[2] as { modules?: LearningModuleProgress[] } | null;
      setMyRows(mineList);
      setMyBadges(badges);
      setMyModules(mineLearning?.modules ?? []);

      let idx = 3;
      if (canWrite || feedScope === 'team') {
        setRows(results[idx] as LearningRecognition[]);
        idx += 1;
      } else {
        setRows(mineList);
      }
      if (canWrite) {
        if (results[idx]) setEmployees(results[idx] as EmployeeLookup[]);
        idx += 1;
        setRecentFeedback((results[idx] as LearningCustomerFeedback[]) ?? []);
      } else {
        setRecentFeedback([]);
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải bảng tin ghi nhận'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canWrite, feedScope]);

  const displayRows = feedScope === 'me' ? myRows : rows;

  const weekPraise = useMemo(
    () =>
      displayRows.filter(
        (r) => daysAgo(r.createdAt) <= 7 && recognitionSource(r) === 'customer',
      ),
    [displayRows],
  );

  const wallOfFame = useMemo(() => {
    if (feedScope !== 'team' || !canWrite) return null;
    const map = new Map<string, { name: string; id: string; count: number }>();
    for (const r of weekPraise) {
      const cur = map.get(r.employeeId) ?? {
        name: r.employeeName,
        id: r.employeeId,
        count: 0,
      };
      cur.count += 1;
      map.set(r.employeeId, cur);
    }
    return [...map.values()].sort((a, b) => b.count - a.count)[0] ?? null;
  }, [weekPraise, feedScope, canWrite]);

  const monthPraiseCount = useMemo(
    () =>
      displayRows.filter(
        (r) => inCurrentMonth(r.createdAt) && recognitionSource(r) === 'customer',
      ).length,
    [displayRows],
  );

  const recognitionBreakdown = useMemo(() => {
    const source = myRows;
    const last30 = source.filter((r) => daysAgo(r.createdAt) <= 30);
    return {
      customer: last30.filter((r) => recognitionSource(r) === 'customer').length,
      peer: last30.filter((r) => recognitionSource(r) === 'peer').length,
      manager: last30.filter((r) => recognitionSource(r) === 'manager').length,
    };
  }, [myRows]);

  const scoreTrend = useMemo(() => {
    const thisM = myRows.filter((r) => inCurrentMonth(r.createdAt)).length;
    const prevM = myRows.filter((r) => inPreviousMonth(r.createdAt)).length;
    return thisM - prevM;
  }, [myRows]);

  const feeds = useMemo(() => {
    const groups: Record<FeedChannel, LearningRecognition[]> = {
      achievement: [],
      learning: [],
      internal: [],
    };
    for (const r of displayRows) {
      groups[classifyFeedChannel(r)].push(r);
    }
    return groups;
  }, [displayRows]);

  const aiNextStep = useMemo(() => {
    if (feedScope === 'me') {
      if (recognitionBreakdown.customer === 0 && recognitionBreakdown.manager === 0) {
        return 'Chưa có ghi nhận gần đây — hoàn thành bài học hoặc nhờ quản lý ghi nhận trên ca.';
      }
      return 'Đây là bảng tin của bạn — không xếp hạng đồng nghiệp. Mỗi ghi nhận góp phần hồ sơ năng lực.';
    }
    if (wallOfFame && wallOfFame.count >= 3) {
      return `${wallOfFame.name} đang dẫn đầu lời khen tuần này — xem xét ghi nhận «Kèm đồng nghiệp» hoặc đề xuất lên bậc nếu đủ điều kiện.`;
    }
    if (monthPraiseCount >= MONTHLY_PRAISE_GOAL) {
      return 'Đã đạt mục tiêu khách khen tháng này — chuyển sang phát triển nghề cho người dẫn đầu.';
    }
    if (monthPraiseCount < MONTHLY_PRAISE_GOAL / 2) {
      return 'Tháng này còn ít lời khen khách — khuyến khích nhân viên chăm sóc khách / gắn điểm thưởng.';
    }
    return 'Mỗi ghi nhận cập nhật hồ sơ năng lực và góp phần phát triển nghề — giữ nhịp 1–2 lần/tuần.';
  }, [wallOfFame, monthPraiseCount, feedScope, recognitionBreakdown]);




  const quickAward = async (aw: QuickAward) => {
    if (!employeeId) {
      message.warning('Chọn nhân viên trước');
      return;
    }
    if (aw.needsStars) {
      setStarModalAward(aw);
      setPraiseStars(aw.tone === 'coach' ? 2 : 5);
      setPraiseNote('');
      setPraiseModalOpen(true);
      return;
    }
    if (aw.tone === 'coach' || aw.needsNote) {
      setCoachAward(aw);
      setCoachNote('');
      setCoachModalOpen(true);
      return;
    }
    setQuickBusy(aw.key);
    try {
      await createLearningRecognition({
        employeeId,
        kind: aw.kind,
        title: aw.title,
        body: aw.body || undefined,
        badgeCode: aw.badgeCode,
        isPublic: true,
        customerRating: null,
      });
      message.success('Đã tạo ghi nhận');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Ghi nhận thất bại'));
    } finally {
      setQuickBusy(null);
    }
  };

  const submitStarModal = async () => {
    if (!employeeId || !starModalAward) return;
    const aw = starModalAward;
    const stars = praiseStars;
    if (!stars || stars < 1 || stars > 5) {
      message.warning('Chọn số sao khách đánh giá (1–5)');
      return;
    }
    let kind = aw.kind;
    let badgeCode = aw.badgeCode;
    let finalTitle = aw.title;
    if (stars >= 4) {
      kind = 'customer_praise';
      badgeCode = 'customer_praise';
      finalTitle = finalTitle.includes('★') ? finalTitle : `Khách đánh giá ${stars}★`;
    } else {
      kind = 'customer_feedback';
      badgeCode = null;
      finalTitle = finalTitle.includes('★') ? finalTitle : `Khách đánh giá ${stars}★`;
    }
    setQuickBusy(aw.key);
    try {
      await createLearningRecognition({
        employeeId,
        kind,
        title: finalTitle,
        body: praiseNote.trim() || aw.body || undefined,
        badgeCode,
        isPublic: true,
        customerRating: stars,
      });
      message.success(stars >= 4 ? 'Đã ghi lời khen khách' : 'Đã ghi góp ý khách');
      setPraiseModalOpen(false);
      setStarModalAward(null);
      setPraiseNote('');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Ghi nhận thất bại'));
    } finally {
      setQuickBusy(null);
    }
  };

  const submitCoachModal = async () => {
    if (!employeeId || !coachAward) return;
    const note = coachNote.trim();
    if (coachAward.needsNote !== false && !note) {
      message.warning('Nhập nội dung góp ý cụ thể');
      return;
    }
    setQuickBusy(coachAward.key);
    try {
      const title = coachAward.title.startsWith('Góp ý')
        ? coachAward.title
        : `Góp ý — ${coachAward.title}`;
      await createLearningRecognition({
        employeeId,
        kind: coachAward.kind,
        title,
        body: note || coachAward.body || undefined,
        badgeCode: coachAward.badgeCode,
        isPublic: true,
        customerRating: null,
      });
      message.success('Đã ghi góp ý');
      setCoachModalOpen(false);
      setCoachAward(null);
      setCoachNote('');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Ghi góp ý thất bại'));
    } finally {
      setQuickBusy(null);
    }
  };

  const openTimeline = (name: string, employeeId: string) => {
    const catalog = rows.length ? rows : myRows;
    setTimeline({
      name,
      employeeId,
      items: catalog
        .filter((r) => r.employeeId === employeeId)
        .sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt)),
    });
  };

  const recentCustomerPraise = recentFeedback;

  const selectedEmp = employees.find((e) => e.id === employeeId);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {canWrite ? 'Ghi nhận & động lực' : 'Hồ sơ năng lực'}
        </Typography.Title>
        <PeoplePageHint>
          {canWrite
            ? 'Khen công khai tại đây. Góp ý sau khách ★ thấp → Hộp thư (nút Hỗ trợ thư riêng).'
            : 'Mọi lời khen và thành tích góp phần vào bậc nghề và điểm năng lực của bạn.'}
        </PeoplePageHint>
      </div>

      <Card
        styles={{
          body: {
            padding: '16px 20px',
            background: 'linear-gradient(180deg, #f7f9fc 0%, #fff 100%)',
          },
        }}
      >
        <NovixaPeopleCycle activeKey="rec" />
      </Card>

      <CompetencyProfilePanel
        badges={myBadges}
        modules={myModules}
        recognitionCount30d={
          recognitionBreakdown.customer + recognitionBreakdown.peer + recognitionBreakdown.manager
        }
        recognitionBreakdown={recognitionBreakdown}
        scoreTrend={scoreTrend}
        loading={loading}
        compactSummary={canWrite}
      />

      {canWrite && recentCustomerPraise.length > 0 ? (
        <Card
          size="small"
          style={{
            borderColor: '#ffadd2',
            background: 'linear-gradient(135deg, #fff0f6 0%, #fff 70%)',
          }}
          title={
            <Space>
              <span style={{ fontSize: 18 }}>❤️</span>
              <span>Khách vừa phản hồi — động viên kịp thời</span>
              <Tag color="magenta">48 giờ</Tag>
            </Space>
          }
        >
          <Space direction="vertical" style={{ width: '100%' }} size={10}>
            {recentCustomerPraise.map((fb) => (
              <div
                key={fb.id}
                style={{
                  display: 'flex',
                  gap: 12,
                  alignItems: 'flex-start',
                  paddingBottom: 8,
                  borderBottom: '1px solid #ffd6e7',
                }}
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Space wrap size={8}>
                    {fb.employeeId ? (
                      <Button
                        type="link"
                        style={{ padding: 0, height: 'auto', fontWeight: 600 }}
                        onClick={() => openTimeline(fb.employeeName, fb.employeeId!)}
                      >
                        {fb.employeeName}
                      </Button>
                    ) : (
                      <Typography.Text strong>{fb.employeeName}</Typography.Text>
                    )}
                    <CustomerStars rating={fb.rating} size={16} />
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {new Date(fb.createdAt).toLocaleString('vi-VN')}
                    </Typography.Text>
                    {fb.rating <= 3 ? <Tag color="orange">Cần chú ý</Tag> : null}
                  </Space>
                  {fb.comment?.trim() ? (
                    <Typography.Paragraph
                      style={{
                        margin: '4px 0 0',
                        fontSize: 13,
                        fontStyle: 'italic',
                      }}
                    >
                      «{fb.comment.trim()}»
                    </Typography.Paragraph>
                  ) : (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Khách đánh giá {fb.rating}★
                    </Typography.Text>
                  )}
                </div>
                {fb.employeeId ? (
                  <Button
                    size="small"
                    type="primary"
                    ghost
                    onClick={() => {
                      setEmployeeId(fb.employeeId!);
                      setFeedScope('team');
                      message.success(
                        fb.rating >= 4
                          ? `Đã chọn ${fb.employeeName} — động viên thêm bên dưới nếu muốn`
                          : `Đã chọn ${fb.employeeName} — góp ý / hỗ trợ kịp thời`,
                      );
                    }}
                  >
                    {fb.rating >= 4 ? 'Động viên' : 'Hỗ trợ'}
                  </Button>
                ) : null}
              </div>
            ))}
          </Space>
        </Card>
      ) : null}

      <Row gutter={[12, 12]}>
        {feedScope === 'team' && canWrite ? (
          <Col xs={24} md={12}>
            <Card
              loading={loading}
              size="small"
              style={{
                height: '100%',
                background: wallOfFame
                  ? 'linear-gradient(135deg, #faad1418 0%, #fff 70%)'
                  : undefined,
                borderColor: wallOfFame ? '#faad1466' : undefined,
              }}
              styles={{
                body: {
                  height: '100%',
                  minHeight: 168,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                },
              }}
            >
              <Space align="center" size={10}>
                <TrophyOutlined style={{ fontSize: 28, color: '#faad14' }} />
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  Ghi nhận nổi bật · Tuần này (nội bộ QL)
                </Typography.Text>
              </Space>
              {wallOfFame ? (
                <>
                  <div style={{ flex: 1 }}>
                    <Typography.Title level={4} style={{ margin: 0 }}>
                      {wallOfFame.name}
                    </Typography.Title>
                    <Typography.Text style={{ display: 'block', marginTop: 6 }}>
                      {wallOfFame.count} lời khen khách hàng
                    </Typography.Text>
                  </div>
                  <Button
                    icon={<HistoryOutlined />}
                    onClick={() => openTimeline(wallOfFame.name, wallOfFame.id)}
                  >
                    Xem lịch sử nghề nghiệp
                  </Button>
                </>
              ) : (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, flex: 1 }}>
                  Chưa có lời khen tuần này — ghi nhận nhanh bên dưới.
                </Typography.Paragraph>
              )}
            </Card>
          </Col>
        ) : null}
        <Col xs={24} md={feedScope === 'team' && canWrite ? 12 : 24}>
          <Card
            size="small"
            loading={loading}
            style={{ height: '100%' }}
            styles={{
              body: {
                height: '100%',
                minHeight: 168,
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              },
            }}
          >
            <Space align="center" size={10}>
              <AimOutlined style={{ fontSize: 28, color: '#eb2f96' }} />
              <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                {feedScope === 'me'
                  ? 'Khách khen · Tháng này (của bạn)'
                  : 'Mục tiêu tháng này (đội)'}
              </Typography.Text>
            </Space>
            <div style={{ flex: 1 }}>
              <Typography.Title level={4} style={{ margin: 0 }}>
                {monthPraiseCount}
                {feedScope === 'team' ? `/${MONTHLY_PRAISE_GOAL}` : ''} khách khen
              </Typography.Title>
              {feedScope === 'team' ? (
                <Progress
                  percent={Math.min(
                    100,
                    Math.round((100 * monthPraiseCount) / MONTHLY_PRAISE_GOAL),
                  )}
                  strokeColor="#eb2f96"
                  showInfo={false}
                  style={{ marginTop: 10, marginBottom: 0 }}
                />
              ) : (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                  Chỉ đếm lời khen gửi tới bạn — không so sánh với đồng nghiệp.
                </Typography.Paragraph>
              )}
            </div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {feedScope === 'me'
                ? 'Mỗi lời khen là một sự kiện trên hồ sơ của bạn.'
                : 'Động lực đội ngũ — mỗi lời khen là một sự kiện trên hồ sơ.'}
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Card
        size="small"
        styles={{
          body: {
            display: 'flex',
            flexDirection: 'column',
            gap: 14,
          },
        }}
      >
        <Space align="start" size={12}>
          <RobotOutlined style={{ fontSize: 26, color: '#722ed1', marginTop: 2 }} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <Typography.Text strong style={{ fontSize: 15 }}>
              Gợi ý bước tiếp theo
            </Typography.Text>
            <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0' }}>
              {aiNextStep}
            </Typography.Paragraph>
          </div>
        </Space>
        <Row gutter={[10, 10]}>
          {canWrite ? (
            <>
              <Col xs={24} sm={8}>
                <Link to="/people/grow" style={{ display: 'block' }}>
                  <Button
                    block
                    size="large"
                    icon={<RiseOutlined />}
                    style={{
                      borderColor: '#52c41a',
                      color: '#389e0d',
                      background: '#f6ffed',
                    }}
                  >
                    Phát triển nghề
                  </Button>
                </Link>
              </Col>
              <Col xs={24} sm={8}>
                <Link to="/people/evaluations" style={{ display: 'block' }}>
                  <Button
                    block
                    size="large"
                    icon={<StarOutlined />}
                    style={{
                      borderColor: '#722ed1',
                      color: '#531dab',
                      background: '#f9f0ff',
                    }}
                  >
                    Đánh giá tháng
                  </Button>
                </Link>
              </Col>
            </>
          ) : null}
          <Col xs={24} sm={canWrite ? 8 : 24}>
            <Link to="/people/learn" style={{ display: 'block' }}>
              <Button
                block
                size="large"
                icon={<ReadOutlined />}
                style={{
                  borderColor: '#1677ff',
                  color: '#0958d9',
                  background: '#e6f4ff',
                }}
              >
                {canWrite ? 'Đào tạo' : 'Học bài của tôi'}
              </Button>
            </Link>
          </Col>
        </Row>
      </Card>

      {canWrite ? (
        <Card
          title="Ghi nhận nhanh (khoảng 10 giây)"
          size="small"
          extra={
            selectedEmp ? (
              <Tag color="blue">Đang chọn: {selectedEmp.fullName}</Tag>
            ) : (
              <Tag color="default">Chưa chọn nhân viên</Tag>
            )
          }
        >
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
            1) Chọn nhân viên · 2) Chọn khen hoặc góp ý · 3) Xong — góp ý để hỗ trợ cải thiện, không
            phải phạt.
          </Typography.Text>

          <Select
            placeholder="Tìm và chọn nhân viên…"
            style={{ width: '100%', maxWidth: 420, marginBottom: 16 }}
            showSearch
            allowClear
            optionFilterProp="label"
            value={employeeId}
            onChange={(v) => setEmployeeId(v ?? null)}
            options={employees.map((e) => ({
              value: e.id,
              label: e.employeeCode ? `${e.fullName} (${e.employeeCode})` : e.fullName,
            }))}
          />

          <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
            Khen / động viên
          </Typography.Text>
          <Row gutter={[10, 10]} style={{ marginBottom: 16 }}>
            {QUICK_AWARDS.filter((aw) => aw.tone === 'praise').map((aw) => (
              <Col xs={12} sm={8} md={8} lg={4} key={aw.key}>
                <Button
                  block
                  type={aw.key === 'customer_praise' ? 'primary' : 'default'}
                  loading={quickBusy === aw.key}
                  disabled={!employeeId}
                  style={{
                    height: 88,
                    padding: '10px 8px',
                    whiteSpace: 'normal',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                  onClick={() => void quickAward(aw)}
                >
                  <TrophyOutlined style={{ fontSize: 20, color: '#faad14' }} />
                  <div style={{ fontSize: 12, marginTop: 6, lineHeight: 1.25 }}>{aw.badgeTitle}</div>
                  {aw.needsStars ? (
                    <div style={{ fontSize: 10, opacity: 0.75, marginTop: 2 }}>kèm chọn sao</div>
                  ) : null}
                </Button>
              </Col>
            ))}
          </Row>

          <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
            Góp ý / hỗ trợ cải thiện
          </Typography.Text>
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
            Ghi việc cần làm tốt hơn — dùng để coaching, không xếp hạng công khai.
          </Typography.Text>
          <Row gutter={[10, 10]}>
            {QUICK_AWARDS.filter((aw) => aw.tone === 'coach').map((aw) => (
              <Col xs={12} sm={8} md={8} lg={6} key={aw.key}>
                <Button
                  block
                  loading={quickBusy === aw.key}
                  disabled={!employeeId}
                  style={{
                    height: 88,
                    padding: '10px 8px',
                    whiteSpace: 'normal',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    borderColor: '#ffd591',
                    background: '#fff7e6',
                  }}
                  onClick={() => void quickAward(aw)}
                >
                  {aw.needsStars ? (
                    <WarningOutlined style={{ fontSize: 20, color: '#fa8c16' }} />
                  ) : (
                    <CommentOutlined style={{ fontSize: 20, color: '#fa8c16' }} />
                  )}
                  <div style={{ fontSize: 12, marginTop: 6, lineHeight: 1.25 }}>{aw.badgeTitle}</div>
                  <div style={{ fontSize: 10, opacity: 0.75, marginTop: 2 }}>
                    {aw.needsStars ? 'kèm chọn sao' : 'nhập nội dung'}
                  </div>
                </Button>
              </Col>
            ))}
          </Row>

          <Collapse
            ghost
            style={{ marginTop: 8 }}
            items={[
              {
                key: 'custom',
                label: 'Cần ghi nhận khác? Mở form tùy chỉnh',
                children: (
                  <Form
                    layout="vertical"
                    key={employeeId ?? 'none'}
                    initialValues={{
                      kind: 'custom',
                      isPublic: true,
                      customerRating: 5,
                      employeeId: employeeId ?? undefined,
                    }}
                    onValuesChange={(changed) => {
                      if (changed.kind != null) setCustomKind(String(changed.kind));
                      if (changed.customerRating != null) setCustomStars(Number(changed.customerRating));
                      if (changed.employeeId != null) setEmployeeId(changed.employeeId ?? null);
                    }}
                    onFinish={async (v) => {
                      setSaving(true);
                      try {
                        const empId = v.employeeId || employeeId;
                        if (!empId) {
                          message.warning('Chọn nhân viên trước');
                          return;
                        }
                        const isCustomer =
                          v.kind === 'customer_praise' ||
                          v.kind === 'customer_feedback' ||
                          v.badgeCode === 'customer_praise';
                        const stars = isCustomer
                          ? Number(v.customerRating) || customStars
                          : undefined;
                        const titlePrefix =
                          v.kind === 'custom' &&
                          v.title &&
                          !/^góp ý/i.test(v.title) &&
                          /góp ý|cải thiện/i.test(v.body ?? '')
                            ? `Góp ý — ${v.title}`
                            : v.title;
                        await createLearningRecognition({
                          employeeId: empId,
                          kind: v.kind,
                          title:
                            isCustomer && stars
                              ? v.title?.includes('★')
                                ? v.title
                                : `Khách đánh giá ${stars}★`
                              : titlePrefix,
                          body: v.body,
                          badgeCode: v.badgeCode || null,
                          isPublic: true,
                          customerRating: isCustomer ? stars : null,
                        });
                        message.success('Đã tạo sự kiện ghi nhận');
                        await reload();
                      } catch (e) {
                        message.error(apiErrorMessage(e, 'Tạo thất bại'));
                      } finally {
                        setSaving(false);
                      }
                    }}
                  >
                    <Form.Item name="employeeId" label="Nhân viên" rules={[{ required: true }]}>
                      <Select
                        style={{ maxWidth: 420 }}
                        showSearch
                        optionFilterProp="label"
                        options={employees.map((e) => ({ value: e.id, label: e.fullName }))}
                      />
                    </Form.Item>
                    <Space wrap size={12} align="start" style={{ width: '100%' }}>
                      <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
                        <Select style={{ minWidth: 180 }} options={KIND_OPTIONS} />
                      </Form.Item>
                      <Form.Item name="title" label="Tiêu đề" rules={[{ required: true }]}>
                        <Input style={{ minWidth: 240 }} maxLength={200} />
                      </Form.Item>
                      <Form.Item name="badgeCode" label="Huy hiệu (tuỳ chọn)">
                        <Select
                          allowClear
                          style={{ minWidth: 200 }}
                          placeholder="Chọn…"
                          options={QUICK_AWARDS.filter((a) => a.badgeCode).map((a) => ({
                            value: a.badgeCode!,
                            label: a.badgeTitle,
                          }))}
                        />
                      </Form.Item>
                    </Space>
                    {customKind === 'customer_praise' || customKind === 'customer_feedback' ? (
                      <Form.Item
                        name="customerRating"
                        label="Sao khách đánh giá"
                        rules={[{ required: true, message: 'Chọn số sao' }]}
                      >
                        <Rate />
                      </Form.Item>
                    ) : null}
                    <Form.Item name="body" label="Nội dung">
                      <Input.TextArea rows={2} maxLength={1000} />
                    </Form.Item>
                    <Button type="primary" htmlType="submit" loading={saving} icon={<PlusOutlined />}>
                      Tạo sự kiện ghi nhận
                    </Button>
                  </Form>
                ),
              },
            ]}
          />
        </Card>
      ) : null}

      <Modal
        title={
          starModalAward?.tone === 'coach'
            ? 'Ghi nhận góp ý khách'
            : 'Ghi nhận lời khen khách'
        }
        open={praiseModalOpen}
        okText="Ghi nhận"
        cancelText="Huỷ"
        confirmLoading={!!starModalAward && quickBusy === starModalAward.key}
        onCancel={() => {
          setPraiseModalOpen(false);
          setStarModalAward(null);
        }}
        onOk={() => void submitStarModal()}
        destroyOnClose
      >
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          <Typography.Text>
            Nhân viên:{' '}
            <Typography.Text strong>
              {employees.find((e) => e.id === employeeId)?.fullName ?? '—'}
            </Typography.Text>
          </Typography.Text>
          <div>
            <Typography.Text type="secondary">Khách đánh giá mấy sao?</Typography.Text>
            <div style={{ marginTop: 8 }}>
              <Rate value={praiseStars} onChange={setPraiseStars} />
              <Typography.Text strong style={{ marginLeft: 8, color: '#d48806' }}>
                {praiseStars}/5
              </Typography.Text>
            </div>
            {praiseStars <= 3 ? (
              <Tag color="orange" style={{ marginTop: 8 }}>
                ≤3★ → ghi nhận góp ý (hỗ trợ NV)
              </Tag>
            ) : (
              <Tag color="magenta" style={{ marginTop: 8 }}>
                ≥4★ → lời khen
              </Tag>
            )}
          </div>
          <Input.TextArea
            rows={3}
            value={praiseNote}
            onChange={(e) => setPraiseNote(e.target.value)}
            placeholder="Nội dung khách nói / góp ý (tuỳ chọn)"
            maxLength={1000}
          />
        </Space>
      </Modal>

      <Modal
        title={coachAward?.badgeTitle ?? 'Góp ý hỗ trợ'}
        open={coachModalOpen}
        okText="Ghi góp ý"
        cancelText="Huỷ"
        confirmLoading={!!coachAward && quickBusy === coachAward.key}
        onCancel={() => {
          setCoachModalOpen(false);
          setCoachAward(null);
        }}
        onOk={() => void submitCoachModal()}
        destroyOnClose
      >
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          <Typography.Text>
            Nhân viên:{' '}
            <Typography.Text strong>
              {employees.find((e) => e.id === employeeId)?.fullName ?? '—'}
            </Typography.Text>
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Viết cụ thể việc cần cải thiện — dùng để coaching, không phạt.
          </Typography.Text>
          <Input.TextArea
            rows={4}
            value={coachNote}
            onChange={(e) => setCoachNote(e.target.value)}
            placeholder="Ví dụ: Nhắc chào khách khi vào quầy; kiểm tra hạn dùng trước khi xuất…"
            maxLength={1000}
          />
        </Space>
      </Modal>

      {/* Feed sống — 3 kênh */}
      <Card
        title={
          <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
            <span>Bảng tin động lực</span>
            <Segmented
              size="small"
              value={feedScope}
              onChange={(v) => setFeedScope(v as 'me' | 'team')}
              options={[
                { label: 'Của tôi', value: 'me' },
                { label: 'Cả đội', value: 'team' },
              ]}
            />
          </Space>
        }
        loading={loading}
        size="small"
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 12, fontSize: 13 }}>
          {feedScope === 'me'
            ? 'Chỉ sự kiện của bạn — không xếp hạng đồng nghiệp.'
            : canWrite
              ? 'Feed cả cửa hàng để quản lý theo dõi và động viên — không dùng để xếp hạng công khai.'
              : 'Xem hoạt động gần đây của cửa hàng — mặc định vẫn nên xem «Của tôi».'}
        </Typography.Paragraph>
        <Tabs
          items={[
            {
              key: 'achievement',
              label: `Thành tích (${feeds.achievement.length})`,
              children: (
                <FeedChannelList
                  items={feeds.achievement}
                  empty={
                    feedScope === 'me'
                      ? 'Bạn chưa có thành tích nổi bật.'
                      : 'Chưa có thành tích nổi bật.'
                  }
                  onPerson={openTimeline}
                  hidePersonName={feedScope === 'me'}
                />
              ),
            },
            {
              key: 'learning',
              label: `Học tập (${feeds.learning.length})`,
              children: (
                <FeedChannelList
                  items={feeds.learning}
                  empty={feedScope === 'me' ? 'Bạn chưa có sự kiện học tập.' : 'Chưa có sự kiện học tập.'}
                  onPerson={openTimeline}
                  hidePersonName={feedScope === 'me'}
                />
              ),
            },
            {
              key: 'internal',
              label: `Nội bộ (${feeds.internal.length})`,
              children: (
                <FeedChannelList
                  items={feeds.internal}
                  empty={feedScope === 'me' ? 'Bạn chưa có sự kiện nội bộ.' : 'Chưa có sự kiện nội bộ.'}
                  onPerson={openTimeline}
                  hidePersonName={feedScope === 'me'}
                />
              ),
            },
          ]}
        />
      </Card>

      <Drawer
        width={400}
        open={!!timeline}
        onClose={() => setTimeline(null)}
        title={timeline ? `Lịch sử nghề nghiệp · ${timeline.name}` : ''}
      >
        {timeline ? (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              Hồ sơ nghề nghiệp theo sự kiện — học · khen · lên bậc · thành tích.
            </Typography.Paragraph>
            <Timeline
              items={timeline.items.map((r) => ({
                color:
                  classifyFeedChannel(r) === 'achievement'
                    ? 'green'
                    : classifyFeedChannel(r) === 'learning'
                      ? 'blue'
                      : 'gray',
                children: (
                  <div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {new Date(r.createdAt).toLocaleDateString('vi-VN')}
                    </Typography.Text>
                    <div>
                      {recognitionIcon(r)} {humanizeRecognitionTitle(r)}
                    </div>
                  </div>
                ),
              }))}
            />
            {!timeline.items.length ? (
              <Typography.Text type="secondary">Chưa có sự kiện cho người này.</Typography.Text>
            ) : null}
            <Link to="/people/grow">
              <Button block icon={<RiseOutlined />}>
                Mở Phát triển nghề
              </Button>
            </Link>
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
