import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Input,
  List,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createOperationalTask,
  fetchLearningRoster,
  fetchOperationalOpenShifts,
  fetchOperationalTasks,
  updateOperationalTaskStatus,
  type LearningCompetencyRosterItem,
  type OperationalShiftOption,
  type OperationalTask,
} from '@/shared/api/learning.api';
import { useCanLearningWrite } from '@/shared/auth/usePermission';

const STATUS_LABEL: Record<string, string> = {
  open: 'Chưa làm',
  in_progress: 'Đang làm',
  done: 'Xong',
  cancelled: 'Đã hủy',
};

function statusColor(status: string) {
  if (status === 'done') return 'success';
  if (status === 'in_progress') return 'processing';
  if (status === 'cancelled') return 'default';
  return 'warning';
}

export function LearningTasksPage() {
  const { message } = App.useApp();
  const canWrite = useCanLearningWrite();
  const [tasks, setTasks] = useState<OperationalTask[]>([]);
  const [roster, setRoster] = useState<LearningCompetencyRosterItem[]>([]);
  const [shifts, setShifts] = useState<OperationalShiftOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [assigneeId, setAssigneeId] = useState<string>();
  const [title, setTitle] = useState('');
  const [belongsToShift, setBelongsToShift] = useState(false);
  const [shiftId, setShiftId] = useState<string>();
  const [evidenceById, setEvidenceById] = useState<Record<string, string>>({});
  const [showClosed, setShowClosed] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [rows, people, openShifts] = await Promise.all([
        fetchOperationalTasks(),
        canWrite ? fetchLearningRoster().catch(() => []) : Promise.resolve([]),
        canWrite ? fetchOperationalOpenShifts().catch(() => []) : Promise.resolve([]),
      ]);
      setTasks(rows);
      setRoster(people);
      setShifts(openShifts);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được việc trong ca'));
    } finally {
      setLoading(false);
    }
  }, [canWrite, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const visible = useMemo(
    () => tasks.filter((task) => showClosed || (task.status !== 'done' && task.status !== 'cancelled')),
    [showClosed, tasks],
  );

  const assign = async () => {
    const text = title.trim();
    if (!assigneeId || text.length === 0) {
      message.warning('Chọn nhân viên và ghi nội dung việc.');
      return;
    }
    if (belongsToShift && !shiftId) {
      message.warning('Việc thuộc ca thì phải chọn ca đang mở.');
      return;
    }
    setBusyId('create');
    try {
      await createOperationalTask({
        assigneeEmployeeId: assigneeId,
        title: text,
        salesShiftId: belongsToShift ? shiftId : null,
      });
      setTitle('');
      message.success('Đã giao việc.');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không giao được việc'));
    } finally {
      setBusyId(null);
    }
  };

  const changeStatus = async (task: OperationalTask, status: string) => {
    setBusyId(task.id);
    try {
      await updateOperationalTaskStatus(task.id, status, evidenceById[task.id]);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không cập nhật được việc'));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: 880 }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          Việc trong ca
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Việc được giao cho đúng người. Khi thuộc một ca, chọn ca đang mở. Ghi chú hoàn thành là bằng chứng, không phải điểm năng lực.
        </Typography.Paragraph>
      </div>

      {canWrite ? (
        <Card title="Giao việc">
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="Nhân viên nhận việc"
              style={{ width: '100%' }}
              value={assigneeId}
              onChange={setAssigneeId}
              options={roster.map((person) => ({
                value: person.employeeId,
                label: person.employeeName,
              }))}
            />
            <Input
              placeholder="Việc cần làm"
              maxLength={200}
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
            <Checkbox
              checked={belongsToShift}
              onChange={(event) => {
                setBelongsToShift(event.target.checked);
                if (!event.target.checked) setShiftId(undefined);
              }}
            >
              Việc này thuộc một ca đang mở
            </Checkbox>
            {belongsToShift ? (
              shifts.length === 0 ? (
                <Alert
                  type="warning"
                  showIcon
                  message="Chưa có ca đang mở. Mở ca bán trước, hoặc bỏ chọn nếu việc không thuộc ca."
                />
              ) : (
                <Select
                  placeholder="Chọn ca"
                  style={{ width: '100%' }}
                  value={shiftId}
                  onChange={setShiftId}
                  options={shifts.map((shift) => ({
                    value: shift.id,
                    label: `${shift.shiftNumber} · ${shift.warehouseName}`,
                  }))}
                />
              )
            ) : null}
            <Button type="primary" onClick={() => void assign()} loading={busyId === 'create'}>
              Giao việc
            </Button>
          </Space>
        </Card>
      ) : (
        <Alert
          type="info"
          showIcon
          message="Bạn chỉ thấy việc được giao cho mình. Quản lý giao việc và hủy việc."
        />
      )}

      <Card
        title={canWrite ? 'Việc của đội' : 'Việc của tôi'}
        extra={
          <Checkbox checked={showClosed} onChange={(event) => setShowClosed(event.target.checked)}>
            Xem việc đã xong
          </Checkbox>
        }
      >
        <List
          loading={loading}
          dataSource={visible}
          locale={{ emptyText: 'Không có việc đang mở.' }}
          renderItem={(task) => {
            const closed = task.status === 'done' || task.status === 'cancelled';
            const shift = shifts.find((item) => item.id === task.salesShiftId);
            return (
              <List.Item>
                <Space direction="vertical" style={{ width: '100%' }} size={8}>
                  <Space wrap>
                    <Typography.Text strong>{task.title}</Typography.Text>
                    <Tag color={statusColor(task.status)}>{STATUS_LABEL[task.status] ?? task.status}</Tag>
                    {canWrite ? <Typography.Text type="secondary">{task.assigneeName}</Typography.Text> : null}
                    {task.salesShiftId ? (
                      <Typography.Text type="secondary">
                        Ca {shift ? `${shift.shiftNumber} · ${shift.warehouseName}` : task.salesShiftId}
                      </Typography.Text>
                    ) : null}
                  </Space>
                  {task.evidenceNote ? (
                    <Typography.Text type="secondary">Bằng chứng: {task.evidenceNote}</Typography.Text>
                  ) : null}
                  {!closed ? (
                    <Space direction="vertical" style={{ width: '100%' }}>
                      <Input.TextArea
                        rows={2}
                        maxLength={2000}
                        placeholder="Ghi chú khi hoàn thành (không bắt buộc)"
                        value={evidenceById[task.id] ?? ''}
                        onChange={(event) =>
                          setEvidenceById((current) => ({ ...current, [task.id]: event.target.value }))
                        }
                      />
                      <Space wrap>
                        {task.status === 'open' ? (
                          <Button
                            loading={busyId === task.id}
                            onClick={() => void changeStatus(task, 'in_progress')}
                          >
                            Bắt đầu
                          </Button>
                        ) : null}
                        <Button
                          type="primary"
                          loading={busyId === task.id}
                          onClick={() => void changeStatus(task, 'done')}
                        >
                          Xong
                        </Button>
                        {canWrite ? (
                          <Button danger loading={busyId === task.id} onClick={() => void changeStatus(task, 'cancelled')}>
                            Hủy
                          </Button>
                        ) : null}
                      </Space>
                    </Space>
                  ) : null}
                </Space>
              </List.Item>
            );
          }}
        />
      </Card>

      <Space>
        <Link to="/people/learn">Học bài</Link>
        <Link to="/success/shift-checklist">Checklist ca</Link>
        <Link to="/people/recognize">Năng lực và ghi nhận</Link>
      </Space>
    </Space>
  );
}
