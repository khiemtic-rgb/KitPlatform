import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Drawer,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { CONTENT_NAV_SETUP } from '@/modules/content/content-nav';
import { ContentFamixaSeriesTab } from '@/modules/content/ContentFamixaSeriesTab';
import { ContentVideoLabTab } from '@/modules/content/ContentVideoLabTab';
import '@/modules/content/content-famixa-studio.css';
import {
  approveContentVideoJob,
  createContentVideoJobFromPackage,
  fetchContentBrands,
  fetchContentPackages,
  fetchContentSettings,
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

const PKG_STATUS: Record<string, { text: string; color: string }> = {
  Draft: { text: 'Nháp', color: 'default' },
  Generating: { text: 'Đang tạo…', color: 'processing' },
  Review: { text: 'Chờ duyệt', color: 'blue' },
  Approved: { text: 'Đã duyệt', color: 'cyan' },
  Scheduled: { text: 'Đã lên lịch', color: 'geekblue' },
  Published: { text: 'Đã đăng', color: 'green' },
};

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

type VideoRow = ContentPackage & { job?: ContentVideoJob };

function openStoryboardWatch(title: string, scenes: StoryBeat[]) {
  const slides = scenes
    .map((s) => {
      const sec = Math.max(3, (s.endSec ?? 0) - (s.startSec ?? 0) || 8);
      const img = s.imageUrl
        ? `<img src="${s.imageUrl.replace(/"/g, '&quot;')}" alt="">`
        : `<div class="ph">${escapeHtml(s.beat || 'Scene')}</div>`;
      return `<section data-sec="${sec}">${img}<p><b>${escapeHtml(s.beat || '')}</b> ${escapeHtml(s.text || '')}</p></section>`;
    })
    .join('');
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>
<style>
body{margin:0;background:#0f172a;color:#fff;font-family:sans-serif}
section{display:none;min-height:100vh;padding:24px;box-sizing:border-box}
section.on{display:block}
img,.ph{width:min(420px,90vw);height:min(740px,70vh);object-fit:cover;border-radius:12px;background:#1e293b;display:flex;align-items:center;justify-content:center}
p{max-width:640px;line-height:1.5}
a{color:#93c5fd}
</style></head><body>
${slides || '<p>Chưa có scene</p>'}
<script>
const ss=[...document.querySelectorAll('section')];
let i=0;
function show(n){ss.forEach((s,j)=>s.classList.toggle('on',j===n));}
function tick(){if(!ss.length)return; show(i); const sec=Number(ss[i].dataset.sec||8); i=(i+1)%ss.length; setTimeout(tick, sec*1000);}
show(0); if(ss.length>1) setTimeout(tick, Number(ss[0].dataset.sec||8)*1000);
</script></body></html>`;
  const url = URL.createObjectURL(new Blob([html], { type: 'text/html' }));
  window.open(url, '_blank', 'noopener');
}

function escapeHtml(s: string) {
  return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] ?? c);
}

function parseStoryboard(json: string): StoryBeat[] {
  try {
    const raw = JSON.parse(json || '[]') as StoryBeat[];
    return Array.isArray(raw) ? raw : [];
  } catch {
    return [];
  }
}

function latestJobByPackage(jobs: ContentVideoJob[]): Map<string, ContentVideoJob> {
  const map = new Map<string, ContentVideoJob>();
  for (const job of jobs) {
    if (!job.packageId) continue;
    const prev = map.get(job.packageId);
    if (!prev || job.updatedAt > prev.updatedAt) map.set(job.packageId, job);
  }
  return map;
}

export function ContentVideosPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [jobs, setJobs] = useState<ContentVideoJob[]>([]);
  const [templates, setTemplates] = useState<ContentVideoTemplate[]>([]);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandFilter, setBrandFilter] = useState<string | undefined>();
  const [coreFilter, setCoreFilter] = useState<string | undefined>();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [templateId, setTemplateId] = useState<string | undefined>();
  const [autoStoryboard, setAutoStoryboard] = useState(true);
  const [skipExisting, setSkipExisting] = useState(true);
  const [panel, setPanel] = useState<VideoRow | null>(null);
  const [busy, setBusy] = useState(false);
  const [creatomateConfigured, setCreatomateConfigured] = useState(false);
  const [elevenLabsConfigured, setElevenLabsConfigured] = useState(false);
  const [mainTab, setMainTab] = useState('series');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [jobList, tpl, pkg, brandList, settings] = await Promise.all([
        fetchContentVideoJobs(),
        fetchContentVideoTemplates(true),
        fetchContentPackages(),
        fetchContentBrands(true),
        fetchContentSettings().catch(() => null),
      ]);
      setJobs(jobList);
      setTemplates(tpl);
      setPackages(pkg.filter((p) => p.variantCount > 0 && !!p.sourcePackageId));
      setBrands(brandList);
      setCreatomateConfigured(settings?.video?.creatomateConfigured ?? false);
      setElevenLabsConfigured(settings?.video?.elevenLabsConfigured ?? false);
      setTemplateId((cur) => cur ?? tpl.find((t) => t.code === 'tiktok_45s_hooks')?.id ?? tpl[0]?.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được Video Factory'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setSelectedIds([]);
  }, [brandFilter, coreFilter]);

  const jobByPackage = useMemo(() => latestJobByPackage(jobs), [jobs]);

  const coreOptions = useMemo(() => {
    const acc: Array<{ value: string; label: string }> = [];
    for (const p of packages) {
      const value = p.sourcePackageId || p.id;
      const label = p.sourceTitle || p.title;
      if (!acc.some((o) => o.value === value)) acc.push({ value, label });
    }
    return acc.sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [packages]);

  const rows: VideoRow[] = useMemo(
    () =>
      packages
        .filter((p) => {
          if (brandFilter && p.brandId !== brandFilter) return false;
          if (coreFilter && (p.sourcePackageId || p.id) !== coreFilter) return false;
          return true;
        })
        .map((p) => ({ ...p, job: jobByPackage.get(p.id) })),
    [packages, brandFilter, coreFilter, jobByPackage],
  );

  const onCreate = async () => {
    if (selectedIds.length === 0) {
      message.warning('Chọn ít nhất một bài');
      return;
    }
    if (!templateId) {
      message.warning('Chọn template');
      return;
    }
    setBusy(true);
    try {
      let created = 0;
      let skipped = 0;
      let failed = 0;
      for (const packageId of selectedIds) {
        if (skipExisting && jobByPackage.has(packageId)) {
          skipped += 1;
          continue;
        }
        try {
          const job = await createContentVideoJobFromPackage({ packageId, templateId });
          if (autoStoryboard) await prepareContentVideoStoryboard(job.id);
          created += 1;
        } catch {
          failed += 1;
        }
      }
      if (created > 0) {
        message.success(
          `Đã tạo ${created} video` +
            (autoStoryboard ? ' + storyboard' : '') +
            (skipped ? ` · bỏ qua ${skipped} đã có` : '') +
            (failed ? ` · lỗi ${failed}` : ''),
        );
      } else if (skipped > 0 && failed === 0) {
        message.info('Các hàng đã chọn đã có video — bỏ tick «Bỏ qua đã có» nếu muốn tạo thêm.');
      } else {
        message.warning(`Không tạo được job${failed ? ` (${failed} lỗi)` : ''}. Cần bản tiktok_script.`);
      }
      setSelectedIds([]);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo video thất bại'));
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
      setPanel((cur) => (cur ? { ...cur, job: next } : { ...rows.find((r) => r.job?.id === next.id)!, job: next }));
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Thao tác thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const openRow = (row: VideoRow) => setPanel(row);

  const createForPanel = async () => {
    if (!panel) return;
    if (!templateId) {
      message.warning('Chọn template');
      return;
    }
    setBusy(true);
    try {
      const job = await createContentVideoJobFromPackage({ packageId: panel.id, templateId });
      const next = autoStoryboard ? await prepareContentVideoStoryboard(job.id) : job;
      setPanel({ ...panel, job: next });
      message.success(autoStoryboard ? 'Đã tạo video + storyboard' : 'Đã tạo video job');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo video thất bại — cần bản tiktok_script'));
    } finally {
      setBusy(false);
    }
  };

  const detail = panel?.job ?? null;
  const storyboard = detail ? parseStoryboard(detail.storyboardJson) : [];

  return (
    <div className={mainTab === 'series' ? 'fx-videos fx-videos--studio' : 'fx-videos'}>
      {mainTab === 'series' ? null : (
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
            <Space>
              <VideoCameraOutlined />
              Videos
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">
            Series Famixa = Pha 0–1 (shot dài tập). Lab = video ngắn đa brand. Factory = job từ góc
            brand (9:16). Không trộn ba tab.
          </Typography.Text>
          <div style={{ marginTop: 8 }}>
            <Space size={4} wrap>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Cài đặt:
              </Typography.Text>
              {CONTENT_NAV_SETUP.map((item) => (
                <Link key={item.key} to={item.path}>
                  <Button size="small" type="link" icon={item.icon} style={{ paddingInline: 6 }}>
                    {item.label}
                  </Button>
                </Link>
              ))}
              <Link to="/content/topics">
                <Button size="small" type="link" style={{ paddingInline: 6 }}>
                  Bài viết
                </Button>
              </Link>
              <Link to="/content/packages">
                <Button size="small" type="link" style={{ paddingInline: 6 }}>
                  Góc brand
                </Button>
              </Link>
            </Space>
          </div>
        </div>
      </div>
      )}

      <Tabs
        activeKey={mainTab}
        onChange={setMainTab}
        items={[
          { key: 'series', label: 'Series Famixa — EP01', children: <ContentFamixaSeriesTab /> },
          { key: 'lab', label: 'Phase 1 — bảng sản xuất', children: <ContentVideoLabTab /> },
          {
            key: 'factory',
            label: 'Factory (góc brand)',
            children: (
              <div>
                {(!creatomateConfigured || !elevenLabsConfigured) && (
                  <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 12 }}
                    message="Chưa cấu hình render MP4 / giọng nói"
                    description={
                      <span>
                        {creatomateConfigured ? 'Creatomate đã có key. ' : 'Chưa có Creatomate — chỉ storyboard / CapCut. '}
                        {elevenLabsConfigured ? 'ElevenLabs đã có key.' : 'Chưa có ElevenLabs — pipeline bỏ qua lồng tiếng.'}{' '}
                        <Link to="/content/ai#video">Cấu hình Creatomate + giọng nói</Link>
                      </span>
                    }
                  />
                )}
                <Space wrap style={{ marginBottom: 12 }}>
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
                    placeholder="Lọc thương hiệu"
                    style={{ width: 200 }}
                    value={brandFilter}
                    onChange={setBrandFilter}
                    options={brands.map((b) => ({ value: b.id, label: b.name }))}
                  />
                  <Select
                    placeholder="Template"
                    style={{ minWidth: 280 }}
                    value={templateId}
                    onChange={setTemplateId}
                    options={templates.map((t) => ({
                      value: t.id,
                      label: `${t.name} (${t.aspectRatio} · ${t.durationSec}s)`,
                    }))}
                  />
                  <Button icon={<ReloadOutlined />} onClick={() => void load()}>
                    Tải lại
                  </Button>
                  <Button type="primary" loading={busy} disabled={selectedIds.length === 0} onClick={() => void onCreate()}>
                    Tạo video ({selectedIds.length})
                  </Button>
                </Space>
                <Space wrap style={{ marginBottom: 12 }}>
        <Checkbox checked={autoStoryboard} onChange={(e) => setAutoStoryboard(e.target.checked)}>
          Tự tạo storyboard
        </Checkbox>
        <Checkbox checked={skipExisting} onChange={(e) => setSkipExisting(e.target.checked)}>
          Bỏ qua bài đã có video
        </Checkbox>
        <Button
          size="small"
          onClick={() => setSelectedIds(rows.filter((r) => !r.job).map((r) => r.id))}
        >
          Chọn chưa có video
        </Button>
        <Button size="small" onClick={() => setSelectedIds(rows.map((r) => r.id))}>
          Chọn tất cả
        </Button>
        <Button size="small" onClick={() => setSelectedIds([])}>
          Bỏ chọn
        </Button>
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={{ pageSize: 20 }}
        locale={{ emptyText: 'Chưa có góc brand đã viết — Generate ở Góc brand trước.' }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys.map(String)),
        }}
        columns={[
          { title: 'Thương hiệu', dataIndex: 'brandName', width: 140 },
          {
            title: 'Tiêu đề',
            dataIndex: 'title',
            render: (title: string, row: VideoRow) => (
              <div>
                <Button
                  type="link"
                  style={{ padding: 0, height: 'auto', whiteSpace: 'normal', textAlign: 'left' }}
                  onClick={(e) => {
                    e.stopPropagation();
                    openRow(row);
                  }}
                >
                  {title}
                </Button>
                {row.sourceTitle ? (
                  <div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      Từ ý tưởng: {row.sourceTitle}
                    </Typography.Text>
                  </div>
                ) : null}
              </div>
            ),
          },
          {
            title: 'Bản viết',
            dataIndex: 'variantCount',
            width: 88,
            align: 'center',
          },
          {
            title: 'Bài',
            dataIndex: 'status',
            width: 110,
            render: (s: string) => {
              const m = PKG_STATUS[s] ?? { text: s, color: 'default' };
              return <Tag color={m.color}>{m.text}</Tag>;
            },
          },
          {
            title: 'Video',
            key: 'video',
            width: 130,
            render: (_: unknown, row: VideoRow) => {
              if (!row.job) return <Typography.Text type="secondary">Chưa có</Typography.Text>;
              const m = STATUS_LABEL[row.job.status] ?? { text: row.job.status, color: 'default' };
              return <Tag color={m.color}>{m.text}</Tag>;
            },
          },
          {
            title: '',
            key: 'a',
            width: 200,
            render: (_: unknown, row: VideoRow) => (
              <Button type="link" onClick={() => openRow(row)}>
                {row.job ? 'Xem video' : 'Tạo video'}
              </Button>
            ),
          },
        ]}
      />
              </div>
            ),
          },
        ]}
      />

      <Drawer
        title={panel ? `${panel.job ? 'Video' : 'Tạo video'} · ${panel.brandName}` : 'Video'}
        width="min(1120px, 92vw)"
        open={!!panel}
        onClose={() => setPanel(null)}
        extra={
          panel && !detail ? (
            <Button type="primary" icon={<ThunderboltOutlined />} loading={busy} onClick={() => void createForPanel()}>
              Tạo video
            </Button>
          ) : detail ? (
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
        {panel && !detail ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Alert
              type="info"
              showIcon
              message="Tạo video từ góc brand này — không rời tab Videos."
            />
            <div>
              <Typography.Title level={5} style={{ margin: 0 }}>
                {panel.title}
              </Typography.Title>
              <Typography.Text type="secondary">
                {panel.brandName}
                {panel.sourceTitle ? ` · Từ ý tưởng: ${panel.sourceTitle}` : ''}
              </Typography.Text>
            </div>
            <div>
              <Typography.Text type="secondary">Template</Typography.Text>
              <Select
                style={{ width: '100%', marginTop: 8 }}
                value={templateId}
                onChange={setTemplateId}
                options={templates.map((t) => ({
                  value: t.id,
                  label: `${t.name} (${t.aspectRatio} · ${t.durationSec}s)`,
                }))}
              />
            </div>
            <Checkbox checked={autoStoryboard} onChange={(e) => setAutoStoryboard(e.target.checked)}>
              Tự tạo storyboard sau khi tạo job
            </Checkbox>
            <Button type="primary" size="large" loading={busy} onClick={() => void createForPanel()}>
              Tạo video
            </Button>
          </Space>
        ) : detail ? (
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
            <Card
              size="small"
              title={detail.outputUrl ? 'Xem video' : 'Xem preview'}
              extra={
                detail.outputUrl ? (
                  <a href={detail.outputUrl} target="_blank" rel="noreferrer">
                    Mở link MP4
                  </a>
                ) : (
                  <Button
                    type="link"
                    style={{ padding: 0 }}
                    onClick={() => openStoryboardWatch(detail.title, storyboard)}
                  >
                    Mở xem (tab mới)
                  </Button>
                )
              }
            >
              {detail.outputUrl ? (
                <video
                  src={detail.outputUrl}
                  controls
                  playsInline
                  style={{ width: '100%', maxHeight: 420, background: '#0f172a', borderRadius: 8 }}
                />
              ) : (
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  <Typography.Paragraph style={{ marginBottom: 0 }}>
                    Chưa có file MP4 (template local / hết credit Creatomate). Xem slideshow scene + ảnh.
                  </Typography.Paragraph>
                  {detail.previewUrl ? (
                    <a href={detail.previewUrl} target="_blank" rel="noreferrer">
                      Mở ảnh preview
                    </a>
                  ) : null}
                  <Button type="primary" onClick={() => openStoryboardWatch(detail.title, storyboard)}>
                    Xem preview
                  </Button>
                </Space>
              )}
            </Card>
            <Card size="small" title="Script">
              <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>
                {detail.scriptBody}
              </Typography.Paragraph>
            </Card>
            <Card size="small" title={`Storyboard (${storyboard.length} scenes)`}>
              {storyboard.length === 0 ? (
                <Typography.Text type="secondary">Chưa có — bấm Storyboard hoặc Chạy MVP.</Typography.Text>
              ) : (
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: '1fr 1fr',
                    gap: 12,
                  }}
                >
                  {storyboard.map((b, i) => (
                    <div
                      key={`${b.beat}-${i}`}
                      style={{
                        border: '1px solid #e2e8f0',
                        borderRadius: 8,
                        padding: 12,
                        background: '#fff',
                      }}
                    >
                      <Typography.Text strong>
                        {b.beat ?? `Scene ${i + 1}`}
                        {b.startSec != null && b.endSec != null ? ` · ${b.startSec}–${b.endSec}s` : ''}
                      </Typography.Text>
                      <Typography.Paragraph style={{ margin: '8px 0 4px', whiteSpace: 'pre-wrap' }}>
                        {b.text || '—'}
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
                </div>
              )}
            </Card>
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
