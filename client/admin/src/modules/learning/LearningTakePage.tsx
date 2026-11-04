import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  App,
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  Form,
  Input,
  Progress,
  Radio,
  Row,
  Space,
  Typography,
} from 'antd';
import {
  CheckSquareOutlined,
  FileTextOutlined,
  PlayCircleOutlined,
  QuestionCircleOutlined,
  ReadOutlined,
  SolutionOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchMyEvaluations,
  fetchMyHabits,
  fetchMyLearning,
  fetchMyLearningBadges,
  submitMyEvaluationFeedback,
  type LearningBadge,
  type LearningEvaluation,
  type LearningMyHabits,
  type LearningMyLearning,
} from '@/shared/api/learning.api';
import { fetchShiftChecklistToday, type ShiftChecklistToday } from '@/shared/api/success.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCanAccessSuccessModule, useCanLearningWrite } from '@/shared/auth/usePermission';
import { PeoplePageHint } from '@/modules/learning/PeopleModuleIntro';
import { PEOPLE_CONTENT_MAX } from '@/modules/learning/people-ui';
import { LearningPathSteps } from '@/modules/learning/LearningPathSteps';
import LearningMonthlyDrillCard from '@/modules/learning/LearningMonthlyDrillCard';
import { PeopleTrainTabs } from '@/modules/learning/PeopleTrainTabs';
import { NovixaPeopleCycle } from '@/modules/learning/NovixaPeopleCycle';
import { STORE_RULES_MODULE_CODE, STORE_RULES_MODULE_ID } from '@/modules/learning/learning-module-ids';
import { resolveLessonMeta } from '@/modules/learning/learning-module-meta';
import { CompetencyProfilePanel } from '@/modules/learning/CompetencyProfilePanel';

const LEARN_FLOW = [
  { key: 'read', label: 'Đọc', icon: <FileTextOutlined />, color: '#1677ff' },
  { key: 'quiz', label: 'Kiểm tra', icon: <QuestionCircleOutlined />, color: '#722ed1' },
  { key: 'apply', label: 'Làm bài ca', icon: <CheckSquareOutlined />, color: '#13c2c2' },
  { key: 'confirm', label: 'Được xác nhận', icon: <SolutionOutlined />, color: '#52c41a' },
] as const;

export function LearningTakePage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const username = useAuthStore((s) => s.user?.username) ?? 'bạn';
  const canWrite = useCanLearningWrite();
  const canSuccess = useCanAccessSuccessModule();
  const [mine, setMine] = useState<LearningMyLearning | null>(null);
  const [badges, setBadges] = useState<LearningBadge[]>([]);
  const [evals, setEvals] = useState<LearningEvaluation[]>([]);
  const [habits, setHabits] = useState<LearningMyHabits | null>(null);
  const [checklist, setChecklist] = useState<ShiftChecklistToday | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingFb, setSavingFb] = useState(false);

  const reload = async () => {
    setLoading(true);
    try {
      const [me, b, ev, h] = await Promise.all([
        fetchMyLearning(),
        fetchMyLearningBadges().catch(() => [] as LearningBadge[]),
        fetchMyEvaluations().catch(() => [] as LearningEvaluation[]),
        fetchMyHabits().catch(() => null),
      ]);
      setMine(me);
      setBadges(b);
      setEvals(ev);
      setHabits(h);
      if (canSuccess) {
        try {
          setChecklist(await fetchShiftChecklistToday());
        } catch {
          setChecklist(null);
        }
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được bài học'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const goAssignOrWait = () => {
    if (canWrite) {
      navigate('/people/enrollments');
      return;
    }
    message.info('Chờ quản lý giao lộ trình. Sau khi được gán, bạn học tiếp tại đây.');
  };

  const enrollment = mine?.enrollment;
  const modules = mine?.modules ?? [];
  const otherPrograms = (mine?.programs ?? []).filter(
    (p) => p.enrollment && p.enrollment.id !== enrollment?.id,
  );
  const nextModule =
    modules.find((m) => m.status !== 'passed') ?? modules[modules.length - 1] ?? null;
  const pct =
    enrollment && enrollment.modulesTotal > 0
      ? Math.round((100 * enrollment.modulesPassed) / enrollment.modulesTotal)
      : 0;
  const allDone =
    !!enrollment &&
    enrollment.modulesTotal > 0 &&
    enrollment.modulesPassed >= enrollment.modulesTotal;
  const pendingFeedback = evals.find((e) => !e.employeeRespondedAt) ?? null;
  const rulesModule =
    modules.find((m) => m.moduleCode === STORE_RULES_MODULE_CODE) ??
    modules.find((m) => m.moduleId === STORE_RULES_MODULE_ID) ??
    null;
  const rulesDone = rulesModule?.status === 'passed';

  const nextMeta = useMemo(
    () =>
      nextModule
        ? resolveLessonMeta({
            moduleCode: nextModule.moduleCode,
            levelCode: nextModule.levelCode,
            title: nextModule.title,
            requireAck: nextModule.requireAck,
          })
        : null,
    [nextModule],
  );

  const remainingMinutes = useMemo(() => {
    return modules
      .filter((m) => m.status !== 'passed')
      .reduce(
        (sum, m) =>
          sum +
          resolveLessonMeta({
            moduleCode: m.moduleCode,
            levelCode: m.levelCode,
            title: m.title,
          }).minutes,
        0,
      );
  }, [modules]);

  const missionItems = useMemo(() => {
    const items: { done: boolean; text: string }[] = [];
    items.push({
      done: rulesDone,
      text: rulesDone ? 'Đã hoàn thành onboarding L0' : 'Onboarding: sẵn sàng ca đầu tiên',
    });
    if (!enrollment) {
      items.push({ done: false, text: 'Chờ quản lý giao lộ trình' });
    } else if (!allDone && nextModule) {
      items.push({
        done: false,
        text: `Học ${nextModule.levelCode}: ${nextModule.title}`,
      });
      if ((nextModule.requireAck || (nextMeta?.kindLabel ?? '').includes('Quiz')) && nextModule.status !== 'passed') {
        items.push({ done: false, text: 'Làm câu hỏi kiểm tra / xác nhận đã hiểu' });
      }
    } else {
      items.push({ done: true, text: 'Hoàn thành lộ trình học' });
    }
    if (habits && !habits.closedToday) {
      items.push({ done: false, text: 'Tick checklist đóng ca' });
    } else if (habits?.closedToday) {
      items.push({ done: true, text: 'Đã đóng ca hôm nay' });
    }
    return items.slice(0, 4);
  }, [rulesDone, enrollment, allDone, nextModule, nextMeta, habits]);

  const todayReward = useMemo(() => {
    const lessonXp = nextModule && !allDone ? 20 : 0;
    const badgeHint = nextModule && !allDone ? 1 : 0;
    const lessonHint = nextModule && !allDone ? 1 : 0;
    return { lessonXp, badgeHint, lessonHint };
  }, [nextModule, allDone]);

  const closeStatus = checklist?.close;

  const situationalTips = useMemo(() => {
    const tips: string[] = [];
    if (habits && !habits.closedToday) {
      tips.push('Checklist cuối ca còn thiếu → nên ôn bài Đóng ca / L4 và tick checklist thật.');
    }
    if (modules.some((m) => m.moduleCode?.includes('fefo') && m.status === 'failed')) {
      tips.push('Câu hỏi kiểm tra xuất hàng gần hết hạn chưa đạt → xem lại bài trước khi lấy hàng.');
    }
    if (modules.some((m) => m.moduleCode?.includes('crm') && m.status !== 'passed')) {
      tips.push('Chưa xong bài chăm sóc khách → khi bán, nhớ hỏi số điện thoại / giới thiệu điểm thưởng.');
    }
    return tips.slice(0, 2);
  }, [habits, modules]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: PEOPLE_CONTENT_MAX }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          gap: 12,
          flexWrap: 'wrap',
          alignItems: 'flex-start',
        }}
      >
        <div style={{ flex: 1, minWidth: 200 }}>
          <Space align="center" size={10} style={{ marginBottom: 4 }}>
            <ReadOutlined style={{ fontSize: 26, color: '#1677ff' }} />
            <Typography.Title level={3} style={{ margin: 0 }}>
              {canWrite ? 'Đào tạo' : 'Học bài'}
            </Typography.Title>
          </Space>
          <PeoplePageHint>
            {canWrite
              ? 'Học bài đã giao cho bạn; giao lộ trình đội ở tab «Giao đào tạo».'
              : 'Học theo lộ trình quản lý đã giao — không khóa bán hàng.'}
          </PeoplePageHint>
        </div>
        <PeopleTrainTabs />
      </div>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 0, borderRadius: 10 }}
        message={
          <span>
            <Typography.Text strong>Bài học L0–L6 · Bậc nghề 1–5</Typography.Text>
            <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
              Học kỹ năng → Làm đúng trên ca → Được xác nhận → Xét lên bậc
            </Typography.Text>
          </span>
        }
      />

      <LearningMonthlyDrillCard />

      <div>
        <Typography.Text strong style={{ display: 'block', marginBottom: 12, fontSize: 15 }}>
          Hành trình học
        </Typography.Text>
        <Row gutter={[12, 12]}>
          {LEARN_FLOW.map((step) => (
            <Col xs={12} sm={6} key={step.key}>
              <div
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 8,
                  minHeight: 96,
                  padding: '16px 10px',
                  borderRadius: 12,
                  background: '#fff',
                  border: '1px solid #f0f0f0',
                  boxShadow: '0 1px 2px rgba(0,0,0,0.03)',
                }}
              >
                <span
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: 10,
                    background: `${step.color}14`,
                    color: step.color,
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 20,
                  }}
                >
                  {step.icon}
                </span>
                <Typography.Text strong style={{ fontSize: 13 }}>
                  {step.label}
                </Typography.Text>
              </div>
            </Col>
          ))}
        </Row>
      </div>

      <Row gutter={[16, 16]} align="stretch">
        <Col xs={24} lg={15}>
          <Card
            loading={loading}
            style={{
              height: '100%',
              borderRadius: 12,
              background: 'linear-gradient(135deg, #f0f5ff 0%, #f9f0ff 55%, #fff 100%)',
              borderColor: '#d6e4ff',
            }}
            styles={{ body: { height: '100%', display: 'flex', flexDirection: 'column' } }}
          >
            <Typography.Title level={4} style={{ marginTop: 0, marginBottom: 8 }}>
              Chào {username}!
            </Typography.Title>
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Cần làm
            </Typography.Text>
            <Space direction="vertical" size={6} style={{ width: '100%', marginBottom: 12, flex: 1 }}>
              {missionItems.map((item) => (
                <Typography.Text
                  key={item.text}
                  type={item.done ? 'secondary' : undefined}
                  style={{ textDecoration: item.done ? 'line-through' : undefined }}
                >
                  • {item.text}
                </Typography.Text>
              ))}
            </Space>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
              Dự kiến: ~
              {Math.max(
                nextMeta?.minutes ?? 8,
                remainingMinutes ? Math.min(remainingMinutes, 25) : 8,
              )}{' '}
              phút
              {todayReward.lessonXp > 0
                ? ` · Nếu xong: +${todayReward.lessonXp} điểm thưởng · +${todayReward.lessonHint} bài`
                : ''}
            </Typography.Text>
            <Button
              type="primary"
              size="large"
              block
              icon={<PlayCircleOutlined />}
              style={{ borderRadius: 10, height: 44 }}
              onClick={() => {
                if (!enrollment) {
                  goAssignOrWait();
                  return;
                }
                if (!rulesDone && rulesModule) {
                  navigate(`/people/learn/modules/${rulesModule.moduleId}`);
                  return;
                }
                if (nextModule && !allDone) {
                  navigate(`/people/learn/modules/${nextModule.moduleId}`);
                  return;
                }
                if (habits && !habits.closedToday && canSuccess) {
                  navigate('/success/shift-checklist');
                  return;
                }
                document.getElementById('learn-path')?.scrollIntoView({ behavior: 'smooth' });
              }}
            >
              {!enrollment
                ? canWrite
                  ? 'Giao lộ trình cho nhân viên'
                  : 'Chờ quản lý giao lộ trình'
                : !rulesDone
                  ? 'Bắt đầu: Onboarding ca đầu tiên'
                  : nextModule && !allDone
                    ? `Bắt đầu: ${nextModule.title}`
                    : habits && !habits.closedToday && canSuccess
                      ? 'Bắt đầu: Checklist đóng ca'
                      : 'Xem lại lộ trình'}
            </Button>
          </Card>
        </Col>
        <Col xs={24} lg={9}>
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Card size="small" styles={{ body: { padding: '14px 16px' } }} style={{ borderRadius: 12 }}>
              <Typography.Text type="secondary">Điểm thưởng dự kiến</Typography.Text>
              <Typography.Title level={2} style={{ margin: '4px 0 0', color: '#1677ff' }}>
                +{todayReward.lessonXp || 0}
              </Typography.Title>
            </Card>
            <Row gutter={[10, 10]}>
              <Col span={12}>
                <Card size="small" styles={{ body: { textAlign: 'center', padding: 14 } }} style={{ borderRadius: 12 }}>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Thành tích
                  </Typography.Text>
                  <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#faad14' }}>
                    {badges.filter((b) => !/^complete_l\d$/i.test(b.badgeCode)).length}
                  </Typography.Title>
                </Card>
              </Col>
              <Col span={12}>
                <Card size="small" styles={{ body: { textAlign: 'center', padding: 14 } }} style={{ borderRadius: 12 }}>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Bài đã đạt
                  </Typography.Text>
                  <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#52c41a' }}>
                    {enrollment?.modulesPassed ?? 0}
                  </Typography.Title>
                </Card>
              </Col>
            </Row>
            {canSuccess ? (
              <Card size="small" style={{ borderRadius: 12 }} styles={{ body: { padding: '14px 16px' } }}>
                <Space style={{ width: '100%', justifyContent: 'space-between' }} align="center">
                  <div>
                    <Typography.Text strong>Checklist cuối ca</Typography.Text>
                    <div>
                      <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                        {closeStatus
                          ? `${closeStatus.checkedCount}/${closeStatus.totalCount || 0} mục`
                          : habits?.closedToday
                            ? 'Đã đóng hôm nay'
                            : '0/0 mục'}
                      </Typography.Text>
                    </div>
                  </div>
                  <Link to="/success/shift-checklist">
                    <Button
                      type={habits?.closedToday ? 'default' : 'primary'}
                      size="small"
                      icon={<PlayCircleOutlined />}
                    >
                      {habits?.closedToday ? 'Xem lại' : 'Bắt đầu'}
                    </Button>
                  </Link>
                </Space>
              </Card>
            ) : null}
          </Space>
        </Col>
      </Row>

      {/* Progress lộ trình */}
      <Card
        id="learn-path"
        loading={loading}
        style={{ borderRadius: 12 }}
        title={
          <div>
            <div>Lộ trình của bạn</div>
            <Typography.Text type="secondary" style={{ fontSize: 12, fontWeight: 400 }}>
              Chỉ học bài quản lý đã giao — không tự ghi danh
            </Typography.Text>
          </div>
        }
      >
        {!enrollment ? (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Typography.Text>
              {canWrite
                ? 'Bạn chưa được gán lộ trình cho tài khoản này. Giao đào tạo cho nhân viên (hoặc chính bạn) tại tab «Giao đào tạo».'
                : 'Chưa có lộ trình. Nhờ quản lý gán bài tại «Giao đào tạo», rồi quay lại đây để học.'}
            </Typography.Text>
            {canWrite ? (
              <Button type="primary" onClick={() => navigate('/people/enrollments')}>
                Mở Giao đào tạo
              </Button>
            ) : null}
          </Space>
        ) : (
          <Space direction="vertical" style={{ width: '100%' }} size={16}>
            <div>
              <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                <Typography.Text strong>Tiến độ lộ trình</Typography.Text>
                <Typography.Text strong style={{ color: allDone ? '#52c41a' : '#1677ff' }}>
                  {pct}%
                </Typography.Text>
              </Space>
              <Progress
                percent={pct}
                status={allDone ? 'success' : 'active'}
                showInfo={false}
                size={['100%', 12]}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {enrollment.modulesPassed}/{enrollment.modulesTotal} bài
                {!allDone && remainingMinutes > 0 ? ` · còn khoảng ${remainingMinutes} phút` : ''}
              </Typography.Text>
            </div>
            {nextModule && !allDone ? (
              <Button
                type="primary"
                size="large"
                block
                onClick={() => navigate(`/people/learn/modules/${nextModule.moduleId}`)}
              >
                Tiếp tục: {nextModule.title}
              </Button>
            ) : null}
            {allDone ? (
              <Alert
                type="success"
                showIcon
                message="Bạn đã hoàn thành lộ trình — giỏi lắm!"
                description="Muốn đọc lại: bấm «Đọc lại bài này» bên dưới từng bài trong danh sách."
              />
            ) : null}
            <LearningPathSteps
              modules={modules.map((m) => ({
                moduleId: m.moduleId,
                title: m.title,
                levelCode: m.levelCode,
                status: m.status,
                moduleCode: m.moduleCode,
                requireAck: m.requireAck,
                requireObservation: m.requireObservation,
                observedAt: m.observedAt,
              }))}
              nextModuleId={allDone ? null : nextModule?.moduleId}
              onOpen={(id) => navigate(`/people/learn/modules/${id}`)}
            />
          </Space>
        )}
        {otherPrograms.length ? (
          <Space direction="vertical" style={{ width: '100%', marginTop: 16 }} size={8}>
            <Typography.Text strong>Lộ trình khác đã được giao</Typography.Text>
            {otherPrograms.map((p) => {
              const next = p.modules.find((m) => m.status !== 'passed');
              return (
                <Space
                  key={p.program.id}
                  style={{ width: '100%', justifyContent: 'space-between' }}
                  wrap
                >
                  <Typography.Text>
                    {p.program.title} · {p.enrollment?.modulesPassed ?? 0}/{p.enrollment?.modulesTotal ?? 0} bài
                  </Typography.Text>
                  {next ? (
                    <Button size="small" onClick={() => navigate(`/people/learn/modules/${next.moduleId}`)}>
                      Học tiếp: {next.title}
                    </Button>
                  ) : (
                    <Typography.Text type="success">Đã xong</Typography.Text>
                  )}
                </Space>
              );
            })}
          </Space>
        ) : null}
      </Card>

      <CompetencyProfilePanel
        badges={badges}
        modules={modules}
        loading={loading}
      />

      {/* Gợi ý theo tình huống — rule-based, chưa AI */}
      {situationalTips.length ? (
        <Alert
          type="warning"
          showIcon
          message="Gợi ý học theo tình huống"
          description={
            <ul style={{ margin: '4px 0 0', paddingLeft: 18 }}>
              {situationalTips.map((t) => (
                <li key={t}>{t}</li>
              ))}
            </ul>
          }
        />
      ) : null}

      <div id="feedback">
        <Card title="Trao đổi hai chiều (đánh giá tháng)" size="small" loading={loading}>
          {!pendingFeedback ? (
            <Typography.Text type="secondary">
              {evals.length
                ? 'Bạn đã trao đổi các đánh giá gần đây. Khi có chấm tháng mới, ô này sẽ mở lại.'
                : 'Chưa có đánh giá tháng — khi quản lý chấm, bạn đọc nhận xét → ý kiến → cam kết → cảm nhận công việc.'}
            </Typography.Text>
          ) : (
            <Space direction="vertical" style={{ width: '100%' }} size={12}>
              <Alert
                type="info"
                showIcon
                message={`Quản lý đã đánh giá ${pendingFeedback.periodMonth}/${pendingFeedback.periodYear}`}
                description={
                  <span>
                    Điểm năng lực: <strong>{pendingFeedback.averageScore}</strong>
                    {pendingFeedback.comment ? (
                      <>
                        <br />
                        Nhận xét: {pendingFeedback.comment}
                      </>
                    ) : null}
                  </span>
                }
              />
              <Form
                layout="vertical"
                initialValues={{
                  employeeFeedback: pendingFeedback.employeeFeedback ?? '',
                  engagementPulse: pendingFeedback.engagementPulse ?? 4,
                  smartGoals: [] as string[],
                }}
                onFinish={async (v) => {
                  const goals = (v.smartGoals as string[] | undefined) ?? [];
                  const goalLabels: Record<string, string> = {
                    l3: 'Hoàn thành bậc / bài tiếp theo',
                    crm: 'Chăm sóc khách / gắn điểm tốt hơn',
                    checklist: 'Không quên checklist đóng ca',
                    fefo: 'Xuất trước hàng gần hết hạn khi lấy hàng',
                    other: v.goalOther?.trim() || '',
                  };
                  const nextMonthGoal = [
                    ...goals.map((k) => goalLabels[k]).filter(Boolean),
                    v.goalOther?.trim(),
                  ]
                    .filter(Boolean)
                    .join('; ');
                  if (!nextMonthGoal) {
                    message.warning('Chọn ít nhất 1 cam kết tháng sau');
                    return;
                  }
                  setSavingFb(true);
                  try {
                    await submitMyEvaluationFeedback(pendingFeedback.id, {
                      employeeFeedback: v.employeeFeedback,
                      nextMonthGoal,
                      engagementPulse: v.engagementPulse,
                    });
                    message.success('Đã gửi trao đổi & cam kết');
                    await reload();
                  } catch (e) {
                    message.error(apiErrorMessage(e, 'Không gửi được'));
                  } finally {
                    setSavingFb(false);
                  }
                }}
              >
                <Form.Item
                  name="employeeFeedback"
                  label="Ý kiến của bạn (đồng ý / cần hỗ trợ gì?)"
                  rules={[{ required: true, message: 'Nhập vài câu' }]}
                >
                  <Input.TextArea
                    rows={3}
                    placeholder="Vd: em đồng ý, cần được kèm thêm bán hàng tại quầy…"
                  />
                </Form.Item>
                <Form.Item
                  name="smartGoals"
                  label="Cam kết tháng sau (cụ thể · đo được)"
                  rules={[{ required: true, message: 'Chọn ít nhất 1 mục' }]}
                >
                  <Checkbox.Group style={{ width: '100%' }}>
                    <Space direction="vertical">
                      <Checkbox value="l3">Hoàn thành bậc / bài tiếp theo</Checkbox>
                      <Checkbox value="crm">Chăm sóc khách / gắn điểm tốt hơn</Checkbox>
                      <Checkbox value="checklist">Không quên checklist đóng ca</Checkbox>
                      <Checkbox value="fefo">Xuất trước hàng gần hết hạn khi lấy hàng</Checkbox>
                    </Space>
                  </Checkbox.Group>
                </Form.Item>
                <Form.Item name="goalOther" label="Cam kết khác (tuỳ chọn)">
                  <Input placeholder="Vd: kèm 1 bạn mới…" maxLength={200} />
                </Form.Item>
                <Form.Item
                  name="engagementPulse"
                  label="❤️ Cảm nhận công việc hôm nay? (vài giây)"
                  rules={[{ required: true, message: 'Chọn cảm xúc' }]}
                >
                  <Radio.Group optionType="button" buttonStyle="solid" size="large">
                    <Radio.Button value={5}>😊 Rất tốt</Radio.Button>
                    <Radio.Button value={3}>😐 Bình thường</Radio.Button>
                    <Radio.Button value={1}>☹ Khó khăn</Radio.Button>
                  </Radio.Group>
                </Form.Item>
                <Button type="primary" htmlType="submit" loading={savingFb} size="large" block>
                  Gửi trao đổi
                </Button>
              </Form>
            </Space>
          )}
        </Card>
      </div>

      <Card size="small" style={{ borderRadius: 12 }}>
        <NovixaPeopleCycle activeKey="train" />
      </Card>
    </Space>
  );
}
