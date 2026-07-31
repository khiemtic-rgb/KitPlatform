import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Input,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { ReloadOutlined, TeamOutlined, UserAddOutlined } from '@ant-design/icons';
import {
  fetchFamilyOsTrialSignups,
  type FamilyOsTrialSignup,
  type FamilyOsTrialSignupList,
} from '@/shared/api/family-os.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import './family-os-routines.css';

function formatDate(iso?: string) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('vi-VN');
}

function formatDateTime(iso?: string) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function statusTag(status: string) {
  switch (status) {
    case 'trial':
      return <Tag color="blue">Dùng thử</Tag>;
    case 'trial_grace':
      return <Tag color="cyan">Grace</Tag>;
    case 'active':
      return <Tag color="green">Đã trả phí</Tag>;
    case 'past_due':
      return <Tag color="orange">Quá hạn TT</Tag>;
    case 'expired':
      return <Tag color="default">Hết trial</Tag>;
    case 'canceled':
      return <Tag>Đã hủy</Tag>;
    default:
      return <Tag>{status || '—'}</Tag>;
  }
}

function sourceLabel(source: string) {
  switch (source) {
    case 'self_register':
      return 'Tự đăng ký';
    case 'seed':
      return 'Demo seed';
    case 'backfill':
      return 'Đồng bộ';
    default:
      return source || '—';
  }
}

export function FamilyOsTrialSignupsPage() {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<FamilyOsTrialSignupList | null>(null);
  const [q, setQ] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchFamilyOsTrialSignups());
    } catch (error) {
      setData(null);
      message.error(apiErrorMessage(error, 'Không tải được danh sách đăng ký dùng thử'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const items = data?.items ?? [];
    const needle = q.trim().toLowerCase();
    if (!needle) return items;
    return items.filter((row) => {
      const hay = [
        row.familyName,
        row.parentDisplayName,
        row.email,
        row.username,
        row.tenantCode,
        row.planCode,
        row.status,
      ]
        .join(' ')
        .toLowerCase();
      return hay.includes(needle);
    });
  }, [data, q]);

  const columns = useMemo(
    () => [
      {
        title: 'Đăng ký',
        key: 'registered',
        width: 150,
        render: (_: unknown, row: FamilyOsTrialSignup) => (
          <Space direction="vertical" size={0}>
            <Typography.Text>{formatDateTime(row.registeredAt)}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {sourceLabel(row.source)}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Gia đình',
        key: 'family',
        render: (_: unknown, row: FamilyOsTrialSignup) => (
          <Space direction="vertical" size={0}>
            <Typography.Text strong>{row.familyName || '—'}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.tenantCode} · {row.memberCount} thành viên
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Phụ huynh',
        key: 'parent',
        render: (_: unknown, row: FamilyOsTrialSignup) => (
          <Space direction="vertical" size={0}>
            <Typography.Text>{row.parentDisplayName || '—'}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.email || row.username || '—'}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Gói',
        dataIndex: 'planCode',
        key: 'planCode',
        width: 130,
      },
      {
        title: 'Trạng thái',
        key: 'status',
        width: 120,
        render: (_: unknown, row: FamilyOsTrialSignup) => statusTag(row.status),
      },
      {
        title: 'Trial',
        key: 'trial',
        width: 140,
        render: (_: unknown, row: FamilyOsTrialSignup) => {
          if (!row.trialEndsAt) return '—';
          return (
            <Space direction="vertical" size={0}>
              <span>đến {formatDate(row.trialEndsAt)}</span>
              {row.trialDaysRemaining != null && row.status === 'trial' ? (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  còn {row.trialDaysRemaining} ngày
                </Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
    ],
    [],
  );

  return (
    <div className="fr-page">
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Space align="center" style={{ justifyContent: 'space-between', width: '100%' }}>
          <Space align="center">
            <UserAddOutlined style={{ fontSize: 20 }} />
            <Typography.Title level={4} style={{ margin: 0 }}>
              Đăng ký dùng thử Famixa
            </Typography.Title>
          </Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        </Space>

        <Alert
          type="info"
          showIcon
          icon={<TeamOutlined />}
          message="Danh sách cross-tenant — mọi nhà tự đăng ký trên app"
          description="Không phụ thuộc tenant đang đăng nhập. Mỗi lần khách tạo nhà trên family-app sẽ xuất hiện ở đây để bạn theo dõi lượng quan tâm."
        />

        <Row gutter={[12, 12]}>
          <Col xs={12} sm={6}>
            <Card size="small">
              <Statistic title="Tổng đăng ký" value={data?.total ?? 0} loading={loading} />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small">
              <Statistic
                title="Đang dùng thử"
                value={data?.trialActive ?? 0}
                loading={loading}
                valueStyle={{ color: '#1677ff' }}
              />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small">
              <Statistic
                title="Hết trial"
                value={data?.trialExpired ?? 0}
                loading={loading}
              />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small">
              <Statistic
                title="Đã trả phí"
                value={data?.paidActive ?? 0}
                loading={loading}
                valueStyle={{ color: '#389e0d' }}
              />
            </Card>
          </Col>
        </Row>

        <Card
          title="Chi tiết đăng ký"
          size="small"
          extra={
            <Input.Search
              allowClear
              placeholder="Tìm tên nhà, email, mã tenant…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              style={{ width: 280 }}
            />
          }
        >
          <Table
            rowKey="id"
            columns={columns}
            dataSource={filtered}
            loading={loading}
            pagination={{ pageSize: 20, showSizeChanger: true }}
            locale={{ emptyText: 'Chưa có đăng ký nào — khách tạo nhà trên app sẽ hiện tại đây.' }}
          />
        </Card>
      </Space>
    </div>
  );
}
