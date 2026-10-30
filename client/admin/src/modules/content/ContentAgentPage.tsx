import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, App, Button, Card, InputNumber, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import { CheckOutlined, CloseOutlined, ReloadOutlined, RobotOutlined, StopOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchContentBrands, type ContentBrand } from '@/shared/api/content.api';
import {
  approveAgentProposal,
  cancelAgentProposal,
  fetchAgentProposals,
  generateAgentProposals,
  rejectAgentProposal,
  type AgentGenerateResult,
  type AgentProposal,
  type AgentProposalStatus,
} from '@/shared/api/content-agent.api';

const STATUS_TEXT: Record<AgentProposalStatus, string> = {
  Proposed: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Đã từ chối',
  Cancelled: 'Đã huỷ',
  Failed: 'Lỗi',
};

const STATUS_COLOR: Record<AgentProposalStatus, string> = {
  Proposed: 'gold',
  Approved: 'green',
  Rejected: 'default',
  Cancelled: 'default',
  Failed: 'red',
};

const STATUS_FILTER = [
  { value: 'open', label: 'Chờ duyệt + Đã duyệt' },
  { value: 'Proposed', label: 'Chỉ chờ duyệt' },
  { value: 'Approved', label: 'Chỉ đã duyệt' },
  { value: 'Rejected', label: 'Đã từ chối' },
  { value: 'Cancelled', label: 'Đã huỷ' },
  { value: 'Failed', label: 'Lỗi' },
  { value: 'all', label: 'Tất cả' },
];

function vnTime(iso: string) {
  return new Date(iso).toLocaleString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function ContentAgentPage() {
  const { message } = App.useApp();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandId, setBrandId] = useState<string | undefined>();
  const [days, setDays] = useState(7);
  const [maxPerDay, setMaxPerDay] = useState(1);
  const [filter, setFilter] = useState('open');
  const [rows, setRows] = useState<AgentProposal[]>([]);
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [last, setLast] = useState<AgentGenerateResult | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const list = await fetchContentBrands(true);
        setBrands(list);
        setBrandId((cur) => cur ?? list[0]?.id);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được danh sách thương hiệu'));
      }
    })();
  }, [message]);

  const load = useCallback(async () => {
    if (!brandId) return;
    setLoading(true);
    try {
      const status = filter === 'open' || filter === 'all' ? undefined : filter;
      const all = await fetchAgentProposals({ brand: brandId, status });
      setRows(filter === 'open' ? all.filter((r) => r.status === 'Proposed' || r.status === 'Approved') : all);
      setSelected([]);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được đề xuất'));
    } finally {
      setLoading(false);
    }
  }, [brandId, filter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const generate = async () => {
    if (!brandId) return;
    setGenerating(true);
    try {
      const res = await generateAgentProposals(brandId, days, maxPerDay);
      setLast(res);
      message.success(
        res.created > 0
          ? `Đã tạo ${res.created} đề xuất mới. Chưa có gì được đăng.`
          : 'Không có đề xuất mới. Xem lý do bên dưới.',
      );
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tạo được đề xuất'));
    } finally {
      setGenerating(false);
    }
  };

  const act = async (id: string, fn: () => Promise<unknown>, ok: string) => {
    setBusyId(id);
    try {
      await fn();
      message.success(ok);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không thực hiện được'));
    } finally {
      setBusyId(null);
      await load();
    }
  };

  const approveSelected = async () => {
    const ids = rows.filter((r) => selected.includes(r.id) && r.status === 'Proposed').map((r) => r.id);
    let okCount = 0;
    for (const id of ids) {
      try {
        await approveAgentProposal(id);
        okCount += 1;
      } catch (e) {
        message.error(apiErrorMessage(e, 'Một đề xuất không duyệt được'));
      }
    }
    if (okCount > 0) message.success(`Đã duyệt ${okCount} đề xuất.`);
    await load();
  };

  const pendingSelected = useMemo(
    () => rows.filter((r) => selected.includes(r.id) && r.status === 'Proposed').length,
    [rows, selected],
  );

  return (
    <div>
      <Space direction="vertical" size={4} style={{ marginBottom: 16 }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          <RobotOutlined /> Agent đăng bài
        </Typography.Title>
        <Typography.Text type="secondary">
          Agent chỉ đề xuất lịch đăng. Bài chỉ được đăng sau khi bạn bấm Duyệt. Hiện chỉ bài chữ cho website và
          Fanpage; video chưa đăng tự động.
        </Typography.Text>
      </Space>

      <Card style={{ marginBottom: 16 }}>
        <Space wrap>
          <Select
            style={{ minWidth: 220 }}
            placeholder="Thương hiệu"
            value={brandId}
            onChange={setBrandId}
            options={brands.map((b) => ({ value: b.id, label: `${b.name} (${b.code})` }))}
          />
          <span>
            Số ngày <InputNumber min={1} max={14} value={days} onChange={(v) => setDays(v ?? 7)} />
          </span>
          <span>
            Tối đa bài/ngày/kênh <InputNumber min={1} max={4} value={maxPerDay} onChange={(v) => setMaxPerDay(v ?? 1)} />
          </span>
          <Button type="primary" icon={<RobotOutlined />} loading={generating} disabled={!brandId} onClick={() => void generate()}>
            Tạo đề xuất
          </Button>
        </Space>
      </Card>

      {last && (
        <Space direction="vertical" style={{ width: '100%', marginBottom: 16 }}>
          {last.warnings.map((w) => (
            <Alert key={w} type="info" showIcon message={w} />
          ))}
          {last.skipped.length > 0 && (
            <Alert
              type="warning"
              showIcon
              message={`${last.skipped.length} bài chưa được đề xuất`}
              description={
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {last.skipped.map((s) => (
                    <li key={s.topicId}>
                      <strong>{s.title}</strong>: {s.reason}
                    </li>
                  ))}
                </ul>
              }
            />
          )}
        </Space>
      )}

      <Card
        title="Đề xuất lịch đăng"
        extra={
          <Space>
            <Select style={{ width: 210 }} value={filter} onChange={setFilter} options={STATUS_FILTER} />
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              Tải lại
            </Button>
            <Popconfirm
              title={`Duyệt ${pendingSelected} đề xuất đã chọn?`}
              description="Mỗi mục sẽ tạo một job đăng đúng giờ đề xuất."
              okText="Duyệt"
              cancelText="Không"
              disabled={pendingSelected === 0}
              onConfirm={() => void approveSelected()}
            >
              <Button type="primary" icon={<CheckOutlined />} disabled={pendingSelected === 0}>
                Duyệt đã chọn ({pendingSelected})
              </Button>
            </Popconfirm>
          </Space>
        }
      >
        <Table<AgentProposal>
          rowKey="id"
          loading={loading}
          dataSource={rows}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          rowSelection={{
            selectedRowKeys: selected,
            onChange: (keys) => setSelected(keys as string[]),
            getCheckboxProps: (r) => ({ disabled: r.status !== 'Proposed' }),
          }}
          locale={{ emptyText: 'Chưa có đề xuất. Bấm "Tạo đề xuất".' }}
          columns={[
            { title: 'Giờ đăng (VN)', dataIndex: 'publishAt', width: 150, render: (v: string) => vnTime(v) },
            { title: 'Bài', dataIndex: 'title', ellipsis: true },
            { title: 'Kênh', dataIndex: 'targetLabel', width: 170 },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              width: 120,
              render: (v: AgentProposalStatus, r) => (
                <Space direction="vertical" size={0}>
                  <Tag color={STATUS_COLOR[v]}>{STATUS_TEXT[v] ?? v}</Tag>
                  {r.lastError && v !== 'Approved' && (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {r.lastError}
                    </Typography.Text>
                  )}
                </Space>
              ),
            },
            { title: 'Lý do', dataIndex: 'reason', ellipsis: true },
            {
              title: '',
              key: 'actions',
              width: 210,
              render: (_: unknown, r) => (
                <Space>
                  {r.status === 'Proposed' && (
                    <>
                      <Popconfirm
                        title="Duyệt đề xuất này?"
                        description={`Sẽ tạo job đăng lúc ${vnTime(r.publishAt)}.`}
                        okText="Duyệt"
                        cancelText="Không"
                        onConfirm={() => void act(r.id, () => approveAgentProposal(r.id), 'Đã duyệt, job đăng đã được tạo.')}
                      >
                        <Button size="small" type="primary" icon={<CheckOutlined />} loading={busyId === r.id}>
                          Duyệt
                        </Button>
                      </Popconfirm>
                      <Button
                        size="small"
                        icon={<CloseOutlined />}
                        disabled={busyId === r.id}
                        onClick={() => void act(r.id, () => rejectAgentProposal(r.id), 'Đã từ chối.')}
                      >
                        Từ chối
                      </Button>
                    </>
                  )}
                  {r.status === 'Approved' && (
                    <Popconfirm
                      title="Huỷ job đăng này?"
                      description="Chỉ huỷ được khi job chưa chạy."
                      okText="Huỷ job"
                      cancelText="Không"
                      onConfirm={() => void act(r.id, () => cancelAgentProposal(r.id), 'Đã huỷ job đăng.')}
                    >
                      <Button size="small" danger icon={<StopOutlined />} loading={busyId === r.id}>
                        Huỷ
                      </Button>
                    </Popconfirm>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}
