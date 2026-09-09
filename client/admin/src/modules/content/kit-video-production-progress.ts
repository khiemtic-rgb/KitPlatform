export const PRODUCTION_PROGRESS_SOT_ID = 'FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1';

export type ProductionProgressStageView = { id: string; label: string; done: boolean; detail: string };

export type ProductionProgressView = {
  buildId: string;
  seriesCode?: string;
  episodeCode?: string;
  title?: string;
  sceneCount: number;
  shotCount: number;
  characterCount: number;
  imageProgress: number;
  imageApprovalProgress: number;
  videoProgress: number;
  videoApprovalProgress: number;
  finalization: string;
  publication: string;
  currentStep: string;
  nextAction: string;
  blockingReason?: string | null;
  storyLine: string;
  completedStages: number;
  totalStages: number;
  tone: string;
  stages: ProductionProgressStageView[];
  generate?: boolean;
};

export function progressByBuildId(items?: ProductionProgressView[] | null) {
  const map: Record<string, ProductionProgressView> = {};
  for (const row of items ?? []) {
    if (row.buildId) map[row.buildId] = row;
  }
  return map;
}

export function studioCardFromProgress(row?: ProductionProgressView | null) {
  if (!row) {
    return {
      tone: 'wait' as const,
      label: 'Đang tải tiến độ',
      storyLine: '',
      stageLine: '',
      checks: [] as { id: string; label: string; done: boolean }[],
      next: '',
    };
  }
  const tone = row.tone === 'done' ? ('done' as const) : row.tone === 'work' ? ('work' as const) : ('wait' as const);
  const checks = (row.stages ?? []).map((stage) => ({
    id: stage.id,
    label: `${stage.label} ${stage.detail}`.trim(),
    done: stage.done,
  }));
  return {
    tone,
    label: tone === 'done' ? 'Hoàn thành' : tone === 'work' ? 'Đang làm' : 'Chưa bắt đầu',
    storyLine: row.storyLine,
    stageLine: `${row.completedStages}/${row.totalStages} bước`,
    checks,
    next: row.nextAction,
  };
}

export function isProgressComplete(row?: ProductionProgressView | null) {
  return row?.tone === 'done';
}

export function isProgressWork(row?: ProductionProgressView | null) {
  return row?.tone === 'work';
}
