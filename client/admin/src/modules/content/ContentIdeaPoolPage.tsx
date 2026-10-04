import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import dayjs from 'dayjs';
import {
  App,
  Button,
  Card,
  Checkbox,
  Divider,
  Drawer,
  Empty,
  Input,
  Modal,
  Pagination,
  Segmented,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  analyzeContentPool,
  applyContentPoolFits,
  createContentPool,
  fetchContentArticleSeries,
  fetchContentBrands,
  fetchContentPackages,
  suggestContentPool,
  type ContentArticleSeries,
  type ContentBrand,
  type ContentBrandFit,
  type ContentPackage,
  type ContentPoolSuggestion,
} from '@/shared/api/content.api';

type IdeaStage = 'new' | 'developing' | 'used';
type StageFilter = 'all' | IdeaStage;

const PAGE_SIZE = 10;

const STAGE_LABEL: Record<IdeaStage, string> = {
  new: 'Mới',
  developing: 'Đang phát triển',
  used: 'Đã dùng',
};

const STAGE_COLOR: Record<IdeaStage, string> = {
  new: 'blue',
  developing: 'gold',
  used: 'default',
};

const EMPTY_DRAFT = { title: '', insight: '', source: '' };

function ideaStage(idea: ContentPackage): IdeaStage {
  const fits = idea.brandFits ?? [];
  if ((idea.adaptationCount ?? 0) > 0 || fits.some((f) => f.packageId)) return 'used';
  return fits.length > 0 ? 'developing' : 'new';
}

function ideaWhy(idea: ContentPackage) {
  const insight = idea.coreIdea?.insight?.trim();
  return insight && insight !== idea.title.trim() ? insight : null;
}

function ideaNote(idea: ContentPackage) {
  return ideaWhy(idea) ?? (idea.coreIdea?.problem?.trim() || null);
}

function formatDate(value?: string | null) {
  return value ? dayjs(value).format('DD/MM/YYYY') : null;
}

function parseBulk(text: string) {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 20)
    .map((line) => {
      const [title, insight, problem, coreMessage, source, factOrOpinion] = line
        .split('|')
        .map((s) => s.trim());
      return {
        title,
        insight: insight || undefined,
        problem: problem || undefined,
        coreMessage: coreMessage || undefined,
        source: source || undefined,
        factOrOpinion: factOrOpinion || undefined,
      };
    });
}

function verdictTag(fit?: ContentBrandFit) {
  if (!fit) return <Tag>Chưa chấm</Tag>;
  if (fit.verdict === 'fit') return <Tag color="green">Phù hợp {fit.score}</Tag>;
  if (fit.verdict === 'maybe') return <Tag color="gold">Có thể {fit.score}</Tag>;
  return <Tag>Bỏ {fit.score}</Tag>;
}

const VERDICT_ORDER: Record<string, number> = { fit: 0, maybe: 1 };

function IdeaField({ label, value }: { label: string; value?: string | null }) {
  if (!value?.trim()) return null;
  return (
    <div style={{ marginBottom: 16 }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {label}
      </Typography.Text>
      <Typography.Paragraph style={{ margin: '2px 0 0' }}>{value}</Typography.Paragraph>
    </div>
  );
}

function IdeaDetailDrawer({
  idea,
  brands,
  seriesByPackage,
  busy,
  onClose,
  onAnalyze,
  onApply,
}: {
  idea: ContentPackage;
  brands: ContentBrand[];
  seriesByPackage: Map<string, ContentArticleSeries[]>;
  busy: boolean;
  onClose: () => void;
  onAnalyze: (brandIds: string[]) => void;
  onApply: (brandIds: string[], generateFits: boolean) => void;
}) {
  const [showFit, setShowFit] = useState(false);
  const [scoreBrandIds, setScoreBrandIds] = useState<string[]>(() => brands.map((b) => b.id));
  const [picked, setPicked] = useState<Record<string, boolean>>({});
  const [generateFits, setGenerateFits] = useState(false);

  const fits = useMemo(
    () =>
      [...(idea.brandFits ?? [])].sort(
        (a, b) => (VERDICT_ORDER[a.verdict] ?? 2) - (VERDICT_ORDER[b.verdict] ?? 2) || b.score - a.score,
      ),
    [idea.brandFits],
  );

  const canPick = (fit: ContentBrandFit) => !fit.packageId && (fit.verdict === 'fit' || fit.verdict === 'maybe');
  const isPicked = (fit: ContentBrandFit) =>
    fit.brandId in picked ? picked[fit.brandId] : fit.verdict === 'fit';
  const pickedBrandIds = fits.filter((f) => canPick(f) && isPicked(f)).map((f) => f.brandId);

  const stage = ideaStage(idea);
  const why = ideaWhy(idea);
  const note = [idea.coreIdea?.source, idea.coreIdea?.sourceUrl, idea.coreIdea?.evidence]
    .map((v) => v?.trim())
    .filter(Boolean)
    .join(' · ');
  const hasDetail = Boolean(why || idea.coreIdea?.problem || idea.coreIdea?.coreMessage || note);

  return (
    <Drawer open width={640} title="Ý tưởng" onClose={onClose} destroyOnHidden>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        {idea.title}
      </Typography.Title>
      <Space size={12} style={{ marginBottom: 24 }}>
        <Tag color={STAGE_COLOR[stage]} bordered={false} style={{ marginInlineEnd: 0 }}>
          {STAGE_LABEL[stage]}
        </Tag>
        {formatDate(idea.createdAt) ? (
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Tạo ngày {formatDate(idea.createdAt)}
          </Typography.Text>
        ) : null}
      </Space>

      <IdeaField label="Vì sao đáng kể" value={why} />
      <IdeaField label="Vấn đề" value={idea.coreIdea?.problem} />
      <IdeaField label="Thông điệp cốt lõi" value={idea.coreIdea?.coreMessage} />
      <IdeaField label="Ghi chú / nguồn" value={note} />
      {hasDetail ? null : <Typography.Paragraph type="secondary">Chưa có ghi chú thêm.</Typography.Paragraph>}

      <Divider />

      {!showFit ? (
        <Space size={12} wrap>
          <Button type="primary" onClick={() => setShowFit(true)}>
            Đánh giá Brand Fit
          </Button>
          <Typography.Text type="secondary">
            {fits.length > 0 ? `Đã đánh giá ${fits.length} brand` : 'Brand nào có lý do để kể ý tưởng này?'}
          </Typography.Text>
        </Space>
      ) : (
        <>
          <Typography.Title level={5} style={{ marginBottom: 0 }}>
            Đánh giá Brand Fit
          </Typography.Title>
          <Typography.Paragraph type="secondary">Brand nào có lý do để kể ý tưởng này?</Typography.Paragraph>

          <Checkbox.Group
            style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 12 }}
            value={scoreBrandIds}
            onChange={(v) => setScoreBrandIds(v.map(String))}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
          <Button loading={busy} disabled={scoreBrandIds.length === 0} onClick={() => onAnalyze(scoreBrandIds)}>
            {fits.length > 0 ? 'Chấm lại Brand Fit' : 'Chấm Brand Fit'}
          </Button>

          {fits.length === 0 ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
              Chưa đánh giá. AI đọc ý tưởng và cho biết brand nào có lý do để kể — có thể mất vài phút.
            </Typography.Paragraph>
          ) : (
            <div style={{ marginTop: 16 }}>
              {fits.map((fit) => {
                const matched = fit.packageId ? seriesByPackage.get(fit.packageId) ?? [] : [];
                return (
                  <div key={fit.brandId} style={{ padding: '12px 0', borderTop: '1px solid #f0f0f0' }}>
                    <Space size={8} wrap>
                      <Typography.Text strong>{fit.brandName}</Typography.Text>
                      {verdictTag(fit)}
                      {canPick(fit) ? (
                        <Checkbox
                          checked={isPicked(fit)}
                          onChange={(e) => setPicked((prev) => ({ ...prev, [fit.brandId]: e.target.checked }))}
                        >
                          Tạo góc
                        </Checkbox>
                      ) : null}
                    </Space>
                    {fit.reason ? (
                      <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 13 }}>
                        {fit.reason}
                      </Typography.Paragraph>
                    ) : null}
                    {fit.packageId ? (
                      <Space size={12} wrap style={{ marginTop: 6, fontSize: 13 }}>
                        <Link to={`/content/packages?package=${fit.packageId}`}>Xem Góc Brand</Link>
                        {matched.slice(0, 2).map((s) => (
                          <Link key={s.id} to={`/content/article-series/${s.id}`}>
                            {s.code} · {s.name}
                          </Link>
                        ))}
                        {matched.length > 2 ? (
                          <Typography.Text type="secondary">+{matched.length - 2} Series</Typography.Text>
                        ) : null}
                        <Link to={`/content/article-series?packageId=${encodeURIComponent(fit.packageId)}`}>
                          Tạo Series
                        </Link>
                      </Space>
                    ) : null}
                  </div>
                );
              })}
              {fits.some(canPick) ? (
                <Space wrap style={{ marginTop: 12 }}>
                  <Button
                    type="primary"
                    loading={busy}
                    disabled={pickedBrandIds.length === 0}
                    onClick={() => onApply(pickedBrandIds, generateFits)}
                  >
                    Tạo {pickedBrandIds.length} góc brand
                  </Button>
                  <Checkbox checked={generateFits} onChange={(e) => setGenerateFits(e.target.checked)}>
                    Generate theo nơi đăng sau khi tạo góc
                  </Checkbox>
                </Space>
              ) : null}
            </div>
          )}
        </>
      )}
    </Drawer>
  );
}

export function ContentIdeaPoolPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [cores, setCores] = useState<ContentPackage[]>([]);
  const [series, setSeries] = useState<ContentArticleSeries[]>([]);
  const [filter, setFilter] = useState<StageFilter>('all');
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string>();

  const [addOpen, setAddOpen] = useState(false);
  const [bulkMode, setBulkMode] = useState(false);
  const [draft, setDraft] = useState(EMPTY_DRAFT);
  const [bulk, setBulk] = useState('');

  const [suggestOpen, setSuggestOpen] = useState(false);
  const [suggesting, setSuggesting] = useState(false);
  const [suggestions, setSuggestions] = useState<ContentPoolSuggestion[]>([]);
  const [savingTitle, setSavingTitle] = useState<string>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [brandList, pkg, seriesRows] = await Promise.all([
        fetchContentBrands(true),
        fetchContentPackages({ coresOnly: true }),
        fetchContentArticleSeries(),
      ]);
      setBrands(brandList);
      setCores(pkg);
      setSeries(seriesRows);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được danh sách ý tưởng'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const seriesByPackage = useMemo(() => {
    const map = new Map<string, ContentArticleSeries[]>();
    for (const s of series) {
      const list = map.get(s.sourcePackageId);
      if (list) list.push(s);
      else map.set(s.sourcePackageId, [s]);
    }
    return map;
  }, [series]);

  const stageCounts = useMemo(() => {
    const counts: Record<IdeaStage, number> = { new: 0, developing: 0, used: 0 };
    for (const c of cores) counts[ideaStage(c)] += 1;
    return counts;
  }, [cores]);

  const visible = filter === 'all' ? cores : cores.filter((c) => ideaStage(c) === filter);
  const pageCount = Math.max(1, Math.ceil(visible.length / PAGE_SIZE));
  const currentPage = Math.min(page, pageCount);
  const pageStart = (currentPage - 1) * PAGE_SIZE;
  const pageItems = visible.slice(pageStart, pageStart + PAGE_SIZE);
  const openIdea = openId ? cores.find((c) => c.id === openId) : undefined;

  useEffect(() => {
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  const onFilterChange = (value: StageFilter) => {
    setFilter(value);
    setPage(1);
  };

  const closeAdd = () => {
    setAddOpen(false);
    setBulkMode(false);
    setDraft(EMPTY_DRAFT);
    setBulk('');
  };

  const onAdd = async () => {
    const ideas = bulkMode
      ? parseBulk(bulk)
      : draft.title.trim()
        ? [
            {
              title: draft.title.trim(),
              insight: draft.insight.trim() || undefined,
              source: draft.source.trim() || undefined,
            },
          ]
        : [];
    if (ideas.length === 0) {
      message.warning(bulkMode ? 'Mỗi dòng một ý tưởng.' : 'Nhập ý tưởng trước khi lưu.');
      return;
    }
    setBusy(true);
    try {
      const res = await createContentPool({ ideas });
      message.success(`Đã thêm ${res.packages.length} ý tưởng`);
      closeAdd();
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Thêm ý tưởng thất bại'));
    } finally {
      setBusy(false);
    }
  };

  const onSuggest = async () => {
    setSuggestOpen(true);
    setSuggesting(true);
    try {
      const res = await suggestContentPool({ limit: 4 });
      setSuggestions(res.ideas ?? []);
    } catch (e) {
      setSuggestions([]);
      message.error(apiErrorMessage(e, 'AI chưa đề xuất được ý tưởng'));
    } finally {
      setSuggesting(false);
    }
  };

  const dismissSuggestion = (title: string) => {
    setSuggestions((prev) => prev.filter((s) => s.title !== title));
  };

  const saveSuggestion = async (s: ContentPoolSuggestion) => {
    setSavingTitle(s.title);
    try {
      await createContentPool({
        ideas: [
          {
            title: s.title,
            insight: s.insight ?? undefined,
            problem: s.problem ?? undefined,
            coreMessage: s.coreMessage ?? undefined,
            factOrOpinion: s.factOrOpinion ?? undefined,
          },
        ],
      });
      message.success('Đã lưu vào Ý tưởng');
      dismissSuggestion(s.title);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Lưu ý tưởng thất bại'));
    } finally {
      setSavingTitle(undefined);
    }
  };

  const onAnalyze = async (packageId: string, brandIds: string[]) => {
    const hide = message.loading('Đang chấm Brand Fit… AI đọc ý tưởng, có thể mất vài phút.', 0);
    setBusy(true);
    try {
      await analyzeContentPool({ packageIds: [packageId], brandIds, includeMaybe: true });
      message.success('Đã chấm Brand Fit');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chấm Brand Fit thất bại'));
    } finally {
      hide();
      setBusy(false);
    }
  };

  const onApply = async (packageId: string, brandIds: string[], generateFits: boolean) => {
    setBusy(true);
    try {
      const res = await applyContentPoolFits({
        items: brandIds.map((brandId) => ({ packageId, brandId })),
        generateFits,
      });
      message.success(res.message ?? `Đã tạo ${res.created} góc brand`);
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Tạo góc thất bại'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ maxWidth: 960 }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          gap: 16,
          marginBottom: 24,
          flexWrap: 'wrap',
        }}
      >
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Ý tưởng
          </Typography.Title>
          <Typography.Text type="secondary">Những điều đáng để kể.</Typography.Text>
        </div>
        <Space wrap>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
            Thêm ý tưởng
          </Button>
          <Button disabled={suggesting} onClick={() => void onSuggest()}>
            ✦ AI đề xuất
          </Button>
        </Space>
      </div>

      {suggestOpen ? (
        <Card
          style={{ marginBottom: 24 }}
          title="✦ AI đề xuất"
          extra={
            <Space size={4}>
              <Button size="small" type="text" disabled={suggesting} onClick={() => void onSuggest()}>
                Đề xuất lại
              </Button>
              <Button size="small" type="text" onClick={() => setSuggestOpen(false)}>
                Đóng
              </Button>
            </Space>
          }
        >
          <Typography.Paragraph type="secondary" style={{ marginTop: -4 }}>
            Ý tưởng gốc tổng quát, chưa gắn Brand. Chỉ những ý bạn lưu mới vào thư viện.
          </Typography.Paragraph>
          {suggesting ? (
            <div style={{ padding: 32, textAlign: 'center' }}>
              <Spin />
            </div>
          ) : suggestions.length === 0 ? (
            <Typography.Text type="secondary">Không còn đề xuất nào. Bấm Đề xuất lại để xem thêm.</Typography.Text>
          ) : (
            suggestions.map((s, index) => (
              <div
                key={s.title}
                style={{
                  display: 'flex',
                  alignItems: 'flex-start',
                  gap: 16,
                  padding: '14px 0',
                  borderTop: index > 0 ? '1px solid #f0f0f0' : undefined,
                }}
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Typography.Text strong>{s.title}</Typography.Text>
                  {s.insight ? (
                    <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0' }}>
                      {s.insight}
                    </Typography.Paragraph>
                  ) : null}
                </div>
                <Space size={4}>
                  <Button
                    size="small"
                    type="primary"
                    ghost
                    loading={savingTitle === s.title}
                    disabled={Boolean(savingTitle) && savingTitle !== s.title}
                    onClick={() => void saveSuggestion(s)}
                  >
                    Lưu vào Ý tưởng
                  </Button>
                  <Button
                    size="small"
                    type="text"
                    disabled={savingTitle === s.title}
                    onClick={() => dismissSuggestion(s.title)}
                  >
                    Bỏ qua
                  </Button>
                </Space>
              </div>
            ))
          )}
        </Card>
      ) : null}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          marginBottom: 12,
          flexWrap: 'wrap',
        }}
      >
        <Typography.Text type="secondary">{cores.length} ý tưởng</Typography.Text>
        <Segmented<StageFilter>
          value={filter}
          onChange={onFilterChange}
          options={[
            { value: 'all', label: `Tất cả (${cores.length})` },
            { value: 'new', label: `Mới (${stageCounts.new})` },
            { value: 'developing', label: `Đang phát triển (${stageCounts.developing})` },
            { value: 'used', label: `Đã dùng (${stageCounts.used})` },
          ]}
        />
      </div>

      <Card styles={{ body: { padding: 0 } }}>
        {loading && cores.length === 0 ? (
          <div style={{ padding: 48, textAlign: 'center' }}>
            <Spin />
          </div>
        ) : visible.length === 0 ? (
          <Empty
            style={{ padding: 48 }}
            description={cores.length === 0 ? 'Chưa có ý tưởng nào' : 'Không có ý tưởng ở trạng thái này'}
          >
            {cores.length === 0 ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
                Thêm ý tưởng
              </Button>
            ) : null}
          </Empty>
        ) : (
          <>
            {pageItems.map((idea, index) => {
              const stage = ideaStage(idea);
              const note = ideaNote(idea);
              return (
                <div
                  key={idea.id}
                  data-idea-row
                  style={{ padding: '20px 24px', borderTop: index > 0 ? '1px solid #f0f0f0' : undefined }}
                >
                  <Typography.Text strong style={{ fontSize: 16, display: 'block' }}>
                    {idea.title}
                  </Typography.Text>
                  {note ? (
                    <Typography.Paragraph type="secondary" ellipsis={{ rows: 2 }} style={{ margin: '6px 0 0' }}>
                      {note}
                    </Typography.Paragraph>
                  ) : null}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginTop: 12 }}>
                    <Tag color={STAGE_COLOR[stage]} bordered={false} style={{ marginInlineEnd: 0 }}>
                      {STAGE_LABEL[stage]}
                    </Tag>
                    <div style={{ flex: 1 }} />
                    {formatDate(idea.createdAt) ? (
                      <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                        {formatDate(idea.createdAt)}
                      </Typography.Text>
                    ) : null}
                    <Button type="link" style={{ paddingInline: 0 }} onClick={() => setOpenId(idea.id)}>
                      Mở →
                    </Button>
                  </div>
                </div>
              );
            })}
            {visible.length > PAGE_SIZE ? (
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: 12,
                  padding: '14px 24px',
                  borderTop: '1px solid #f0f0f0',
                  flexWrap: 'wrap',
                }}
              >
                <Typography.Text type="secondary">
                  {pageStart + 1}–{pageStart + pageItems.length} trong {visible.length} ý tưởng
                </Typography.Text>
                <Pagination
                  current={currentPage}
                  pageSize={PAGE_SIZE}
                  total={visible.length}
                  showSizeChanger={false}
                  onChange={setPage}
                  itemRender={(_, type, element) => {
                    if (type === 'prev') {
                      return (
                        <Button type="text" size="small" disabled={currentPage === 1}>
                          ← Trước
                        </Button>
                      );
                    }
                    if (type === 'next') {
                      return (
                        <Button type="text" size="small" disabled={currentPage === pageCount}>
                          Sau →
                        </Button>
                      );
                    }
                    return element;
                  }}
                />
              </div>
            ) : null}
          </>
        )}
      </Card>

      {openIdea ? (
        <IdeaDetailDrawer
          key={openIdea.id}
          idea={openIdea}
          brands={brands}
          seriesByPackage={seriesByPackage}
          busy={busy}
          onClose={() => setOpenId(undefined)}
          onAnalyze={(brandIds) => void onAnalyze(openIdea.id, brandIds)}
          onApply={(brandIds, generateFits) => void onApply(openIdea.id, brandIds, generateFits)}
        />
      ) : null}

      <Modal
        open={addOpen}
        title="Thêm ý tưởng"
        okText="Lưu ý tưởng"
        cancelText="Hủy"
        confirmLoading={busy}
        onOk={() => void onAdd()}
        onCancel={closeAdd}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">
          Một điều đáng để kể — chưa gắn với Brand, sản phẩm hay kênh đăng.
        </Typography.Paragraph>
        {bulkMode ? (
          <Input.TextArea
            rows={8}
            value={bulk}
            onChange={(e) => setBulk(e.target.value)}
            placeholder={
              'Mỗi dòng một ý tưởng (tối đa 20). Có thể thêm sau dấu |\n' +
              'Ý tưởng | Vì sao đáng kể | Vấn đề | Thông điệp | Nguồn | fact/opinion\n\n' +
              'Những điều nhỏ bị bỏ qua lâu ngày thường tạo ra hậu quả lớn | Nguyên nhân xuất hiện từ rất sớm nhưng ít ai để ý'
            }
          />
        ) : (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Input.TextArea
              autoFocus
              rows={3}
              value={draft.title}
              onChange={(e) => setDraft((d) => ({ ...d, title: e.target.value }))}
              placeholder="Ví dụ: Những điều quen thuộc thường chỉ được đánh giá đúng sau khi chúng biến mất."
            />
            <Input.TextArea
              rows={2}
              value={draft.insight}
              onChange={(e) => setDraft((d) => ({ ...d, insight: e.target.value }))}
              placeholder="Vì sao đáng kể? (không bắt buộc)"
            />
            <Input
              value={draft.source}
              onChange={(e) => setDraft((d) => ({ ...d, source: e.target.value }))}
              placeholder="Ghi chú / nguồn (không bắt buộc)"
            />
          </Space>
        )}
        <Button type="link" style={{ paddingInline: 0, marginTop: 8 }} onClick={() => setBulkMode((v) => !v)}>
          {bulkMode ? 'Thêm từng ý' : 'Thêm nhiều ý một lúc'}
        </Button>
      </Modal>
    </div>
  );
}
