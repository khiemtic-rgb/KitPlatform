import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Collapse,
  Col,
  Divider,
  Drawer,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import {
  AimOutlined,
  ApiOutlined,
  BankOutlined,
  BgColorsOutlined,
  BookOutlined,
  CodeOutlined,
  DollarOutlined,
  EditOutlined,
  FolderOpenOutlined,
  FontColorsOutlined,
  GithubOutlined,
  GlobalOutlined,
  KeyOutlined,
  LinkOutlined,
  NumberOutlined,
  PictureOutlined,
  PlusOutlined,
  ReloadOutlined,
  SaveOutlined,
  SendOutlined,
  StopOutlined,
  TagsOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createContentBrand,
  fetchContentBrands,
  fetchContentChannels,
  fetchContentSettings,
  fetchContentSites,
  updateContentBrand,
  upsertContentChannel,
  upsertContentSite,
  type ContentBrand,
  type ContentChannelTarget,
  type ContentSiteTarget,
} from '@/shared/api/content.api';
import {
  clearLocalImageLibrary,
  getLocalImageLibraryNames,
  isLocalImageLibrarySupported,
  pickLocalImageLibrary,
} from '@/modules/content/content-local-image-library';

/** Unified destination kinds shown in one combobox. */
type DestKind =
  | 'site:manual'
  | 'site:wordpress_rest'
  | 'site:astro_git'
  | 'site:buffer'
  | 'channel:facebook_page'
  | 'channel:instagram'
  | 'channel:linkedin'
  | 'channel:threads'
  | 'channel:zalo_oa'
  | 'channel:tiktok'
  | 'channel:youtube'
  | 'channel:other';

type DestRow = {
  key: string;
  group: 'site' | 'channel';
  code: string;
  name: string;
  kindLabel: string;
  kindValue: DestKind | string;
  address: string;
  secretRef?: string | null;
  secretConfigured: boolean;
  /** Empty = OK; otherwise short Vietnamese warning. */
  configWarning?: string;
  raw: ContentSiteTarget | ContentChannelTarget;
};

const DEST_KIND_OPTIONS: { value: DestKind; label: string; group: 'site' | 'channel' }[] = [
  { value: 'site:manual', label: 'Website · Thủ công (chép bài)', group: 'site' },
  { value: 'site:wordpress_rest', label: 'Website · WordPress', group: 'site' },
  { value: 'site:astro_git', label: 'Website · Astro / Git', group: 'site' },
  { value: 'site:buffer', label: 'Website · Buffer / lịch đăng', group: 'site' },
  { value: 'channel:facebook_page', label: 'MXH · Facebook Fanpage', group: 'channel' },
  { value: 'channel:instagram', label: 'MXH · Instagram', group: 'channel' },
  { value: 'channel:linkedin', label: 'MXH · LinkedIn', group: 'channel' },
  { value: 'channel:threads', label: 'MXH · Threads', group: 'channel' },
  { value: 'channel:zalo_oa', label: 'MXH · Zalo OA', group: 'channel' },
  { value: 'channel:tiktok', label: 'MXH · TikTok', group: 'channel' },
  { value: 'channel:youtube', label: 'MXH · YouTube', group: 'channel' },
  { value: 'channel:other', label: 'MXH · Khác', group: 'channel' },
];

const KIND_LABEL = Object.fromEntries(DEST_KIND_OPTIONS.map((o) => [o.value, o.label])) as Record<
  string,
  string
>;

function siteKind(connectorType: string): string {
  const k = `site:${connectorType}` as DestKind;
  return KIND_LABEL[k] ?? `Website · ${connectorType}`;
}

function channelKind(channelType: string): string {
  const k = `channel:${channelType}` as DestKind;
  return KIND_LABEL[k] ?? `MXH · ${channelType}`;
}

function parseKind(kind: string): { group: 'site' | 'channel'; type: string } {
  const [group, ...rest] = kind.split(':');
  return {
    group: group === 'channel' ? 'channel' : 'site',
    type: rest.join(':') || (group === 'channel' ? 'other' : 'manual'),
  };
}

function parseConfigObj(raw?: string | null): Record<string, unknown> {
  if (!raw?.trim()) return {};
  try {
    const v = JSON.parse(raw) as unknown;
    return v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function buildConfigJson(kind: string, v: Record<string, unknown>): string {
  const base = parseConfigObj(typeof v.configJson === 'string' ? v.configJson : '{}');
  if (kind === 'site:wordpress_rest') {
    if (typeof v.wpUsername === 'string' && v.wpUsername.trim()) base.username = v.wpUsername.trim();
    if (typeof v.wpStatus === 'string' && v.wpStatus.trim()) base.status = v.wpStatus.trim();
    if (typeof v.wpCategories === 'string' && v.wpCategories.trim()) {
      base.wpCategories = v.wpCategories.trim();
    }
  }
  if (kind === 'site:astro_git') {
    if (typeof v.gitOwner === 'string' && v.gitOwner.trim()) base.owner = v.gitOwner.trim();
    if (typeof v.gitRepo === 'string' && v.gitRepo.trim()) base.repo = v.gitRepo.trim();
    if (typeof v.gitBranch === 'string' && v.gitBranch.trim()) base.branch = v.gitBranch.trim();
    if (typeof v.gitContentPath === 'string' && v.gitContentPath.trim()) {
      base.contentPath = v.gitContentPath.trim();
    }
    if (typeof v.gitImagePath === 'string' && v.gitImagePath.trim()) {
      base.imagePath = v.gitImagePath.trim();
    }
    if (typeof v.insightLocale === 'string' && v.insightLocale.trim()) {
      base.locale = v.insightLocale.trim();
    }
    if (typeof v.insightCategory === 'string' && v.insightCategory.trim()) {
      base.insightCategory = v.insightCategory.trim();
    }
    if (typeof v.blogCategory === 'string' && v.blogCategory.trim()) {
      base.blogCategory = v.blogCategory.trim();
    }
    if (typeof v.insightSection === 'string' && v.insightSection.trim()) {
      base.insightSection = v.insightSection.trim();
    }
    const path = typeof base.contentPath === 'string' ? base.contentPath.toLowerCase() : '';
    if (path.includes('insights')) base.contentFormat = 'insights';
    if (path.includes('famixa-site')) base.contentFormat = 'famixa';
  }
  return JSON.stringify(base);
}

function secretHint(kind?: string): { label: string; pasteLabel: string; extra: string; placeholder: string } {
  switch (kind) {
    case 'site:wordpress_rest':
      return {
        label: 'Hoặc dùng tên biến env',
        pasteLabel: 'Dán Application Password WordPress',
        extra: 'Dán mật khẩu ứng dụng WP vào ô trên (lưu server, không hiện lại). Env chỉ cần nếu không muốn lưu DB.',
        placeholder: 'xxxx xxxx xxxx xxxx',
      };
    case 'site:astro_git':
      return {
        label: 'Hoặc dùng tên biến env',
        pasteLabel: 'Dán GitHub token',
        extra: 'Dán token (ghp_…) vào ô trên. Env chỉ cần nếu không muốn lưu DB.',
        placeholder: 'ghp_…',
      };
    case 'channel:facebook_page':
      return {
        label: 'Hoặc dùng tên biến env',
        pasteLabel: 'Dán Page Access Token Facebook',
        extra: 'Dán token (EAA…) vào ô trên — lưu trên server, GET không trả lại. Để trống = giữ token cũ.',
        placeholder: 'EAAxxxx…',
      };
    default:
      return {
        label: 'Hoặc dùng tên biến env',
        pasteLabel: 'Dán token / mật khẩu',
        extra: 'Dán vào ô trên nếu đăng tự động. Để trống khi sửa = giữ token đã lưu.',
        placeholder: 'token…',
      };
  }
}

type BrandTargets = { sites: number; channels: number };

export function ContentBrandsPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [targetCounts, setTargetCounts] = useState<Record<string, BrandTargets>>({});
  const [imageFolderNames, setImageFolderNames] = useState<Record<string, string | null>>({});
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerTab, setDrawerTab] = useState('info');
  const [editing, setEditing] = useState<ContentBrand | null>(null);
  const [sites, setSites] = useState<ContentSiteTarget[]>([]);
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [savingDest, setSavingDest] = useState(false);
  const [secretConfigured, setSecretConfigured] = useState(false);
  const [form] = Form.useForm();
  const [destForm] = Form.useForm();
  const destKind = Form.useWatch('kind', destForm) as string | undefined;

  const loadTargetCounts = useCallback(async (list: ContentBrand[]) => {
    const entries = await Promise.all(
      list.map(async (b) => {
        try {
          const [s, c] = await Promise.all([fetchContentSites(b.id), fetchContentChannels(b.id)]);
          return [b.id, { sites: s.length, channels: c.length }] as const;
        } catch {
          return [b.id, { sites: 0, channels: 0 }] as const;
        }
      }),
    );
    setTargetCounts(Object.fromEntries(entries));
  }, []);

  const loadImageFolders = useCallback(async (list: ContentBrand[]) => {
    if (!isLocalImageLibrarySupported()) {
      setImageFolderNames({});
      return;
    }
    try {
      const map = await getLocalImageLibraryNames(list.map((b) => b.id));
      setImageFolderNames(map);
    } catch {
      setImageFolderNames({});
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const b = await fetchContentBrands();
      setBrands(b);
      void loadTargetCounts(b);
      void loadImageFolders(b);
      // warm settings (connector lists live in org; kinds are fixed in UI for flexibility)
      void fetchContentSettings().catch(() => undefined);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được thương hiệu'));
    } finally {
      setLoading(false);
    }
  }, [loadTargetCounts, loadImageFolders, message]);

  const onPickBrandImageFolder = async (brand: ContentBrand) => {
    if (!isLocalImageLibrarySupported()) {
      message.warning('Cần Chrome / Edge để chọn thư mục ảnh trên máy.');
      return;
    }
    // Do not setState before showDirectoryPicker — React re-render drops user gesture → SecurityError.
    try {
      const r = await pickLocalImageLibrary(brand.id);
      setImageFolderNames((prev) => ({ ...prev, [brand.id]: r.name }));
      message.success(
        r.count > 0
          ? `«${brand.code}» → kho ảnh «${r.name}» (${r.count} ảnh)`
          : `Đã gắn «${r.name}» cho «${brand.code}» — chưa thấy file ảnh trong thư mục.`,
      );
    } catch (e) {
      if (e && typeof e === 'object' && 'name' in e && (e as { name: string }).name === 'AbortError') return;
      message.error(e instanceof Error && e.message ? e.message : apiErrorMessage(e, 'Không chọn được thư mục'));
    }
  };

  const onClearBrandImageFolder = async (brand: ContentBrand) => {
    await clearLocalImageLibrary(brand.id);
    setImageFolderNames((prev) => ({ ...prev, [brand.id]: null }));
    message.info(`Đã bỏ kho ảnh của «${brand.code}»`);
  };

  useEffect(() => {
    void load();
  }, [load]);

  const loadTargets = async (brandId: string) => {
    const [s, c] = await Promise.all([fetchContentSites(brandId), fetchContentChannels(brandId)]);
    setSites(s);
    setChannels(c);
    setTargetCounts((prev) => ({ ...prev, [brandId]: { sites: s.length, channels: c.length } }));
  };

  const destRows: DestRow[] = useMemo(() => {
    const siteRows: DestRow[] = sites.map((s) => {
      const cfg = parseConfigObj(s.configJson);
      let configWarning: string | undefined;
      if (s.connectorType === 'astro_git') {
        const missing: string[] = [];
        if (!(typeof cfg.owner === 'string' && cfg.owner.trim())) missing.push('owner');
        if (!(typeof cfg.repo === 'string' && cfg.repo.trim())) missing.push('repo');
        if (!s.secretConfigured) missing.push('token');
        if (missing.length) configWarning = `Thiếu ${missing.join(', ')}`;
      }
      return {
        key: `site:${s.id}`,
        group: 'site' as const,
        code: s.code,
        name: s.name,
        kindLabel: siteKind(s.connectorType),
        kindValue: `site:${s.connectorType}`,
        address: s.baseUrl?.trim() || '—',
        secretRef: s.secretRef,
        secretConfigured: s.secretConfigured,
        configWarning,
        raw: s,
      };
    });
    const channelRows: DestRow[] = channels.map((c) => {
      let configWarning: string | undefined;
      if (c.channelType === 'facebook_page') {
        if (!c.externalId?.trim()) configWarning = 'Thiếu Page ID';
        else if (!c.secretConfigured) configWarning = 'Thiếu token';
      }
      return {
        key: `channel:${c.id}`,
        group: 'channel' as const,
        code: c.code,
        name: c.name,
        kindLabel: channelKind(c.channelType),
        kindValue: `channel:${c.channelType}`,
        address: c.externalId?.trim() || '—',
        secretRef: c.secretRef,
        secretConfigured: c.secretConfigured,
        configWarning,
        raw: c,
      };
    });
    return [...siteRows, ...channelRows].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }, [sites, channels]);

  const openCreate = () => {
    setEditing(null);
    setDrawerTab('info');
    form.resetFields();
    form.setFieldsValue({
      pauseWhenExceeded: true,
      isActive: true,
      sortOrder: 100,
      knowledge: {
        tone: [],
        forbiddenTopics: [],
        preferredTerms: [],
        avoidTerms: [],
        hashtags: [],
      },
    });
    setSites([]);
    setChannels([]);
    destForm.resetFields();
    setDrawerOpen(true);
  };

  const resetDestForm = (kind: DestKind = 'site:manual') => {
    destForm.resetFields();
    destForm.setFieldsValue({ kind, configJson: '{}', secret: '' });
    setSecretConfigured(false);
  };

  const openEdit = async (row: ContentBrand, tab: 'info' | 'targets' = 'info') => {
    setEditing(row);
    setDrawerTab(tab);
    const k = row.knowledge ?? {
      tone: [],
      forbiddenTopics: [],
      preferredTerms: [],
      avoidTerms: [],
      hashtags: [],
    };
    form.setFieldsValue({
      code: row.code,
      name: row.name,
      defaultCtaUrl: row.defaultCtaUrl ?? undefined,
      defaultCtaLabel: row.defaultCtaLabel ?? undefined,
      monthlyCeilingUsd: row.monthlyCeilingUsd ?? undefined,
      imageTier: row.imageTier ?? undefined,
      pauseWhenExceeded: row.pauseWhenExceeded,
      isActive: row.isActive,
      sortOrder: row.sortOrder,
      operationalBrief: row.operationalBrief ?? undefined,
      knowledge: {
        positioning: k.positioning ?? undefined,
        audience: k.audience ?? undefined,
        tone: k.tone ?? [],
        forbiddenTopics: k.forbiddenTopics ?? [],
        preferredTerms: k.preferredTerms ?? [],
        avoidTerms: k.avoidTerms ?? [],
        hashtags: k.hashtags ?? [],
        ctaStyle: k.ctaStyle ?? undefined,
        voiceNotes: k.voiceNotes ?? undefined,
        visualStyle: k.visualStyle ?? undefined,
        visualColors: k.visualColors ?? undefined,
        imageNotes: k.imageNotes ?? undefined,
      },
    });
    resetDestForm();
    setDrawerOpen(true);
    try {
      await loadTargets(row.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được nơi đăng'));
    }
  };

  const fillDestFromRow = (row: DestRow) => {
    setSecretConfigured(row.secretConfigured);
    if (row.group === 'site') {
      const s = row.raw as ContentSiteTarget;
      const cfg = parseConfigObj(s.configJson);
      destForm.setFieldsValue({
        kind: `site:${s.connectorType}`,
        code: s.code,
        name: s.name,
        baseUrl: s.baseUrl ?? undefined,
        externalId: undefined,
        secretRef: s.secretRef ?? undefined,
        secret: '',
        configJson: s.configJson || '{}',
        wpUsername: typeof cfg.username === 'string' ? cfg.username : undefined,
        wpStatus: typeof cfg.status === 'string' ? cfg.status : 'draft',
        wpCategories: typeof cfg.wpCategories === 'string' ? cfg.wpCategories : undefined,
        gitOwner: typeof cfg.owner === 'string' ? cfg.owner : undefined,
        gitRepo: typeof cfg.repo === 'string' ? cfg.repo : undefined,
        gitBranch: typeof cfg.branch === 'string' ? cfg.branch : 'main',
        gitContentPath: typeof cfg.contentPath === 'string' ? cfg.contentPath : 'src/content/blog',
        gitImagePath: typeof cfg.imagePath === 'string' ? cfg.imagePath : undefined,
        insightLocale: typeof cfg.locale === 'string' ? cfg.locale : 'vi',
        insightCategory: typeof cfg.insightCategory === 'string' ? cfg.insightCategory : undefined,
        blogCategory: typeof cfg.blogCategory === 'string' ? cfg.blogCategory : undefined,
        insightSection: typeof cfg.insightSection === 'string' ? cfg.insightSection : undefined,
      });
    } else {
      const c = row.raw as ContentChannelTarget;
      destForm.setFieldsValue({
        kind: `channel:${c.channelType}`,
        code: c.code,
        name: c.name,
        baseUrl: undefined,
        externalId: c.externalId ?? undefined,
        secretRef: c.secretRef ?? undefined,
        secret: '',
        configJson: c.configJson || '{}',
      });
    }
  };

  const saveBrand = async () => {
    try {
      const v = await form.validateFields();
      const positioning = String(v.knowledge?.positioning ?? '').trim();
      const brief = String(v.operationalBrief ?? '').trim();
      if (brief.length < 40 && positioning.length < 20) {
        message.error('Cần Brief (≥40 ký tự) hoặc Positioning (≥20 ký tự) trong Brand Knowledge.');
        return;
      }
      const payload = {
        code: v.code,
        name: v.name,
        defaultCtaUrl: v.defaultCtaUrl,
        defaultCtaLabel: v.defaultCtaLabel,
        monthlyCeilingUsd: v.monthlyCeilingUsd,
        imageTier: v.imageTier,
        pauseWhenExceeded: v.pauseWhenExceeded,
        isActive: v.isActive,
        sortOrder: v.sortOrder,
        operationalBrief: v.operationalBrief,
        knowledge: {
          positioning: v.knowledge?.positioning ?? null,
          audience: v.knowledge?.audience ?? null,
          tone: v.knowledge?.tone ?? [],
          forbiddenTopics: v.knowledge?.forbiddenTopics ?? [],
          preferredTerms: v.knowledge?.preferredTerms ?? [],
          avoidTerms: v.knowledge?.avoidTerms ?? [],
          hashtags: v.knowledge?.hashtags ?? [],
          ctaStyle: v.knowledge?.ctaStyle ?? null,
          voiceNotes: v.knowledge?.voiceNotes ?? null,
          visualStyle: v.knowledge?.visualStyle ?? null,
          visualColors: v.knowledge?.visualColors ?? null,
          imageNotes: v.knowledge?.imageNotes ?? null,
        },
      };
      if (editing) {
        const updated = await updateContentBrand(editing.id, payload);
        setEditing(updated);
        message.success('Đã lưu thương hiệu');
        await load();
      } else {
        const created = await createContentBrand(payload);
        message.success('Đã tạo thương hiệu — thêm nơi đăng bên dưới');
        await load();
        setEditing(created);
        setDrawerTab('targets');
        resetDestForm();
        await loadTargets(created.id);
      }
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được thương hiệu'));
    }
  };

  const saveDestination = async () => {
    if (!editing) return;
    try {
      const v = await destForm.validateFields();
      const { group, type } = parseKind(v.kind);
      const configJson = buildConfigJson(v.kind, v);
      if (v.kind === 'site:astro_git') {
        const cfg = parseConfigObj(configJson);
        if (!cfg.owner || !cfg.repo) {
          message.error('Astro/Git: phải điền GitHub owner và Tên repo trước khi Lưu.');
          return;
        }
      }
      const pasted = typeof v.secret === 'string' ? v.secret.trim() : '';
      const needsSecretNow =
        v.kind === 'site:wordpress_rest' ||
        v.kind === 'site:astro_git' ||
        v.kind === 'channel:facebook_page';
      if (needsSecretNow && !pasted && !secretConfigured && !v.secretRef?.trim()) {
        message.warning('Dán token vào ô bên dưới (hoặc chọn biến env) trước khi lưu.');
        return;
      }
      setSavingDest(true);
      const secretPayload = pasted ? { secret: pasted } : {};
      if (group === 'site') {
        const saved = await upsertContentSite(editing.id, {
          code: v.code.trim(),
          name: v.name.trim(),
          connectorType: type,
          baseUrl: v.baseUrl?.trim() || undefined,
          secretRef: v.secretRef?.trim() || null,
          configJson,
          isActive: true,
          ...secretPayload,
        });
        setSecretConfigured(saved.secretConfigured);
        if (type === 'astro_git') {
          message.success(`Đã lưu Astro/Git — owner=${parseConfigObj(saved.configJson).owner ?? '?'} / repo=${parseConfigObj(saved.configJson).repo ?? '?'}`);
        } else {
          message.success('Đã lưu nơi đăng');
        }
      } else {
        const saved = await upsertContentChannel(editing.id, {
          code: v.code.trim(),
          name: v.name.trim(),
          channelType: type,
          externalId: v.externalId?.trim() || undefined,
          secretRef: v.secretRef?.trim() || null,
          configJson,
          isActive: true,
          ...secretPayload,
        });
        setSecretConfigured(saved.secretConfigured);
        message.success('Đã lưu nơi đăng');
      }
      destForm.setFieldValue('secret', '');
      await loadTargets(editing.id);
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được nơi đăng'));
    } finally {
      setSavingDest(false);
    }
  };

  const isSiteKind = !destKind || destKind.startsWith('site:');
  const secretUi = secretHint(destKind);
  const showSecret = destKind !== 'site:manual';
  const showWpFields = destKind === 'site:wordpress_rest';
  const showAstroFields = destKind === 'site:astro_git';

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16, gap: 12 }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Thương hiệu & nơi đăng
          </Typography.Title>
          <Typography.Text type="secondary">
            Bước 1 — thương hiệu + Brief, rồi danh sách nơi đăng (web, fanpage, LinkedIn, Zalo…).
          </Typography.Text>
        </div>
        <Space wrap>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm thương hiệu
          </Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Bấm «Nơi đăng» trên từng thương hiệu để thêm website / mạng xã hội. «Kho ảnh» gắn thư mục máy riêng từng brand (không trùng Novixa/Famixa…)."
        description="Chọn loại trong combobox khi thêm nơi đăng. Chưa có connector tự động thì vẫn lưu được và xuất bản thủ công (chép bài)."
      />

      <Table
        rowKey="id"
        loading={loading}
        dataSource={brands}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 110 },
          { title: 'Thương hiệu', dataIndex: 'name' },
          {
            title: 'Knowledge',
            key: 'brief',
            width: 90,
            render: (_, row) =>
              row.operationalBrief?.trim() || row.knowledge?.positioning?.trim() ? (
                <Tag color="success">OK</Tag>
              ) : (
                <Tag color="error">Thiếu</Tag>
              ),
          },
          {
            title: 'Nơi đăng',
            key: 'dest',
            width: 100,
            render: (_, row) => {
              const n = (targetCounts[row.id]?.sites ?? 0) + (targetCounts[row.id]?.channels ?? 0);
              return n > 0 ? <Tag color="blue">{n} chỗ</Tag> : <Tag>Chưa có</Tag>;
            },
          },
          {
            title: 'Kho ảnh máy',
            key: 'imageLib',
            width: 220,
            render: (_, row) => {
              const folder = imageFolderNames[row.id];
              if (!isLocalImageLibrarySupported()) {
                return <Typography.Text type="secondary">Cần Chrome/Edge</Typography.Text>;
              }
              return (
                <Space direction="vertical" size={2}>
                  {folder ? (
                    <Tag color="success" icon={<FolderOpenOutlined />}>
                      {folder}
                    </Tag>
                  ) : (
                    <Tag>Chưa chọn</Tag>
                  )}
                  <Space size={0}>
                    <Button
                      type="link"
                      size="small"
                      onClick={() => void onPickBrandImageFolder(row)}
                    >
                      {folder ? 'Đổi thư mục' : 'Chọn thư mục'}
                    </Button>
                    {folder ? (
                      <Button type="link" size="small" danger onClick={() => void onClearBrandImageFolder(row)}>
                        Bỏ
                      </Button>
                    ) : null}
                  </Space>
                </Space>
              );
            },
          },
          {
            title: 'Đang dùng',
            dataIndex: 'isActive',
            width: 90,
            render: (v: boolean) => (v ? 'Có' : 'Không'),
          },
          {
            title: '',
            key: 'actions',
            width: 180,
            render: (_, row) => (
              <Space>
                <Button type="link" onClick={() => void openEdit(row, 'info')}>
                  Sửa
                </Button>
                <Button type="link" onClick={() => void openEdit(row, 'targets')}>
                  Nơi đăng
                </Button>
              </Space>
            ),
          },
        ]}
      />

      <Drawer
        title={
          <Space>
            <BankOutlined style={{ color: '#1677ff' }} />
            <span>{editing ? `Thương hiệu: ${editing.name}` : 'Thêm thương hiệu'}</span>
          </Space>
        }
        width={1080}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        styles={{ body: { paddingTop: 12, background: '#f8fafc' } }}
        extra={
          drawerTab === 'info' ? (
            <Button type="primary" icon={<SaveOutlined />} onClick={() => void saveBrand()}>
              {editing ? 'Lưu thương hiệu' : 'Tạo & tiếp tục nơi đăng'}
            </Button>
          ) : null
        }
      >
        <Tabs
          activeKey={drawerTab}
          onChange={(k) => {
            if (k === 'targets' && !editing) {
              message.warning('Lưu thương hiệu trước, rồi mới thêm nơi đăng.');
              return;
            }
            setDrawerTab(k);
          }}
          items={[
            {
              key: 'info',
              label: (
                <Space size={6}>
                  <BookOutlined />
                  Thông tin & Brand Knowledge
                </Space>
              ),
              children: (
                <Form form={form} layout="vertical" requiredMark="optional" style={{ maxWidth: 920 }}>
                  <Card
                    size="small"
                    title={
                      <Space>
                        <BankOutlined />
                        <span>Định danh</span>
                      </Space>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Row gutter={[16, 0]}>
                      <Col xs={24} sm={8}>
                        <Form.Item
                          name="code"
                          label={
                            <Space size={4}>
                              <NumberOutlined />
                              Mã ngắn
                            </Space>
                          }
                          rules={[{ required: true, message: 'Bắt buộc' }]}
                          extra="Không đổi sau khi tạo"
                        >
                          <Input disabled={!!editing} placeholder="novixa" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} sm={16}>
                        <Form.Item
                          name="name"
                          label={
                            <Space size={4}>
                              <FontColorsOutlined />
                              Tên hiển thị
                            </Space>
                          }
                          rules={[{ required: true, message: 'Bắt buộc' }]}
                        >
                          <Input placeholder="Novixa Healthcare Platform" />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Row gutter={16}>
                      <Col xs={24} sm={8}>
                        <Form.Item name="isActive" label="Đang dùng" valuePropName="checked">
                          <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} sm={8}>
                        <Form.Item name="sortOrder" label="Thứ tự hiển thị">
                          <InputNumber style={{ width: '100%' }} placeholder="0" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} sm={8}>
                        <Form.Item
                          name="pauseWhenExceeded"
                          label="Dừng gen khi hết ngân sách"
                          valuePropName="checked"
                        >
                          <Switch />
                        </Form.Item>
                      </Col>
                    </Row>
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <FolderOpenOutlined style={{ color: '#1677ff' }} />
                        <span>Kho ảnh máy (theo thương hiệu)</span>
                      </Space>
                    }
                    extra={
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        Mỗi brand một thư mục — tránh trùng ảnh Novixa/Famixa…
                      </Typography.Text>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    {!isLocalImageLibrarySupported() ? (
                      <Alert type="warning" showIcon message="Cần Chrome / Edge để chọn thư mục ảnh trên máy." />
                    ) : !editing ? (
                      <Typography.Text type="secondary">
                        Lưu thương hiệu trước, rồi chọn thư mục ảnh tại đây (hoặc cột «Kho ảnh máy» trên bảng).
                      </Typography.Text>
                    ) : (
                      <Space wrap align="center">
                        {imageFolderNames[editing.id] ? (
                          <Tag color="success" icon={<FolderOpenOutlined />}>
                            {imageFolderNames[editing.id]}
                          </Tag>
                        ) : (
                          <Tag>Chưa chọn thư mục</Tag>
                        )}
                        <Button
                          type="primary"
                          ghost
                          icon={<PictureOutlined />}
                          onClick={() => void onPickBrandImageFolder(editing)}
                        >
                          {imageFolderNames[editing.id] ? 'Đổi thư mục ảnh' : 'Chọn thư mục ảnh'}
                        </Button>
                        {imageFolderNames[editing.id] ? (
                          <Button danger type="link" onClick={() => void onClearBrandImageFolder(editing)}>
                            Bỏ liên kết
                          </Button>
                        ) : null}
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Trình duyệt nhớ thư mục trên máy bạn — không upload lên server.
                        </Typography.Text>
                      </Space>
                    )}
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <ThunderboltOutlined style={{ color: '#fa8c16' }} />
                        <span>Brand Knowledge · Giọng AI</span>
                      </Space>
                    }
                    extra={
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        Mỗi brand một giọng — tránh viết giống nhau
                      </Typography.Text>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Form.Item
                      name={['knowledge', 'positioning']}
                      label={
                        <Space size={4}>
                          <AimOutlined />
                          Positioning
                        </Space>
                      }
                      extra="Bắt buộc nếu chưa có Brief (≥20 ký tự)"
                    >
                      <Input.TextArea
                        rows={3}
                        placeholder="Nền tảng quản trị nhà thuốc hiện đại — giúp chủ nhà thuốc kiểm soát dòng tiền, tồn kho và vận hành…"
                      />
                    </Form.Item>
                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item
                          name={['knowledge', 'audience']}
                          label={
                            <Space size={4}>
                              <TeamOutlined />
                              Đối tượng
                            </Space>
                          }
                        >
                          <Input placeholder="Chủ nhà thuốc độc lập, chuỗi nhỏ…" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item
                          name={['knowledge', 'tone']}
                          label={
                            <Space size={4}>
                              <TagsOutlined />
                              Tone (thẻ)
                            </Space>
                          }
                        >
                          <Select
                            mode="tags"
                            placeholder="chuyên nghiệp, thực tế, gần gũi…"
                            tokenSeparators={[',']}
                          />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Form.Item
                      name={['knowledge', 'forbiddenTopics']}
                      label={
                        <Space size={4}>
                          <StopOutlined />
                          Chủ đề cấm
                        </Space>
                      }
                    >
                      <Select
                        mode="tags"
                        placeholder="cam kết chữa bệnh, so sánh giá đối thủ…"
                        tokenSeparators={[',']}
                      />
                    </Form.Item>
                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item name={['knowledge', 'preferredTerms']} label="Ưu tiên dùng từ">
                          <Select mode="tags" placeholder="nền tảng, module, dòng tiền…" tokenSeparators={[',']} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name={['knowledge', 'avoidTerms']} label="Tránh dùng từ">
                          <Select mode="tags" placeholder="siêu rẻ, #1 thị trường…" tokenSeparators={[',']} />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item name={['knowledge', 'hashtags']} label="Hashtag gợi ý">
                          <Select mode="tags" placeholder="#Novixa #NhaThuoc…" tokenSeparators={[',', ' ']} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name={['knowledge', 'ctaStyle']} label="Phong cách CTA">
                          <Input placeholder="Nhẹ nhàng · mời dùng thử · liên hệ tư vấn…" />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Form.Item name={['knowledge', 'voiceNotes']} label="Ghi chú giọng viết">
                      <Input.TextArea
                        rows={2}
                        placeholder="Không phô trương; lấy ví dụ thực tế từ vận hành nhà thuốc…"
                      />
                    </Form.Item>
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <PictureOutlined style={{ color: '#722ed1' }} />
                        <span>Visual & ảnh</span>
                      </Space>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item
                          name={['knowledge', 'visualStyle']}
                          label={
                            <Space size={4}>
                              <BgColorsOutlined />
                              Visual style
                            </Space>
                          }
                        >
                          <Input placeholder="Modern healthcare, sạch sẽ, tin cậy…" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name={['knowledge', 'visualColors']} label="Màu chủ đạo">
                          <Input placeholder="Trắng + xanh dương / xanh lá…" />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Form.Item name={['knowledge', 'imageNotes']} label="Ghi chú ảnh">
                      <Input placeholder="Brand-safe; tránh chữ chồng lên ảnh khi đăng MXH…" />
                    </Form.Item>
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <BookOutlined />
                        <span>Brief vận hành</span>
                      </Space>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Form.Item
                      name="operationalBrief"
                      label="Nội dung brief (tuỳ chọn nếu đã có Positioning)"
                      extra="Có thể dán brief dài từ ChatGPT/SoT. Cần Brief ≥40 ký tự hoặc Positioning ≥20."
                    >
                      <Input.TextArea
                        rows={6}
                        placeholder="Dán nội dung tổng hợp yêu cầu / chiến lược nội dung…"
                        showCount
                      />
                    </Form.Item>
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <SendOutlined style={{ color: '#1677ff' }} />
                        <span>CTA & ngân sách</span>
                      </Space>
                    }
                  >
                    <Row gutter={16}>
                      <Col xs={24} md={14}>
                        <Form.Item
                          name="defaultCtaUrl"
                          label={
                            <Space size={4}>
                              <LinkOutlined />
                              Link CTA mặc định
                            </Space>
                          }
                        >
                          <Input placeholder="https://novixa.vn/…" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={10}>
                        <Form.Item name="defaultCtaLabel" label="Chữ trên nút CTA">
                          <Input placeholder="Tìm hiểu thêm" />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Divider style={{ margin: '4px 0 16px' }} />
                    <Row gutter={16}>
                      <Col xs={24} sm={12}>
                        <Form.Item
                          name="monthlyCeilingUsd"
                          label={
                            <Space size={4}>
                              <DollarOutlined />
                              Trần chi phí riêng (USD/tháng)
                            </Space>
                          }
                        >
                          <InputNumber min={0} style={{ width: '100%' }} placeholder="Theo mức chung" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} sm={12}>
                        <Form.Item name="imageTier" label="Chất lượng ảnh riêng">
                          <Select
                            allowClear
                            placeholder="Theo mặc định"
                            options={[
                              { value: 'lean', label: 'Tiết kiệm' },
                              { value: 'balanced', label: 'Cân bằng' },
                              { value: 'premium', label: 'Cao cấp' },
                            ]}
                          />
                        </Form.Item>
                      </Col>
                    </Row>
                  </Card>
                </Form>
              ),
            },
            {
              key: 'targets',
              label: editing ? (
                <Space size={6}>
                  <SendOutlined />
                  Nơi đăng ({destRows.length})
                </Space>
              ) : (
                <Space size={6}>
                  <SendOutlined />
                  Nơi đăng
                </Space>
              ),
              disabled: !editing,
              children: editing ? (
                <div style={{ maxWidth: 920 }}>
                  <Card
                    size="small"
                    title={
                      <Space>
                        <UnorderedListOutlined />
                        <span>Danh sách nơi đăng</span>
                      </Space>
                    }
                    extra={
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        Bấm một dòng để sửa
                      </Typography.Text>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Table
                      size="small"
                      rowKey="key"
                      pagination={false}
                      dataSource={destRows}
                      locale={{ emptyText: 'Chưa có nơi đăng — thêm bằng form bên dưới' }}
                      onRow={(row) => ({
                        onClick: () => fillDestFromRow(row),
                        style: { cursor: 'pointer' },
                      })}
                      columns={[
                        { title: 'Mã', dataIndex: 'code', width: 100 },
                        { title: 'Tên', dataIndex: 'name' },
                        {
                          title: 'Loại',
                          dataIndex: 'kindLabel',
                          render: (v: string, row) => (
                            <Tag color={row.group === 'site' ? 'blue' : 'purple'}>{v}</Tag>
                          ),
                        },
                        {
                          title: 'URL / Page ID',
                          dataIndex: 'address',
                          ellipsis: true,
                        },
                        {
                          title: 'Token',
                          key: 'token',
                          width: 100,
                          render: (_, row) =>
                            row.secretConfigured ? (
                              <Tag color="success">Đã có</Tag>
                            ) : (
                              <Tag>Chưa có</Tag>
                            ),
                        },
                        {
                          title: 'Cấu hình',
                          key: 'cfg',
                          width: 120,
                          render: (_, row) =>
                            row.configWarning ? (
                              <Tag color="error">{row.configWarning}</Tag>
                            ) : (
                              <Tag color="success">OK</Tag>
                            ),
                        },
                      ]}
                    />
                  </Card>

                  <Card
                    size="small"
                    title={
                      <Space>
                        <EditOutlined />
                        <span>Thêm / cập nhật nơi đăng</span>
                      </Space>
                    }
                    style={{ marginBottom: 16 }}
                  >
                    <Form
                      form={destForm}
                      layout="vertical"
                      requiredMark="optional"
                      initialValues={{
                        kind: 'site:manual',
                        configJson: '{}',
                        wpStatus: 'draft',
                        gitBranch: 'main',
                        gitContentPath: 'novixa-site/src/content/tin-tuc',
                        secret: '',
                      }}
                    >
                      <Form.Item
                        name="kind"
                        label={
                          <Space size={4}>
                            <SendOutlined />
                            Loại nơi đăng
                          </Space>
                        }
                        rules={[{ required: true, message: 'Chọn loại' }]}
                        extra="Website hoặc mạng xã hội"
                      >
                        <Select
                          showSearch
                          optionFilterProp="label"
                          options={DEST_KIND_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                          placeholder="Chọn loại…"
                          size="large"
                        />
                      </Form.Item>

                      <Row gutter={16}>
                        <Col xs={24} sm={8}>
                          <Form.Item
                            name="code"
                            label={
                              <Space size={4}>
                                <NumberOutlined />
                                Mã ngắn
                              </Space>
                            }
                            rules={[{ required: true, message: 'Bắt buộc' }]}
                            extra="vd. blog, fb-main"
                          >
                            <Input placeholder="fb-main" />
                          </Form.Item>
                        </Col>
                        <Col xs={24} sm={8}>
                          <Form.Item
                            name="name"
                            label={
                              <Space size={4}>
                                <FontColorsOutlined />
                                Tên gọi
                              </Space>
                            }
                            rules={[{ required: true, message: 'Bắt buộc' }]}
                          >
                            <Input placeholder="Fanpage Novixa" />
                          </Form.Item>
                        </Col>
                        <Col xs={24} sm={8}>
                          {isSiteKind ? (
                            <Form.Item
                              name="baseUrl"
                              label={
                                <Space size={4}>
                                  <GlobalOutlined />
                                  Địa chỉ web
                                </Space>
                              }
                            >
                              <Input placeholder="https://novixa.vn/" />
                            </Form.Item>
                          ) : (
                            <Form.Item
                              name="externalId"
                              label={
                                <Space size={4}>
                                  <ApiOutlined />
                                  Page / Channel ID
                                </Space>
                              }
                              extra="Facebook Page ID…"
                            >
                              <Input placeholder="123456789" />
                            </Form.Item>
                          )}
                        </Col>
                      </Row>

                      {showWpFields ? (
                        <Card
                          type="inner"
                          size="small"
                          title={
                            <Space>
                              <GlobalOutlined />
                              WordPress
                            </Space>
                          }
                          style={{ marginBottom: 16 }}
                        >
                          <Row gutter={16}>
                            <Col xs={24} sm={12}>
                              <Form.Item
                                name="wpUsername"
                                label="Tài khoản WordPress"
                                rules={[{ required: true, message: 'Nhập username WP' }]}
                              >
                                <Input placeholder="admin" />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={12}>
                              <Form.Item name="wpStatus" label="Trạng thái bài đăng">
                                <Select
                                  options={[
                                    { value: 'draft', label: 'Nháp' },
                                    { value: 'publish', label: 'Xuất bản ngay' },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                          </Row>
                          <Form.Item
                            name="wpCategories"
                            label="Chuyên mục (slug)"
                            extra="vandinhtra.vn: journal (bắt buộc để hiện Journal), thêm cau-chuyen, kien-thuc-tra… Cách nhau bằng dấu phẩy. Để trống = tự chọn."
                          >
                            <Input placeholder="journal,cau-chuyen" />
                          </Form.Item>
                        </Card>
                      ) : null}

                      {showAstroFields ? (
                        <Card
                          type="inner"
                          size="small"
                          title={
                            <Space>
                              <GithubOutlined />
                              Astro / GitHub
                            </Space>
                          }
                          style={{ marginBottom: 16 }}
                        >
                          <Alert
                            type="info"
                            showIcon
                            style={{ marginBottom: 12 }}
                            message="Ba website Git"
                            description={
                              <ul style={{ margin: '4px 0 0', paddingLeft: 18 }}>
                                <li>
                                  <b>novixa.vn</b> — repo <code>KitPlatform</code>, path{' '}
                                  <code>novixa-site/src/content/tin-tuc</code>
                                </li>
                                <li>
                                  <b>famixa.vn</b> — repo <code>KitPlatform</code>, path{' '}
                                  <code>famixa-site/content/blog</code> → URL{' '}
                                  <code>/vi/goi-cha-me/…</code>
                                </li>
                                <li>
                                  <b>kittech.vn</b> — repo <code>Kit-Technology</code>, path{' '}
                                  <code>src/content/insights</code> → URL <code>/vi/blog/…</code>
                                </li>
                              </ul>
                            }
                          />
                          <Row gutter={16}>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="gitOwner"
                                label="GitHub owner"
                                rules={[{ required: true, message: 'Bắt buộc' }]}
                                extra="vd. khiemtic-rgb"
                              >
                                <Input placeholder="khiemtic-rgb" />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="gitRepo"
                                label="Tên repo"
                                rules={[{ required: true, message: 'Bắt buộc' }]}
                              >
                                <Input placeholder="Kit-Technology" />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={8}>
                              <Form.Item name="gitBranch" label="Branch">
                                <Input placeholder="main" />
                              </Form.Item>
                            </Col>
                          </Row>
                          <Form.Item
                            name="gitContentPath"
                            label="Thư mục bài viết"
                            extra="Novixa: novixa-site/src/content/tin-tuc · Famixa: famixa-site/content/blog · Kittech: src/content/insights"
                          >
                            <Input placeholder="famixa-site/content/blog" />
                          </Form.Item>
                          <Form.Item
                            name="gitImagePath"
                            label="Thư mục ảnh (tùy chọn)"
                            extra="Famixa: famixa-site/public/images/blog · Kittech: public/images/insights · Novixa: novixa-site/public/images/tin-tuc"
                          >
                            <Input placeholder="famixa-site/public/images/blog" />
                          </Form.Item>
                          <Row gutter={16}>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="insightLocale"
                                label="Ngôn ngữ (kittech)"
                                extra="vi → /vi/blog/… · en → /en/insights/…"
                              >
                                <Select
                                  allowClear
                                  options={[
                                    { value: 'vi', label: 'vi (tiếng Việt)' },
                                    { value: 'en', label: 'en (English)' },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="blogCategory"
                                label="Category mặc định (famixa)"
                                extra="nuoi-day / routine / man-hinh / tu-giac / famixa — để trống = tự chọn"
                              >
                                <Select
                                  allowClear
                                  options={[
                                    { value: 'nuoi-day', label: 'nuoi-day · Nuôi dạy' },
                                    { value: 'routine', label: 'routine · Nhịp sinh hoạt' },
                                    { value: 'man-hinh', label: 'man-hinh · Màn hình' },
                                    { value: 'tu-giac', label: 'tu-giac · Tự giác' },
                                    { value: 'famixa', label: 'famixa · Dùng app' },
                                  ]}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="insightCategory"
                                label="Category mặc định (kittech)"
                                extra="Để trống = tự chọn theo tiêu đề. Chỉ dùng khi không khớp từ khóa."
                              >
                                <Select
                                  allowClear
                                  showSearch
                                  options={[
                                    'ai',
                                    'healthcare',
                                    'digital-transformation',
                                    'engineering',
                                    'company-news',
                                    'business',
                                    'technology',
                                    'solutions',
                                    'products',
                                    'faq',
                                  ].map((v) => ({ value: v, label: v }))}
                                />
                              </Form.Item>
                            </Col>
                            <Col xs={24} sm={8}>
                              <Form.Item
                                name="insightSection"
                                label="Section (kittech)"
                                extra="insights / technology / company…"
                              >
                                <Select
                                  allowClear
                                  options={[
                                    'insights',
                                    'technology',
                                    'solutions',
                                    'products',
                                    'company',
                                    'faq',
                                  ].map((v) => ({ value: v, label: v }))}
                                />
                              </Form.Item>
                            </Col>
                          </Row>
                        </Card>
                      ) : null}

                      {showSecret ? (
                        <Card
                          type="inner"
                          size="small"
                          title={
                            <Space>
                              <KeyOutlined />
                              {secretUi.pasteLabel}
                            </Space>
                          }
                          style={{ marginBottom: 16 }}
                          extra={
                            secretConfigured ? <Tag color="success">Token đã lưu</Tag> : <Tag>Chưa có token</Tag>
                          }
                        >
                          <Form.Item
                            name="secret"
                            label="Dán token"
                            extra={
                              secretConfigured
                                ? `${secretUi.extra} · Để trống nếu giữ token cũ.`
                                : secretUi.extra
                            }
                          >
                            <Input.Password
                              placeholder={secretUi.placeholder}
                              autoComplete="new-password"
                              size="large"
                            />
                          </Form.Item>
                          {destKind === 'channel:facebook_page' ? (
                            <Collapse
                              ghost
                              items={[
                                {
                                  key: 'fb-token-help',
                                  label: 'Hướng dẫn lấy Page Access Token (Facebook)',
                                  children: (
                                    <ol style={{ margin: 0, paddingLeft: 18, lineHeight: 1.7 }}>
                                      <li>
                                        Vào{' '}
                                        <a href="https://developers.facebook.com/" target="_blank" rel="noreferrer">
                                          developers.facebook.com
                                        </a>{' '}
                                        → App → Graph API Explorer.
                                      </li>
                                      <li>
                                        Generate token với <code>pages_show_list</code>,{' '}
                                        <code>pages_read_engagement</code>, <code>pages_manage_posts</code>.
                                      </li>
                                      <li>
                                        Gọi <code>GET /me/accounts</code> → lấy <code>id</code> và{' '}
                                        <code>access_token</code> của Page.
                                      </li>
                                      <li>Dán token + Page ID → Lưu vào danh sách.</li>
                                    </ol>
                                  ),
                                },
                              ]}
                            />
                          ) : null}
                          <Collapse
                            ghost
                            items={[
                              {
                                key: 'env-ref',
                                label: 'Tuỳ chọn: biến môi trường thay vì dán token',
                                children: (
                                  <Form.Item
                                    name="secretRef"
                                    label={secretUi.label}
                                    extra="Chỉ tên biến (vd FB_KIT_PAGE_TOKEN) nếu đã set env trên máy API."
                                  >
                                    <Input placeholder="FB_KIT_PAGE_TOKEN" />
                                  </Form.Item>
                                ),
                              },
                            ]}
                          />
                        </Card>
                      ) : (
                        <Alert
                          type="info"
                          showIcon
                          style={{ marginBottom: 16 }}
                          message="Đăng thủ công không cần token"
                          description="Khi xuất bản, hệ thống cho chép bài / tải file — không gọi API bên ngoài."
                        />
                      )}

                      <Collapse
                        ghost
                        style={{ marginBottom: 12 }}
                        items={[
                          {
                            key: 'adv',
                            label: (
                              <Space size={4}>
                                <CodeOutlined />
                                Tuỳ chọn nâng cao (JSON phụ)
                              </Space>
                            ),
                            children: (
                              <Form.Item
                                name="configJson"
                                label="JSON cấu hình thêm"
                                extra="WordPress/Astro đã có ô riêng — hệ thống tự ghép vào JSON này."
                              >
                                <Input.TextArea rows={3} placeholder="{}" style={{ fontFamily: 'monospace' }} />
                              </Form.Item>
                            ),
                          },
                        ]}
                      />

                      <Space size="middle">
                        <Button
                          type="primary"
                          icon={<SaveOutlined />}
                          loading={savingDest}
                          onClick={() => void saveDestination()}
                        >
                          Lưu vào danh sách
                        </Button>
                        <Button onClick={() => resetDestForm((destKind as DestKind) || 'site:manual')}>
                          Xoá form
                        </Button>
                      </Space>
                    </Form>
                  </Card>

                  <Alert
                    type="warning"
                    showIcon
                    message="Đăng tự động hiện hỗ trợ: WordPress, Astro/Git, Facebook Page, và Thủ công."
                    description="Instagram / LinkedIn / Zalo / TikTok… vẫn thêm vào danh sách; chưa có connector thì xuất thủ công (chép bài)."
                  />
                </div>
              ) : (
                <Alert type="warning" showIcon message="Lưu thương hiệu ở tab 1 trước." />
              ),
            },
          ]}
        />
      </Drawer>
    </div>
  );
}
