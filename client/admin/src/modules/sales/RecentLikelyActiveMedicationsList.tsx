import { Card, Empty, List, Tag, Typography } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import type { CustomerPosActiveMedicationDto } from '@/shared/api/pos-customer-context.types';

const { Text } = Typography;

interface RecentLikelyActiveMedicationsListProps {
  medications: CustomerPosActiveMedicationDto[];
}

export function RecentLikelyActiveMedicationsList({
  medications,
}: RecentLikelyActiveMedicationsListProps) {
  if (medications.length === 0) {
    return (
      <Card
        size="small"
        title="CÓ TRONG LẦN MUA GẦN ĐÂY"
        className="pos-active-meds-card"
      >
        <Empty description="Không có thuốc gần đây" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      </Card>
    );
  }

  return (
    <Card
      size="small"
      title="CÓ TRONG LẦN MUA GẦN ĐÂY"
      className="pos-active-meds-card"
    >
      <div style={{ marginBottom: 8 }}>
        <Text type="secondary" style={{ fontSize: 11, fontStyle: 'italic' }}>
          Đây là sản phẩm có trong lần mua hoặc lịch nhắc, chưa phải thuốc đang sử dụng đã xác nhận.
        </Text>
      </div>
      <List
        size="small"
        dataSource={medications}
        renderItem={(med) => (
          <List.Item>
            <div style={{ width: '100%' }}>
              <div style={{ marginBottom: 2 }}>
                <Text strong>{med.productName}</Text>
                {med.isNearEnd && (
                  <Tag color="orange" style={{ marginLeft: 8, fontSize: 11 }}>
                    <WarningOutlined /> Sắp hết
                  </Tag>
                )}
              </div>
              <div>
                {med.lastPurchaseDate && (
                  <Text type="secondary" style={{ fontSize: 11 }}>
                    Mua: {med.lastPurchaseDate}
                  </Text>
                )}
                {med.daysSupply ? (
                  <Text type="secondary" style={{ fontSize: 11, marginLeft: 8 }}>
                    · Đủ {med.daysSupply} ngày
                  </Text>
                ) : null}
                {med.supplyEndDate && (
                  <Text type="secondary" style={{ fontSize: 11, marginLeft: 8 }}>
                    · Hết: {med.supplyEndDate}
                  </Text>
                )}
              </div>
            </div>
          </List.Item>
        )}
      />
    </Card>
  );
}
