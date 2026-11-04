import { Card, Empty, List, Tag, Typography } from 'antd';
import type { CustomerPosPrescriptionDto } from '@/shared/api/pos-customer-context.types';

const { Text } = Typography;

const STATUS_LABEL: Record<string, string> = {
  draft: 'Nháp',
  pending_verification: 'Chờ kiểm',
  verified: 'Đã kiểm',
  signed: 'Đã ký',
  partially_dispensed: 'Đã bán một phần',
  dispensed: 'Đã bán theo đơn',
  expired: 'Hết hạn',
  cancelled: 'Đã hủy',
};

interface RecentPrescriptionsListProps {
  prescriptions: CustomerPosPrescriptionDto[];
}

export function RecentPrescriptionsList({ prescriptions }: RecentPrescriptionsListProps) {
  if (prescriptions.length === 0) {
    return (
      <Card size="small" title="ĐƠN THUỐC ĐÃ GHI NHẬN" className="pos-prescriptions-card">
        <Empty description="Chưa có đơn thuốc gắn với khách này" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      </Card>
    );
  }

  return (
    <Card size="small" title="ĐƠN THUỐC ĐÃ GHI NHẬN" className="pos-prescriptions-card">
      <Text type="secondary" style={{ fontSize: 11, fontStyle: 'italic' }}>
        Lịch sử đơn đã ghi. Không phải chỉ định của lần mua này và không tự đưa vào giỏ.
      </Text>
      <List
        size="small"
        dataSource={prescriptions}
        renderItem={(item) => (
          <List.Item>
            <div style={{ width: '100%' }}>
              <div style={{ marginBottom: 2 }}>
                <Text strong>{item.prescriptionCode}</Text>
                <Tag style={{ marginLeft: 8 }}>{STATUS_LABEL[item.status] ?? item.status}</Tag>
              </div>
              <Text type="secondary" style={{ fontSize: 11 }}>
                {new Date(item.createdAt).toLocaleDateString('vi-VN')}
                {item.prescriberName ? ` · ${item.prescriberName}` : ''}
              </Text>
              {item.productNames ? (
                <div>
                  <Text type="secondary" style={{ fontSize: 12 }}>{item.productNames}</Text>
                </div>
              ) : null}
            </div>
          </List.Item>
        )}
      />
    </Card>
  );
}
