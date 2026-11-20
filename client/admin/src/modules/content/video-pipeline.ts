/** Brand-owned video pipeline. Mirrors VideoPipelineRules. Workspace comes from the film row. */

export const VIDEO_PIPELINE_WORKFLOWS = [
  {
    value: 'famixa_series',
    label: 'Phim theo series',
    description: 'Phim nhiều tập có liên kết về câu chuyện, nhân vật hoặc chủ đề.',
  },
  {
    value: 'factory_short',
    label: 'Video ngắn độc lập',
    description: 'Mỗi video là một nội dung riêng, tập trung truyền tải một thông điệp.',
  },
  {
    value: 'guided_production',
    label: 'Sản xuất phim theo kịch bản',
    description: 'Phát triển phim từ kịch bản, tổ chức phân cảnh và thực hiện các bước sản xuất theo cấu hình.',
  },
] as const;

export function videoWorkflowLabel(workflowKey: string | null | undefined) {
  return VIDEO_PIPELINE_WORKFLOWS.find((row) => row.value === workflowKey)?.label || '';
}

export function videoWorkflowDescription(workflowKey: string | null | undefined) {
  return VIDEO_PIPELINE_WORKFLOWS.find((row) => row.value === workflowKey)?.description || '';
}

export const VIDEO_PIPELINE_STEPS = [
  { value: 'script', label: 'Kịch bản' },
  { value: 'scene', label: 'Cảnh' },
  { value: 'shot', label: 'Phân cảnh' },
  { value: 'voice', label: 'Thoại' },
  { value: 'image', label: 'Hình' },
  { value: 'video', label: 'Video' },
  { value: 'review', label: 'Duyệt' },
  { value: 'export', label: 'Xuất' },
] as const;

export const VIDEO_PIPELINE_ASPECTS = ['', '16:9', '9:16', '1:1'] as const;

export type VideoWorkspace = 'series_continuity' | 'short_factory' | 'guided' | 'unconfigured';

const LEGACY_SERIES_CATALOG = 'FAMIXA';

/** One script surface. Short and guided films use the film desk. A series film keeps the story desk. */
export function filmScriptSurfaces(workflowKey: string | null | undefined): Array<'film' | 'story'> {
  if (workflowKey === 'factory_short' || workflowKey === 'guided_production') return ['film'];
  return ['story'];
}

export function usesFilmScriptDesk(workflowKey: string | null | undefined) {
  return filmScriptSurfaces(workflowKey)[0] === 'film';
}

/** Stored Novixa film. The proven MP4 is the ElevenLabs stock-motion proof, not the nav brand and not the Famixa shot bench. */
export const NOVIXA_LOCAL_BRAND = 'novixa';
export const NOVIXA_LOCAL_TEMPLATE = 'tiktok_45s_hooks';
/** Các thương hiệu khác dùng chung luồng ảnh/giọng/video Novixa (mã thương hiệu, chữ thường). */
export const NOVIXA_FLOW_BRANDS = ['novixa', 'trading', 'trading-agent', 'trading_agent', 'tradingagent'];

export function usesNovixaLocalVideo(film: {
  filmTypeId?: string | null;
  workflowKey?: string | null;
  brandCode?: string | null;
} | null | undefined) {
  if (!NOVIXA_FLOW_BRANDS.includes((film?.brandCode || '').trim().toLowerCase())) return false;
  if (!(film?.filmTypeId || '').trim()) return false;
  const workflow = (film?.workflowKey || '').trim();
  return workflow === 'factory_short' || workflow === 'guided_production';
}

/** Novixa motion desk. A stored famixa_series pipeline stays on that desk. The nav brand is not read. */
export function opensNovixaMotion(film: {
  filmTypeId?: string | null;
  workflowKey?: string | null;
  brandCode?: string | null;
  seriesCode?: string | null;
} | null | undefined) {
  if ((film?.workflowKey || '').trim() === 'famixa_series') return false;
  if (usesNovixaLocalVideo(film)) return true;
  return (film?.seriesCode || '').trim().toLowerCase() === NOVIXA_LOCAL_BRAND;
}

export type CharacterDesk = 'famixa' | 'unavailable' | 'unconfigured';

/**
 * Famixa Character Studio belongs to a film only when the stored pipeline is
 * famixa_series, or the film has no pipeline and its series catalog is FAMIXA.
 * Opening the series workspace is not that evidence. The nav brand is not read.
 */
export function characterDeskForFilm(film: {
  filmTypeId?: string | null;
  workflowKey?: string | null;
  seriesCode?: string | null;
} | null | undefined): CharacterDesk {
  const workflow = (film?.workflowKey || '').trim();
  if (workflow === 'famixa_series') return 'famixa';
  if (workflow === 'factory_short' || workflow === 'guided_production') return 'unavailable';
  if (workflow) return 'unconfigured';
  const typed = (film?.filmTypeId || '').trim().length > 0;
  const series = (film?.seriesCode || '').trim().toUpperCase();
  if (!typed && series === LEGACY_SERIES_CATALOG) return 'famixa';
  return 'unconfigured';
}

/** What ?build= shows. A shot query is an explicit edit. The first shot is not opened for a film desk. */
export function reopenFilmSurface(film: {
  filmTypeId?: string | null;
  workflowKey?: string | null;
  seriesCode?: string | null;
  shot?: string | null;
}) {
  const workspace = resolveVideoWorkspace(film);
  const workflowKey =
    (film.workflowKey || '').trim() ||
    (workspace === 'short_factory' ? 'factory_short' : workspace === 'guided' ? 'guided_production' : '');
  const filmDesk = usesFilmScriptDesk(workflowKey);
  const shot = (film.shot || '').trim();
  return {
    workspace,
    workflowKey,
    script: filmDesk ? ('film' as const) : ('story' as const),
    autoOpenShot: shot ? ('url' as const) : filmDesk ? ('none' as const) : ('first' as const),
  };
}

export function workspaceForWorkflow(workflowKey: string | null | undefined): VideoWorkspace {
  if (workflowKey === 'famixa_series') return 'series_continuity';
  if (workflowKey === 'factory_short') return 'short_factory';
  if (workflowKey === 'guided_production') return 'guided';
  return 'unconfigured';
}

export function resolveVideoWorkspace(film: {
  filmTypeId?: string | null;
  workflowKey?: string | null;
  seriesCode?: string | null;
}): VideoWorkspace {
  if ((film.filmTypeId || '').trim()) return workspaceForWorkflow(film.workflowKey);
  if ((film.seriesCode || '').trim()) return 'series_continuity';
  return 'unconfigured';
}

/** Undefined keeps the existing series chrome. */
export function videoSeriesLine(film: {
  filmTypeId?: string | null;
  seriesCode?: string | null;
  seriesLabel?: string | null;
  pipelineName?: string | null;
}): string | undefined {
  const label = (film.seriesLabel || '').trim();
  if (label) return label;
  if (!(film.filmTypeId || '').trim() && (film.seriesCode || '').trim() === LEGACY_SERIES_CATALOG) return undefined;
  const name = (film.pipelineName || '').trim();
  if (name) return name;
  const code = (film.seriesCode || '').trim();
  if (!code || code === LEGACY_SERIES_CATALOG) return undefined;
  return code;
}

export function offeredForNewFilm(status: string | null | undefined) {
  return status === 'active';
}

export type PipelineCreateChoice<T> = {
  active: T[];
  /** Set only when exactly one active pipeline can be used without asking. */
  selected?: T;
};

/** Active pipelines only. One pipeline is selected. Several stay open unless a single default is set. */
export function createPipelineChoice<T extends { status: string; isDefault?: boolean }>(rows: T[]): PipelineCreateChoice<T> {
  const active = rows.filter((row) => offeredForNewFilm(row.status));
  if (active.length === 1) return { active, selected: active[0] };
  const defaults = active.filter((row) => row.isDefault);
  if (defaults.length === 1) return { active, selected: defaults[0] };
  return { active };
}

export function seriesCodeForPipeline(pipelineCode: string | null | undefined): string | null {
  const clean = (pipelineCode || '')
    .trim()
    .toUpperCase()
    .replace(/-/g, '_')
    .replace(/[^A-Z0-9_]/g, '');
  if (clean.length < 2 || clean.length > 64 || clean[0] < 'A' || clean[0] > 'Z') return null;
  return clean;
}

export function retargetRefusal(storedPipelineId: string | null | undefined, requestedPipelineId: string | null | undefined) {
  const stored = (storedPipelineId || '').trim();
  const requested = (requestedPipelineId || '').trim();
  if (!stored || !requested || stored === requested) return null;
  return 'Đổi pipeline của phim cần thao tác chuyển riêng.';
}
