import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Card,
  Collapse,
  Drawer,
  Form,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  Upload,
} from 'antd';
import {
  CheckOutlined,
  CloudUploadOutlined,
  DownloadOutlined,
  FileExcelOutlined,
  FolderOpenOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  UnorderedListOutlined,
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
import {
  clearLocalImageLibrary,
  getLocalImageLibraryName,
  isLocalImageLibrarySupported,
  listLocalImages,
  pickBestLocalImage,
  pickLocalImageLibrary,
  readLocalImageAsBase64,
} from '@/modules/content/content-local-image-library';
import {
  downloadCsvTemplate,
  parseOptionalDate,
  parseSpreadsheetFile,
  pickRowValue,
} from '@/shared/utils/spreadsheet-import';

const CONTENT_EXCEL_HEADERS = ['tieu_de', 'ngay_hien_thi', 'series', 'goi_y'];

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

function parseTitleLines(raw: string): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const line of raw.split(/\r?\n/)) {
    const t = line.replace(/^[-*•\d.)\s]+/, '').trim();
    if (!t || seen.has(t.toLowerCase())) continue;
    seen.add(t.toLowerCase());
    out.push(t);
  }
  return out;
}

export function ContentTopicsPage() {
  const { message, modal } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [open, setOpen] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [editing, setEditing] = useState<ContentTopic | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<ContentTopicDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [rowBusyId, setRowBusyId] = useState<string | null>(null);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});
  const [bulkBrandId, setBulkBrandId] = useState<string | undefined>();
  const [bulkTitles, setBulkTitles] = useState('');
  const [bulkSaving, setBulkSaving] = useState(false);
  const [excelImporting, setExcelImporting] = useState(false);
  const [localLibName, setLocalLibName] = useState<string | null>(null);
  const [localLibCount, setLocalLibCount] = useState(0);
  const [form] = Form.useForm();

  const refreshLocalLib = useCallback(async () => {
    if (!isLocalImageLibrarySupported()) {
      setLocalLibName(null);
      setLocalLibCount(0);
      return;
    }
    const name = await getLocalImageLibraryName();
    setLocalLibName(name);
    if (!name) {
      setLocalLibCount(0);
      return;
    }
    try {
      const imgs = await listLocalImages();
      setLocalLibCount(imgs.length);
    } catch {
      setLocalLibCount(0);
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [t, b] = await Promise.all([
        fetchContentTopics({ brandId: brandFilter }),
        fetchContentBrands(true),
      ]);
      setTopics(t);
      setBrands(b);
      void refreshLocalLib();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được danh sách bài'));
    } finally {
      setLoading(false);
    }
  }, [brandFilter, message, refreshLocalLib]);

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
      message.error(apiErrorMessage(e, 'Không tải được chi tiết bài'));
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

  const openBulk = () => {
    setBulkBrandId(brandFilter ?? brands[0]?.id);
    setBulkTitles('');
    setBulkOpen(true);
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

  const save = async (andGenerate: boolean) => {
    try {
      const v = await form.validateFields();
      setBusy(true);
      if (editing) {
        await updateContentTopic(editing.id, v);
        message.success('Đã cập nhật bài');
        setOpen(false);
        await load();
        return;
      }

      const created = await createContentTopic({
        brandId: v.brandId,
        title: v.title,
        pillar: v.pillar,
        goal: v.goal ?? 'traffic',
        ctaUrl: v.ctaUrl,
        utmCampaign: v.utmCampaign,
        priority: v.priority ?? 'P1',
        status: 'Draft',
        bodyOutline: v.bodyOutline,
      });
      setOpen(false);
      message.success(andGenerate ? 'Đã tạo — đang nhờ AI viết…' : 'Đã thêm vào hàng đợi');
      await load();

      if (andGenerate) {
        setDetailOpen(true);
        await loadDetail(created.id);
        const res = await generateContentTopic(created.id, { skipImages: false });
        if (res.budgetBlocked) message.warning(res.message ?? 'Hết ngân sách AI');
        else message.success(res.message ?? 'AI đã viết xong');
        await loadDetail(created.id);
        await load();
      }
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được bài'));
    } finally {
      setBusy(false);
    }
  };

  const saveBulk = async () => {
    const brandId = bulkBrandId;
    if (!brandId) {
      message.warning('Chọn thương hiệu');
      return;
    }
    const titles = parseTitleLines(bulkTitles);
    if (titles.length === 0) {
      message.warning('Dán ít nhất 1 tiêu đề (mỗi dòng một ý)');
      return;
    }
    setBulkSaving(true);
    try {
      let ok = 0;
      for (const title of titles) {
        await createContentTopic({
          brandId,
          title,
          goal: 'traffic',
          priority: 'P1',
          status: 'Draft',
        });
        ok += 1;
      }
      message.success(`Đã thêm ${ok} ý vào hàng đợi`);
      setBulkOpen(false);
      setBulkTitles('');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tạo hàng loạt được'));
    } finally {
      setBulkSaving(false);
    }
  };

  const importExcel = async (file: File) => {
    const brandId = brandFilter ?? brands[0]?.id;
    if (!brandId) {
      message.warning('Chọn thương hiệu (bộ lọc hoặc tạo brand trước)');
      return false;
    }
    setExcelImporting(true);
    try {
      const rows = await parseSpreadsheetFile(file);
      if (rows.length === 0) {
        message.warning('File trống hoặc thiếu dòng dữ liệu');
        return false;
      }
      let ok = 0;
      let skipped = 0;
      for (const row of rows) {
        const title = pickRowValue(row, 'tieu_de', 'title', 'tieu de', 'chu_de');
        if (!title) {
          skipped += 1;
          continue;
        }
        const dateRaw = pickRowValue(
          row,
          'ngay_hien_thi',
          'ngay',
          'display_at',
          'display_date',
          'pub_date',
          'ngay dang',
        );
        const isoDate = parseOptionalDate(dateRaw);
        const displayAt = isoDate ? `${isoDate}T00:00:00.000Z` : null;
        const pillar = pickRowValue(row, 'series', 'pillar', 'chu_de_series') || undefined;
        const outline = pickRowValue(row, 'goi_y', 'outline', 'brief') || undefined;
        await createContentTopic({
          brandId,
          title,
          pillar,
          bodyOutline: outline,
          displayAt,
          goal: 'traffic',
          priority: 'P1',
          status: 'Draft',
        });
        ok += 1;
      }
      message.success(
        skipped > 0
          ? `Đã nhập ${ok} dòng từ Excel (${skipped} dòng bỏ qua vì thiếu tiêu đề)`
          : `Đã nhập ${ok} dòng từ Excel`,
      );
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không đọc được Excel'));
    } finally {
      setExcelImporting(false);
    }
    return false;
  };

  const onGenerateRow = async (row: ContentTopic) => {
    setRowBusyId(row.id);
    try {
      const res = await generateContentTopic(row.id, { skipImages: !!localLibName });
      if (res.budgetBlocked) {
        message.warning(res.message ?? 'Hết ngân sách AI');
      } else {
        message.success(
          `AI xong: ${row.title}` + (localLibName ? ' · Ảnh lấy từ kho máy khi Xuất bản' : ''),
        );
      }
      await load();
      setDetailOpen(true);
      await loadDetail(row.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'AI tạo bài thất bại'));
    } finally {
      setRowBusyId(null);
    }
  };

  const confirmGenerateRow = (row: ContentTopic) => {
    modal.confirm({
      title: 'Nhờ AI viết bài này?',
      content: row.title,
      okText: 'Viết ngay',
      cancelText: 'Huỷ',
      onOk: () => onGenerateRow(row),
    });
  };

  const onGenerate = async (skipImages = false) => {
    if (!detail) return;
    setBusy(true);
    try {
      // Prefer local library → don't burn Gemini/Pollinations for images.
      const skip = skipImages || !!localLibName;
      const res = await generateContentTopic(detail.topic.id, { skipImages: skip });
      if (res.budgetBlocked) {
        message.warning(res.message ?? 'Đã chặn vì hết ngân sách AI');
      } else if (res.message?.includes('ảnh lỗi') || res.message?.includes('Không tạo được ảnh')) {
        message.warning(res.message);
      } else {
        message.success(
          (res.message ?? 'AI đã viết xong') +
            (localLibName ? ' · Ảnh sẽ lấy từ kho máy khi Xuất bản.' : ''),
        );
      }
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'AI tạo bài thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onGenerateImages = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      const res = await generateContentTopic(detail.topic.id, { imagesOnly: true });
      if (res.budgetBlocked) {
        message.warning(res.message ?? 'Đã chặn vì hết ngân sách AI');
      } else if ((detail && res.assets.length === 0) || res.message?.includes('Không tạo')) {
        message.warning(res.message ?? 'Không tạo được ảnh');
      } else {
        message.success(res.message ?? 'Đã tạo ảnh');
      }
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo ảnh thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onSelectAsset = async (asset: ContentAsset) => {
    if (!detail) return;
    setBusy(true);
    try {
      await selectContentAsset(detail.topic.id, asset.id);
      message.success('Đã chọn ảnh này');
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
      message.success('Đã duyệt — có thể xuất bản');
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onPickLocalLibrary = async () => {
    try {
      const r = await pickLocalImageLibrary();
      setLocalLibName(r.name);
      setLocalLibCount(r.count);
      message.success(`Đã chọn kho ảnh «${r.name}» (${r.count} ảnh) — chỉ trên máy bạn, không upload lên server.`);
    } catch (e) {
      if (e && typeof e === 'object' && 'name' in e && (e as { name: string }).name === 'AbortError') return;
      message.error(apiErrorMessage(e, 'Không chọn được thư mục ảnh'));
    }
  };

  const onClearLocalLibrary = async () => {
    await clearLocalImageLibrary();
    setLocalLibName(null);
    setLocalLibCount(0);
    message.info('Đã bỏ liên kết kho ảnh local');
  };

  const onPublish = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      // Auto-approve so WP/FB path can run from Review/Draft-with-variants.
      if (detail.topic.status === 'Review' || detail.topic.status === 'Draft') {
        try {
          await approveContentTopic(detail.topic.id);
        } catch {
          /* may already be approved / allowed by API */
        }
      }

      const publishAt = detail.topic.displayAt ?? undefined;
      const whenLabel = publishAt
        ? new Date(publishAt).toLocaleString('vi-VN')
        : 'ngay lập tức (không có ngày hiển thị)';

      let pickedFileName: string | undefined;
      let localImages: Awaited<ReturnType<typeof listLocalImages>> = [];

      if (localLibName) {
        localImages = await listLocalImages();
        if (localImages.length === 0) {
          message.warning('Kho ảnh trống — chọn thư mục có file .png/.jpg/.webp');
          return;
        }
        const suggested = pickBestLocalImage(detail.topic.title, localImages);
        pickedFileName = suggested?.name ?? localImages[0]!.name;
      }

      const okConfirm = await new Promise<boolean>((resolve) => {
        modal.confirm({
          title: 'Đẩy bài lên web / Facebook?',
          width: 480,
          content: (
            <div>
              {localLibName && localImages.length > 0 ? (
                <div style={{ marginBottom: 12 }}>
                  <Typography.Text>Ảnh từ kho máy «{localLibName}»:</Typography.Text>
                  <Select
                    style={{ width: '100%', marginTop: 6 }}
                    defaultValue={pickedFileName}
                    options={localImages.map((img) => ({ value: img.name, label: img.name }))}
                    showSearch
                    optionFilterProp="label"
                    onChange={(v) => {
                      pickedFileName = v;
                    }}
                  />
                </div>
              ) : (
                <p>Ảnh: dùng ảnh đã chọn trên server (nếu có), hoặc đăng không kèm ảnh.</p>
              )}
              <p>
                Lịch đăng: <strong>{whenLabel}</strong> — WP/FB tự lên vào giờ đó, không cần mở lại KitPlatform.
              </p>
              <p style={{ color: '#64748b', marginBottom: 0 }}>
                Ảnh chỉ gửi một lần lên WP/FB — không lưu kho lâu trên server KitPlatform.
              </p>
            </div>
          ),
          okText: 'Đẩy lịch đăng',
          cancelText: 'Huỷ',
          onOk: () => resolve(true),
          onCancel: () => resolve(false),
        });
      });
      if (!okConfirm) return;

      let imageBase64: string | undefined;
      let imageFileName: string | undefined;
      let imageContentType: string | undefined;
      if (localLibName && pickedFileName) {
        const entry = localImages.find((i) => i.name === pickedFileName) ?? localImages[0];
        if (!entry) {
          message.warning('Không đọc được ảnh đã chọn');
          return;
        }
        const file = await readLocalImageAsBase64(entry);
        imageBase64 = file.base64;
        imageFileName = file.fileName;
        imageContentType = file.contentType;
      }

      const res = await publishContentTopic(detail.topic.id, {
        includeManualExport: true,
        runImmediately: true,
        publishAt: publishAt ?? null,
        imageBase64,
        imageFileName,
        imageContentType,
      });
      const failed = res.jobs.filter((j) => j.status === 'Failed').length;
      const succeeded = res.jobs.filter((j) => j.status === 'Succeeded').length;
      if (failed > 0) message.warning(`Xuất bản: ${succeeded} thành công, ${failed} lỗi`);
      else
        message.success(
          `Đã đẩy lịch đăng (${succeeded} kênh)` +
            (imageFileName ? ` · ảnh ${imageFileName}` : ''),
        );
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Xuất bản thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onRetryJob = async (job: ContentPublishJob) => {
    setBusy(true);
    try {
      await runContentPublishJob(job.id);
      message.success('Đã chạy lại');
      if (detail) await loadDetail(detail.topic.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chạy lại thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const bulkPreviewCount = parseTitleLines(bulkTitles).length;

  return (
    <div>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          marginBottom: 16,
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ maxWidth: 560 }}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Hàng đợi bài
          </Typography.Title>
          <Typography.Text type="secondary">
            Nhanh hơn CMS từng site: <strong>Excel / dán tiêu đề</strong> → <strong>Nhờ AI</strong> (chỉ chữ) → chọn
            ảnh từ <strong>kho thư mục máy bạn</strong> → xuất bản kèm ngày → WP/FB tự đăng, không giữ ảnh trên
            server KitPlatform.
            {brands.length === 0 ? ' Hãy thêm thương hiệu trước.' : null}
          </Typography.Text>
        </div>
        <Space wrap>
          {isLocalImageLibrarySupported() ? (
            localLibName ? (
              <Space.Compact>
                <Button icon={<FolderOpenOutlined />} onClick={() => void onPickLocalLibrary()}>
                  Kho ảnh: {localLibName} ({localLibCount})
                </Button>
                <Button onClick={() => void onClearLocalLibrary()}>Bỏ</Button>
              </Space.Compact>
            ) : (
              <Button type="dashed" icon={<FolderOpenOutlined />} onClick={() => void onPickLocalLibrary()}>
                Chọn kho ảnh trên máy
              </Button>
            )
          ) : (
            <Typography.Text type="warning" style={{ fontSize: 12 }}>
              Kho ảnh local cần Chrome/Edge
            </Typography.Text>
          )}
          <Select
            allowClear
            placeholder="Lọc / chọn thương hiệu"
            style={{ width: 180 }}
            value={brandFilter}
            onChange={setBrandFilter}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button
            icon={<DownloadOutlined />}
            onClick={() => downloadCsvTemplate('mau-hang-doi-bai.csv', CONTENT_EXCEL_HEADERS)}
          >
            Mẫu Excel/CSV
          </Button>
          <Upload
            accept=".xlsx,.xls,.csv"
            showUploadList={false}
            beforeUpload={(file) => {
              void importExcel(file);
              return false;
            }}
            disabled={brands.length === 0 || excelImporting}
          >
            <Button type="primary" icon={<FileExcelOutlined />} loading={excelImporting} disabled={brands.length === 0}>
              Nhập Excel
            </Button>
          </Upload>
          <Button icon={<PlusOutlined />} onClick={openCreate} disabled={brands.length === 0}>
            Thêm 1 bài
          </Button>
          <Button icon={<UnorderedListOutlined />} onClick={openBulk} disabled={brands.length === 0}>
            Dán nhiều tiêu đề
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={topics}
        locale={{
          emptyText:
            brands.length === 0
              ? 'Chưa có thương hiệu — vào tab Thương hiệu trước'
              : 'Chưa có bài — bấm «Nhập Excel» hoặc «Dán nhiều tiêu đề»',
        }}
        columns={[
          { title: 'Thương hiệu', dataIndex: 'brandName', width: 120 },
          { title: 'Tiêu đề', dataIndex: 'title' },
          {
            title: 'Ngày hiển thị',
            dataIndex: 'displayAt',
            width: 120,
            render: (v?: string | null) => (v ? String(v).slice(0, 10) : '—'),
          },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            width: 120,
            render: (s: string) => <StatusTag status={s} />,
          },
          {
            title: 'Việc cần làm',
            key: 'a',
            width: 280,
            render: (_, row) => (
              <Space wrap size={0}>
                {(row.status === 'Draft' ||
                  row.status === 'BudgetBlocked' ||
                  row.status === 'Rejected') && (
                  <Button
                    type="link"
                    icon={<ThunderboltOutlined />}
                    loading={rowBusyId === row.id}
                    onClick={() => confirmGenerateRow(row)}
                  >
                    Nhờ AI
                  </Button>
                )}
                <Button type="link" onClick={() => openDetail(row)}>
                  Xem / duyệt
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
        title="Dán nhiều tiêu đề"
        open={bulkOpen}
        onClose={() => setBulkOpen(false)}
        width={520}
        extra={
          <Button type="primary" loading={bulkSaving} onClick={() => void saveBulk()}>
            Thêm {bulkPreviewCount > 0 ? `${bulkPreviewCount} ý` : 'vào hàng đợi'}
          </Button>
        }
      >
        <Typography.Paragraph type="secondary">
          Mỗi dòng một tiêu đề → tạo bài <strong>Nháp</strong>. Sau đó ở bảng bấm «Nhờ AI».
        </Typography.Paragraph>
        <Form layout="vertical">
          <Form.Item label="Thương hiệu" required>
            <Select
              value={bulkBrandId}
              onChange={setBulkBrandId}
              options={brands.map((b) => ({ value: b.id, label: b.name }))}
              placeholder="Chọn thương hiệu"
            />
          </Form.Item>
          <Form.Item label="Danh sách tiêu đề" required>
            <Input.TextArea
              value={bulkTitles}
              onChange={(e) => setBulkTitles(e.target.value)}
              rows={14}
              placeholder={[
                '5 việc chủ nhà thuốc nên làm mỗi sáng',
                'Khi nào nên nhập hàng tuần này?',
                'Vì sao kiểm kê cuối tháng là chưa đủ',
              ].join('\n')}
            />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={editing ? 'Sửa bài' : 'Thêm 1 bài'}
        open={open}
        onClose={() => setOpen(false)}
        width={480}
        extra={
          editing ? (
            <Button type="primary" loading={busy} onClick={() => void save(false)}>
              Lưu
            </Button>
          ) : (
            <Space>
              <Button loading={busy} onClick={() => void save(false)}>
                Chỉ thêm nháp
              </Button>
              <Button type="primary" loading={busy} onClick={() => void save(true)}>
                Thêm & nhờ AI viết
              </Button>
            </Space>
          )
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="brandId" label="Thương hiệu" rules={[{ required: true }]}>
            <Select options={brands.map((b) => ({ value: b.id, label: b.name }))} />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true }]}>
            <Input placeholder="Ví dụ: 5 việc chủ nhà thuốc nên làm mỗi sáng" />
          </Form.Item>

          {!editing ? (
            <>
              <Form.Item name="bodyOutline" label="Gợi ý cho AI (tuỳ chọn)">
                <Input.TextArea rows={3} placeholder="Để trống cũng được — AI tự viết theo tiêu đề." />
              </Form.Item>
              <Collapse
                ghost
                items={[
                  {
                    key: 'more',
                    label: 'Tuỳ chọn thêm (không bắt buộc)',
                    children: (
                      <>
                        <Form.Item name="pillar" label="Chủ đề / series">
                          <Input placeholder="tồn kho / khách hàng / …" />
                        </Form.Item>
                        <Form.Item name="goal" label="Mục tiêu bài" initialValue="traffic">
                          <Select
                            options={[
                              { value: 'traffic', label: 'Thu hút đọc web' },
                              { value: 'seo', label: 'SEO / tìm kiếm' },
                              { value: 'lead', label: 'Lấy lead' },
                              { value: 'phc', label: 'Đẩy Health Check / PHC' },
                              { value: 'other', label: 'Khác' },
                            ]}
                          />
                        </Form.Item>
                        <Form.Item name="ctaUrl" label="Link CTA">
                          <Input />
                        </Form.Item>
                        <Form.Item name="priority" label="Ưu tiên" initialValue="P1">
                          <Select
                            options={[
                              { value: 'P0', label: 'P0 — gấp' },
                              { value: 'P1', label: 'P1 — bình thường' },
                              { value: 'P2', label: 'P2 — chờ' },
                            ]}
                          />
                        </Form.Item>
                      </>
                    ),
                  },
                ]}
              />
            </>
          ) : (
            <>
              <Form.Item name="pillar" label="Chủ đề / series">
                <Input />
              </Form.Item>
              <Form.Item name="bodyOutline" label="Gợi ý viết cho AI">
                <Input.TextArea rows={4} />
              </Form.Item>
              <Form.Item name="goal" label="Mục tiêu bài">
                <Select
                  options={[
                    { value: 'traffic', label: 'Thu hút đọc web' },
                    { value: 'seo', label: 'SEO / tìm kiếm' },
                    { value: 'lead', label: 'Lấy lead' },
                    { value: 'phc', label: 'Đẩy Health Check / PHC' },
                    { value: 'other', label: 'Khác' },
                  ]}
                />
              </Form.Item>
              <Form.Item name="ctaUrl" label="Link CTA">
                <Input />
              </Form.Item>
              <Form.Item name="utmCampaign" label="UTM campaign">
                <Input />
              </Form.Item>
              <Form.Item name="priority" label="Ưu tiên">
                <Select
                  options={[
                    { value: 'P0', label: 'P0 — gấp' },
                    { value: 'P1', label: 'P1 — bình thường' },
                    { value: 'P2', label: 'P2 — chờ' },
                  ]}
                />
              </Form.Item>
              <Form.Item name="status" label="Trạng thái">
                <Select
                  options={Object.entries(STATUS_LABEL).map(([value, { text }]) => ({
                    value,
                    label: text,
                  }))}
                />
              </Form.Item>
            </>
          )}
        </Form>
      </Drawer>

      <Drawer
        title={detail ? detail.topic.title : 'Chi tiết bài'}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        width={720}
        loading={detailLoading}
        extra={
          detail ? (
            <Space wrap>
              <Button
                type="primary"
                icon={<ThunderboltOutlined />}
                loading={busy}
                onClick={() => void onGenerate(false)}
              >
                Nhờ AI viết
              </Button>
              <Button loading={busy} onClick={() => void onGenerate(true)}>
                Chỉ viết chữ
              </Button>
              {!localLibName ? (
                <Button loading={busy} onClick={() => void onGenerateImages()}>
                  Tạo ảnh
                </Button>
              ) : null}
              <Button icon={<CheckOutlined />} loading={busy} onClick={() => void onApprove()}>
                Duyệt
              </Button>
              <Button icon={<CloudUploadOutlined />} loading={busy} onClick={() => void onPublish()}>
                {localLibName ? 'Đẩy lịch đăng' : 'Xuất bản'}
              </Button>
            </Space>
          ) : null
        }
      >
        {detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <div>
              <StatusTag status={detail.topic.status} />
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {detail.topic.brandName}
              </Typography.Text>
              <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                {localLibName
                  ? <>AI viết chữ → <strong>Đẩy lịch đăng</strong> (chọn ảnh từ kho máy + ngày hiển thị → WP/FB tự đăng).</>
                  : <>Chọn ảnh → <strong>Duyệt</strong> → <strong>Xuất bản</strong>.</>}
              </Typography.Paragraph>
            </div>

            <Card size="small" title={`Bản viết (${detail.variants.length})`}>
              {detail.variants.length === 0 ? (
                <Typography.Text type="secondary">Chưa có — bấm «Nhờ AI viết».</Typography.Text>
              ) : (
                detail.variants.map((v: ContentVariant) => (
                  <div key={v.id} style={{ marginBottom: 12 }}>
                    <Typography.Text strong>
                      {v.kind}
                      {v.title ? ` — ${v.title}` : ''}
                    </Typography.Text>
                    <Typography.Paragraph
                      ellipsis={{ rows: 4, expandable: true, symbol: 'xem thêm' }}
                      style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}
                    >
                      {v.bodyMarkdown}
                    </Typography.Paragraph>
                  </div>
                ))
              )}
            </Card>

            <Card
              size="small"
              title={
                localLibName
                  ? `Ảnh AI (tuỳ chọn — đang dùng kho máy «${localLibName}»)`
                  : `Ảnh để chọn (${detail.assets.length})`
              }
            >
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
                    <Button
                      size="small"
                      block
                      type={a.isSelected ? 'primary' : 'default'}
                      style={{ marginTop: 6 }}
                      loading={busy}
                      onClick={() => void onSelectAsset(a)}
                    >
                      {a.isSelected ? 'Đang dùng' : 'Chọn ảnh này'}
                    </Button>
                  </div>
                ))}
                {detail.assets.length === 0 ? (
                  <Space direction="vertical" size={8}>
                    <Typography.Text type="secondary">
                      {localLibName
                        ? 'Không cần ảnh AI — khi Đẩy lịch đăng sẽ chọn ảnh từ kho máy.'
                        : 'Chưa có ảnh — lần gen trước có thể chỉ viết chữ hoặc model ảnh lỗi.'}
                    </Typography.Text>
                    {!localLibName ? (
                      <Button type="primary" loading={busy} onClick={() => void onGenerateImages()}>
                        Tạo ảnh ngay
                      </Button>
                    ) : null}
                  </Space>
                ) : null}
              </div>
            </Card>

            <Card size="small" title={`Xuất bản (${detail.jobs.length})`}>
              <Table
                size="small"
                rowKey="id"
                pagination={false}
                dataSource={detail.jobs}
                locale={{ emptyText: 'Chưa xuất bản' }}
                columns={[
                  { title: 'Kênh', dataIndex: 'connectorType', width: 140 },
                  {
                    title: 'Kết quả',
                    dataIndex: 'status',
                    width: 110,
                    render: (s: string) => (
                      <Tag color={s === 'Failed' ? 'red' : s === 'Succeeded' ? 'green' : 'default'}>
                        {s === 'Succeeded'
                          ? 'Thành công'
                          : s === 'Failed'
                            ? 'Lỗi'
                            : s === 'Queued'
                              ? 'Chờ'
                              : s}
                      </Tag>
                    ),
                  },
                  {
                    title: 'Chi tiết',
                    key: 'info',
                    render: (_, j: ContentPublishJob) => j.lastError || j.externalRef || '—',
                  },
                  {
                    title: '',
                    width: 90,
                    render: (_, j) =>
                      j.status === 'Failed' || j.status === 'Queued' ? (
                        <Button
                          type="link"
                          size="small"
                          loading={busy}
                          onClick={() => void onRetryJob(j)}
                        >
                          Chạy lại
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
