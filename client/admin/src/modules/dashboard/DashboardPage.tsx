import { useEffect } from 'react';
import { Card, Col, Row, Statistic, Typography, Tag, Space } from 'antd';
import {
  MedicineBoxOutlined,
  InboxOutlined,
  ShopOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '@/shared/auth/auth.store';
import { meApi } from '@/shared/api/auth.api';

export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);

  useEffect(() => {
    meApi()
      .then(setUser)
      .catch(() => undefined);
  }, [setUser]);

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          Xin chào, {user?.username ?? 'Admin'}
        </Typography.Title>
        <Space wrap>
          <Tag color="blue">{user?.tenantCode ?? 'DEMO_PHARMACY'}</Tag>
          {user?.roles.map((role) => (
            <Tag key={role} color="green">
              {role}
            </Tag>
          ))}
        </Space>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Sản phẩm" value="4" prefix={<MedicineBoxOutlined />} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Module Catalog — đã bật
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Tồn kho (lô)" value="—" prefix={<InboxOutlined />} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Module Inventory — sắp có
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Đơn bán hôm nay" value="—" prefix={<ShopOutlined />} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Module Sales — sắp có
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Khách hàng" value="—" prefix={<TeamOutlined />} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Module Customer — sắp có
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Card title="Trạng thái Phase 1">
        <Typography.Paragraph>
          Shell web đã sẵn sàng: đăng nhập JWT, layout, menu module. Các module sẽ được bật dần khi API
          backend hoàn thiện.
        </Typography.Paragraph>
      </Card>
    </Space>
  );
}
