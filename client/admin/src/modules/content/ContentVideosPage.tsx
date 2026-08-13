import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Drawer,
  Form,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  approveContentVideoJob,
  createContentVideoJobFromPackage,
  fetchContentBrands,
  fetchContentPackages,
  fetchContentVideoJobs,
  fetchContentVideoTemplates,
  prepareContentVideoStoryboard,
  refreshContentVideoJob,
  renderContentVideoJob,
  runContentVideoMvpPipeline,
  type ContentBrand,
  type ContentPackage,
  type ContentVideoJob,
  type ContentVideoTemplate,
} from '@/shared/api/content.api';

const STATUS_LABEL: Record<string, { text: string; color: string }> = {
  Draft: { text: 'Nháp', color: 'default' },
  GeneratingScript: { text: 'Đang viết script…', color: 'processing' },
  GeneratingAssets: { text: 'Đang tạo ảnh…', color: 'processing' },
  GeneratingVoice: { text: 'Đang tạo voice…', color: 'processing' },
  PreparingRender: { text: 'Chuẩn bị render…', color: 'processing' },
  Queued: { text: 'Hàng đợi', color: 'processing' },
  Rendering: { text: 'Đang render…', color: 'blue' },
  Ready: { text: 'Sẵn sàng', color: 'cyan' },
  Failed: { text: 'Lỗi', color: 'red' },
  Approved: { text: 'Đã duyệt', color: 'green' },
};

type StoryBeat = {
  beat?: string;
  order?: number;
  type?: string;
  startSec?: number;
  endSec?: number;
  text?: string;
  visualHint?: string;
  visualPrompt?: string;
  imageUrl?: string;
};

function parseStoryboard(json: string): StoryBeat[] {
  try {
    const raw = JSON.parse(json || '[]') as StoryBeat[];
    return Array.isArray(raw) ? raw : [];
  } catch {
    return [];
  }
}

export function ContentVideosPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [jobs, setJobs] = useState<ContentVideoJob[]>([]);
  const [templates, setTemplates] = useState<ContentVideoTemplate[]>([]);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [createOpen, setCreateOpen] = useState(false);
  const [detail, setDetail] = useState<ContentVideoJob | null>(null);
  const [busy, setBusy] = useState(false);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [jobList, tpl, pkg, brandList] = await Promise.all([
        fetchContentVideoJobs(brandFilter ? { brandId: brandFilter } : undefined),
        fetchContentVideoTemplates(true),
        fetchContentPackages(brandFilter ? { brandId: brandFilter } : undefined),
        fetchContentBrands(true),
      ]);
      setJobs(jobList);
      setTemplates(tpl);
      setPackages(pkg.filter((p) => p.variantCount > 0));
      setBrands(brandList);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được Video Factory'));
    } finally {
      setLoading(false);
    }
  }, [brandFilter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const existingPackageIds = useMemo(
    () => new Set(jobs.map((j) => j.packageId).filter(Boolean) as string[]),
    [jobs],
  );

  const openCreate = () => {
    const scoped = brandFilter ? packages.filter((p) => p.brandId === brandFilter) : packages;
    form.setFieldsValue({
      brandId: brandFilter,
      packageIds: scoped.filter((p) => !existingPackageIds.has(p.id)).map((p) => p.id),
      templateId: templates.find((t) => t.code === 'tiktok_45s_hooks')?.id ?? templates[0]?.id,
      autoStoryboard: true,
      skipExisting: true,
    });
    setCreateOpen(true);
  };

  const modalBrandId = Form.useWatch('brandId', form);
  const packagesForModal = useMemo(() => {
    if (!modalBrandId) return packages;
    return packages.filter((p) => p.brandId === modalBrandId);
  }, [packages, modalBrandId]);

  const packageOptions = useMemo(
    () =>
      packagesForModal.map((p) => ({
        value: p.id,
        label: `${existingPackageIds.has(p.id) ? '✓ ' : ''}${p.brandName} · ${p.title} (${p.status})`,
      })),
    [packagesForModal, existingPackageIds],
  );

  const onCreate = async () => {
    const values = await form.validateFields();
    const packageIds = (values.packageIds as string[]) ?? [];
    if (packageIds.length === 0) {
      message.warning('Chọn ít nhất một package');
      return;
    }
    setBusy(true);
    try {
      let created = 0;
      let skipped = 0;
      let failed = 0;
      for (const packageId of packageIds) {
        if (values.skipExisting && existingPackageIds.has(packageId)) {
          skipped += 1;
          continue;
        }
        try {
          const job = await createContentVideoJobFromPackage({
            packageId,
            templateId: values.templateId,
          });
          if (values.autoStoryboard) {
            await prepareContentVideoStoryboard(job.id);
          }
          created += 1;
        } catch {
          failed += 1;
        }
      }
      setCreateOpen(false);
      form.resetFields();
      if (created > 0) {
        message.success(
          `Đã tạo ${created} video job` +
            (values.autoStoryboard ? ' + storyboard' : '') +
            (skipped ? ` · bỏ qua ${skipped} đã có` : '') +
            (failed ? ` · lỗi ${failed}` : ''),
        );
      } else if (skipped > 0 && failed === 0) {
        message.info(
          `Không tạo mới — ${skipped} package đã có video job (bỏ tick «Bỏ qua đã có» nếu muốn tạo thêm).`,
        );
      } else {
        message.warning(
          `Không tạo được job nào${failed ? ` (${failed} lỗi)` : ''}. Ý tưởng cần đã Generate (có tiktok_script).`,
        );
      }
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo video job thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const runAction = async (
    job: ContentVideoJob,
    action: 'storyboard' | 'render' | 'refresh' | 'approve' | 'mvp',
  ) => {
    setBusy(true);
    try {
      let next: ContentVideoJob;
      if (action === 'storyboard') next = await prepareContentVideoStoryboard(job.id);
      else if (action === 'mvp') next = await runContentVideoMvpPipeline(job.id);
      else if (action === 'render') next = await renderContentVideoJob(job.id);
      else if (action === 'refresh') next = await refreshContentVideoJob(job.id);
      else next = await approveContentVideoJob(job.id);
      message.success(
        action === 'mvp'
          ? next.outputUrl
            ? 'MVP xong — có MP4'
            : next.errorMessage || 'MVP xong (storyboard/assets; cần Creatomate để có MP4)'
          : action === 'render'
            ? 'Đã queue / chuẩn bị render'
            : action === 'storyboard'
              ? 'Đã chuẩn bị storyboard'
              : action === 'approve'
                ? 'Đã duyệt'
                : 'Đã refresh',
      );
      setDetail(next);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Thao tác thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const storyboard = detail ? parseStoryboard(detail.storyboardJson) : [];

  return (
    <div>
      <Card
        title={
          <Space>
            <VideoCameraOutlined />
            Videos
          </Space>
        }
        extra={
          <Space wrap>
            <Select
              allowClear
              placeholder="Lọc brand"
              style={{ minWidth: 180 }}
              value={brandFilter}
              onChange={(v) => setBrandFilter(v)}
              options={brands.map((b) => ({ value: b.id, label: b.name }))}
            />
            <Button icon={<ReloadOutlined />} onClick={() => void load()}>
              Tải lại
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Tạo từ ý tưởng
            </Button>
          </Space>
        }
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="MVP V1 — pipeline tới MP4 (Creatomate)"
          description={
            <>
              Luồng: Ý tưởng đã Generate → tạo job → <strong>Chạy MVP</strong> (ảnh scene + voice tuỳ
              chọn + Creatomate). Không có Creatomate key / template UUID thì vẫn ra storyboard để dựng
              CapCut. Publish MXH = giai đoạn sau.
            </>
          }
        />

        <Table
          rowKey="id"
          loading={loading}
          dataSource={jobs}
          pagination={{ pageSize: 20 }}
          onRow={(row) => ({
            onClick: () => setDetail(row),
            style: { cursor: 'pointer' },
          })}
          columns={[
            {
              title: 'Tiêu đề',
              dataIndex: 'title',
              ellipsis: true,
              render: (v: string, row) => (
                <Space direction="vertical" size={0}>
                  <Typography.Text strong>{v}</Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {row.brandName} · {row.templateName}
                  </Typography.Text>
                </Space>
              ),
            },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              width: 120,
              render: (s: string) => {
                const m = STATUS_LABEL[s] ?? { text: s, color: 'default' };
                return <Tag color={m.color}>{m.text}</Tag>;
              },
            },
            {
              title: 'Provider',
              dataIndex: 'provider',
              width: 140,
              render: (p: string) => <Tag>{p}</Tag>,
            },
            {
              title: 'Ý tưởng',
              dataIndex: 'packageId',
              width: 100,
              render: (id?: string | null) =>
                id ? <Link to="/content/packages">Mở</Link> : '—',
            },
          ]}
        />
      </Card>

      <Modal
        title="Tạo video từ ý tưởng đã Generate"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void onCreate()}
        confirmLoading={busy}
        destroyOnClose
        okText="Tạo hàng loạt"
        width={560}
      >
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 16 }}
          message="Mỗi ý tưởng thuộc một thương hiệu — lọc brand rồi chọn ý tưởng đó. Không liên quan đã đăng FB."
        />
        <Form
          form={form}
          layout="vertical"
          onValuesChange={(changed) => {
            if ('brandId' in changed) {
              const scoped = changed.brandId
                ? packages.filter((p) => p.brandId === changed.brandId)
                : packages;
              form.setFieldValue(
                'packageIds',
                scoped.filter((p) => !existingPackageIds.has(p.id)).map((p) => p.id),
              );
            }
          }}
        >
          <Form.Item
            name="brandId"
            label="Thương hiệu"
            extra="Để trống = hiện package mọi brand. Chọn Novixa thì chỉ tạo video cho Novixa."
          >
            <Select
              allowClear
              placeholder="Tất cả thương hiệu"
              options={brands.map((b) => ({ value: b.id, label: `${b.name} (${b.code})` }))}
            />
          </Form.Item>
          <Form.Item
            name="packageIds"
            label={`Ý tưởng đã Generate (${packagesForModal.length})`}
            rules={[{ required: true, message: 'Chọn ít nhất 1 package' }]}
            extra="Có thể chọn nhiều · ✓ = đã có video job · brand lấy theo từng package"
          >
            <Select
              mode="multiple"
              allowClear
              showSearch
              optionFilterProp="label"
              options={packageOptions}
              placeholder="Chọn một hoặc nhiều package"
              maxTagCount="responsive"
            />
          </Form.Item>
          <Space style={{ marginBottom: 12 }} wrap>
            <Button
              size="small"
              onClick={() =>
                form.setFieldValue(
                  'packageIds',
                  packagesForModal.filter((p) => !existingPackageIds.has(p.id)).map((p) => p.id),
                )
              }
            >
              Chọn chưa có video
            </Button>
            <Button
              size="small"
              onClick={() => form.setFieldValue('packageIds', packagesForModal.map((p) => p.id))}
            >
              Chọn tất cả
            </Button>
            <Button size="small" onClick={() => form.setFieldValue('packageIds', [])}>
              Bỏ chọn
            </Button>
          </Space>
          <Form.Item
            name="templateId"
            label="Template chung"
            rules={[{ required: true, message: 'Chọn template' }]}
          >
            <Select
              options={templates.map((t) => ({
                value: t.id,
                label: `${t.name} (${t.aspectRatio} · ${t.durationSec}s · ${t.provider})`,
              }))}
            />
          </Form.Item>
          <Form.Item name="autoStoryboard" valuePropName="checked" initialValue={true}>
            <Checkbox>Tự tạo storyboard ngay sau khi tạo job</Checkbox>
          </Form.Item>
          <Form.Item name="skipExisting" valuePropName="checked" initialValue={true}>
            <Checkbox>Bỏ qua package đã có video job</Checkbox>
          </Form.Item>
        </Form>
      </Modal>

      <Drawer
        title={detail?.title ?? 'Chi tiết video'}
        width={560}
        open={!!detail}
        onClose={() => setDetail(null)}
        extra={
          detail ? (
            <Space wrap>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                loading={busy}
                onClick={() => void runAction(detail, 'mvp')}
              >
                Chạy MVP (ảnh→voice→render)
              </Button>
              <Button loading={busy} onClick={() => void runAction(detail, 'storyboard')}>
                Storyboard
              </Button>
              <Button loading={busy} onClick={() => void runAction(detail, 'render')}>
                Render
              </Button>
              {detail.externalRenderId ? (
                <Button loading={busy} onClick={() => void runAction(detail, 'refresh')}>
                  Refresh
                </Button>
              ) : null}
              <Button
                icon={<CheckOutlined />}
                disabled={detail.status !== 'Ready' && detail.status !== 'Approved'}
                loading={busy}
                onClick={() => void runAction(detail, 'approve')}
              >
                Duyệt
              </Button>
            </Space>
          ) : null
        }
      >
        {detail ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <div>
              <Tag color={(STATUS_LABEL[detail.status] ?? { color: 'default' }).color}>
                {(STATUS_LABEL[detail.status] ?? { text: detail.status }).text}
              </Tag>
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {detail.provider} · {detail.templateName}
              </Typography.Text>
            </div>
            {detail.errorMessage ? <Alert type="warning" showIcon message={detail.errorMessage} /> : null}
            {detail.outputUrl ? (
              <Card
                size="small"
                title="Preview MP4"
                extra={
                  <a href={detail.outputUrl} target="_blank" rel="noreferrer" download>
                    Tải về
                  </a>
                }
              >
                <video
                  src={detail.outputUrl}
                  controls
                  playsInline
                  style={{ width: '100%', maxHeight: 420, background: '#0f172a', borderRadius: 8 }}
                />
              </Card>
            ) : detail.previewUrl ? (
              <Card size="small" title="Preview">
                <a href={detail.previewUrl} target="_blank" rel="noreferrer">
                  {detail.previewUrl}
                </a>
              </Card>
            ) : null}
            <Card size="small" title="Script">
              <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>
                {detail.scriptBody}
              </Typography.Paragraph>
            </Card>
            <Card size="small" title={`Storyboard (${storyboard.length} scenes)`}>
              {storyboard.length === 0 ? (
                <Typography.Text type="secondary">Chưa có — bấm Storyboard hoặc Chạy MVP.</Typography.Text>
              ) : (
                <Space direction="vertical" style={{ width: '100%' }} size="small">
                  {storyboard.map((b, i) => (
                    <div key={`${b.beat}-${i}`} style={{ borderBottom: '1px solid #f0f0f0', paddingBottom: 8 }}>
                      <Typography.Text strong>
                        {b.beat ?? `Scene ${i + 1}`}
                        {b.startSec != null && b.endSec != null ? ` · ${b.startSec}–${b.endSec}s` : ''}
                      </Typography.Text>
                      <Typography.Paragraph style={{ marginBottom: 4, whiteSpace: 'pre-wrap' }}>
                        {b.text}
                      </Typography.Paragraph>
                      {b.imageUrl ? (
                        <img
                          src={b.imageUrl}
                          alt={b.beat ?? 'scene'}
                          style={{ width: 96, height: 160, objectFit: 'cover', borderRadius: 6 }}
                        />
                      ) : b.visualHint ? (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Visual: {b.visualHint}
                        </Typography.Text>
                      ) : null}
                    </div>
                  ))}
                </Space>
              )}
            </Card>
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
