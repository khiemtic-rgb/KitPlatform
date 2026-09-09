import type { CharacterLibraryViewRow } from '@/shared/api/content.api';
import type { FamixaSceneNode, SeriesPilotState } from './content-famixa-series';
import { viewSlotLabel } from './kit-video-character-library';

export const PRODUCTION_WORKFLOW_HARDENING_ID = 'FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1';

export type ShotProgressMedia = {
  hasStill: boolean;
  hasClip: boolean;
  imageApproved?: boolean;
  videoApproved?: boolean;
};

export type ShotProgressCount = {
  imageMade: boolean;
  imageApproved: boolean;
  videoMade: boolean;
  videoApproved: boolean;
  complete: boolean;
};

export function countScenesAndShots(scenes: { shots: unknown[] }[]) {
  return {
    sceneCount: scenes.length,
    shotCount: scenes.reduce((n, scene) => n + scene.shots.length, 0),
  };
}

export function productionShotProgress(media: ShotProgressMedia): ShotProgressCount {
  const imageMade = Boolean(media.hasStill);
  const imageApproved = imageMade && Boolean(media.imageApproved);
  const videoMade = imageMade && Boolean(media.hasClip);
  const videoApproved = videoMade && Boolean(media.videoApproved);
  return {
    imageMade,
    imageApproved,
    videoMade,
    videoApproved,
    complete: imageApproved && videoApproved,
  };
}

export function sumShotProgress(rows: ShotProgressMedia[]) {
  const items = rows.map(productionShotProgress);
  return {
    shotCount: items.length,
    imageMade: items.filter((x) => x.imageMade).length,
    imageApproved: items.filter((x) => x.imageApproved).length,
    videoMade: items.filter((x) => x.videoMade).length,
    videoApproved: items.filter((x) => x.videoApproved).length,
    complete: items.filter((x) => x.complete).length,
  };
}

export type ProductionProgressInput = {
  scriptLocked: boolean;
  sceneCount: number;
  shotCount: number;
  characterCount: number;
  characterReady: boolean;
  characterNeed?: string;
  imageMade: number;
  imageApproved: number;
  videoMade: number;
  videoApproved: number;
  complete: number;
};

export type ProductionProgressNode = {
  id: 'script' | 'scenes' | 'cast' | 'image' | 'imageReview' | 'video' | 'videoReview' | 'finish' | 'publish';
  label: string;
  done: boolean;
  detail: string;
};

export function deriveProductionProgress(input: ProductionProgressInput) {
  const total = Math.max(input.shotCount, 0);
  const finishReady = total > 0 && input.complete >= total;
  const nodes: ProductionProgressNode[] = [
    { id: 'script', label: 'Kịch bản', done: input.scriptLocked, detail: input.scriptLocked ? 'Hoàn thành' : 'Chưa khóa' },
    {
      id: 'scenes',
      label: 'Chia cảnh',
      done: input.sceneCount > 0,
      detail: input.sceneCount > 0 ? `${input.sceneCount} cảnh · ${total} shot` : 'Chưa chia',
    },
    {
      id: 'cast',
      label: 'Nhân vật',
      done: input.characterReady,
      detail: input.characterReady ? 'Hoàn thành' : input.characterNeed || 'Cần hoàn thiện',
    },
    { id: 'image', label: 'Tạo hình', done: total > 0 && input.imageMade >= total, detail: total ? `${input.imageMade}/${total}` : '0' },
    {
      id: 'imageReview',
      label: 'Duyệt hình',
      done: total > 0 && input.imageApproved >= total,
      detail: total ? `${input.imageApproved}/${total}` : '0',
    },
    { id: 'video', label: 'Tạo video', done: total > 0 && input.videoMade >= total, detail: total ? `${input.videoMade}/${total}` : '0' },
    {
      id: 'videoReview',
      label: 'Duyệt video',
      done: total > 0 && input.videoApproved >= total,
      detail: total ? `${input.videoApproved}/${total}` : '0',
    },
    {
      id: 'finish',
      label: 'Hoàn thiện',
      done: finishReady,
      detail: finishReady ? `${input.complete}/${total}` : 'Chưa sẵn sàng',
    },
    { id: 'publish', label: 'Xuất bản', done: finishReady, detail: finishReady ? 'Sẵn sàng' : 'Chưa sẵn sàng' },
  ];
  const current = nodes.find((n) => !n.done)?.id || 'publish';
  return { nodes, current, finishReady };
}

export function nextProductionAction(input: ProductionProgressInput & { firstOpenShot?: number; blockedName?: string }) {
  const total = input.shotCount;
  const shot = String(input.firstOpenShot ?? 1).padStart(2, '0');
  if (!input.scriptLocked) {
    return { step: 'script' as const, label: 'Hoàn thiện kịch bản', tab: 'script' as const };
  }
  if (input.sceneCount <= 0) {
    return { step: 'scenes' as const, label: 'Chia video thành các cảnh', tab: 'scenes' as const };
  }
  if (!input.characterReady) {
    return {
      step: 'cast' as const,
      label: input.blockedName ? `Hoàn thiện bộ ảnh chuẩn cho ${input.blockedName}` : 'Hoàn thiện bộ ảnh chuẩn',
      tab: 'characters' as const,
    };
  }
  if (total > 0 && input.imageMade < total) {
    return { step: 'image' as const, label: `Tạo hình cho Shot ${shot}`, tab: 'images' as const };
  }
  if (total > 0 && input.imageApproved < total) {
    return { step: 'imageReview' as const, label: `Duyệt hình Shot ${shot}`, tab: 'images' as const };
  }
  if (total > 0 && input.videoMade < total) {
    return { step: 'video' as const, label: `Tạo video cho Shot ${shot}`, tab: 'video' as const };
  }
  if (total > 0 && input.videoApproved < total) {
    return { step: 'videoReview' as const, label: `Duyệt video Shot ${shot}`, tab: 'video' as const };
  }
  if (total > 0 && input.complete < total) {
    return { step: 'finish' as const, label: 'Hoàn thiện tập', tab: 'finish' as const };
  }
  return { step: 'publish' as const, label: 'Xuất bản video', tab: 'publish' as const };
}

export function uniqueCharacterIds(ids: string[]) {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const raw of ids) {
    const id = (raw || '').trim();
    const key = id.toUpperCase();
    if (!key || seen.has(key)) continue;
    seen.add(key);
    out.push(id);
  }
  return out;
}

export function resolveSceneCharacterRefs(requested: string[], knownIds: string[]) {
  const known = new Set(knownIds.map((id) => id.trim().toUpperCase()).filter(Boolean));
  const ids = uniqueCharacterIds(requested);
  for (const id of ids) {
    if (!known.has(id.toUpperCase())) {
      return {
        ok: false as const,
        error: 'Không thể sử dụng nhân vật này',
        hint: 'Nhân vật không có trong thư viện. Không thay bằng nhân vật khác.',
        characterId: id,
      };
    }
  }
  return { ok: true as const, characterIds: ids };
}

export function applySceneCharacterRefs(state: SeriesPilotState, sceneId: string, characterIds: string[]): SeriesPilotState {
  const id = (sceneId || '').trim();
  if (!id) return state;
  const scenes = [...(state.scenes ?? [])];
  const at = scenes.findIndex((scene) => scene.id === id);
  const next: FamixaSceneNode = {
    ...(at >= 0 ? scenes[at] : { id, characterIds: [] }),
    id,
    characterIds: [...characterIds],
    castAssigned: true,
  };
  if (at >= 0) scenes[at] = next;
  else scenes.push(next);
  return { ...state, scenes };
}

export function missingReferenceLabels(types?: string[] | null) {
  return (types ?? []).map((type) => viewSlotLabel(type)).filter(Boolean);
}

export function coverageLabel(ready?: number, total?: number) {
  const have = Math.max(ready ?? 0, 0);
  const all = Math.max(total ?? 0, 0);
  return all ? `${have}/${all}` : '0/4';
}

export function staffReadinessCopy(row?: Pick<CharacterLibraryViewRow, 'canUse' | 'readinessCode' | 'readinessLabel' | 'requiredReady' | 'requiredTotal'> & { missingTypes?: string[] }) {
  if (!row) return { badge: 'CHƯA BẮT ĐẦU', detail: 'Chưa có dữ liệu nhân vật.', action: 'Xem nhân vật' };
  if (row.canUse) return { badge: 'HOÀN THÀNH', detail: 'Sẵn sàng dùng trong cảnh.', action: 'XEM BỘ ẢNH' };
  const missing = missingReferenceLabels(row.missingTypes);
  if ((row.readinessCode || '').toUpperCase().includes('REFERENCE') || (row.requiredReady ?? 0) < (row.requiredTotal ?? 4)) {
    return {
      badge: 'CẦN BỔ SUNG',
      detail: missing.length ? `Thiếu: ${missing.join(', ')}` : 'Bộ ảnh nhân vật chưa hoàn tất.',
      action: 'HOÀN THIỆN BỘ ẢNH',
    };
  }
  if ((row.readinessCode || '').toUpperCase().includes('REVIEW') || row.readinessLabel.includes('duyệt')) {
    return { badge: 'CẦN DUYỆT', detail: row.readinessLabel, action: 'XEM BỘ ẢNH' };
  }
  return { badge: 'BỊ CHẶN', detail: row.readinessLabel || 'Nhân vật chưa sẵn sàng.', action: 'HOÀN THIỆN BỘ ẢNH' };
}

export function staffBlockCopy(code?: string) {
  const raw = (code || '').toUpperCase();
  if (raw.includes('REFERENCE_MISSING') || raw.includes('CRP_DRAFT') || raw.includes('REFERENCE')) {
    return {
      title: 'Bị chặn — Bộ ảnh nhân vật chưa hoàn tất',
      reason: 'Bộ ảnh chuẩn còn thiếu góc bắt buộc.',
      next: 'Hoàn thiện bộ ảnh trước khi tạo hình.',
    };
  }
  if (raw.includes('VIDEO') && (raw.includes('NOT_READY') || raw.includes('BLOCK'))) {
    return {
      title: 'Chưa thể tạo video',
      reason: 'Ảnh tạo hình chưa được duyệt.',
      next: 'Duyệt ảnh trước khi tạo video.',
    };
  }
  if (raw.includes('READY_FOR_DIRECTOR')) {
    return { title: 'Đang chờ duyệt hình', reason: 'Ảnh đã tạo, chưa duyệt.', next: 'Xem và duyệt ảnh.' };
  }
  return { title: 'Bị chặn', reason: 'Bước này chưa sẵn sàng.', next: 'Xem việc tiếp theo trên tập.' };
}

export function episodeStoryLine(input: { sceneCount: number; shotCount: number; characterCount: number }) {
  return `${input.sceneCount} cảnh · ${input.shotCount} shot · ${input.characterCount} nhân vật`;
}
