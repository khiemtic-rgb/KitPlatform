import { useEffect, useState, type ReactNode } from 'react';
import { Card, Space, Tag, Typography } from 'antd';
import { Link } from 'react-router-dom';
import {
  fetchContentArticleSeries,
  fetchContentArticleSeriesDetail,
  fetchContentPackages,
  type ContentArticleEpisode,
  type ContentArticleSeries,
  type ContentBrandFit,
  type ContentCreativeBrief,
  type ContentPackage,
  type ContentQualityGate,
} from '@/shared/api/content.api';

export type ContentLineageTrailItem = {
  label: string;
  to?: string;
};

export function ContentKindTag({ kind }: { kind: 'series' | 'standalone' }) {
  return kind === 'series' ? <Tag color="blue">Series content</Tag> : <Tag>Standalone</Tag>;
}

export function seriesContentTrail(
  lineage: { series: ContentArticleSeries; episode: ContentArticleEpisode },
  contentTitle: string,
): ContentLineageTrailItem[] {
  const { series, episode } = lineage;
  const angle = seriesBrandAngle(series);
  return [
    { label: series.brandName, to: '/content/brands' },
    {
      label: angle || series.sourcePackageTitle,
      to: `/content/packages?package=${series.sourcePackageId}`,
    },
    { label: series.code, to: `/content/article-series/${series.id}` },
    {
      label: `${episodeSequenceLabel(episode.episodeNo)} · ${episode.title}`,
      to: `/content/article-series/${series.id}/episodes/${episode.id}`,
    },
    { label: contentTitle },
  ];
}

export function seriesDetailTrail(series: ContentArticleSeries): ContentLineageTrailItem[] {
  const angle = seriesBrandAngle(series);
  return [
    { label: 'Idea Pool', to: '/content/pool' },
    {
      label: angle || 'Brand Angle',
      to: `/content/packages?package=${series.sourcePackageId}`,
    },
    { label: series.code },
  ];
}

export function episodeDetailTrail(
  series: ContentArticleSeries,
  episode: ContentArticleEpisode,
): ContentLineageTrailItem[] {
  const angle = seriesBrandAngle(series);
  return [
    { label: series.brandName, to: '/content/brands' },
    {
      label: angle || series.sourcePackageTitle,
      to: `/content/packages?package=${series.sourcePackageId}`,
    },
    { label: series.code, to: `/content/article-series/${series.id}` },
    { label: `${episodeSequenceLabel(episode.episodeNo)} · ${episode.title}` },
  ];
}

export function ContentCanonFields({
  rows,
}: {
  rows: { label: string; value?: ReactNode | null }[];
}) {
  return (
    <div style={{ display: 'grid', rowGap: 8 }}>
      {rows.map((row) => {
        const empty = row.value === undefined || row.value === null || row.value === '';
        return (
          <div
            key={row.label}
            style={{ display: 'grid', gridTemplateColumns: 'minmax(148px, 190px) 1fr', columnGap: 12, alignItems: 'start' }}
          >
            <Typography.Text type="secondary">{row.label}</Typography.Text>
            <div>{empty ? <Typography.Text type="secondary">Chưa có</Typography.Text> : row.value}</div>
          </div>
        );
      })}
    </div>
  );
}

export function standaloneContentTrail(
  brandName: string,
  brandAngle?: { label: string; packageId?: string | null } | null,
): ContentLineageTrailItem[] {
  const angle = brandAngle?.label?.trim() ?? '';
  const items: ContentLineageTrailItem[] = [{ label: brandName, to: '/content/brands' }];
  if (angle) {
    items.push({
      label: angle,
      to: brandAngle?.packageId ? `/content/packages?package=${brandAngle.packageId}` : '/content/packages',
    });
  }
  items.push({ label: 'Bài độc lập' });
  return items;
}

export function ContentLineageTrail({ items }: { items: ContentLineageTrailItem[] }) {
  const visible = items.filter((i) => i.label.trim());
  if (visible.length === 0) return null;
  return (
    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
      {visible.map((item, i) => (
        <span key={`${item.label}-${i}`}>
          {i > 0 ? <span style={{ margin: '0 6px' }}>→</span> : null}
          {item.to ? <Link to={item.to}>{item.label}</Link> : item.label}
        </span>
      ))}
    </Typography.Paragraph>
  );
}

export function ContentLineageStack({
  steps,
}: {
  steps: { title: string; body?: string | null; extra?: ReactNode; empty?: string }[];
}) {
  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      {steps.map((step, i) => (
        <div key={step.title}>
          {i > 0 ? (
            <Typography.Text type="secondary" style={{ display: 'block', margin: '0 0 8px 8px' }}>
              ↓
            </Typography.Text>
          ) : null}
          <Card size="small" title={step.title}>
            {step.body?.trim() ? (
              <Typography.Paragraph style={{ marginBottom: step.extra ? 8 : 0 }}>
                {step.body}
              </Typography.Paragraph>
            ) : step.extra ? null : (
              <Typography.Text type="secondary">{step.empty ?? 'Chưa xác định'}</Typography.Text>
            )}
            {step.extra}
          </Card>
        </div>
      ))}
    </Space>
  );
}

export function ContentBrandOpportunityList({
  corePackageId,
  fits,
}: {
  corePackageId: string;
  fits: ContentBrandFit[];
}) {
  if (fits.length === 0) {
    return (
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Chưa có Brand Fit
      </Typography.Text>
    );
  }
  return (
    <div style={{ marginTop: 8 }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Brand Opportunities
      </Typography.Text>
      {fits.map((fit) => {
        const promising = fit.verdict === 'fit' || fit.verdict === 'maybe';
        return (
          <div key={fit.brandId} style={{ marginTop: 4 }}>
            <Space wrap size={6}>
              <Typography.Text strong>{fit.brandName}</Typography.Text>
              <Typography.Text type="secondary">{fit.score}</Typography.Text>
              {promising ? (
                <Typography.Text type="secondary">Có tiềm năng khai thác</Typography.Text>
              ) : fit.verdict ? (
                <Typography.Text type="secondary">{fit.verdict}</Typography.Text>
              ) : null}
              {fit.packageId ? (
                <Link to={`/content/packages?package=${fit.packageId}`}>Xem Góc Brand</Link>
              ) : (
                <Link to={`/content/packages?source=${corePackageId}`}>Tạo Góc Brand</Link>
              )}
            </Space>
          </div>
        );
      })}
    </div>
  );
}

export function asTextList(value: unknown): string[] {
  if (value == null) return [];
  if (typeof value === 'string') return value.trim() ? [value.trim()] : [];
  if (Array.isArray(value)) {
    return value.flatMap((item) => {
      if (typeof item === 'string' && item.trim()) return [item.trim()];
      if (item && typeof item === 'object') {
        const rec = item as Record<string, unknown>;
        for (const key of ['label', 'title', 'name', 'text', 'phase', 'rule']) {
          if (typeof rec[key] === 'string' && String(rec[key]).trim()) return [String(rec[key]).trim()];
        }
      }
      return [];
    });
  }
  return [];
}

export function readBlueprintField(blueprint: unknown, key: string): unknown {
  if (!blueprint || typeof blueprint !== 'object' || Array.isArray(blueprint)) return undefined;
  return (blueprint as Record<string, unknown>)[key];
}

export function readContinuityField(continuity: unknown, key: string): unknown {
  if (!continuity || typeof continuity !== 'object' || Array.isArray(continuity)) return undefined;
  return (continuity as Record<string, unknown>)[key];
}

export function seriesProgressLabel(row: ContentArticleSeries) {
  return `${row.plannedEpisodeRows}/${row.episodeCount} tập · ${row.publishedCount} đã đăng`;
}

export function seriesBlueprintText(blueprint: unknown, key: string) {
  const raw = readBlueprintField(blueprint, key);
  return typeof raw === 'string' ? raw.trim() : '';
}

export function seriesBrandAngle(series: ContentArticleSeries) {
  return seriesBlueprintText(series.blueprint, 'brandAngle') || series.sourcePackageTitle || '';
}

export function seriesTerritory(series: ContentArticleSeries) {
  const id = seriesBlueprintText(series.blueprint, 'territoryId');
  const name = seriesBlueprintText(series.blueprint, 'territory');
  if (id) return name && name !== 'core' && name !== 'off-brand' ? `${name} · ${id}` : id;
  return name;
}

export function seriesCtaStrategy(series: ContentArticleSeries) {
  return (
    seriesBlueprintText(series.blueprint, 'ctaStrategy') ||
    seriesBlueprintText(series.blueprint, 'seriesCta')
  );
}

export function episodeSequenceLabel(episodeNo: number) {
  return `E${String(episodeNo).padStart(2, '0')}`;
}

export function seriesHasEpisodeContent(episodes: { contentTopicId?: string | null }[]) {
  return episodes.some((episode) => Boolean(episode.contentTopicId));
}

export function contentStatusApproved(status?: string | null) {
  const value = (status ?? '').trim().toLowerCase();
  return value === 'approved' || value === 'scheduled' || value === 'published';
}

export function episodeContinuityStatus(episode: ContentArticleEpisode) {
  const mustContinue = asTextList(readContinuityField(episode.continuity, 'mustContinueFrom'));
  if (mustContinue.length > 0 || episode.previousEpisodeId) return 'Tiếp nối';
  if (episode.episodeNo <= 1) return 'Mở đầu';
  return 'Chưa nối';
}

export function qualityGateLabel(gate?: ContentQualityGate | null) {
  if (!gate) return 'Chưa có';
  return gate.passed ? 'PASS' : 'Chưa đạt';
}

export function packageForTopic(packages: ContentPackage[], topicId?: string | null) {
  if (!topicId) return undefined;
  return packages.find((row) => row.topicId === topicId);
}

const VARIANT_KIND_LABEL: Record<string, string> = {
  web_long: 'Website',
  seo_meta: 'SEO',
  fb_page: 'Facebook',
  fb_short: 'Facebook ngắn',
  social_caption: 'Caption',
};

export function variantKindLabel(kind: string) {
  return VARIANT_KIND_LABEL[kind] ?? kind;
}

export async function listSeriesForSourcePackage(sourcePackageId: string) {
  const rows = await fetchContentArticleSeries();
  return rows.filter((row) => row.sourcePackageId === sourcePackageId);
}

export type TopicSeriesLineage = {
  series: ContentArticleSeries;
  episode: ContentArticleEpisode;
};

export async function findTopicSeriesLineage(topicId: string): Promise<TopicSeriesLineage | null> {
  const index = await loadTopicLineageIndex();
  return index.get(topicId) ?? null;
}

export async function loadTopicLineageIndex(): Promise<Map<string, TopicSeriesLineage>> {
  const rows = await fetchContentArticleSeries();
  const index = new Map<string, TopicSeriesLineage>();
  for (const row of rows) {
    const detail = await fetchContentArticleSeriesDetail(row.id);
    for (const episode of detail.episodes) {
      if (episode.contentTopicId) index.set(episode.contentTopicId, { series: detail.series, episode });
    }
  }
  return index;
}

export function useTopicLineageIndex() {
  const [index, setIndex] = useState<Map<string, TopicSeriesLineage> | null>(null);
  useEffect(() => {
    let alive = true;
    void loadTopicLineageIndex().then((next) => {
      if (alive) setIndex(next);
    });
    return () => {
      alive = false;
    };
  }, []);
  return index;
}

export function ContentSeriesNarrative({ blueprint }: { blueprint: unknown }) {
  const arc = asTextList(readBlueprintField(blueprint, 'narrativeArc'));
  const pillars = asTextList(readBlueprintField(blueprint, 'contentPillars'));
  const rules = asTextList(readBlueprintField(blueprint, 'continuityRules'));
  const empty = arc.length === 0 && pillars.length === 0 && rules.length === 0;
  if (empty) {
    return <Typography.Text type="secondary">Chưa có Blueprint</Typography.Text>;
  }
  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      {arc.length > 0 ? (
        <div>
          {arc.map((step, i) => (
            <div key={`${step}-${i}`}>
              {i > 0 ? (
                <Typography.Text type="secondary" style={{ display: 'block', marginLeft: 8 }}>
                  ↓
                </Typography.Text>
              ) : null}
              <Typography.Text>{step}</Typography.Text>
            </div>
          ))}
        </div>
      ) : null}
      {pillars.length > 0 ? (
        <div>
          <Typography.Text type="secondary">Content pillars</Typography.Text>
          <ul style={{ margin: '4px 0 0', paddingLeft: 18 }}>
            {pillars.map((p) => (
              <li key={p}>{p}</li>
            ))}
          </ul>
        </div>
      ) : null}
      {rules.length > 0 ? (
        <div>
          <Typography.Text type="secondary">Continuity</Typography.Text>
          <ul style={{ margin: '4px 0 0', paddingLeft: 18 }}>
            {rules.map((r) => (
              <li key={r}>{r}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </Space>
  );
}

function continuityLines(value: unknown) {
  const lines = asTextList(value);
  if (lines.length === 0) return null;
  return (
    <Space direction="vertical" size={2}>
      {lines.map((line, i) => (
        <div key={`${i}-${line}`}>{line}</div>
      ))}
    </Space>
  );
}

export function ContentEpisodeContinuity({
  continuity,
  previous,
  next,
  seriesId,
}: {
  continuity: unknown;
  previous?: ContentArticleEpisode | null;
  next?: ContentArticleEpisode | null;
  seriesId: string;
}) {
  return (
    <ContentCanonFields
      rows={[
        {
          label: 'Previous Episode',
          value: previous ? (
            <Link to={`/content/article-series/${seriesId}/episodes/${previous.id}`}>
              {episodeSequenceLabel(previous.episodeNo)} · {previous.title}
            </Link>
          ) : null,
        },
        { label: 'Previous Summary', value: continuityLines(readContinuityField(continuity, 'previousSummary')) },
        { label: 'Must Continue From', value: continuityLines(readContinuityField(continuity, 'mustContinueFrom')) },
        { label: 'Must Not Repeat', value: continuityLines(readContinuityField(continuity, 'mustNotRepeat')) },
        { label: 'Open Loop', value: continuityLines(readContinuityField(continuity, 'openLoops')) },
        {
          label: 'Next Episode Direction',
          value: continuityLines(readContinuityField(continuity, 'nextEpisodeDirection')),
        },
        {
          label: 'Next Episode',
          value: next ? (
            <Link to={`/content/article-series/${seriesId}/episodes/${next.id}`}>
              {episodeSequenceLabel(next.episodeNo)} · {next.title}
            </Link>
          ) : null,
        },
      ]}
    />
  );
}

export function EpisodeCreativeBrief({
  brief,
  audience,
  corePoint,
}: {
  brief?: ContentCreativeBrief | null;
  audience?: string | null;
  corePoint?: string | null;
}) {
  const present = Boolean(brief && (brief.objective?.trim() || brief.format?.trim()));
  if (!present) {
    return <Typography.Text type="secondary">Chưa có Creative Brief</Typography.Text>;
  }
  return (
    <ContentCanonFields
      rows={[
        { label: 'Objective', value: brief?.objective?.trim() },
        { label: 'Audience', value: audience?.trim() },
        { label: 'Core Point', value: corePoint?.trim() },
        { label: 'Format', value: brief?.format?.trim() },
      ]}
    />
  );
}

export function ContentRelatedSeriesList({ series }: { series: ContentArticleSeries[] }) {
  if (series.length === 0) {
    return <Typography.Text type="secondary">Chưa có Series</Typography.Text>;
  }
  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      {series.map((row) => (
        <div key={row.id}>
          <Link to={`/content/article-series/${row.id}`}>
            {row.code} · {row.name}
          </Link>
          <div>
            <Tag>{row.status}</Tag>
            <Typography.Text type="secondary">{seriesProgressLabel(row)}</Typography.Text>
          </div>
        </div>
      ))}
    </Space>
  );
}

export function useSeriesForSourcePackage(sourcePackageId?: string | null) {
  const [rows, setRows] = useState<ContentArticleSeries[]>([]);
  useEffect(() => {
    if (!sourcePackageId) {
      setRows([]);
      return;
    }
    let alive = true;
    void listSeriesForSourcePackage(sourcePackageId).then((next) => {
      if (alive) setRows(next);
    });
    return () => {
      alive = false;
    };
  }, [sourcePackageId]);
  return rows;
}

export function PackageLineageBlock({ packageId }: { packageId?: string | null }) {
  const series = useSeriesForSourcePackage(packageId);
  return (
    <Card size="small" title="Series từ góc này">
      <ContentRelatedSeriesList series={series} />
    </Card>
  );
}

export function usePackagesForBrand(brandId?: string | null) {
  const [rows, setRows] = useState<ContentPackage[]>([]);
  useEffect(() => {
    if (!brandId) {
      setRows([]);
      return;
    }
    let alive = true;
    void fetchContentPackages({ brandId }).then((next) => {
      if (alive) setRows(next);
    });
    return () => {
      alive = false;
    };
  }, [brandId]);
  return rows;
}

export function useTopicSeriesLineage(topicId?: string | null) {
  const [lineage, setLineage] = useState<TopicSeriesLineage | null>(null);
  useEffect(() => {
    if (!topicId) {
      setLineage(null);
      return;
    }
    let alive = true;
    void findTopicSeriesLineage(topicId).then((next) => {
      if (alive) setLineage(next);
    });
    return () => {
      alive = false;
    };
  }, [topicId]);
  return lineage;
}
