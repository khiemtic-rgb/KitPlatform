import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Card,
  Descriptions,
  Space,
  Spin,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  ArrowLeftOutlined,
  CommentOutlined,
  EditOutlined,
  FormOutlined,
  MedicineBoxOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { fetchCustomer } from '@/shared/api/customer-admin.api';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import {
  CUSTOMER_GENDER_LABELS,
  CUSTOMER_STATUS_LABELS,
} from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { CustomerConsentPanel } from '@/modules/customer/CustomerConsentPanel';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import { CustomerLoyaltyPanel } from '@/modules/customer/CustomerLoyaltyPanel';
import { CustomerOrdersPanel } from '@/modules/customer/CustomerOrdersPanel';
import { formatDisplayDate } from '@/shared/utils/date';

type DetailTab = 'profile' | 'consents' | 'loyalty' | 'orders';

export function CustomerDetailPage() {
  const { customerId = '' } = useParams();
  const navigate = useNavigate();
  const canWrite = useHasPermission('sales.write');
  const [detail, setDetail] = useState<CustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<DetailTab>('profile');
  const [drawerOpen, setDrawerOpen] = useState(false);

  const load = useCallback(async () => {
    if (!customerId) return;
    setLoading(true);
    try {
      setDetail(await fetchCustomer(customerId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được hồ sơ khách hàng'));
      navigate('/customer/list');
    } finally {
      setLoading(false);
    }
  }, [customerId, navigate]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading || !detail) {
    return (
      <div style={{ padding: 48, textAlign: 'center' }}>
        <Spin tip="Đang tải hồ sơ..." />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/customer/list')}>
          Danh sách
        </Button>
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          Tải lại
        </Button>
        {canWrite && detail ? (
          <Button icon={<EditOutlined />} onClick={() => setDrawerOpen(true)}>
            Sửa
          </Button>
        ) : null}
      </Space>

      <Card size="small">
        <Space direction="vertical" size={4}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {detail.fullName}
          </Typography.Title>
          <Typography.Text type="secondary">
            {detail.customerCode} · {detail.phone}
            {detail.email ? ` · ${detail.email}` : ''}
          </Typography.Text>
          <Space wrap>
            <Tag color={detail.status === 1 ? 'green' : 'default'}>
              {CUSTOMER_STATUS_LABELS[detail.status] ?? detail.status}
            </Tag>
            {detail.hasAppAccount ? (
              <Tag color={detail.appVerified ? 'blue' : 'gold'}>
                App {detail.appVerified ? 'đã xác minh' : 'chưa xác minh'}
              </Tag>
            ) : (
              <Tag>Chưa có tài khoản app</Tag>
            )}
          </Space>
        </Space>
      </Card>

      <Tabs
        activeKey={activeTab}
        onChange={(key) => setActiveTab(key as DetailTab)}
        items={[
          {
            key: 'profile',
            label: 'Hồ sơ',
            children: (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Descriptions bordered size="small" column={1}>
                  <Descriptions.Item label="Mã khách">{detail.customerCode}</Descriptions.Item>
                  <Descriptions.Item label="Họ tên">{detail.fullName}</Descriptions.Item>
                  <Descriptions.Item label="SĐT">{detail.phone}</Descriptions.Item>
                  <Descriptions.Item label="Email">{detail.email ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label="Ngày sinh">
                    {detail.dateOfBirth ? formatDisplayDate(detail.dateOfBirth) : '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label="Giới tính">
                    {detail.gender != null ? (CUSTOMER_GENDER_LABELS[detail.gender] ?? detail.gender) : '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label="Ngày tạo">
                    {formatDisplayDate(detail.createdAt)}
                  </Descriptions.Item>
                  <Descriptions.Item label="Tài khoản app">
                    {detail.hasAppAccount
                      ? `Có · đăng nhập ${detail.appLastLoginAt ? dayjs(detail.appLastLoginAt).format('DD/MM/YYYY HH:mm') : '—'}`
                      : 'Chưa đăng ký'}
                  </Descriptions.Item>
                </Descriptions>

                <Card size="small" title="Hoạt động app">
                  <Space wrap>
                    <Link to="/sales/customer-drafts">
                      <Button icon={<FormOutlined />}>Đơn tạm app</Button>
                    </Link>
                    <Link to="/sales/customer-reservations">
                      <Button icon={<MedicineBoxOutlined />}>Đặt trước</Button>
                    </Link>
                    <Link to="/sales/chat">
                      <Button icon={<CommentOutlined />}>Chat</Button>
                    </Link>
                  </Space>
                </Card>
              </Space>
            ),
          },
          {
            key: 'consents',
            label: 'Đồng ý',
            children: <CustomerConsentPanel customerId={detail.id} />,
          },
          {
            key: 'loyalty',
            label: 'Tích điểm',
            children: <CustomerLoyaltyPanel customerId={detail.id} />,
          },
          {
            key: 'orders',
            label: 'Đơn hàng',
            children: <CustomerOrdersPanel customerId={detail.id} />,
          },
        ]}
      />

      {detail ? (
        <CustomerFormDrawer
          open={drawerOpen}
          editing={detail}
          onClose={() => setDrawerOpen(false)}
          onSaved={(customer) => setDetail(customer)}
        />
      ) : null}
    </Space>
  );
}
