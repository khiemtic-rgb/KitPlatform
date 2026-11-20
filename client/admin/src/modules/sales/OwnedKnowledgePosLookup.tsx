import { useState } from 'react';
import { Button, Card, Empty, Input, List, Space, Tag, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  REVIEW_OVERDUE_LABEL,
  getOwnedKnowledgeInUse,
  listOwnedKnowledgeInUse,
  sendKnowledgeFeedback,
  type OwnedKnowledgeInUse,
} from '@/shared/api/pharmacy-owned-knowledge.api';
import { parseOwnedKnowledgeBody } from '@/modules/pharmacy/knowledge-experience';

const { Text, Paragraph } = Typography;

export function OwnedKnowledgePosLookup({ initialQuery }: { initialQuery?: string }) {
  const [query, setQuery] = useState(initialQuery ?? '');
  const [rows, setRows] = useState<OwnedKnowledgeInUse[] | null>(null);
  const [open, setOpen] = useState<OwnedKnowledgeInUse | null>(null);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState('');

  async function search() {
    const text = query.trim();
    if (!text) return;
    setLoading(true);
    try {
      setRows(await listOwnedKnowledgeInUse({ q: text }));
      setSearched(text);
      setOpen(null);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tra được tài liệu nhà thuốc'));
    } finally {
      setLoading(false);
    }
  }

  async function view(row: OwnedKnowledgeInUse) {
    if (open?.id === row.id) {
      setOpen(null);
      return;
    }
    try {
      setOpen(await getOwnedKnowledgeInUse(row.id));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được tài liệu'));
    }
  }

  async function feedback(kind: 'missing' | 'not_helpful', knowledgeId?: string) {
    try {
      await sendKnowledgeFeedback({ kind, knowledgeId: knowledgeId ?? null, query: searched || query.trim() || null });
      message.success('Đã ghi nhận');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được phản hồi'));
    }
  }

  const sections = open ? Object.entries(parseOwnedKnowledgeBody(open.body)) : [];

  return (
    <Card size="small" title="TÀI LIỆU NHÀ THUỐC ĐANG DÙNG">
      <Space.Compact style={{ width: '100%' }}>
        <Input
          value={query}
          placeholder="Tìm theo tiêu đề hoặc nội dung"
          onChange={(event) => setQuery(event.target.value)}
          onPressEnter={() => void search()}
        />
        <Button loading={loading} onClick={() => void search()}>Tìm</Button>
      </Space.Compact>
      {rows && rows.length === 0 ? (
        <Empty description="Chưa có tài liệu phù hợp" image={Empty.PRESENTED_IMAGE_SIMPLE}>
          <Button size="small" onClick={() => void feedback('missing')}>Báo thiếu nội dung</Button>
        </Empty>
      ) : null}
      <List
        size="small"
        dataSource={rows ?? []}
        renderItem={(row) => (
          <List.Item>
            <Space size={4} wrap>
              <Button type="link" style={{ paddingLeft: 0 }} onClick={() => void view(row)}>{row.title}</Button>
              {row.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
            </Space>
          </List.Item>
        )}
      />
      {open ? (
        <div>
          {sections.length === 0 ? (
            <Paragraph style={{ whiteSpace: 'pre-wrap' }}>{open.body}</Paragraph>
          ) : sections.map(([heading, text]) => (
            <div key={heading}>
              <Text strong>{heading}</Text>
              <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 8 }}>{text || '—'}</Paragraph>
            </div>
          ))}
          <Button size="small" onClick={() => void feedback('not_helpful', open.id)}>Chưa giúp được</Button>
        </div>
      ) : null}
    </Card>
  );
}
