import { useState } from 'react';
import { Alert, Button, Card, Empty, Input, List, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import { listLocalRegimensInUse, type LocalRegimenInUse } from '@/shared/api/pharmacy-local-regimen.api';
import { REVIEW_OVERDUE_LABEL } from '@/shared/api/pharmacy-owned-knowledge.api';

const { Text, Paragraph } = Typography;

const MATCH_LABEL: Record<string, string> = {
  MATCHED: 'Khớp danh mục',
  UNMATCHED: 'Chưa khớp danh mục',
  SUBSTITUTE_PROPOSED: 'Thay thế chưa xác nhận',
  SUBSTITUTE_CONFIRMED: 'Thay thế đã xác nhận',
};

export function LocalRegimenPosLookup() {
  const [query, setQuery] = useState('');
  const [rows, setRows] = useState<LocalRegimenInUse[] | null>(null);
  const [storeReady, setStoreReady] = useState(true);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState<LocalRegimenInUse | null>(null);

  async function search() {
    setLoading(true);
    try {
      const result = await listLocalRegimensInUse(query);
      const items = result.items.filter((row) => !row.title.toLowerCase().includes('không dùng để tư vấn'));
      setRows(items);
      setStoreReady(result.storeReady);
      setOpen(items.length === 1 ? items[0] : null);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tra được kho đơn tham khảo'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card size="small" title="KHO ĐƠN THAM KHẢO">
      <Paragraph type="secondary" style={{ fontSize: 12 }}>
        Nhập triệu chứng khách đang mô tả. Đơn mẫu hiện sản phẩm của nhà thuốc để bán. Nhân viên tự thêm vào đơn, quầy không tự đưa vào giỏ.
      </Paragraph>
      <Space.Compact style={{ width: '100%' }}>
        <Input
          value={query}
          placeholder="Triệu chứng, đối tượng hoặc nhóm vấn đề"
          onChange={(event) => setQuery(event.target.value)}
          onPressEnter={() => void search()}
        />
        <Button loading={loading} onClick={() => void search()}>Tra cứu</Button>
      </Space.Compact>
      {storeReady === false ? (
        <Alert style={{ marginTop: 8 }} type="info" showIcon message="Kho đơn tham khảo chưa được bật trên database." />
      ) : null}
      {rows && rows.length === 0 && storeReady ? (
        <Empty description="Không có mẫu đang áp dụng" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : null}
      <List
        size="small"
        dataSource={rows ?? []}
        renderItem={(row) => (
          <List.Item>
            <Button type="link" onClick={() => setOpen(open?.id === row.id ? null : row)}>{row.title}</Button>
            <Text type="secondary">{row.conditionGroup}</Text>
            {row.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
          </List.Item>
        )}
      />
      {open ? (
        <div>
          <Alert type="warning" showIcon message="Đây là đơn mẫu của nhà thuốc. Chỉ bán sản phẩm đã gắn. Dòng chưa có sản phẩm không được bán thay. Tự thêm vào đơn bán." />
          <Paragraph style={{ marginTop: 8 }}><Text strong>Đối tượng: </Text>{open.audience}</Paragraph>
          <Paragraph><Text strong>Triệu chứng: </Text>{open.symptoms}</Paragraph>
          <Paragraph><Text strong>Điều kiện: </Text>{open.applicability}</Paragraph>
          <Paragraph><Text strong>Giới hạn: </Text>{open.limits}</Paragraph>
          {open.items.map((item) => (
            <div key={item.id}>
              <Text>{item.ingredient}</Text>
              {item.strength ? <Text type="secondary"> {item.strength}</Text> : null}
              {item.dosageForm ? <Text type="secondary"> · {item.dosageForm}</Text> : null}
              <div>
                <Tag color={item.validChoice ? 'green' : 'orange'}>{MATCH_LABEL[item.matchStatus] ?? item.matchStatus}</Tag>
                <Text type="secondary">{item.catalogProductName || item.productLabel || 'Chưa có sản phẩm danh mục'}</Text>
              </div>
              {item.validChoice ? (
                <Text style={{ fontSize: 12 }}>Sản phẩm của nhà thuốc trong đơn mẫu. Tự thêm vào đơn bán.</Text>
              ) : (
                <Text type="danger" style={{ fontSize: 12 }}>Chưa có sản phẩm của nhà thuốc. Không bán thay bằng thuốc khác.</Text>
              )}
            </div>
          ))}
        </div>
      ) : null}
    </Card>
  );
}
