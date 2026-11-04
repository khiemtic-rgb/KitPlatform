import { useState } from 'react';
import { Alert, Button, Card, Checkbox, Empty, Input, List, Select, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import { createCustomerKnowledge, revokeCustomerKnowledge } from '@/shared/api/pharmacy-customer-knowledge.api';
import type { CustomerKnowledgeEntryDto } from '@/shared/api/pos-customer-context.types';

const { Text, Paragraph } = Typography;

const KINDS = [
  { value: 'CHRONIC', label: 'Bệnh nền' },
  { value: 'ALLERGY_DRUG', label: 'Dị ứng thuốc' },
  { value: 'ALLERGY_OTHER', label: 'Dị ứng khác' },
  { value: 'ADVERSE_REACTION', label: 'Phản ứng bất lợi' },
  { value: 'MEDICATION_USE', label: 'Đã xác nhận sử dụng' },
  { value: 'FOLLOW_UP', label: 'Cần hỏi lại' },
];

const SOURCES = [
  { value: 'SELF_REPORT', label: 'Khách tự khai' },
  { value: 'PRESCRIPTION', label: 'Từ đơn thuốc' },
  { value: 'TRANSACTION', label: 'Từ giao dịch' },
  { value: 'PROFESSIONAL', label: 'Nhận định dược sĩ' },
  { value: 'UNVERIFIED', label: 'Cần xác minh' },
];

const labelOf = (options: { value: string; label: string }[], value: string) =>
  options.find((item) => item.value === value)?.label ?? value;

const SENSITIVE = new Set(['CHRONIC', 'ALLERGY_DRUG', 'ALLERGY_OTHER', 'ADVERSE_REACTION']);

interface CustomerKnowledgeCardProps {
  customerId: string;
  entries: CustomerKnowledgeEntryDto[];
  storeReady?: boolean;
  onChanged: () => void;
}

export function CustomerKnowledgeCard({ customerId, entries, storeReady, onChanged }: CustomerKnowledgeCardProps) {
  const [kind, setKind] = useState('FOLLOW_UP');
  const [sourceKind, setSourceKind] = useState('SELF_REPORT');
  const [body, setBody] = useState('');
  const [needsVerification, setNeedsVerification] = useState(false);
  const [saving, setSaving] = useState(false);
  const sensitive = entries.some((entry) => SENSITIVE.has(entry.kind) || entry.needsVerification || entry.sourceKind === 'UNVERIFIED');

  async function save() {
    setSaving(true);
    try {
      await createCustomerKnowledge({ customerId, kind, body, sourceKind, needsVerification });
      setBody('');
      message.success('Đã ghi nhận');
      onChanged();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ghi được'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card size="small" title="HỒ SƠ SỨC KHỎE ĐÃ GHI NHẬN">
      {storeReady === false ? (
        <Alert
          type="info"
          showIcon
          message="Phần dị ứng, bệnh nền và ghi chú tiếp nối chưa được bật trên database. Ghi chú khách và lịch sử mua vẫn xem được."
        />
      ) : null}
      {sensitive ? (
        <Alert
          style={{ marginTop: 8 }}
          type="warning"
          showIcon
          message="Có dị ứng, bệnh nền, phản ứng hoặc nội dung chưa xác minh. Chuyển dược sĩ phụ trách trước khi bán thuốc liên quan."
        />
      ) : null}
      {entries.length === 0 && storeReady !== false ? (
        <Empty description="Chưa có ghi nhận sức khỏe" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : (
        <List
          size="small"
          dataSource={entries}
          renderItem={(entry) => (
            <List.Item
              actions={[
                <Button
                  key="revoke"
                  type="link"
                  size="small"
                  onClick={() => {
                    void revokeCustomerKnowledge(entry.id).then(onChanged).catch((error) => {
                      message.error(apiErrorMessage(error, 'Không thu hồi được'));
                    });
                  }}
                >
                  Thu hồi
                </Button>,
              ]}
            >
              <div>
                <Space size={4} wrap>
                  <Tag>{labelOf(KINDS, entry.kind)}</Tag>
                  <Tag>{labelOf(SOURCES, entry.sourceKind)}</Tag>
                  {entry.needsVerification ? <Tag color="orange">Cần xác minh</Tag> : null}
                </Space>
                <Paragraph style={{ marginBottom: 0 }}>{entry.body}</Paragraph>
                <Text type="secondary" style={{ fontSize: 11 }}>
                  {entry.recordedByName || entry.recordedBy} · {new Date(entry.recordedAt).toLocaleString('vi-VN')}
                </Text>
              </div>
            </List.Item>
          )}
        />
      )}
      {storeReady ? (
        <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
          <Select value={kind} options={KINDS} onChange={setKind} />
          <Select value={sourceKind} options={SOURCES} onChange={setSourceKind} />
          <Input.TextArea rows={2} value={body} placeholder="Nội dung đã có căn cứ" onChange={(event) => setBody(event.target.value)} />
          <Checkbox checked={needsVerification} onChange={(event) => setNeedsVerification(event.target.checked)}>
            Cần xác minh lại
          </Checkbox>
          <Button size="small" type="primary" loading={saving} disabled={!body.trim()} onClick={() => void save()}>
            Ghi nhận
          </Button>
        </Space>
      ) : null}
    </Card>
  );
}
