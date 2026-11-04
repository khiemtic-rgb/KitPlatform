import { Card, Empty, List, Tag, Typography } from 'antd';
import { WarningOutlined, CheckCircleOutlined } from '@ant-design/icons';
import type { CustomerPosConsultationDto } from '@/shared/api/pos-customer-context.types';

const { Text } = Typography;

interface RecentConsultationsListProps {
  consultations: CustomerPosConsultationDto[];
}

const SAFETY_LEVEL_CONFIG: Record<string, { color: string; icon: React.ReactNode; label: string }> = {
  safe: { color: 'success', icon: <CheckCircleOutlined />, label: 'Bình thường' },
  caution: { color: 'warning', icon: <WarningOutlined />, label: 'Cẩn thận' },
  refer_pharmacist: { color: 'error', icon: <WarningOutlined />, label: 'Cần dược sĩ' },
  refer_medical: { color: 'error', icon: <WarningOutlined />, label: 'Gửi bác sĩ' },
};

export function RecentConsultationsList({ consultations }: RecentConsultationsListProps) {
  if (consultations.length === 0) {
    return (
      <Card size="small" title="TƯ VẤN GẦN ĐÂY" className="pos-consultations-card">
        <Empty description="Chưa có lịch sử tư vấn" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      </Card>
    );
  }

  return (
    <Card size="small" title="TƯ VẤN GẦN ĐÂY" className="pos-consultations-card">
      <List
        size="small"
        dataSource={consultations}
        renderItem={(consultation) => {
          const safetyConfig = SAFETY_LEVEL_CONFIG[consultation.safetyLevel] ?? SAFETY_LEVEL_CONFIG.safe;

          return (
            <List.Item>
              <div style={{ width: '100%' }}>
                <div style={{ marginBottom: 4 }}>
                  <Text strong>
                    {new Date(consultation.consultationDate).toLocaleDateString('vi-VN')}
                  </Text>
                </div>
                {consultation.safetyMessage && (
                  <div style={{ marginBottom: 4 }}>
                    <Tag color={safetyConfig.color} icon={safetyConfig.icon}>
                      {consultation.safetyMessage}
                    </Tag>
                  </div>
                )}
                {consultation.symptoms.length > 0 && (
                  <div>
                    <Text type="secondary" style={{ fontSize: 11 }}>
                      {consultation.symptoms.slice(0, 3).join(', ')}
                      {consultation.symptoms.length > 3 && '...'}
                    </Text>
                  </div>
                )}
              </div>
            </List.Item>
          );
        }}
      />
    </Card>
  );
}
