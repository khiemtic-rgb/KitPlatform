import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Drawer,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  ClusterOutlined,
  CopyOutlined,
  DownloadOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  adaptContentPackage,
  adaptContentPackageMulti,
  approveContentPackage,
  approveContentPackagesBatch,
  createContentPackage,
  fetchContentBrands,
  fetchContentChannels,
  fetchContentPackageDetail,
  fetchContentSites,
  fetchContentPackages,
  downloadContentPackageExport,
  fetchContentAssetObjectUrl,
  fetchContentWritePlans,
  generateContentPackage,
  type ContentAsset,
  type ContentBrand,
  type ContentChannelTarget,
  type ContentPackage,
  type ContentPackageDetail,
  type ContentSiteTarget,
  type ContentVariant,
  type ContentWritePlan,
} from '@/shared/api/content.api';
import { Link, useNavigate } from 'react-router-dom';
import { kindsFromWriteSlots, writeSlotLabel } from '@/modules/content/content-channels';
import { ContentManualPostTab } from '@/modules/content/ContentManualPostTab';
import { writeClipboardImage } from '@/modules/content/content-manual-dest';
import { ContentPackageBriefCard } from '@/modules/content/ContentPackageBriefCard';
import { ContentPackagePerformanceCard } from '@/modules/content/ContentPackagePerformanceCard';
import {
  CONTENT_BRIEF_EMOTIONS,
  CONTENT_BRIEF_FORMATS,
  CONTENT_BRIEF_OBJECTIVES,
} from '@/modules/content/content-brief';

const VARIANT_KIND_LABEL: Record<string, string> = {
  web_long: 'Website',
  seo_meta: 'SEO',
  fb_page: 'Facebook',
  fb_short: 'Facebook ngắn',
  social_caption: 'Caption MXH',
  group_suggested: 'Bài nhóm (giọng thành viên)',
  tiktok_script: 'Video / TikTok',
};

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
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [writePlans, setWritePlans] = useState<ContentWritePlan[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [sourceFilter, setSourceFilter] = useState<string | undefined>();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<ContentPackageDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [sites, setSites] = useState<ContentSiteTarget[]>([]);
  const [busy, setBusy] = useState(false);
  const [adaptOpen, setAdaptOpen] = useState(false);
  const [adaptSource, setAdaptSource] = useState<ContentPackage | null>(null);
  const [multiOpen, setMultiOpen] = useState(false);
  const [multiSource, setMultiSource] = useState<ContentPackage | null>(null);
  const [multiBrandIds, setMultiBrandIds] = useState<string[]>([]);
  const [includeMaybe, setIncludeMaybe] = useState(true);
  const [generateFits, setGenerateFits] = useState(false);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});
  const [form] = Form.useForm();
  const [adaptForm] = Form.useForm();

  const coreOptions = packages
    .filter((p) => !p.sourcePackageId)
    .map((p) => ({ value: p.id, label: p.title }));

  const visiblePackages = packages
    .filter((p) => {
      if (!p.sourcePackageId) return false;
      if (!sourceFilter) return true;
      return p.sourcePackageId === sourceFilter;
    })
    .slice()
    .sort((a, b) => {
      const ga = a.sourcePackageId ?? a.id;
      const gb = b.sourcePackageId ?? b.id;
      const ta = a.sourceTitle || a.title;
      const tb = b.sourceTitle || b.title;
      const g = ta.localeCompare(tb, 'vi');
      if (g !== 0) return g;
      if (ga !== gb) return ga.localeCompare(gb);
      const ac = a.sourcePackageId ? 1 : 0;
      const bc = b.sourcePackageId ? 1 : 0;
      if (ac !== bc) return ac - bc;
      return a.brandName.localeCompare(b.brandName, 'vi');
    });

  const sourceBanner = sourceFilter
    ? packages.find((p) => p.id === sourceFilter)
    : undefined;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [pkg, brandList, plans] = await Promise.all([
        fetchContentPackages(brandFilter ? { brandId: brandFilter } : undefined),
        fetchContentBrands(true),
        fetchContentWritePlans(),
      ]);
      setPackages(pkg);
      setBrands(brandList);
      setWritePlans(plans);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được package'));
    } finally {
      setLoading(false);
    }
  }, [brandFilter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const revokeAssetUrls = (urls: Record<string, string>) => {
    for (const url of Object.values(urls)) URL.revokeObjectURL(url);
  };

  const openDetail = async (id: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    try {
      const next = await fetchContentPackageDetail(id);
      setDetail(next);
      try {
        const [ch, st] = await Promise.all([
          fetchContentChannels(next.package.brandId),
          fetchContentSites(next.package.brandId),
        ]);
        setChannels(ch);
        setSites(st);
      } catch {
        setChannels([]);
        setSites([]);
      }
      revokeAssetUrls(assetUrls);
      const urls: Record<string, string> = {};
      for (const a of next.topicDetail.assets) {
        try {
          urls[a.id] = await fetchContentAssetObjectUrl(a.id);
        } catch {
          /* preview optional */
        }
      }
      setAssetUrls(urls);
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
        insight: values.insight?.trim() || undefined,
        problem: values.problem?.trim() || undefined,
        coreMessage: values.coreMessage?.trim() || undefined,
        source: values.source?.trim() || undefined,
        sourceUrl: values.sourceUrl?.trim() || undefined,
        sourceType: values.sourceType || undefined,
        evidence: values.evidence?.trim() || undefined,
        factOrOpinion: values.factOrOpinion || undefined,
        creativeBrief: {
          objective: values.briefObjective || undefined,
          emotion: values.briefEmotion || undefined,
          format: values.briefFormat || undefined,
          visualDirection: values.briefVisual?.trim() || undefined,
          durationSec: values.briefDuration || undefined,
        },
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

  const onGenerateAll = async (pkg: ContentPackage, skipImages = false, variantKinds?: string[]) => {
    setBusy(true);
    try {
      const res = await generateContentPackage(pkg.id, { skipImages, variantKinds });
      message.success(res.message ?? 'AI đã viết xong — kiểm tra bản ở Bài viết.');
      await load();
      if (detail?.package.id === pkg.id) await openDetail(pkg.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Generate thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const confirmGenerate = (pkg: ContentPackage) => {
    const plan = writePlans.find((p) => p.brandId === pkg.brandId);
    if (!plan || plan.slots.length === 0) {
      modal.confirm({
        title: 'Chưa có nơi đăng',
        content: `${pkg.brandName} chưa khai báo Website / Fanpage / nhóm. Generate chỉ viết đúng nơi đăng.`,
        okText: 'Mở Thương hiệu',
        cancelText: 'Huỷ',
        onOk: () => navigate('/content/brands'),
      });
      return;
    }
    let keys = plan.slots.map((s) => s.key);
    modal.confirm({
      title: 'Generate theo nơi đăng',
      content: (
        <div>
          <p>
            Ý tưởng: <strong>{pkg.title}</strong> · {pkg.brandName}
          </p>
          <p style={{ color: '#64748b' }}>{plan.summary}. Bỏ tick chỗ lần này không cần.</p>
          <Checkbox.Group
            defaultValue={keys}
            style={{ display: 'flex', flexDirection: 'column', gap: 6 }}
            options={plan.slots.map((s) => ({ value: s.key, label: writeSlotLabel(s) }))}
            onChange={(v) => {
              keys = v.map(String);
            }}
          />
        </div>
      ),
      okText: 'Generate',
      cancelText: 'Huỷ',
      onOk: () => {
        const slots = plan.slots.filter((s) => keys.includes(s.key));
        if (slots.length === 0) {
          message.warning('Tick ít nhất một nơi đăng');
          return Promise.reject();
        }
        return onGenerateAll(pkg, false, kindsFromWriteSlots(slots));
      },
    });
  };

  const onExport = async (pkg: ContentPackage) => {
    setBusy(true);
    try {
      await downloadContentPackageExport(pkg.id);
      message.success('Đã tải pack đăng thủ công (zip)');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Xuất pack thất bại — cần có bản viết'));
    } finally {
      setBusy(false);
    }
  };

  const selectedRows = visiblePackages.filter((p) => selectedIds.includes(p.id));

  const confirmGenerateBatch = () => {
    const rows = selectedRows;
    if (rows.length === 0) {
      message.warning('Chọn ít nhất một góc / ý tưởng');
      return;
    }
    modal.confirm({
      title: `Generate ${rows.length} góc đã chọn`,
      content: (
        <div>
          <p>Mỗi góc viết đúng nơi đăng của brand đó — không đăng lên Facebook/web.</p>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {writePlans
              .filter((p) => rows.some((r) => r.brandId === p.brandId))
              .map((p) => `${p.brandName}: ${p.slots.length === 0 ? 'chưa có nơi đăng' : p.summary}`)
              .join(' · ') || 'Chưa tải nơi đăng.'}
          </Typography.Paragraph>
        </div>
      ),
      okText: 'Generate',
      onOk: async () => {
        const targets = rows.filter((p) => p.sourcePackageId);
        if (targets.length === 0) {
          message.warning('Chọn góc brand — ý tưởng gốc không Generate');
          return;
        }
        const missing = targets.filter((row) => {
          const plan = writePlans.find((p) => p.brandId === row.brandId);
          return !plan || plan.slots.length === 0;
        });
        if (missing.length > 0) {
          message.warning(
            `Chưa có nơi đăng: ${[...new Set(missing.map((r) => r.brandName))].join(', ')}. Vào Thương hiệu.`,
          );
          return;
        }
        setBusy(true);
        let ok = 0;
        const failed: string[] = [];
        try {
          for (const row of targets) {
            try {
              await generateContentPackage(row.id);
              ok++;
            } catch (e) {
              failed.push(`${row.brandName}: ${apiErrorMessage(e, 'lỗi')}`);
            }
          }
          if (failed.length === 0) {
            message.success(`AI đã viết xong ${ok}/${targets.length} góc — cột Bản viết phải > 0`);
          } else {
            message.error(`Viết được ${ok}/${targets.length}. Lỗi: ${failed.join(' · ')}`);
          }
          setSelectedIds([]);
          await load();
        } finally {
          setBusy(false);
        }
      },
    });
  };

  const onExportBatch = async () => {
    const rows = selectedRows.filter((p) => p.variantCount > 0);
    if (rows.length === 0) {
      message.warning('Chọn góc đã có bản viết để xuất pack đăng tay');
      return;
    }
    setBusy(true);
    let ok = 0;
    try {
      for (const row of rows) {
        await downloadContentPackageExport(row.id);
        ok++;
      }
      message.success(`Đã tải ${ok} pack (đăng thủ công — không auto-post)`);
    } catch (e) {
      message.error(apiErrorMessage(e, `Xuất dở (${ok}/${rows.length})`));
    } finally {
      setBusy(false);
    }
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

  const openMulti = (pkg: ContentPackage) => {
    setMultiSource(pkg);
    setMultiBrandIds(brands.filter((b) => b.id !== pkg.brandId).map((b) => b.id));
    setIncludeMaybe(true);
    setGenerateFits(false);
    setMultiOpen(true);
  };

  const onMultiAdapt = async () => {
    if (!multiSource) return;
    if (multiBrandIds.length === 0) {
      message.warning('Chọn ít nhất một brand để chấm Fit');
      return;
    }
    setBusy(true);
    try {
      await adaptContentPackageMulti(multiSource.id, {
        brandIds: multiBrandIds,
        includeMaybe,
        generateFits,
      });
      message.success('Đã chấm Fit và tạo góc nhìn riêng — brand không phù hợp đã bỏ.');
      setMultiOpen(false);
      await load();
      await openDetail(multiSource.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Adapt nhiều brand thất bại'));
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
      message.success(`Đã tạo bản cho ${created.brandName} — chưa Generate (chọn kênh khi duyệt góc).`);
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
            Core idea → góc brand → Generate theo nơi đăng. Nhập cả tuần ở{' '}
            <Link to="/content/pool">Idea Pool</Link> — AI chấm Fit, bạn tick ô nào dùng.
          </Typography.Text>
        </div>
        <Space wrap>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Lọc ý tưởng gốc"
            style={{ minWidth: 240 }}
            value={sourceFilter}
            onChange={setSourceFilter}
            options={coreOptions}
          />
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
            icon={<ThunderboltOutlined />}
            disabled={selectedIds.length === 0}
            loading={busy}
            onClick={() => confirmGenerateBatch()}
          >
            Generate đã chọn
          </Button>
          <Button
            icon={<DownloadOutlined />}
            disabled={selectedIds.length === 0}
            loading={busy}
            onClick={() => void onExportBatch()}
          >
            Pack đã chọn
          </Button>
          <Button
            icon={<CheckOutlined />}
            disabled={selectedIds.length === 0}
            loading={busy}
            onClick={() => void onApproveBatch()}
          >
            Duyệt đã chọn ({selectedIds.length})
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              const home = brands.find((b) => /kit/i.test(b.code)) ?? brands[0];
              form.setFieldsValue({ brandId: home?.id });
              setCreateOpen(true);
            }}
          >
            Ý tưởng mới
          </Button>
        </Space>
      </div>

      {sourceBanner ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={`Ý tưởng gốc: ${sourceBanner.title}`}
          description={
            sourceBanner.coreIdea?.insight ||
            sourceBanner.angle ||
            'Các hàng bên dưới chỉ là góc brand — Generate / Pack từng brand, không đăng tự động.'
          }
          action={
            <Space direction="vertical">
              <Button size="small" icon={<ClusterOutlined />} onClick={() => openMulti(sourceBanner)}>
                Adapt nhiều brand
              </Button>
              <Button size="small" onClick={() => openAdapt(sourceBanner)}>
                1 brand
              </Button>
            </Space>
          }
        />
      ) : null}

      <Table
        rowKey="id"
        loading={loading}
        dataSource={visiblePackages}
        pagination={{ pageSize: 20 }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
        }}
        columns={[
          {
            title: 'Ý tưởng',
            dataIndex: 'title',
            render: (title: string, row) => (
              <div>
                <Button type="link" style={{ padding: 0, height: 'auto' }} onClick={() => void openDetail(row.id)}>
                  {title}
                </Button>
                {row.sourceTitle && !sourceFilter ? (
                  <div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Từ ý tưởng:{' '}
                      <Button
                        type="link"
                        size="small"
                        style={{ padding: 0, height: 'auto', fontSize: 12 }}
                        onClick={() => setSourceFilter(row.sourcePackageId ?? undefined)}
                      >
                        {row.sourceTitle}
                      </Button>
                    </Typography.Text>
                  </div>
                ) : null}
              </div>
            ),
          },
          {
            title: 'Brand',
            dataIndex: 'brandName',
            width: 160,
            render: (name: string, row: ContentPackage) =>
              row.sourcePackageId ? (
                name
              ) : (
                <Typography.Text type="secondary">Sổ tay · {name}</Typography.Text>
              ),
          },
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
            title: 'Loại',
            dataIndex: 'sourcePackageId',
            width: 170,
            render: (v: string | null | undefined, row: ContentPackage) =>
              v ? (
                <Tag color="purple">Góc brand</Tag>
              ) : (
                <Tag color="blue">
                  Ý tưởng gốc
                  {(row.adaptationCount ?? 0) > 0 ? ` · ${row.adaptationCount} góc` : ''}
                </Tag>
              ),
          },
          {
            title: '',
            width: 380,
            render: (_: unknown, row: ContentPackage) => (
              <Space size={4} wrap>
                <Button
                  size="small"
                  type="primary"
                  icon={<ThunderboltOutlined />}
                  loading={busy}
                  disabled={!row.sourcePackageId}
                  onClick={() => confirmGenerate(row)}
                >
                  Generate
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
                <Button
                  size="small"
                  icon={<ClusterOutlined />}
                  disabled={!!row.sourcePackageId}
                  onClick={() => openMulti(row)}
                >
                  Adapt nhiều brand
                </Button>
                <Button size="small" icon={<CopyOutlined />} onClick={() => openAdapt(row)}>
                  1 brand
                </Button>
                <Button
                  size="small"
                  icon={<DownloadOutlined />}
                  disabled={row.variantCount === 0}
                  loading={busy}
                  onClick={() => void onExport(row)}
                >
                  Pack
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
          <Form.Item name="insight" label="Insight (core idea)">
            <Input.TextArea rows={2} placeholder="Người dùng coi trọng đồng hành sau giao dịch…" />
          </Form.Item>
          <Form.Item name="problem" label="Vấn đề">
            <Input.TextArea rows={2} placeholder="Doanh nghiệp chỉ tập trung bán hàng…" />
          </Form.Item>
          <Form.Item name="coreMessage" label="Core message">
            <Input.TextArea rows={2} placeholder="Giá trị lâu dài nằm ở mối quan hệ…" />
          </Form.Item>
          <Form.Item name="source" label="Nguồn">
            <Input placeholder="GPP / Local OS / bài đã xuất bản…" />
          </Form.Item>
          <Form.Item name="sourceUrl" label="URL nguồn">
            <Input placeholder="https://…" />
          </Form.Item>
          <Form.Item name="evidence" label="Evidence">
            <Input.TextArea rows={2} placeholder="Chỉ nêu số liệu đã có nguồn — không bịa" />
          </Form.Item>
          <Space>
            <Form.Item name="sourceType" label="Loại nguồn">
              <Select
                allowClear
                style={{ width: 140 }}
                options={[
                  { value: 'ops', label: 'Vận hành' },
                  { value: 'listing', label: 'Tin / listing' },
                  { value: 'article', label: 'Bài đã có' },
                  { value: 'opinion', label: 'Opinion' },
                ]}
              />
            </Form.Item>
            <Form.Item name="factOrOpinion" label="Fact / opinion">
              <Select
                allowClear
                style={{ width: 140 }}
                options={[
                  { value: 'fact', label: 'Fact (cần nguồn)' },
                  { value: 'opinion', label: 'Opinion' },
                ]}
              />
            </Form.Item>
          </Space>
          <Form.Item name="briefObjective" label="Brief — mục tiêu">
            <Select allowClear options={[...CONTENT_BRIEF_OBJECTIVES]} placeholder="Nhận biết / traffic / tin…" />
          </Form.Item>
          <Form.Item name="briefFormat" label="Brief — format">
            <Select allowClear options={[...CONTENT_BRIEF_FORMATS]} placeholder="Bài web / mini story…" />
          </Form.Item>
          <Form.Item name="briefEmotion" label="Brief — cảm xúc">
            <Select allowClear options={[...CONTENT_BRIEF_EMOTIONS]} />
          </Form.Item>
          <Form.Item name="briefVisual" label="Brief — hướng hình">
            <Input.TextArea rows={2} placeholder="Screen-first, hiện trường…" />
          </Form.Item>
          <Form.Item name="bodyOutline" label="Gợi ý / dàn ý">
            <Input.TextArea rows={3} placeholder="Các ý chính muốn nhấn…" />
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
          Nhân bản ý tưởng sang brand đích (Nháp). Chỉnh góc nhìn rồi mới Generate theo nơi đăng — không copy nguyên bài.
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

      <Modal
        title={multiSource ? `Adapt nhiều brand · ${multiSource.title}` : 'Adapt nhiều brand'}
        open={multiOpen}
        onCancel={() => setMultiOpen(false)}
        onOk={() => void onMultiAdapt()}
        confirmLoading={busy}
        okText="Chấm Fit + tạo góc"
        width={560}
      >
        <Typography.Paragraph type="secondary">
          AI đọc Brand Knowledge từng brand. Fit / có thể chuyển góc → tạo package riêng. Không phù
          hợp → bỏ. Không copy cùng một bài.
        </Typography.Paragraph>
        <Checkbox.Group
          style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 16 }}
          value={multiBrandIds}
          onChange={(v) => setMultiBrandIds(v.map(String))}
          options={brands
            .filter((b) => b.id !== multiSource?.brandId)
            .map((b) => ({ value: b.id, label: `${b.name} (${b.code})` }))}
        />
        <Space direction="vertical">
          <Checkbox checked={includeMaybe} onChange={(e) => setIncludeMaybe(e.target.checked)}>
            Tạo cả brand «có thể chuyển góc» (maybe)
          </Checkbox>
          <Checkbox checked={generateFits} onChange={(e) => setGenerateFits(e.target.checked)}>
            Generate theo nơi đăng của brand Fit (tốn AI)
          </Checkbox>
        </Space>
      </Modal>

      <Drawer
        title={
          detail
            ? detail.package.sourceTitle
              ? `${detail.package.brandName} · ${detail.package.sourceTitle}`
              : detail.package.title
            : 'Chi tiết package'
        }
        open={detailOpen}
        onClose={() => {
          setDetailOpen(false);
          revokeAssetUrls(assetUrls);
          setAssetUrls({});
        }}
        width="min(1120px, 92vw)"
        loading={detailLoading}
        extra={
          detail ? (
            <Space>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                loading={busy}
                disabled={!detail.package.sourcePackageId}
                onClick={() => confirmGenerate(detail.package)}
              >
                Generate
              </Button>
              <Button
                icon={<DownloadOutlined />}
                loading={busy}
                disabled={detail.topicDetail.variants.length === 0}
                onClick={() => void onExport(detail.package)}
              >
                Pack đăng
              </Button>
              <Button
                icon={<CheckOutlined />}
                loading={busy}
                disabled={
                  detail.topicDetail.variants.length === 0 ||
                  detail.package.status === 'Approved' ||
                  detail.package.qualityGate?.canApprove === false
                }
                onClick={() => void onApproveOne(detail.package)}
              >
                Duyệt
              </Button>
              <Button
                icon={<ClusterOutlined />}
                disabled={!!detail.package.sourcePackageId}
                onClick={() => openMulti(detail.package)}
              >
                Adapt nhiều brand
              </Button>
              <Button icon={<CopyOutlined />} onClick={() => openAdapt(detail.package)}>
                1 brand
              </Button>
            </Space>
          ) : null
        }
      >
        {detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            {detail.package.sourceTitle ? (
              <Alert
                type="info"
                showIcon
                message={`Thuộc ý tưởng: ${detail.package.sourceTitle}`}
                description={detail.package.angle || detail.package.title}
              />
            ) : null}

            <div>
              <StatusTag status={detail.package.status} />
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {detail.package.brandName}
                {detail.package.angle ? ` · ${detail.package.angle}` : ''}
              </Typography.Text>
              <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                Ảnh + xuất bản:{' '}
                <Link to={`/content/topics?topic=${detail.package.topicId}`}>mở đúng bài này</Link>
                {' '}(không mở ý tưởng gốc).
              </Typography.Paragraph>
            </div>

            {detail.package.qualityGate &&
            ((detail.package.qualityGate.blockingIssues?.length ?? 0) > 0 ||
              (detail.package.qualityGate.approveIssues?.length ?? 0) > 0 ||
              !detail.package.qualityGate.passed) ? (
              <Alert
                type={
                  (detail.package.qualityGate.blockingIssues?.length ?? 0) > 0 ? 'error' : 'warning'
                }
                showIcon
                message={
                  (detail.package.qualityGate.blockingIssues?.length ?? 0) > 0
                    ? 'Quality Gate — chặn đăng (bài mỏng / claim / thiếu angle)'
                    : 'Quality Gate — điền Brief hoặc sửa gợi ý trước khi duyệt'
                }
                description={
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {(detail.package.qualityGate.issues ?? []).map((i) => (
                      <li key={i}>{i}</li>
                    ))}
                  </ul>
                }
              />
            ) : null}

            <Card size="small" title="Creative Brief">
              <ContentPackageBriefCard
                key={`${detail.package.id}-${detail.package.updatedAt}`}
                pkg={detail.package}
                busy={busy}
                onSaved={() => void openDetail(detail.package.id)}
              />
            </Card>

            {detail.package.coreIdea &&
            (detail.package.coreIdea.insight ||
              detail.package.coreIdea.problem ||
              detail.package.coreIdea.coreMessage ||
              detail.package.coreIdea.source) ? (
              <Card size="small" title="Core idea">
                {detail.package.coreIdea.insight ? (
                  <Typography.Paragraph style={{ marginBottom: 8 }}>
                    <strong>Insight.</strong> {detail.package.coreIdea.insight}
                  </Typography.Paragraph>
                ) : null}
                {detail.package.coreIdea.problem ? (
                  <Typography.Paragraph style={{ marginBottom: 8 }}>
                    <strong>Vấn đề.</strong> {detail.package.coreIdea.problem}
                  </Typography.Paragraph>
                ) : null}
                {detail.package.coreIdea.coreMessage ? (
                  <Typography.Paragraph style={{ marginBottom: 8 }}>
                    <strong>Core message.</strong> {detail.package.coreIdea.coreMessage}
                  </Typography.Paragraph>
                ) : null}
                {detail.package.coreIdea.source || detail.package.coreIdea.sourceUrl ? (
                  <Typography.Paragraph style={{ marginBottom: 0 }}>
                    <strong>Nguồn.</strong> {detail.package.coreIdea.source}{' '}
                    {detail.package.coreIdea.sourceUrl}
                    {detail.package.coreIdea.factOrOpinion
                      ? ` · ${detail.package.coreIdea.factOrOpinion}`
                      : ''}
                  </Typography.Paragraph>
                ) : null}
              </Card>
            ) : null}

            <Card size="small" title="Số liệu (nhập tay)">
              <ContentPackagePerformanceCard packageId={detail.package.id} />
            </Card>

            {detail.package.brandFits && detail.package.brandFits.length > 0 ? (
              <Card size="small" title="Brand Fit">
                {detail.package.brandFits.map((f) => (
                  <div key={f.brandId} style={{ marginBottom: 10 }}>
                    <Space>
                      <Tag
                        color={
                          f.verdict === 'fit' ? 'green' : f.verdict === 'maybe' ? 'gold' : 'default'
                        }
                      >
                        {f.verdict === 'fit' ? 'Phù hợp' : f.verdict === 'maybe' ? 'Có thể' : 'Bỏ'}
                      </Tag>
                      <Typography.Text strong>{f.brandName}</Typography.Text>
                      <Typography.Text type="secondary">{f.score}/100</Typography.Text>
                    </Space>
                    {f.reason ? (
                      <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0' }}>
                        {f.reason}
                      </Typography.Paragraph>
                    ) : null}
                    {f.angle ? (
                      <Typography.Paragraph style={{ margin: '4px 0 0' }}>{f.angle}</Typography.Paragraph>
                    ) : null}
                  </div>
                ))}
              </Card>
            ) : null}

            {detail.adaptations && detail.adaptations.length > 0 ? (
              <Card size="small" title={`Góc brand (${detail.adaptations.length})`}>
                {detail.adaptations.map((a) => (
                  <div key={a.id} style={{ marginBottom: 8 }}>
                    <Button type="link" style={{ padding: 0 }} onClick={() => void openDetail(a.id)}>
                      {a.brandName}
                    </Button>
                    <Typography.Text type="secondary">
                      {' '}
                      · {a.angle || a.title} · {a.variantCount} bản
                    </Typography.Text>
                  </div>
                ))}
              </Card>
            ) : null}

            <Tabs
              items={[
                {
                  key: 'write',
                  label: `Bản viết (${detail.topicDetail.variants.length})`,
                  children:
                    detail.topicDetail.variants.length === 0 ? (
                      <Typography.Text type="secondary">Chưa có — bấm Generate và chọn kênh.</Typography.Text>
                    ) : (
                      detail.topicDetail.variants.map((v: ContentVariant) => (
                        <div
                          key={v.id}
                          style={{
                            marginBottom: 16,
                            paddingBottom: 16,
                            borderBottom: '1px solid #f1f5f9',
                          }}
                        >
                          <Space size={8} wrap style={{ marginBottom: 8 }}>
                            <Tag color="blue">{VARIANT_KIND_LABEL[v.kind] ?? v.kind}</Tag>
                            {v.title ? <Typography.Text strong>{v.title}</Typography.Text> : null}
                          </Space>
                          <Typography.Paragraph
                            style={{ marginBottom: 0, whiteSpace: 'pre-wrap', fontSize: 14, lineHeight: 1.65 }}
                          >
                            {v.bodyMarkdown}
                          </Typography.Paragraph>
                        </div>
                      ))
                    ),
                },
                {
                  key: 'manual',
                  label: 'Đăng tay',
                  children: (
                    <ContentManualPostTab
                      channels={channels}
                      sites={sites}
                      variants={detail.topicDetail.variants}
                      hasImage={
                        detail.topicDetail.assets.some((a) => a.isSelected) ||
                        detail.topicDetail.assets.length > 0
                      }
                      onCopyImage={async () => {
                        const selected =
                          detail.topicDetail.assets.find((a) => a.isSelected) ??
                          detail.topicDetail.assets[0];
                        if (!selected || !assetUrls[selected.id]) {
                          throw new Error('Chưa có ảnh trên góc này — mở Bài viết để chọn ảnh.');
                        }
                        const blob = await fetch(assetUrls[selected.id]).then((r) => r.blob());
                        await writeClipboardImage(blob);
                      }}
                      onCopied={(ok, detailMsg) => {
                        if (ok) message.success(detailMsg);
                        else message.warning(detailMsg);
                      }}
                    />
                  ),
                },
              ]}
            />

            <Card
              size="small"
              title={`Ảnh (${detail.topicDetail.assets.length})`}
              extra={
                <Link to={`/content/topics?topic=${detail.package.topicId}`}>
                  Mở đúng bài này trên Bài viết
                </Link>
              }
            >
              {detail.topicDetail.assets.length === 0 ? (
                <Typography.Text type="secondary">
                  Generate góc chỉ viết chữ. Ảnh chọn/tạo ở tab Bài viết — bấm «mở đúng bài này»
                  phía trên, không mở hàng ý tưởng gốc (Xuân Hòa).
                </Typography.Text>
              ) : (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                  {detail.topicDetail.assets.map((a: ContentAsset) => (
                    <div
                      key={a.id}
                      style={{
                        width: 168,
                        border: a.isSelected ? '2px solid #1677ff' : '1px solid #e2e8f0',
                        borderRadius: 8,
                        padding: 8,
                      }}
                    >
                      {assetUrls[a.id] ? (
                        <img
                          src={assetUrls[a.id]}
                          alt={a.fileName}
                          style={{ width: '100%', height: 110, objectFit: 'cover', borderRadius: 4 }}
                        />
                      ) : (
                        <div
                          style={{
                            height: 110,
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
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {a.isSelected ? 'Đang dùng' : a.fileName}
                      </Typography.Text>
                    </div>
                  ))}
                </div>
              )}
            </Card>
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
