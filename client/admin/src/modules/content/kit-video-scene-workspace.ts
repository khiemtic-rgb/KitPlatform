import type { CharacterLibraryViewRow } from '@/shared/api/content.api';
import type { FamixaCharacter, FamixaSceneNode, FamixaSeriesShot, SeriesShotRun } from './content-famixa-series';
import { shotCharacterIds } from './content-famixa-series';
import { sceneCodeOf, sceneIdOfShot } from './content-famixa-scene-first';
import { clipOf, stillOf } from './content-famixa-shot-catalog';
import { pageSlice } from './kit-video-character-library';

export const SCENE_PRODUCTION_WORKSPACE_ID = 'SCENE_PRODUCTION_WORKSPACE_V1';

export type ProductionUserModeLike = 'staff' | 'director';

export const SCENE_WORK_STEPS = [
  { id: 'overview', label: 'Tổng quan' },
  { id: 'script', label: 'Kịch bản' },
  { id: 'shots', label: 'Chia shot' },
  { id: 'cast', label: 'Nhân vật' },
  { id: 'image', label: 'Tạo hình' },
  { id: 'video', label: 'Dựng video' },
  { id: 'review', label: 'Duyệt' },
  { id: 'finish', label: 'Hoàn thiện' },
] as const;

export type SceneWorkStep = (typeof SCENE_WORK_STEPS)[number]['id'];

export const SCENE_SHOT_FILTERS = [
  { id: 'all', label: 'Tất cả' },
  { id: 'wait', label: 'Chưa làm' },
  { id: 'work', label: 'Đang làm' },
  { id: 'review', label: 'Chờ duyệt' },
  { id: 'approved', label: 'Đã duyệt' },
  { id: 'fix', label: 'Cần sửa' },
  { id: 'done', label: 'Hoàn thành' },
] as const;

export type SceneShotFilter = (typeof SCENE_SHOT_FILTERS)[number]['id'];

export type SceneStaffStatus =
  | 'not_started'
  | 'prep'
  | 'wait'
  | 'imaging'
  | 'shooting'
  | 'review'
  | 'approved'
  | 'done'
  | 'fix';

export type ShotMedia = { hasStill: boolean; hasClip: boolean; needsFix: boolean };

export type SceneShotView = {
  shot: FamixaSeriesShot;
  index: number;
  beatId: string;
  beatLabel: string;
  media: ShotMedia;
  status: SceneStaffStatus;
  statusLabel: string;
};

export type SceneBeatView = { id: string; label: string; shots: SceneShotView[] };

export type SceneView = {
  id: string;
  number: number;
  title: string;
  goal: string;
  emotion: string;
  location: string;
  seconds: number;
  clock: string;
  characterIds: string[];
  beats: SceneBeatView[];
  shots: SceneShotView[];
  status: SceneStaffStatus;
  statusLabel: string;
};

export type SceneStepMark = { id: SceneWorkStep; label: string; done: boolean; detail: string };

export type SceneNextAction = {
  step: SceneWorkStep;
  label: string;
  shotId?: string;
};

export const SCENE_STATUS_LABEL: Record<SceneStaffStatus, string> = {
  not_started: 'Chưa bắt đầu',
  prep: 'Đang chuẩn bị',
  wait: 'Chưa bắt đầu',
  imaging: 'Đang tạo hình',
  shooting: 'Đang dựng shot',
  review: 'Chờ duyệt',
  approved: 'Đã duyệt',
  done: 'Hoàn thành',
  fix: 'Cần sửa',
};

export function shotMediaOf(run?: SeriesShotRun): ShotMedia {
  const review = run?.review;
  const needsFix = Boolean(review && Object.values(review).some((v) => v === false));
  return { hasStill: Boolean(stillOf(run)), hasClip: Boolean(clipOf(run)), needsFix };
}

export function shotStaffStatus(media: ShotMedia): SceneStaffStatus {
  if (media.needsFix) return 'fix';
  if (media.hasStill && media.hasClip) return 'done';
  if (media.hasStill) return 'review';
  return 'wait';
}

export function shotStaffLabel(status: SceneStaffStatus) {
  if (status === 'wait') return 'Chưa có hình';
  if (status === 'review') return 'Đã có hình';
  if (status === 'done') return 'Đã có hình';
  if (status === 'fix') return 'Cần sửa';
  return SCENE_STATUS_LABEL[status] || 'Chưa làm';
}

export function sceneStaffStatus(shots: SceneShotView[]): SceneStaffStatus {
  if (!shots.length) return 'not_started';
  if (shots.some((s) => s.status === 'fix')) return 'fix';
  const allStill = shots.every((s) => s.media.hasStill);
  const allClip = shots.every((s) => s.media.hasClip);
  const anyStill = shots.some((s) => s.media.hasStill);
  const anyClip = shots.some((s) => s.media.hasClip);
  if (allStill && allClip) return 'done';
  if (allStill && !allClip) return anyClip ? 'shooting' : 'review';
  if (!anyStill && !anyClip) return 'prep';
  if (!allStill) return 'imaging';
  return 'prep';
}

export function formatClock(seconds: number) {
  const s = Math.max(0, Math.round(seconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${String(m).padStart(2, '0')}:${String(r).padStart(2, '0')}`;
}

export function displayPersonName(
  id: string,
  people?: FamixaCharacter[],
  library?: CharacterLibraryViewRow[],
) {
  const code = (id || '').trim();
  if (!code) return 'Nhân vật';
  const lib = library?.find((row) => row.characterId.toUpperCase() === code.toUpperCase());
  if (lib?.displayName || lib?.name) return lib.displayName || lib.name;
  const graph = people?.find((row) => row.id.toUpperCase() === code.toUpperCase());
  if (graph?.name) return graph.name;
  return code;
}

export function locationLabel(raw?: string) {
  const v = (raw || '').trim();
  return v || '';
}

function shotSeconds(shot: FamixaSeriesShot) {
  return shot.editSeconds || shot.seconds || 0;
}

function sceneKey(shot: FamixaSeriesShot) {
  return sceneIdOfShot(shot) || shot.sceneId || shot.scene || shot.id;
}

function beatKey(shot: FamixaSeriesShot) {
  return (shot.beatId || '').trim() || 'beat';
}

function beatLabel(shot: FamixaSeriesShot, fallback: string) {
  const text = (shot.beatText || '').trim();
  return text || fallback;
}

export function buildSceneViews(
  shots: FamixaSeriesShot[],
  runOf: (id: string) => SeriesShotRun,
  nodes?: FamixaSceneNode[],
): SceneView[] {
  const views = shots.map((shot, index) => {
    const media = shotMediaOf(runOf(shot.id));
    const status = shotStaffStatus(media);
    return {
      shot,
      index,
      beatId: beatKey(shot),
      beatLabel: beatLabel(shot, 'Beat'),
      media,
      status,
      statusLabel: shotStaffLabel(status),
    } satisfies SceneShotView;
  });

  const byScene = new Map<string, SceneShotView[]>();
  for (const row of views) {
    const key = sceneKey(row.shot);
    const list = byScene.get(key) ?? [];
    list.push(row);
    byScene.set(key, list);
  }

  const usedShots = new Set<string>();
  const scenes: SceneView[] = [];
  let number = 1;

  const pushScene = (id: string, node: FamixaSceneNode | undefined, sceneShots: SceneShotView[]) => {
    if (!sceneShots.length && !node) return;
    sceneShots.forEach((row) => usedShots.add(row.shot.id));
    const beats = groupBeats(sceneShots, node);
    const characterIds = node?.castAssigned
      ? uniqueIds(node.characterIds ?? [])
      : uniqueIds([
          ...(node?.characterIds ?? []),
          ...sceneShots.flatMap((row) => shotCharacterIds(row.shot)),
        ]);
    const seconds = sceneShots.reduce((sum, row) => sum + shotSeconds(row.shot), 0);
    const status = sceneStaffStatus(sceneShots);
    const title =
      (node?.title || '').trim() ||
      (sceneShots[0]?.shot.scene || '').replace(/^SC\s*\d+\s*[–-]\s*/i, '').trim() ||
      `Cảnh ${String(number).padStart(2, '0')}`;
    scenes.push({
      id,
      number,
      title,
      goal: (node?.content || sceneShots[0]?.shot.story || title).trim(),
      emotion: (node?.performance || '').trim(),
      location: locationLabel(node?.environment || sceneShots[0]?.shot.location),
      seconds,
      clock: sceneShots[0]?.shot.clock || formatClock(seconds),
      characterIds,
      beats,
      shots: sceneShots,
      status,
      statusLabel: SCENE_STATUS_LABEL[status],
    });
    number += 1;
  };

  if (nodes?.length) {
    for (const node of nodes) {
      const code = sceneCodeOf(node.id) || node.id;
      const sceneShots = views.filter(
        (row) =>
          row.shot.sceneId === node.id ||
          sceneKey(row.shot) === node.id ||
          sceneKey(row.shot) === code,
      );
      pushScene(node.id, node, sceneShots);
    }
  }
  for (const [id, sceneShots] of byScene) {
    if (sceneShots.every((row) => usedShots.has(row.shot.id))) continue;
    pushScene(id, undefined, sceneShots.filter((row) => !usedShots.has(row.shot.id)));
  }
  return scenes;
}

function groupBeats(shots: SceneShotView[], node?: FamixaSceneNode): SceneBeatView[] {
  if (node?.scriptBeats?.length) {
    const used = new Set<string>();
    const groups = node.scriptBeats.map((beat, i) => {
      const rows = shots.filter((row) => beat.shotIds.includes(row.shot.id) || row.shot.beatId === beat.id);
      rows.forEach((row) => used.add(row.shot.id));
      return {
        id: beat.id || `beat-${i + 1}`,
        label: (beat.text || '').trim() || `Beat ${String(i + 1).padStart(2, '0')}`,
        shots: rows,
      };
    });
    const leftover = shots.filter((row) => !used.has(row.shot.id));
    if (leftover.length) groups.push({ id: 'beat-more', label: 'Shot khác', shots: leftover });
    return groups.filter((g) => g.shots.length);
  }
  const map = new Map<string, SceneShotView[]>();
  for (const row of shots) {
    const list = map.get(row.beatId) ?? [];
    list.push(row);
    map.set(row.beatId, list);
  }
  if (map.size <= 1) {
    return [{ id: shots[0]?.beatId || 'beat', label: shots[0]?.beatLabel || 'Shot', shots }];
  }
  return [...map.entries()].map(([id, rows], i) => ({
    id,
    label: rows[0]?.beatLabel || `Beat ${String(i + 1).padStart(2, '0')}`,
    shots: rows,
  }));
}

export function sceneCastFaceSource(
  row?: Pick<CharacterLibraryViewRow, 'frontPackId' | 'frontItemId' | 'masterLocked'> | null,
  studio?: {
    officialLocked?: boolean;
    masterSha256?: string | null;
    slots?: { type: string; present: boolean }[];
  } | null,
) {
  if (row?.frontPackId && row?.frontItemId) return 'crp';
  const front = studio?.slots?.some((s) => s.type.toUpperCase() === 'FRONT' && s.present);
  if (front) return 'studio-front';
  if (studio?.officialLocked || studio?.masterSha256 || row?.masterLocked) return 'studio-master';
  return null;
}

export function sceneCastCopy(
  row?: Pick<CharacterLibraryViewRow, 'canUse' | 'readinessReason' | 'readinessLabel'> | null,
  studio?: { officialLocked?: boolean } | null,
) {
  if (row?.canUse) {
    return {
      mark: '✓ Sẵn sàng',
      reason: row.readinessReason || 'Nhân vật đã khóa và sẵn sàng dùng trong cảnh.',
    };
  }
  if (studio?.officialLocked) {
    return {
      mark: '✓ Đã khóa hồ sơ',
      reason: 'Hồ sơ Character Studio đã khóa. Thiếu bộ 4 góc kho Nhân vật — chưa tạo hình cảnh.',
    };
  }
  if (row) {
    return {
      mark: '⚠ Cần bổ sung',
      reason: row.readinessReason || row.readinessLabel || 'Chưa đủ dữ liệu nhân vật.',
    };
  }
  return { mark: '✕ Chưa đủ dữ liệu', reason: undefined };
}

function uniqueIds(ids: string[]) {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const id of ids) {
    const key = id.trim().toUpperCase();
    if (!key || seen.has(key)) continue;
    seen.add(key);
    out.push(id.trim());
  }
  return out;
}

export function sceneStepMarks(
  scene: SceneView,
  library?: CharacterLibraryViewRow[],
  scriptLocked?: boolean,
): SceneStepMark[] {
  const hasScript = Boolean(scene.goal || scriptLocked);
  const hasShots = scene.shots.length > 0;
  const castReady =
    scene.characterIds.length === 0 ||
    scene.characterIds.every((id) => library?.find((row) => row.characterId.toUpperCase() === id.toUpperCase())?.canUse);
  const images = scene.shots.filter((s) => s.media.hasStill).length;
  const imageReviews = scene.shots.filter((s) => s.media.hasStill && (s.status === 'review' || s.status === 'approved' || s.status === 'done')).length;
  const videos = scene.shots.filter((s) => s.media.hasStill && s.media.hasClip).length;
  const videoReviews = scene.shots.filter((s) => s.status === 'done' || s.status === 'approved').length;
  const finished = scene.shots.filter((s) => s.media.hasStill && s.media.hasClip && (s.status === 'done' || s.status === 'approved')).length;
  const total = scene.shots.length;
  return [
    { id: 'script', label: 'Kịch bản', done: hasScript, detail: hasScript ? '✓' : '○' },
    { id: 'shots', label: 'Chia cảnh', done: hasShots, detail: hasShots ? `${total} shot` : 'Chưa chia' },
    { id: 'cast', label: 'Nhân vật', done: Boolean(castReady && scene.characterIds.length), detail: castReady && scene.characterIds.length ? '✓' : 'Cần hoàn thiện' },
    { id: 'image', label: 'Tạo hình', done: total > 0 && images >= total, detail: total ? `${images}/${total}` : '0' },
    { id: 'review', label: 'Duyệt hình', done: total > 0 && imageReviews >= total, detail: total ? `${imageReviews}/${total}` : '0' },
    { id: 'video', label: 'Tạo video', done: total > 0 && videos >= total, detail: total ? `${videos}/${total}` : '0' },
    { id: 'finish', label: 'Hoàn thiện', done: total > 0 && finished >= total, detail: total > 0 && finished >= total ? `${finished}/${total}` : 'Chưa sẵn sàng' },
  ];
}

export function nextSceneAction(
  scene: SceneView,
  marks: SceneStepMark[],
  library?: CharacterLibraryViewRow[],
): SceneNextAction {
  const blocked = scene.characterIds
    .map((id) => library?.find((row) => row.characterId.toUpperCase() === id.toUpperCase()))
    .find((row) => row && !row.canUse);
  if (blocked) {
    return {
      step: 'cast',
      label: `Hoàn thiện bộ ảnh chuẩn cho ${blocked.displayName || blocked.name || 'nhân vật'}`,
    };
  }
  if (!scene.characterIds.length) return { step: 'cast', label: 'Chọn nhân vật cho cảnh' };
  const fix = scene.shots.find((s) => s.status === 'fix');
  if (fix) return { step: 'review', label: `Sửa shot ${String(fix.index + 1).padStart(2, '0')}`, shotId: fix.shot.id };
  const needImage = scene.shots.find((s) => !s.media.hasStill);
  if (needImage) return { step: 'image', label: `Tạo hình cho Shot ${String(needImage.index + 1).padStart(2, '0')}`, shotId: needImage.shot.id };
  const needReview = scene.shots.find((s) => s.status === 'review');
  if (needReview) return { step: 'review', label: `Duyệt hình shot ${String(needReview.index + 1).padStart(2, '0')}`, shotId: needReview.shot.id };
  const needVideo = scene.shots.find((s) => s.media.hasStill && !s.media.hasClip);
  if (needVideo) return { step: 'video', label: `Dựng video shot ${String(needVideo.index + 1).padStart(2, '0')}`, shotId: needVideo.shot.id };
  const open = marks.find((m) => !m.done);
  if (open) return { step: open.id, label: `Tiếp tục: ${open.label}` };
  return { step: 'finish', label: 'Cảnh đã hoàn thành' };
}

export function filterSceneShots(shots: SceneShotView[], filter: SceneShotFilter, query: string, characterId?: string) {
  const q = query.trim().toLowerCase();
  return shots.filter((row) => {
    if (filter !== 'all') {
      if (filter === 'wait' && row.status !== 'wait') return false;
      if (filter === 'work' && row.status !== 'review' && row.status !== 'imaging' && row.status !== 'shooting') return false;
      if (filter === 'review' && row.status !== 'review') return false;
      if (filter === 'approved' && row.status !== 'approved' && row.status !== 'done') return false;
      if (filter === 'fix' && row.status !== 'fix') return false;
      if (filter === 'done' && row.status !== 'done') return false;
    }
    if (characterId && !shotCharacterIds(row.shot).some((id) => id.toUpperCase() === characterId.toUpperCase())) {
      return false;
    }
    if (!q) return true;
    return `${row.index + 1} ${row.shot.story || ''} ${row.shot.scene || ''} ${row.beatLabel}`.toLowerCase().includes(q);
  });
}

export function staffErrorCopy(code?: string) {
  const raw = (code || '').toUpperCase();
  if (!raw) return { title: 'Bị chặn', hint: 'Bước này chưa sẵn sàng.' };
  if (raw.includes('REFERENCE_MISSING') || raw.includes('CRP_DRAFT') || raw.includes('REFERENCE')) {
    return { title: 'Bị chặn — Bộ ảnh nhân vật chưa hoàn tất', hint: 'Hoàn thiện bộ ảnh trước khi tạo hình.' };
  }
  if (raw.includes('IDENTITY') || raw.includes('DNA')) {
    return { title: 'Bị chặn', hint: 'Hình ảnh nhân vật chưa khớp nhận diện đã khóa.' };
  }
  if (raw.includes('VIDEO_GENERATION_NOT_READY') || raw.includes('VIDEO') || raw.includes('PROVIDER') || raw.includes('CAPABILITY')) {
    return { title: 'Chưa thể tạo video', hint: 'Cần duyệt hình trước khi tạo video.' };
  }
  if (raw.includes('READY_FOR_DIRECTOR')) {
    return { title: 'Đang chờ duyệt hình', hint: 'Xem ảnh rồi duyệt.' };
  }
  return { title: 'Bị chặn', hint: 'Bước này chưa sẵn sàng.' };
}

export function sceneActionsFromMedia(media: ShotMedia) {
  return {
    canEdit: false,
    canCreateShot: false,
    canCreateImage: false,
    canApproveImage: media.hasStill,
    canCreateVideo: false,
    canApproveVideo: media.hasClip,
    canComplete: media.hasClip,
  };
}

export { pageSlice };
