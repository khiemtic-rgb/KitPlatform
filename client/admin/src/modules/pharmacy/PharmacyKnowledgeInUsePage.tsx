import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Checkbox, Drawer, Empty, Input, Result, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  REVIEW_OVERDUE_LABEL,
  getOwnedKnowledgeInUse,
  listOwnedKnowledgeInUse,
  sendKnowledgeFeedback,
  type OwnedKnowledgeInUse,
} from '@/shared/api/pharmacy-owned-knowledge.api';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { allowsPharmacyKnowledgeVertical, parseOwnedKnowledgeBody } from '@/modules/pharmacy/knowledge-experience';
import { ownedKnowledgeKinds } from '@/modules/pharmacy/PharmacyOwnedKnowledgePage';

const kindLabel = (kind: string) => ownedKnowledgeKinds.find((item) => item.value === kind)?.label ?? kind;

export function PharmacyKnowledgeInUsePage() {
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const vertical = useTenantPlatformStore((s) => s.settings?.vertical);
  const pharmacyTenant = platformLoaded && allowsPharmacyKnowledgeVertical(vertical);
  const [kind, setKind] = useState<string | undefined>();
  const [query, setQuery] = useState('');
  const [classification, setClassification] = useState('');
  const [rows, setRows] = useState<OwnedKnowledgeInUse[]>([]);
  const [open, setOpen] = useState<OwnedKnowledgeInUse | null>(null);
  const [loading, setLoading] = useState(false);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const visibleRows = overdueOnly ? rows.filter((row) => row.reviewOverdue) : rows;

  async function reportMissing() {
    const text = window.prompt('Bạn cần tìm nội dung gì mà chưa có?', query) ?? '';
    if (!text.trim()) return;
    try {
      await sendKnowledgeFeedback({ kind: 'missing', query: text.trim() });
      message.success('Đã ghi nhận nội dung còn thiếu');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được phản hồi'));
    }
  }

  async function reportNotHelpful(row: OwnedKnowledgeInUse) {
    try {
      await sendKnowledgeFeedback({ kind: 'not_helpful', knowledgeId: row.id, query: query.trim() || null });
      message.success('Đã ghi nhận');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không gửi được phản hồi'));
    }
  }

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await listOwnedKnowledgeInUse({ kind, q: query, classification }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tri thức đang dùng'));
    } finally {
      setLoading(false);
    }
  }, [classification, kind, query]);

  useEffect(() => {
    if (pharmacyTenant) void load();
  }, [load, pharmacyTenant]);

  const columns: ColumnsType<OwnedKnowledgeInUse> = useMemo(() => [
    { title: 'Tiêu đề', dataIndex: 'title' },
    { title: 'Loại', dataIndex: 'kind', render: (value: string) => kindLabel(value) },
    { title: 'Phân loại', dataIndex: 'classification', render: (value?: string | null) => value || '—' },
    { title: 'Phiên bản', dataIndex: 'version', width: 120, render: (value: number) => `Phiên bản ${value}` },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 150,
      render: (_value: string, row: OwnedKnowledgeInUse) => (
        <Space size={4} wrap>
          <Tag>Đang dùng</Tag>
          {row.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
        </Space>
      ),
    },
    {
      title: '',
      width: 90,
      render: (_value, row) => (
        <Button
          type="link"
          onClick={() => {
            void getOwnedKnowledgeInUse(row.id).then(setOpen).catch((error) => {
              message.error(apiErrorMessage(error, 'Không mở được tri thức'));
            });
          }}
        >
          Xem
        </Button>
      ),
    },
  ], []);

  if (!platformLoaded) return null;
  if (!pharmacyTenant) {
    return (
      <Result
        status="403"
        title="Tri thức nhà thuốc không thuộc tenant này"
        subTitle="Mục này chỉ mở khi tenant đang vận hành nhà thuốc."
      />
    );
  }

  const sections = open ? Object.entries(parseOwnedKnowledgeBody(open.body)) : [];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>Tra cứu tài liệu vận hành</Typography.Title>
        <Typography.Text type="secondary">
          Chỉ hiện tài liệu vận hành đang áp dụng và bạn được phép đọc. Không dùng để xem hồ sơ khách, không thay đơn thuốc, và không đưa vào gợi ý bán hàng.
        </Typography.Text>
      </div>
      <Space wrap>
        <Input
          allowClear
          placeholder="Tìm tiêu đề hoặc nội dung"
          style={{ width: 240 }}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <Select
          allowClear
          placeholder="Lọc theo loại"
          style={{ width: 220 }}
          value={kind}
          options={ownedKnowledgeKinds
            .filter((item) => item.value !== 'CLINICAL_KNOWLEDGE')
            .map((item) => ({ value: item.value, label: item.label }))}
          onChange={(value) => setKind(value)}
        />
        <Input
          allowClear
          placeholder="Phân loại"
          style={{ width: 180 }}
          value={classification}
          onChange={(event) => setClassification(event.target.value)}
        />
        <Checkbox checked={overdueOnly} onChange={(event) => setOverdueOnly(event.target.checked)}>
          Chỉ {REVIEW_OVERDUE_LABEL.toLowerCase()}
        </Checkbox>
        <Button onClick={() => void reportMissing()}>Báo thiếu nội dung</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={visibleRows}
        locale={{ emptyText: <Empty description="Chưa có tri thức được phép xem" /> }}
        pagination={false}
      />
      <Drawer title={open?.title || 'Tri thức'} open={open !== null} width={640} onClose={() => setOpen(null)}>
        {open ? (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Typography.Text>Loại: {kindLabel(open.kind)}</Typography.Text>
            <Typography.Text>Phân loại: {open.classification || '—'}</Typography.Text>
            <Typography.Text>Phiên bản {open.version} · Đang dùng</Typography.Text>
            {open.reviewOverdue ? <Tag color="orange">{REVIEW_OVERDUE_LABEL}</Tag> : null}
            {sections.length === 0 ? (
              <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{open.body}</Typography.Paragraph>
            ) : sections.map(([heading, text]) => (
              <div key={heading}>
                <Typography.Text strong>{heading}</Typography.Text>
                <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{text || '—'}</Typography.Paragraph>
              </div>
            ))}
            <Button onClick={() => void reportNotHelpful(open)}>Nội dung này chưa giúp được</Button>
          </Space>
        ) : null}
      </Drawer>
    </Space>
  );
}
