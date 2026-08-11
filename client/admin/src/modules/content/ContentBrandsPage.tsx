import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Collapse,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
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
  }
  if (kind === 'site:astro_git') {
    if (typeof v.gitOwner === 'string' && v.gitOwner.trim()) base.owner = v.gitOwner.trim();
    if (typeof v.gitRepo === 'string' && v.gitRepo.trim()) base.repo = v.gitRepo.trim();
    if (typeof v.gitBranch === 'string' && v.gitBranch.trim()) base.branch = v.gitBranch.trim();
    if (typeof v.gitContentPath === 'string' && v.gitContentPath.trim()) {
      base.contentPath = v.gitContentPath.trim();
    }
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

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const b = await fetchContentBrands();
      setBrands(b);
      void loadTargetCounts(b);
      // warm settings (connector lists live in org; kinds are fixed in UI for flexibility)
      void fetchContentSettings().catch(() => undefined);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được thương hiệu'));
    } finally {
      setLoading(false);
    }
  }, [loadTargetCounts, message]);

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
    const siteRows: DestRow[] = sites.map((s) => ({
      key: `site:${s.id}`,
      group: 'site',
      code: s.code,
      name: s.name,
      kindLabel: siteKind(s.connectorType),
      kindValue: `site:${s.connectorType}`,
      address: s.baseUrl?.trim() || '—',
      secretRef: s.secretRef,
      secretConfigured: s.secretConfigured,
      raw: s,
    }));
    const channelRows: DestRow[] = channels.map((c) => ({
      key: `channel:${c.id}`,
      group: 'channel',
      code: c.code,
      name: c.name,
      kindLabel: channelKind(c.channelType),
      kindValue: `channel:${c.channelType}`,
      address: c.externalId?.trim() || '—',
      secretRef: c.secretRef,
      secretConfigured: c.secretConfigured,
      raw: c,
    }));
    return [...siteRows, ...channelRows].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }, [sites, channels]);

  const openCreate = () => {
    setEditing(null);
    setDrawerTab('info');
    form.resetFields();
    form.setFieldsValue({ pauseWhenExceeded: true, isActive: true, sortOrder: 100 });
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
        gitOwner: typeof cfg.owner === 'string' ? cfg.owner : undefined,
        gitRepo: typeof cfg.repo === 'string' ? cfg.repo : undefined,
        gitBranch: typeof cfg.branch === 'string' ? cfg.branch : 'main',
        gitContentPath: typeof cfg.contentPath === 'string' ? cfg.contentPath : 'src/content/blog',
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
      if (editing) {
        const updated = await updateContentBrand(editing.id, v);
        setEditing(updated);
        message.success('Đã lưu thương hiệu');
        await load();
      } else {
        const created = await createContentBrand(v);
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
      }
      message.success('Đã lưu nơi đăng');
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
        message="Bấm «Nơi đăng» trên từng thương hiệu để thêm website / mạng xã hội trong một danh sách."
        description="Chọn loại trong combobox khi thêm. Chưa có connector tự động thì vẫn lưu được và xuất bản thủ công (chép bài)."
      />

      <Table
        rowKey="id"
        loading={loading}
        dataSource={brands}
        columns={[
          { title: 'Mã', dataIndex: 'code', width: 110 },
          { title: 'Thương hiệu', dataIndex: 'name' },
          {
            title: 'Brief',
            key: 'brief',
            width: 90,
            render: (_, row) =>
              row.operationalBrief?.trim() ? <Tag color="success">Đã có</Tag> : <Tag color="error">Thiếu</Tag>,
          },
          {
            title: 'Nơi đăng',
            key: 'dest',
            width: 140,
            render: (_, row) => {
              const n = (targetCounts[row.id]?.sites ?? 0) + (targetCounts[row.id]?.channels ?? 0);
              return n > 0 ? <Tag color="blue">{n} chỗ</Tag> : <Tag>Chưa có</Tag>;
            },
          },
          {
            title: 'Đang dùng',
            dataIndex: 'isActive',
            width: 100,
            render: (v: boolean) => (v ? 'Có' : 'Không'),
          },
          {
            title: '',
            key: 'actions',
            width: 220,
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
        title={editing ? `Thương hiệu: ${editing.name}` : 'Thêm thương hiệu'}
        width={760}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          drawerTab === 'info' ? (
            <Button type="primary" onClick={() => void saveBrand()}>
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
              label: '1. Thông tin & Brief',
              children: (
                <Form form={form} layout="vertical">
                  <Form.Item
                    name="code"
                    label="Mã ngắn"
                    rules={[{ required: true }]}
                    extra="Ví dụ: kit — không đổi sau khi tạo."
                  >
                    <Input disabled={!!editing} placeholder="kit" />
                  </Form.Item>
                  <Form.Item name="name" label="Tên hiển thị" rules={[{ required: true }]}>
                    <Input placeholder="KIT Technology" />
                  </Form.Item>
                  <Form.Item
                    name="operationalBrief"
                    label="Brief vận hành (dán từ ChatGPT / SoT)"
                    extra="Bắt buộc trước khi «Nhờ AI»."
                    rules={[
                      {
                        validator: async (_, value) => {
                          if (typeof value === 'string' && value.trim().length >= 80) return;
                          throw new Error('Dán brief đủ dài (≥ ~80 ký tự).');
                        },
                      },
                    ]}
                  >
                    <Input.TextArea
                      rows={10}
                      placeholder="Dán nội dung tổng hợp yêu cầu / chiến lược nội dung…"
                      showCount
                    />
                  </Form.Item>
                  <Form.Item name="defaultCtaUrl" label="Link CTA mặc định">
                    <Input placeholder="https://..." />
                  </Form.Item>
                  <Form.Item name="defaultCtaLabel" label="Chữ trên nút CTA">
                    <Input />
                  </Form.Item>
                  <Space size="large" wrap style={{ width: '100%' }}>
                    <Form.Item name="monthlyCeilingUsd" label="Trần chi phí riêng (USD/tháng)">
                      <InputNumber min={0} style={{ width: 200 }} placeholder="Theo mức chung" />
                    </Form.Item>
                    <Form.Item name="imageTier" label="Chất lượng ảnh riêng">
                      <Select
                        allowClear
                        style={{ width: 200 }}
                        placeholder="Theo mặc định"
                        options={[
                          { value: 'lean', label: 'Tiết kiệm' },
                          { value: 'balanced', label: 'Cân bằng' },
                          { value: 'premium', label: 'Cao cấp' },
                        ]}
                      />
                    </Form.Item>
                  </Space>
                  <Space size="large" wrap>
                    <Form.Item name="pauseWhenExceeded" label="Dừng gen khi hết ngân sách" valuePropName="checked">
                      <Switch />
                    </Form.Item>
                    <Form.Item name="isActive" label="Đang dùng" valuePropName="checked">
                      <Switch />
                    </Form.Item>
                    <Form.Item name="sortOrder" label="Thứ tự">
                      <InputNumber style={{ width: 100 }} />
                    </Form.Item>
                  </Space>
                </Form>
              ),
            },
            {
              key: 'targets',
              label: editing ? `2. Nơi đăng (${destRows.length})` : '2. Nơi đăng',
              disabled: !editing,
              children: editing ? (
                <div>
                  <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                    Một danh sách cho mọi chỗ đăng. Bấm dòng để sửa; chọn <strong>Loại</strong> khi thêm mới.
                  </Typography.Paragraph>

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
                        width: 110,
                        render: (_, row) =>
                          row.secretConfigured ? (
                            <Tag color="success">Đã có</Tag>
                          ) : (
                            <Tag>Chưa có</Tag>
                          ),
                      },
                    ]}
                    style={{ marginBottom: 20 }}
                  />

                  <Typography.Title level={5} style={{ marginTop: 0 }}>
                    Thêm / cập nhật nơi đăng
                  </Typography.Title>
                  <Form
                    form={destForm}
                    layout="vertical"
                    initialValues={{
                      kind: 'site:manual',
                      configJson: '{}',
                      wpStatus: 'draft',
                      gitBranch: 'main',
                      gitContentPath: 'src/content/blog',
                      secret: '',
                    }}
                  >
                    <Form.Item
                      name="kind"
                      label="Loại nơi đăng"
                      rules={[{ required: true, message: 'Chọn loại' }]}
                      extra="Web hoặc mạng xã hội — chọn trong danh sách."
                    >
                      <Select
                        showSearch
                        optionFilterProp="label"
                        options={DEST_KIND_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                        placeholder="Chọn loại…"
                      />
                    </Form.Item>
                    <Space wrap style={{ width: '100%' }} align="start">
                      <Form.Item
                        name="code"
                        label="Mã ngắn (nội bộ)"
                        rules={[{ required: true }]}
                        style={{ marginBottom: 8 }}
                        extra="ID duy nhất trong thương hiệu — vd blog, fb-main."
                      >
                        <Input placeholder="blog" style={{ width: 140 }} />
                      </Form.Item>
                      <Form.Item
                        name="name"
                        label="Tên gọi"
                        rules={[{ required: true }]}
                        style={{ marginBottom: 8 }}
                        extra="Tên bạn nhìn thấy khi chọn nơi đăng."
                      >
                        <Input placeholder="Blog KIT" style={{ width: 220 }} />
                      </Form.Item>
                      {isSiteKind ? (
                        <Form.Item name="baseUrl" label="Địa chỉ web" style={{ marginBottom: 8 }}>
                          <Input placeholder="https://..." style={{ width: 260 }} />
                        </Form.Item>
                      ) : (
                        <Form.Item
                          name="externalId"
                          label="Page / Channel ID"
                          style={{ marginBottom: 8 }}
                          extra="ID trang trên nền tảng (Facebook Page ID…)"
                        >
                          <Input placeholder="123456789" style={{ width: 200 }} />
                        </Form.Item>
                      )}
                    </Space>

                    {showWpFields ? (
                      <Space wrap style={{ width: '100%' }} align="start">
                        <Form.Item
                          name="wpUsername"
                          label="Tài khoản WordPress"
                          rules={[{ required: true, message: 'Nhập username WP' }]}
                          style={{ marginBottom: 8 }}
                        >
                          <Input placeholder="editor" style={{ width: 180 }} />
                        </Form.Item>
                        <Form.Item name="wpStatus" label="Trạng thái bài đăng" style={{ marginBottom: 8 }}>
                          <Select
                            style={{ width: 160 }}
                            options={[
                              { value: 'draft', label: 'Nháp' },
                              { value: 'publish', label: 'Xuất bản ngay' },
                            ]}
                          />
                        </Form.Item>
                      </Space>
                    ) : null}

                    {showAstroFields ? (
                      <Space wrap style={{ width: '100%' }} align="start">
                        <Form.Item
                          name="gitOwner"
                          label="GitHub owner"
                          rules={[{ required: true }]}
                          style={{ marginBottom: 8 }}
                        >
                          <Input placeholder="org-or-user" style={{ width: 160 }} />
                        </Form.Item>
                        <Form.Item
                          name="gitRepo"
                          label="Tên repo"
                          rules={[{ required: true }]}
                          style={{ marginBottom: 8 }}
                        >
                          <Input placeholder="kit-site" style={{ width: 180 }} />
                        </Form.Item>
                        <Form.Item name="gitBranch" label="Branch" style={{ marginBottom: 8 }}>
                          <Input placeholder="main" style={{ width: 120 }} />
                        </Form.Item>
                        <Form.Item name="gitContentPath" label="Thư mục bài viết" style={{ marginBottom: 8 }}>
                          <Input placeholder="src/content/blog" style={{ width: 220 }} />
                        </Form.Item>
                      </Space>
                    ) : null}

                    {showSecret ? (
                      <>
                        <Form.Item
                          name="secret"
                          label={secretUi.pasteLabel}
                          extra={
                            secretConfigured
                              ? `${secretUi.extra} · Đã có token trên server — để trống ô này nếu giữ nguyên.`
                              : secretUi.extra
                          }
                        >
                          <Input.Password
                            placeholder={secretUi.placeholder}
                            autoComplete="new-password"
                            style={{ maxWidth: 480 }}
                          />
                        </Form.Item>
                        {secretConfigured ? (
                          <Tag color="success" style={{ marginBottom: 12 }}>
                            Token đã lưu trên server
                          </Tag>
                        ) : null}
                        {destKind === 'channel:facebook_page' ? (
                          <Collapse
                            ghost
                            style={{ marginBottom: 12 }}
                            items={[
                              {
                                key: 'fb-token-help',
                                label: 'Cách lấy Page Access Token (Facebook)',
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
                                      Generate token với quyền <code>pages_show_list</code>,{' '}
                                      <code>pages_read_engagement</code>, <code>pages_manage_posts</code>.
                                    </li>
                                    <li>
                                      Gọi <code>GET /me/accounts</code> → lấy <code>id</code> (Page ID) và{' '}
                                      <code>access_token</code>.
                                    </li>
                                    <li>
                                      Dán <code>access_token</code> vào ô phía trên, Page ID vào ô Page / Channel ID,
                                      rồi bấm Lưu.
                                    </li>
                                  </ol>
                                ),
                              },
                            ]}
                          />
                        ) : null}
                        <Collapse
                          ghost
                          style={{ marginBottom: 12 }}
                          items={[
                            {
                              key: 'env-ref',
                              label: 'Tuỳ chọn: dùng biến môi trường thay vì dán token',
                              children: (
                                <Form.Item
                                  name="secretRef"
                                  label={secretUi.label}
                                  extra="Chỉ ghi tên biến (vd FB_KIT_PAGE_TOKEN) nếu đã set env trên máy API. Không bắt buộc nếu đã dán token ở trên."
                                >
                                  <Input placeholder="FB_KIT_PAGE_TOKEN" style={{ maxWidth: 360 }} />
                                </Form.Item>
                              ),
                            },
                          ]}
                        />
                      </>
                    ) : (
                      <Alert
                        type="info"
                        showIcon
                        style={{ marginBottom: 12 }}
                        message="Đăng thủ công không cần token — khi xuất bản hệ thống cho chép bài / tải file."
                      />
                    )}

                    <Collapse
                      ghost
                      style={{ marginBottom: 12 }}
                      items={[
                        {
                          key: 'adv',
                          label: 'Tuỳ chọn nâng cao (JSON phụ)',
                          children: (
                            <Form.Item
                              name="configJson"
                              label="JSON cấu hình thêm"
                              extra="Chỉ cần nếu bạn biết. WordPress/Astro đã có ô riêng phía trên — hệ thống tự ghép vào JSON này."
                            >
                              <Input.TextArea rows={3} placeholder="{}" style={{ fontFamily: 'monospace' }} />
                            </Form.Item>
                          ),
                        },
                      ]}
                    />

                    <Space>
                      <Button type="primary" loading={savingDest} onClick={() => void saveDestination()}>
                        Lưu vào danh sách
                      </Button>
                      <Button onClick={() => resetDestForm((destKind as DestKind) || 'site:manual')}>
                        Xoá form
                      </Button>
                    </Space>
                  </Form>

                  <Alert
                    style={{ marginTop: 16 }}
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
