import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Drawer,
  Form,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  CloudUploadOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  approveContentTopic,
  createContentTopic,
  fetchContentAssetObjectUrl,
  fetchContentBrands,
  fetchContentTopicDetail,
  fetchContentTopics,
  generateContentTopic,
  publishContentTopic,
  runContentPublishJob,
  selectContentAsset,
  updateContentTopic,
  type ContentAsset,
  type ContentBrand,
  type ContentPublishJob,
  type ContentTopic,
  type ContentTopicDetail,
  type ContentVariant,
} from '@/shared/api/content.api';

export function ContentTopicsPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ContentTopic | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<ContentTopicDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [t, b] = await Promise.all([
        fetchContentTopics({ brandId: brandFilter }),
        fetchContentBrands(true),
      ]);
      setTopics(t);
      setBrands(b);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được chủ đề'));
    } finally {
      setLoading(false);
    }
  }, [brandFilter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    return () => {
      Object.values(assetUrls).forEach((u) => URL.revokeObjectURL(u));
    };
  }, [assetUrls]);

  const loadDetail = async (topicId: string) => {
    setDetailLoading(true);
    try {
      const d = await fetchContentTopicDetail(topicId);
      setDetail(d);
      const urls: Record<string, string> = {};
      for (const a of d.assets) {
        try {
          urls[a.id] = await fetchContentAssetObjectUrl(a.id);
        } catch {
          /* preview optional */
        }
      }
      setAssetUrls((prev) => {
        Object.values(prev).forEach((u) => URL.revokeObjectURL(u));
        return urls;
      });
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải chi tiết chủ đề'));
    } finally {
      setDetailLoading(false);
    }
  };

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({
      brandId: brandFilter ?? brands[0]?.id,
      priority: 'P1',
      status: 'Draft',
      goal: 'traffic',
    });
    setOpen(true);
  };

  const openEdit = (row: ContentTopic) => {
    setEditing(row);
    form.setFieldsValue({
      brandId: row.brandId,
      title: row.title,
      pillar: row.pillar ?? undefined,
      goal: row.goal,
      ctaUrl: row.ctaUrl ?? undefined,
      utmCampaign: row.utmCampaign ?? undefined,
      priority: row.priority,
      status: row.status,
      bodyOutline: row.bodyOutline ?? undefined,
    });
    setOpen(true);
  };

  const openDetail = (row: ContentTopic) => {
    setDetailOpen(true);
    void loadDetail(row.id);
  };

  const save = async () => {
    try {
      const v = await form.validateFields();
      if (editing) {
        await updateContentTopic(editing.id, v);
        message.success('Đã cập nhật chủ đề');
      } else {
        await createContentTopic(v);
        message.success('Đã tạo chủ đề');
      }
      setOpen(false);
      await load();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được chủ đề'));
    }
  };

  const onGenerate = async (skipImages = false) => {
    if (!detail) return;
    setBusy(true);
    try {
      const res = await generateContentTopic(detail.topic.id, { skipImages });
      if (res.budgetBlocked) {
        message.warning(res.message ?? 'Đã chặn vì trần ngân sách');
      } else {
        message.success(res.message ?? 'Generate xong');
      }
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Generate thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onSelectAsset = async (asset: ContentAsset) => {
    if (!detail) return;
    setBusy(true);
    try {
      await selectContentAsset(detail.topic.id, asset.id);
      message.success('Đã chọn ảnh');
      await loadDetail(detail.topic.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không chọn được ảnh'));
    } finally {
      setBusy(false);
    }
  };

  const onApprove = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      await approveContentTopic(detail.topic.id);
      message.success('Đã duyệt');
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onPublish = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      const res = await publishContentTopic(detail.topic.id, {
        includeManualExport: true,
        runImmediately: true,
      });
      const failed = res.jobs.filter((j) => j.status === 'Failed').length;
      const ok = res.jobs.filter((j) => j.status === 'Succeeded').length;
      if (failed > 0) message.warning(`Publish: ${ok} OK, ${failed} lỗi`);
      else message.success(`Publish: ${ok} job thành công`);
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Publish thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onRetryJob = async (job: ContentPublishJob) => {
    setBusy(true);
    try {
      await runContentPublishJob(job.id);
      message.success('Đã chạy lại job');
      if (detail) await loadDetail(detail.topic.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chạy lại job thất bại'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Chủ đề / Topic
          </Typography.Title>
          <Typography.Text type="secondary">
            AI generate · chặn trần khi gen ảnh · publish connector
          </Typography.Text>
        </div>
        <Space>
          <Select
            allowClear
            placeholder="Lọc brand"
            style={{ width: 200 }}
            value={brandFilter}
            onChange={setBrandFilter}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={brands.length === 0}>
            Thêm chủ đề
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={topics}
        columns={[
          { title: 'Brand', dataIndex: 'brandName', width: 140 },
          { title: 'Tiêu đề', dataIndex: 'title' },
          { title: 'Pillar', dataIndex: 'pillar', width: 120 },
          { title: 'Priority', dataIndex: 'priority', width: 80 },
          {
            title: 'Status',
            dataIndex: 'status',
            width: 130,
            render: (s: string) => (
              <Tag color={s === 'BudgetBlocked' ? 'red' : s === 'Published' ? 'green' : 'blue'}>{s}</Tag>
            ),
          },
          {
            title: '',
            key: 'a',
            width: 160,
            render: (_, row) => (
              <Space>
                <Button type="link" onClick={() => openDetail(row)}>
                  Chi tiết
                </Button>
                <Button type="link" onClick={() => openEdit(row)}>
                  Sửa
                </Button>
              </Space>
            ),
          },
        ]}
      />

      <Drawer
        title={editing ? 'Sửa chủ đề' : 'Thêm chủ đề'}
        open={open}
        onClose={() => setOpen(false)}
        width={480}
        extra={
          <Button type="primary" onClick={() => void save()}>
            Lưu
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="brandId" label="Brand" rules={[{ required: true }]}>
            <Select options={brands.map((b) => ({ value: b.id, label: b.name }))} />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="pillar" label="Pillar">
            <Input placeholder="inventory / customer / ..." />
          </Form.Item>
          <Form.Item name="bodyOutline" label="Outline / brief">
            <Input.TextArea rows={4} placeholder="Gợi ý góc viết cho AI" />
          </Form.Item>
          <Form.Item name="goal" label="Goal">
            <Select
              options={[
                { value: 'traffic', label: 'traffic' },
                { value: 'seo', label: 'seo' },
                { value: 'lead', label: 'lead' },
                { value: 'phc', label: 'phc' },
                { value: 'other', label: 'other' },
              ]}
            />
          </Form.Item>
          <Form.Item name="ctaUrl" label="CTA URL">
            <Input />
          </Form.Item>
          <Form.Item name="utmCampaign" label="UTM campaign">
            <Input />
          </Form.Item>
          <Form.Item name="priority" label="Priority">
            <Select options={['P0', 'P1', 'P2'].map((p) => ({ value: p, label: p }))} />
          </Form.Item>
          <Form.Item name="status" label="Status">
            <Select
              options={[
                'Draft',
                'Review',
                'Approved',
                'Scheduled',
                'Published',
                'BudgetBlocked',
                'Rejected',
              ].map((s) => ({ value: s, label: s }))}
            />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? `Topic · ${detail.topic.title}` : 'Chi tiết'}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        width={720}
        loading={detailLoading}
        extra={
          detail ? (
            <Space wrap>
              <Button
                icon={<ThunderboltOutlined />}
                loading={busy}
                onClick={() => void onGenerate(false)}
              >
                Generate
              </Button>
              <Button loading={busy} onClick={() => void onGenerate(true)}>
                Text only
              </Button>
              <Button icon={<CheckOutlined />} loading={busy} onClick={() => void onApprove()}>
                Duyệt
              </Button>
              <Button
                type="primary"
                icon={<CloudUploadOutlined />}
                loading={busy}
                onClick={() => void onPublish()}
              >
                Publish
              </Button>
            </Space>
          ) : null
        }
      >
        {detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div>
              <Tag>{detail.topic.status}</Tag>
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {detail.topic.brandName} · {detail.topic.priority} · {detail.topic.goal}
              </Typography.Text>
            </div>

            <Card size="small" title={`Biến thể (${detail.variants.length})`}>
              {detail.variants.length === 0 ? (
                <Typography.Text type="secondary">Chưa generate.</Typography.Text>
              ) : (
                detail.variants.map((v: ContentVariant) => (
                  <div key={v.id} style={{ marginBottom: 12 }}>
                    <Typography.Text strong>
                      {v.kind}
                      {v.title ? ` — ${v.title}` : ''}
                    </Typography.Text>
                    <Typography.Paragraph
                      ellipsis={{ rows: 4, expandable: true }}
                      style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}
                    >
                      {v.bodyMarkdown}
                    </Typography.Paragraph>
                  </div>
                ))
              )}
            </Card>

            <Card size="small" title={`Ảnh ứng viên (${detail.assets.length})`}>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                {detail.assets.map((a) => (
                  <div
                    key={a.id}
                    style={{
                      width: 160,
                      border: a.isSelected ? '2px solid #1677ff' : '1px solid #e2e8f0',
                      borderRadius: 8,
                      padding: 8,
                    }}
                  >
                    {assetUrls[a.id] ? (
                      <img
                        src={assetUrls[a.id]}
                        alt={a.fileName}
                        style={{ width: '100%', height: 100, objectFit: 'cover', borderRadius: 4 }}
                      />
                    ) : (
                      <div
                        style={{
                          height: 100,
                          background: '#f1f5f9',
                          borderRadius: 4,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontSize: 12,
                          color: '#64748b',
                        }}
                      >
                        {a.fileName}
                      </div>
                    )}
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                      ${a.estimateUsd} · {a.model ?? '—'}
                    </Typography.Text>
                    <Button
                      size="small"
                      block
                      type={a.isSelected ? 'primary' : 'default'}
                      style={{ marginTop: 6 }}
                      loading={busy}
                      onClick={() => void onSelectAsset(a)}
                    >
                      {a.isSelected ? 'Đã chọn' : 'Chọn'}
                    </Button>
                  </div>
                ))}
                {detail.assets.length === 0 ? (
                  <Typography.Text type="secondary">Chưa có ảnh (hoặc bị chặn trần).</Typography.Text>
                ) : null}
              </div>
            </Card>

            <Card size="small" title={`Publish jobs (${detail.jobs.length})`}>
              <Table
                size="small"
                rowKey="id"
                pagination={false}
                dataSource={detail.jobs}
                columns={[
                  { title: 'Connector', dataIndex: 'connectorType', width: 120 },
                  {
                    title: 'Status',
                    dataIndex: 'status',
                    width: 100,
                    render: (s: string) => (
                      <Tag color={s === 'Failed' ? 'red' : s === 'Succeeded' ? 'green' : 'default'}>
                        {s}
                      </Tag>
                    ),
                  },
                  {
                    title: 'Ref / lỗi',
                    key: 'info',
                    render: (_, j: ContentPublishJob) => j.lastError || j.externalRef || '—',
                  },
                  {
                    title: '',
                    width: 90,
                    render: (_, j) =>
                      j.status === 'Failed' || j.status === 'Queued' ? (
                        <Button type="link" size="small" loading={busy} onClick={() => void onRetryJob(j)}>
                          Chạy
                        </Button>
                      ) : null,
                  },
                ]}
              />
            </Card>
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
