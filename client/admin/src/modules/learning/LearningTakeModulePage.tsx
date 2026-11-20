import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Alert, App, Button, Card, Col, Radio, Row, Space, Tag, Typography } from 'antd';
import {
  ArrowLeftOutlined,
  ArrowRightOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  RocketOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  acknowledgeLearningModule,
  fetchLearningModule,
  fetchLearningProgram,
  fetchMyLearning,
  startLearningModule,
  submitLearningQuiz,
  uploadLearningAckSelfie,
  type LearningModuleDetail,
  type LearningModuleProgress,
  type LearningMyLearning,
} from '@/shared/api/learning.api';
import { fetchShiftChecklistToday, type ShiftChecklistToday } from '@/shared/api/success.api';
import { useCanAccessSuccessModule } from '@/shared/auth/usePermission';
import { useAuthStore } from '@/shared/auth/auth.store';
import {
  difficultyLabel,
  resolveLessonMeta,
} from '@/modules/learning/learning-module-meta';
import { LearningCommitSignCard } from '@/modules/learning/LearningCommitSignCard';
import {
  mapPracticeAnswersToOriginal,
  shuffleQuizForPractice,
  type ShuffledQuizQuestion,
} from '@/modules/learning/quiz-shuffle';

function checklistStatusVi(status: string) {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'complete') return 'Đã xong';
  if (s === 'in_progress' || s === 'active') return 'Đang làm';
  if (s === 'not_started' || s === 'pending') return 'Chưa làm';
  return status;
}

/** Bài thuộc lộ trình nào thì lấy tiến độ của lộ trình đó (không chỉ lộ trình đầu). */
function modulesOfProgram(mine: LearningMyLearning, programId: string): LearningModuleProgress[] {
  const match = mine.programs?.find((p) => p.program.id === programId);
  return match ? match.modules : mine.modules;
}

function levelLabelVi(code: string) {
  const m = code?.trim().match(/^L?(\d+)$/i);
  return m ? `Bậc ${m[1]}` : code;
}

/** Markdown nhẹ — kiểu tài liệu trình bày (tiêu đề, danh sách, đoạn). */
function inlineMd(text: string): ReactNode {
  const parts = text.split(/(\*\*[^*]+\*\*)/g);
  return parts.map((part, i) => {
    if (part.startsWith('**') && part.endsWith('**') && part.length > 4) {
      return <strong key={i}>{part.slice(2, -2)}</strong>;
    }
    return <span key={i}>{part}</span>;
  });
}

function renderMarkdownLite(md: string) {
  const nodes: ReactNode[] = [];
  let listBuf: { ordered: boolean; items: string[] } | null = null;

  const flushList = () => {
    if (!listBuf) return;
    const TagName = listBuf.ordered ? 'ol' : 'ul';
    nodes.push(
      <TagName
        key={`list-${nodes.length}`}
        style={{
          margin: '0 0 16px',
          paddingLeft: 22,
          color: '#434343',
          lineHeight: 1.7,
        }}
      >
        {listBuf.items.map((item, i) => (
          <li key={i} style={{ marginBottom: 6 }}>
            {inlineMd(item)}
          </li>
        ))}
      </TagName>,
    );
    listBuf = null;
  };

  md.split('\n').forEach((line, i) => {
    const ordered = line.match(/^(\d+)\.\s+(.*)$/);
    const bullet = line.match(/^[-*]\s+(.*)$/);

    if (ordered) {
      if (!listBuf || !listBuf.ordered) {
        flushList();
        listBuf = { ordered: true, items: [] };
      }
      listBuf.items.push(ordered[2]);
      return;
    }
    if (bullet) {
      if (!listBuf || listBuf.ordered) {
        flushList();
        listBuf = { ordered: false, items: [] };
      }
      listBuf.items.push(bullet[1]);
      return;
    }

    flushList();

    if (/^---+$/.test(line.trim())) {
      nodes.push(
        <hr
          key={i}
          style={{ border: 'none', borderTop: '1px solid #f0f0f0', margin: '20px 0' }}
        />,
      );
      return;
    }
    if (line.startsWith('### ')) {
      nodes.push(
        <Typography.Title
          level={5}
          key={i}
          style={{ marginTop: 18, marginBottom: 8, color: '#8c8c8c', fontWeight: 600 }}
        >
          {line.slice(4)}
        </Typography.Title>,
      );
      return;
    }
    if (line.startsWith('## ')) {
      nodes.push(
        <Typography.Title
          level={4}
          key={i}
          style={{
            marginTop: 28,
            marginBottom: 12,
            color: '#595959',
            borderBottom: '1px solid #f5f5f5',
            paddingBottom: 8,
            fontWeight: 600,
          }}
        >
          {line.slice(3)}
        </Typography.Title>,
      );
      return;
    }
    if (line.startsWith('# ')) {
      nodes.push(
        <Typography.Title
          level={3}
          key={i}
          style={{ marginTop: 24, marginBottom: 12, color: '#434343', fontWeight: 650 }}
        >
          {line.slice(2)}
        </Typography.Title>,
      );
      return;
    }
    if (!line.trim()) {
      nodes.push(<div key={i} style={{ height: 8 }} />);
      return;
    }
    nodes.push(
      <Typography.Paragraph
        key={i}
        style={{ marginBottom: 12, fontSize: 15, lineHeight: 1.75, color: '#434343' }}
      >
        {inlineMd(line)}
      </Typography.Paragraph>,
    );
  });

  flushList();
  return nodes;
}

function L4PracticeCard({ checklist }: { checklist: ShiftChecklistToday | null }) {
  return (
    <Card
      size="small"
      title="Thực hành — Checklist ca thật"
      style={{ borderColor: '#1677ff55', borderRadius: 12 }}
    >
      <Typography.Paragraph style={{ marginBottom: 8 }}>
        Học xong chưa đủ — áp dụng trên ca hôm nay: tick checklist{' '}
        <strong>đầu ca / cuối ca</strong> và thực hành tốt hàng ngày.
      </Typography.Paragraph>
      {checklist ? (
        <Space wrap style={{ marginBottom: 8 }}>
          <Tag color={checklist.open.status === 'completed' ? 'success' : 'default'}>
            Đầu ca: {checklistStatusVi(checklist.open.status)} (
            {checklist.open.checkedCount}/{checklist.open.totalCount || 0})
          </Tag>
          <Tag color={checklist.close.status === 'completed' ? 'success' : 'default'}>
            Cuối ca: {checklistStatusVi(checklist.close.status)} (
            {checklist.close.checkedCount}/{checklist.close.totalCount || 0})
          </Tag>
        </Space>
      ) : (
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
          Mở checklist để thực hành nhịp đầu / giữa / cuối ca.
        </Typography.Text>
      )}
      <Space wrap>
        <Link to="/success/shift-checklist">
          <Button type="primary">Checklist đầu / cuối ca</Button>
        </Link>
        <Link to="/inventory/gpp-checklist">
          <Button>Thực hành tốt hàng ngày</Button>
        </Link>
      </Space>
    </Card>
  );
}


async function resolveModuleNeighbors(programId: string, moduleId: string) {
  const program = await fetchLearningProgram(programId);
  const mods = program.modules ?? [];
  const idx = mods.findIndex((m) => m.id === moduleId);
  return {
    prev: idx > 0 ? mods[idx - 1] ?? null : null,
    next: idx >= 0 && idx < mods.length - 1 ? mods[idx + 1] ?? null : null,
  };
}

export function LearningTakeModulePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const canSuccess = useCanAccessSuccessModule();
  const username = useAuthStore((s) => s.user?.username) ?? 'Nhân viên';
  const [detail, setDetail] = useState<LearningModuleDetail | null>(null);
  const [progress, setProgress] = useState<LearningModuleProgress | null>(null);
  const [answers, setAnswers] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);
  const [nextModuleId, setNextModuleId] = useState<string | null>(null);
  const [nextModuleTitle, setNextModuleTitle] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [checklist, setChecklist] = useState<ShiftChecklistToday | null>(null);
  const [lastQuizFailed, setLastQuizFailed] = useState(false);
  const [showApply, setShowApply] = useState(false);
  const [practiceQuiz, setPracticeQuiz] = useState(false);
  const [shuffledQuestions, setShuffledQuestions] = useState<ShuffledQuizQuestion[] | null>(null);

  const isL4 =
    detail?.levelCode === 'L4' ||
    detail?.code === 'l4_own_shift' ||
    (detail?.title?.includes('đầu ca') ?? false);

  const reload = async () => {
    if (!id) return;
    const [mod, mine] = await Promise.all([fetchLearningModule(id), fetchMyLearning()]);
    setDetail(mod);
    const pathModules = modulesOfProgram(mine, mod.programId);
    setProgress(pathModules.find((m) => m.moduleId === id) ?? null);
    setAnswers(Array(mod.questions.length).fill(-1));
    // Prefer next unfinished in path; else sequential next in program.
    const nextUnfinished = pathModules
      .filter((m) => m.moduleId !== id && m.status !== 'passed')
      .sort((a, b) => a.sortOrder - b.sortOrder)[0];
    if (nextUnfinished) {
      setNextModuleId(nextUnfinished.moduleId);
      setNextModuleTitle(nextUnfinished.title);
    } else {
      try {
        const n = await resolveModuleNeighbors(mod.programId, mod.id);
        setNextModuleId(n.next?.id ?? null);
        setNextModuleTitle(n.next?.title ?? null);
      } catch {
        setNextModuleId(null);
        setNextModuleTitle(null);
      }
    }
    try {
      await startLearningModule(id);
    } catch {
      /* chưa enroll */
    }
  };

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    (async () => {
      try {
        await reload();
      } catch (e) {
        if (!cancelled) message.error(apiErrorMessage(e, 'Không tải bài học'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    if (!isL4 || !canSuccess) {
      setChecklist(null);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        const t = await fetchShiftChecklistToday();
        if (!cancelled) setChecklist(t);
      } catch {
        if (!cancelled) setChecklist(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isL4, canSuccess]);

  const canQuiz = useMemo(() => {
    if (!detail) return false;
    if (!detail.requireAck) return true;
    return Boolean(progress?.acknowledgedAt);
  }, [detail, progress]);

  const meta = useMemo(
    () =>
      detail
        ? resolveLessonMeta({
            moduleCode: detail.code,
            levelCode: detail.levelCode,
            title: detail.title,
            requireAck: detail.requireAck,
            questionCount: detail.questions.length,
            durationMinutes: detail.durationMinutes,
          })
        : null,
    [detail],
  );

  const onAck = async (selfieFile?: File | null) => {
    if (!id) return;
    setBusy(true);
    try {
      let selfieUrl: string | null = null;
      if (selfieFile && detail?.levelCode?.toUpperCase() === 'L0') {
        selfieUrl = await uploadLearningAckSelfie(selfieFile);
      }
      await acknowledgeLearningModule(id, selfieUrl);
      message.success('Đã ký cam kết điện tử');
      await reload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không xác nhận được — hãy «Bắt đầu» lộ trình trước'));
    } finally {
      setBusy(false);
    }
  };

  const onSubmit = async () => {
    if (!id || !detail) return;
    if (answers.some((a) => a < 0)) {
      message.warning('Hãy trả lời đủ các câu');
      return;
    }
    setBusy(true);
    try {
      const payloadAnswers =
        practiceQuiz && shuffledQuestions
          ? mapPracticeAnswersToOriginal(detail.questions, shuffledQuestions, answers)
          : answers;
      const result = await submitLearningQuiz(id, payloadAnswers, {
        practice: practiceQuiz,
        questionIds: detail.questions.map((q) => q.id),
        quizVariant: detail.quizVariant,
      });
      if (result.passed) {
        setLastQuizFailed(false);
        if (practiceQuiz) {
          setPracticeQuiz(false);
          setShuffledQuestions(null);
          message.success(`Ôn lại xong (${result.scorePct}%) — điểm chính thức không đổi`);
        } else {
          setShowApply(true);
          message.success(`Đạt ${result.scorePct}% — bài này xong`);
        }
        const [mod, mine] = await Promise.all([fetchLearningModule(id), fetchMyLearning()]);
        setDetail(mod);
        setAnswers(Array(mod.questions.length).fill(-1));
        const pathModules = modulesOfProgram(mine, mod.programId);
        setProgress(pathModules.find((m) => m.moduleId === id) ?? null);
        const next = pathModules.find((m) => m.status !== 'passed');
        if (next) {
          setNextModuleId(next.moduleId);
          setNextModuleTitle(next.title);
        }
      } else {
        setLastQuizFailed(true);
        if (!practiceQuiz) setShowApply(false);
        message.warning(
          practiceQuiz
            ? `Ôn chưa đạt (${result.scorePct}% / cần ${result.passScorePct}%) — điểm chính thức giữ nguyên`
            : `Chưa đạt (${result.scorePct}% / cần ${result.passScorePct}%)`,
        );
        if (!practiceQuiz) await reload();
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Nộp bài thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const quizQuestions = practiceQuiz && shuffledQuestions ? shuffledQuestions : detail?.questions ?? [];

  return (
    <Space
      direction="vertical"
      size={16}
      style={{ width: '100%', maxWidth: 880, margin: '0 auto', paddingBottom: 72 }}
    >
      <Link to="/people/learn">
        <Button icon={<ArrowLeftOutlined />}>Về danh sách bài học</Button>
      </Link>

      <Card
        loading={loading}
        bordered={false}
        style={{
          width: '100%',
          borderRadius: 16,
          overflow: 'hidden',
          boxShadow: '0 8px 28px rgba(15, 23, 42, 0.06)',
        }}
        styles={{ body: { padding: 0 } }}
      >
        {detail ? (
          <>
            {/* Cover — full-bleed; text column centered */}
            <div
              style={{
                background:
                  'linear-gradient(135deg, #e6f4ff 0%, #f9f0ff 45%, #ffffff 100%)',
                borderBottom: '1px solid #f0f0f0',
              }}
            >
              <div style={{ padding: '32px 36px 28px' }}>
                <Space wrap size={8} style={{ marginBottom: 10 }}>
                  <Tag color="purple">{levelLabelVi(detail.levelCode)}</Tag>
                  {meta ? <Tag color="blue">{meta.kindLabel}</Tag> : null}
                  <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                    <ClockCircleOutlined /> {meta?.minutes ?? detail.durationMinutes} phút
                    {meta
                      ? ` · ${difficultyLabel(meta.difficulty)} · cần ≥ ${detail.passScorePct}%`
                      : ''}
                  </Typography.Text>
                </Space>
                <Typography.Title
                  level={2}
                  style={{
                    margin: 0,
                    fontSize: 28,
                    fontWeight: 650,
                    color: '#262626',
                    lineHeight: 1.35,
                  }}
                >
                  {detail.title}
                </Typography.Title>
                {detail.summary ? (
                  <Typography.Paragraph
                    type="secondary"
                    style={{ margin: '10px 0 0', fontSize: 15 }}
                  >
                    {detail.summary}
                  </Typography.Paragraph>
                ) : null}
              </div>
            </div>

            <div style={{ padding: '28px 36px 36px' }}>
              {meta?.outcomes.length ? (
                <div style={{ marginBottom: 28 }}>
                  <Typography.Text
                    strong
                    style={{
                      display: 'block',
                      marginBottom: 12,
                      fontSize: 13,
                      letterSpacing: 0.3,
                      color: '#8c8c8c',
                    }}
                  >
                    Sau bài này bạn sẽ
                  </Typography.Text>
                  <Row gutter={[12, 12]}>
                    {meta.outcomes.map((o) => (
                      <Col xs={24} sm={12} md={8} key={o}>
                        <div
                          style={{
                            height: '100%',
                            padding: '14px 14px',
                            borderRadius: 12,
                            background: '#f6ffed',
                            border: '1px solid #b7eb8f55',
                            display: 'flex',
                            gap: 10,
                            alignItems: 'flex-start',
                          }}
                        >
                          <CheckCircleOutlined style={{ color: '#52c41a', fontSize: 16, marginTop: 2 }} />
                          <Typography.Text style={{ fontSize: 13, lineHeight: 1.55, color: '#389e0d' }}>
                            {o}
                          </Typography.Text>
                        </div>
                      </Col>
                    ))}
                  </Row>
                </div>
              ) : null}

              <div
                style={{
                  display: 'flex',
                  flexWrap: 'wrap',
                  gap: 8,
                  alignItems: 'center',
                  marginBottom: 24,
                  padding: '12px 14px',
                  borderRadius: 12,
                  background: '#e6f4ff',
                  border: '1px solid #91caff55',
                }}
              >
                <Typography.Text strong style={{ color: '#0958d9', marginRight: 4 }}>
                  Làm lần lượt:
                </Typography.Text>
                {['Đọc', 'Ký cam kết', 'Câu hỏi kiểm tra', 'Áp dụng trên ca'].map((step, idx, arr) => (
                  <span key={step} style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                    <Tag color="blue" style={{ margin: 0 }}>
                      {idx + 1}. {step}
                    </Tag>
                    {idx < arr.length - 1 ? (
                      <Typography.Text type="secondary">→</Typography.Text>
                    ) : null}
                  </span>
                ))}
                <Typography.Text type="secondary" style={{ fontSize: 12, marginLeft: 'auto' }}>
                  Sai thì học lại — không khóa bán
                </Typography.Text>
              </div>

              <Typography.Text
                strong
                style={{
                  display: 'block',
                  marginBottom: 14,
                  fontSize: 13,
                  letterSpacing: 0.3,
                  color: '#8c8c8c',
                }}
              >
                Nội dung cần đọc
              </Typography.Text>
              <article
                style={{
                  background: '#fff',
                  borderRadius: 0,
                  padding: '4px 0 8px',
                }}
              >
                {renderMarkdownLite(detail.bodyMarkdown)}
              </article>

              <Space direction="vertical" size={16} style={{ width: '100%', marginTop: 28 }}>
                {isL4 ? <L4PracticeCard checklist={checklist} /> : null}

                {detail.requireAck ? (
                  <LearningCommitSignCard
                    acknowledgedAt={progress?.acknowledgedAt}
                    acknowledgeSelfieUrl={progress?.acknowledgeSelfieUrl}
                    signerLabel={username}
                    busy={busy}
                    allowSelfie={detail.levelCode?.toUpperCase() === 'L0'}
                    onSign={onAck}
                  />
                ) : null}

                {canQuiz && (progress?.status !== 'passed' || practiceQuiz) ? (
                  <Card
                    title={
                      practiceQuiz
                        ? 'Ôn lại câu hỏi (xáo thứ tự — không đổi điểm chính thức)'
                        : 'Trả lời vài câu (không cần sợ)'
                    }
                    style={{ borderRadius: 12 }}
                  >
                    <Space direction="vertical" style={{ width: '100%' }} size={16}>
                      {quizQuestions.map((q, qi) => (
                        <div key={q.id}>
                          <Typography.Text strong>
                            Câu {qi + 1}. {q.prompt}
                          </Typography.Text>
                          <Radio.Group
                            style={{
                              display: 'flex',
                              flexDirection: 'column',
                              gap: 8,
                              marginTop: 8,
                            }}
                            value={answers[qi] >= 0 ? answers[qi] : undefined}
                            onChange={(e) => {
                              const next = [...answers];
                              next[qi] = e.target.value as number;
                              setAnswers(next);
                            }}
                          >
                            {q.options.map((opt, oi) => (
                              <Radio
                                key={oi}
                                value={oi}
                                style={{ whiteSpace: 'normal', height: 'auto' }}
                              >
                                {opt}
                              </Radio>
                            ))}
                          </Radio.Group>
                        </div>
                      ))}
                      <Space wrap style={{ width: '100%' }}>
                        <Button
                          type="primary"
                          size="large"
                          loading={busy}
                          onClick={() => void onSubmit()}
                        >
                          {practiceQuiz ? 'Nộp bài ôn' : 'Nộp bài'}
                        </Button>
                        {practiceQuiz ? (
                          <Button
                            size="large"
                            onClick={() => {
                              setPracticeQuiz(false);
                              setShuffledQuestions(null);
                              setLastQuizFailed(false);
                            }}
                          >
                            Đóng
                          </Button>
                        ) : null}
                      </Space>
                    </Space>
                  </Card>
                ) : null}

                {lastQuizFailed && meta?.coachHint ? (
                  <Alert
                    type="warning"
                    showIcon
                    message="Gợi ý ôn lại"
                    description={meta.coachHint}
                  />
                ) : null}

                {(showApply || progress?.status === 'passed') && meta?.applyMission ? (
                  <Card
                    title={
                      <Space>
                        <RocketOutlined style={{ color: '#722ed1' }} />
                        Áp dụng hôm nay
                      </Space>
                    }
                    style={{ borderColor: '#722ed133', borderRadius: 12 }}
                  >
                    <Typography.Paragraph style={{ marginBottom: 8, fontSize: 15 }}>
                      {meta.applyMission}
                    </Typography.Paragraph>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Học → Làm trên ca → Quản lý có thể ghi nhận. Không cần báo cáo dài.
                    </Typography.Text>
                    {isL4 || meta.kind === 'checklist' ? (
                      <div style={{ marginTop: 8 }}>
                        <Link to="/success/shift-checklist">
                          <Button type="primary" size="small">
                            Mở checklist ca
                          </Button>
                        </Link>
                      </div>
                    ) : null}
                  </Card>
                ) : null}

                {!progress && detail.questions.length > 0 ? (
                  <Alert
                    type="warning"
                    showIcon
                    message="Chưa bắt đầu lộ trình"
                    description={
                      <span>
                        Vào <Link to="/people/learn">Học bài</Link> bấm «Bắt đầu học» trước — chỉ một
                        lần.
                      </span>
                    }
                  />
                ) : null}

                {progress?.status === 'passed' && !practiceQuiz ? (
                  <Space direction="vertical" style={{ width: '100%' }} size={12}>
                    <Alert
                      type="success"
                      showIcon
                      message={`Đã hoàn thành${progress.scorePct != null ? ` (${progress.scorePct}%)` : ''} — thành tích đã được ghi nhận.`}
                    />
                    {detail.requireObservation || progress.requireObservation ? (
                      progress.observedAt ? (
                        <Alert
                          type="success"
                          showIcon
                          message="Đã áp dụng tại quầy"
                          description={`Quản lý ${progress.observerName ?? ''} xác nhận ${new Date(progress.observedAt).toLocaleString('vi-VN')}. Không khóa bán hàng.`}
                        />
                      ) : (
                        <Alert
                          type="info"
                          showIcon
                          message="Chờ quan sát tại quầy"
                          description="Bạn đã đạt câu hỏi kiểm tra. Quản lý sẽ quan sát trên ca thật rồi ghi nhận «Đã áp dụng tại quầy». Có thể học bài tiếp — không bị khóa."
                        />
                      )
                    ) : null}
                    {isL4 ? <L4PracticeCard checklist={checklist} /> : null}
                    <Button
                      size="large"
                      block
                      onClick={() => {
                        const shuffled = shuffleQuizForPractice(detail.questions);
                        setShuffledQuestions(shuffled);
                        setPracticeQuiz(true);
                        setAnswers(Array(shuffled.length).fill(-1));
                        setLastQuizFailed(false);
                      }}
                    >
                      Làm lại để nhớ
                    </Button>
                    {nextModuleId ? (
                      <Button
                        type="primary"
                        size="large"
                        block
                        onClick={() => navigate(`/people/learn/modules/${nextModuleId}`)}
                      >
                        Học bài tiếp: {nextModuleTitle}
                      </Button>
                    ) : (
                      <Button
                        size="large"
                        block
                        icon={<ArrowLeftOutlined />}
                        onClick={() => navigate('/people/learn')}
                      >
                        Về danh sách bài học
                      </Button>
                    )}
                  </Space>
                ) : null}
              </Space>
            </div>
          </>
        ) : null}
      </Card>

      {detail && nextModuleId && progress?.status !== 'passed' ? (
        <div
          style={{
            position: 'sticky',
            bottom: 0,
            zIndex: 10,
            padding: '10px 12px',
            background: '#fff',
            borderTop: '1px solid #f0f0f0',
            boxShadow: '0 -4px 12px rgba(0,0,0,0.04)',
            display: 'flex',
            justifyContent: 'flex-end',
            gap: 8,
          }}
        >
          <Button
            type="primary"
            icon={<ArrowRightOutlined />}
            onClick={() => navigate(`/people/learn/modules/${nextModuleId}`)}
          >
            Bài tiếp: {nextModuleTitle}
          </Button>
        </div>
      ) : null}
    </Space>
  );
}
