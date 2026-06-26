import { useEffect, useState } from 'react';
import { Button, Card, Empty, List, Space, Tag, Typography } from 'antd';
import { Link, useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import {
  listCustomerNotifications,
  markAllCustomerNotificationsRead,
  markCustomerNotificationRead,
  subscribeCustomerNotifications,
  type CustomerNotification,
} from '@/shared/notifications/customer-notifications';

export function NotificationsPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<CustomerNotification[]>(() => listCustomerNotifications());

  useEffect(() => {
    const refresh = () => setItems(listCustomerNotifications());
    return subscribeCustomerNotifications(refresh);
  }, []);

  const unreadCount = items.filter((item) => !item.read).length;

  const openNotification = (item: CustomerNotification) => {
    markCustomerNotificationRead(item.id);
    if (item.href) {
      navigate(item.href);
    }
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <BackToHomeButton />
      <Typography.Title level={5} style={{ margin: 0 }}>
        Thông báo
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
        Lịch sử thông báo trên thiết bị này — đơn thuốc mới và cập nhật quan trọng.
      </Typography.Paragraph>

      {unreadCount > 0 ? (
        <Button size="small" onClick={() => markAllCustomerNotificationsRead()}>
          Đánh dấu tất cả đã đọc
        </Button>
      ) : null}

      {items.length === 0 ? (
        <Card size="small" style={{ borderRadius: 12 }}>
          <Empty description="Chưa có thông báo" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        </Card>
      ) : (
        <List
          dataSource={items}
          renderItem={(item) => (
            <Card
              size="small"
              style={{
                marginBottom: 8,
                borderRadius: 12,
                borderColor: item.read ? undefined : '#5eead4',
                cursor: item.href ? 'pointer' : 'default',
              }}
              onClick={() => openNotification(item)}
            >
              <Space direction="vertical" size={4} style={{ width: '100%' }}>
                <Space wrap>
                  <Typography.Text strong={!item.read}>{item.title}</Typography.Text>
                  {!item.read ? <Tag color="processing">Mới</Tag> : null}
                </Space>
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  {item.body}
                </Typography.Text>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {dayjs(item.createdAt).format('DD/MM/YYYY HH:mm')}
                </Typography.Text>
              </Space>
            </Card>
          )}
        />
      )}

      <Link to="/profile">← Về tài khoản</Link>
    </Space>
  );
}
