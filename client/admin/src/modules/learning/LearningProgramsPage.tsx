import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Alert,
  App,
  Avatar,
  Button,
  Card,
  Col,
  Collapse,
  Input,
  List,
  Progress,
  Row,
  Space,
  Tag,
  Rate,
  Timeline,
  Typography,
} from 'antd';
import {
  BulbOutlined,
  CalendarOutlined,
  CheckSquareOutlined,
  HeartOutlined,
  PlusOutlined,
  ReadOutlined,
  RiseOutlined,
  SearchOutlined,
  TrophyOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchCareerLevels,
  fetchCareerRoster,
  fetchLearningRecognitions,
  fetchOperationalTasks,
  fetchPeopleDashboard,
  type LearningCareerLevel,
  type LearningCareerRosterItem,
  type LearningPeopleDashboard,
  type LearningRecognition,
} from '@/shared/api/learning.api';
import { useCanLearningWrite } from '@/shared/auth/usePermission';
import { useAuthStore } from '@/shared/auth/auth.store';
import { STORE_RULES_MODULE_ID } from '@/modules/learning/learning-module-ids';
import { parseCustomerPraiseRating } from '@/modules/learning/recognition-display';

const C = {
  train: '#1677ff',
  win: '#52c41a',
  watch: '#faad14',
  urgent: '#ff4d4f',
  grow: '#722ed1',
} as const;

function clampPct(n: number) {
  return Math.max(0, Math.min(100, Math.round(n)));
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

function healthLabel(pct: number) {
  if (pct >= 85) return { text: 'Khỏe', color: C.win };
  if (pct >= 70) return { text: 'Ổn', color: C.watch };
  return { text: 'Cần cải thiện', color: C.urgent };
}



type PriorityCard = {
  key: string;
  tone: 'urgent' | 'watch' | 'ok';
  title: string;
  detail: string;
  cta: string;
  to: string;
};

/** Card việc cần làm — theo mockup: viền trên + badge + CTA. */
function scoreToStars(score: number) {
  if (score <= 5) return Math.max(0, Math.min(5, score));
  if (score <= 10) return Math.max(0, Math.min(5, score / 2));
  return Math.max(0, Math.min(5, score / 20));
}

function PrioritySectionHeader({
  label,
  count,
  color,
}: {
  label: string;
  count: number;
  color: string;
}) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
      <Typography.Text strong style={{ color, fontSize: 14 }}>
        {label}
      </Typography.Text>
      <Tag style={{ margin: 0, borderRadius: 999 }}>{count}</Tag>
    </div>
  );
}

function PriorityTaskCard({ card }: { card: PriorityCard }) {
  const isUrgent = card.tone === 'urgent';
  const isOk = card.tone === 'ok';
  const accent = isUrgent ? C.urgent : isOk ? C.win : C.watch;
  const badgeLabel = isUrgent ? 'Khẩn cấp' : isOk ? 'Động viên' : 'Theo dõi';

  return (
    <div
      style={{
        height: '100%',
        minHeight: 148,
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        padding: '14px 16px 16px',
        borderRadius: 10,
        background: '#fff',
        border: '1px solid #f0f0f0',
        borderTop: `3px solid ${accent}`,
      }}
    >
      <Tag
        color={isUrgent ? 'error' : isOk ? 'success' : 'warning'}
        style={{ margin: 0, width: 'fit-content', borderRadius: 999 }}
      >
        {badgeLabel}
      </Tag>
      <div style={{ flex: 1 }}>
        <Typography.Title level={5} style={{ margin: '0 0 4px', fontSize: 16 }}>
          {card.title}
        </Typography.Title>
        <Typography.Text type="secondary" style={{ fontSize: 13, lineHeight: 1.45 }}>
          {card.detail}
        </Typography.Text>
      </div>
      <Link to={card.to} style={{ marginTop: 'auto' }}>
        {isUrgent ? (
          <Button type="primary" danger block style={{ borderRadius: 8 }}>
            {card.cta}
          </Button>
        ) : (
          <Button block style={{ borderRadius: 8, borderColor: accent, color: accent }}>
            {card.cta}
          </Button>
        )}
      </Link>
    </div>
  );
}

function StaffLanding() {
  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: 560 }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          Chào bạn
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Ba việc gợi ý (không bắt buộc): onboarding ca đầu · học bài ngắn · tick checklist ca.
        </Typography.Paragraph>
      </div>
      <Card style={{ borderColor: C.train }}>
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Link to="/people/learn">
            <Button type="primary" size="large" block icon={<ReadOutlined />}>
              Học bài hôm nay
            </Button>
          </Link>
          <Link to={`/people/learn/modules/${STORE_RULES_MODULE_ID}`}>
            <Button size="large" block>
              Onboarding: sẵn sàng ca đầu tiên
            </Button>
          </Link>
          <Link to="/people/recognize">
            <Button size="large" block icon={<TrophyOutlined />}>
              Hồ sơ năng lực của tôi
            </Button>
          </Link>
          <Link to="/people/tasks">
            <Button size="large" block icon={<CheckSquareOutlined />}>
              Việc được giao
            </Button>
          </Link>
          <Link to="/success/shift-checklist">
            <Button size="large" block icon={<CheckSquareOutlined />}>
              Checklist mở / đóng ca
            </Button>
          </Link>
        </Space>
      </Card>
      <Alert
        type="success"
        showIcon
        message="Không khóa bán hàng"
        description="Chưa học xong vẫn bán bình thường. Học để làm đúng hơn và được ghi nhận."
      />
    </Space>
  );
}

/**
 * Tổng quan Phát triển Nhân sự — trả lời 3 câu trong 30 giây:
 * Đội ổn không? Việc gì cần làm? Ai tiến bộ / ai cần hỗ trợ?
 */
export function LearningProgramsPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanLearningWrite();
  const username = useAuthStore((s) => s.user?.username) ?? 'bạn';
  const [dash, setDash] = useState<LearningPeopleDashboard | null>(null);
  const [levels, setLevels] = useState<LearningCareerLevel[]>([]);
  const [roster, setRoster] = useState<LearningCareerRosterItem[]>([]);
  const [feed, setFeed] = useState<LearningRecognition[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [healthOpen, setHealthOpen] = useState(true);
  const [openTaskCount, setOpenTaskCount] = useState(0);

  useEffect(() => {
    if (!canWrite) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        const [lv, d, r, f, taskRows] = await Promise.all([
          fetchCareerLevels().catch(() => [] as LearningCareerLevel[]),
          fetchPeopleDashboard().catch(() => null),
          fetchCareerRoster().catch(() => [] as LearningCareerRosterItem[]),
          fetchLearningRecognitions(40).catch(() => [] as LearningRecognition[]),
          fetchOperationalTasks().catch(() => []),
        ]);
        if (cancelled) return;
        setLevels(lv);
        setDash(d);
        setRoster(r);
        setFeed(f);
        setOpenTaskCount(taskRows.filter((task) => task.status === 'open' || task.status === 'in_progress').length);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được tổng quan'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [message, canWrite]);

  /** Sức khỏe đội — chỉ tỷ lệ tính được từ dashboard; thiếu dữ liệu thì để trống, không tự đặt số. */
  const health = useMemo(() => {
    const employees = dash?.employeeCount ?? 0;
    const ratio = (part: number) => (employees > 0 ? clampPct((100 * part) / employees) : null);
    const train =
      dash && dash.modulesTotalAssigned > 0 ? clampPct(dash.trainingCompletionPct) : null;
    const evaluated = dash ? ratio(employees - dash.unevaluatedThisMonth) : null;
    const evalAvg = dash?.avgEvaluateScore != null ? clampPct(dash.avgEvaluateScore) : null;
    const posBasic = dash ? ratio(employees - dash.missingPosBasicCount) : null;
    const known = [train, evaluated, evalAvg, posBasic].filter((v): v is number => v != null);
    const overall = known.length
      ? clampPct(known.reduce((s, v) => s + v, 0) / known.length)
      : null;
    return { train, evaluated, evalAvg, posBasic, overall };
  }, [dash]);

  const heat = useMemo(() => {
    const rows: { key: string; label: string; pct: number | null; color: string }[] = [
      { key: 'train', label: 'Hoàn thành đào tạo', pct: health.train, color: C.train },
      { key: 'pos', label: 'Có chứng nhận bán hàng tại quầy', pct: health.posBasic, color: C.win },
      { key: 'evaluated', label: 'Đã đánh giá tháng này', pct: health.evaluated, color: C.watch },
      { key: 'eval', label: 'Điểm đánh giá tháng TB', pct: health.evalAvg, color: C.grow },
    ];
    return rows.filter(
      (h): h is { key: string; label: string; pct: number; color: string } => h.pct != null,
    );
  }, [health]);

  const healthInsight = useMemo(() => {
    const sorted = [...heat].sort((a, b) => b.pct - a.pct);
    const strengths = sorted.filter((h) => h.pct >= 80).slice(0, 2);
    const weak = sorted.filter((h) => h.pct < 80).sort((a, b) => a.pct - b.pct).slice(0, 2);
    // Xu hướng tuần (có tín hiệu) — không bịa % tháng trước.
    const weekPulse =
      (dash?.modulesPassedThisWeek ?? 0) +
      (dash?.recognitionsThisWeek ?? 0) +
      (dash?.promotionsThisWeek ?? 0);
    const openIssues =
      (dash?.missingCloseChecklistBranchesToday ?? 0) + (dash?.unevaluatedThisMonth ?? 0);
    let trend: { dir: 'up' | 'flat' | 'down'; label: string };
    if (weekPulse >= 3 && openIssues === 0) {
      trend = { dir: 'up', label: '↑ Nhịp tuần này tốt' };
    } else if (openIssues >= 2) {
      trend = { dir: 'down', label: '↓ Có việc cần ưu tiên' };
    } else {
      trend = { dir: 'flat', label: '→ Giữ ổn định' };
    }
    return { strengths, weak, trend };
  }, [heat, dash]);

  const priorityCards = useMemo((): PriorityCard[] => {
    const cards: PriorityCard[] = [];
    if (openTaskCount > 0) {
      cards.push({
        key: 'tasks',
        tone: 'urgent',
        title: `${openTaskCount} việc`,
        detail: 'Đang mở hoặc chưa xong',
        cta: 'Theo dõi',
        to: '/people/tasks',
      });
    }
    if ((dash?.missingCloseChecklistBranchesToday ?? 0) > 0) {
      cards.push({
        key: 'close',
        tone: 'urgent',
        title: `${dash!.missingCloseChecklistBranchesToday} chi nhánh`,
        detail: 'Chưa đóng ca hôm nay',
        cta: 'Bấm xử lý',
        to: '/success/shift-checklist',
      });
    }
    if ((dash?.unevaluatedThisMonth ?? 0) > 0) {
      cards.push({
        key: 'eval',
        tone: 'urgent',
        title: `${dash!.unevaluatedThisMonth} nhân viên`,
        detail: 'Chưa chấm đánh giá tháng này',
        cta: 'Chấm ngay',
        to: '/people/evaluations',
      });
    }
    if ((dash?.missingPosBasicCount ?? 0) > 0) {
      cards.push({
        key: 'pos',
        tone: 'watch',
        title: `${dash!.missingPosBasicCount} NV`,
        detail: 'Chưa học bán hàng cơ bản tại quầy',
        cta: 'Giao đào tạo',
        to: '/people/enrollments',
      });
    }
    if ((dash?.pendingFeedbackCount ?? 0) > 0) {
      cards.push({
        key: 'fb',
        tone: 'watch',
        title: `${dash!.pendingFeedbackCount} đánh giá`,
        detail: 'Chờ nhân viên phản hồi',
        cta: 'Xem',
        to: '/people/evaluations',
      });
    }
    if ((dash?.eligiblePromotionCount ?? 0) > 0) {
      cards.push({
        key: 'promo',
        tone: 'watch',
        title: `${dash!.eligiblePromotionCount} người`,
        detail: 'Đủ điều kiện lên bậc',
        cta: 'Duyệt lên bậc',
        to: '/people/grow',
      });
    }
    const praiseRecent = feed.filter(
      (r) =>
        (r.kind === 'customer_praise' ||
          r.kind === 'customer_feedback' ||
          r.badgeCode === 'customer_praise') &&
        Date.now() - new Date(r.createdAt).getTime() <= 48 * 3600 * 1000 &&
        parseCustomerPraiseRating(r) != null,
    );
    if (praiseRecent.length > 0) {
      cards.unshift({
        key: 'customer-praise',
        tone: 'ok',
        title: `${praiseRecent.length} lời khen khách`,
        detail: 'Trong 48 giờ — xem sao + góp ý, động viên NV kịp thời',
        cta: 'Xem & động viên',
        to: '/people/recognize',
      });
    }
    return cards.slice(0, 6);
  }, [dash, feed, openTaskCount]);

  const urgentCards = useMemo(
    () => priorityCards.filter((c) => c.tone === 'urgent'),
    [priorityCards],
  );
  const followCards = useMemo(
    () => priorityCards.filter((c) => c.tone !== 'urgent'),
    [priorityCards],
  );
  const topPerformer = useMemo(() => {
    const scored = roster.filter((r) => r.latestAvgEvaluate != null);
    if (!scored.length) return null;
    return [...scored].sort(
      (a, b) => (b.latestAvgEvaluate ?? 0) - (a.latestAvgEvaluate ?? 0),
    )[0] ?? null;
  }, [roster]);
  const mostImproved = useMemo(() => {
    const eligible = roster.filter((r) => r.eligibleForNext);
    if (eligible.length) {
      return [...eligible].sort((a, b) => b.credentialCount - a.credentialCount)[0] ?? null;
    }
    if (!roster.length) return null;
    return [...roster].sort((a, b) => b.credentialCount - a.credentialCount)[0] ?? null;
  }, [roster]);

  const heroMood = useMemo(() => {
    const openCount = priorityCards.filter((c) => c.tone === 'urgent').length;
    if (health.overall == null) {
      return openCount ? 'Còn vài việc bạn nên ưu tiên.' : 'Chưa đủ dữ liệu để chấm sức khỏe đội.';
    }
    if (health.overall >= 85 && openCount === 0) {
      return 'Hôm nay đội ngũ đang hoạt động tốt.';
    }
    if (health.overall >= 70) {
      return 'Đội ngũ ổn — còn vài việc bạn nên ưu tiên.';
    }
    return 'Đội ngũ cần bạn vào cuộc hôm nay.';
  }, [health.overall, priorityCards]);

  const searchHits = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return [];
    return roster.filter((r) => r.employeeName.toLowerCase().includes(q)).slice(0, 6);
  }, [roster, search]);

  const timelineItems = useMemo(() => {
    if (feed.length) {
      return feed.slice(0, 8).map((r) => {
        const time = new Date(r.createdAt).toLocaleString('vi-VN', {
          hour: '2-digit',
          minute: '2-digit',
        });
        const isBadge = Boolean(r.badgeCode) || r.kind?.includes('badge');
        return {
          key: r.id,
          color: isBadge ? C.watch : C.win,
          children: (
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {time}
              </Typography.Text>
              <div>
                <Typography.Text strong>{r.employeeName}</Typography.Text>
                <Typography.Text> — {r.title}</Typography.Text>
              </div>
            </div>
          ),
        };
      });
    }
    return (dash?.celebrationItems ?? []).slice(0, 5).map((t, i) => ({
      key: String(i),
      color: C.train,
      children: (
        <div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Tuần này
          </Typography.Text>
          <div>
            <Typography.Text>{t}</Typography.Text>
          </div>
        </div>
      ),
    }));
  }, [feed, dash]);

  if (!canWrite) return <StaffLanding />;

  const status =
    health.overall != null ? healthLabel(health.overall) : { text: 'Chưa có dữ liệu', color: '#8c8c8c' };
  const maxLevelCount = Math.max(
    1,
    ...(dash?.careerLevelCounts?.map((c) => c.employeeCount) ?? [1]),
  );

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {/* Search */}
      <Input
        size="large"
        allowClear
        prefix={<SearchOutlined />}
        placeholder="Tìm nhân viên…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{ borderRadius: 10, maxWidth: '100%' }}
      />
      {searchHits.length ? (
        <Card size="small" styles={{ body: { padding: 8 } }}>
          <List
            size="small"
            dataSource={searchHits}
            renderItem={(r) => (
              <List.Item
                style={{ cursor: 'pointer', padding: '6px 8px' }}
                onClick={() => {
                  setSearch('');
                  navigate('/people/evaluations');
                }}
              >
                <Space>
                  <Avatar size="small" style={{ background: C.grow }}>
                    {initials(r.employeeName)}
                  </Avatar>
                  <Typography.Text strong>{r.employeeName}</Typography.Text>
                  <Tag color="purple">{r.currentLevelTitle ?? '—'}</Tag>
                </Space>
              </List.Item>
            )}
          />
        </Card>
      ) : null}

      {canWrite && !loading && !dash ? (
        <Alert
          type="info"
          showIcon
          style={{ borderRadius: 10 }}
          message="Tổng quan đội cần API dashboard"
          description="Máy chủ chưa có GET /learning/people/dashboard — deploy API mới để hiện metric / việc ưu tiên. Giao đào tạo, Đánh giá, Ghi nhận vẫn dùng được."
        />
      ) : null}

      {/* 1. Hero */}
      <Card
        loading={loading}
        style={{
          background: '#fafafa',
          borderColor: '#f0f0f0',
          borderRadius: 12,
        }}
      >
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 4 }}>
          Xin chào {username}
        </Typography.Title>
        <Typography.Paragraph style={{ marginBottom: 16, fontSize: 15 }}>{heroMood}</Typography.Paragraph>
        <Row gutter={[12, 12]}>
          <Col xs={12} sm={6}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Đào tạo
            </Typography.Text>
            <div>
              <Typography.Text strong style={{ color: C.train, fontSize: 18 }}>
                {health.train != null ? `${health.train}%` : '—'}
              </Typography.Text>
              <Typography.Text type="secondary">
                {health.train != null ? ' hoàn thành' : ' chưa có dữ liệu'}
              </Typography.Text>
            </div>
          </Col>
          <Col xs={12} sm={6}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Ghi nhận
            </Typography.Text>
            <div>
              <Typography.Text strong style={{ color: C.win, fontSize: 18 }}>
                {dash?.recognitionCount30d ?? 0}
              </Typography.Text>
              <Typography.Text type="secondary"> lượt / 30 ngày</Typography.Text>
            </div>
          </Col>
          <Col xs={12} sm={6}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Lên bậc
            </Typography.Text>
            <div>
              <Typography.Text strong style={{ color: C.grow, fontSize: 18 }}>
                {dash?.eligiblePromotionCount ?? 0}
              </Typography.Text>
              <Typography.Text type="secondary"> đủ điều kiện</Typography.Text>
            </div>
          </Col>
          <Col xs={12} sm={6}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Cần xử lý
            </Typography.Text>
            <div>
              <Typography.Text
                strong
                style={{
                  color: priorityCards.length ? C.urgent : C.win,
                  fontSize: 18,
                }}
              >
                {priorityCards.length}
              </Typography.Text>
              <Typography.Text type="secondary"> việc</Typography.Text>
            </div>
          </Col>
        </Row>
      </Card>

      {/* 2. Team Health + insight */}
      <Card
        loading={loading}
        title={
          <Space>
            <HeartOutlined style={{ color: C.urgent }} />
            <span>Sức khỏe đội ngũ</span>
          </Space>
        }
        extra={
          <Button type="link" onClick={() => setHealthOpen((v) => !v)}>
            {healthOpen ? 'Thu gọn' : 'Mở rộng'}
          </Button>
        }
      >
        <Row gutter={[16, 16]} align="middle">
          <Col xs={24} md={10}>
            <Space align="center" size={16} style={{ width: '100%' }}>
              <div style={{ textAlign: 'center', minWidth: 88 }}>
                <Typography.Title level={1} style={{ margin: 0, color: status.color, lineHeight: 1 }}>
                  {health.overall != null ? `${health.overall}%` : '—'}
                </Typography.Title>
                <Tag
                  color={
                    health.overall == null
                      ? 'default'
                      : status.color === C.win
                        ? 'success'
                        : status.color === C.watch
                          ? 'warning'
                          : 'error'
                  }
                >
                  {status.text}
                </Tag>
              </div>
              <div style={{ flex: 1 }}>
                <Progress
                  percent={health.overall ?? 0}
                  strokeColor={status.color}
                  showInfo={false}
                  size={['100%', 16]}
                />
                <Typography.Text
                  style={{
                    fontSize: 13,
                    color:
                      healthInsight.trend.dir === 'up'
                        ? C.win
                        : healthInsight.trend.dir === 'down'
                          ? C.urgent
                          : undefined,
                  }}
                >
                  {healthInsight.trend.label}
                </Typography.Text>
                <div>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Trung bình các tỷ lệ có dữ liệu bên dưới. Xu hướng theo tín hiệu tuần này — chưa so tháng trước.
                  </Typography.Text>
                </div>
              </div>
            </Space>
          </Col>
          <Col xs={24} md={14}>
            <Row gutter={[12, 8]}>
              <Col xs={24} sm={12}>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Điểm mạnh
                </Typography.Text>
                {healthInsight.strengths.length ? (
                  healthInsight.strengths.map((s) => (
                    <div key={s.key}>
                      <Typography.Text style={{ color: C.win }}>✓ {s.label}</Typography.Text>
                    </div>
                  ))
                ) : (
                  <Typography.Text type="secondary">Chưa có mảng ≥ 80%</Typography.Text>
                )}
              </Col>
              <Col xs={24} sm={12}>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Cần cải thiện
                </Typography.Text>
                {healthInsight.weak.length ? (
                  healthInsight.weak.map((s) => (
                    <div key={s.key}>
                      <Typography.Text style={{ color: C.watch }}>⚠ {s.label}</Typography.Text>
                    </div>
                  ))
                ) : (
                  <Typography.Text style={{ color: C.win }}>✓ Không điểm yếu rõ</Typography.Text>
                )}
              </Col>
            </Row>
          </Col>
        </Row>
        {healthOpen ? (
          <Row gutter={[12, 8]} style={{ marginTop: 16 }}>
            {heat.length ? (
              heat.map((h) => (
                <Col xs={12} sm={8} md={6} key={h.key}>
                  <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                    {h.label}
                  </Typography.Text>
                  <Progress percent={h.pct} strokeColor={h.color} size="small" />
                </Col>
              ))
            ) : (
              <Col span={24}>
                <Typography.Text type="secondary">Chưa có dữ liệu</Typography.Text>
              </Col>
            )}
          </Row>
        ) : null}
      </Card>

      {/* 3. Việc cần xử lý — theo mockup chuyên nghiệp */}
      <Card
        loading={loading}
        styles={{
          body: { padding: '20px 24px' },
        }}
        style={{ borderRadius: 12 }}
      >
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 12,
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            marginBottom: 20,
          }}
        >
          <div>
            <Space align="start" size={10}>
              <WarningOutlined style={{ color: C.urgent, fontSize: 22, marginTop: 2 }} />
              <div>
                <Typography.Title level={4} style={{ margin: 0, color: '#1d39c4' }}>
                  Việc cần xử lý hôm nay
                </Typography.Title>
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  Tổng hợp các công việc quan trọng cần bạn xử lý
                </Typography.Text>
              </div>
            </Space>
          </div>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 12px',
              borderRadius: 10,
              background: '#f5f5f5',
              color: '#595959',
              fontSize: 13,
            }}
          >
            <CalendarOutlined />
            <span>
              {new Date().toLocaleDateString('vi-VN', {
                weekday: 'long',
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
              })}
            </span>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              · Cập nhật lúc{' '}
              {new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
            </Typography.Text>
          </div>
        </div>

        {priorityCards.length ? (
          <Space direction="vertical" size={20} style={{ width: '100%' }}>
            {urgentCards.length ? (
              <div>
                <PrioritySectionHeader
                  label="Công việc khẩn cấp"
                  count={urgentCards.length}
                  color={C.urgent}
                />
                <Row gutter={[14, 14]}>
                  {urgentCards.map((card) => (
                    <Col xs={24} sm={12} lg={8} key={card.key}>
                      <PriorityTaskCard card={card} />
                    </Col>
                  ))}
                </Row>
              </div>
            ) : null}
            {followCards.length ? (
              <div>
                <PrioritySectionHeader
                  label="Công việc theo dõi"
                  count={followCards.length}
                  color={C.watch}
                />
                <Row gutter={[14, 14]}>
                  {followCards.map((card) => (
                    <Col xs={24} sm={12} lg={8} key={card.key}>
                      <PriorityTaskCard card={card} />
                    </Col>
                  ))}
                </Row>
              </div>
            ) : null}
          </Space>
        ) : (
          <Alert
            type="success"
            showIcon
            style={{ borderRadius: 10 }}
            message="Không việc gấp — giữ nhịp ghi nhận và học bài."
          />
        )}

        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 12,
            justifyContent: 'space-between',
            alignItems: 'stretch',
            marginTop: 20,
            paddingTop: 16,
            borderTop: '1px solid #f0f0f0',
          }}
        >
          <Space wrap size={10}>
            <Link to="/people/evaluations">
              <Button type="primary" icon={<PlusOutlined />} style={{ borderRadius: 8 }}>
                Chấm tháng
              </Button>
            </Link>
            <Link to="/people/enrollments">
              <Button
                icon={<ReadOutlined />}
                style={{ borderRadius: 8, borderColor: C.train, color: C.train }}
              >
                Giao học
              </Button>
            </Link>
            <Link to="/people/recognize">
              <Button
                icon={<TrophyOutlined />}
                style={{ borderRadius: 8, borderColor: C.win, color: C.win }}
              >
                Khen
              </Button>
            </Link>
          </Space>
          <div
            style={{
              flex: '1 1 240px',
              maxWidth: 420,
              display: 'flex',
              gap: 10,
              alignItems: 'flex-start',
              padding: '10px 14px',
              borderRadius: 10,
              background: '#f5f8ff',
              border: '1px solid #e6f0ff',
            }}
          >
            <BulbOutlined style={{ color: C.train, fontSize: 16, marginTop: 2 }} />
            <div>
              <Typography.Text strong style={{ fontSize: 13, display: 'block' }}>
                Mẹo nhỏ
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Xử lý các mục khẩn cấp trước để vận hành hiệu quả hơn
              </Typography.Text>
            </div>
          </div>
        </div>
      </Card>

      {/* 8. Team Heat + standouts */}
      <Row gutter={[12, 12]}>
        <Col xs={24} lg={14}>
          <Card title="Điểm nóng đội ngũ — yếu ở đâu?" size="small" loading={loading}>
            <Space direction="vertical" style={{ width: '100%' }} size={12}>
              {!heat.length ? <Typography.Text type="secondary">Chưa có dữ liệu</Typography.Text> : null}
              {heat.map((h) => (
                <div key={h.key}>
                  <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                    <Typography.Text style={{ color: h.color }}>{h.label}</Typography.Text>
                    <Typography.Text strong>{h.pct}%</Typography.Text>
                  </Space>
                  <Progress percent={h.pct} strokeColor={h.color} showInfo={false} />
                </div>
              ))}
            </Space>
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Card size="small" loading={loading}>
              <Space align="start" size={12}>
                <Avatar size={48} style={{ background: C.win, fontSize: 16 }}>
                  {topPerformer ? initials(topPerformer.employeeName) : '?'}
                </Avatar>
                <div>
                  <Typography.Text type="secondary">Top đánh giá</Typography.Text>
                  <Typography.Title level={5} style={{ margin: '2px 0 4px' }}>
                    {topPerformer?.employeeName ?? 'Chưa có điểm chấm'}
                  </Typography.Title>
                  {topPerformer?.latestAvgEvaluate != null ? (
                    <>
                      <Rate
                        disabled
                        allowHalf
                        value={scoreToStars(topPerformer.latestAvgEvaluate)}
                        style={{ fontSize: 14, color: C.watch }}
                      />
                      <div>
                        <Typography.Text strong style={{ color: C.win, fontSize: 18 }}>
                          {topPerformer.latestAvgEvaluate}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                          {' '}
                          · {topPerformer.currentLevelTitle ?? '—'}
                        </Typography.Text>
                      </div>
                    </>
                  ) : (
                    <Link to="/people/evaluations">Chấm để hiện Top</Link>
                  )}
                </div>
              </Space>
            </Card>
            <Card size="small" loading={loading}>
              <Space align="start" size={12}>
                <Avatar size={48} style={{ background: C.train, fontSize: 16 }}>
                  {mostImproved ? initials(mostImproved.employeeName) : '?'}
                </Avatar>
                <div>
                  <Typography.Text type="secondary">Tiến bộ / đáng chú ý</Typography.Text>
                  <Typography.Title level={5} style={{ margin: '2px 0 4px' }}>
                    {mostImproved?.employeeName ?? '—'}
                  </Typography.Title>
                  {mostImproved ? (
                    <Typography.Text type="secondary">
                      {mostImproved.eligibleForNext
                        ? `Đủ điều kiện lên ${mostImproved.nextLevelTitle ?? 'bậc tiếp'}`
                        : `${mostImproved.credentialCount} năng lực · ${mostImproved.currentLevelTitle ?? ''}`}
                    </Typography.Text>
                  ) : (
                    <Typography.Text type="secondary">Khi có học + chấm sẽ hiện</Typography.Text>
                  )}
                </div>
              </Space>
            </Card>
          </Space>
        </Col>
      </Row>

      {/* 4–6. Thành tích badge + Timeline */}
      <Row gutter={[12, 12]}>
        <Col xs={24} md={10}>
          <Card
            title={
              <Space>
                <TrophyOutlined style={{ color: C.win }} />
                Thành tích đội ngũ
              </Space>
            }
            size="small"
            loading={loading}
            extra={<Link to="/people/recognize">Ghi nhận</Link>}
          >
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
              Chỉ tóm tắt — chi tiết xem ở Ghi nhận / hồ sơ từng người (tránh quá nhiều huy hiệu trên
              một màn).
            </Typography.Paragraph>
            <Row gutter={[8, 8]}>
              {(
                [
                  {
                    value: dash?.recognitionCount30d ?? 0,
                    label: 'Ghi nhận',
                    color: C.win,
                  },
                  {
                    value: dash?.badgeCount ?? 0,
                    label: 'Chứng nhận / thành tích',
                    color: C.watch,
                  },
                  {
                    value: dash?.promotionsThisWeek ?? 0,
                    label: 'Lên bậc tuần',
                    color: C.grow,
                  },
                  {
                    value: dash?.recognitionsThisWeek ?? 0,
                    label: 'Tin vui tuần',
                    color: C.urgent,
                  },
                ] as const
              ).map((tile) => (
                <Col xs={12} key={tile.label}>
                  <Link to="/people/recognize">
                    <Card size="small" hoverable styles={{ body: { padding: 12 } }}>
                      <Typography.Title level={3} style={{ margin: 0, color: tile.color }}>
                        {tile.value}
                      </Typography.Title>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {tile.label}
                      </Typography.Text>
                    </Card>
                  </Link>
                </Col>
              ))}
            </Row>
            <Link to="/people/recognize">
              <Button type="primary" ghost style={{ marginTop: 12 }} block icon={<TrophyOutlined />}>
                Ghi nhận có ý nghĩa
              </Button>
            </Link>
          </Card>
        </Col>
        <Col xs={24} md={14}>
          <Card title="Hoạt động gần đây" size="small" loading={loading}>
            {timelineItems.length ? (
              <Timeline items={timelineItems} style={{ marginTop: 8 }} />
            ) : (
              <Typography.Text type="secondary">
                Chưa có hoạt động — hãy ghi nhận hoặc hoàn thành bài học.
              </Typography.Text>
            )}
          </Card>
        </Col>
      </Row>

      {/* 7. Lộ trình nghề — lớn hơn */}
      <Card
        title={
          <Space>
            <RiseOutlined style={{ color: C.grow }} />
            Lộ trình phát triển
          </Space>
        }
        extra={
          <Link to="/people/grow">
            <Button
              size="small"
              icon={<RiseOutlined />}
              style={{
                borderColor: C.grow,
                color: C.grow,
                background: '#f9f0ff',
              }}
            >
              Phát triển nghề
            </Button>
          </Link>
        }
        loading={loading}
      >
        {levels.length ? (
          <div style={{ overflowX: 'auto', paddingBottom: 8 }}>
            <div style={{ display: 'flex', alignItems: 'stretch', minWidth: levels.length * 160 }}>
              {levels.map((l, idx) => {
                const count =
                  dash?.careerLevelCounts?.find((c) => c.levelCode === l.code)?.employeeCount ?? 0;
                const bar = clampPct((count / maxLevelCount) * 100);
                return (
                  <div key={l.id} style={{ display: 'flex', alignItems: 'center', flex: 1 }}>
                    <Link to="/people/grow" style={{ textDecoration: 'none', flex: 1 }}>
                      <div
                        style={{
                          minWidth: 140,
                          padding: '14px 14px 12px',
                          borderRadius: 12,
                          border: `1px solid ${C.grow}40`,
                          background: count > 0 ? `${C.grow}0a` : '#fafafa',
                        }}
                      >
                        <Typography.Text
                          style={{
                            fontSize: 11,
                            letterSpacing: 0.6,
                            color: C.grow,
                            fontWeight: 600,
                          }}
                        >
                          Bậc {idx + 1}
                        </Typography.Text>
                        <Typography.Title level={5} style={{ margin: '4px 0 10px', color: C.grow }}>
                          {l.title}
                        </Typography.Title>
                        <Progress
                          percent={bar}
                          strokeColor={C.grow}
                          showInfo={false}
                          size="small"
                        />
                        <Typography.Text strong>
                          {count} người
                        </Typography.Text>
                      </div>
                    </Link>
                    {idx < levels.length - 1 ? (
                      <Typography.Text
                        style={{
                          margin: '0 8px',
                          color: C.grow,
                          fontSize: 18,
                          flexShrink: 0,
                        }}
                      >
                        →
                      </Typography.Text>
                    ) : null}
                  </div>
                );
              })}
            </div>
          </div>
        ) : (
          <Typography.Text type="secondary">Đang tải lộ trình…</Typography.Text>
        )}
        <Collapse
          ghost
          style={{ marginTop: 4 }}
          items={[
            {
              key: 'hint',
              label: 'Mỗi bậc gồm gì?',
              children: (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                  Điều kiện: học đủ · điểm đánh giá · thâm niên · năng lực. Bấm «Phát triển nghề»
                  để mở hồ sơ và duyệt khi sẵn sàng.
                </Typography.Paragraph>
              ),
            },
          ]}
        />
      </Card>

      <Alert
        type="info"
        showIcon
        message="Không khóa bán hàng"
        description="Chỉ số sức khỏe đội ngũ tổng hợp từ đào tạo, đánh giá, checklist, hiệu suất và ghi nhận — giúp ưu tiên cải thiện, không thay báo cáo chi tiết và không chặn bán tại quầy."
      />
    </Space>
  );
}

export default LearningProgramsPage;
