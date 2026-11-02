import type { ProductionShotActions, ProductionShotState } from '../../shared/api/content.api';
import type { FamixaSeriesShot } from './content-famixa-series';
import type { DirectorDeskCta } from './ContentFamixaShotProduction/ShotProductionCta';

export const PRODUCTION_OS_ACTION_AUTHORITY_ID = 'FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_AUTHORITY_V1';
export const PRODUCTION_OS_UI_CUTOVER_ID = 'FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1';

/** TEST sandbox ScopeId / buildId from ProductionTestSandboxIds. Fixture only. */
export const PRODUCTION_OS_TEST_SCOPE_ID = 'bbbbbbbb-0000-4000-8000-000000000371';
export const PRODUCTION_OS_TEST_SHOT_CODES = ['TEST-SC01-SH01', 'TEST-SC01-SH02'] as const;

export const PRODUCTION_OS_CTA: Record<string, string> = {
  CREATE_VIDEO: 'Tạo video',
  CREATE_IMAGE: 'Tạo hình',
  CREATE_VOICE: 'Tạo thoại',
  CREATE_LIPSYNC: 'Khớp môi',
  MIX_VIDEO: 'Hoàn thiện',
  ACCEPT: 'Dùng video này',
  ASSEMBLE: 'Lắp tập',
  ASSEMBLE_EPISODE: 'Lắp tập phim',
  REGENERATE: 'Tạo lại',
};

export type OsDeskCtaAction =
  | 'os-create-video'
  | 'os-create-image'
  | 'os-create-voice'
  | 'os-create-lipsync'
  | 'os-create-mix'
  | 'os-accept'
  | 'os-assemble'
  | 'os-regenerate';

const DESK_FOR_ACTION: Record<string, OsDeskCtaAction> = {
  CREATE_VIDEO: 'os-create-video',
  CREATE_IMAGE: 'os-create-image',
  CREATE_VOICE: 'os-create-voice',
  CREATE_LIPSYNC: 'os-create-lipsync',
  MIX_VIDEO: 'os-create-mix',
  ACCEPT: 'os-accept',
  ASSEMBLE: 'os-assemble',
  REGENERATE: 'os-regenerate',
};

export const OS_ACTION_FOR_DESK: Record<OsDeskCtaAction, string> = {
  'os-create-video': 'CREATE_VIDEO',
  'os-create-image': 'CREATE_IMAGE',
  'os-create-voice': 'CREATE_VOICE',
  'os-create-lipsync': 'CREATE_LIPSYNC',
  'os-create-mix': 'MIX_VIDEO',
  'os-accept': 'ACCEPT',
  'os-assemble': 'ASSEMBLE',
  'os-regenerate': 'REGENERATE',
};

export function osActionRow(catalog: ProductionShotActions | undefined | null, action: string) {
  return catalog?.actions?.find((row) => row.action === action);
}

export function isOsActionAvailable(catalog: ProductionShotActions | undefined | null, action: string) {
  const row = osActionRow(catalog, action);
  return Boolean(row && row.available === true);
}

/** Visible Production OS CTAs from GET /actions. Fail-closed if catalog missing. */
export function visibleProductionOsCatalogActions(catalog: ProductionShotActions | undefined | null) {
  return (catalog?.actions || [])
    .filter((row) => row.available === true && Boolean(PRODUCTION_OS_CTA[row.action]))
    .map((row) => ({
      action: row.action,
      label: row.action === 'CREATE_VOICE' ? 'Nạp thoại' : row.action === 'ACCEPT' ? 'Dùng kết quả' : PRODUCTION_OS_CTA[row.action],
    }));
}

export function productionOsCtaLabel(action: string) {
  return PRODUCTION_OS_CTA[action];
}

export const SHOT_IDENTITY_MISMATCH = 'IDENTITY_MISMATCH';
export const SHOT_IDENTITY_NOT_FOUND = 'SHOT_NOT_FOUND';

export function isProductionOsTestShot(shotCode?: string) {
  const id = (shotCode || '').trim();
  return (PRODUCTION_OS_TEST_SHOT_CODES as readonly string[]).includes(id);
}

export function isProductionOsTestBuild(buildId?: string) {
  return (buildId || '').trim().toLowerCase() === PRODUCTION_OS_TEST_SCOPE_ID;
}

export function looksLikeProductionShotCode(shotCode?: string) {
  const id = (shotCode || '').trim();
  return /^(EP\d+|TEST)-SC\d+-SH\d+$/i.test(id);
}

/** Canonical identity is (BuildId, ShotId). ShotId is not globally unique. */
export function shotIdentityBelongs(buildId?: string, shotCode?: string) {
  const build = (buildId || '').trim();
  const shot = (shotCode || '').trim();
  if (!build || !shot) return false;
  return isProductionOsTestBuild(build) === isProductionOsTestShot(shot);
}

export function resolveShotWorkspaceIdentity(input: {
  requestedBuildId?: string;
  requestedShotId?: string;
  loadedBuildId?: string;
  loadedShotId?: string;
}) {
  const requestedBuildId = (input.requestedBuildId || '').trim();
  const requestedShotId = (input.requestedShotId || '').trim();
  const loadedBuildId = (input.loadedBuildId || '').trim();
  const loadedShotId = (input.loadedShotId || '').trim();
  if (!requestedBuildId || !requestedShotId) {
    return {
      ok: false as const,
      reason: SHOT_IDENTITY_NOT_FOUND,
      requestedBuildId,
      requestedShotId,
      loadedBuildId,
      loadedShotId,
    };
  }
  if (!shotIdentityBelongs(requestedBuildId, requestedShotId)) {
    return {
      ok: false as const,
      reason: SHOT_IDENTITY_MISMATCH,
      requestedBuildId,
      requestedShotId,
      loadedBuildId: loadedBuildId || (isProductionOsTestShot(requestedShotId) ? PRODUCTION_OS_TEST_SCOPE_ID : requestedBuildId),
      loadedShotId: loadedShotId || requestedShotId,
    };
  }
  if (loadedBuildId && loadedBuildId.toLowerCase() !== requestedBuildId.toLowerCase()) {
    return {
      ok: false as const,
      reason: SHOT_IDENTITY_MISMATCH,
      requestedBuildId,
      requestedShotId,
      loadedBuildId,
      loadedShotId,
    };
  }
  if (loadedShotId && loadedShotId !== requestedShotId) {
    return {
      ok: false as const,
      reason: SHOT_IDENTITY_MISMATCH,
      requestedBuildId,
      requestedShotId,
      loadedBuildId: loadedBuildId || requestedBuildId,
      loadedShotId,
    };
  }
  return {
    ok: true as const,
    reason: undefined,
    requestedBuildId,
    requestedShotId,
    loadedBuildId: loadedBuildId || requestedBuildId,
    loadedShotId: loadedShotId || requestedShotId,
  };
}

export function formatShotIdentityError(row: {
  reason?: string;
  requestedBuildId?: string;
  requestedShotId?: string;
  loadedBuildId?: string;
  loadedShotId?: string;
}) {
  return [
    row.reason || SHOT_IDENTITY_MISMATCH,
    `Requested BuildId: ${row.requestedBuildId || '—'}`,
    `Requested ShotId: ${row.requestedShotId || '—'}`,
    `Loaded BuildId: ${row.loadedBuildId || '—'}`,
    `Loaded ShotId: ${row.loadedShotId || '—'}`,
  ].join('\n');
}

/** API BuildId for a requested pair. Never remaps official film onto the TEST sandbox. */
export function productionOsBuildIdForShot(filmBuildId: string | undefined, shotCode: string) {
  const build = (filmBuildId || '').trim();
  const shot = (shotCode || '').trim();
  if (!shotIdentityBelongs(build, shot)) return undefined;
  return build;
}

/** Fixture POST is allowed only on the TEST sandbox. Film EP01/EP99 never mutate. */
export function mayMutateProductionOs(buildId?: string, shotCode?: string) {
  if (isProductionOsTestShot(shotCode)) return true;
  return (buildId || '').trim().toLowerCase() === PRODUCTION_OS_TEST_SCOPE_ID;
}

/** One paid Gemini still on a tenant EP01 shot. Official films and SH03 stay closed. */
export function mayAuthorizeTenantEp01Image(buildId?: string, shotCode?: string) {
  const build = (buildId || '').trim().toLowerCase();
  const shot = (shotCode || '').trim().toUpperCase();
  if (!build || OFFICIAL_FILM_BUILD_IDS.includes(build)) return false;
  if (!/^EP01-SC\d+-SH\d+$/.test(shot)) return false;
  return shot !== 'EP01-SC01-SH03';
}

/** Historical official films stay closed. Any other local EP01 build may post ASSEMBLE_EPISODE. */
const OFFICIAL_FILM_BUILD_IDS = [
  '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
  '77262ec0-74ad-459d-9128-f09bb553a5dd',
  'fd760913-0eb8-46a5-b6eb-ec2990a4b0d0',
  'fca0c122-797e-4356-9bd1-2a70daac1ed4',
];

export const EPISODE_ASSEMBLY_SHOT_CODE = 'EP01';
export const EPISODE_ASSEMBLY_ORDER = ['EP01-SC01-SH01', 'EP01-SC01-SH02'] as const;

export function mayPostProductionEpisode(buildId?: string) {
  const build = (buildId || '').trim().toLowerCase();
  if (!build) return false;
  return !OFFICIAL_FILM_BUILD_IDS.includes(build);
}

export function productionOsTestShotStub(shotCode: (typeof PRODUCTION_OS_TEST_SHOT_CODES)[number]): FamixaSeriesShot {
  const n = shotCode.endsWith('SH02') ? '02' : '01';
  return {
    id: shotCode,
    scene: 'TEST-SC01',
    shot: n,
    clock: '0:00',
    seconds: 5,
    story: 'TEST fixture story',
    visual: 'TEST fixture visual',
    characters: ['CHAR-TEST'],
    location: 'TEST',
    motionPrompt: 'TEST',
    motionPromptVi: 'TEST',
    status: 'story_locked',
    sceneId: 'TEST-SC01',
    characterIds: ['CHAR-TEST'],
    dialogueSegmentIds: [],
  };
}

export function productionOsTestShotStubs(): FamixaSeriesShot[] {
  return PRODUCTION_OS_TEST_SHOT_CODES.map(productionOsTestShotStub);
}

/** Director list for the open build only. Never mix TEST stubs onto an official film. */
export function productionOsDeskShots(buildId: string | undefined, filmShots: FamixaSeriesShot[]): FamixaSeriesShot[] {
  if (isProductionOsTestBuild(buildId)) return productionOsTestShotStubs();
  return filmShots.filter((s) => !isProductionOsTestShot(s.id));
}

export function productionOsIdempotencyKey(action: string, buildId: string, shotCode: string, extra?: string) {
  return ['os-ui', action, buildId, shotCode, extra].filter(Boolean).join(':');
}

export function productionNodeArtifactId(state: ProductionShotState | undefined | null, node: string) {
  const id = (state?.nodes || []).find((row) => row.node === node)?.artifactId;
  return (id || '').trim() || undefined;
}

/**
 * OS-controlled CURRENT:VOICE identity.
 * AUTHORITY = Production OS VOICE node / GET /voice.
 * Never kit-famixa-tts, voiceId, provider URL, or latest line audio.
 */
export function resolveCurrentVoiceArtifactId(state: ProductionShotState | undefined | null) {
  return productionNodeArtifactId(state, 'VOICE');
}

export function formatProductionActionReason(catalog: ProductionShotActions | undefined | null, action?: string) {
  if (action) {
    const row = osActionRow(catalog, action);
    if (!row) return `${action} · UNAVAILABLE`;
    if (row.available) return undefined;
    const inputs = (row.requiredInputs || []).filter(Boolean).join(', ');
    return [row.action, row.blockReason || 'UNAVAILABLE', inputs || undefined].filter(Boolean).join(' · ');
  }
  return (catalog?.actions || [])
    .filter((row) => PRODUCTION_OS_CTA[row.action] && row.available !== true)
    .map((row) => [row.action, row.blockReason || 'UNAVAILABLE'].filter(Boolean).join(' · '))
    .slice(0, 3)
    .join(' | ');
}

export function productionOsLegacyRefuseNote(action: string, endpoint: string) {
  return `${action} · LEGACY ${endpoint} · NON-AUTHORITY · dùng Production OS`;
}

/** Always true at runtime. A boolean return keeps the closed legacy body type-checked. */
export function productionOsLegacyPathClosed(): boolean {
  return true;
}

export const VOICE_PRODUCTION_OS_NOT_READY = 'Voice Production OS chưa sẵn sàng';
export const VIDEO_TAKE_CURRENT_INPUT = 'VIDEO_TAKE_CURRENT';

export const OFFICIAL_EP01_BUILD_ID = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
export const OFFICIAL_SH02_SHOT_CODE = 'EP01-SC01-SH02';
export const OFFICIAL_SH02_VIDEO_TAKE_ID = 'b44e530b-5b59-422f-9b80-e058a3a639b5';
export const OFFICIAL_SH02_LINE_ID = 'line-SC01-CHAR-001-2';
export const OFFICIAL_SH02_LINE_TEXT = '(lý nhí) Dạ… nay con làm bài không tốt.';
export const OFFICIAL_SH02_SPEAKER_ID = 'CHAR-001';
export const OFFICIAL_SH02_VOICE_ID = 'ywBZEqUhld86Jeajq94o';
export const OFFICIAL_SH02_PROVIDER_ID = 'elevenlabs';
export const OFFICIAL_SH02_MODEL_ID = 'eleven_v3';
export const OFFICIAL_SH02_DURATION_SEC = 10;
export const OFFICIAL_SH02_VOICE_ARTIFACT_ID = '0fde86c2-250f-4303-bda0-4913a6dbd1e4';
export const LIPSYNC_PRODUCTION_OS_NOT_READY = 'LipSync Production OS chưa sẵn sàng';
export const OFFICIAL_SH02_LIPSYNC_PROVIDER_ID = 'fal';
export const OFFICIAL_SH02_LIPSYNC_MODEL_ID = 'sync-lipsync-v3';

export type OfficialVoiceIngressTake = {
  empty?: boolean;
  artifactId?: string | null;
  role?: string | null;
  acceptanceStatus?: string | null;
};

/** CK22 — SH02 official CREATE_VOICE when ACCEPTED:VIDEO_TAKE matches the locked parent. Other shots stay preflight-only. */
export function prepareOfficialVoiceIngress(input: {
  buildId?: string;
  shotCode?: string;
  take?: OfficialVoiceIngressTake | null;
}) {
  const buildId = (input.buildId || '').trim();
  const shotCode = (input.shotCode || '').trim();
  const take = input.take;
  if (!buildId || !shotCode) {
    return { ok: false, execute: false, artifactId: undefined as string | undefined, reason: VOICE_PRODUCTION_OS_NOT_READY };
  }
  const artifactId = (take?.artifactId || '').trim();
  const videoTake =
    !take?.empty &&
    (take?.role || '').trim() === 'VIDEO_TAKE' &&
    Boolean(artifactId);
  const accepted = videoTake && (take?.acceptanceStatus || '').trim() === 'ACCEPTED';
  const currentVideo = videoTake && (take?.acceptanceStatus || '').trim() === 'CURRENT';
  if (!accepted && !currentVideo) {
    return { ok: false, execute: false, artifactId: undefined, reason: VOICE_PRODUCTION_OS_NOT_READY };
  }
  if (currentVideo) {
    return { ok: true, execute: false, artifactId, reason: VIDEO_TAKE_CURRENT_INPUT };
  }
  const officialSh02 =
    buildId.toLowerCase() === OFFICIAL_EP01_BUILD_ID &&
    shotCode === OFFICIAL_SH02_SHOT_CODE &&
    artifactId.toLowerCase() === OFFICIAL_SH02_VIDEO_TAKE_ID;
  if (officialSh02) {
    return {
      ok: true,
      execute: true,
      artifactId,
      reason: undefined as string | undefined,
      lineId: OFFICIAL_SH02_LINE_ID,
      lineText: OFFICIAL_SH02_LINE_TEXT,
      speakerCharacterId: OFFICIAL_SH02_SPEAKER_ID,
      voiceId: OFFICIAL_SH02_VOICE_ID,
      providerId: OFFICIAL_SH02_PROVIDER_ID,
      modelId: OFFICIAL_SH02_MODEL_ID,
      durationSec: OFFICIAL_SH02_DURATION_SEC,
    };
  }
  return { ok: true, execute: false, artifactId, reason: VOICE_PRODUCTION_OS_NOT_READY };
}

export type OfficialLipsyncIngressParents = {
  video?: OfficialVoiceIngressTake | null;
  voice?: { empty?: boolean; artifactId?: string | null; role?: string | null } | null;
};

/** CK23 — SH02 official CREATE_LIPSYNC when ACCEPTED:VIDEO_TAKE + ACCEPTED/CURRENT:VOICE match locked parents. */
export function prepareOfficialLipsyncIngress(input: {
  buildId?: string;
  shotCode?: string;
  video?: OfficialVoiceIngressTake | null;
  voice?: { empty?: boolean; artifactId?: string | null; role?: string | null } | null;
}) {
  const buildId = (input.buildId || '').trim();
  const shotCode = (input.shotCode || '').trim();
  if (!buildId || !shotCode) {
    return { ok: false, execute: false, reason: LIPSYNC_PRODUCTION_OS_NOT_READY };
  }
  const videoId = (input.video?.artifactId || '').trim();
  const voiceId = (input.voice?.artifactId || '').trim();
  const videoOk =
    !input.video?.empty &&
    (input.video?.role || '').trim() === 'VIDEO_TAKE' &&
    (input.video?.acceptanceStatus || '').trim() === 'ACCEPTED' &&
    videoId.toLowerCase() === OFFICIAL_SH02_VIDEO_TAKE_ID;
  const voiceRole = (input.voice?.role || '').trim();
  const voiceOk =
    !input.voice?.empty &&
    (voiceRole === 'VOICE' || voiceRole.endsWith(':VOICE')) &&
    voiceId.toLowerCase() === OFFICIAL_SH02_VOICE_ARTIFACT_ID;
  const recoveredVideo =
    !input.video?.empty &&
    (input.video?.role || '').trim() === 'VIDEO_TAKE' &&
    (input.video?.acceptanceStatus || '').trim() === 'CURRENT' &&
    Boolean(videoId) &&
    buildId.toLowerCase() !== OFFICIAL_EP01_BUILD_ID;
  if (recoveredVideo) {
    return { ok: true, execute: false, reason: VIDEO_TAKE_CURRENT_INPUT, videoArtifactId: videoId };
  }
  const officialSh02 =
    buildId.toLowerCase() === OFFICIAL_EP01_BUILD_ID && shotCode === OFFICIAL_SH02_SHOT_CODE;
  if (!officialSh02 || !videoOk || !voiceOk) {
    return { ok: false, execute: false, reason: LIPSYNC_PRODUCTION_OS_NOT_READY };
  }
  return {
    ok: true,
    execute: true,
    reason: undefined as string | undefined,
    videoArtifactId: videoId,
    voiceArtifactId: voiceId,
    lineId: OFFICIAL_SH02_LINE_ID,
    lineText: OFFICIAL_SH02_LINE_TEXT,
    speakerCharacterId: OFFICIAL_SH02_SPEAKER_ID,
    providerId: OFFICIAL_SH02_LIPSYNC_PROVIDER_ID,
    modelId: OFFICIAL_SH02_LIPSYNC_MODEL_ID,
  };
}

export function formatOfficialVideoProviderLine(providerId?: string | null, modelId?: string | null) {
  const p = (providerId || '').trim();
  const m = (modelId || '').trim();
  if (p.toLowerCase() === 'google' && m.toLowerCase().includes('veo-3.1')) return 'Google / Veo 3.1';
  if (!p && !m) return '';
  return [p, m].filter(Boolean).join(' / ');
}

export function isLegacyCreateVideoCta(cta?: { label?: string; action?: string } | null) {
  if (!cta) return false;
  if (cta.action === 'os-create-video') return true;
  if (cta.action === 'os-regenerate') return false;
  const label = (cta.label || '').trim();
  if (label === 'Tạo lại video' || label === 'Tạo lại') return false;
  if (cta.action === 'motion') return label === 'Tạo video' || !label;
  return label === 'Tạo video';
}

export function createVideoDependencyNote(catalog: ProductionShotActions | undefined | null) {
  return formatProductionActionReason(catalog, 'CREATE_VIDEO');
}

type DeskLike<T> = T & {
  pendingApproval: boolean;
  phase?: 'picture' | 'motion' | 'finish';
  note: string;
  technical?: string;
  primary?: DirectorDeskCta;
  secondary?: DirectorDeskCta;
  tertiary?: DirectorDeskCta;
};

function compactDeskCtas<T extends DeskLike<unknown>>(desk: T): T {
  const slots = [desk.primary, desk.secondary, desk.tertiary].filter(Boolean);
  return {
    ...desk,
    primary: slots[0],
    secondary: slots[1],
    tertiary: slots[2],
  };
}

export function productionActionForDeskCta(cta?: DirectorDeskCta | null): string | undefined {
  if (!cta) return undefined;
  if (cta.action && cta.action in OS_ACTION_FOR_DESK) return OS_ACTION_FOR_DESK[cta.action as OsDeskCtaAction];
  const label = (cta.label || '').trim();
  if (label === 'Tạo video' || cta.action === 'motion') return label === 'Tạo lại video' || label === 'Tạo lại' ? 'REGENERATE' : 'CREATE_VIDEO';
  if (label === 'Tạo lại video' || label === 'Tạo lại') return 'REGENERATE';
  if (label === 'Tạo hình' || label === 'Tạo hình mới' || cta.action === 'picture' || cta.action === 'os-create-image')
    return 'CREATE_IMAGE';
  if (label === 'Tạo thoại' || label === 'Nạp thoại' || cta.action === 'os-create-voice') return 'CREATE_VOICE';
  if (
    label === 'Khớp môi' ||
    label === 'Khớp môi mới' ||
    label === 'Hỏi lại môi' ||
    cta.action === 'lipsync' ||
    cta.action === 'os-create-lipsync'
  )
    return 'CREATE_LIPSYNC';
  if (label === 'Hoàn thiện' || label === 'Hoàn thiện Shot' || cta.action === 'mix' || cta.action === 'os-create-mix')
    return 'MIX_VIDEO';
  if (
    label === 'Dùng video này' ||
    label === 'Dùng kết quả' ||
    label === 'Dùng kết quả này' ||
    cta.action === 'use-current-take' ||
    cta.action === 'accept-existing' ||
    cta.action === 'approve-video' ||
    cta.action === 'os-accept'
  )
    return 'ACCEPT';
  if (label === 'Lắp tập' || cta.action === 'os-assemble') return 'ASSEMBLE';
  return undefined;
}

function osDeskCta(action: string): DirectorDeskCta | undefined {
  const desk = DESK_FOR_ACTION[action];
  const label = PRODUCTION_OS_CTA[action];
  if (!desk || !label) return undefined;
  return { label, action: desk };
}

function remapProductionCta(cta: DirectorDeskCta | undefined, catalog: ProductionShotActions | undefined | null) {
  const action = productionActionForDeskCta(cta);
  if (!action) return cta;
  if (!isOsActionAvailable(catalog, action)) return undefined;
  return osDeskCta(action);
}

/** Fail-closed. Production CTAs exist only when GET /actions says available === true. */
export function applyProductionOsActionAuthority<T extends DeskLike<unknown>>(
  desk: T,
  catalog: ProductionShotActions | undefined | null,
): T {
  let next = compactDeskCtas({
    ...desk,
    primary: remapProductionCta(desk.primary, catalog),
    secondary: remapProductionCta(desk.secondary, catalog),
    tertiary: remapProductionCta(desk.tertiary, catalog),
  });

  const reasons = [
    formatProductionActionReason(catalog, 'CREATE_VIDEO'),
    formatProductionActionReason(catalog, 'CREATE_LIPSYNC'),
    formatProductionActionReason(catalog, 'MIX_VIDEO'),
  ].filter(Boolean);
  if (reasons.length) next = { ...next, technical: reasons.join('\n') };

  if (isOsActionAvailable(catalog, 'ASSEMBLE') && !productionActionForDeskCta(next.primary) && !productionActionForDeskCta(next.secondary) && !productionActionForDeskCta(next.tertiary)) {
    const assemble = osDeskCta('ASSEMBLE');
    if (!next.primary) next = { ...next, primary: assemble };
    else if (!next.secondary) next = { ...next, secondary: assemble };
    else if (!next.tertiary) next = { ...next, tertiary: assemble };
  } else if (isOsActionAvailable(catalog, 'ASSEMBLE') && !next.tertiary) {
    next = { ...next, tertiary: osDeskCta('ASSEMBLE') };
  }

  const video = osActionRow(catalog, 'CREATE_VIDEO');
  if (!video || video.available !== true) return next;
  if (next.primary?.action === 'os-create-video') return next;
  const osCta = osDeskCta('CREATE_VIDEO');
  if (next.pendingApproval || next.primary?.action === 'approve-picture') {
    if (!next.secondary) return { ...next, secondary: osCta };
    if (!next.tertiary) return { ...next, tertiary: osCta };
    return next;
  }
  if (!next.primary) {
    return {
      ...next,
      phase: 'motion',
      primary: osCta,
      secondary: next.secondary,
    };
  }
  return next;
}

export function mayExecuteProductionAction(catalog: ProductionShotActions | undefined | null, action: string) {
  if (!PRODUCTION_OS_CTA[action]) return false;
  return isOsActionAvailable(catalog, action);
}

/** Card / recovery CTA. Hide production execution unless GET /actions available === true. */
export function authorizeOsDeskCta(cta?: DirectorDeskCta | null, catalog?: ProductionShotActions | null) {
  if (!cta || cta.action === 'blocked') return undefined;
  const action = productionActionForDeskCta(cta);
  if (!action) return cta;
  if (!isOsActionAvailable(catalog, action)) return undefined;
  return osDeskCta(action);
}

export function isCreateVideoRecoveryType(type?: string) {
  return type === 'CONFIRM_MOTION' || type === 'RETRY_MOTION';
}
