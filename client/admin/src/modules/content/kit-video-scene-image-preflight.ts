import { CAST_NAME } from './content-famixa-shot-catalog';
import { canonRowOf, normCanonId } from './content-famixa-char-canon';
import {
  characterProductionGuard,
  type FamixaCharacterRecord,
} from './content-famixa-character-memory';
import {
  FAMIXA_VISUAL_MODE,
  type VisualReferenceClaim,
  resolveSceneVisualReferences,
} from './kit-video-visual-mode';

export const SCENE_IMAGE_PREFLIGHT_ID = 'FAMIXA_SCENE_IMAGE_PREFLIGHT_V1';

export type SceneImageCheckId =
  | 'script'
  | 'characters'
  | 'visual_style'
  | 'visual_mode'
  | 'character_lock'
  | 'master_reference'
  | 'voice'
  | 'shot_graph'
  | 'scene_master'
  | 'scene'
  | 'provider';

export type SceneImageActionId =
  | 'open_character_studio'
  | 'open_character_view'
  | 'open_character_universe'
  | 'open_script'
  | 'open_voice'
  | 'open_scenes'
  | 'open_visual_style'
  | 'open_config'
  | 'lock_voice'
  | 'approve_shot_graph'
  | 'set_aspect';

export type SceneImageAction = {
  id: SceneImageActionId;
  label: string;
  characterId?: string;
  shotId?: string;
  aspect?: '16:9' | '9:16';
  stay?: boolean;
  destLabel?: string;
};

export type SceneImageBlocker = {
  code: string;
  type: SceneImageCheckId;
  entityId?: string;
  title: string;
  message: string;
  action: SceneImageAction;
};

export function sceneImageActionStays(id: SceneImageActionId) {
  return id === 'lock_voice' || id === 'approve_shot_graph' || id === 'set_aspect';
}

export type SceneImageCheck = {
  id: SceneImageCheckId;
  ok: boolean;
  label: string;
};

export type SceneVisualRefRow = {
  characterId: string;
  name: string;
  locked: boolean;
  visual: string;
  eligible: boolean;
};

export type SceneImagePreflight = {
  allowed: boolean;
  checks: SceneImageCheck[];
  blockers: SceneImageBlocker[];
  visualRefs?: SceneVisualRefRow[];
  visualCompatibility?: 'PASS' | 'BLOCKED';
  legacyEligibleCount?: number;
};

export type SceneStudioLockRow = {
  characterId: string;
  characterName?: string;
  officialLocked?: boolean;
  masterSha256?: string | null;
  version?: string | null;
};

export type SceneImagePreflightInput = {
  characterIds: string[];
  registry: FamixaCharacterRecord[];
  studioRows?: SceneStudioLockRow[];
  scriptLocked?: boolean;
  voiceReady?: boolean;
  shotGraphLocked?: boolean;
  sceneMasterLocked?: boolean;
  visualStyleReady?: boolean;
  visualStyleKnown?: boolean;
  aspectOk?: boolean;
  hasGemini?: boolean;
  shotAllowsGeneration?: boolean;
  sceneLabel?: string;
  projectVisualMode?: string;
  visualReferences?: VisualReferenceClaim[];
};

export function sceneCharacterCode(id: string) {
  return canonRowOf(id)?.id || normCanonId(id);
}

export function findStudioLockRow(id: string, studioRows?: SceneStudioLockRow[]) {
  const code = sceneCharacterCode(id);
  const name = (canonRowOf(code)?.name || '').toLowerCase();
  return (studioRows ?? []).find((row) => {
    const rowId = sceneCharacterCode(row.characterId);
    const rowName = (row.characterName || '').trim().toLowerCase();
    return rowId === code || (!!name && rowName === name);
  });
}

export function directorCharacterLocked(
  id: string,
  registry?: FamixaCharacterRecord[],
  studioRows?: SceneStudioLockRow[],
) {
  if (findStudioLockRow(id, studioRows)?.officialLocked) return true;
  const code = sceneCharacterCode(id);
  const row = (registry ?? []).find((r) => normCanonId(r.characterCode) === code);
  return (row?.lifecycle || '').toLowerCase() === 'locked';
}

export function sceneCharacterName(
  id: string,
  registry?: FamixaCharacterRecord[],
  studioRows?: SceneStudioLockRow[],
) {
  const code = sceneCharacterCode(id);
  const row = (registry ?? []).find((r) => normCanonId(r.characterCode) === code);
  if (row?.name?.trim()) return row.name.trim();
  const studio = findStudioLockRow(code, studioRows);
  if (studio?.characterName?.trim()) return studio.characterName.trim();
  return CAST_NAME[code] || canonRowOf(code)?.name || code || id;
}

function codeFromBlocked(text: string) {
  return text.match(/CHAR-\d+/i)?.[0]?.toUpperCase();
}

function pushCheck(
  checks: SceneImageCheck[],
  id: SceneImageCheckId,
  ok: boolean,
  label: string,
) {
  const cur = checks.find((c) => c.id === id);
  if (cur) {
    cur.ok = cur.ok && ok;
    return;
  }
  checks.push({ id, ok, label });
}

export function canGenerateScene(input: SceneImagePreflightInput): SceneImagePreflight {
  const checks: SceneImageCheck[] = [];
  const blockers: SceneImageBlocker[] = [];
  const ids = [...new Set((input.characterIds ?? []).map((id) => sceneCharacterCode(id)).filter(Boolean))];
  const studioById = new Map(
    (input.studioRows ?? []).map((row) => [sceneCharacterCode(row.characterId), row] as const),
  );

  const scriptOk = Boolean(input.scriptLocked);
  pushCheck(checks, 'script', scriptOk, 'Kịch bản');
  if (!scriptOk) {
    blockers.push({
      code: 'SCRIPT_NOT_LOCKED',
      type: 'script',
      title: 'Kịch bản chưa khóa',
      message: 'Khóa kịch bản rồi mới tạo hình ảnh production.',
      action: { id: 'open_script', label: 'Sang tab Kịch bản', destLabel: 'Kịch bản' },
    });
  }

  const voiceOk = input.voiceReady !== false;
  pushCheck(checks, 'voice', voiceOk, 'Thoại');
  if (input.voiceReady === false) {
    blockers.push({
      code: 'VOICE_NOT_LOCKED',
      type: 'voice',
      title: 'Thoại chưa khóa',
      message: 'Khóa thoại trên bàn này nếu đã TTS xong. Nếu chưa, máy mở đúng tab Thoại rồi quay lại tạo ảnh.',
      action: { id: 'lock_voice', label: 'Khóa thoại', stay: true },
    });
  }

  const graphOk = input.shotGraphLocked !== false;
  pushCheck(checks, 'shot_graph', graphOk, 'Cách chia shot');
  if (input.shotGraphLocked === false) {
    blockers.push({
      code: 'SHOT_GRAPH_NOT_LOCKED',
      type: 'shot_graph',
      title: 'Chưa duyệt cách chia shot',
      message: 'Duyệt danh sách shot đang có — ở lại màn Hình ảnh. Không vẽ shot rỗng.',
      action: { id: 'approve_shot_graph', label: 'Duyệt cách chia shot', stay: true },
    });
  }

  const masterOk = input.sceneMasterLocked !== false;
  pushCheck(checks, 'scene_master', masterOk, 'Scene Master');
  if (input.sceneMasterLocked === false) {
    blockers.push({
      code: 'SCENE_MASTER_NOT_LOCKED',
      type: 'scene_master',
      title: 'Scene Master chưa khóa',
      message: 'Khóa Scene Master của cảnh này rồi mới tạo hình.',
      action: { id: 'open_scenes', label: 'Sang tab Chia cảnh', destLabel: 'Chia cảnh' },
    });
  }

  if (input.visualStyleKnown) {
    const styleOk = Boolean(input.visualStyleReady);
    pushCheck(checks, 'visual_style', styleOk, 'Visual style');
    if (!styleOk) {
      blockers.push({
        code: 'VISUAL_STYLE_NOT_READY',
        type: 'visual_style',
        title: 'Visual style chưa sẵn',
        message: 'Cần Project Visual Style trước khi tạo hình ảnh production.',
        action: { id: 'open_visual_style', label: 'Mở Visual Style' },
      });
    }
  } else {
    pushCheck(checks, 'visual_style', true, 'Visual style');
  }

  const projectMode = input.projectVisualMode || FAMIXA_VISUAL_MODE.visualMode;
  const visualClaims = input.visualReferences ?? [];
  const visualResolved = visualClaims.length
    ? resolveSceneVisualReferences(visualClaims, projectMode)
    : { allowed: Boolean(projectMode), blocked: [] as { code?: string }[], attachable: [] as VisualReferenceClaim[] };
  const visualModeOk = Boolean(projectMode) && (visualClaims.length === 0 || visualResolved.allowed);
  pushCheck(checks, 'visual_mode', visualModeOk, 'Visual Mode');
  if (!visualModeOk) {
    blockers.push({
      code: visualResolved.blocked[0]?.code || 'VISUAL_MODE_CONFLICT',
      type: 'visual_mode',
      title: 'Visual Mode không khớp Project',
      message: 'Scene không được dùng reference photoreal / Canon cũ. Khóa Character Studio cùng Visual Mode với Project.',
      action: { id: 'open_character_studio', label: 'Mở Character Studio' },
    });
  }

  const visualRefs: SceneVisualRefRow[] = visualClaims.map((claim) => ({
    characterId: claim.characterId,
    name: claim.name,
    locked: (claim.authorityStatus || '').toUpperCase() === 'LOCKED' && claim.source === 'CHARACTER_STUDIO',
    visual: claim.visualStyle || claim.visualMode,
    eligible: visualResolved.attachable.some((a) => a.characterId === claim.characterId),
  }));
  const legacyEligibleCount = visualClaims.filter((c) =>
    (c.source === 'CANON_SEED' || c.referenceStatus === 'LEGACY')
    && visualResolved.attachable.some((a) => a.characterId === c.characterId),
  ).length;

  const sceneOk = input.shotAllowsGeneration !== false && input.aspectOk !== false;
  pushCheck(checks, 'scene', sceneOk, 'Cấu hình cảnh');
  if (input.shotAllowsGeneration === false) {
    blockers.push({
      code: 'SHOT_NOT_ELIGIBLE',
      type: 'scene',
      title: 'Shot chưa đủ điều kiện tạo ảnh',
      message: 'Shot này HOLD/SKIP hoặc chưa có Action hợp lệ — không vẽ KF.',
      action: { id: 'open_scenes', label: 'Xem shot trên tab Chia cảnh', destLabel: 'Chia cảnh' },
    });
  } else if (input.aspectOk === false) {
    blockers.push({
      code: 'ASPECT_NOT_SET',
      type: 'scene',
      title: 'Chưa chọn khung xuất',
      message: 'Chọn 16:9 hoặc 9:16 ngay dưới đây. Cả tập dùng một khung khi vẽ KF.',
      action: { id: 'set_aspect', label: 'Chọn khung xuất', stay: true },
    });
  }

  if (input.hasGemini === false) {
    pushCheck(checks, 'provider', false, 'Gemini');
    blockers.push({
      code: 'GEMINI_KEY_MISSING',
      type: 'provider',
      title: 'Chưa có Gemini API key',
      message: 'Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.',
      action: { id: 'open_config', label: 'Mở cấu hình AI' },
    });
  } else {
    pushCheck(checks, 'provider', true, 'Gemini');
  }

  const charactersExist = ids.every((id) =>
    (input.registry ?? []).some((row) => normCanonId(row.characterCode) === id),
  );
  pushCheck(checks, 'characters', ids.length === 0 || charactersExist, 'Nhân vật');

  const guard = characterProductionGuard({
    registry: input.registry ?? [],
    characterIds: ids,
  });
  let lockOk = true;
  let masterOkChars = true;
  for (const text of guard.blocked) {
    const code = codeFromBlocked(text);
    const name = code ? sceneCharacterName(code, input.registry, input.studioRows) : 'Nhân vật';
    const studioLocked = code
      ? Boolean(findStudioLockRow(code, input.studioRows)?.officialLocked || studioById.get(code)?.officialLocked)
      : false;
    if (/REQUEST CREATION/i.test(text)) {
      if (studioLocked || (code && findStudioLockRow(code, input.studioRows))) continue;
      lockOk = false;
      blockers.push({
        code: 'CHARACTER_MISSING',
        type: 'characters',
        entityId: code,
        title: `${name} chưa có trong registry`,
        message: 'Không tự tạo Canon. Mở Character Studio để xem nhân vật còn thiếu.',
        action: {
          id: 'open_character_studio',
          label: 'Mở Character Studio',
          characterId: code,
        },
      });
      continue;
    }
    if (/FRONT/i.test(text)) {
      if (studioLocked) continue;
      masterOkChars = false;
      blockers.push({
        code: 'MASTER_REFERENCE_MISSING',
        type: 'master_reference',
        entityId: code,
        title: `${name} thiếu Master Reference FRONT`,
        message: 'Cần ảnh FRONT đã khóa trước khi đưa nhân vật vào production.',
        action: {
          id: 'open_character_studio',
          label: 'Mở Character Studio',
          characterId: code,
        },
      });
      continue;
    }
    if (/chưa LOCK|APPROVE\/LOCK/i.test(text)) {
      if (studioLocked) continue;
      lockOk = false;
      blockers.push({
        code: 'CHARACTER_NOT_LOCKED',
        type: 'character_lock',
        entityId: code,
        title: `${name} chưa LOCK`,
        message: `Để khuôn mặt và tạo hình không đổi giữa các shot, ${name} phải được Director duyệt và khóa Master trước khi vào production. APPROVED chưa đủ.`,
        action: { id: 'open_character_studio', label: 'Mở Character Studio', characterId: code },
      });
      continue;
    }
    if (/Voice ID/i.test(text)) {
      blockers.push({
        code: 'CHARACTER_VOICE_MISSING',
        type: 'characters',
        entityId: code,
        title: `${name} có thoại nhưng chưa Voice ID`,
        message: 'Gắn Voice ID cho nhân vật rồi mới tạo hình có thoại.',
        action: { id: 'open_character_studio', label: 'Mở Character Studio', characterId: code },
      });
      continue;
    }
    lockOk = false;
    blockers.push({
      code: 'CHARACTER_BLOCKED',
      type: 'characters',
      entityId: code,
      title: `${name} chưa đủ điều kiện production`,
      message: text,
      action: { id: 'open_character_studio', label: 'Mở Character Studio', characterId: code },
    });
  }

  for (const id of ids) {
    const studio = findStudioLockRow(id, input.studioRows) || studioById.get(id);
    if (!studio || studio.officialLocked) continue;
    if (blockers.some((b) => b.entityId === id && (b.code === 'CHARACTER_NOT_LOCKED' || b.code === 'MASTER_REFERENCE_MISSING'))) {
      continue;
    }
    masterOkChars = false;
    const name = sceneCharacterName(id, input.registry, input.studioRows);
    blockers.push({
      code: 'MASTER_REFERENCE_NOT_LOCKED',
      type: 'master_reference',
      entityId: id,
      title: `Master Reference của ${name} chưa khóa`,
      message: `Duyệt và khóa Master Reference của ${name} trong Character Studio trước khi tạo hình.`,
      action: { id: 'open_character_studio', label: 'Mở Character Studio', characterId: id },
    });
  }

  const lockCheck =
    ids.length === 0
    || ids.every((id) => directorCharacterLocked(id, input.registry, input.studioRows));
  pushCheck(checks, 'character_lock', lockOk && lockCheck, 'Khóa nhân vật');
  pushCheck(checks, 'master_reference', masterOkChars, 'Master Reference');
  if (ids.length && !charactersExist) {
    lockOk = false;
  }

  const seen = new Set<string>();
  const unique = blockers.filter((b) => {
    const key = `${b.code}:${b.entityId || b.title}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });

  return {
    allowed: unique.length === 0,
    checks,
    blockers: unique,
    visualRefs,
    visualCompatibility: visualModeOk ? 'PASS' : 'BLOCKED',
    legacyEligibleCount,
  };
}
