import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Drawer,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  CopyOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  adaptContentPackage,
  approveContentPackage,
  approveContentPackagesBatch,
  createContentPackage,
  fetchContentBrands,
  fetchContentPackageDetail,
  fetchContentPackages,
  generateContentPackage,
  type ContentBrand,
  type ContentPackage,
  type ContentPackageDetail,
  type ContentVariant,
} from '@/shared/api/content.api';
import { Link } from 'react-router-dom';

const STATUS_LABEL: Record<string, { text: string; color: string }> = {
  Draft: { text: 'Nháp', color: 'default' },
  Generating: { text: 'Đang tạo…', color: 'processing' },
  Review: { text: 'Chờ duyệt', color: 'blue' },
  Approved: { text: 'Đã duyệt', color: 'cyan' },
  Scheduled: { text: 'Đã lên lịch', color: 'geekblue' },
  Published: { text: 'Đã đăng', color: 'green' },
  BudgetBlocked: { text: 'Hết ngân sách', color: 'red' },
  Rejected: { text: 'Từ chối', color: 'orange' },
};

function StatusTag({ status }: { status: string }) {
  const m = STATUS_LABEL[status] ?? { text: status, color: 'default' };
  return <Tag color={m.color}>{m.text}</Tag>;
}

export function ContentPackagesPage() {
  const { message, modal } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<ContentPackageDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [adaptOpen, setAdaptOpen] = useState(false);
  const [adaptSource, setAdaptSource] = useState<ContentPackage | null>(null);
  const [form] = Form.useForm();
  const [adaptForm] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [pkg, brandList] = await Promise.all([
        fetchContentPackages(brandFilter ? { brandId: brandFilter } : undefined),
        fetchContentBrands(true),
      ]);
      setPackages(pkg);
      setBrands(brandList);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được package'));
    } finally {
      setLoading(false);
    }
  }, [brandFilter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const openDetail = async (id: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    try {
      setDetail(await fetchContentPackageDetail(id));
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải chi tiết'));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const onCreate = async () => {
    try {
      const values = await form.validateFields();
      setBusy(true);
      const created = await createContentPackage({
        brandId: values.brandId,
        title: values.title.trim(),
        angle: values.angle?.trim() || undefined,
        audience: values.audience?.trim() || undefined,
        contentType: values.contentType || 'educational',
        pillar: values.pillar?.trim() || undefined,
        goal: values.goal || 'traffic',
        priority: values.priority || 'P1',
        bodyOutline: values.bodyOutline?.trim() || undefined,
        displayAt: values.displayAt || null,
      });
      message.success('Đã tạo ý tưởng');
      setCreateOpen(false);
      form.resetFields();
      await load();
      await openDetail(created.id);
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Tạo package thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onGenerateAll = async (pkg: ContentPackage, skipImages = false) => {
    setBusy(true);
    try {
      const res = await generateContentPackage(pkg.id, { skipImages });
      if (res.budgetBlocked) message.warning(res.message ?? 'Hết ngân sách AI');
      else message.success(res.message ?? `Đã tạo ${res.variants.length} bản viết`);
      await load();
      if (detail?.package.id === pkg.id) await openDetail(pkg.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Generate All thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const confirmGenerate = (pkg: ContentPackage) => {
    modal.confirm({
      title: 'Generate All — AI viết đủ kênh?',
      content: (
        <div>
          <p>
            Ý tưởng: <strong>{pkg.title}</strong> · {pkg.brandName}
          </p>
          <p style={{ marginBottom: 0, color: '#64748b' }}>
            Chỉ tạo các bản đúng nơi đăng đã khai báo (web / FB / TikTok script / caption…). Không tự
            gen brand khác.
          </p>
        </div>
      ),
      okText: 'Generate All',
      cancelText: 'Huỷ',
      onOk: () => onGenerateAll(pkg, false),
    });
  };

  const onApproveOne = async (pkg: ContentPackage) => {
    setBusy(true);
    try {
      await approveContentPackage(pkg.id);
      message.success('Đã duyệt ý tưởng');
      await load();
      if (detail?.package.id === pkg.id) await openDetail(pkg.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onApproveBatch = async () => {
    if (selectedIds.length === 0) {
      message.warning('Chọn ít nhất 1 package');
      return;
    }
    setBusy(true);
    try {
      const res = await approveContentPackagesBatch(selectedIds);
      if (res.failedIds.length > 0) message.warning(res.message ?? 'Duyệt một phần');
      else message.success(res.message ?? `Đã duyệt ${res.approved}`);
      setSelectedIds([]);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt hàng loạt thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const openAdapt = (pkg: ContentPackage) => {
    setAdaptSource(pkg);
    adaptForm.setFieldsValue({
      targetBrandId: undefined,
      title: pkg.title,
      angle: `Góc nhìn mới: ${pkg.angle ?? pkg.title}`,
    });
    setAdaptOpen(true);
  };

  const onAdapt = async () => {
    if (!adaptSource) return;
    try {
      const values = await adaptForm.validateFields();
      setBusy(true);
      const created = await adaptContentPackage(adaptSource.id, {
        targetBrandId: values.targetBrandId,
        title: values.title?.trim() || undefined,
        angle: values.angle?.trim() || undefined,
        bodyOutline: values.bodyOutline?.trim() || undefined,
      });
      message.success(`Đã tạo bản cho ${created.brandName} — chưa Generate (bấm Generate All khi duyệt góc nhìn).`);
      setAdaptOpen(false);
      setAdaptSource(null);
      adaptForm.resetFields();
      await load();
      await openDetail(created.id);
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Sang brand khác thất bại'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Ý tưởng (đa bản viết)
          </Typography.Title>
          <Typography.Text type="secondary">
            Chỉ tab này: 1 ý → <strong>Generate All</strong> (nhiều bản) → duyệt → sang{' '}
            <Link to="/content/topics">Bài viết</Link>.{' '}
            <strong>Sang brand khác</strong> = nhân bản ý tưởng sang Famixa/Novixa… (chưa viết lại AI).
            Videos cũng lấy script từ đây.
          </Typography.Text>
        </div>
        <Space wrap>
          <Select
            allowClear
            placeholder="Lọc thương hiệu"
            style={{ width: 180 }}
            value={brandFilter}
            onChange={setBrandFilter}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button
            icon={<CheckOutlined />}
            disabled={selectedIds.length === 0}
            loading={busy}
            onClick={() => void onApproveBatch()}
          >
            Duyệt đã chọn ({selectedIds.length})
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            Ý tưởng mới
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={packages}
        pagination={{ pageSize: 20 }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
          getCheckboxProps: (row) => ({
            disabled: row.status === 'Approved' || row.variantCount === 0,
          }),
        }}
        columns={[
          {
            title: 'Ý tưởng',
            dataIndex: 'title',
            render: (title: string, row) => (
              <Button type="link" style={{ padding: 0, height: 'auto' }} onClick={() => void openDetail(row.id)}>
                {title}
              </Button>
            ),
          },
          { title: 'Brand', dataIndex: 'brandName', width: 140 },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            width: 120,
            render: (s: string) => <StatusTag status={s} />,
          },
          {
            title: 'Bản viết',
            dataIndex: 'variantCount',
            width: 90,
            align: 'center',
          },
          {
            title: 'Nguồn',
            dataIndex: 'sourcePackageId',
            width: 90,
            render: (v: string | null | undefined) => (v ? <Tag>Từ brand khác</Tag> : '—'),
          },
          {
            title: '',
            width: 320,
            render: (_: unknown, row: ContentPackage) => (
              <Space size={4} wrap>
                <Button
                  size="small"
                  type="primary"
                  icon={<ThunderboltOutlined />}
                  loading={busy}
                  onClick={() => confirmGenerate(row)}
                >
                  Generate All
                </Button>
                <Button
                  size="small"
                  icon={<CheckOutlined />}
                  disabled={row.variantCount === 0 || row.status === 'Approved'}
                  loading={busy}
                  onClick={() => void onApproveOne(row)}
                >
                  Duyệt
                </Button>
                <Button size="small" icon={<CopyOutlined />} onClick={() => openAdapt(row)}>
                  Sang brand khác
                </Button>
              </Space>
            ),
          },
        ]}
      />

      <Drawer
        title="Tạo ý tưởng mới"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        width={480}
        extra={
          <Button type="primary" loading={busy} onClick={() => void onCreate()}>
            Tạo
          </Button>
        }
      >
        <Form form={form} layout="vertical" initialValues={{ contentType: 'educational', goal: 'traffic', priority: 'P1' }}>
          <Form.Item name="brandId" label="Thương hiệu" rules={[{ required: true }]}>
            <Select options={brands.map((b) => ({ value: b.id, label: b.name }))} placeholder="Chọn brand" />
          </Form.Item>
          <Form.Item name="title" label="Ý tưởng / tiêu đề" rules={[{ required: true }]}>
            <Input placeholder="AI đang thay đổi cách quản lý công việc…" />
          </Form.Item>
          <Form.Item name="angle" label="Góc nhìn (angle)">
            <Input.TextArea rows={2} placeholder="AI không thay thế — AI đổi cách vận hành…" />
          </Form.Item>
          <Form.Item name="audience" label="Đối tượng">
            <Input placeholder="CEO / Founder / IT Manager" />
          </Form.Item>
          <Form.Item name="pillar" label="Pillar / series">
            <Input placeholder="AI Agent" />
          </Form.Item>
          <Form.Item name="bodyOutline" label="Gợi ý / dàn ý">
            <Input.TextArea rows={4} placeholder="Các ý chính muốn nhấn…" />
          </Form.Item>
          <Space>
            <Form.Item name="contentType" label="Loại">
              <Select
                style={{ width: 160 }}
                options={[
                  { value: 'educational', label: 'Educational' },
                  { value: 'product', label: 'Product' },
                  { value: 'insight', label: 'Insight' },
                  { value: 'story', label: 'Story' },
                ]}
              />
            </Form.Item>
            <Form.Item name="priority" label="Ưu tiên">
              <Select
                style={{ width: 100 }}
                options={[
                  { value: 'P0', label: 'P0' },
                  { value: 'P1', label: 'P1' },
                  { value: 'P2', label: 'P2' },
                ]}
              />
            </Form.Item>
          </Space>
        </Form>
      </Drawer>

      <Modal
        title={adaptSource ? `Sang brand khác · ${adaptSource.title}` : 'Sang brand khác'}
        open={adaptOpen}
        onCancel={() => setAdaptOpen(false)}
        onOk={() => void onAdapt()}
        confirmLoading={busy}
        okText="Tạo bản cho brand đích"
      >
        <Typography.Paragraph type="secondary">
          Nhân bản ý tưởng sang brand đích (Nháp). Chỉnh góc nhìn rồi mới Generate All — không copy nguyên bài.
        </Typography.Paragraph>
        <Form form={adaptForm} layout="vertical">
          <Form.Item
            name="targetBrandId"
            label="Brand đích"
            rules={[{ required: true, message: 'Chọn brand đích' }]}
          >
            <Select
              options={brands
                .filter((b) => b.id !== adaptSource?.brandId)
                .map((b) => ({ value: b.id, label: b.name }))}
              placeholder="Novixa / Famixa / …"
            />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề (tuỳ chỉnh)">
            <Input />
          </Form.Item>
          <Form.Item name="angle" label="Góc nhìn mới">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="bodyOutline" label="Ghi chú thêm">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      <Drawer
        title={detail ? detail.package.title : 'Chi tiết package'}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        width={720}
        loading={detailLoading}
        extra={
          detail ? (
            <Space>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                loading={busy}
                onClick={() => confirmGenerate(detail.package)}
              >
                Generate All
              </Button>
              <Button
                icon={<CheckOutlined />}
                loading={busy}
                disabled={
                  detail.topicDetail.variants.length === 0 || detail.package.status === 'Approved'
                }
                onClick={() => void onApproveOne(detail.package)}
              >
                Duyệt
              </Button>
              <Button icon={<CopyOutlined />} onClick={() => openAdapt(detail.package)}>
                Sang brand khác
              </Button>
            </Space>
          ) : null
        }
      >
        {detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div>
              <StatusTag status={detail.package.status} />
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {detail.package.brandName}
                {detail.package.angle ? ` · ${detail.package.angle}` : ''}
              </Typography.Text>
              <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                Topic liên kết:{' '}
                <Typography.Text code>{detail.package.topicId.slice(0, 8)}…</Typography.Text> — xuất
                bản / ảnh ở tab <Link to="/content/topics">Bài viết</Link>.
              </Typography.Paragraph>
            </div>

            <Card size="small" title={`Bản viết (${detail.topicDetail.variants.length})`}>
              {detail.topicDetail.variants.length === 0 ? (
                <Typography.Text type="secondary">Chưa có — bấm Generate All.</Typography.Text>
              ) : (
                detail.topicDetail.variants.map((v: ContentVariant) => (
                  <div key={v.id} style={{ marginBottom: 12 }}>
                    <Typography.Text strong>
                      {v.kind}
                      {v.title ? ` — ${v.title}` : ''}
                    </Typography.Text>
                    <Typography.Paragraph
                      ellipsis={{ rows: 3, expandable: true, symbol: 'xem thêm' }}
                      style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}
                    >
                      {v.bodyMarkdown}
                    </Typography.Paragraph>
                  </div>
                ))
              )}
            </Card>
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
