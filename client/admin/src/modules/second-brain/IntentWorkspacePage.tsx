import { useEffect, useState } from 'react';
import {
  Alert, Button, Card, Descriptions, Input, Space, Table, Tag, Typography,
} from 'antd';
import { intentApi } from './intent.api';
import { taskApi, type PaperTask } from './task.api';
import { tradingApi } from './trading.api';
import { intentRuntimeNote, presentIntent, secondBrainAccess, type IntentInput, type IntentPhase } from './intent-workspace';
import { useAuthStore } from '@/shared/auth/auth.store';

const { TextArea } = Input;
const { Paragraph, Title } = Typography;

function taskNext(task: PaperTask): string {
  if (task.status === 'waiting_approval') return 'Chờ người có quyền phê duyệt hoặc từ chối. Chưa có paper execution.';
  if (task.status === 'completed') return 'Đã xác minh decision accepted và đúng một execution PAPER.';
  if (task.status === 'blocked') return 'Decision đã bị từ chối. Không có paper execution.';
  return `Chưa xác minh được kết quả (${task.verificationNote || 'unverified'}). Không coi đây là từ chối.`;
}

function phaseColor(phase: IntentPhase): string {
  if (phase === 'completed') return 'blue';
  if (phase === 'partial' || phase === 'needs_clarification' || phase === 'waiting') return 'gold';
  if (phase === 'idle') return 'default';
  return 'red';
}

function errorInput(error: unknown): IntentInput {
  const err = error as { response?: { status?: number; data?: { message?: string } }; message?: string };
  const code = err.response?.status ?? 0;
  const message = err.response?.data?.message;
  if (code === 401) return { status: 'http', code, message: message || 'Phiên đăng nhập đã hết hạn.' };
  if (code === 403) return { status: 'http', code, message: message || 'Không có quyền hoặc module chưa được bật.' };
  if (!err.response) return { status: 'http', code, message: 'API không phản hồi. Chưa có kết luận nghiệp vụ.' };
  return { status: 'http', code, message: message || 'API không trả bằng chứng.' };
}

export function IntentWorkspacePage() {
  const [utterance, setUtterance] = useState('');
  const [input, setInput] = useState<IntentInput>({ status: 'idle' });
  const [runtimeNote, setRuntimeNote] = useState('Đang đọc cấu hình model và publication.');
  const [sourceLabel, setSourceLabel] = useState('');
  const [tasks, setTasks] = useState<PaperTask[]>([]);
  const [taskNote, setTaskNote] = useState('');
  const [decisionRead, setDecisionRead] = useState<{ status: string; paperExecutionCount: number; operationKey: string | null } | null>(null);
  const [decisionError, setDecisionError] = useState('');
  const [deciding, setDeciding] = useState(false);
  const view = presentIntent(input);
  const waiting = input.status === 'waiting';
  const roles = useAuthStore((state) => state.user?.roles ?? []);
  const permissions = useAuthStore((state) => state.user?.permissions ?? []);
  const canApprove = secondBrainAccess(roles, permissions).canApprove;

  useEffect(() => {
    let cancelled = false;
    intentApi.status()
      .then((status) => {
        if (!cancelled) {
          setRuntimeNote(intentRuntimeNote(status));
          setSourceLabel(status.sourceLabel?.trim() || '');
        }
      })
      .catch(() => {
        if (!cancelled) setRuntimeNote('Không đọc được cấu hình model và publication. Chưa có kết luận nghiệp vụ.');
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    taskApi.list()
      .then((rows) => { if (!cancelled) setTasks(rows); })
      .catch(() => { if (!cancelled) setTaskNote('Không đọc được danh sách task.'); });
    return () => { cancelled = true; };
  }, [view.storedDecision?.id]);

  const decisionId = view.storedDecision?.id ?? '';
  useEffect(() => {
    if (!decisionId) {
      setDecisionRead(null);
      return;
    }
    let cancelled = false;
    tradingApi.decision(decisionId)
      .then((detail) => {
        if (cancelled) return;
        setDecisionRead({
          status: detail.decision.status,
          paperExecutionCount: detail.paperExecutionCount ?? 0,
          operationKey: detail.decision.operationKey,
        });
        setDecisionError('');
      })
      .catch(() => {
        if (!cancelled) setDecisionError('Không đọc lại được quyết định đã lưu.');
      });
    return () => {
      cancelled = true;
    };
  }, [decisionId]);

  async function decide(action: 'approve' | 'reject') {
    if (!decisionId || deciding) return;
    setDeciding(true);
    setDecisionError('');
    try {
      if (action === 'approve') await tradingApi.approvePaper(decisionId);
      else await tradingApi.rejectPaper(decisionId);
    } catch (error) {
      const err = error as { response?: { data?: { message?: string } } };
      setDecisionError(err.response?.data?.message || 'Thao tác trên quyết định không thành công.');
    }
    try {
      const detail = await tradingApi.decision(decisionId);
      setDecisionRead({
        status: detail.decision.status,
        paperExecutionCount: detail.paperExecutionCount ?? 0,
        operationKey: detail.decision.operationKey,
      });
      const rows = await taskApi.list();
      setTasks(rows);
    } catch {
      setDecisionError((current) => current || 'Không đọc lại được quyết định sau thao tác.');
    } finally {
      setDeciding(false);
    }
  }

  const readStatus = decisionRead?.status ?? view.storedDecision?.status ?? '';
  const readCount = decisionRead?.paperExecutionCount;

  async function submit() {
    const text = utterance.trim();
    if (!text || waiting) return;
    setInput({ status: 'waiting' });
    setDecisionRead(null);
    setDecisionError('');
    try {
      const evidence = await intentApi.submit(text);
      setInput({ status: 'evidence', evidence });
    } catch (error) {
      setInput(errorInput(error));
    }
  }

  return (
    <Space direction="vertical" size={16} style={{ display: 'flex', padding: 16 }}>
      <div>
        <Title level={3} style={{ marginBottom: 4 }}>Yêu cầu</Title>
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Câu trả lời bên dưới là bằng chứng API trả về. Gửi thành công chưa phải là yêu cầu đã hoàn thành.
        </Paragraph>
        <Paragraph type="secondary">{runtimeNote}</Paragraph>
      </div>
      <Card>
        <Space direction="vertical" size={12} style={{ display: 'flex' }}>
          <TextArea
            value={utterance}
            onChange={(event) => setUtterance(event.target.value)}
            rows={4}
            maxLength={4000}
            placeholder="Viết yêu cầu bằng ngôn ngữ tự nhiên"
            disabled={waiting}
          />
          <Button type="primary" onClick={() => void submit()} disabled={waiting || utterance.trim().length === 0}>
            {waiting ? 'Đang chờ API' : view.retryable ? 'Thử lại' : 'Gửi yêu cầu'}
          </Button>
        </Space>
      </Card>

      <Card>
        <Space direction="vertical" size={12} style={{ display: 'flex' }}>
          <Space>
            <Tag color={phaseColor(view.phase)}>{view.phaseLabel}</Tag>
            <Tag>outcome: {view.backendOutcome}</Tag>
            {view.understood ? <Tag>Đã hiểu</Tag> : <Tag>Chưa xác nhận ý định</Tag>}
          </Space>
          {waiting ? (
            <Alert type="info" showIcon message="Đang chờ API. Chưa có kết luận nghiệp vụ." />
          ) : null}
          {view.phase === 'needs_clarification' ? (
            <Alert type="warning" showIcon message="Chưa chọn hành động" description={view.clarification} />
          ) : null}
          {view.phase === 'model_unavailable' || view.phase === 'unreadable' || view.phase === 'rejected' || view.phase === 'transport_failed' ? (
            <Alert type="error" showIcon message={view.phaseLabel} description={view.nextStep} />
          ) : null}
          <Descriptions column={1} size="small">
            <Descriptions.Item label={view.goalConfirmed ? 'Mục tiêu' : 'Diễn giải chưa xác nhận'}>{view.goal || '—'}</Descriptions.Item>
            <Descriptions.Item label="Capability được chọn">
              {view.selectedCapabilities.length > 0 ? view.selectedCapabilities.join(', ') : 'Không có capability được chọn để chạy'}
            </Descriptions.Item>
            <Descriptions.Item label="Điều kiện hoàn thành">
              {view.completionConditions.length > 0 ? view.completionConditions.join(' · ') : '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Giới hạn">{view.limit || '—'}</Descriptions.Item>
            <Descriptions.Item label="Bước tiếp theo">{view.nextStep || '—'}</Descriptions.Item>
          </Descriptions>
        </Space>
      </Card>

      <Card title="Thông tin còn thiếu">
        {view.missing.length === 0 ? 'Không có mục nào được API ghi là còn thiếu.' : (
          <Table
            size="small"
            pagination={false}
            rowKey={(row) => `${row.slot}-${row.why}`}
            dataSource={view.missing}
            columns={[
              { title: 'Mục', dataIndex: 'slot' },
              { title: 'Vì sao', dataIndex: 'why' },
            ]}
          />
        )}
      </Card>

      <Card title="Quyết định của người">
        {view.humanDecisions.length === 0 ? 'API không ghi quyết định nào cần người thực hiện.' : (
          <Table
            size="small"
            pagination={false}
            rowKey={(row) => `${row.capabilityId}-${row.why}`}
            dataSource={view.humanDecisions}
            columns={[
              { title: 'Capability', dataIndex: 'capabilityId' },
              {
                title: 'Trong catalog',
                dataIndex: 'inCatalog',
                render: (value: boolean) => (value ? 'có' : 'không, không được chạy'),
              },
              { title: 'Việc cần người quyết', dataIndex: 'why' },
            ]}
          />
        )}
      </Card>

      <Card title="Kế hoạch">
        <Table
          size="small"
          pagination={false}
          rowKey={(row) => `${row.capabilityId}-${row.disposition}-${row.reason}`}
          dataSource={view.plan}
          columns={[
            { title: 'Capability', dataIndex: 'capabilityId' },
            { title: 'Disposition', dataIndex: 'disposition' },
            { title: 'Chạy handler', dataIndex: 'runHandler', render: (value: boolean) => (value ? 'có' : 'không') },
            { title: 'Lý do', dataIndex: 'reason' },
          ]}
        />
      </Card>

      <Card title="Bằng chứng thực thi">
        {view.sideEffect ? (
          <Alert type="error" showIcon message="API báo có ghi dữ liệu hoặc gọi ra ngoài. Giao diện không coi đây là hoàn thành." />
        ) : null}
        {view.facts.some((fact) => fact.label === 'publicationId') && sourceLabel ? (
          <Alert
            type="info"
            showIcon
            message={`Nguồn publication: ${sourceLabel}. Đây là cấu hình local, không phải dữ liệu nghiệp vụ của tenant.`}
          />
        ) : null}
        {view.facts.length === 0 ? 'Chưa có bằng chứng thực thi.' : (
          <Descriptions column={1} size="small">
            {view.facts.map((fact) => (
              <Descriptions.Item key={`${fact.label}-${fact.value}`} label={fact.label}>{fact.value}</Descriptions.Item>
            ))}
          </Descriptions>
        )}
      </Card>

      <Card title="Công việc đã lưu">
        {taskNote ? <Alert type="error" showIcon message={taskNote} /> : null}
        {tasks.length === 0 ? 'Chưa có task. Câu đọc một lần và câu chưa đủ căn cứ không tạo task.' : (
          <Space direction="vertical" style={{ display: 'flex' }}>
            {tasks.map((task) => (
              <Descriptions key={task.taskId} column={1} size="small" title={task.goal}>
                <Descriptions.Item label="taskId">{task.taskId}</Descriptions.Item>
                <Descriptions.Item label="Trạng thái task">{task.status}</Descriptions.Item>
                <Descriptions.Item label="decision">{task.decisionStatus || '—'}</Descriptions.Item>
                <Descriptions.Item label="Paper execution">{String(task.paperExecutionCount)}</Descriptions.Item>
                <Descriptions.Item label="Ghi chú xác minh">{task.verificationNote || '—'}</Descriptions.Item>
                <Descriptions.Item label="Bước tiếp">{taskNext(task)}</Descriptions.Item>
                {(task.steps ?? []).map((step) => (
                  <Descriptions.Item key={step.key} label={`${step.key} (${step.kind})`}>
                    {`${step.status}${step.note ? ` · ${step.note}` : ''}${step.dependsOn.length > 0 ? ` · sau ${step.dependsOn.join(', ')}` : ''}`}
                  </Descriptions.Item>
                ))}
              </Descriptions>
            ))}
          </Space>
        )}
      </Card>

      {view.storedDecision ? (
        <Card title="Quyết định đã lưu">
          <Space direction="vertical" style={{ display: 'flex' }}>
            {decisionError ? <Alert type="error" showIcon message={decisionError} /> : null}
            <Descriptions column={1} size="small">
              <Descriptions.Item label="decisionId">{view.storedDecision.id}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái đọc lại">{readStatus || '—'}</Descriptions.Item>
              <Descriptions.Item label="Paper execution">{readCount == null ? '—' : String(readCount)}</Descriptions.Item>
              <Descriptions.Item label="operationKey">{decisionRead?.operationKey || view.storedDecision.operationKey || '—'}</Descriptions.Item>
            </Descriptions>
            {readStatus === 'accepted' && readCount === 1 ? (
              <Alert type="info" showIcon message="Đã phê duyệt. Đọc lại được đúng một execution PAPER. Không có lệnh live." />
            ) : null}
            {readStatus === 'rejected' && readCount === 0 ? (
              <Alert type="warning" showIcon message="Đã từ chối. Không có paper execution." />
            ) : null}
            {readStatus === 'accepted' && readCount != null && readCount !== 1 ? (
              <Alert type="error" showIcon message={`Đọc lại thấy ${readCount} execution PAPER. Không ghi nhận hoàn thành.`} />
            ) : null}
            {canApprove && readStatus === 'proposed' ? (
              <Space>
                <Button type="primary" disabled={deciding} onClick={() => void decide('approve')}>Phê duyệt giấy</Button>
                <Button disabled={deciding} onClick={() => void decide('reject')}>Từ chối</Button>
              </Space>
            ) : null}
            {!canApprove && readStatus === 'proposed' ? (
              <Alert type="info" showIcon message="Quyết định đang chờ người có quyền phê duyệt hoặc từ chối." />
            ) : null}
          </Space>
        </Card>
      ) : null}
    </Space>
  );
}
