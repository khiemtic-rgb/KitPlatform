import type { ContentSeriesBuildSummary } from '@/shared/api/content.api';
import { SERIES_BUILD_STATUS_VI, type SeriesBuildStatus } from './content-famixa-build';
import {
  isProgressComplete,
  isProgressWork,
  studioCardFromProgress,
  type ProductionProgressView,
} from './kit-video-production-progress';

export const SERIES_DESK_ID = 'FAMIXA_SERIES_DESK_V1';

export function buildStatusKey(raw?: string): SeriesBuildStatus {
  if (raw && raw in SERIES_BUILD_STATUS_VI) return raw as SeriesBuildStatus;
  return 'draft';
}

export function videoCardStatus(row: ContentSeriesBuildSummary, progress?: ProductionProgressView | null) {
  const card = studioCardFromProgress(progress ?? undefined);
  if (progress) return { tone: card.tone, label: card.label, step: progress.currentStep };
  const key = buildStatusKey(row.status);
  if (key === 'final') return { tone: 'wait' as const, label: 'Đang tải tiến độ', step: 'overview' };
  if (key === 'draft') return { tone: 'wait' as const, label: 'Chưa bắt đầu', step: 'script' };
  return { tone: 'work' as const, label: 'Đang làm', step: 'overview' };
}

export function videoCardProgress(_row: ContentSeriesBuildSummary, progress?: ProductionProgressView | null) {
  if (!progress || !progress.totalStages) return 0;
  return Math.round((progress.completedStages / progress.totalStages) * 100);
}

export function videoCardChecks(_row: ContentSeriesBuildSummary, progress?: ProductionProgressView | null) {
  if (!progress) return [];
  return studioCardFromProgress(progress).checks;
}

export function studioBuildName(row: { episodeCode?: string; title?: string }) {
  const code = (row.episodeCode || 'Tập mới').trim() || 'Tập mới';
  const title = (row.title || 'Chưa đặt tên').trim() || 'Chưa đặt tên';
  return `${code} · ${title}`;
}

export function studioBuildLabel(
  row: ContentSeriesBuildSummary,
  peers: { episodeCode?: string; title?: string; shotCount?: number }[],
) {
  const name = studioBuildName(row);
  const sameName = peers.filter((peer) => studioBuildName(peer) === name).length > 1;
  if (!sameName) return name;
  return `${name} · ${Number(row.shotCount) || 0} shot`;
}

export function studioBuildCounts(row: ContentSeriesBuildSummary) {
  const shots = Number(row.shotCount) || 0;
  const kf = Number(row.kfCount) || 0;
  const video = Number(row.videoCount) || 0;
  const voice = Number(row.voiceLines) || 0;
  return `Thoại ${voice} · Hình ${kf}/${shots} · Video ${video}/${shots}`;
}

export function suggestNextEpisodeCode(rows: { episodeCode?: string }[]) {
  const nums = rows.map((r) => Number((r.episodeCode || '').match(/\d+/)?.[0] || 0));
  const next = Math.max(0, ...nums) + 1;
  return `EP${String(next).padStart(2, '0')}`;
}

export function videoPrimaryAction(row: ContentSeriesBuildSummary, progress?: ProductionProgressView | null) {
  const st = videoCardStatus(row, progress);
  if (st.tone === 'done') return 'Xem video';
  if (st.tone === 'wait' && !progress) return 'Bắt đầu';
  return 'Tiếp tục →';
}

export function studioNextAction(_row: ContentSeriesBuildSummary, progress?: ProductionProgressView | null) {
  if (progress?.nextAction) return { step: progress.currentStep, need: progress.nextAction };
  return { step: 'overview', need: 'Đang tải tiến độ' };
}

export type StudioFilter = 'all' | 'work' | 'done' | 'paused';

export function filterStudioBuilds(
  rows: ContentSeriesBuildSummary[],
  filter: StudioFilter,
  query: string,
  progressById?: Record<string, ProductionProgressView>,
) {
  const q = query.trim().toLowerCase();
  return rows
    .filter((row) => {
      const progress = progressById?.[row.id];
      if (filter === 'work') return isProgressWork(progress) || (!progress && buildStatusKey(row.status) !== 'draft');
      if (filter === 'done') return isProgressComplete(progress);
      if (filter === 'paused') return false;
      return true;
    })
    .filter((row) => {
      if (!q) return true;
      return `${row.episodeCode} ${row.title} ${row.shotCount} ${row.kfCount} ${row.videoCount}`.toLowerCase().includes(q);
    })
    .sort((a, b) => +new Date(b.updatedAt) - +new Date(a.updatedAt));
}

export const STUDIO_PAGE_SIZE = 20;
