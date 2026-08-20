import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Card,
  Collapse,
  Drawer,
  Form,
  Input,
  Popconfirm,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
  Upload,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckOutlined,
  CloudUploadOutlined,
  DeleteOutlined,
  DownloadOutlined,
  FacebookOutlined,
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
  deleteContentTopic,
  fetchContentAssetObjectUrl,
  fetchContentBrands,
  fetchContentChannels,
  fetchContentSites,
  fetchContentTopicDetail,
  fetchContentTopics,
  generateContentTopic,
  publishContentTopic,
  runContentPublishJob,
  selectContentAsset,
  startFacebookOAuth,
  updateContentTopic,
  type ContentAsset,
  type ContentBrand,
  type ContentChannelTarget,
  type ContentPublishJob,
  type ContentSiteTarget,
  type ContentTopic,
  type ContentTopicDetail,
  type ContentVariant,
} from '@/shared/api/content.api';
import { FB_RETURN_KEY } from '@/modules/content/ContentFacebookCallbackPage';
import { ContentManualPostTab } from '@/modules/content/ContentManualPostTab';
import { writeClipboardImage } from '@/modules/content/content-manual-dest';
import {
  getLocalImageLibraryStatus,
  isConfidentLocalMatch,
  isLocalImageLibrarySupported,
  listLocalImages,
  loadLocalImagePreviews,
  pickBestLocalImage,
  prepareLocalImageForPublish,
  rankLocalImages,
  requestLocalImageLibraryPermission,
  revokeLocalPreviewUrls,
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

const CONNECTOR_LABEL: Record<string, { title: string; color: string }> = {
  facebook_page: { title: 'Facebook Page', color: 'blue' },
  astro_git: { title: 'Website · Astro / Git', color: 'purple' },
  wordpress_rest: { title: 'WordPress', color: 'geekblue' },
  manual: { title: 'Thủ công', color: 'default' },
  linkedin: { title: 'LinkedIn', color: 'cyan' },
  zalo_oa: { title: 'Zalo OA', color: 'green' },
};

function connectorLabel(type: string) {
  return CONNECTOR_LABEL[type] ?? { title: type, color: 'default' };
}

/** Human-readable publish error; keep raw text for expand. */
function formatPublishError(raw: string): { summary: string; hint?: string; raw: string } {
  const text = raw.trim();
  const lower = text.toLowerCase();
  if (
    lower.includes('resource not accessible by personal access token') ||
    (lower.includes('github 403') && lower.includes('personal access token'))
  ) {
    return {
      summary: 'GitHub từ chối token (403) — không đủ quyền ghi repo.',
      hint: 'Tạo PAT classic mới với quyền repo (full control of private repositories), dán lại ở Thương hiệu → Nơi đăng Astro, rồi «Đăng lại + ảnh». Fine-grained token cần Contents: Read and write trên đúng repo.',
      raw: text,
    };
  }
  if (lower.includes('github 401') || lower.includes('bad credentials')) {
    return {
      summary: 'GitHub token sai hoặc đã hết hạn.',
      hint: 'Dán lại PAT ở Thương hiệu → Nơi đăng Astro / Git.',
      raw: text,
    };
  }
  if (lower.includes('github 404') || lower.includes('not found')) {
    const repoMatch = text.match(/repo\s+([A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+)/i);
    const repoHint = repoMatch?.[1] ? ` (đang gọi ${repoMatch[1]})` : '';
    return {
      summary: `Không truy cập được repo trên GitHub${repoHint}.`,
      hint: 'Mở Thương hiệu → Nơi đăng Astro và kiểm tra đúng: owner = khiemtic-rgb, repo = Kit-Technology, thư mục = src/content/insights. Nếu dùng fine-grained token: phải chọn đúng repo Kit-Technology + Contents Read and write (không chọn Account). Token chỉ gắn KitPlatform sẽ báo 404 với Kit-Technology.',
      raw: text,
    };
  }
  // Prefer GitHub / WordPress message field when present
  const msgMatch = text.match(/"message"\s*:\s*"((?:\\.|[^"\\])*)"/);
  if (msgMatch?.[1]) {
    let decoded = msgMatch[1]
      .replace(/\\u([0-9a-fA-F]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)))
      .replace(/\\"/g, '"')
      .replace(/\\\\/g, '\\');
    if (lower.includes('wordpress') && (lower.includes('401') || lower.includes('rest_cannot_create'))) {
      return {
        summary: decoded,
        hint: 'Tài khoản WordPress thiếu quyền đăng bài / upload ảnh. Dùng user Editor hoặc Administrator, tạo Application Password mới, nhập đúng username (login, không phải tên hiển thị) → Lưu → Đăng lại + ảnh.',
        raw: text,
      };
    }
    if (lower.includes('wordpress') && lower.includes('403')) {
      return {
        summary: decoded,
        hint: 'WordPress từ chối quyền. Kiểm tra role user và plugin bảo mật có chặn REST / Application Passwords không.',
        raw: text,
      };
    }
    return { summary: decoded, raw: text };
  }
  if (
    lower.includes('mất quyền đăng') ||
    lower.includes('kết nối lại facebook') ||
    lower.includes('pages_manage_posts')
  ) {
    return {
      summary: text.length > 160 ? `${text.slice(0, 160)}…` : text,
      hint: 'Bấm «Kết nối lại» ngay cột Thao tác — đăng nhập Facebook, chọn Page, rồi «Đăng lại + ảnh».',
      raw: text,
    };
  }
  const short = text.length > 160 ? `${text.slice(0, 160)}…` : text;
  return { summary: short, raw: text };
}

function isFacebookReconnectJob(job: ContentPublishJob) {
  if (job.connectorType !== 'facebook_page' || job.status !== 'Failed') return false;
  const err = (job.lastError ?? '').toLowerCase();
  return (
    err.includes('kết nối lại') ||
    err.includes('mất quyền') ||
    err.includes('pages_manage_posts') ||
    err.includes('oauth') ||
    err.includes('"code":190') ||
    err.includes('(#190)') ||
    err.includes('expired')
  );
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
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [coreFilter, setCoreFilter] = useState<string | undefined>();
  const [open, setOpen] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [editing, setEditing] = useState<ContentTopic | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<ContentTopicDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailAction, setDetailAction] = useState<
    'write' | 'writeText' | 'images' | 'approve' | 'publish' | 'pickImage' | null
  >(null);
  const [detailTab, setDetailTab] = useState('write');
  const detailIdRef = useRef<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [rowBusyId, setRowBusyId] = useState<string | null>(null);
  const [assetUrls, setAssetUrls] = useState<Record<string, string>>({});
  const [bulkBrandId, setBulkBrandId] = useState<string | undefined>();
  const [bulkTitles, setBulkTitles] = useState('');
  const [bulkSaving, setBulkSaving] = useState(false);
  const [excelImporting, setExcelImporting] = useState(false);
  const [localLibName, setLocalLibName] = useState<string | null>(null);
  const [localLibCount, setLocalLibCount] = useState(0);
  /** After reload Chrome may remember the folder but need a click to re-allow read. */
  const [localLibNeedsPermission, setLocalLibNeedsPermission] = useState(false);
  const [localPreviews, setLocalPreviews] = useState<
    { name: string; url: string; confidence: number; reason: string }[]
  >([]);
  const [localPreviewLoading, setLocalPreviewLoading] = useState(false);
  const [pickedLocalName, setPickedLocalName] = useState<string | null>(null);
  const [localMatchConfident, setLocalMatchConfident] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [batchApproving, setBatchApproving] = useState(false);
  const [batchPublishing, setBatchPublishing] = useState(false);
  const [form] = Form.useForm();
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [sites, setSites] = useState<ContentSiteTarget[]>([]);

  /** Folder is per-brand — prefer topic brand, else list filter. */
  const libBrandId = detail?.topic.brandId ?? brandFilter ?? undefined;

  const canBulkApprove = (status: string) =>
    status === 'Review' || status === 'Draft' || status === 'Rejected';

  const canBulkPublish = (status: string) =>
    status === 'Approved' || status === 'Review' || status === 'Draft';

  const canSelectRow = (status: string) => canBulkApprove(status) || canBulkPublish(status);

  const topicCoreKey = (row: ContentTopic) => row.corePackageId || row.id;
  const topicCoreLabel = (row: ContentTopic) => row.coreTitle || row.title;

  const coreOptions = [...topics]
    .reduce<Array<{ value: string; label: string }>>((acc, row) => {
      const value = topicCoreKey(row);
      if (!acc.some((o) => o.value === value)) acc.push({ value, label: topicCoreLabel(row) });
      return acc;
    }, [])
    .sort((a, b) => a.label.localeCompare(b.label, 'vi'));

  const visibleTopics = coreFilter
    ? topics.filter((row) => topicCoreKey(row) === coreFilter)
    : topics;

  const refreshLocalLib = useCallback(async () => {
    if (!isLocalImageLibrarySupported() || !libBrandId) {
      setLocalLibName(null);
      setLocalLibCount(0);
      setLocalLibNeedsPermission(false);
      return;
    }
    const status = await getLocalImageLibraryStatus(libBrandId);
    setLocalLibName(status.name);
    if (!status.hasHandle) {
      setLocalLibCount(0);
      setLocalLibNeedsPermission(false);
      return;
    }
    if (status.permission !== 'granted') {
      setLocalLibCount(0);
      setLocalLibNeedsPermission(true);
      return;
    }
    setLocalLibNeedsPermission(false);
    try {
      const imgs = await listLocalImages(libBrandId);
      setLocalLibCount(imgs.length);
    } catch {
      setLocalLibCount(0);
    }
  }, [libBrandId]);

  const loadLocalGallery = useCallback(async (
    brandId: string,
    topicTitle?: string,
    opts?: { requestPermission?: boolean },
  ) => {
    if (!brandId) {
      setLocalLibName(null);
      setLocalLibCount(0);
      setLocalLibNeedsPermission(false);
      setLocalPreviews((prev) => {
        revokeLocalPreviewUrls(prev);
        return [];
      });
      return;
    }
    setLocalPreviewLoading(true);
    try {
      if (opts?.requestPermission) {
        const ok = await requestLocalImageLibraryPermission(brandId);
        if (!ok) {
          setLocalLibNeedsPermission(true);
          setLocalLibCount(0);
          message.warning('Trình duyệt chưa cho phép đọc thư mục ảnh. Bấm «Cho phép đọc lại» hoặc chọn lại thư mục.');
          return;
        }
        setLocalLibNeedsPermission(false);
      }
      const imgs = await listLocalImages(brandId, { requestPermission: opts?.requestPermission });
      setLocalLibCount(imgs.length);
      setLocalPreviews((prev) => {
        revokeLocalPreviewUrls(prev);
        return [];
      });
      const status = await getLocalImageLibraryStatus(brandId);
      setLocalLibName(status.name);
      if (imgs.length === 0) {
        setPickedLocalName(null);
        setLocalMatchConfident(false);
        setLocalLibNeedsPermission(status.hasHandle && status.permission !== 'granted');
        return;
      }
      setLocalLibNeedsPermission(false);

      const title = topicTitle ?? '';
      const ranked = title ? rankLocalImages(title, imgs) : imgs.map((entry) => ({
        entry,
        score: 0,
        confidence: 0,
        reason: '',
      }));
      const ordered = ranked.map((r) => r.entry);
      const previewsRaw = await loadLocalImagePreviews(ordered, 48);
      const scoreByName = new Map(ranked.map((r) => [r.entry.name, r]));
      setLocalPreviews(
        previewsRaw.map((p) => {
          const s = scoreByName.get(p.name);
          return {
            name: p.name,
            url: p.url,
            confidence: s?.confidence ?? 0,
            reason: s?.reason ?? '',
          };
        }),
      );

      const confident = title ? isConfidentLocalMatch(title, imgs) : false;
      setLocalMatchConfident(confident);
      const best = title ? pickBestLocalImage(title, imgs) : ordered[0];
      setPickedLocalName(best?.name ?? ordered[0]!.name);
    } catch {
      setLocalLibCount(0);
      setLocalMatchConfident(false);
      setLocalPreviews((prev) => {
        revokeLocalPreviewUrls(prev);
        return [];
      });
    } finally {
      setLocalPreviewLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void refreshLocalLib();
  }, [refreshLocalLib]);

  useEffect(() => {
    if (detail?.topic.brandId) {
      void loadLocalGallery(detail.topic.brandId, detail.topic.title);
    }
  }, [detail?.topic.brandId, detail?.topic.id]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    return () => {
      revokeLocalPreviewUrls(localPreviews);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only revoke on unmount
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
    setSelectedIds([]);
  }, [brandFilter, coreFilter]);

  useEffect(() => {
    return () => {
      Object.values(assetUrls).forEach((u) => URL.revokeObjectURL(u));
    };
  }, [assetUrls]);

  const loadDetail = async (topicId: string, opts?: { silent?: boolean }) => {
    const silent = opts?.silent ?? detailIdRef.current === topicId;
    if (!silent) setDetailLoading(true);
    try {
      const d = await fetchContentTopicDetail(topicId);
      detailIdRef.current = d.topic.id;
      setDetail(d);
      if (d.topic.corePackageId) setCoreFilter(d.topic.corePackageId);
      try {
        const [ch, st] = await Promise.all([
          fetchContentChannels(d.topic.brandId),
          fetchContentSites(d.topic.brandId),
        ]);
        setChannels(ch);
        setSites(st);
      } catch {
        setChannels([]);
        setSites([]);
      }
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
      if (localLibName && !localLibNeedsPermission) {
        void loadLocalGallery(d.topic.brandId, d.topic.title);
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được chi tiết bài'));
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    const topicId = searchParams.get('topic');
    if (!topicId) return;
    setDetailTab('write');
    setDetailOpen(true);
    void loadDetail(topicId);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- open from Videos / Góc brand
  }, [searchParams.get('topic')]);

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
    setDetailTab('write');
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
        message.success(res.message ?? 'AI đã viết xong');
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
      const res = await generateContentTopic(row.id);
      message.success(res.message ?? `AI xong: ${row.title}`);
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
      title: 'AI viết + ảnh bài này?',
      content: row.title,
      okText: 'Viết + tạo ảnh',
      cancelText: 'Huỷ',
      onOk: () => onGenerateRow(row),
    });
  };

  const onGenerate = async (skipImages = false) => {
    if (!detail) return;
    setDetailAction(skipImages ? 'writeText' : 'write');
    try {
      const res = await generateContentTopic(detail.topic.id, { skipImages });
      message.success(res.message ?? (skipImages ? 'AI đã viết xong' : 'AI đã viết + tạo ảnh'));
      setDetailTab(skipImages ? 'write' : 'images');
      await loadDetail(detail.topic.id, { silent: true });
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'AI tạo bài thất bại'));
    } finally {
      setDetailAction(null);
    }
  };

  const confirmGenerate = (skipImages: boolean) => {
    if (!detail) return;
    if (detail.variants.length === 0) {
      void onGenerate(skipImages);
      return;
    }
    modal.confirm({
      title: skipImages ? 'Viết lại chữ?' : 'AI viết lại chữ + ảnh?',
      content: skipImages
        ? 'Bài đã có bản viết. Tiếp tục sẽ viết lại các kênh — không giữ bản cũ. Ảnh giữ nguyên.'
        : 'Bài đã có chữ/ảnh. Tiếp tục sẽ viết lại các kênh và tạo ảnh mới theo chủ đề — không giữ bản cũ.',
      okText: skipImages ? 'Viết lại chữ' : 'Viết + tạo ảnh lại',
      cancelText: 'Giữ bản hiện tại',
      onOk: () => onGenerate(skipImages),
    });
  };

  const onGenerateImages = async () => {
    if (!detail) return;
    setDetailAction('images');
    try {
      const res = await generateContentTopic(detail.topic.id, { imagesOnly: true });
      message.success(res.message ?? 'Đã tạo ảnh');
      setDetailTab('images');
      await loadDetail(detail.topic.id, { silent: true });
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo ảnh thất bại'));
    } finally {
      setDetailAction(null);
    }
  };

  const confirmGenerateImages = () => {
    if (!detail) return;
    if (detail.assets.length === 0) {
      void onGenerateImages();
      return;
    }
    modal.confirm({
      title: 'Tạo ảnh mới?',
      content: 'Sẽ thay ảnh AI hiện có. Bản viết giữ nguyên — không chạy lại cả quy trình.',
      okText: 'Tạo ảnh mới',
      cancelText: 'Giữ ảnh cũ',
      onOk: () => onGenerateImages(),
    });
  };

  const onSelectAsset = async (asset: ContentAsset) => {
    if (!detail) return;
    setDetailAction('pickImage');
    try {
      await selectContentAsset(detail.topic.id, asset.id);
      message.success('Đã chọn ảnh này');
      await loadDetail(detail.topic.id, { silent: true });
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không chọn được ảnh'));
    } finally {
      setDetailAction(null);
    }
  };

  const onCopyManualImage = async () => {
    if (!detail) throw new Error('Chưa mở bài');
    if (libBrandId && (pickedLocalName || localPreviews.length > 0)) {
      const imgs = await listLocalImages(libBrandId, { requestPermission: true });
      const entry =
        (pickedLocalName && imgs.find((i) => i.name === pickedLocalName)) ||
        pickBestLocalImage(detail.topic.title, imgs) ||
        imgs[0];
      if (entry) {
        const file = await prepareLocalImageForPublish(entry);
        await writeClipboardImage(file.blob);
        return;
      }
    }
    const selected = detail.assets.find((a) => a.isSelected) ?? detail.assets[0];
    if (selected && assetUrls[selected.id]) {
      const blob = await fetch(assetUrls[selected.id]).then((r) => r.blob());
      await writeClipboardImage(blob);
      return;
    }
    throw new Error('Chưa chọn ảnh — sang tab Ảnh trước.');
  };

  const onApprove = async () => {
    if (!detail) return;
    setDetailAction('approve');
    try {
      await approveContentTopic(detail.topic.id);
      message.success('Đã duyệt — có thể xuất bản');
      await loadDetail(detail.topic.id, { silent: true });
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Duyệt thất bại'));
    } finally {
      setDetailAction(null);
    }
  };

  const onApproveBatch = async () => {
    const ids = selectedIds.filter((id) => {
      const row = topics.find((t) => t.id === id);
      return row && canBulkApprove(row.status);
    });
    if (ids.length === 0) {
      message.warning('Chọn ít nhất 1 bài Chờ duyệt / Nháp / Từ chối');
      return;
    }
    setBatchApproving(true);
    let ok = 0;
    const failed: string[] = [];
    try {
      for (const id of ids) {
        try {
          await approveContentTopic(id);
          ok += 1;
        } catch {
          const title = topics.find((t) => t.id === id)?.title ?? id;
          failed.push(title);
        }
      }
      if (failed.length === 0) message.success(`Đã duyệt ${ok} bài (chưa đăng)`);
      else if (ok > 0) message.warning(`Duyệt ${ok} bài; lỗi ${failed.length}: ${failed.slice(0, 2).join(', ')}`);
      else message.error('Duyệt hàng loạt thất bại');
      setSelectedIds([]);
      await load();
    } finally {
      setBatchApproving(false);
    }
  };

  const onPublishBatch = async () => {
    const rows = selectedIds
      .map((id) => topics.find((t) => t.id === id))
      .filter((t): t is ContentTopic => !!t && canBulkPublish(t.status));
    if (rows.length === 0) {
      message.warning('Chọn ít nhất 1 bài Đã duyệt / Chờ duyệt / Nháp để xuất bản');
      return;
    }
    if (!brandFilter) {
      message.warning('Lọc một thương hiệu trước khi xuất bản hàng loạt (kho ảnh theo brand).');
      return;
    }
    if (!isLocalImageLibrarySupported() || !localLibName) {
      message.error('Gắn kho ảnh ở Thương hiệu (cột Kho ảnh máy), rồi lọc brand này để xuất bản hàng loạt.');
      return;
    }

    let localImages: Awaited<ReturnType<typeof listLocalImages>> = [];
    try {
      localImages = await listLocalImages(brandFilter, { requestPermission: true });
    } catch {
      message.error('Không đọc được kho ảnh — mở bài, tab Ảnh, bấm Cho phép đọc lại.');
      return;
    }
    if (localImages.length === 0) {
      message.warning('Kho ảnh trống — đổi thư mục ở Thương hiệu (cột Kho ảnh máy).');
      return;
    }

    const okConfirm = await new Promise<boolean>((resolve) => {
      modal.confirm({
        title: `Xuất bản ${rows.length} bài?`,
        width: 480,
        content: (
          <div>
            <p>
              Mỗi bài lấy ảnh khớp tiêu đề từ kho «<strong>{localLibName}</strong>» ({localImages.length} ảnh).
            </p>
            <p>
              Lịch đăng = <strong>ngày hiển thị</strong> của từng bài (nếu trống → đăng ngay).
            </p>
            <p style={{ color: '#64748b', marginBottom: 0 }}>
              Duyệt tự động nếu còn Chờ duyệt/Nháp — rồi đẩy lên web/FB. Không lưu ảnh lâu trên server.
            </p>
          </div>
        ),
        okText: 'Đẩy lịch đăng hàng loạt',
        cancelText: 'Huỷ',
        onOk: () => resolve(true),
        onCancel: () => resolve(false),
      });
    });
    if (!okConfirm) return;

    setBatchPublishing(true);
    let ok = 0;
    const failed: string[] = [];
    try {
      for (let i = 0; i < rows.length; i++) {
        const row = rows[i]!;
        message.loading({
          content: `Xuất bản ${i + 1}/${rows.length}: ${row.title.slice(0, 48)}…`,
          key: 'content-batch-publish',
          duration: 0,
        });
        try {
          if (row.status === 'Review' || row.status === 'Draft') {
            try {
              await approveContentTopic(row.id);
            } catch {
              /* already approved */
            }
          }
          const preferred =
            pickBestLocalImage(row.title, localImages) || localImages[i % localImages.length]!;
          const file = await prepareLocalImageForPublish(preferred);
          if (!file.blob || file.blob.size < 100) {
            failed.push(`${row.title} (ảnh)`);
            continue;
          }
          const res = await publishContentTopic(row.id, {
            includeManualExport: true,
            runImmediately: true,
            publishAt: row.displayAt ?? null,
            imageBlob: file.blob,
            imageFileName: file.fileName,
            imageContentType: file.contentType,
          });
          const jobFailed = res.jobs.filter((j) => j.status === 'Failed').length;
          if (jobFailed > 0 && res.jobs.every((j) => j.status === 'Failed')) {
            failed.push(row.title);
          } else {
            ok += 1;
          }
        } catch {
          failed.push(row.title);
        }
      }
      message.destroy('content-batch-publish');
      if (failed.length === 0) message.success(`Đã xuất bản ${ok} bài`);
      else if (ok > 0)
        message.warning(`Xuất bản ${ok} bài; lỗi ${failed.length}: ${failed.slice(0, 2).join(', ')}`);
      else message.error(`Xuất bản thất bại: ${failed.slice(0, 3).join(', ')}`);
      setSelectedIds([]);
      await load();
    } finally {
      setBatchPublishing(false);
      message.destroy('content-batch-publish');
    }
  };

  const onAllowLocalLibrary = async () => {
    if (!libBrandId) return;
    await loadLocalGallery(libBrandId, detail?.topic.title, { requestPermission: true });
  };

  const onPublish = async () => {
    if (!detail) return;
    setDetailAction('publish');
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

      const hasServerImage = detail.assets.some((a) => a.isSelected) || detail.assets.length > 0;

      let pickedFileName: string | undefined;
      let localImages: Awaited<ReturnType<typeof listLocalImages>> = [];

      if (localLibName && libBrandId) {
        localImages = await listLocalImages(libBrandId, { requestPermission: true });
        if (localImages.length === 0) {
          if (localLibNeedsPermission) {
            message.warning('Bấm «Cho phép đọc lại» kho ảnh, rồi xuất bản lại để kèm ảnh.');
          } else {
            message.warning('Kho ảnh trống — đổi thư mục ở Thương hiệu (cột Kho ảnh máy).');
          }
          return;
        }
        const preferred =
          (pickedLocalName && localImages.find((i) => i.name === pickedLocalName)) ||
          pickBestLocalImage(detail.topic.title, localImages) ||
          localImages[0]!;
        pickedFileName = preferred.name;
      } else if (!hasServerImage) {
        message.error(
          'Chưa có ảnh — bấm «Tạo ảnh» hoặc gắn kho ở Thương hiệu (Kho ảnh máy), rồi Đẩy lịch đăng. Không đăng chỉ chữ.',
        );
        return;
      }

      const okConfirm = await new Promise<boolean>((resolve) => {
        modal.confirm({
          title: 'Đẩy bài lên web / Facebook?',
          width: 480,
          content: (
            <div>
              {localLibName && localImages.length > 0 ? (
                <div style={{ marginBottom: 12 }}>
                  <Typography.Text>Ảnh từ kho máy «{localLibName}» (bắt buộc):</Typography.Text>
                  <Select
                    style={{ width: '100%', marginTop: 6 }}
                    defaultValue={pickedFileName}
                    options={rankLocalImages(detail.topic.title, localImages).map((r) => ({
                      value: r.entry.name,
                      label: `${r.entry.name}${r.reason ? ` — ${r.reason}` : ''}`,
                    }))}
                    showSearch
                    optionFilterProp="label"
                    onChange={(v) => {
                      pickedFileName = v;
                      setPickedLocalName(v);
                    }}
                  />
                </div>
              ) : (
                <p>Ảnh: dùng ảnh đã có trên bài (AI / đã chọn).</p>
              )}
              <p>
                Lịch đăng: <strong>{whenLabel}</strong> — WP/FB tự lên vào giờ đó, không cần mở lại KitPlatform.
              </p>
              <p style={{ color: '#64748b', marginBottom: 0 }}>
                Ảnh gửi kèm lần này lên WP/FB/novixa — không lưu kho lâu trên server KitPlatform.
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

      let imageBlob: Blob | undefined;
      let imageFileName: string | undefined;
      let imageContentType: string | undefined;
      if (localLibName && pickedFileName) {
        const entry = localImages.find((i) => i.name === pickedFileName) ?? localImages[0];
        if (!entry) {
          message.warning('Không đọc được ảnh đã chọn');
          return;
        }
        const file = await prepareLocalImageForPublish(entry);
        imageBlob = file.blob;
        imageFileName = file.fileName;
        imageContentType = file.contentType;
        if (!imageBlob || imageBlob.size < 100) {
          message.error('Ảnh quá nhỏ / không đọc được — chọn ảnh khác.');
          return;
        }
      } else if (!hasServerImage) {
        message.error('Thiếu ảnh — Tạo ảnh hoặc gắn kho ở Thương hiệu trước khi đăng.');
        return;
      }

      message.loading({
        content: imageFileName ? `Đang gửi ảnh «${imageFileName}»…` : 'Đang xuất bản…',
        key: 'content-publish',
        duration: 0,
      });

      const res = await publishContentTopic(detail.topic.id, {
        includeManualExport: true,
        runImmediately: true,
        publishAt: publishAt ?? null,
        imageBlob,
        imageFileName,
        imageContentType,
      });
      message.destroy('content-publish');
      const failed = res.jobs.filter((j) => j.status === 'Failed').length;
      const succeeded = res.jobs.filter((j) => j.status === 'Succeeded').length;
      const fbPhoto = res.jobs.some((j) => {
        if (j.connectorType !== 'facebook_page' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { facebook?: string }).facebook === 'photo';
        } catch {
          return false;
        }
      });
      const fbTextOnly = res.jobs.some((j) => {
        if (j.connectorType !== 'facebook_page' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { facebook?: string }).facebook === 'feed';
        } catch {
          return false;
        }
      });
      const astroHasImage = res.jobs.some((j) => {
        if (j.connectorType !== 'astro_git' || j.status !== 'Succeeded') return false;
        try {
          return (JSON.parse(j.resultJson) as { hasImage?: boolean }).hasImage === true;
        } catch {
          return false;
        }
      });
      if (failed > 0) message.warning(`Xuất bản: ${succeeded} thành công, ${failed} lỗi`);
      else if (fbTextOnly && !fbPhoto)
        message.error(
          'Facebook vẫn chỉ chữ — ảnh không tới server. Kiểm tra kho ảnh / Cho phép đọc lại, rồi Đẩy lịch đăng lại.',
        );
      else if (imageFileName && !fbPhoto && !astroHasImage && !hasServerImage)
        message.warning('Đã gửi ảnh nhưng kênh chưa xác nhận có ảnh — mở Chi tiết job để kiểm tra.');
      else
        message.success(
          `Đã đẩy lịch đăng (${succeeded} kênh)` +
            (fbPhoto || astroHasImage || imageFileName
              ? ` · có ảnh${imageFileName ? ` ${imageFileName}` : ''}`
              : hasServerImage
                ? ' · ảnh trên bài'
                : ''),
        );
      await loadDetail(detail.topic.id);
      await load();
    } catch (e) {
      message.destroy('content-publish');
      message.error(apiErrorMessage(e, 'Xuất bản thất bại'));
    } finally {
      setDetailAction(null);
    }
  };

  const onRetryJob = async (job: ContentPublishJob) => {
    if (!detail) return;
    setBusy(true);
    try {
      let imageBlob: Blob | undefined;
      let imageFileName: string | undefined;
      const needsImage =
        job.connectorType === 'facebook_page' ||
        job.connectorType === 'astro_git' ||
        job.connectorType === 'wordpress_rest';

      if (needsImage && localLibName && libBrandId) {
        const localImages = await listLocalImages(libBrandId, { requestPermission: true });
        const preferred =
          (pickedLocalName && localImages.find((i) => i.name === pickedLocalName)) ||
          pickBestLocalImage(detail.topic.title, localImages) ||
          localImages[0];
        if (preferred) {
          const file = await prepareLocalImageForPublish(preferred);
          imageBlob = file.blob;
          imageFileName = file.fileName;
          message.loading({ content: `Đang chạy lại ${job.connectorType} + ảnh «${imageFileName}»…`, key: 'content-retry', duration: 0 });
        } else {
          message.warning('Chưa chọn được ảnh local — sẽ chạy lại kênh này không kèm ảnh mới.');
        }
      }

      const updated = await runContentPublishJob(job.id, {
        imageBlob,
        imageFileName,
        publishAt: detail.topic.displayAt ?? undefined,
      });
      message.destroy('content-retry');
      if (updated.status === 'Succeeded') {
        message.success(`Đã chạy lại ${job.connectorType} — thành công`);
      } else {
        message.error(updated.lastError || `Chạy lại ${job.connectorType} thất bại`);
      }
      await loadDetail(detail.topic.id);
    } catch (e) {
      message.destroy('content-retry');
      message.error(apiErrorMessage(e, 'Chạy lại thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onReconnectFacebook = async (job: ContentPublishJob) => {
    const brandId = job.brandId || detail?.topic.brandId;
    if (!brandId) {
      message.error('Thiếu thương hiệu để kết nối Facebook.');
      return;
    }
    setBusy(true);
    try {
      if (detail?.topic.id) {
        sessionStorage.setItem(FB_RETURN_KEY, `/content/topics?topic=${detail.topic.id}`);
      }
      const r = await startFacebookOAuth(brandId);
      window.location.href = r.url;
    } catch (e) {
      const msg = apiErrorMessage(e, 'Chưa mở được Facebook Login. Cấu hình App ở Model AI → Facebook.');
      message.error(msg);
      if (/chưa cấu hình facebook app|model ai/i.test(msg)) {
        if (detail?.topic.id) {
          sessionStorage.setItem(FB_RETURN_KEY, `/content/topics?topic=${detail.topic.id}`);
        }
        navigate('/content/ai#facebook');
      }
      setBusy(false);
    }
  };

  const onDeleteTopic = async (row: ContentTopic) => {
    setRowBusyId(row.id);
    try {
      await deleteContentTopic(row.id);
      message.success('Đã xóa bài');
      if (detail?.topic.id === row.id) {
        setDetailOpen(false);
        setDetail(null);
      }
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Xóa bài thất bại'));
    } finally {
      setRowBusyId(null);
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
            Bài viết
          </Typography.Title>
          <Typography.Text type="secondary">
            Ảnh + duyệt + xuất bản cho góc brand đã viết. Ý tưởng gốc chỉ nằm ở Góc brand / Idea Pool,
            không đưa vào đây.
            {brands.length === 0 ? ' Hãy thêm thương hiệu trước.' : null}
          </Typography.Text>
        </div>
        <Space wrap>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Lọc ý tưởng gốc"
            style={{ minWidth: 260 }}
            value={coreFilter}
            onChange={setCoreFilter}
            options={coreOptions}
          />
          <Select
            allowClear
            placeholder="Lọc / chọn thương hiệu"
            style={{ width: 200 }}
            value={brandFilter}
            onChange={(v) => setBrandFilter(v)}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
          <Button
            icon={<CheckOutlined />}
            disabled={selectedIds.length === 0 || batchPublishing}
            loading={batchApproving}
            onClick={() => void onApproveBatch()}
          >
            Duyệt đã chọn ({selectedIds.length})
          </Button>
          <Button
            type="primary"
            icon={<CloudUploadOutlined />}
            disabled={selectedIds.length === 0 || batchApproving}
            loading={batchPublishing}
            onClick={() => void onPublishBatch()}
          >
            Xuất bản đã chọn ({selectedIds.length})
          </Button>
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
        dataSource={visibleTopics}
        locale={{
          emptyText:
            brands.length === 0
              ? 'Chưa có thương hiệu — vào tab Thương hiệu trước'
              : 'Chưa có bài — bấm «Nhập Excel» hoặc «Dán nhiều tiêu đề»',
        }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
          getCheckboxProps: (row) => ({
            disabled: !canSelectRow(row.status),
          }),
        }}
        columns={
          [
            ...(brandFilter
              ? []
              : [{ title: 'Thương hiệu', dataIndex: 'brandName', width: 120 } as const]),
            {
              title: 'Tiêu đề',
              dataIndex: 'title',
              render: (title: string, row: ContentTopic) => (
                <div>
                  <div>{title}</div>
                  {row.coreTitle && row.coreTitle !== title ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Từ ý tưởng: {row.coreTitle}
                    </Typography.Text>
                  ) : null}
                </div>
              ),
            },
            {
              title: 'Bản viết',
              dataIndex: 'variantCount',
              width: 90,
              align: 'center',
              render: (n?: number) => n ?? 0,
            },
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
              render: (_: unknown, row: ContentTopic) => (
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
                      AI viết + ảnh
                    </Button>
                  )}
                  <Button type="link" onClick={() => openDetail(row)}>
                    Xem / duyệt
                  </Button>
                  <Button type="link" onClick={() => openEdit(row)}>
                    Sửa
                  </Button>
                  <Popconfirm
                    title="Xóa bài này?"
                    description="Xóa luôn bản viết, ảnh AI và lịch xuất bản gắn bài. Không gỡ bài đã lên Facebook/web."
                    okText="Xóa"
                    okButtonProps={{ danger: true }}
                    cancelText="Huỷ"
                    onConfirm={() => void onDeleteTopic(row)}
                  >
                    <Button type="link" danger icon={<DeleteOutlined />} loading={rowBusyId === row.id}>
                      Xóa
                    </Button>
                  </Popconfirm>
                </Space>
              ),
            },
          ] as ColumnsType<ContentTopic>
        }
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
        width={1040}
        styles={{ body: { paddingTop: 12, background: '#f8fafc' } }}
        loading={detailLoading}
        extra={
          detail ? (
            <Space wrap>
              <Button
                type={detail.variants.length === 0 ? 'primary' : 'default'}
                icon={<ThunderboltOutlined />}
                loading={detailAction === 'write'}
                disabled={!!detailAction && detailAction !== 'write'}
                onClick={() => confirmGenerate(false)}
              >
                AI viết + ảnh
              </Button>
              <Button
                loading={detailAction === 'writeText'}
                disabled={!!detailAction && detailAction !== 'writeText'}
                onClick={() => confirmGenerate(true)}
              >
                Chỉ viết chữ
              </Button>
              {!localLibName ? (
                <Button
                  loading={detailAction === 'images'}
                  disabled={!!detailAction && detailAction !== 'images'}
                  onClick={() => confirmGenerateImages()}
                >
                  Tạo ảnh
                </Button>
              ) : null}
              <Button
                icon={<CheckOutlined />}
                loading={detailAction === 'approve'}
                disabled={!!detailAction && detailAction !== 'approve'}
                onClick={() => void onApprove()}
              >
                Duyệt
              </Button>
              <Button
                icon={<CloudUploadOutlined />}
                loading={detailAction === 'publish'}
                disabled={!!detailAction && detailAction !== 'publish'}
                onClick={() => void onPublish()}
              >
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
                  ? <>«AI viết + ảnh» ra chữ và ảnh theo chủ đề. Đã xong thì <strong>Đẩy lịch đăng</strong>. Nhóm / LinkedIn → tab <strong>Đăng tay</strong>.</>
                  : <>«AI viết + ảnh» = chữ + ảnh cùng lúc. Xong thì chọn ảnh → <strong>Duyệt</strong> → <strong>Xuất bản</strong>. «Chỉ viết chữ» nếu không cần ảnh mới.</>}
              </Typography.Paragraph>
            </div>

            <Tabs
              activeKey={detailTab}
              onChange={setDetailTab}
              items={[
                {
                  key: 'write',
                  label: `Bản viết (${detail.variants.length})`,
                  children:
                    detail.variants.length === 0 ? (
                      <Alert
                        type="info"
                        showIcon
                        message="Bài này chưa có chữ"
                        description="Đây thường là ý tưởng gốc (Xuân Hòa / sổ tay), không phải góc Famixa/KIT Tech đã Generate. Xóa lọc thương hiệu trên bảng, tìm hàng Bản viết > 0, hoặc mở từ Góc brand."
                      />
                    ) : (
                      detail.variants.map((v: ContentVariant) => (
                        <div key={v.id} style={{ marginBottom: 16 }}>
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
                    ),
                },
                {
                  key: 'images',
                  label: localLibName
                    ? `Ảnh · ${localLibName} (${localLibCount})`
                    : `Ảnh (${detail.assets.length})`,
                  children: (
            <Card
              size="small"
              title={
                localLibName
                  ? `Kho ảnh máy «${localLibName}» (${localLibCount})`
                  : `Ảnh để chọn (${detail.assets.length})`
              }
              extra={
                localLibName ? (
                  <Space size={4}>
                    {localLibNeedsPermission ? (
                      <Button size="small" type="primary" loading={localPreviewLoading} onClick={() => void onAllowLocalLibrary()}>
                        Cho phép đọc lại
                      </Button>
                    ) : (
                      <Button size="small" loading={localPreviewLoading} onClick={() => void loadLocalGallery(detail.topic.brandId, detail.topic.title)}>
                        Tải lại ảnh
                      </Button>
                    )}
                  </Space>
                ) : null
              }
            >
              {localLibName ? (
                <div>
                  {localPreviewLoading ? (
                    <Typography.Text type="secondary">Đang đọc ảnh từ máy…</Typography.Text>
                  ) : localLibNeedsPermission ? (
                    <Space direction="vertical" size={8}>
                      <Typography.Text type="warning">
                        Trình duyệt đã nhớ thư mục «{localLibName}», nhưng sau khi tải lại trang cần bấm một lần để cho phép đọc (Chrome không tự mở lại quyền).
                      </Typography.Text>
                      <Button type="primary" icon={<FolderOpenOutlined />} onClick={() => void onAllowLocalLibrary()}>
                        Cho phép đọc lại
                      </Button>
                    </Space>
                  ) : localPreviews.length === 0 ? (
                    <Space direction="vertical" size={8}>
                      <Typography.Text type="warning">
                        Chưa thấy ảnh trong thư mục. Đổi kho ở Thương hiệu (cột Kho ảnh máy) — file .png / .jpg / .webp.
                      </Typography.Text>
                    </Space>
                  ) : (
                    <>
                      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                        Ảnh xếp theo độ khớp tiêu đề bài
                        {localMatchConfident ? (
                          <>
                            {' '}
                            · <Typography.Text type="success">đã chọn ảnh khớp mạnh</Typography.Text>
                          </>
                        ) : (
                          <>
                            {' '}
                            · <Typography.Text type="warning">khớp yếu — hãy chọn tay nếu sai</Typography.Text>
                          </>
                        )}
                        {pickedLocalName ? (
                          <>
                            {' '}
                            · đang chọn: <Typography.Text code>{pickedLocalName}</Typography.Text>
                          </>
                        ) : null}
                        {localLibCount > localPreviews.length
                          ? ` · đang hiện ${localPreviews.length}/${localLibCount}`
                          : null}
                        . «Đang dùng» chỉ chọn trên máy — phải bấm <strong>Đẩy lịch đăng</strong> mới gửi ảnh lên
                        Facebook.
                      </Typography.Paragraph>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                        {localPreviews.map((p) => {
                          const selected = pickedLocalName === p.name;
                          return (
                            <div
                              key={p.name}
                              style={{
                                width: 160,
                                border: selected ? '2px solid #1677ff' : '1px solid #e2e8f0',
                                borderRadius: 8,
                                padding: 8,
                                cursor: 'pointer',
                              }}
                              onClick={() => setPickedLocalName(p.name)}
                            >
                              <img
                                src={p.url}
                                alt={p.name}
                                style={{ width: '100%', height: 100, objectFit: 'cover', borderRadius: 4 }}
                              />
                              <Typography.Text
                                ellipsis
                                style={{ display: 'block', marginTop: 6, fontSize: 12 }}
                                title={p.name}
                              >
                                {p.name}
                              </Typography.Text>
                              {p.reason ? (
                                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                                  {p.reason}
                                  {p.confidence > 0 ? ` · ${p.confidence}%` : ''}
                                </Typography.Text>
                              ) : null}
                              <Button
                                size="small"
                                block
                                type={selected ? 'primary' : 'default'}
                                style={{ marginTop: 6 }}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setPickedLocalName(p.name);
                                }}
                              >
                                {selected ? 'Đang dùng' : 'Chọn ảnh này'}
                              </Button>
                            </div>
                          );
                        })}
                      </div>
                    </>
                  )}
                  {detail.assets.length > 0 ? (
                    <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                      (Vẫn còn {detail.assets.length} ảnh AI trên server — khi dùng kho máy, ưu tiên ảnh local khi đăng.)
                    </Typography.Paragraph>
                  ) : null}
                </div>
              ) : (
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
                        loading={detailAction === 'pickImage'}
                        onClick={() => void onSelectAsset(a)}
                      >
                        {a.isSelected ? 'Đang dùng' : 'Chọn ảnh này'}
                      </Button>
                    </div>
                  ))}
                  {detail.assets.length === 0 ? (
                    <Space direction="vertical" size={8}>
                      <Typography.Text type="secondary">
                        Chưa có ảnh. Bấm «Tạo ảnh ngay», hoặc gắn kho máy ở Thương hiệu (cột Kho ảnh máy).
                      </Typography.Text>
                      <Button
                        type="primary"
                        loading={detailAction === 'images'}
                        onClick={() => confirmGenerateImages()}
                      >
                        Tạo ảnh ngay
                      </Button>
                    </Space>
                  ) : null}
                </div>
              )}
            </Card>
                  ),
                },
                {
                  key: 'manual',
                  label: 'Đăng tay',
                  children: (
                    <ContentManualPostTab
                      channels={channels}
                      sites={sites}
                      variants={detail.variants}
                      hasImage={
                        !!pickedLocalName ||
                        localPreviews.length > 0 ||
                        detail.assets.some((a) => a.isSelected) ||
                        detail.assets.length > 0
                      }
                      onCopyImage={onCopyManualImage}
                      onCopied={(ok, detailMsg) => {
                        if (ok) message.success(detailMsg);
                        else message.warning(detailMsg);
                      }}
                    />
                  ),
                },
                {
                  key: 'jobs',
                  label: `Xuất bản (${detail.jobs.length})`,
                  children: (
            <Card
              size="small"
              title={`Xuất bản (${detail.jobs.length})`}
              styles={{ body: { paddingTop: 12 } }}
            >
              <Table
                size="middle"
                rowKey="id"
                pagination={false}
                dataSource={detail.jobs}
                locale={{ emptyText: 'Chưa xuất bản' }}
                scroll={{ x: 880 }}
                columns={[
                  {
                    title: 'Kênh',
                    dataIndex: 'connectorType',
                    width: 200,
                    render: (t: string) => {
                      const c = connectorLabel(t);
                      return <Tag color={c.color}>{c.title}</Tag>;
                    },
                  },
                  {
                    title: 'Kết quả',
                    dataIndex: 'status',
                    width: 120,
                    render: (s: string) => (
                      <Tag color={s === 'Failed' ? 'error' : s === 'Succeeded' ? 'success' : 'processing'}>
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
                    render: (_, j: ContentPublishJob) => {
                      if (j.lastError) {
                        const err = formatPublishError(j.lastError);
                        return (
                          <div style={{ maxWidth: 520 }}>
                            <Typography.Text type="danger" strong style={{ display: 'block' }}>
                              {err.summary}
                            </Typography.Text>
                            {err.hint ? (
                              <Typography.Paragraph
                                type="secondary"
                                style={{ margin: '6px 0 0', fontSize: 12, lineHeight: 1.45 }}
                              >
                                {err.hint}
                              </Typography.Paragraph>
                            ) : null}
                            <Collapse
                              ghost
                              size="small"
                              style={{ marginTop: 4 }}
                              items={[
                                {
                                  key: 'raw',
                                  label: (
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                      Xem lỗi kỹ thuật
                                    </Typography.Text>
                                  ),
                                  children: (
                                    <Typography.Paragraph
                                      code
                                      copyable
                                      style={{
                                        margin: 0,
                                        fontSize: 11,
                                        whiteSpace: 'pre-wrap',
                                        wordBreak: 'break-word',
                                        maxHeight: 120,
                                        overflow: 'auto',
                                      }}
                                    >
                                      {err.raw}
                                    </Typography.Paragraph>
                                  ),
                                },
                              ]}
                            />
                          </div>
                        );
                      }
                      let mediaHint = '';
                      let pathHint = '';
                      let categoryHint = '';
                      try {
                        const r = j.resultJson
                          ? (JSON.parse(j.resultJson) as {
                              facebook?: string;
                              hasImage?: boolean;
                              image?: string;
                              path?: string;
                              category?: string;
                            })
                          : null;
                        if (r?.facebook === 'photo') mediaHint = 'Có ảnh';
                        else if (r?.facebook === 'feed') mediaHint = 'Chỉ chữ (không ảnh)';
                        else if (r?.hasImage) mediaHint = `Có ảnh${r.image ? ` · ${r.image}` : ''}`;
                        else if (j.connectorType === 'astro_git' && r && r.hasImage === false)
                          mediaHint = 'Chưa kèm ảnh';
                        if (r?.path) pathHint = r.path;
                        if (r?.category) categoryHint = r.category;
                      } catch {
                        /* ignore */
                      }
                      return (
                        <Space direction="vertical" size={2} style={{ maxWidth: 480 }}>
                          <Typography.Text style={{ wordBreak: 'break-all' }}>
                            {j.externalRef || 'OK'}
                          </Typography.Text>
                          <Space size={4} wrap>
                            {mediaHint ? (
                              <Tag color={mediaHint.startsWith('Có') ? 'success' : 'warning'}>{mediaHint}</Tag>
                            ) : null}
                            {categoryHint ? <Tag color="purple">Chuyên mục: {categoryHint}</Tag> : null}
                          </Space>
                          {pathHint ? (
                            <Typography.Text type="secondary" style={{ fontSize: 12 }} copyable>
                              {pathHint}
                            </Typography.Text>
                          ) : null}
                        </Space>
                      );
                    },
                  },
                  {
                    title: 'Thao tác',
                    width: 220,
                    fixed: 'right',
                    render: (_, j) =>
                      j.status === 'Failed' || j.status === 'Queued' || j.status === 'Succeeded' ? (
                        <Space size={6} wrap>
                          {isFacebookReconnectJob(j) ? (
                            <Button
                              type="primary"
                              size="small"
                              icon={<FacebookOutlined />}
                              loading={busy}
                              onClick={() => void onReconnectFacebook(j)}
                            >
                              Kết nối lại
                            </Button>
                          ) : null}
                          <Button
                            type={j.status === 'Failed' && !isFacebookReconnectJob(j) ? 'primary' : 'link'}
                            size="small"
                            danger={j.status === 'Failed' && !isFacebookReconnectJob(j)}
                            loading={busy}
                            onClick={() => void onRetryJob(j)}
                          >
                            {j.connectorType === 'facebook_page' ||
                            j.connectorType === 'astro_git' ||
                            j.connectorType === 'wordpress_rest'
                              ? 'Đăng lại + ảnh'
                              : 'Chạy lại'}
                          </Button>
                        </Space>
                      ) : null,
                  },
                ]}
              />
            </Card>
                  ),
                },
              ]}
            />
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
