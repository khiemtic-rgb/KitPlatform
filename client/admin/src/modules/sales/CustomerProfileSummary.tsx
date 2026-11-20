import { Card, Typography, Button, Space } from 'antd';
import { WarningOutlined, EditOutlined } from '@ant-design/icons';
import type { CustomerPosProfileDto } from '@/shared/api/pos-customer-context.types';

const { Text, Paragraph } = Typography;

interface CustomerProfileSummaryProps {
  profile: CustomerPosProfileDto;
  onEdit?: () => void;
}

export function CustomerProfileSummary({ profile, onEdit }: CustomerProfileSummaryProps) {
  return (
    <Card
      size="small"
      title="THÔNG TIN KHÁCH HÀNG"
      className="pos-customer-profile-card"
      extra={
        onEdit && (
          <Button size="small" icon={<EditOutlined />} onClick={onEdit}>
            Sửa
          </Button>
        )
      }
    >
      <Space direction="vertical" size="small" style={{ width: '100%' }}>
        <div>
          <Text strong>{profile.fullName}</Text>
          <Text type="secondary"> · {profile.customerCode}</Text>
        </div>

        {profile.ageYears !== undefined && profile.gender && (
          <Text type="secondary">
            {profile.ageYears} tuổi · {profile.gender}
          </Text>
        )}

        {profile.lastPurchaseDate && (
          <Text type="secondary" style={{ fontSize: 12 }}>
            Mua gần nhất: {new Date(profile.lastPurchaseDate).toLocaleDateString('vi-VN')}
          </Text>
        )}

        {profile.clinicalNotes && (
          <div className="pos-customer-clinical-notes">
            <WarningOutlined style={{ color: '#faad14', marginRight: 8 }} />
            <Paragraph
              style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}
              ellipsis={{ rows: 3, expandable: true, symbol: 'xem thêm' }}
            >
              {profile.clinicalNotes}
            </Paragraph>
            <Text type="secondary" style={{ fontSize: 10, fontStyle: 'italic' }}>
              (Ghi chú từ khách hàng — cần xác nhận)
            </Text>
          </div>
        )}

        {profile.profileGaps.length > 0 && (
          <div className="pos-customer-profile-gaps">
            <WarningOutlined style={{ color: '#ff7875', marginRight: 4 }} />
            <Text type="danger" style={{ fontSize: 12 }}>
              Thiếu thông tin: {profile.profileGaps.join(', ')}
            </Text>
            {onEdit && (
              <Button type="link" size="small" onClick={onEdit}>
                → Bổ sung thông tin
              </Button>
            )}
          </div>
        )}
      </Space>
    </Card>
  );
}
