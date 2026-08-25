import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Collapse,
  Drawer,
  Input,
  InputNumber,
  Segmented,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import {
  CheckOutlined,
  CopyOutlined,
  DeleteOutlined,
  DownloadOutlined,
  FolderOpenOutlined,
  LinkOutlined,
  LockOutlined,
  PlusOutlined,
  ReloadOutlined,
  SoundOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import './content-famixa-studio.css';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  draftContentSeriesScript,
  fetchContentBrands,
  fetchContentSeriesPilot,
  fetchContentSeriesVoices,
  fetchContentSettings,
  generateContentSeriesStill,
  qaContentSeriesStill,
  getContentSeriesTurbo,
  startContentSeriesLipsync,
  previewContentSeriesTts,
  putContentSeriesPilot,
  fetchContentSeriesBuilds,
  fetchContentSeriesBuild,
  putContentSeriesBuild,
  deleteContentSeriesBuild,
  type ContentSeriesBuildSummary,
  assembleContentSeriesCut,
  startContentSeriesTurbo,
  probeContentSeriesTake,
  type ContentBrand,
  type ContentSeriesVoice,
} from '@/shared/api/content.api';
import {
  CONTINUITY_GATES,
  FAMIXA_PACK_SKELETON,
  SERIES_STATUS_LABEL,
  previousLockedShot,
  previousKeyframeShot,
  shotHasValidAction,
  bindShotToMemory,
  pruneEmptyShots,
  shotPlanDensity,
  groupShotsByBeat,
  primeLongShotsOnScriptLock,
  applyShotLockToGraph,
  compileI2vPrompt,
  outputAspectOf,
  sceneHasKeyframe,
  formatSeriesVideoContext,
  pickFamixaBrand,
  lockFromGraph,
  voiceCuesForShot,
  scriptListenCues,
  ensurePilotGraph,
  ensureScriptFollowsVoice,
  PILOT_SCHEMA,
  studioI2vPrecheck,
  i2vActionOf,
  studioShotCode,
  removeSceneShots,
  insertShortClip,
  allLongShotsLocked,
  approvedShortCount,
  canProduceShot,
  canLockScript,
  canLockCast,
  canOpenStudio,
  canWorkScene,
  canWorkShorts,
  sceneBlockReason,
  studioFallbackPane,
  creditSum,
  runwaySpentSum,
  episodeCodeOf,
  episodeShots,
  FAMIXA_SERIES_CODE,
  hasSeriesGraph,
  emptyPilot,
  loadSeriesPilot,
  lockCast,
  localFileRef,
  mergeRemotePilot,
  linesForScene,
  replaceStoryFromParse,
  refreshSpokenLinesFromPack,
  newRoleRow,
  newStillRow,
  packForShotEdit,
  parseFamixaPack,
  preflightTurboSend,
  reviewComplete,
  rolesReady,
  applyStillFromCanon,
  characterOfRole,
  characterCanonReady,
  canonDisplayOf,
  canonImageOf,
  canonStillRefs,
  hydratePilotCanon,
  hydratePilotKeyframes,
  seriesSceneStillPrompt,
  effectiveShotAction,
  looksLikePackHeading,
  roleCanonReady,
  roleVoiceReady,
  isVoiceOnlyRole,
  syncVoiceOnlyRoles,
  voiceLaneForRole,
  voicesForLane,
  voiceSoundsNorthern,
  isSouthernOrCentralVoice,
  isChildVoiceLane,
  isKidLibraryVoice,
  setCharacterCanon,
  setCharacterVoice,
  seriesCanonHint,
  saveSeriesPilot,
  slimPilotForServer,
  slimPilotForStorage,
  shortRunOf,
  stillsForShort,
  shotRunOf,
  type FamixaSeriesShot,
  type FamixaListenCue,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesReviewAxis,
  type SeriesShotRun,
} from './content-famixa-series';
import {
  applySceneKfReuses,
  buildSceneKfPlan,
  continuityPlaceHint,
  firstNewKfShot,
  kfIsApproved,
  sceneKfToGenerate,
} from './content-famixa-batch-plan';
import { mapPreviewCut, shotsInInclusiveRange } from './content-famixa-preview-cut';
import { productionShorts, setShortSeconds, canWorkV2Scene, v2SceneBlockReason, readyV2VideoShots, isLockedTemplateKf, isOperatorSuppliedKf, visualLockShot, canRetryTurboStart, shouldResumeTurboPoll, isTurboRateLimit, isTurboDailyQuota, parseRetryAfterSec, loadRunwayQuietUntil, persistRunwayQuietUntil, clearRunwayQuietUntil, nextRunwayQuietUntil, runwayQuietRemainMin, shouldResumeLipsync, estimateFalLipsyncUsdForShots, lipsyncVideoUrl, parseFalLipsyncRef, takeVideoUrl, lipsyncQaReady, normalizeLipsyncModel, normalizeLipsyncSyncMode, lipsyncTierOf, lipsyncTaskPrefix, parseFalJobIdFromError, shotI2vPromptHash } from './content-famixa-prod-v2';
import { assembleVideoUrl, resolveTakeUrl, stampFalFinal, stampMuteTake } from './content-famixa-final-source';
import {
  attemptToDiagRow,
  canManualRetry,
  capRunwayBatch,
  classifyRunwayFailure,
  classifyVideoPipe,
  dataUriHash,
  explainPipeError,
  inspectKfDataUri,
  inspectRunwayPayload,
  isKitPrecheckError,
  lastGenerationFail,
  kfCheckFromMeasure,
  latestAttempt,
  parseFailureCode,
  patchRunwayAttempt,
  promptHashOf,
  sameFailedInput,
  sanitizeKitPrecheck,
  stampFailedInput,
  startRunwayAttempt,
  summarizeAbDiagnostic,
} from './content-famixa-runway-pipe';
import { buildRunwayJob, sameRequestBlocked } from './content-runway-adapter';
import { compileRunwayPromptV1 } from './content-runway-prompt-v1';
import {
  assembleFileStem,
  buildAssembleTimeline,
  formatSrt,
  looksLikeVideoUrl,
  assembleConfirmCopy,
  assembleNeedTtsOverlay,
  completeCutBlocked,
  completeCutHolds,
  planCompleteCut,
  takeDownloadName,
  lipsyncDownloadName,
} from './content-famixa-assemble';
import { assembleMixPayload, compileMixCueSheet, normalizeMixPrefs } from './content-famixa-mix';
import { blobToBase64, recordAssembledCut, takeBlobFromUrl, triggerDownload } from './content-famixa-assemble-render';
import { deleteTtsScope, findTtsBlobForLine, loadTtsBlob, loadTtsBlobAny, measureAudioSec, saveTtsBlob, ttsLineKey, ttsLookupKeys, ttsTextKey } from './content-famixa-tts-store';
import { loadKfPixels } from './content-famixa-kf-store';
import { applyContinuityChain, buildContinuityChain } from './content-famixa-continuity-chain';
import {
  applyEditDurations,
  applyVisibleCast,
  compileShotSceneCard,
  compileShotStillMood,
  deriveSceneMaster,
  lockSceneMaster,
  peopleCountLock,
  pickShots,
  previousSceneKf,
  sceneIdOfShot,
  sceneMasterOf,
  sequentialKfIds,
  unlockSceneMaster,
  upsertSceneMaster,
  visibleFrameCast,
} from './content-famixa-scene-first';
import {
  approveBlockReason,
  parseVisionQa,
  peopleCountForSpec,
  seedQaChecks,
  shouldAttachPrevKf,
  visualQaAllowsApprove,
  type VisualSpec,
} from './content-famixa-visual-spec';
import {
  applySoloCast,
  compileCorrectionPrompt,
  soloCastFromNote,
  identityCanonIds,
  mergeReferencePack,
  nextShotNeedingKf,
} from './content-famixa-kf-pipeline';
import { applyDialogueMap, coverageOf, dialogueMapNeedsHeal, linesForShot, lipsyncSpeakerMismatch, multiSpeakerBlock } from './content-famixa-dialogue-map';
import { ACTING_EMOTIONS, actingTtsPerformText, actingTtsVoiceSettings, resolveLinePerformance } from './content-famixa-acting-law';
import { isChildFromBible, linePerformanceOf, patchDialoguePerformance, patchVoiceBible, stampDialoguePerformances } from './content-famixa-performance';
import {
  measureKfImage,
  prepareRunwayKf,
  shrinkStillDataUrl,
} from './content-famixa-still-ref';
import {
  SERIES_BUILD_STATUS_VI,
  bindBuildMedia,
  ensureBuildId,
  newSeriesBuild,
  type SeriesBuildStatus,
} from './content-famixa-build';
import { deleteKfScope } from './content-famixa-kf-store';
import { saveCanonPixels } from './content-famixa-canon-store';
import { famixaCanonSeedFor } from './content-famixa-canon-seed';
import { ContentFamixaStudioView, FamixaTimelinePane } from './ContentFamixaStudioView';
import { ContentFamixaStoryMemoryCard } from './ContentFamixaStoryMemoryCard';
import { needsInheritanceReview } from './content-famixa-story-memory';
import {
  canLockVoice,
  deriveVoiceScript,
  emptyVoicePreview,
  formatVoiceScriptPreview,
  looksLikeScreenplayDump,
  mergeVoiceGenerated,
  verifyVoiceGeneration,
  voiceLockBlockReason,
  voicePreviewCoversScript,
  voiceProductionReady,
} from './content-famixa-voice-script';

const REVIEW_AXES: { id: SeriesReviewAxis; label: string }[] = [
  { id: 'character', label: 'Character OK' },
  { id: 'motion', label: 'Motion OK' },
  { id: 'emotion', label: 'Emotion OK' },
  { id: 'canon', label: 'Canon OK' },
];

function shCode(st: SeriesPilotState, shot?: FamixaSeriesShot) {
  if (!shot) return 'SH';
  return studioShotCode(shot, episodeShots(st));
}

function ContinuityMemoryCard({
  lock,
  prevShot,
  onChange,
}: {
  lock: SceneContinuityLock;
  prevShot?: FamixaSeriesShot;
  onChange: (lock: SceneContinuityLock) => void;
}) {
  const patch = (p: Partial<SceneContinuityLock>) => onChange({ ...lock, ...p });
  const ro = lock.locked;
  return (
    <Card
      size="small"
      title="FAMIXA MEMORY — Continuity"
      extra={ro ? <Tag color="green">Scene lock</Tag> : <Tag>Chưa khóa</Tag>}
      style={{ marginBottom: 16 }}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="Khóa một lần cho cảnh. Shot sau chỉ nhập Action. Không chạy V03 để 'thử prompt'."
        description={
          prevShot
            ? `Baseline: ${prevShot.id} đã khóa. Shot sau kế thừa KF cảnh + wardrobe / phòng / vị trí — không viết lại.`
            : 'Khóa take shot đầu cảnh để Memory nhớ áo / ghế / phòng. Shot sau chỉ nhập Action.'
        }
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {lock.id}
      </Typography.Paragraph>
      {(
        [
          ['episode', 'Episode'],
          ['scene', 'Scene'],
          ['characters', 'Characters'],
          ['wardrobe', 'Wardrobe'],
          ['position', 'Position'],
          ['environment', 'Environment'],
          ['camera', 'Camera'],
          ['performance', 'Performance'],
        ] as const
      ).map(([key, label]) => (
        <div key={key} style={{ marginBottom: 8 }}>
          <Typography.Text type="secondary">{label}</Typography.Text>
          <Input.TextArea
            rows={key === 'environment' || key === 'performance' ? 2 : 1}
            disabled={ro}
            value={lock[key]}
            onChange={(e) => patch({ [key]: e.target.value })}
          />
        </div>
      ))}
      <Space wrap>
        <Button
          type="primary"
          icon={<LockOutlined />}
          disabled={ro}
          onClick={() => onChange({ ...lock, locked: true })}
        >
          Khóa Continuity
        </Button>
        {ro ? (
          <Button onClick={() => onChange({ ...lock, locked: false })}>Mở để sửa</Button>
        ) : null}
      </Space>
    </Card>
  );
}

let persistCache: { json: string; state: SeriesPilotState } | undefined;

function persist(next: SeriesPilotState, setState: (s: SeriesPilotState) => void) {
  const seeded =
    next.buildId ||
    (next.packDraft ?? '').trim() ||
    (next.episode?.shots?.length ?? 0) > 0 ||
    (next.scenes?.length ?? 0) > 0
      ? ensureBuildId(next)
      : next;
  if (seeded.buildId) bindBuildMedia(seeded);
  const graph = ensurePilotGraph({ ...seeded, schemaVersion: PILOT_SCHEMA });
  const slim = slimPilotForStorage(graph);
  const json = JSON.stringify(slim);
  if (persistCache?.json === json) return persistCache.state;
  saveSeriesPilot(graph, json);
  const cloned: SeriesPilotState = {
    ...slim,
    roles: [...slim.roles],
    runs: { ...slim.runs },
    stills: [...(slim.stills ?? [])],
    episode: slim.episode ? { ...slim.episode, shots: [...slim.episode.shots] } : undefined,
    continuity: slim.continuity ? { ...slim.continuity } : undefined,
    characters: [...(slim.characters ?? [])],
    scenes: [...(slim.scenes ?? [])],
    lines: [...(slim.lines ?? [])],
    shorts: [...(slim.shorts ?? [])],
    storyMemory: slim.storyMemory ? { ...slim.storyMemory } : undefined,
    voicePreview: slim.voicePreview ? { ...slim.voicePreview } : undefined,
  };
  persistCache = { json, state: cloned };
  setState(cloned);
  return cloned;
}

function packSummary(parsed: ReturnType<typeof parseFamixaPack>) {
  const shotN = parsed.episode?.shots.length ?? 0;
  const shortN = parsed.shorts.length;
  const stillN = parsed.stills.length;
  const charN = parsed.characters.length;
  const ids = [
    ...parsed.shorts.map((s) => s.id),
    ...(parsed.episode?.shots ?? []).map((s) => s.id),
  ].filter(Boolean);
  const sceneN = parsed.scenes.length;
  const lineN = parsed.lines.length;
  const parts = [
    parsed.episode?.episode ? parsed.episode.episode : null,
    shortN ? `${shortN} short` : null,
    sceneN ? `${sceneN} cảnh` : null,
    shotN ? `${shotN} shot` : null,
    stillN ? `${stillN} hàng ảnh CHAR` : null,
    charN ? `${charN} CHAR` : null,
    lineN ? `${lineN} câu thoại` : null,
  ].filter(Boolean);
  return `${parts.join(', ') || 'đã nhận'}${ids.length ? ` · ${ids.slice(0, 4).join(', ')}` : ''}`;
}

function stripLeadingId3(buf: Uint8Array) {
  if (buf.length >= 10 && buf[0] === 0x49 && buf[1] === 0x44 && buf[2] === 0x33) {
    const size =
      ((buf[6] & 0x7f) << 21) | ((buf[7] & 0x7f) << 14) | ((buf[8] & 0x7f) << 7) | (buf[9] & 0x7f);
    return buf.subarray(Math.min(10 + size, buf.length));
  }
  return buf;
}

async function concatMp3Blobs(blobs: Blob[]) {
  const parts: BlobPart[] = [];
  for (let i = 0; i < blobs.length; i++) {
    const buf = new Uint8Array(await blobs[i]!.arrayBuffer());
    parts.push(i === 0 ? buf : stripLeadingId3(buf));
  }
  return new Blob(parts, { type: 'audio/mpeg' });
}

function slugFilePart(raw: string) {
  return (
    raw
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'D')
      .replace(/[^\w]+/g, '-')
      .replace(/^-|-$/g, '')
      .toLowerCase()
      .slice(0, 48) || 'tap'
  );
}

function fullScriptMp3Name(state: SeriesPilotState) {
  const ep = episodeCodeOf(state.episode?.episode || state.episode?.title) || 'EP01';
  const title = slugFilePart(state.episode?.title || 'bo-dung-hua-nua');
  return `famixa-${ep}-${title}.mp3`;
}

function explainTurboError(raw?: string | null) {
  const t = (raw ?? '').trim();
  if (isTurboDailyQuota(t)) {
    return 'Hết số job/ngày (không phải hết cr). Mua credit không mở khóa. Đợi cửa sổ 24h hoặc nâng tier trên dev.runwayml.com → Usage.';
  }
  if (isTurboRateLimit(t)) {
    return 'Runway 429: khóa số job/ngày hoặc đang chạy quá số song song — không phải hết cr. Mua credit không gỡ. Đợi rồi Hỏi lại · 0 cr.';
  }
  if (/insufficient|quota|payment|billing|credits?/i.test(t) && !/INTERNAL/i.test(t)) {
    return 'Runway hết credit hoặc Dev chưa thanh toán. Kiểm tra tài khoản, rồi gửi nốt clip thiếu.';
  }
  return explainPipeError(t);
}

function runwayCredits(seconds: number) {
  const sec = seconds >= 8 ? 10 : 5;
  return { sec, credits: sec * 5 };
}

const ENGINE_KEY = 'kit.famixaSeries.engine';
const VOICE_KEY = 'kit.famixaSeries.voice';

function loadEngine(): 'turbo' | 'wan' {
  try {
    return localStorage.getItem(ENGINE_KEY) === 'wan' ? 'wan' : 'turbo';
  } catch {
    return 'turbo';
  }
}

function loadVoice(): 'elevenlabs' | 'f5' {
  try {
    return localStorage.getItem(VOICE_KEY) === 'f5' ? 'f5' : 'elevenlabs';
  } catch {
    return 'elevenlabs';
  }
}

function generateCost(engine: 'turbo' | 'wan', seconds: number) {
  if (engine === 'wan') {
    return { credits: 0, label: 'Fal · 1 đơn vị (720p · ~5s). Wan không làm 10s — tối đa ~6s.' };
  }
  const c = runwayCredits(seconds);
  return { credits: c.credits, label: `Runway Turbo · ${c.credits} cr` };
}

function clipPlaySrc(url?: string, sessionSrc?: string) {
  if (sessionSrc) return sessionSrc;
  return looksLikeVideoUrl(url) ? (url ?? '').trim() : '';
}

function clipOpenHref(url?: string) {
  const href = (url ?? '').trim();
  return /^https?:\/\//i.test(href) ? href : '';
}

function suggestedTakeName(clipId: string) {
  let compact = clipId.trim().replace(/-/g, '_');
  compact = compact.replace(/^FAMIXA_/i, '').replace(/^EP/i, 'E').replace(/_V\d+$/i, '');
  return `FAMIXA_${compact}_V01.mp4`;
}

function ClipWatch({
  clipId,
  url,
  path,
  sessionSrc,
  history = [],
  onUrl,
  onPath,
  onFile,
}: {
  clipId: string;
  url?: string;
  path?: string;
  sessionSrc?: string;
  history?: { url: string; taskId?: string }[];
  onUrl: (value: string) => void;
  onPath: (value: string) => void;
  onFile: (file: File) => void;
}) {
  const pick = useRef<HTMLInputElement>(null);
  const play = clipPlaySrc(url, sessionSrc);
  const open = clipOpenHref(url);
  const expected = suggestedTakeName(clipId);
  return (
    <Card size="small" title="Xem thử take" style={{ marginBottom: 12 }}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="KIT không có file video. Take nằm ở Runway hoặc thư mục bạn tải về."
        description={
          <>
            Tên gợi ý từ pack: <Typography.Text copyable>{expected}</Typography.Text>
            . Take mới ghi đè link trên KIT — take trước nằm ở Downloads / Runway (thường là file V01). Bấm{' '}
            <strong>Chọn video trên máy</strong>.
          </>
        }
      />
      <input
        ref={pick}
        type="file"
        accept="video/*,.mp4,.webm,.mov,.m4v"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onFile(file);
          e.target.value = '';
        }}
      />
      <Space wrap style={{ marginBottom: 8 }}>
        <Button type="primary" icon={<FolderOpenOutlined />} onClick={() => pick.current?.click()}>
          Chọn video trên máy
        </Button>
        {open ? (
          <Typography.Link href={open} target="_blank" rel="noreferrer">
            Mở / tải take
          </Typography.Link>
        ) : null}
      </Space>
      <Input
        size="small"
        prefix={<LinkOutlined />}
        value={url ?? ''}
        placeholder="Hoặc dán https://… (Runway share / mp4)"
        onChange={(e) => onUrl(e.target.value)}
        style={{ marginBottom: 8 }}
      />
      <Input
        size="small"
        value={path ?? ''}
        placeholder={`Đường dẫn máy — ${expected}`}
        onChange={(e) => onPath(e.target.value)}
        style={{ marginBottom: 8 }}
      />
      {history.length > 0 ? (
        <div style={{ marginBottom: 8 }}>
          <Typography.Text type="secondary">Take trước (link còn trên máy): </Typography.Text>
          <Space wrap>
            {history.map((h, i) => (
              <Button key={`${h.url}-${i}`} size="small" onClick={() => onUrl(h.url)}>
                Dùng lại take {history.length - i}
              </Button>
            ))}
          </Space>
        </div>
      ) : null}
      {play ? (
        <video src={play} controls playsInline style={{ width: '100%', maxWidth: 560, borderRadius: 8 }} />
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Chưa có take để phát. Download từ Runway rồi chọn file — không gõ path rồi Enter.
        </Typography.Paragraph>
      )}
    </Card>
  );
}

function readImageFile(file: File) {
  return new Promise<string>((resolve, reject) => {
    if (file.size > 4_000_000) {
      reject(new Error('Ảnh ≤ 4MB — lưu trên máy, không lên server.'));
      return;
    }
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.onerror = () => reject(new Error('Không đọc được ảnh'));
    reader.readAsDataURL(file);
  });
}

export function ContentFamixaSeriesTab() {
  const { message, modal } = App.useApp();
  const [state, setState] = useState<SeriesPilotState>(emptyPilot);
  const [packText, setPackText] = useState('');
  const [activeId, setActiveId] = useState<string | undefined>();
  const [activeShortId, setActiveShortId] = useState<string | undefined>();
  const [stillOnlyActive, setStillOnlyActive] = useState(true);
  const [packResult, setPackResult] = useState<{ ok: boolean; text: string } | null>(null);
  const [builds, setBuilds] = useState<ContentSeriesBuildSummary[]>([]);
  const [buildBusy, setBuildBusy] = useState(false);
  const [sessionClips, setSessionClips] = useState<Record<string, string>>({});
  const [turboBusy, setTurboBusy] = useState<string | undefined>();
  const [lipsyncBusy, setLipsyncBusy] = useState<string | undefined>();
  const turboLockRef = useRef(Promise.resolve());
  const turboLaunchRef = useRef(new Set<string>());
  const [runwayQuietUntil, setRunwayQuietUntil] = useState(loadRunwayQuietUntil);
  const [, setQuietTick] = useState(0);
  const [assembleBusy, setAssembleBusy] = useState(false);
  const assembleAspect = outputAspectOf(state);
  const [stillBusy, setStillBusy] = useState<string | undefined>();
  const kfInflightRef = useRef<string | undefined>(undefined);
  const [memOpen, setMemOpen] = useState(false);
  const [studioPane, setStudioPane] = useState<'script' | 'voice' | 'shorts' | 'studio' | 'timeline' | 'advanced'>(
    'script',
  );
  const [engine, setEngine] = useState<'turbo' | 'wan'>(loadEngine);
  const [voiceProvider, setVoiceProvider] = useState<'elevenlabs' | 'f5'>(loadVoice);
  const [keys, setKeys] = useState({ runway: false, fal: false, elevenLabs: false, gemini: false });
  const [voices, setVoices] = useState<ContentSeriesVoice[]>([]);
  const [voicesLoading, setVoicesLoading] = useState(true);
  const [packMode, setPackMode] = useState<'paste' | 'ai'>('paste');
  const [packSeed, setPackSeed] = useState('');
  const [draftBusy, setDraftBusy] = useState(false);
  const [famixaBrand, setFamixaBrand] = useState<ContentBrand | undefined>();
  const videoContext = useMemo(
    () => formatSeriesVideoContext(famixaBrand?.knowledge),
    [famixaBrand],
  );
  const stateRef = useRef(state);
  stateRef.current = state;
  const ttsUrls = useRef(new Map<string, string>());
  const ttsBlobs = useRef(new Map<string, Blob>());
  const ttsSent = useRef(new Map<string, string>());
  const ttsAudio = useRef<HTMLAudioElement | null>(null);
  const ttsStop = useRef(false);
  const [ttsBusy, setTtsBusy] = useState(false);
  const [playingLineId, setPlayingLineId] = useState<string>();
  const [ttsNote, setTtsNote] = useState<string>();
  const [ttsFiles, setTtsFiles] = useState<Record<string, { url: string; fileName: string }>>({});
  const [cutFrom, setCutFrom] = useState<string>();
  const [cutTo, setCutTo] = useState<string>();
  const [cutPick, setCutPick] = useState<string[]>([]);
  const [ttsFull, setTtsFull] = useState<{ url: string; fileName: string }>();
  const serverSaveTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const refreshBuilds = () =>
    fetchContentSeriesBuilds(FAMIXA_SERIES_CODE)
      .then(setBuilds)
      .catch(() => undefined);

  const lastSlimJson = useRef('');
  const queueServerSave = (next: SeriesPilotState) => {
    if (serverSaveTimer.current) clearTimeout(serverSaveTimer.current);
    serverSaveTimer.current = setTimeout(() => {
      const slim = slimPilotForServer(next) as unknown as Record<string, unknown>;
      const json = JSON.stringify(slim);
      if (json === lastSlimJson.current) return;
      lastSlimJson.current = json;
      if (next.buildId) {
        void putContentSeriesBuild({
          id: next.buildId,
          seriesCode: FAMIXA_SERIES_CODE,
          graph: slim,
        }).catch(() => undefined);
      }
      void putContentSeriesPilot({
        seriesCode: FAMIXA_SERIES_CODE,
        graph: slim,
      }).catch(() => undefined);
    }, 2400);
  };
  const persistState = (next: SeriesPilotState) => {
    const cloned = persist(ensureScriptFollowsVoice(syncVoiceOnlyRoles(next)), setState);
    stateRef.current = cloned;
    queueServerSave(cloned);
    return cloned;
  };

  const setOutputAspect = (aspect: '16:9' | '9:16') => {
    const live = stateRef.current;
    if (aspect === live.outputAspect) return;
    if (sceneHasKeyframe(live)) {
      modal.confirm({
        title: `Đổi khung ${outputAspectOf(live)} → ${aspect}`,
        content:
          'Đã có KF. Đổi khung lúc này crop/pad lệch ý. Tạo lại KF + I2V theo khung mới. Take cũ không tự sửa.',
        okText: `Khóa ${aspect}`,
        cancelText: 'Giữ khung cũ',
        onOk: () => persistState({ ...stateRef.current, outputAspect: aspect }),
      });
      return;
    }
    persistState({ ...live, outputAspect: aspect });
  };

  const clearSessionTts = () => {
    ttsUrls.current.forEach((url) => URL.revokeObjectURL(url));
    ttsUrls.current.clear();
    ttsBlobs.current.clear();
    ttsSent.current.clear();
    setTtsFiles({});
  };

  const voiceIdsForLine = (line: { characterId?: string; voiceId?: string }) => {
    const ch = (stateRef.current.characters ?? []).find((c) => c.id === line.characterId);
    const role = stateRef.current.roles.find((r) => r.characterId === line.characterId);
    return [line.voiceId, ch?.voiceId, role?.voiceId].filter(Boolean) as string[];
  };

  const hydrateSessionTts = async (graph: SeriesPilotState) => {
    const lines = deriveVoiceScript(graph).lines;
    for (const line of lines) {
      if (ttsBlobs.current.has(line.id)) continue;
      const blob =
        (await loadTtsBlobAny(ttsLookupKeys(line, voiceIdsForLine(line)))) || (await findTtsBlobForLine(line.id));
      if (!blob) continue;
      ttsBlobs.current.set(line.id, blob);
      ttsBlobs.current.set(ttsTextKey(line.text, line.voiceId), blob);
      const url = URL.createObjectURL(blob);
      ttsUrls.current.set(line.id, url);
      setTtsFiles((m) => ({ ...m, [line.id]: { url, fileName: `${line.id}.mp3` } }));
    }
  };

  const openBuild = (id: string) => {
    if (id === state.buildId) return;
    persistState(stateRef.current);
    setBuildBusy(true);
    void fetchContentSeriesBuild(id)
      .then(async (row) => {
        const remote = (row.graph && typeof row.graph === 'object' ? row.graph : {}) as SeriesPilotState;
        clearSessionTts();
        const next = persistState(ensurePilotGraph({ ...remote, buildId: row.id, schemaVersion: PILOT_SCHEMA }));
        setPackText(next.packDraft ?? '');
        await hydratePilotKeyframes(await hydratePilotCanon(next));
        setState((cur) => ({ ...cur, runs: { ...cur.runs } }));
        await hydrateSessionTts(stateRef.current);
        setStudioPane('script');
        message.success(`Đã mở ${row.episodeCode || row.title || 'bản dựng'} — Voice / KF / take của bản này.`);
      })
      .catch((e) => message.error(apiErrorMessage(e, 'Không mở được bản dựng.')))
      .finally(() => setBuildBusy(false));
  };

  const createBuild = () => {
    persistState(stateRef.current);
    clearSessionTts();
    const next = persistState(newSeriesBuild(stateRef.current));
    setPackText('');
    setPackResult(null);
    setStudioPane('script');
    message.success('Bản dựng mới. Canon giữ. Dán kịch bản rồi Nhận pack.');
    void next;
  };

  const removeBuild = (id: string, title: string) => {
    modal.confirm({
      title: `Xóa bản dựng «${title || 'không tên'}»?`,
      content: 'Xóa hàng trên server. KF/TTS trên máy của bản này cũng xóa. Take Runway không hoàn credit.',
      okText: 'Xóa hẳn',
      okButtonProps: { danger: true },
      cancelText: 'Giữ',
      onOk: async () => {
        await deleteContentSeriesBuild(id);
        await deleteKfScope(id);
        await deleteTtsScope(id);
        if (stateRef.current.buildId === id) {
          clearSessionTts();
          persistState(newSeriesBuild(stateRef.current));
          setPackText('');
        }
        await refreshBuilds();
        message.success('Đã xóa bản dựng.');
      },
    });
  };

  const stopScriptListen = () => {
    ttsStop.current = true;
    ttsAudio.current?.pause();
    setPlayingLineId(undefined);
    setTtsBusy(false);
    setTtsNote(undefined);
  };

  const loadCueAudio = async (cue: {
    id: string;
    voiceId?: string;
    text: string;
    name: string;
    characterId?: string;
    performance?: import('./content-famixa-acting-law').LinePerformance;
  }) => {
    if (looksLikeScreenplayDump(cue.text)) {
      throw new Error('TTS chỉ nhận thoại CHAR — không gửi heading/cảnh/action.');
    }
    const voiceId = (cue.voiceId ?? '').trim();
    if (!voiceId) throw new Error(`Chưa gán Voice Canon cho ${cue.name}.`);
    if (voices.some((v) => v.voiceId === voiceId && v.cloned)) {
      throw new Error(
        `${cue.name}: giọng Instant Clone — gói ElevenLabs hiện tại không TTS được. Đổi sang giọng thư viện tiếng Việt miền Bắc.`,
      );
    }
    const ch = cue.characterId
      ? (stateRef.current.characters ?? []).find((c) => c.id === cue.characterId)
      : undefined;
    const dir = resolveLinePerformance({
      text: cue.text,
      characterId: cue.characterId,
      name: cue.name,
      performance: cue.performance ?? linePerformanceOf(stateRef.current, cue.id),
    });
    const child =
      isChildVoiceLane(voiceLaneForRole({ characterId: cue.characterId, name: cue.name, title: '' }, ch)) ||
      isChildFromBible(ch);
    const pick = voices.find((v) => v.voiceId === voiceId);
    const northern = voiceSoundsNorthern(pick) || /hanoi|ha noi|northern|mien bac/i.test(`${pick?.accent || ''} ${pick?.name || ''} ${ch?.voiceName || ''}`);
    const vs = actingTtsVoiceSettings(
      dir,
      {
        stability: ch?.voiceStability,
        similarity: ch?.voiceSimilarity,
        style: ch?.voiceStyle,
        speed: ch?.voiceSpeed,
      },
      { child, northern: child || northern },
    );
    const perform = actingTtsPerformText(cue.text, dir);
    const key = `vi-north|${voiceId}|${perform}|${dir.emotion}|${vs.stability}|${vs.speed}|${vs.style}`;
    const hit = ttsUrls.current.get(key);
    if (hit) {
      ttsSent.current.set(cue.id, cue.text);
      return hit;
    }
    const blob = await previewContentSeriesTts({
      voiceId,
      text: perform,
      publicOwnerId: pick?.publicOwnerId || undefined,
      voiceName: pick?.name || ch?.voiceName,
      accent: pick?.accent || (child ? 'northern' : undefined),
      stability: vs.stability,
      similarityBoost: vs.similarityBoost,
      style: vs.style,
      speed: vs.speed,
    });
    const url = URL.createObjectURL(blob);
    ttsUrls.current.set(key, url);
    ttsBlobs.current.set(key, blob);
    ttsBlobs.current.set(cue.id, blob);
    ttsSent.current.set(cue.id, cue.text);
    void saveTtsBlob(ttsLineKey(cue.id, voiceId), blob);
    void saveTtsBlob(ttsTextKey(cue.text, voiceId), blob);
    void measureAudioSec(blob).then((sec) => {
      if (sec <= 0) return;
      const cur = stateRef.current;
      persistState({
        ...cur,
        voiceAssets: {
          ...(cur.voiceAssets ?? {}),
          [cue.id]: {
            lineId: cue.id,
            characterId: cue.characterId,
            duration: Number(sec.toFixed(2)),
            status: 'ready',
          },
        },
      });
    });
    const idx = scriptListenCues(stateRef.current).findIndex((c) => c.id === cue.id);
    const who = (cue.name || 'thoai').replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '') || 'line';
    const fileName = `famixa-${String((idx >= 0 ? idx : 0) + 1).padStart(2, '0')}-${who}.mp3`;
    setTtsFiles((m) => ({ ...m, [cue.id]: { url, fileName } }));
    return url;
  };

  const commitVoicePreview = (st = stateRef.current) => {
    const script = deriveVoiceScript(st);
    const now = [...ttsSent.current.entries()].map(([id, text]) => ({ id, text }));
    const merged = mergeVoiceGenerated(st.voicePreview?.generated, now);
    if (merged.length === 0 && voicePreviewCoversScript(script, st.voicePreview)) {
      return st.voicePreview!;
    }
    const report = merged.length ? verifyVoiceGeneration(script, merged) : emptyVoicePreview(script);
    const keep =
      report.status !== 'complete' && voicePreviewCoversScript(script, st.voicePreview)
        ? {
            ...st.voicePreview!,
            ...report,
            status: 'complete' as const,
            issues: [] as string[],
            generated: merged.length ? merged : st.voicePreview?.generated,
          }
        : report;
    persistState({
      ...st,
      voicePreview: keep,
      voiceLocked: keep.status === 'complete' ? Boolean(st.voiceLocked) : false,
    });
    return keep;
  };

  const playCueAudio = (url: string) =>
    new Promise<void>((resolve, reject) => {
      ttsAudio.current?.pause();
      const audio = new Audio(url);
      ttsAudio.current = audio;
      audio.onended = () => resolve();
      audio.onerror = () => reject(new Error('Không phát được tiếng.'));
      void audio.play().catch(reject);
    });

  const playListenCue = async (cue: FamixaListenCue) => {
    ttsStop.current = false;
    setTtsBusy(true);
    setPlayingLineId(cue.id);
    try {
      const url = await loadCueAudio(cue);
      if (ttsStop.current) return;
      commitVoicePreview();
      await playCueAudio(url);
    } catch (e) {
      message.error(e instanceof Error ? e.message : apiErrorMessage(e, 'Không nghe được thoại.'));
    } finally {
      if (!ttsStop.current) setPlayingLineId(undefined);
      setTtsBusy(false);
    }
  };

  const playWholeScript = () => {
    const script = deriveVoiceScript(stateRef.current);
    const cues = script.lines;
    const ready = cues.filter((c) => c.voiceId);
    const missing = cues.length - ready.length;
    const speakers = new Set(cues.map((c) => c.characterId));
    if (cues.length === 0) {
      message.warning('Nhận pack kịch bản rồi duyệt Parsed Story — KIT tự tách Voice Script (chỉ thoại).');
      return;
    }
    if (ready.length === 0 || missing > 0) {
      const names = [...new Set(cues.filter((c) => !c.voiceId).map((c) => c.name || c.characterId))].slice(0, 4).join(', ');
      message.warning(
        names
          ? `Gán Voice Canon cho: ${names}. Lời bình không cần ảnh — mở khóa Cast nếu đang khóa, chọn giọng nam miền Bắc, rồi Tạo Full Voice.`
          : 'Gán Voice Canon cho từng vai rồi mới Full Voice.',
      );
      return;
    }
    if (!keys.elevenLabs) {
      message.warning('Chưa có key ElevenLabs — Cấu hình AI.');
      return;
    }
    modal.confirm({
      title: 'Full Voice — chỉ thoại CHAR',
      content: `Voice Script ${script.sourceLineCount} câu · ${script.sourceCharCount} ký tự · ~${script.estimatedSec}s · ${speakers.size} giọng. Không đọc heading/action. Thiếu voice: ${missing}. ElevenLabs tính ký tự — không trừ Runway.`,
      okText: 'Tạo Full Voice',
      onOk: () => {
        ttsStop.current = false;
        setTtsBusy(true);
        void (async () => {
          const urls: (string | null)[] = [];
          const cloneSkip = new Set<string>();
          try {
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) return;
              const cue = cues[i]!;
              setPlayingLineId(cue.id);
              setTtsNote(`Voice Script ${i + 1}/${cues.length} · ${cue.name}`);
              if (!cue.voiceId || voices.some((v) => v.voiceId === cue.voiceId && v.cloned)) {
                if (cue.voiceId) cloneSkip.add(cue.name);
                urls.push(null);
                continue;
              }
              try {
                urls.push(await loadCueAudio(cue));
              } catch (e) {
                urls.push(null);
                message.warning(`${cue.name}: ${e instanceof Error ? e.message : 'bỏ câu này.'}`);
              }
            }
            const report = commitVoicePreview();
            if (cloneSkip.size > 0) {
              message.warning(`Bỏ giọng Instant Clone của ${[...cloneSkip].join(', ')}.`);
            }
            if (ttsStop.current) return;
            if (report.status !== 'complete') {
              message.error(`VOICE INCOMPLETE — ${report.issues[0] || 'thiếu thoại'}. Chưa được khóa, chưa I2V.`);
              return;
            }
            const playable = urls.filter(Boolean).length;
            setTtsNote(`COMPLETE · đọc ${playable} câu`);
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) break;
              const url = urls[i];
              if (!url) continue;
              setPlayingLineId(cues[i]!.id);
              await playCueAudio(url);
              if (ttsStop.current) break;
              await new Promise((r) => setTimeout(r, 180));
            }
            message.success(`Voice COMPLETE · ${report.generatedLineCount} câu · ${report.generatedCharCount} ký tự.`);
          } catch (e) {
            message.error(e instanceof Error ? e.message : apiErrorMessage(e, 'Không nghe được thoại.'));
          } finally {
            setPlayingLineId(undefined);
            setTtsBusy(false);
            setTtsNote(undefined);
          }
        })();
      },
    });
  };

  const downloadFullScriptMp3 = () => {
    const script = deriveVoiceScript(stateRef.current);
    const cues = script.lines;
    if (cues.length === 0) {
      message.warning('Chưa có Voice Script — Nhận pack kịch bản.');
      return;
    }
    if (!keys.elevenLabs) {
      message.warning('Chưa có key ElevenLabs — Cấu hình AI.');
      return;
    }
    modal.confirm({
      title: 'Tải MP3 Voice Script',
      content: `Ghép ${script.sourceLineCount} câu thoại (không gồm heading/action) · ${script.sourceCharCount} ký tự.`,
      okText: 'Ghép và tải',
      onOk: () => {
        ttsStop.current = false;
        setTtsBusy(true);
        void (async () => {
          try {
            const blobs: Blob[] = [];
            for (let i = 0; i < cues.length; i++) {
              if (ttsStop.current) return;
              const cue = cues[i]!;
              setPlayingLineId(cue.id);
              setTtsNote(`Voice Script ${i + 1}/${cues.length} · ${cue.name}`);
              if (!cue.voiceId || voices.some((v) => v.voiceId === cue.voiceId && v.cloned)) continue;
              await loadCueAudio(cue);
              const blob = ttsBlobs.current.get(cue.id);
              if (blob) blobs.push(blob);
            }
            const report = commitVoicePreview();
            if (ttsStop.current) return;
            if (report.status !== 'complete') {
              message.error(`VOICE INCOMPLETE — không tải file full khi còn thiếu thoại.`);
              return;
            }
            setTtsNote(`Đang ghép ${blobs.length} câu thành 1 MP3`);
            const full = await concatMp3Blobs(blobs);
            const fileName = fullScriptMp3Name(stateRef.current).replace('.mp3', '-voice-script.mp3');
            const url = URL.createObjectURL(full);
            setTtsFull((prev) => {
              if (prev?.url) URL.revokeObjectURL(prev.url);
              return { url, fileName };
            });
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            a.rel = 'noopener';
            a.click();
            message.success(`COMPLETE · ${fileName}`);
          } catch (e) {
            message.error(e instanceof Error ? e.message : apiErrorMessage(e, 'Không ghép được MP3 cả tập.'));
          } finally {
            setPlayingLineId(undefined);
            setTtsBusy(false);
            setTtsNote(undefined);
          }
        })();
      },
    });
  };

  const voiceOptionsForRole = (role: (typeof state.roles)[number], selectedId: string) => {
    const lane = voiceLaneForRole(role, characterOfRole(state, role));
    const ch = characterOfRole(state, role);
    let rows = voicesForLane(voices, lane, selectedId);
    if (!rows.length) {
      rows = voices.filter((v) => !v.cloned && !isSouthernOrCentralVoice(v));
    }
    if (!rows.length) rows = voices.filter((v) => !v.cloned);
    const options = rows.map((v) => ({
      value: v.voiceId,
      label: v.cloned ? `${v.name} · clone (gói chưa TTS được)` : v.name,
    }));
    if (selectedId && !options.some((o) => o.value === selectedId)) {
      options.unshift({
        value: selectedId,
        label: (ch?.voiceName || role.voiceName || selectedId).trim(),
      });
    }
    const filterOption = (input: string, option?: { label?: string }) => {
      const q = input.trim().toLowerCase();
      if (!q || /lời bình|loi binh|miền bắc|mien bac|giọng /.test(q)) return true;
      return String(option?.label ?? '').toLowerCase().includes(q);
    };
    return { lane, options, filterOption };
  };
  const castFrozen = Boolean(state.castLocked || state.scriptLocked);
  const ready = rolesReady(state.roles);
  const shorts = state.shorts ?? [];
  const shots = episodeShots(state);
  const ep = state.episode;
  const activeShort = shorts.find((s) => s.id === activeShortId) ?? shorts[0];
  const shortRun = activeShort ? shortRunOf(state, activeShort.id) : undefined;
  const active = shots.find((s) => s.id === activeId) ?? shots[0];
  const run = active ? shotRunOf(state, active) : undefined;
  const unlocked = active ? canProduceShot(state, active) : false;
  const sceneReady = allLongShotsLocked(state);
  const credits = creditSum(state);
  const spentOnRunway = runwaySpentSum(state);
  const voiceScript = useMemo(() => deriveVoiceScript(state), [state]);
  const listenCues = voiceScript.lines;

  useEffect(() => {
    if (voiceScript.lines.length > 0) return;
    const next = refreshSpokenLinesFromPack(stateRef.current);
    if (deriveVoiceScript(next).lines.length <= voiceScript.lines.length) return;
    persistState(next);
  }, [state.packDraft, voiceScript.lines.length]);

  useEffect(() => {
    if (!state.shotGraphLocked) return;
    if (!dialogueMapNeedsHeal(stateRef.current)) return;
    persistState(applyDialogueMap(stateRef.current));
  }, [state.shotGraphLocked, voiceScript.lines.length, state.episode?.shots?.length]);
  const voiceLockHint =
    !keys.elevenLabs && state.voicePreview?.status !== 'complete'
      ? 'Chưa có key ElevenLabs — Cấu hình AI, rồi bấm Tạo Full Voice.'
      : voiceLockBlockReason(state) ?? '';

  useEffect(() => {
    const t = window.requestAnimationFrame(() => {
      const s = loadSeriesPilot();
      bindBuildMedia(s);
      setState(s);
      setPackText(s.packDraft ?? '');
      setActiveShortId(s.shorts?.[0]?.id);
    });
    return () => window.cancelAnimationFrame(t);
  }, []);

  useEffect(() => {
    if (runwayQuietUntil <= Date.now()) return;
    const t = window.setInterval(() => setQuietTick((n) => n + 1), 15_000);
    return () => window.clearInterval(t);
  }, [runwayQuietUntil]);

  useEffect(() => {
    if (studioPane !== 'shorts') return;
    if ((state.shorts?.length ?? 0) > 0) return;
    if (!canOpenStudio(state)) return;
    setStudioPane('studio');
  }, [studioPane, state.shorts?.length, state.scriptLocked]);

  useEffect(() => {
    void fetchContentSettings()
      .then((s) => {
        setKeys({
          runway: Boolean(s.video?.runwayConfigured),
          fal: Boolean(s.video?.falConfigured),
          elevenLabs: Boolean(s.video?.elevenLabsConfigured),
          gemini: Boolean(s.ai?.apiKeyConfigured),
        });
      })
      .catch(() => undefined);
    void fetchContentBrands(true)
      .then((rows) => setFamixaBrand(pickFamixaBrand(rows)))
      .catch(() => undefined);
  }, []);

  const loadVoiceLibrary = (opts?: { force?: boolean }) => {
    if (!opts?.force) {
      try {
        const cached = sessionStorage.getItem('kit.famixa.voices.v3');
        if (cached) {
          const rows = JSON.parse(cached) as ContentSeriesVoice[];
          if (Array.isArray(rows) && rows.length) {
            setVoices(rows);
            setVoicesLoading(false);
          }
        }
      } catch {
        /* ignore */
      }
    }
    setVoicesLoading(true);
    return fetchContentSeriesVoices()
      .then((rows) => {
        const next = rows ?? [];
        if (next.length) {
          setVoices(next);
          try {
            sessionStorage.setItem('kit.famixa.voices.v3', JSON.stringify(next));
          } catch {
            /* quota */
          }
        } else if (opts?.force) {
          setVoices([]);
          message.warning('API không trả giọng — restart API :5290 hoặc dán Voice ID.');
        }
        return next;
      })
      .catch(() => {
        message.warning('Không tải được thư viện giọng — dán Voice ID nam Hà Nội.');
        return [] as ContentSeriesVoice[];
      })
      .finally(() => setVoicesLoading(false));
  };

  useEffect(() => {
    void loadVoiceLibrary();
  }, []);

  useEffect(
    () => () => {
      ttsStop.current = true;
      ttsAudio.current?.pause();
      ttsUrls.current.forEach((url) => URL.revokeObjectURL(url));
      ttsUrls.current.clear();
      ttsBlobs.current.clear();
    },
    [],
  );

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const row = await fetchContentSeriesPilot(FAMIXA_SERIES_CODE);
        if (cancelled) return;
        const remote = row.graph && typeof row.graph === 'object' ? (row.graph as SeriesPilotState) : null;
        const local = stateRef.current;
        void refreshBuilds();
        if (!remote || !hasSeriesGraph(remote as SeriesPilotState)) {
          if (hasSeriesGraph(local)) queueServerSave(local);
        } else {
          const merged = slimPilotForStorage(mergeRemotePilot(remote, local));
          stateRef.current = merged;
          setState(merged);
          saveSeriesPilot(merged);
        }
      } catch {
        /* keep localStorage */
      }
      if (cancelled) return;
      await hydratePilotCanon(stateRef.current);
    })();
    return () => {
      cancelled = true;
    };
    // hydrate once
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const next = syncVoiceOnlyRoles(stateRef.current);
    if (next !== stateRef.current) persistState(next);
  }, [state.roles.length, state.characters, state.castLocked]);

  useEffect(() => {
    if (studioPane !== 'studio') return;
    let cancelled = false;
    void (async () => {
      await hydratePilotKeyframes(stateRef.current);
      if (!cancelled) setState((cur) => ({ ...cur, runs: { ...cur.runs } }));
    })();
    return () => {
      cancelled = true;
    };
  }, [studioPane]);

  useEffect(() => {
    if (studioPane !== 'voice' && studioPane !== 'studio') return;
    let cancelled = false;
    void hydrateSessionTts(stateRef.current).then(() => {
      if (cancelled) return;
      setState((cur) => ({ ...cur }));
    });
    return () => {
      cancelled = true;
    };
  }, [studioPane]);

  const persistEngine = (next: 'turbo' | 'wan') => {
    setEngine(next);
    try {
      localStorage.setItem(ENGINE_KEY, next);
    } catch {
      /* quota */
    }
  };

  const persistVoice = (next: 'elevenlabs' | 'f5') => {
    setVoiceProvider(next);
    try {
      localStorage.setItem(VOICE_KEY, next);
    } catch {
      /* quota */
    }
  };

  const patchRole = (id: string, patch: {
    title?: string;
    name?: string;
    line?: string;
    voiceNote?: string;
    voiceId?: string;
    voiceName?: string;
    performance?: string;
  }) => {
    let next: SeriesPilotState = {
      ...state,
      roles: state.roles.map((r) => (r.id === id ? { ...r, ...patch } : r)),
    };
    const role = next.roles.find((r) => r.id === id);
    if (role?.characterId && (patch.voiceId !== undefined || patch.voiceName !== undefined)) {
      next = setCharacterVoice(next, role.characterId, {
        voiceId: patch.voiceId ?? role.voiceId,
        voiceName: patch.voiceName ?? role.voiceName,
      });
    }
    persistState(next);
  };

  const addRole = () => {
    if (state.roles.length >= 8) {
      message.warning('Tối đa 8 vai.');
      return;
    }
    persistState({ ...state, roles: [...state.roles, newRoleRow()] });
  };

  const onRoleCanon = async (roleId: string, file: File | undefined) => {
    if (!file) return;
    const role = state.roles.find((r) => r.id === roleId);
    if (!role) return;
    if (!role.name.trim()) {
      message.warning('Điền tên người rồi gắn Canon.');
      return;
    }
    try {
      const imageDataUrl = await readImageFile(file);
      const ref = localFileRef(file);
      let next = { ...state, roles: state.roles.map((r) => (r.id === roleId ? { ...r } : r)) };
      next = ensurePilotGraph({ ...next, schemaVersion: PILOT_SCHEMA });
      const charId = next.roles.find((r) => r.id === roleId)?.characterId;
      if (!charId) {
        message.warning('Chưa gắn CHAR — điền tên vai + người.');
        return;
      }
      persistState(
        setCharacterCanon(next, charId, {
          canonFileName: ref.fileName,
          canonLocalPath: ref.localPath,
          canonImageDataUrl: imageDataUrl,
        }),
      );
      void saveCanonPixels(charId, imageDataUrl, ref.fileName);
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không gắn được Canon');
    }
  };

  const removeRole = (id: string) => {
    persistState({ ...state, roles: state.roles.filter((r) => r.id !== id) });
  };

  const stills = state.stills ?? [];
  const stillFilterId = activeShort?.id ?? active?.id;
  const visibleStills =
    stillOnlyActive && stillFilterId ? stillsForShort(stills, stillFilterId) : stills;

  const patchStill = (id: string, patch: Partial<(typeof stills)[number]>) => {
    persistState(
      { ...state, stills: stills.map((s) => (s.id === id ? { ...s, ...patch } : s)) },
    );
  };

  const addStill = () => {
    persistState(
      {
        ...state,
        stills: [
          ...stills,
          newStillRow({
            shortId: activeShort?.id ?? shorts[0]?.id ?? '',
            scene: activeShort?.scene ?? '',
          }),
        ],
      },
    );
  };

  const patchRun = (id: string, patch: Partial<SeriesShotRun>) => {
    const cur = stateRef.current;
    const shot = episodeShots(cur).find((s) => s.id === id);
    const prev = cur.runs[id] ?? { status: shot?.status ?? 'keyframe_ready' };
    persistState({ ...cur, runs: { ...cur.runs, [id]: { ...prev, ...patch } } });
  };

  const onKeyframeFile = async (id: string, file: File | undefined) => {
    if (!file) return;
    try {
      const keyframeDataUrl = await readImageFile(file);
      const ref = localFileRef(file);
      const st = stateRef.current;
      const shot = episodeShots(st).find((s) => s.id === id);
      const card = shot ? compileShotSceneCard(st, shot, previousSceneKf(st, shot, productionShorts(st))?.shot) : undefined;
      patchRun(id, {
        keyframeDataUrl,
        keyframeFileName: ref.fileName,
        keyframePath: ref.localPath,
        keyframeInheritedFrom: undefined,
        kfApproved: false,
        kfRetryOk: false,
        visualSpec: card?.visualSpec,
        visualQa: card?.visualSpec ? seedQaChecks(card.visualSpec) : undefined,
        status: 'keyframe_ready',
      });
      const cur = stateRef.current;
      const pack = productionShorts(cur);
      persistState(applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)));
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không gắn được KF01');
    }
  };

  const generateKfFromCanon = async (opts: {
    clipId: string;
    aspect: '9:16' | '16:9';
    visual: string;
    action?: string;
    location?: string;
    characterIds?: string[];
    prevKfUrl?: string;
    inheritFromId?: string;
    continuityNote?: string;
    peopleCount?: number;
    peopleNames?: string;
    atmosphere?: string;
    lightingLock?: string;
    speakers?: string;
    visualSpec?: VisualSpec;
    sceneMasterUrl?: string;
    correction?: string;
    failedKfUrl?: string;
  }) => {
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon mặt.');
      return false;
    }
    if (kfInflightRef.current) {
      message.warning('Đang vẽ 1 KF — chờ xong, không bấm lần 2.');
      return false;
    }
    kfInflightRef.current = opts.clipId;
    try {
    const failedRun = shortRunOf(stateRef.current, opts.clipId);
    if (lastGenerationFail(failedRun) && !failedRun.failedKfHash) {
      patchRun(opts.clipId, stampFailedInput(failedRun, failedRun.keyframeDataUrl));
    }
    const st = await hydratePilotCanon(stateRef.current);
    if (st !== stateRef.current) persistState(st);
    const faces: { name: string; role?: string; imageDataUrl: string }[] = [];
    const visualIds = (opts.characterIds ?? []).filter((id) => !/^CHAR-VO$/i.test(id) && !/loi binh|narrator/i.test(id));
    for (const row of canonStillRefs(st, visualIds).slice(0, Math.max(1, visualIds.length))) {
      const slim = await shrinkStillDataUrl(row.imageDataUrl);
      if (slim) faces.push({ ...row, imageDataUrl: slim });
    }
    let prevUrl: string | undefined;
    if (opts.prevKfUrl?.startsWith('data:image')) {
      prevUrl = await shrinkStillDataUrl(opts.prevKfUrl);
      if (!prevUrl) {
        message.warning('KF shot trước quá nặng — vẽ từ Canon + lệnh continuity, không gắn pixel shot trước.');
      }
    }
    let sceneUrl: string | undefined;
    if (opts.sceneMasterUrl?.startsWith('data:image') && opts.sceneMasterUrl !== opts.prevKfUrl) {
      sceneUrl = await shrinkStillDataUrl(opts.sceneMasterUrl);
    }
    let failedUrl: string | undefined;
    if (
      opts.failedKfUrl?.startsWith('data:image') &&
      opts.correction &&
      !soloCastFromNote(opts.correction) &&
      !/Remove every extra person|Only Linh|CAST: Linh only/i.test(opts.correction)
    ) {
      failedUrl = await shrinkStillDataUrl(opts.failedKfUrl);
    }
    const refs = mergeReferencePack({
      scene: sceneUrl ? { name: 'Scene Master', role: 'scene', imageDataUrl: sceneUrl } : undefined,
      prev: prevUrl ? { name: 'Previous KF', role: 'continuity', imageDataUrl: prevUrl } : undefined,
      identities: faces.map((f, i) => ({
        name: f.name,
        role: i === 0 ? 'identity' : 'identity-secondary',
        imageDataUrl: f.imageDataUrl,
      })),
    });
    if (failedUrl) {
      refs.unshift({ name: 'FAILED-STILL', role: 'continuity', imageDataUrl: failedUrl });
      refs.splice(4);
    }
    if (refs.length === 0) {
      const named = (st.characters ?? []).filter((c) => characterCanonReady(c));
      message.warning(
        named.length
          ? 'Có tên file Canon nhưng mất pixels (F5). Bấm Thay Canon một lần — KIT nhớ ảnh trên máy, không gửi lên server.'
          : 'Ảnh Canon mặt chưa có trong session. Gắn lại ảnh vai (mặt/tóc) rồi tạo KF cảnh.',
      );
      return false;
    }
    for (const c of st.characters ?? []) {
      const url = canonImageOf(st, c.id);
      if (url) void saveCanonPixels(c.id, url, c.canonFileName);
    }
    setStillBusy(opts.clipId);
    message.loading({ content: 'Đang vẽ 1 KF…', key: 'fx-kf-gen', duration: 0 });
      const res = await generateContentSeriesStill({
        prompt: seriesSceneStillPrompt({
          aspect: opts.aspect,
          visual: looksLikePackHeading(opts.visual) ? '' : opts.visual,
          action: looksLikePackHeading(opts.action) ? '' : opts.action,
          location: opts.location,
          refs,
          continuityNote: opts.continuityNote,
          peopleCount: opts.peopleCount ?? visualIds.length,
          peopleNames: opts.peopleNames,
          atmosphere: opts.atmosphere,
          lightingLock: opts.lightingLock,
          speakers: opts.speakers,
          visualSpec: opts.visualSpec,
          correction: opts.correction,
        }),
        aspect: opts.aspect,
        references: refs,
      });
      const fitted = res.imageDataUrl.startsWith('data:image')
        ? await prepareRunwayKf(res.imageDataUrl, opts.aspect, {
            people: opts.peopleCount ?? visualIds.length,
            faceSafe: true,
          })
        : res.imageDataUrl;
      const marked = fitted;
      let visualQa = opts.visualSpec ? seedQaChecks(opts.visualSpec) : undefined;
      if (opts.visualSpec && marked.startsWith('data:image')) {
        message.loading({ content: 'Đang chấm QA (không vẽ ảnh mới)…', key: 'fx-kf-gen', duration: 0 });
        try {
          const raw = await qaContentSeriesStill({
            imageDataUrl: marked,
            specJson: JSON.stringify(opts.visualSpec),
          });
          visualQa = parseVisionQa(raw, opts.visualSpec);
        } catch (e) {
          visualQa = {
            ...(visualQa ?? seedQaChecks(opts.visualSpec)),
            status: 'PENDING',
            notes: e instanceof Error ? e.message : 'Image QA lỗi — Chấm lại, đừng tạo ảnh mới.',
          };
          message.warning('Image QA chưa chấm được. Chấm lại trên hàng — đừng tạo 10 KF mới.');
        }
      }
      patchRun(opts.clipId, {
        keyframeDataUrl: marked,
        keyframeFileName: `kf-${opts.clipId}-canon.jpg`,
        keyframeInheritedFrom: undefined,
        kfApproved: false,
        visualSpec: opts.visualSpec,
        visualQa,
        kfTechNote: opts.continuityNote?.trim() || undefined,
        status: 'keyframe_ready',
      });
      if (visualQa?.status === 'REJECT') {
        message.warning(`Image QA REJECT ${opts.clipId}: ${(visualQa.hardFails.join(', ') || visualQa.notes || 'hard fail').slice(0, 160)}`);
      } else if (visualQa && visualQaAllowsApprove(visualQa)) {
        message.success(`Image QA ${visualQa.total} PASS — có thể Duyệt KF.`);
      }
      const cur = stateRef.current;
      persistState({
        ...cur,
        episode: cur.episode
          ? {
              ...cur.episode,
              shots: cur.episode.shots.map((s) =>
                s.id === opts.clipId ? { ...s, inheritFromShotId: opts.inheritFromId || s.inheritFromShotId } : s,
              ),
            }
          : cur.episode,
      });
      message.success({ content: `Đã vẽ 1 KF (${res.model}) — duyệt rồi mới I2V.`, key: 'fx-kf-gen' });
      return true;
    } catch (e) {
      message.error({ content: apiErrorMessage(e, 'Không vẽ được KF từ Canon.'), key: 'fx-kf-gen' });
      return false;
    } finally {
      kfInflightRef.current = undefined;
      setStillBusy(undefined);
    }
  };

  const rerunVisualQa = async (shotId: string) => {
    const pack = productionShorts(stateRef.current);
    const shot = pack.find((s) => s.id === shotId);
    if (!shot) return;
    const run = shotRunOf(stateRef.current, shot);
    if (!run.keyframeDataUrl?.startsWith('data:image')) {
      message.warning('Chưa có KF để chấm. Tạo 1 ảnh — đừng chấm không.');
      return;
    }
    const spec = compileShotSceneCard(stateRef.current, shot, previousSceneKf(stateRef.current, shot, pack)?.shot).visualSpec;
    const locked = isLockedTemplateKf(run);
    setStillBusy(shotId);
    try {
      const raw = await qaContentSeriesStill({
        imageDataUrl: run.keyframeDataUrl,
        specJson: JSON.stringify(spec),
      });
      let visualQa = parseVisionQa(raw, spec, { sceneMaster: locked });
      if (locked && run.kfApproved && visualQa.hardFails.length) {
        visualQa = {
          ...visualQa,
          hardFails: [],
          status: 'PASS',
          notes: 'Scene Master đã KHÓA — không BLOCK vì action/count/place. Ảnh này là chỗ/áo cho shot sau.',
        };
      }
      patchRun(shotId, { visualSpec: spec, visualQa });
      if (visualQaAllowsApprove(visualQa)) {
        message.success(
          locked
            ? `KF khóa: QA ${visualQa.total ?? '—'} — giữ KHÓA, không BLOCK Scene Master.`
            : `QA ${visualQa.total} PASS — Duyệt KF được.`,
        );
      } else {
        message.info(`QA ${visualQa.total ?? '—'} ${visualQa.status}${visualQa.hardFails.length ? ` · ${visualQa.hardFails.join(', ')}` : ''}`);
      }
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chấm lại QA thất bại. Ảnh cũ còn — đừng tạo KF mới.'));
    } finally {
      setStillBusy(undefined);
    }
  };

  const onClipFile = (id: string, file: File) => {
    if (file.size > 80_000_000) {
      message.error('Video xem thử ≤ 80MB — chỉ trên máy, không lên server.');
      return;
    }
    const prev = sessionClips[id];
    if (prev) URL.revokeObjectURL(prev);
    setSessionClips((m) => ({ ...m, [id]: URL.createObjectURL(file) }));
    patchRun(id, { localVideoPath: localFileRef(file).localPath });
  };

  const applyPack = () => {
    try {
      const parsed = parseFamixaPack(packText);
      if (parsed.error) {
        setPackResult({ ok: false, text: parsed.error });
        message.error(parsed.error);
        return;
      }
      persistState(
        replaceStoryFromParse(state, parsed, packText),
      );
      setActiveShortId(parsed.shorts[0]?.id);
      setActiveId(parsed.episode?.shots[0]?.id);
      setStudioPane('script');
      const text = `Đã nhận: ${packSummary(parsed)}. Chưa gửi Runway.`;
      setPackResult({ ok: true, text });
      message.success(text);
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Không nhận được pack.';
      setPackResult({ ok: false, text });
      message.error(text);
    }
  };

  const requestDraft = () => {
    const seed = packSeed.trim();
    if (seed.length < 12) {
      message.warning('Nhập hạt giống ít nhất một câu (12 ký tự).');
      return;
    }
    if (state.scriptLocked) {
      message.warning('Mở khóa kịch bản rồi mới đề xuất lại.');
      return;
    }
    const run = () => {
      setDraftBusy(true);
      void draftContentSeriesScript({
        seed,
        charactersHint: seriesCanonHint(state) || undefined,
        episodeHint: ep?.episode || ep?.title || undefined,
        brandId: famixaBrand?.id,
      })
        .then((res) => {
          setPackText(res.pack);
          setPackResult({ ok: true, text: `${res.costNote} Đọc lại rồi bấm Nhận pack — chưa khóa, chưa gửi I2V.` });
          message.success('Đã điền nháp vào ô. Sửa rồi Nhận pack.');
        })
        .catch((e) => {
          const text = apiErrorMessage(e, 'Không đề xuất được pack.');
          setPackResult({ ok: false, text });
          message.error(text);
        })
        .finally(() => setDraftBusy(false));
    };
    if (packText.trim()) {
      modal.confirm({
        title: 'Ghi đè ô dán?',
        content: 'Bản đề xuất sẽ thay chữ đang có. Nhận pack vẫn do bạn bấm.',
        okText: 'Đề xuất',
        cancelText: 'Giữ ô hiện tại',
        onOk: run,
      });
      return;
    }
    run();
  };

  const shotPackText = (shot: FamixaSeriesShot) =>
    packForShotEdit(shot, ep, state.packDraft, packText, stills);

  const putShotPackToPaste = (shot: FamixaSeriesShot) => {
    const text = shotPackText(shot);
    setPackText(text);
    const note = `Khối ${shot.id} trên ô dán — sửa rồi Nhận pack. Chưa sang shot sau.`;
    setPackResult({ ok: true, text: note });
    message.success(note);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const copyShotPack = (shot: FamixaSeriesShot) => {
    void navigator.clipboard.writeText(shotPackText(shot));
    message.success(`Đã copy khối ${shot.id} (không gồm shot sau)`);
  };

  const lockShotGraph = () => {
    const pruned = pruneEmptyShots(state);
    const mapped = applyDialogueMap(pruned);
    let next = applyContinuityChain(mapped, productionShorts(mapped));
    next = applyEditDurations(next, productionShorts(next));
    for (const sc of [...new Set(productionShorts(next).map(sceneIdOfShot))]) {
      if (!next.sceneMasters?.[sc]) next = upsertSceneMaster(next, deriveSceneMaster(next, sc));
    }
    const n = next.episode?.shots.length ?? 0;
    persistState(next);
    const remain = next.episode?.shots ?? [];
    if (active && !remain.some((s) => s.id === active.id)) setActiveId(remain[0]?.id);
    if (n === 0) {
      message.warning('Không còn shot có Script Beat. Sửa kịch bản rồi Nhận pack.');
      return;
    }
    const cov = coverageOf(next, remain);
    const dens = shotPlanDensity(remain);
    const densNote = dens.warn
      ? ` · mật độ ${dens.ratio.toFixed(1)} shot/beat — xem lại`
      : '';
    message.success(
      `SHOT GRAPH LOCKED — ${n} Short · thoại ${cov.spoken} · câm ${cov.silent}` +
        densNote +
        (cov.extraUnmapped.length ? ` · ${cov.extraUnmapped.length} câu chưa gắn` : '') +
        (cov.message ? ` · ${cov.message}` : ''),
    );
  };

  const removeShots = (ids: string[]) => {
    if (!ids.length) return;
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trước khi xóa shot.');
      return;
    }
    modal.confirm({
      title: `Xóa ${ids.length} shot khỏi plan?`,
      content: 'Chỉ gỡ khỏi bảng dựng. KF trên máy giữ nếu thêm lại cùng id. Không xóa thoại kịch bản.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Giữ',
      onOk: () => {
        const next = removeSceneShots(stateRef.current, ids);
        persistState(next);
        const remain = episodeShots(next);
        if (active && ids.includes(active.id)) setActiveId(remain[0]?.id);
        message.success(`Đã xóa ${ids.length} shot.`);
      },
    });
  };

  const selectShot = (shot: FamixaSeriesShot) => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    const bound = bindShotToMemory(state, shot);
    if (bound !== state) persistState(bound);
    setActiveId(shot.id);
  };

  const markKeyframe = () => {
    if (!ready || !active) return;
    patchRun(active.id, {
      status: 'keyframe_ready',
      kfApproved: true,
      kfRetryOk: true,
      continuity: Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true])),
    });
  };

  const sendTurbo = async (opts: {
    clipId: string;
    prompt: string;
    negative?: string;
    seconds: number;
    ratio: '16:9' | '9:16';
    engine: 'turbo' | 'wan';
    silent?: boolean;
    resume?: boolean;
    forceNew?: boolean;
    diagnostic?: boolean;
  }) => {
    const existing = shortRunOf(stateRef.current, opts.clipId);
    const resume = Boolean(opts.resume || (!opts.forceNew && shouldResumeTurboPoll(existing)));
    const keyframe = existing.keyframeDataUrl;
    if (!resume && !keyframe) {
      message.warning('Gắn KF cảnh (cả khung hình), đừng gửi ảnh mặt CHAR.');
      return false;
    }
    const imageDataUrl = keyframe;
    const prev = turboLockRef.current;
    let release = () => {};
    turboLockRef.current = new Promise<void>((resolve) => {
      release = resolve;
    });
    await prev;
    const quietMin = runwayQuietRemainMin(runwayQuietUntil);
    if (quietMin && !opts.resume) {
      message.warning(`KIT còn khóa gửi job mới (429 cũ). Bấm «Đã lên tier — mở gửi» rồi Gửi lại từng hàng. Hỏi lại · 0 cr vẫn dùng được.`);
      release();
      return false;
    }
    setTurboBusy(opts.clipId);

    const finishOk = (task: {
      videoUrl?: string | null;
      seconds?: number;
      model?: string;
      taskId?: string;
      videoBytes?: number | null;
      videoMime?: string | null;
      videoVerified?: boolean;
    }, wan: boolean) => {
      const current = shortRunOf(stateRef.current, opts.clipId);
      const sameLip = Boolean(
        task.videoUrl &&
          (current.lipsyncUrl === task.videoUrl || (current.lipsynced && current.previewUrl === task.videoUrl)),
      );
      const shotRow = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
      const silentTake = shotRow ? linesForShot(stateRef.current, shotRow).length === 0 : false;
      patchRun(opts.clipId, {
        status: 'turbo_testing',
        ...(sameLip
          ? { finalSource: 'FAL' as const, takeUrl: current.takeUrl || current.previewUrl }
          : { ...stampMuteTake(task.videoUrl || '', silentTake), lipsyncStatus: undefined }),
        runwaySpent: wan ? 0 : runwayCredits(task.seconds || opts.seconds).credits,
        model: task.model,
        turboStatus: 'SUCCEEDED',
        turboError: undefined,
        videoPipe: 'VIDEO_READY',
        videoVerified: true,
        videoBytes: task.videoBytes ?? undefined,
        videoMime: task.videoMime ?? undefined,
        runwayAttempts: patchRunwayAttempt(current.runwayAttempts, task.taskId || current.turboTaskId, {
          status: 'SUCCEEDED',
          outputUrl: task.videoUrl ?? undefined,
          downloadOk: true,
          videoBytes: task.videoBytes ?? undefined,
          videoMime: task.videoMime ?? undefined,
        }),
      });
      if (!opts.silent) {
        message.success(
          wan
            ? `Take Wan sẵn. Fal đã trừ ~1 đơn vị 720p — sổ KIT không ghi Runway cr.`
            : resume
              ? `Lấy được take từ task cũ (0 cr thêm).`
              : `Take sẵn. Runway đã trừ ~${runwayCredits(task.seconds || opts.seconds).credits} cr — sổ KIT chỉ ghi khi khóa take đạt.`,
        );
      }
      return true;
    };

    const markQuiet = (optsQuiet: { daily?: boolean; hits?: number; retryAfterSec?: number }) => {
      const until = nextRunwayQuietUntil({ ...optsQuiet, prev: runwayQuietUntil });
      persistRunwayQuietUntil(until);
      setRunwayQuietUntil(until);
    };

    const pollTask = async (taskId: string) => {
      const t0 = Date.now();
      let waitMs = 6000;
      let hits429 = 0;
      while (Date.now() - t0 < 240_000) {
        try {
          const task = await getContentSeriesTurbo(taskId);
          hits429 = 0;
          const live = shortRunOf(stateRef.current, opts.clipId);
          const pipeStatus =
            task.status === 'SUCCEEDED'
              ? task.videoUrl
                ? 'VIDEO_SUCCEEDED'
                : 'FILE_INVALID'
              : task.status === 'RUNNING' || task.status === 'PROCESSING'
                ? 'VIDEO_PROCESSING'
                : task.status === 'PENDING'
                  ? 'VIDEO_SUBMITTED'
                  : classifyVideoPipe({ ...live, turboStatus: task.status, turboTaskId: task.taskId });
          patchRun(opts.clipId, {
            turboStatus: task.status,
            turboTaskId: task.taskId,
            turboError: undefined,
            videoPipe: pipeStatus,
            runwayAttempts: patchRunwayAttempt(live.runwayAttempts, task.taskId, {
              status: task.status,
              outputUrl: task.videoUrl ?? undefined,
              failureCode: task.failureCode ?? parseFailureCode(task.error || ''),
            }),
          });
          if (task.status === 'SUCCEEDED' && task.videoUrl) {
            const wan = opts.engine === 'wan' || (task.model ?? '').startsWith('wan');
            if (task.videoVerified) return finishOk(task, wan);
            patchRun(opts.clipId, { videoPipe: 'VIDEO_DOWNLOADING' });
            try {
              const probed = await probeContentSeriesTake(task.videoUrl);
              if (probed.ok) {
                return finishOk(
                  { ...task, videoBytes: probed.bytes, videoMime: probed.mime },
                  wan,
                );
              }
              const lastText = explainTurboError(probed.error || 'Có URL nhưng file không đọc được.');
              const after = shortRunOf(stateRef.current, opts.clipId);
              patchRun(opts.clipId, {
                turboStatus: 'SUCCEEDED',
                turboError: lastText,
                videoPipe: 'DOWNLOAD_FAILED',
                videoVerified: false,
                runwayAttempts: patchRunwayAttempt(after.runwayAttempts, task.taskId, {
                  status: 'SUCCEEDED',
                  outputUrl: task.videoUrl,
                  downloadOk: false,
                  error: lastText,
                }),
              });
              if (!opts.silent) message.error(lastText);
              return false;
            } catch (probeErr) {
              if (looksLikeVideoUrl(task.videoUrl)) return finishOk(task, wan);
              const lastText = explainTurboError(apiErrorMessage(probeErr, 'Không xác được file take.'));
              patchRun(opts.clipId, { turboError: lastText, videoPipe: 'DOWNLOAD_FAILED', videoVerified: false });
              if (!opts.silent) message.error(lastText);
              return false;
            }
          }
          if (task.status === 'SUCCEEDED' && !task.videoUrl) {
            waitMs = 4000;
            continue;
          }
          if (task.status === 'FAILED' || task.status === 'CANCELLED') {
            const lastText = explainTurboError(task.error || `Turbo ${task.status}. Chưa có file.`);
            const after = shortRunOf(stateRef.current, opts.clipId);
            patchRun(opts.clipId, {
              turboStatus: task.status,
              turboError: lastText,
              turboTaskId: task.taskId,
              videoPipe: 'RUNWAY_FAILED',
              videoVerified: false,
              runwayAttempts: patchRunwayAttempt(after.runwayAttempts, task.taskId, {
                status: task.status,
                failureCode: task.failureCode ?? parseFailureCode(task.error || lastText),
                error: lastText,
              }),
            });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          waitMs = 6000;
        } catch (e) {
          const raw = apiErrorMessage(e, 'Không hỏi được task Runway.');
          if (isTurboDailyQuota(raw)) {
            markQuiet({ daily: true });
            const lastText = explainTurboError(raw);
            patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: lastText, turboTaskId: taskId });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          if (isTurboRateLimit(raw)) {
            hits429 += 1;
            const sec = parseRetryAfterSec(raw, 30);
            if (hits429 >= 3) {
              markQuiet({ hits: hits429, retryAfterSec: sec });
              const lastText = `Runway chặn liên tục (429). Đợi ~${runwayQuietRemainMin(nextRunwayQuietUntil({ hits: hits429, retryAfterSec: sec, prev: runwayQuietUntil }))} phút. Không bấm thêm.`;
              patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: lastText, turboTaskId: taskId });
              if (!opts.silent) message.error(lastText);
              return false;
            }
            patchRun(opts.clipId, {
              turboStatus: 'RETRY',
              turboError: `Runway 429 — hỏi lại task sau ${sec}s (0 cr).`,
              turboTaskId: taskId,
            });
            await new Promise((r) => setTimeout(r, sec * 1000));
            waitMs = Math.min(Math.max(waitMs * 2, sec * 1000), 30_000);
            continue;
          }
          const lastText = explainTurboError(raw);
          patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED', turboTaskId: taskId });
          if (!opts.silent) message.error(lastText);
          return false;
        }
        await new Promise((r) => setTimeout(r, waitMs));
      }
      const lastText = 'Runway chưa trả take. Đợi hết hạn mức rồi Hỏi lại · 0 cr — đừng gửi job mới.';
      patchRun(opts.clipId, {
        turboError: lastText,
        turboStatus: 'RETRY',
        turboTaskId: taskId,
        videoPipe: 'TIMEOUT',
      });
      if (!opts.silent) message.error(lastText);
      return false;
    };

    try {
      const latest = shortRunOf(stateRef.current, opts.clipId);
      if (resume && latest.turboTaskId?.trim()) {
        if (!opts.silent) message.info('Hỏi lại task Runway đã gửi — 0 cr.');
        return await pollTask(latest.turboTaskId.trim());
      }
      if (!imageDataUrl) {
        message.warning('Gắn KF cảnh (cả khung hình), đừng gửi ảnh mặt CHAR.');
        return false;
      }
      let sourceCheck = inspectKfDataUri(imageDataUrl);
      if (imageDataUrl.startsWith('data:image')) {
        try {
          const dim = await measureKfImage(imageDataUrl);
          sourceCheck = kfCheckFromMeasure(sourceCheck, dim.width, dim.height);
        } catch {
          sourceCheck = {
            ...sourceCheck,
            ok: false,
            reasons: [...sourceCheck.reasons, 'Không đọc được ảnh KF.'],
          };
        }
      }
      if (!sourceCheck.ok) {
        patchRun(opts.clipId, {
          kfCheck: sourceCheck,
          videoPipe: 'INPUT_INVALID',
          turboStatus: 'BLOCKED',
          turboError: sourceCheck.reasons.join(' '),
        });
        message.error(`Chưa gửi (0 cr): ${sourceCheck.reasons.join(' ')}`);
        return false;
      }
      const cleaned = sanitizeKitPrecheck(latest);
      if (cleaned) patchRun(opts.clipId, cleaned);
      const gated = { ...latest, ...cleaned };
      const shotForPrompt = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
      const compiledPrompt = shotForPrompt
        ? compileI2vPrompt(
            stateRef.current,
            shotForPrompt,
            i2vActionOf(stateRef.current, shotForPrompt) || opts.prompt,
            videoContext,
          )
        : compileRunwayPromptV1({ action: opts.prompt }).text;
      if (!opts.diagnostic && sameFailedInput(gated, dataUriHash(imageDataUrl), promptHashOf(compiledPrompt))) {
        message.warning('Circuit breaker — cùng KF + prompt đã FAIL. Không gửi Runway. Sửa KF rồi duyệt.');
        return false;
      }
      let lastText = '';
      for (let attempt = 0; attempt < 2; attempt++) {
        const prompt = compiledPrompt;
        const gate = preflightTurboSend({ prompt, imageDataUrl });
        if (!gate.ok) {
          message.error(`Chưa gửi (0 cr): ${gate.reasons.join(' ')}`);
          return false;
        }
        let createdTask = false;
        try {
          const current = shortRunOf(stateRef.current, opts.clipId);
          const takeHistory = [...(current.takeHistory ?? [])];
          if (!opts.diagnostic && current.previewUrl?.trim()) {
            takeHistory.unshift({ url: current.previewUrl.trim(), taskId: current.turboTaskId });
          }
          if (opts.engine !== 'wan' && !imageDataUrl.startsWith('data:image')) {
            throw new Error('KF phải là data-URI — không gửi URL gốc vào Runway (0 cr).');
          }
          const shotRow = (stateRef.current.episode?.shots ?? []).find((s) => s.id === opts.clipId);
          const peopleN = shotRow ? visibleFrameCast(stateRef.current, shotRow).count : 0;
          const kf =
            opts.engine === 'wan'
              ? imageDataUrl
              : await prepareRunwayKf(imageDataUrl, opts.ratio, { people: peopleN });
          const sentDim = kf.startsWith('data:image')
            ? await measureKfImage(kf).catch(() => ({ width: 0, height: 0 }))
            : { width: 0, height: 0 };
          let sentCheck = inspectRunwayPayload(kf, opts.ratio);
          sentCheck = kfCheckFromMeasure(sentCheck, sentDim.width, sentDim.height);
          const exactPx =
            (sentDim.width === 1280 && sentDim.height === 720) ||
            (sentDim.width === 720 && sentDim.height === 1280);
          sentCheck = {
            ...sentCheck,
            width: sentDim.width || sentCheck.width,
            height: sentDim.height || sentCheck.height,
            reasons: exactPx
              ? sentCheck.reasons.filter((r) => !/pixel|1280|720|chưa đo/i.test(r))
              : sentCheck.reasons,
            ok:
              (exactPx || sentCheck.reasons.filter((r) => !/pixel|1280|720|chưa đo/i.test(r)).length === 0) &&
              exactPx,
            checks: [
              ...sentCheck.checks.filter((c) => c.id !== 'pixels' && c.id !== 'res'),
              {
                id: 'pixels',
                ok: exactPx,
                label:
                  sentDim.width && sentDim.height
                    ? `${sentDim.width}×${sentDim.height}`
                    : 'chưa đo pixel',
              },
            ],
          };
          if (opts.engine !== 'wan' && !sentCheck.ok) {
            patchRun(opts.clipId, {
              kfCheck: sourceCheck,
              sentKfCheck: sentCheck,
              videoPipe: 'INPUT_INVALID',
              turboStatus: 'BLOCKED',
              turboError: sentCheck.reasons.join(' ') || 'Normalize JPEG 1280×720 thất bại.',
            });
            message.error(`Chưa gửi (0 cr): ${sentCheck.reasons.join(' ') || 'JPEG 1280×720 bắt buộc.'}`);
            return false;
          }
          const job = buildRunwayJob({
            shotId: opts.clipId,
            image: kf,
            prompt,
            duration: opts.seconds,
            ratio: opts.ratio,
            width: sentDim.width || sentCheck.width,
            height: sentDim.height || sentCheck.height,
          });
          if (!job.ok) {
            patchRun(opts.clipId, {
              kfCheck: sourceCheck,
              sentKfCheck: sentCheck,
              videoPipe: 'INPUT_INVALID',
              turboStatus: 'BLOCKED',
              turboError: job.blocked.reasons.join(' ') || 'RUNWAY BLOCKED',
            });
            message.error(`RUNWAY BLOCKED (0 cr): ${job.blocked.reasons.join(' · ')}`);
            return false;
          }
          if (!opts.diagnostic && sameRequestBlocked(current, job.fingerprint)) {
            message.warning('SAME REQUEST — circuit. Không gửi lại cùng KF + prompt + model + duration.');
            return false;
          }
          const started = await startContentSeriesTurbo({
            clipId: opts.clipId,
            prompt: job.payload.promptText,
            negativePrompt: opts.negative,
            imageDataUrl: kf,
            seconds: opts.seconds,
            ratio: opts.ratio,
            engine: opts.engine,
          });
          createdTask = true;
          const estimatedCost = opts.engine === 'wan' ? 0 : runwayCredits(opts.seconds).credits;
          const sentBytes = kf.startsWith('data:') ? Math.max(0, Math.floor((kf.length * 3) / 4) - 32) : sourceCheck.bytes;
          const keepTake = Boolean(opts.diagnostic && current.previewUrl?.trim());
          const attempts = startRunwayAttempt(current, {
            taskId: started.taskId,
            submitOk: true,
            status: started.status,
            estimatedCost,
            refundStatus: 'PENDING',
            billed: undefined,
            httpStatus: 200,
            model: started.model || 'gen4_turbo',
            duration: opts.seconds,
            ratio: opts.ratio,
            promptHash: job.exact.promptHash,
            fingerprint: job.fingerprint,
            exactRequest: {
              promptText: job.exact.promptText,
              promptHash: job.exact.promptHash,
              kfHash: job.exact.kfHash,
              model: job.exact.model,
              duration: job.exact.duration,
              ratio: job.exact.ratio,
              apiVersion: job.exact.apiVersion,
              compiler: job.exact.promptCompiler,
            },
            diagnostic: opts.diagnostic,
            source: {
              mime: sourceCheck.mime,
              bytes: sourceCheck.bytes,
              width: sourceCheck.width,
              height: sourceCheck.height,
              hash: dataUriHash(imageDataUrl),
            },
            kf: {
              mime: 'image/jpeg',
              bytes: sentBytes,
              width: sentDim.width,
              height: sentDim.height,
              hash: dataUriHash(kf),
            },
          });
          const diagRow = attemptToDiagRow(opts.clipId, attempts.at(-1)!, {
            kfId: current.keyframeFileName || opts.clipId,
          });
          patchRun(opts.clipId, {
            status: keepTake ? current.status : 'turbo_testing',
            model: started.model,
            previewUrl: keepTake ? current.previewUrl : undefined,
            takeUrl: keepTake ? current.takeUrl : current.takeUrl,
            takeHistory: takeHistory.slice(0, 6),
            turboTaskId: started.taskId,
            turboStatus: started.status,
            turboError: undefined,
            videoPipe: keepTake ? current.videoPipe || 'VIDEO_READY' : 'VIDEO_SUBMITTED',
            videoVerified: keepTake ? current.videoVerified : undefined,
            kfCheck: sourceCheck,
            sentKfCheck: sentCheck,
            kfRetryOk: false,
            runwayEstimated: estimatedCost,
            runwayRefund: 'PENDING',
            runwayAttempts: attempts,
            runwayDiagnostics: [...(current.runwayDiagnostics ?? []), diagRow].slice(-12),
          });
          if (started.usedPlaceholderImage) {
            message.warning('Không nhận được KF01 — gửi lại sau khi gắn ảnh cảnh.');
          }
          const ok = await pollTask(started.taskId);
          const after = shortRunOf(stateRef.current, opts.clipId);
          const last = latestAttempt(after);
          if (last) {
            const klass = classifyRunwayFailure({
              sentOk: true,
              httpStatus: last.httpStatus ?? 200,
              runwayStatus: after.turboStatus,
              failureCode: last.failureCode,
              error: after.turboError,
            });
            const ready = Boolean(ok || (keepTake && current.previewUrl));
            const failedStamp = ready
              ? { failedKfHash: undefined, failedPromptHash: undefined, runwayRefund: 'NONE' as const, renderFailure: false }
              : stampFailedInput(after, imageDataUrl, prompt);
            patchRun(opts.clipId, {
              previewUrl: keepTake ? current.previewUrl : after.previewUrl,
              takeUrl: keepTake ? current.takeUrl : after.takeUrl,
              videoPipe: keepTake && current.previewUrl ? 'VIDEO_READY' : after.videoPipe,
              videoVerified: keepTake ? current.videoVerified : after.videoVerified,
              runwayBilled: ready ? (current.runwayBilled ?? current.runwaySpent ?? 0) + estimatedCost : current.runwayBilled,
              ...failedStamp,
              runwayAttempts: patchRunwayAttempt(after.runwayAttempts, started.taskId, {
                classification: klass,
                billed: ready ? estimatedCost : undefined,
                refundStatus: ready ? 'NONE' : 'REFUND_PENDING',
              }),
              runwayDiagnostics: [...(after.runwayDiagnostics ?? [])].map((row) =>
                row.runwayJobId === started.taskId
                  ? {
                      ...row,
                      runwayStatus: after.turboStatus,
                      outputUrl: last.outputUrl || after.previewUrl,
                      downloadOk: last.downloadOk,
                      videoBytes: last.videoBytes ?? after.videoBytes,
                      errorCode: last.failureCode,
                      errorMessage: after.turboError,
                      classification: klass,
                    }
                  : row,
              ),
            });
          }
          return ok;
        } catch (e) {
          lastText = explainTurboError(
            apiErrorMessage(e, 'Không gửi được I2V. Kiểm tra key Runway / Fal (Model AI → Video).'),
          );
          if (isTurboDailyQuota(lastText)) {
            const until = nextRunwayQuietUntil({ daily: true, prev: runwayQuietUntil });
            persistRunwayQuietUntil(until);
            setRunwayQuietUntil(until);
            patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED' });
            if (!opts.silent) message.error(lastText);
            return false;
          }
          if (canRetryTurboStart(lastText, createdTask) && attempt === 0) {
            const sec = parseRetryAfterSec(lastText, 30);
            patchRun(opts.clipId, {
              turboStatus: 'RETRY',
              turboError: `Runway 429 — đợi ${sec}s rồi gửi lại (chưa tạo job, 0 cr).`,
            });
            await new Promise((r) => setTimeout(r, sec * 1000));
            continue;
          }
          patchRun(opts.clipId, { turboError: lastText, turboStatus: 'FAILED' });
          if (!opts.silent) message.error(lastText);
          return false;
        }
      }
      patchRun(opts.clipId, { turboError: lastText || 'Runway chưa trả take.', turboStatus: 'FAILED' });
      return false;
    } finally {
      setTurboBusy(undefined);
      release();
    }
  };

  const resumePollBoot = useRef(false);
  useEffect(() => {
    if (resumePollBoot.current) return;
    const hung = productionShorts(stateRef.current).find((s) =>
      shouldResumeTurboPoll(shotRunOf(stateRef.current, s)),
    );
    if (!hung) return;
    resumePollBoot.current = true;
    void sendTurbo({
      clipId: hung.id,
      prompt: '',
      seconds: hung.seconds || 10,
      ratio: outputAspectOf(stateRef.current),
      engine: 'turbo',
      silent: true,
      resume: true,
    });
  }, [state.episode?.shots?.length]);

  const startTurbo = () => {
    if (!active || !run) {
      message.warning('Chọn shot đang làm.');
      return;
    }
    const lock = lockFromGraph(state, active);
    const pre = studioI2vPrecheck({
      lock,
      action: run.shotAction,
      keyframeDataUrl: run.keyframeDataUrl,
      status: run.status,
      unlocked,
      sceneLocked: state.sceneLocked,
      scriptLocked: state.scriptLocked,
      shortsReady: canWorkScene(state),
      engine,
      hasEngineKey: engine === 'wan' ? keys.fal : keys.runway,
      state,
      shot: active,
      videoContext,
      run,
    });
    if (!pre.ok) {
      modal.warning({
        title: 'Pre-check chưa đạt — 0 cr',
        content: pre.items
          .filter((i) => !i.ok)
          .map((i) => i.label)
          .join(' · '),
      });
      return;
    }
    const cost = generateCost(engine, active.seconds);
    modal.confirm({
      title: engine === 'wan' ? 'Pre-check đạt · gửi Wan (Fal)' : `Confirm credit · ước ${cost.credits} cr (5 cr/s)`,
      content: `${pre.warnings.length ? `${pre.warnings.join(' ')} ` : ''}RUNWAY COST — Estimated: ${cost.credits} cr · Status: PENDING đến khi SUCCEEDED + file đọc được. Lỗi generation = REFUND PENDING (Runway), KIT không ghi «đã trừ». Không gửi lại cùng KF.`,
      okText: engine === 'wan' ? 'Gửi Wan' : `Confirm · ước ${cost.credits} cr`,
      cancelText: 'Không gửi',
      onOk: () =>
        sendTurbo({
          clipId: active.id,
          prompt: pre.prompt,
          negative: undefined,
          seconds: active.seconds,
          ratio: outputAspectOf(state),
          engine,
        }),
    });
  };

  const checkTurbo = (prompt: string, imageDataUrl?: string) => {
    const gate = preflightTurboSend({ prompt, imageDataUrl });
    if (!gate.ok) {
      message.error(`Check máy: chưa đạt (0 cr). ${gate.reasons.join(' ')}`);
      return;
    }
    if (gate.warnings.length) {
      message.warning(`Check máy: cảnh báo (0 cr). ${gate.warnings.join(' ')} Vẫn gửi được — moderation có thể trừ cr.`);
      return;
    }
    message.success(`Check máy: đạt (0 cr). Prompt ${gate.prompt.length} ký tự — có thể gửi Turbo.`);
  };

  const startShortTurbo = () => {
    if (!canWorkShorts(state)) {
      message.warning(
        !voiceProductionReady(state)
          ? 'Khóa Full Voice (thoại COMPLETE) trước khi tốn credit hình/video.'
          : 'Khóa kịch bản ở bước 1 rồi mới làm short.',
      );
      setStudioPane('script');
      return;
    }
    if (!ready || !activeShort) {
      message.warning('Điền vai rồi dán pack.');
      return;
    }
    const cost = runwayCredits(activeShort.seconds);
    const prompt = compileRunwayPromptV1({ action: activeShort.motionPrompt }).text;
    const gate = preflightTurboSend({ prompt, imageDataUrl: shortRun?.keyframeDataUrl });
    if (!gate.ok) {
      modal.warning({
        title: 'Check máy: chưa đạt — chưa trừ credit',
        content: gate.reasons.join(' '),
      });
      return;
    }
    modal.confirm({
      title: `Check đạt · gửi Turbo dọc ~${cost.credits} cr`,
      content: `Check trên máy: đạt (0 cr). Runway không check hộ trước. Sẽ gửi: ${gate.prompt}`,
      okText: `Gửi (~${cost.credits} cr)`,
      cancelText: 'Không gửi',
      onOk: () =>
        sendTurbo({
          clipId: activeShort.id,
          prompt,
          seconds: activeShort.seconds,
          ratio: '9:16',
          engine,
        }),
    });
  };

  const lockShort = () => {
    if (!activeShort || !shortRun) return;
    if (!reviewComplete(shortRun)) {
      message.warning('Tick đủ 4 câu rồi khóa short.');
      return;
    }
    const next: SeriesPilotState = {
      ...state,
      runs: {
        ...state.runs,
        [activeShort.id]: {
          ...shortRun,
          status: 'approved',
          credits: shortRun.runwaySpent ?? shortRun.credits ?? runwayCredits(activeShort.seconds).credits,
        },
      },
    };
    persistState(next);
    if (canWorkScene(next)) {
      message.success('Đã khóa hết short. Sang dựng cảnh 16:9.');
      setStudioPane('studio');
    }
  };

  const lockShot = () => {
    if (active) lockShotOf(active);
  };

  const lockShotOf = (shot: FamixaSeriesShot, opts?: { stay?: boolean; autoReview?: boolean }) => {
    const cur = stateRef.current;
    const take = shotRunOf(cur, shot);
    const reviewed = opts?.autoReview
      ? {
          ...take,
          review: { character: true, motion: true, emotion: true, canon: true },
        }
      : take;
    if (!reviewComplete(reviewed)) {
      message.warning('Tick đủ 4 câu review — hoặc mở Shot Detail.');
      setActiveId(shot.id);
      return;
    }
    const lockedRun = {
      ...reviewed,
      status: 'approved' as const,
      credits: take.runwaySpent ?? take.credits ?? runwayCredits(shot.seconds).credits,
    };
    let nextState: SeriesPilotState = applyShotLockToGraph(
      {
        ...cur,
        runs: {
          ...cur.runs,
          [shot.id]: lockedRun,
        },
      },
      shot,
      lockedRun.shotAction ?? take.shotAction,
    );
    const pack = episodeShots(nextState);
    const i = pack.findIndex((s) => s.id === shot.id);
    const next = pack[i + 1];
    if (next) nextState = bindShotToMemory(nextState, next);
    persistState(nextState);
    if (opts?.stay) {
      message.success(`PASS ${shCode(state, shot)}.`);
      return;
    }
    if (next) {
      setActiveId(next.id);
      message.success(`Đã khóa ${shCode(state, shot)}. Shot sau kế thừa Memory + KF cảnh.`);
    } else {
      setStudioPane('timeline');
      message.success(`Đã khóa ${shCode(state, shot)}. Timeline · Final — ghi chú ghép rồi khóa cảnh.`);
    }
  };

  const failShot = () => {
    if (!active) return;
    patchRun(active.id, { status: 'rejected' });
  };

  const setReview = (axis: SeriesReviewAxis, on: boolean) => {
    if (!active || !run) return;
    patchRun(active.id, {
      status: run.status === 'turbo_testing' || run.status === 'rejected' ? 'reviewed' : run.status,
      review: { ...run.review, [axis]: on },
    });
  };

  const lockScene = () => {
    if (!sceneReady) return;
    persistState({ ...state, sceneLocked: true });
    message.success('Đã khóa cảnh (Final). I2V tắt đến khi mở khóa trên Timeline.');
  };

  const lockScript = () => {
    if (!canLockScript(state)) {
      message.warning(
        (state.scenes?.length ?? 0) > 0 && !state.storyReviewed
          ? 'Duyệt Parsed Story (nút «Parsed Story đúng») trước khi khóa kịch bản.'
          : needsInheritanceReview(state)
          ? 'Duyệt kế thừa trạng thái tập trước (card Trạng thái chuyện) trước khi khóa kịch bản.'
          : state.roles.length > 0 && !state.castLocked
          ? 'Khóa Cast & Canon (ảnh + giọng từng vai) trước.'
          : state.roles.length > 0 && !state.roles.every((r) => roleCanonReady(state, r))
            ? 'Gắn ảnh Character Canon cho đủ vai (mặt/tóc — không phải ảnh bàn ăn).'
            : 'Nhận pack (có short hoặc shot) và điền đủ vai nếu đã thêm vai.',
      );
      return;
    }
    persistState(primeLongShotsOnScriptLock({ ...state, scriptLocked: true }));
    setStudioPane('voice');
    message.success('Đã khóa kịch bản. Duyệt Full Voice — chưa tạo hình/video.');
  };

  const lockVoice = (opts?: { skipRegen?: boolean }) => {
    const why = voiceLockBlockReason(state);
    if (why && !opts?.skipRegen) {
      message.warning(
        !keys.elevenLabs && state.voicePreview?.status !== 'complete'
          ? 'Chưa có key ElevenLabs — Cấu hình AI, rồi bấm Tạo Full Voice.'
          : why,
      );
      return;
    }
    const script = deriveVoiceScript(state);
    if (opts?.skipRegen) {
      if (script.lines.length === 0 || script.lines.some((l) => !l.voiceId)) {
        message.warning(why ?? 'Gán Voice Canon đủ vai trước khi khóa.');
        return;
      }
    }
    persistState({
      ...stampDialoguePerformances(state),
      scriptLocked: true,
      voiceLocked: true,
      voicePreview: {
        ...(state.voicePreview ?? emptyVoicePreview(script)),
        status: 'complete',
        issues: [],
        operatorConfirmed: Boolean(opts?.skipRegen) || Boolean(state.voicePreview?.operatorConfirmed),
      },
    });
    setStudioPane('studio');
    message.success('VOICE LOCKED. Duyệt cách chia Short từ kịch bản — chưa tạo hình.');
  };

  const isStudio = studioPane === 'studio';
  const prodQueue = isStudio ? productionShorts(state) : [];
  const sceneShotKey = prodQueue.map((s) => s.id).join('|');
  useEffect(() => {
    if (!prodQueue.length) return;
    const ids = new Set(prodQueue.map((s) => s.id));
    const pick = prodQueue;
    setCutFrom((cur) => (cur && ids.has(cur) ? cur : pick[0]!.id));
    setCutTo((cur) => (cur && ids.has(cur) ? cur : pick[Math.min(9, pick.length - 1)]!.id));
  }, [sceneShotKey]);
  const cutRange = isStudio
    ? cutPick.length
      ? pickShots(prodQueue, cutPick)
      : shotsInInclusiveRange(prodQueue, cutFrom, cutTo)
    : [];
  const sceneKfPlan = isStudio ? buildSceneKfPlan(state, prodQueue) : [];
  const sceneVideo = isStudio ? readyV2VideoShots(state, cutRange.length ? cutRange : prodQueue) : { ready: [], blocked: [] };
  const sceneKfNew = isStudio ? sceneKfToGenerate(prodQueue, sceneKfPlan, state) : [];
  const sceneKfReuse = isStudio ? sceneKfPlan.filter((p) => p.lane === 'reuse').length : 0;
  const scenePlaceHint =
    active && lockFromGraph(state, active).locked
      ? continuityPlaceHint(lockFromGraph(state, active), active, shotRunOf(state, active).shotAction)
      : undefined;
  const hasVideoKey = engine === 'wan' ? keys.fal : keys.runway;
  const sceneBatchCredits = sceneVideo.ready.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
  const sceneBatchLabel =
    !hasVideoKey
      ? engine === 'wan'
        ? 'Cần key Fal (Cấu hình AI → Video)'
        : 'Cần key Runway (Cấu hình AI → Video)'
      : sceneVideo.ready.length === 0
        ? prodQueue.length === 0
          ? 'Chưa có Short từ kịch bản'
          : v2SceneBlockReason(state) ||
            (sceneVideo.blocked[0]
              ? `${sceneVideo.blocked.length} Short: ${sceneVideo.blocked[0].reason}`
              : 'Chưa có Short để gửi')
        : engine === 'wan'
          ? `Tạo video ${sceneVideo.ready.length} Short · Wan`
          : state.i2vProductionMode
            ? `BATCH · tối đa 3 · ước −${sceneBatchCredits} cr`
            : `SAFE MODE · 1 shot · Confirm credit`;

  const stillArgsFor = (
    s: FamixaSeriesShot,
    pack: FamixaSeriesShot[],
    continuityNote?: string,
    edit?: { correction?: string; failedKfUrl?: string },
  ) => {
    const st = stateRef.current;
    const loc = lockFromGraph(st, s);
    const master = sceneMasterOf(st, sceneIdOfShot(s));
    const prev = previousSceneKf(st, s, pack);
    const lock = visualLockShot(st, pack);
    const card = compileShotSceneCard(st, s, prev?.shot);
    persistState(applyVisibleCast(st, s.id, card.cast.ids));
    const solo = soloCastFromNote(edit?.correction);
    const spec = solo ? applySoloCast(card.visualSpec, solo) : card.visualSpec;
    const soloFrame = Boolean(solo) || (!spec.secondary.length && spec.subjectKind !== 'prop');
    const count = peopleCountForSpec(spec);
    const wide =
      spec.framing === 'WIDE' || spec.framing === 'MEDIUM' || spec.framing === 'ESTABLISHING';
    const lockUrl = lock ? shotRunOf(st, lock).keyframeDataUrl : undefined;
    const prevUrl = prev?.run.keyframeDataUrl;
    const attachPrev =
      Boolean(prevUrl?.startsWith('data:image')) &&
      !soloFrame &&
      spec.framing !== 'INSERT' &&
      spec.subjectKind !== 'prop' &&
      shouldAttachPrevKf(prev?.run.visualSpec?.framing, spec.framing);
    const placeLock = [
      master.location || loc.environment || s.location,
      master.lighting || card.lighting,
      prev ? `Same locked room as ${shCode(st, prev.shot)}. Do not copy that crop.` : '',
    ]
      .filter(Boolean)
      .join('. ');
    return {
      clipId: s.id,
      aspect: outputAspectOf(st),
      visual: placeLock,
      action: spec.shotAction || card.stillAction,
      location: [master.location || loc.environment || s.location, card.lighting].filter(Boolean).join('. '),
      characterIds: identityCanonIds(spec, soloFrame && spec.primary?.id ? [spec.primary.id] : card.cast.ids),
      prevKfUrl: attachPrev ? prevUrl : undefined,
      sceneMasterUrl: lockUrl?.startsWith('data:image') ? lockUrl : undefined,
      inheritFromId: prev?.shot.id,
      peopleCount: count,
      peopleNames: spec.primary?.name || (count <= 1 ? card.cast.names[0] : card.cast.names.join(', ')) || '',
      atmosphere: compileShotStillMood(st, s, spec.shotAction || card.stillAction),
      lightingLock: card.lighting,
      speakers: spec.framing === 'INSERT' ? '' : card.speakerNames.join(', '),
      visualSpec: spec,
      continuityNote: [
        continuityNote?.trim(),
        wide ? peopleCountLock(card.cast, prev ? visibleFrameCast(st, prev.shot).count : undefined) : '',
      ]
        .filter(Boolean)
        .join(' '),
      correction: edit?.correction,
      failedKfUrl: soloFrame ? undefined : edit?.failedKfUrl,
    };
  };

  const generateSceneKf = (onlyIds?: string[]) => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản rồi mới dựng cảnh.');
      return;
    }
    if (!voiceProductionReady(state)) {
      message.warning('Duyệt thoại (VOICE LOCKED) trước khi tạo hình.');
      setStudioPane('voice');
      return;
    }
    if (state.shotGraphLocked === false) {
      message.warning('Duyệt cách chia Short rồi mới tạo hình. KIT không vẽ Short rỗng.');
      return;
    }
    const packNow = productionShorts(stateRef.current);
    const unlocked = [...new Set(packNow.map(sceneIdOfShot))].filter((sc) => !sceneMasterOf(stateRef.current, sc).locked);
    if (unlocked.length) {
      message.warning(`Khóa Scene Master (${unlocked.join(', ')}) trước khi tạo hình.`);
      return;
    }
    if (state.outputAspect !== '16:9' && state.outputAspect !== '9:16') {
      message.warning('Chọn khung xuất 16:9 hoặc 9:16 trên Scene Master trước khi vẽ KF.');
      return;
    }
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.');
      return;
    }
    void (async () => {
      let st = await hydratePilotCanon(stateRef.current);
      if (st !== stateRef.current) st = persistState(st);
      st = persistState(applyContinuityChain(st, productionShorts(st)));
      const pack = productionShorts(st);
      let plan = buildSceneKfPlan(st, pack);
      let todo = sceneKfToGenerate(pack, plan, st);
      const lock = visualLockShot(st, pack);
      if (onlyIds?.length) {
        todo = pack.filter((s) => onlyIds.includes(s.id) && shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip);
      } else {
        const seq = new Set(sequentialKfIds(st, pack));
        todo = todo.filter((s) => seq.has(s.id) && shotHasValidAction(s, shotRunOf(st, s)));
        todo = todo.filter((s) => {
          if (lock && s.id === lock.id) return false;
          return !isLockedTemplateKf(shotRunOf(st, s)) && !isOperatorSuppliedKf(shotRunOf(st, s));
        });
      }
      if (todo.length === 0) {
        message.warning(
          onlyIds?.length
            ? 'Short này HOLD/SKIP hoặc không có Action — không vẽ KF.'
            : `Không có Short nào cần tạo hình (${plan.filter((p) => p.eligible).length} Short hợp lệ).`,
        );
        return;
      }
      const one = nextShotNeedingKf(todo, todo.map((s) => s.id), lock?.id) || todo[0]!;
      todo = [one];
      const reuseIds = new Set(plan.filter((p) => p.lane === 'reuse' && p.eligible).map((p) => p.shotId));
      const reuseN = todo.filter((s) => reuseIds.has(s.id)).length;
      const draw = todo.filter((s) => !reuseIds.has(s.id));
      persistState(applySceneKfReuses(stateRef.current, pack, plan));
      if (reuseN && !draw.length) {
        persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
        message.success(`REUSE ${shCode(st, one)} — 0 Gemini. Duyệt KF rồi mới shot tiếp.`);
        return;
      }
      if (kfInflightRef.current) {
        message.warning('Đang vẽ 1 KF — chờ xong.');
        return;
      }
      for (const s of draw) {
        if (!shotHasValidAction(s, shotRunOf(stateRef.current, s))) continue;
        const ok = await generateKfFromCanon(stillArgsFor(s, pack));
        if (!ok) {
          message.error(`Dừng KF tại ${shCode(stateRef.current, s)}.`);
          return;
        }
        const qa = shotRunOf(stateRef.current, s).visualQa;
        if (qa && !visualQaAllowsApprove(qa)) {
          message.warning(`${shCode(stateRef.current, s)} chưa PASS — sửa đúng lỗi, đừng tạo shot tiếp.`);
          return;
        }
      }
      persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
    })();
  };

  const regenerateSelectedKf = (ids: string[], continuityNote?: string) => {
    if (!ids.length) {
      message.warning('Chọn shot trên KF Review.');
      return;
    }
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.');
      return;
    }
    void (async () => {
      let st = await hydratePilotCanon(stateRef.current);
      if (st !== stateRef.current) st = persistState(st);
      const pack = productionShorts(st);
      const requested = pack.filter((s) => ids.includes(s.id));
      const targets = requested.filter((s) => shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip);
      if (!targets.length) {
        message.warning(
          requested.length
            ? 'Shot đã chọn đang HOLD/SKIP — không vẽ KF.'
            : 'Không tìm thấy Short để tạo lại.',
        );
        return;
      }
      const one = targets[0]!;
      const failed = shotRunOf(st, one).keyframeDataUrl;
      if (kfInflightRef.current) {
        message.warning('Đang vẽ 1 KF — chờ xong.');
        return;
      }
      const run = shotRunOf(st, one);
      const spec = run.visualSpec ?? stillArgsFor(one, pack).visualSpec;
      const correction =
        continuityNote?.trim() ||
        (spec && run.visualQa?.hardFails.length
          ? compileCorrectionPrompt(spec, [], run.visualQa.evidence || run.visualQa.notes)
          : undefined);
      const solo = soloCastFromNote(correction);
      const nextSpec = spec && solo ? applySoloCast(spec, solo) : spec;
      patchRun(one.id, {
        kfForceNew: true,
        kfApproved: false,
        kfTechNote: correction,
        visualSpec: nextSpec,
      });
      const ok = await generateKfFromCanon(
        stillArgsFor(one, pack, undefined, {
          correction,
          failedKfUrl: failed,
        }),
      );
      if (!ok) {
        message.error(`Dừng KF tại ${shCode(stateRef.current, one)}.`);
        return;
      }
      persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
    })();
  };

  const approveSceneKf = (ids?: string[]) => {
    const gates = Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true]));
    const range = shotsInInclusiveRange(prodQueue, cutFrom, cutTo);
    const pack = ids?.length ? prodQueue.filter((s) => ids.includes(s.id)) : range;
    const chained = applyContinuityChain(state, productionShorts(state));
    const links = buildContinuityChain(chained, productionShorts(chained));
    const runs = { ...chained.runs };
    let n = 0;
    let blocked = 0;
    const reasons: string[] = [];
    for (const s of pack) {
      const run = shotRunOf(chained, s);
      if (!run.keyframeDataUrl || run.status === 'approved' || run.kfApproved) continue;
      const spec = run.visualSpec ?? compileShotSceneCard(chained, s, previousSceneKf(chained, s, productionShorts(chained))?.shot).visualSpec;
      const qa = run.visualQa ?? seedQaChecks(spec);
      const why = approveBlockReason(qa);
      if (why) {
        blocked += 1;
        if (reasons.length < 3) reasons.push(`${shCode(chained, s)}: ${why}`);
        runs[s.id] = { ...run, visualSpec: spec, visualQa: qa };
        continue;
      }
      const link = links.find((l) => l.shotId === s.id);
      runs[s.id] = {
        ...run,
        status: run.status === 'turbo_testing' || run.status === 'reviewed' ? run.status : 'keyframe_ready',
        kfApproved: true,
        kfRetryOk: true,
        continuity: run.continuity ?? gates,
        startState: run.startState ?? link?.start,
        endState: run.endState ?? link?.end,
        transitionType: run.transitionType ?? link?.transitionType,
        stateLocked: true,
        visualSpec: spec,
        visualQa: qa,
      };
      n += 1;
    }
    persistState({ ...chained, runs });
    if (blocked) {
      message.warning(
        n
          ? `Duyệt ${n} KF. ${blocked} bị chặn Image QA — ${reasons.join(' · ')}`
          : `Không duyệt KF: Image QA chưa PASS. ${reasons.join(' · ')}`,
      );
      return;
    }
    if (n) {
      message.success(`Đã duyệt ${n} KF — Start/End đã khóa.`);
      return;
    }
    const already = pack.filter((s) => {
      const run = shotRunOf(chained, s);
      return Boolean(run.keyframeDataUrl) && (run.kfApproved || run.status === 'approved');
    });
    if (already.length) {
      message.info(
        already.length === 1
          ? `${shCode(chained, already[0]!)} đã duyệt rồi. Sang Video nếu cần take / khớp môi.`
          : `${already.length} KF đã duyệt rồi. Sang Video nếu cần take / khớp môi.`,
      );
      return;
    }
    message.warning('Chưa có KF để duyệt.');
  };

  const recoverTake = async (clipId: string) => {
    const run = shortRunOf(stateRef.current, clipId);
    const url = (latestAttempt(run)?.outputUrl || run.previewUrl || '').trim();
    if (!url) {
      message.warning('Không có output URL để đọc lại. Mở Nhật ký — đừng Gửi lại.');
      return;
    }
    patchRun(clipId, { videoPipe: 'VIDEO_DOWNLOADING', turboError: undefined });
    try {
      const probed = await probeContentSeriesTake(url);
      if (!probed.ok) {
        const lastText = explainTurboError(probed.error || 'URL cũ không đọc được.');
        patchRun(clipId, { videoPipe: 'DOWNLOAD_FAILED', videoVerified: false, turboError: lastText });
        message.error(lastText);
        return;
      }
      patchRun(clipId, {
        status: 'turbo_testing',
        previewUrl: url,
        turboStatus: 'SUCCEEDED',
        turboError: undefined,
        videoPipe: 'VIDEO_READY',
        videoVerified: true,
        videoBytes: probed.bytes ?? undefined,
        videoMime: probed.mime ?? undefined,
        runwayAttempts: patchRunwayAttempt(run.runwayAttempts, run.turboTaskId, {
          outputUrl: url,
          downloadOk: true,
          videoBytes: probed.bytes ?? undefined,
          videoMime: probed.mime ?? undefined,
        }),
      });
      message.success('Đã gắn file từ URL cũ — 0 cr.');
    } catch (e) {
      const lastText = explainTurboError(apiErrorMessage(e, 'Không đọc được URL cũ.'));
      patchRun(clipId, { videoPipe: 'DOWNLOAD_FAILED', videoVerified: false, turboError: lastText });
      message.error(lastText);
    }
  };

  const startAbDiagnostic = (successId: string, failId: string) => {
    const st = stateRef.current;
    const pack = productionShorts(st);
    const success = pack.find((s) => s.id === successId);
    const fail = pack.find((s) => s.id === failId);
    if (!success || !fail) {
      message.warning('Chọn 1 shot READY và 1 shot INTERNAL.');
      return;
    }
    const cr = generateCost('turbo', success.seconds).credits + generateCost('turbo', fail.seconds).credits;
    const total = cr * 3;
    modal.confirm({
      title: 'A/B Runway · 6 job · JPEG 1280×720',
      content: `3× ${shCode(st, success)} (READY) + 3× ${shCode(st, fail)} (lỗi). Cùng normalize JPEG, model gen4_turbo, duration, prompt từng KF. Tuần tự, không batch, không xóa take READY. Ước tính −${total} cr.`,
      okText: `CONFIRM 6 job · −${total} cr`,
      cancelText: 'Không gửi',
      onOk: async () => {
        const jobs = [success, success, success, fail, fail, fail];
        for (let i = 0; i < jobs.length; i += 1) {
          const shot = jobs[i]!;
          const after = stateRef.current;
          const run = shotRunOf(after, shot);
          const loc = lockFromGraph(after, shot);
          const lock = { ...loc, locked: loc.locked || Boolean(loc.environment) };
          const pre = studioI2vPrecheck({
            lock,
            action: i2vActionOf(after, shot, run),
            keyframeDataUrl: run.keyframeDataUrl,
            status: run.status === 'story_locked' ? 'keyframe_ready' : run.status,
            unlocked: true,
            sceneLocked: after.sceneLocked,
            scriptLocked: after.scriptLocked,
            shortsReady: canWorkV2Scene(after),
            engine: 'turbo',
            hasEngineKey: keys.runway,
            state: after,
            shot,
            videoContext,
            run,
          });
          if (!pre.ok) {
            message.error(`Dừng A/B tại ${shCode(after, shot)}: ${pre.items.filter((x) => !x.ok).map((x) => x.label).join(' · ')}`);
            break;
          }
          message.loading({ content: `A/B ${i + 1}/6 · ${shCode(after, shot)}`, key: 'ab-runway', duration: 0 });
          await sendTurbo({
            clipId: shot.id,
            prompt: pre.prompt,
            seconds: shot.seconds,
            ratio: outputAspectOf(after),
            engine: 'turbo',
            silent: true,
            forceNew: true,
            diagnostic: true,
          });
          if (i < jobs.length - 1) await new Promise((r) => window.setTimeout(r, 8000));
        }
        const rows = productionShorts(stateRef.current).flatMap((s) => shotRunOf(stateRef.current, s).runwayDiagnostics ?? []).filter((r) => r.diagnostic);
        const sum = summarizeAbDiagnostic(rows.slice(-6));
        message.destroy('ab-runway');
        modal.info({
          title: sum.verdict === 'INTERMITTENT' ? 'Kết luận: Runway ngẫu nhiên' : sum.verdict === 'INPUT' ? 'Kết luận: lỗi KF/input' : 'A/B chưa đủ dữ liệu',
          content: sum.lines.join('\n'),
        });
      },
    });
  };

  const startSceneTurbo = (onlyIds?: string[], opts?: { remake?: boolean }) => {
    const quietMin = runwayQuietRemainMin(runwayQuietUntil);
    if (opts?.remake) {
      const pack = productionShorts(stateRef.current);
      const shots = (onlyIds?.length ? pack.filter((s) => onlyIds.includes(s.id)) : pack).filter((s) => {
        const run = shotRunOf(stateRef.current, s);
        if (!run.keyframeDataUrl || !kfIsApproved(run) || run.prodSkip || !shotHasValidAction(s, run)) return false;
        if (sameFailedInput(run, dataUriHash(run.keyframeDataUrl), shotI2vPromptHash(stateRef.current, s, run))) return false;
        return Boolean(run.previewUrl?.trim());
      });
      if (!shots.length) {
        message.warning('Không gửi lại shot FAIL cùng KF. Chỉ Tạo lại take đã READY — hoặc sửa KF rồi Confirm 1 job.');
        return;
      }
      if (quietMin) {
        message.warning('KIT còn khóa gửi job mới vì 429 cũ. Bấm «Đã lên tier — mở gửi» rồi Tạo lại từng hàng.');
        return;
      }
      const capped = capRunwayBatch(shots, stateRef.current.i2vProductionMode === true);
      const total = capped.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
      modal.confirm({
        title: `Tạo lại ${capped.length} take · ước ${total} cr`,
        content:
          'Chỉ take READY. Shot INTERNAL.BAD_OUTPUT không nằm trong list. Estimated — Status PENDING đến SUCCEEDED + file. Fail → BATCH PAUSED.',
        okText: `Confirm · ước ${total} cr`,
        cancelText: 'Hủy — 0 cr',
        onOk: async () => {
          for (const shot of capped) {
            const run = shotRunOf(stateRef.current, shot);
            const ok = await sendTurbo({
              clipId: shot.id,
              prompt: i2vActionOf(stateRef.current, shot, run) || run.shotAction || shot.story || '',
              seconds: shot.seconds,
              ratio: outputAspectOf(stateRef.current),
              engine,
              silent: true,
              forceNew: true,
            });
            if (!ok) {
              message.error(`BATCH PAUSED tại ${shCode(stateRef.current, shot)} — không gửi shot sau.`);
              break;
            }
          }
        },
      });
      return;
    }
    if (onlyIds?.length === 1) {
      const one = productionShorts(stateRef.current).find((s) => s.id === onlyIds[0]);
      const retry = one
        ? canManualRetry(shotRunOf(stateRef.current, one), shotI2vPromptHash(stateRef.current, one, shotRunOf(stateRef.current, one)))
        : { ok: false, kind: 'none' as const };
      if (retry.kind === 'recover' && one) {
        void recoverTake(one.id);
        return;
      }
    }
    if (quietMin) {
      const one = onlyIds?.length === 1 ? productionShorts(stateRef.current).find((s) => s.id === onlyIds[0]) : undefined;
      const resumeOne = Boolean(one && shouldResumeTurboPoll(shotRunOf(stateRef.current, one)));
      if (!resumeOne) {
        message.warning('KIT còn khóa gửi job mới vì 429 cũ. Bấm «Đã lên tier — mở gửi» trên bước Video. Hỏi lại · 0 cr không bị khóa.');
        return;
      }
    }
    if (!canWorkV2Scene(state)) {
      message.warning(v2SceneBlockReason(state) ?? 'Chưa mở dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trên Timeline trước khi tạo hết shot.');
      return;
    }
    persistState(applyContinuityChain(stateRef.current, productionShorts(stateRef.current)));
    const pack = productionShorts(stateRef.current);
    let { ready, blocked } = readyV2VideoShots(stateRef.current, pack);
    if (onlyIds?.length) {
      ready = ready.filter((s) => onlyIds.includes(s.id));
      blocked = blocked.filter((b) => onlyIds.includes(b.shot.id));
    }
    if (onlyIds?.length === 1 && turboLaunchRef.current.has(onlyIds[0]!)) {
      message.info('Clip này đang gửi hoặc chờ Confirm.');
      return;
    }
    if ((onlyIds?.length ?? 0) !== 1 && turboLaunchRef.current.size) {
      message.warning('Đang gửi hàng loạt. Gửi lại từng hàng lỗi — KIT xếp sau clip hiện tại.');
      return;
    }
    const unlockedMaster = [...new Set(ready.map(sceneIdOfShot))].filter(
      (sc) => !sceneMasterOf(stateRef.current, sc).locked,
    );
    if (unlockedMaster.length) {
      message.warning(`Khóa Scene Master (${unlockedMaster.join(', ')}) trước khi gửi I2V.`);
      return;
    }
    if (ready.length === 0) {
      message.warning(
        blocked.length
          ? `Chưa gửi được. ${blocked.map((b) => `${shCode(stateRef.current, b.shot)}: ${b.reason}`).join(' · ')}`
          : 'Không còn Short cần I2V.',
      );
      return;
    }
    const resumePack = ready.filter((s) => shouldResumeTurboPoll(shotRunOf(stateRef.current, s)));
    const explicitOne = onlyIds?.length === 1;
    const blockedInternal = ready.filter((s) =>
      sameFailedInput(
        shotRunOf(stateRef.current, s),
        dataUriHash(shotRunOf(stateRef.current, s).keyframeDataUrl),
        shotI2vPromptHash(stateRef.current, s, shotRunOf(stateRef.current, s)),
      ),
    );
    const newPack = ready.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      return (
        !shouldResumeTurboPoll(run) &&
        !sameFailedInput(run, dataUriHash(run.keyframeDataUrl), shotI2vPromptHash(stateRef.current, s, run))
      );
    });
    if (blockedInternal.length && !newPack.length && !resumePack.length) {
      message.warning(
        `INTERNAL.BAD_OUTPUT trên ${blockedInternal.map((s) => shCode(stateRef.current, s)).join(', ')} — circuit breaker. Không gửi lại cùng KF. Sửa KF → duyệt → Confirm 1 job.`,
      );
      return;
    }
    if (explicitOne && blockedInternal.length && !newPack.length && !resumePack.length) {
      return;
    }
    if (onlyIds?.length === 1 && resumePack.length === 1 && newPack.length === 0) {
      const shot = resumePack[0]!;
      const run = shotRunOf(stateRef.current, shot);
      void sendTurbo({
        clipId: shot.id,
        prompt: i2vActionOf(stateRef.current, shot, run) || run.shotAction || shot.story || '',
        seconds: shot.seconds,
        ratio: outputAspectOf(stateRef.current),
        engine,
        resume: true,
      });
      return;
    }
    const production = stateRef.current.i2vProductionMode === true;
    const sendNew = explicitOne ? newPack : capRunwayBatch(newPack, production);
    const queue = explicitOne ? ready : [...resumePack, ...sendNew.filter((s) => !resumePack.some((r) => r.id === s.id))];
    const total = sendNew.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
    const wan = engine === 'wan';
    const readyIds = queue.map((s) => s.id);
    for (const id of readyIds) turboLaunchRef.current.add(id);
    modal.confirm({
      title: wan
        ? `Tạo video ${queue.length} Short · Wan`
        : resumePack.length && !sendNew.length
          ? `Hỏi lại ${resumePack.length} task cũ · 0 cr`
          : production
            ? `BATCH ${sendNew.length}/3 · ước ${total} cr`
            : `SAFE MODE · 1 shot · ước ${total} cr`,
      content: blocked.length
        ? `Chỉ gửi Short đã duyệt. Bỏ qua: ${blocked.map((b) => shCode(stateRef.current, b.shot)).join(', ')}. Estimated — PENDING đến file hợp lệ. 1 FAIL = BATCH PAUSED.`
        : resumePack.length && !sendNew.length
          ? 'Task cũ còn trên Runway. KIT hỏi lại file — không POST job mới, không trừ cr.'
          : `SAFE mặc định 1 shot. Production tối đa 3. Fail → dừng, không gửi shot sau. Estimated ${total} cr · chưa phải đã trừ.`,
      okText: 'CONFIRM & GENERATE',
      cancelText: 'Hủy',
      onCancel: () => {
        for (const id of readyIds) turboLaunchRef.current.delete(id);
      },
      onOk: async () => {
        let sent = 0;
        const failed: string[] = [];
        let lastFailErr = '';
        try {
          for (const shot of queue) {
            try {
              if (
                sameFailedInput(
                  shotRunOf(stateRef.current, shot),
                  dataUriHash(shotRunOf(stateRef.current, shot).keyframeDataUrl),
                  shotI2vPromptHash(stateRef.current, shot, shotRunOf(stateRef.current, shot)),
                ) &&
                !shouldResumeTurboPoll(shotRunOf(stateRef.current, shot))
              ) {
                message.warning(`${shCode(stateRef.current, shot)} circuit — bỏ qua.`);
                continue;
              }
              if (shotRunOf(stateRef.current, shot).previewUrl?.trim()) continue;
              setActiveId(shot.id);
              const after = stateRef.current;
              const run = shotRunOf(after, shot);
              if (run.keyframeDataUrl && run.status === 'story_locked') {
                patchRun(shot.id, { status: 'keyframe_ready' });
              }
              const loc = lockFromGraph(stateRef.current, shot);
              const lock = { ...loc, locked: loc.locked || Boolean(loc.environment) };
              const pre = studioI2vPrecheck({
                lock,
                action: i2vActionOf(after, shot, run),
                keyframeDataUrl: run.keyframeDataUrl,
                status: run.status === 'story_locked' ? 'keyframe_ready' : run.status,
                unlocked: true,
                sceneLocked: after.sceneLocked,
                scriptLocked: after.scriptLocked,
                shortsReady: canWorkV2Scene(after),
                engine,
                hasEngineKey: engine === 'wan' ? keys.fal : keys.runway,
                state: after,
                shot,
                videoContext,
                run,
              });
              if (!pre.ok) {
                failed.push(shCode(after, shot));
                message.error(
                  `Bỏ ${shCode(after, shot)} (0 cr): ${pre.items
                    .filter((i) => !i.ok)
                    .map((i) => i.label)
                    .join(' · ')}`,
                );
                continue;
              }
              const resumeShot = shouldResumeTurboPoll(run);
              const ok = await sendTurbo({
                clipId: shot.id,
                prompt: pre.prompt,
                seconds: shot.seconds,
                ratio: outputAspectOf(stateRef.current),
                engine,
                silent: true,
                resume: resumeShot,
              });
              if (!ok) {
                failed.push(shCode(stateRef.current, shot));
                const err = shotRunOf(stateRef.current, shot).turboError || '';
                lastFailErr = err;
                message.error(
                  isTurboDailyQuota(err)
                    ? 'Hết hạn mức ngày Runway — BATCH PAUSED. Hỏi lại · 0 cr.'
                    : isKitPrecheckError(err)
                      ? `BATCH PAUSED tại ${shCode(stateRef.current, shot)} — KIT PRECHECK · 0 cr. Không phải 429. TEST INPUT rồi Confirm 1 shot.`
                      : `BATCH PAUSED tại ${shCode(stateRef.current, shot)} — không gửi shot sau. Sửa KF, đừng bấm lại cùng input.`,
                );
                break;
              }
              sent += 1;
              await new Promise((r) => window.setTimeout(r, 8000));
            } finally {
              turboLaunchRef.current.delete(shot.id);
            }
          }
          if (sent && failed.length) {
            message.warning(
              `Được ${sent} take. Còn ${failed.join(', ')} — Hỏi lại · 0 cr từng hàng. Đừng gửi job mới khi còn 429.`,
            );
          } else if (sent) {
            message.success(`Đã gửi ${sent} take. QC từng shot — không tự Final.`);
          } else if (failed.length) {
            message.warning(
              isKitPrecheckError(lastFailErr)
                ? `Chưa gửi (${failed.join(', ')}). KIT PRECHECK · 0 cr — không phải 429. F5 rồi TEST INPUT.`
                : isTurboRateLimit(lastFailErr) || isTurboDailyQuota(lastFailErr)
                  ? `Chưa lấy được take (${failed.join(', ')}). Đang 429 / hạn mức — đợi rồi Hỏi lại · 0 cr từng hàng. Không CONFIRM job mới.`
                  : `Chưa lấy được take (${failed.join(', ')}). Xem lỗi từng hàng. Không CONFIRM cùng input.`,
            );
          } else {
            message.warning('Không gửi clip nào (0 cr). Sửa prompt/KF rồi CONFIRM lại.');
          }
        } finally {
          for (const id of readyIds) turboLaunchRef.current.delete(id);
        }
      },
    });
  };

  const rememberLineAudio = (lineId: string, blob: Blob) => {
    ttsBlobs.current.set(lineId, blob);
    let url = ttsUrls.current.get(lineId);
    if (!url) {
      url = URL.createObjectURL(blob);
      ttsUrls.current.set(lineId, url);
    }
    setTtsFiles((m) => (m[lineId] ? m : { ...m, [lineId]: { url, fileName: `${lineId}.mp3` } }));
    return url;
  };

  const resolveLineAudio = async (line: { id: string; text: string; voiceId?: string; characterId?: string }) => {
    const hit = ttsBlobs.current.get(line.id);
    if (hit) {
      rememberLineAudio(line.id, hit);
      return hit;
    }
    const same = (a: string, b: string) => a.replace(/\s+/g, ' ').trim() === b.replace(/\s+/g, ' ').trim();
    const voices = voiceIdsForLine(line);
    const alts = ttsLookupKeys(line, voices);
    for (const g of stateRef.current.voicePreview?.generated ?? []) {
      if (g.id !== line.id && same(g.text, line.text)) {
        alts.push(...ttsLookupKeys({ id: g.id, text: g.text, voiceId: line.voiceId }, voices));
      }
    }
    const blob = (await loadTtsBlobAny(alts)) || (await findTtsBlobForLine(line.id));
    if (!blob) return undefined;
    rememberLineAudio(line.id, blob);
    rememberLineAudio(ttsTextKey(line.text, line.voiceId), blob);
    return blob;
  };

  const cutPlan = mapPreviewCut(state, cutRange, {
    hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
  });

  const spokenLinesOf = (shot: FamixaSeriesShot) => {
    const item = cutPlan?.items.find((i) => i.shotId === shot.id);
    if (item?.silent) return [];
    return linesForShot(stateRef.current, shot);
  };

  const startLipsync = async (onlyIds?: string[], opts?: { confirmed?: boolean; remake?: boolean }) => {
    const pack = productionShorts(stateRef.current);
    const range = onlyIds?.length ? pack.filter((s) => onlyIds.includes(s.id)) : cutRange;
    const falModel = normalizeLipsyncModel(stateRef.current.lipsyncModel);
    const falSync = normalizeLipsyncSyncMode(stateRef.current.lipsyncSyncMode);
    const need = range.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      if (!run.previewUrl?.trim() || run.prodSkip || spokenLinesOf(s).length === 0) return false;
      if (run.lipsynced && !opts?.remake) return false;
      return true;
    });
    if (!need.length) {
      message.warning('Chưa có take + thoại cần khớp môi (hoặc đã khớp). Không gửi Fal.');
      return;
    }
    const qaBlock = need.filter((s) => !lipsyncQaReady(shotRunOf(stateRef.current, s)));
    if (qaBlock.length) {
      message.error(
        `${qaBlock.map((s) => studioShotCode(s, pack)).join(', ')} chưa tick ACTION + CONTINUITY + VOICE/FACE. Sai mặt/giọng thì sửa KF — không gửi Fal.`,
      );
      return;
    }
    const speakerBlock = need.filter((s) => multiSpeakerBlock(spokenLinesOf(s)));
    if (speakerBlock.length) {
      message.error(
        speakerBlock.map((s) => `${studioShotCode(s, pack)}: ${multiSpeakerBlock(spokenLinesOf(s))}`).join(' '),
      );
      return;
    }
    const faceBlock = need
      .map((s) => {
        const line = spokenLinesOf(s)[0];
        if (!line) return '';
        const why = lipsyncSpeakerMismatch(s, line, shotRunOf(stateRef.current, s), stateRef.current.characters);
        return why ? `${studioShotCode(s, pack)}: ${why}` : '';
      })
      .filter(Boolean);
    if (faceBlock.length) {
      message.error(`${faceBlock.join(' · ')} — không gửi Fal. Sửa map thoại hoặc KF.`);
      return;
    }
    if (!keys.fal) {
      message.error('Chưa có Fal API key (Model AI → Video). Khớp môi không trừ Runway.');
      return;
    }
    await hydrateSessionTts(stateRef.current);
    const missingVoice: string[] = [];
    for (const s of need) {
      const line = spokenLinesOf(s)[0];
      if (!line) continue;
      if (!(await resolveLineAudio(line))) {
        missingVoice.push(`${studioShotCode(s, pack)} «${(line.text || line.id).slice(0, 42)}»`);
      }
    }
    if (missingVoice.length) {
      message.error(
        `Thiếu Voice master: ${missingVoice.join(' · ')}. Mở tab Voice → phát lại đúng câu (KIT nhớ file trên máy). Không tạo TTS lúc Fal.`,
      );
      return;
    }
    if (!opts?.confirmed) {
      const falUsd = estimateFalLipsyncUsdForShots(need, falModel);
      const alreadyTried = need.some((s) => Boolean(shotRunOf(stateRef.current, s).lipsyncError));
      const tier = lipsyncTierOf(falModel);
      modal.confirm({
        title: `Khớp môi ${need.length} Short · ${tier.title} · ~$${falUsd.toFixed(2)} · 0 cr Runway`,
        content: alreadyTried
          ? `${need.map((s) => studioShotCode(s, pack)).join(', ')}. Lần trước Fal có thể đã trừ. Confirm = job MỚI, trừ thêm ~$${falUsd.toFixed(2)}. Xem fal.ai → Usage trước — file có thể đã xong.`
          : `${need.map((s) => studioShotCode(s, pack)).join(', ')}. ${tier.title} — ${tier.hint} · ${tier.rate}. Sync ${falSync}. Nhận job là trừ. KIT chỉ hiện KHỚP MÔI khi có file. Take cũ giữ lịch sử.`,
        okText: `Confirm · trừ ~$${falUsd.toFixed(2)} Fal`,
        cancelText: 'Hủy — 0 Fal',
        onOk: () => void startLipsync(need.map((s) => s.id), { confirmed: true, remake: opts?.remake }),
      });
      return;
    }
    for (const s of need) {
      patchRun(s.id, { lipsyncError: undefined, lipsyncStatus: undefined });
    }
    let ok = 0;
    for (let i = 0; i < need.length; i++) {
      const shot = need[i]!;
      const run = shotRunOf(stateRef.current, shot);
      const videoUrl = resolveTakeUrl(run) || run.previewUrl?.trim();
      if (!videoUrl) continue;
      const lines = spokenLinesOf(shot);
      const line = lines[0];
      if (!line) continue;
      if (multiSpeakerBlock(lines)) {
        message.error(`${studioShotCode(shot, pack)}: ${multiSpeakerBlock(lines)}`);
        continue;
      }
      const blob = await resolveLineAudio(line);
      if (!blob) {
        message.warning(
          `${studioShotCode(shot, pack)}: thiếu Voice master «${(line.text || line.id).slice(0, 42)}». Mở Voice → phát câu. Không tạo TTS lúc Fal.`,
        );
        continue;
      }
      if (lines.length > 1) {
        message.info(`${studioShotCode(shot, pack)}: cùng người — khớp câu đầu. Câu sau nên tách shot hoặc voiceChainFrom.`);
      }
      const prev = turboLockRef.current;
      let release = () => {};
      turboLockRef.current = new Promise<void>((resolve) => {
        release = resolve;
      });
      await prev;
      setLipsyncBusy(shot.id);
      try {
        const pollLipsync = async (taskId: string) => {
          const t0 = Date.now();
          while (Date.now() - t0 < 720_000) {
            const task = await getContentSeriesTurbo(taskId);
            patchRun(shot.id, { lipsyncStatus: task.status, lipsyncTaskId: task.taskId, lipsyncError: task.error ?? undefined });
            if (task.status === 'SUCCEEDED' && task.videoUrl) {
              const live = shotRunOf(stateRef.current, shot);
              patchRun(shot.id, {
                ...stampFalFinal(live, task.videoUrl),
                lipsyncStatus: 'SUCCEEDED',
                lipsyncError: undefined,
                model: task.model || 'sync-lipsync-1.9',
                shotQa: { ...live.shotQa, lipsync: true },
              });
              return true;
            }
            if (task.status === 'FAILED' || task.status === 'CANCELLED') {
              patchRun(shot.id, {
                lipsyncStatus: task.status,
                lipsyncError: task.error || 'Fal lipsync lỗi. Take cũ còn trên hàng.',
              });
              message.error(`${studioShotCode(shot, pack)}: ${task.error || 'Fal khớp môi lỗi.'}`);
              return false;
            }
            await new Promise((r) => setTimeout(r, 6000));
          }
          patchRun(shot.id, {
            lipsyncStatus: 'RETRY',
            lipsyncError: 'Fal chưa trả take khớp môi. Hỏi lại sau — 0 cr Runway.',
          });
          message.warning(`${studioShotCode(shot, pack)}: Fal chưa trả file. Hỏi lại môi nếu còn task — đừng Confirm job mới.`);
          return false;
        };

        if (shouldResumeLipsync(run) && !opts?.remake) {
          const recovered = parseFalJobIdFromError(run.lipsyncError);
          const resumeId =
            run.lipsyncTaskId?.trim() ||
            (recovered ? `${lipsyncTaskPrefix(falModel)}${recovered}` : '');
          if (!resumeId) {
            message.error(`${studioShotCode(shot, pack)}: hết task id. Dán URL từ fal.ai → Usage (Gắn Fal · 0$).`);
            continue;
          }
          if (!run.lipsyncTaskId?.trim()) patchRun(shot.id, { lipsyncTaskId: resumeId, lipsyncStatus: 'RETRY' });
          message.loading({
            content: `Hỏi lại khớp môi ${studioShotCode(shot, pack)} (${i + 1}/${need.length}) · 0 Fal mới`,
            key: 'lipsync',
            duration: 0,
          });
          if (await pollLipsync(resumeId)) ok += 1;
          continue;
        }

        const takeHistory = [...(run.takeHistory ?? [])];
        const muteTake = resolveTakeUrl(run) || run.previewUrl?.trim();
        if (muteTake) {
          takeHistory.unshift({ url: muteTake, taskId: run.turboTaskId });
        }
        message.loading({
          content: `Khớp môi ${studioShotCode(shot, pack)} (${i + 1}/${need.length}) · Fal`,
          key: 'lipsync',
          duration: 0,
        });
        const started = await startContentSeriesLipsync({
          clipId: shot.id,
          videoUrl,
          audioBase64: await blobToBase64(blob),
          mime: blob.type || 'audio/mpeg',
          syncMode: falSync,
          model: falModel,
        });
        patchRun(shot.id, {
          lipsyncTaskId: started.taskId,
          lipsyncStatus: started.status,
          lipsyncError: undefined,
          lipsynced: false,
          takeHistory: takeHistory.slice(0, 6),
        });
        if (started.status === 'SUCCEEDED' && started.videoUrl) {
          const live = shotRunOf(stateRef.current, shot);
          patchRun(shot.id, {
            ...stampFalFinal(live, started.videoUrl),
            lipsyncStatus: 'SUCCEEDED',
            lipsyncError: undefined,
            model: started.model || 'sync-lipsync-1.9',
            shotQa: { ...live.shotQa, lipsync: true },
          });
          ok += 1;
        } else if (await pollLipsync(started.taskId)) {
          ok += 1;
        }
      } catch (e) {
        const raw = apiErrorMessage(e, 'Không gửi được Fal khớp môi.');
        const status = typeof e === 'object' && e && 'response' in e
          ? Number((e as { response?: { status?: number } }).response?.status)
          : 0;
        const why =
          status === 404
            ? 'API :5290 chưa có khớp môi (process cũ). Restart API rồi F5. Đừng Confirm job mới nếu Fal Usage đã có file.'
            : raw;
        const recovered = parseFalJobIdFromError(why);
        patchRun(shot.id, {
          lipsyncStatus: recovered ? 'RETRY' : 'FAILED',
          lipsyncError: why,
          ...(recovered ? { lipsyncTaskId: `${lipsyncTaskPrefix(falModel)}${recovered}` } : {}),
        });
        message.error(`${studioShotCode(shot, pack)}: ${why}`);
      } finally {
        setLipsyncBusy(undefined);
        release();
      }
    }
    if (ok) {
      message.success({ content: `Đã khớp môi ${ok}/${need.length} Short. Ghép sẽ giữ tiếng trong video.`, key: 'lipsync' });
    } else {
      message.destroy('lipsync');
    }
  };

  const attachLipsync = (shotId: string) => {
    let raw = '';
    const shot = productionShorts(stateRef.current).find((s) => s.id === shotId);
    const code = shot ? studioShotCode(shot, productionShorts(stateRef.current)) : shotId;
    modal.confirm({
      title: `Gắn file Fal ${code} · 0$`,
      content: (
        <div>
          <p style={{ marginTop: 0 }}>
            Usage đã trừ loạt v3 cho Short này. Dán URL mp4 (Assets) hoặc Request ID — KIT chỉ lấy file, không gửi job mới.
          </p>
          <Input.TextArea
            rows={3}
            placeholder="https://v3b.fal.media/…/output.mp4 hoặc request id"
            onChange={(e) => {
              raw = e.target.value;
            }}
          />
        </div>
      ),
      okText: 'Gắn · 0$',
      cancelText: 'Hủy',
      async onOk() {
        if (!shot) {
          message.error('Không tìm thấy Short.');
          return Promise.reject();
        }
        const ref = parseFalLipsyncRef(raw);
        if (ref.url) {
          const live = shotRunOf(stateRef.current, shot);
          patchRun(shotId, {
            ...stampFalFinal(live, ref.url),
            lipsyncStatus: 'SUCCEEDED',
            lipsyncError: undefined,
            model: 'sync-lipsync-v3',
            shotQa: { ...live.shotQa, lipsync: true },
          });
          message.success(`${code}: đã gắn file Fal · 0$.`);
          return;
        }
        if (!ref.taskId) {
          message.error('Dán URL mp4 hoặc Request ID trên Fal Usage.');
          return Promise.reject();
        }
        const task = await getContentSeriesTurbo(ref.taskId);
        if (!task.videoUrl) {
          message.error(task.error || 'Fal chưa trả file cho id này. Thử dòng Quantity lớn hơn (0.17).');
          return Promise.reject();
        }
        const live = shotRunOf(stateRef.current, shot);
        patchRun(shotId, {
          ...stampFalFinal(live, task.videoUrl!),
          lipsyncTaskId: task.taskId || ref.taskId,
          lipsyncStatus: 'SUCCEEDED',
          lipsyncError: undefined,
          model: task.model || 'sync-lipsync-v3',
          shotQa: { ...live.shotQa, lipsync: true },
        });
        message.success(`${code}: lấy lại file Fal · 0$.`);
      },
    });
  };

  const hydrateCutVoice = async () => {
    const lines = cutPlan.items.flatMap((i) => (i.lines.length ? i.lines : i.line ? [i.line] : []));
    let n = 0;
    for (const line of lines) {
      if (await resolveLineAudio(line)) n += 1;
    }
    return n;
  };
  const fillPreviewCut = (kind: 'story' | 'motion') => {
    const needVoice = cutPlan.items.filter((i) => !i.silent && !i.hasVoiceFile);
    if (kind === 'story') {
      if (cutPlan.storyMissingKf.length) {
        generateSceneKf(cutPlan.storyMissingKf);
        return;
      }
      if (needVoice.length) {
        message.info(
          `Đã đủ hình. Thiếu ${needVoice.length} file TTS session (F5 mất MP3) — GHÉP PREVIEW vẫn chạy hình. Phát lại ở bước Thoại, không gọi ElevenLabs nếu đã khóa.`,
        );
        return;
      }
      message.success('Đoạn đã chọn đã đủ hình.');
      return;
    }
    if (cutPlan.durationBlocked) {
      message.error('Thoại dài hơn shot — không gửi I2V. Sửa thoại hoặc tăng seconds.');
      return;
    }
    if (cutPlan.storyMissingKf.length) {
      message.warning('Còn thiếu KF. Tạo và duyệt KF đoạn trước, rồi mới video.');
      generateSceneKf(cutPlan.storyMissingKf);
      return;
    }
    startSceneTurbo(cutRange.map((s) => s.id));
  };

  const downloadTakes = async (ids?: string[], kind?: 'take' | 'lipsync') => {
    const pack = productionShorts(stateRef.current);
    const range = ids?.length ? pack.filter((s) => ids.includes(s.id)) : cutRange;
    const wantLip = kind === 'lipsync';
    const ready = range.filter((s) => {
      const run = shotRunOf(stateRef.current, s);
      return wantLip ? Boolean(lipsyncVideoUrl(run)) : Boolean(run.previewUrl?.trim());
    });
    if (!ready.length) {
      message.warning(
        wantLip
          ? 'Chưa có file khớp môi trên dải. Confirm Khớp môi trước — rồi mới tải.'
          : 'Chưa có take trên dải đã chọn. Gửi Runway ở bước 5 Video trước.',
      );
      return;
    }
    setAssembleBusy(true);
    try {
        let saved = 0;
        for (let i = 0; i < ready.length; i++) {
          const s = ready[i]!;
          const run = shotRunOf(stateRef.current, s);
          const url = (wantLip ? lipsyncVideoUrl(run) : run.previewUrl?.trim()) || '';
          const name = wantLip ? lipsyncDownloadName(s, pack) : takeDownloadName(s, pack);
          message.loading({
            content: `Tải ${wantLip ? 'khớp môi ' : ''}${studioShotCode(s, pack)} (${i + 1}/${ready.length})`,
            key: 'take-dl',
            duration: 0,
          });
          try {
            const blob = await takeBlobFromUrl(url);
            triggerDownload(blob, name);
            saved += 1;
          } catch (e) {
            window.open(url, '_blank', 'noopener,noreferrer');
            message.warning(
              `${studioShotCode(s, pack)}: ${e instanceof Error ? e.message : 'Không tải qua API'} — đã mở link, lưu MP4 từ tab đó.`,
            );
          }
          await new Promise((r) => window.setTimeout(r, 400));
        }
        message.success({
          content: saved === ready.length
            ? `Đã tải ${saved} ${wantLip ? 'khớp môi' : 'take'}.`
            : `Tải ${saved}/${ready.length}. Phần còn lại mở tab.`,
          key: 'take-dl',
        });
    } catch (e) {
      message.error({
        content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không tải được take.'),
        key: 'take-dl',
      });
    } finally {
      setAssembleBusy(false);
    }
  };

  const assembleCut = async (opts?: { resynth?: boolean; confirmed?: boolean }) => {
    persistState(applyContinuityChain(stateRef.current, productionShorts(stateRef.current)));
    if ((stateRef.current.episode?.shots ?? []).some((s) => !Array.isArray(s.dialogueSegmentIds))) {
      persistState(applyDialogueMap(stateRef.current));
    }
    await hydrateCutVoice();
    const hole = completeCutBlocked(cutPlan);
    if (hole.length) {
      message.error(`Chưa ghép đủ tập — thiếu KF: ${hole.join(', ')}. Tạo / duyệt KF rồi ghép. Không bỏ shot.`);
      return;
    }
    const readyPlan = planCompleteCut(cutPlan);
    if (!readyPlan.items.length) {
      message.warning('Chưa có KF trên dải. Tạo hình rồi ghép — không cắt nhịp kịch bản.');
      return;
    }
    let mixSheet = compileMixCueSheet(
      buildAssembleTimeline(readyPlan, { hasVoiceFile: () => true, fit: 'speech' }),
      productionShorts(stateRef.current),
      stateRef.current,
    );
    const confirm = assembleConfirmCopy(readyPlan, mixSheet);
    const holdN = completeCutHolds(readyPlan).length;
    if (!opts?.confirmed) {
      modal.confirm({
        title: `Ghép tập hoàn chỉnh · ${readyPlan.items.length} shot + thoại · 0 cr Runway`,
        content: `${confirm.detail} Câu chưa gắn Short không đưa vào file. Không gửi job mới.`,
        okText: confirm.okText,
        cancelText: 'Hủy',
        onOk: () => void assembleCut({ ...opts, confirmed: true }),
      });
      return;
    }
    const script = deriveVoiceScript(stateRef.current);
    const cov = coverageOf(stateRef.current, cutRange, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
    });
    if (cov.extraUnmapped.length) {
      message.warning(
        `${cov.extraUnmapped.length} câu chưa gắn Short — bỏ khỏi file ghép. Không đoán map, không trừ Runway.`,
      );
    }
    const spoken = assembleNeedTtsOverlay(readyPlan).flatMap((i) => (i.lines.length ? i.lines : i.line ? [i.line] : []));
    const missing = [];
    for (const line of spoken) {
      if (!(await resolveLineAudio(line))) missing.push(line);
    }
    if (missing.length && !opts?.resynth) {
      modal.confirm({
        title: 'Thiếu file thoại trên máy',
        content: `File vừa tải câm vì không gửi được MP3 (F5 mất file). ${missing.length} câu trên dải này. Tạo lại rồi mix — ElevenLabs tính ký tự, không trừ Runway, không tạo ảnh mới.`,
        okText: `Tạo lại ${missing.length} câu rồi ghép MP4`,
        cancelText: 'Hủy — không tải file câm',
        onOk: () => void assembleCut({ ...opts, resynth: true, confirmed: true }),
      });
      return;
    }
    if (missing.length && opts?.resynth) {
      if (!keys.elevenLabs) {
        message.error('Chưa có key ElevenLabs — không tạo được thoại, không ghép file câm.');
        return;
      }
      setAssembleBusy(true);
      try {
        for (let i = 0; i < missing.length; i++) {
          const line = missing[i]!;
          const full =
            script.lines.find((l) => l.id === line.id) ||
            script.lines.find((l) => l.text.replace(/\s+/g, ' ').trim() === line.text.replace(/\s+/g, ' ').trim());
          if (!full?.voiceId) throw new Error(`Chưa gán Voice Canon cho “${line.text.slice(0, 40)}”.`);
          message.loading({ content: `TTS ${i + 1}/${missing.length} · ${full.name}`, key: 'assemble', duration: 0 });
          await loadCueAudio(full);
        }
      } catch (e) {
        message.error({
          content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không tạo lại được thoại.'),
          key: 'assemble',
        });
        setAssembleBusy(false);
        return;
      }
    }
    const stillMissing = [];
    for (const line of spoken) {
      if (!(await resolveLineAudio(line))) stillMissing.push(line);
    }
    if (stillMissing.length) {
      message.error(`Chưa ghép — vẫn thiếu TTS (${stillMissing.length} câu). Không tải file câm.`);
      setAssembleBusy(false);
      return;
    }
    const assets = stateRef.current.voiceAssets ?? {};
    const measured: Record<string, number> = {};
    for (const line of spoken) {
      const full = script.lines.find((l) => l.id === line.id);
      const blob = await resolveLineAudio({
        id: line.id,
        text: line.text,
        voiceId: full?.voiceId ?? line.voiceId,
        characterId: full?.characterId ?? line.characterId,
      });
      if (!blob) continue;
      const sec = (await measureAudioSec(blob)) || assets[line.id]?.duration || 0;
      if (sec > 0.2) measured[line.id] = Number(sec.toFixed(2));
    }
    const tl = buildAssembleTimeline(readyPlan, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id) || measured[id]),
      voiceSecOf: (id) => measured[id] || assets[id]?.duration,
      fit: 'speech',
    });
    mixSheet = compileMixCueSheet(tl, productionShorts(stateRef.current), stateRef.current);
    const stem = assembleFileStem(readyPlan, ep?.episode, ep?.title);
    triggerDownload(new Blob([formatSrt(tl.cues)], { type: 'text/plain;charset=utf-8' }), `${stem}.srt`);
    setAssembleBusy(true);
    try {
      message.loading({
        content: mixSheet.music
          ? 'FFmpeg: phòng + Foley + nhạc (duck) + thoại → −14 LUFS…'
          : 'FFmpeg: phòng + Foley + thoại → −14 LUFS…',
        key: 'assemble',
        duration: 0,
      });
      const pack = productionShorts(stateRef.current);
      const clips = [];
      for (const clip of tl.clips) {
        const shot = pack.find((s) => s.id === clip.shotId);
        const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
        const videoUrl = assembleVideoUrl(run) || takeVideoUrl(run) || '';
        let stillBase64: string | undefined;
        if (!videoUrl) {
          const kf =
            run?.keyframeDataUrl?.startsWith('data:image')
              ? run.keyframeDataUrl
              : (await loadKfPixels(clip.shotId))?.dataUrl;
          if (!kf?.startsWith('data:image')) {
            throw new Error(`${clip.code}: thiếu KF để giữ khung. Không bỏ shot — tạo KF rồi ghép.`);
          }
          stillBase64 = kf.includes(',') ? kf.slice(kf.indexOf(',') + 1) : kf;
        }
        const keepLip = Boolean(videoUrl && run?.lipsyncUrl?.trim());
        const voices = [];
        for (const cue of clip.cues) {
          const full = script.lines.find((l) => l.id === cue.lineId);
          const blob = await resolveLineAudio({
            id: cue.lineId,
            text: cue.text,
            voiceId: full?.voiceId,
            characterId: full?.characterId,
          });
          if (!blob) {
            if (!keepLip) throw new Error(`Thiếu thoại ${cue.code}: “${cue.text.slice(0, 40)}”. Không ghép file câm.`);
            continue;
          }
          voices.push({
            lineId: cue.lineId,
            startSec: Math.max(0, cue.startSec - clip.startSec),
            audioBase64: await blobToBase64(blob),
            mime: blob.type || 'audio/mpeg',
          });
        }
        const requireVoice = clip.cues.length > 0;
        if (requireVoice && !keepLip && !voices.length) {
          throw new Error(`${clip.code}: thiếu thoại — không ghép file câm.`);
        }
        clips.push({
          code: clip.code,
          videoUrl: videoUrl || undefined,
          seconds: clip.seconds,
          voices,
          useVideoAudio: keepLip,
          requireVoice,
          stillBase64,
        });
      }
      const voiceCount = clips.reduce((n, c) => n + c.voices.length, 0);
      const lipN = clips.filter((c) => c.useVideoAudio).length;
      if (clips.some((c) => c.requireVoice && !c.useVideoAudio && !c.voices.length)) {
        throw new Error('Không gửi được file thoại lên API. Không tải file câm.');
      }
      try {
        const mp4 = await assembleContentSeriesCut({
          fileStem: stem,
          aspect: assembleAspect,
          clips,
          mix: assembleMixPayload(mixSheet),
        });
        triggerDownload(mp4, `${stem}.mp4`);
        persistState({ ...stateRef.current, previewApproved: true });
        message.success({
          content: `Đã ghép tập hoàn chỉnh ${tl.clips.length} shot${
            holdN ? ` · ${holdN} HOLD KF` : ''
          }${lipN ? ` · ${lipN} khớp môi` : ''}${voiceCount ? ` · ${voiceCount} câu thoại` : ''}${
            mixSheet.room ? ' · phòng' : ''
          }${mixSheet.sfx.length ? ` · Foley ${mixSheet.sfx.length}` : ''}${
            mixSheet.music ? ' · nhạc duck' : ''
          }${mixSheet.loudnorm ? ' · −14 LUFS' : ''} → ${stem}.mp4`,
          key: 'assemble',
        });
        return;
      } catch (e) {
        const why = e instanceof Error ? e.message : apiErrorMessage(e, 'FFmpeg lỗi.');
        message.warning({ content: `${why} — ghép tạm trên trình duyệt (WebM).`, key: 'assemble', duration: 2 });
      }
      const blob = await recordAssembledCut({
        clips: tl.clips,
        videoOf: async (shotId) => {
          const shot = pack.find((s) => s.id === shotId);
          const run = shot ? shotRunOf(stateRef.current, shot) : undefined;
          const url = run ? assembleVideoUrl(run) || takeVideoUrl(run) : '';
          if (url) return takeBlobFromUrl(url);
          const kf =
            run?.keyframeDataUrl?.startsWith('data:image')
              ? run.keyframeDataUrl
              : (await loadKfPixels(shotId))?.dataUrl;
          if (!kf?.startsWith('data:image')) throw new Error('Thiếu KF để giữ khung.');
          const res = await fetch(kf);
          return res.blob();
        },
        audioOf: async (lineId) => {
          const line = script.lines.find((l) => l.id === lineId);
          return line ? resolveLineAudio(line) : ttsBlobs.current.get(lineId) || loadTtsBlob(lineId);
        },
        onProgress: (msg) => message.loading({ content: `Ghép ${msg}`, key: 'assemble', duration: 0 }),
      });
      triggerDownload(blob, `${stem}.webm`);
      persistState({ ...stateRef.current, previewApproved: true });
      message.success({ content: `Đã ghép hoàn thiện ${tl.clips.length} take (WebM) + SRT.`, key: 'assemble' });
    } catch (e) {
      message.error({
        content: e instanceof Error ? e.message : apiErrorMessage(e, 'Không ghép được file.'),
        key: 'assemble',
      });
    } finally {
      setAssembleBusy(false);
    }
  };

  const lockMem = lockFromGraph(state, active);
  const prevLocked = previousLockedShot(state, active) ?? previousKeyframeShot(state, active);
  const prevRun = prevLocked ? shotRunOf(state, prevLocked) : undefined;

  return (
    <div>
      <ContentFamixaStudioView
        shots={shots}
        episode={ep}
        active={active}
        run={run}
        lock={lockMem}
        pilot={state}
        videoContext={videoContext}
        prevLocked={prevLocked}
        prevRun={prevRun}
        unlocked={unlocked}
        sceneLocked={state.sceneLocked}
        scriptLocked={state.scriptLocked}
        shortsReady={canWorkScene(state)}
        shortsCount={shorts.length}
        shortsLockedCount={approvedShortCount(state)}
        sessionSrc={active ? sessionClips[active.id] : undefined}
        turboBusy={turboBusy}
        generateKfBusy={stillBusy === active?.id}
        kitCredits={credits}
        runwaySpent={spentOnRunway}
        expectedCost={generateCost(engine, active?.seconds ?? 5).credits}
        costLabel={generateCost(engine, active?.seconds ?? 5).label}
        engine={engine}
        onEngine={persistEngine}
        keys={keys}
        voiceProvider={voiceProvider}
        onVoiceProvider={persistVoice}
        statusOf={(s) => shotRunOf(state, s).status}
        runOf={(s) => shotRunOf(state, s)}
        actionOf={(s) => effectiveShotAction(s, shotRunOf(state, s))}
        onSelectShot={selectShot}
        onLockShotGraph={lockShotGraph}
        onRemoveShots={removeShots}
        onAction={(shotAction) => {
          if (active) patchRun(active.id, { shotAction });
        }}
        onPickKeyframe={(file) => {
          if (active) void onKeyframeFile(active.id, file);
        }}
        onGenerateKeyframe={() => {
          if (!active) return;
          void generateKfFromCanon(stillArgsFor(active, productionShorts(stateRef.current)));
        }}
        onInheritKeyframe={() => {
          if (!active) return;
          const src = previousKeyframeShot(state, active);
          const srcRun = src ? shotRunOf(state, src) : undefined;
          if (!src || !srcRun?.keyframeDataUrl) {
            message.warning('Chưa có ảnh cảnh trên shot nào. Gắn KF01 (bàn ăn) trên shot đầu, không dùng ảnh mặt CHAR.');
            return;
          }
          patchRun(active.id, {
            keyframeDataUrl: srcRun.keyframeDataUrl,
            keyframeFileName: srcRun.keyframeFileName,
            keyframePath: srcRun.keyframePath,
            keyframeInheritedFrom: src.id,
          });
        }}
        onApproveKeyframe={() => {
          markKeyframe();
        }}
        onCreateVideo={startTurbo}
        onCreateSceneVideo={startSceneTurbo}
        onAbDiagnostic={startAbDiagnostic}
        onLipsync={startLipsync}
        onLipsyncPrefs={(prefs) => persistState({ ...stateRef.current, ...prefs })}
        onMixPrefs={(prefs) =>
          persistState({
            ...stateRef.current,
            mixPrefs: { ...normalizeMixPrefs(stateRef.current.mixPrefs), ...prefs },
          })
        }
        onAttachLipsync={attachLipsync}
        lipsyncBusy={lipsyncBusy}
        sceneBatchLabel={sceneBatchLabel}
        sceneBatchDisabled={
          !hasVideoKey ||
          sceneVideo.ready.length === 0 ||
          !canWorkV2Scene(state) ||
          Boolean(state.sceneLocked) ||
          Boolean(turboBusy) ||
          runwayQuietRemainMin(runwayQuietUntil) > 0
        }
        batchPlan={sceneKfPlan}
        batchSceneShots={prodQueue}
        batchKfNew={sceneKfNew.length}
        batchKfReuse={sceneKfReuse}
        onGenerateSceneKf={generateSceneKf}
        onPickSceneKeyframe={(file, shotId) => {
          const pack = productionShorts(stateRef.current);
          const plan = buildSceneKfPlan(stateRef.current, pack);
          const target =
            (shotId ? pack.find((s) => s.id === shotId) : undefined) ||
            firstNewKfShot(pack, plan) ||
            active;
          if (!target) {
            message.warning('Chưa có Short để gắn hình.');
            return;
          }
          void (async () => {
            await onKeyframeFile(target.id, file);
            const cur = stateRef.current;
            persistState(applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)));
            message.success(`Đã gắn ảnh SK cho ${shCode(cur, target)}. 0 Gemini.`);
          })();
        }}
        onApproveSceneKf={approveSceneKf}
        onQaKf={rerunVisualQa}
        onRegenerateSelectedKf={regenerateSelectedKf}
        generateSceneKfBusy={Boolean(stillBusy)}
        sceneVideoReady={sceneVideo.ready.length}
        sceneVideoBlocked={sceneVideo.blocked.length}
        sceneBatchCredits={sceneBatchCredits}
        runwayQuietMin={runwayQuietRemainMin(runwayQuietUntil)}
        onClearRunwayQuiet={() => {
          clearRunwayQuietUntil();
          setRunwayQuietUntil(0);
          message.success('Đã mở gửi. Hỏi lại · 0 cr trước; Gửi lại từng hàng (Confirm). Không gửi hàng loạt.');
        }}
        sessionSrcOf={(id) => sessionClips[id]}
        onPassTake={(s) => lockShotOf(s, { stay: true, autoReview: true })}
        onFailTake={(s) => {
          setActiveId(s.id);
          patchRun(s.id, { status: 'rejected' });
        }}
        cutFrom={cutFrom}
        cutTo={cutTo}
        cutPick={cutPick}
        onCutRange={(from, to) => {
          setCutFrom(from);
          setCutTo(to);
        }}
        onCutPick={setCutPick}
        onLockSceneMaster={(sceneId, locked) => {
          if (locked && state.outputAspect !== '16:9' && state.outputAspect !== '9:16') {
            message.warning('Chọn khung xuất 16:9 hoặc 9:16 trước khi khóa Scene Master.');
            return;
          }
          persistState(locked ? lockSceneMaster(state, sceneId) : unlockSceneMaster(state, sceneId));
        }}
        onOutputAspect={setOutputAspect}
        onPatchSceneMaster={(sceneId, patch) => {
          persistState(upsertSceneMaster(state, { sceneId, ...patch }));
        }}
        cutPlan={cutPlan}
        ttsUrlOf={(id) => {
          if (ttsFiles[id]?.url) return ttsFiles[id].url;
          const cached = ttsUrls.current.get(id);
          if (cached) return cached;
          const blob = ttsBlobs.current.get(id);
          if (!blob) return undefined;
          const url = URL.createObjectURL(blob);
          ttsUrls.current.set(id, url);
          return url;
        }}
        onFillPreviewCut={fillPreviewCut}
        onDownloadTakes={downloadTakes}
        onAssembleCut={assembleCut}
        onEnsureTts={hydrateCutVoice}
        assembleBusy={assembleBusy}
        assembleAspect={assembleAspect}
        onAssembleAspect={setOutputAspect}
        placeHint={scenePlaceHint}
        onAcceptPlaceChange={() => {
          if (active) patchRun(active.id, { kfForceNew: true });
        }}
        onKeepPlaceBaseline={() => {
          if (active) patchRun(active.id, { kfForceNew: false });
        }}
        onForceKfNew={(s) => patchRun(s.id, { kfForceNew: true })}
        onReview={setReview}
        onLockShot={lockShot}
        onFailShot={() => {
          failShot();
        }}
        onPickVideo={(file) => {
          if (active) onClipFile(active.id, file);
        }}
        pane={studioPane}
        onOpenScript={() => setStudioPane('script')}
        onOpenVoice={() => setStudioPane('voice')}
        onSeconds={(id, sec) => persistState(setShortSeconds(state, id, sec))}
        onLockScene={lockScene}
        onApprovePreview={() => persistState({ ...state, previewApproved: true })}
        onPatchRun={(id, patch) => patchRun(id, patch)}
        onApproveScene={(sceneId) =>
          persistState({ ...state, sceneApproved: { ...state.sceneApproved, [sceneId]: true } })
        }
        onI2vProductionMode={(on) => persistState({ ...stateRef.current, i2vProductionMode: on })}
        onOpenShorts={() => setStudioPane('advanced')}
        onOpenStudio={() => {
          if (!state.scriptLocked && !state.voiceLocked && !voiceProductionReady(state)) {
            message.warning('Khóa kịch bản rồi mới chia Short.');
            setStudioPane('script');
            return;
          }
          if (!state.scriptLocked) persistState({ ...state, scriptLocked: true });
          if (!voiceProductionReady(state)) {
            message.warning('Duyệt thoại (VOICE LOCKED) trước khi tạo hình/video.');
            setStudioPane('voice');
            return;
          }
          setStudioPane('studio');
        }}
        onOpenTimeline={() => {
          if (!canOpenStudio(state)) {
            message.warning(sceneBlockReason(state) ?? 'Dựng cảnh (shot dài) chưa mở.');
            setStudioPane('script');
            return;
          }
          setStudioPane('timeline');
        }}
        onOpenAdvanced={() => setStudioPane('advanced')}
        onOpenMemory={() => setMemOpen(true)}
      >
        {studioPane === 'timeline' ? (
          <FamixaTimelinePane
            shots={shots}
            episode={ep}
            lock={lockMem}
            sceneNotes={state.sceneNotes}
            sceneReady={sceneReady}
            sceneLocked={state.sceneLocked}
            statusOf={(s) => shotRunOf(state, s).status}
            actionOf={(s) => effectiveShotAction(s, shotRunOf(state, s))}
            runOf={(s) => shotRunOf(state, s)}
            cuesOf={(s) => voiceCuesForShot(state, s)}
            kitCredits={credits}
            runwaySpent={spentOnRunway}
            onNotes={(sceneNotes) => persistState({ ...state, sceneNotes })}
            onLockScene={lockScene}
            onUnlockScene={() => persistState({ ...state, sceneLocked: false })}
            onOpenShot={(s) => {
              selectShot(s);
              setStudioPane('studio');
            }}
          />
        ) : null}
        {studioPane === 'script' || studioPane === 'voice' ? (
          <Card size="small" title="Bản dựng" style={{ marginBottom: 16 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              Mỗi dòng là một lần dựng (kịch bản + thoại + hình + video). Graph trên server. Tệp KF/TTS/take ở máy này.
              Không giới hạn số tập. Nhận pack ghi đè bản đang mở.
            </Typography.Paragraph>
            <Space wrap style={{ marginBottom: 8 }}>
              <Button onClick={createBuild} disabled={buildBusy}>
                Bản dựng mới
              </Button>
              <Button onClick={() => void refreshBuilds()} disabled={buildBusy}>
                Tải lại danh sách
              </Button>
              {state.buildId ? <Tag color="blue">Đang mở</Tag> : <Tag>Chưa lưu hàng</Tag>}
            </Space>
            {builds.length === 0 ? (
              <Typography.Text type="secondary">Chưa có hàng — Nhận pack sẽ tạo bản Nháp.</Typography.Text>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="fx-build-table">
                  <thead>
                    <tr>
                      <th>Tập</th>
                      <th>Tên</th>
                      <th>Trạng thái</th>
                      <th>Short</th>
                      <th>Thoại</th>
                      <th>KF</th>
                      <th>Video</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {builds.map((b) => {
                      const on = b.id === state.buildId;
                      const st = (b.status in SERIES_BUILD_STATUS_VI
                        ? b.status
                        : 'draft') as SeriesBuildStatus;
                      return (
                        <tr key={b.id} className={on ? 'fx-build-table__on' : undefined}>
                          <td>
                            <Button type="link" size="small" disabled={buildBusy} onClick={() => openBuild(b.id)}>
                              {b.episodeCode || '—'}
                            </Button>
                          </td>
                          <td>
                            <Button type="link" size="small" disabled={buildBusy} onClick={() => openBuild(b.id)}>
                              {b.title || 'Bản dựng'}
                            </Button>
                          </td>
                          <td>
                            <Tag color={on ? 'blue' : undefined}>{SERIES_BUILD_STATUS_VI[st]}</Tag>
                          </td>
                          <td>{b.shotCount}</td>
                          <td>{b.voiceLines}</td>
                          <td>{b.kfCount}</td>
                          <td>{b.videoCount}</td>
                          <td>
                            <Button
                              type="link"
                              size="small"
                              danger
                              disabled={buildBusy}
                              onClick={() => removeBuild(b.id, b.title || b.episodeCode)}
                            >
                              Xóa
                            </Button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        ) : null}
        {studioPane === 'script' || studioPane === 'voice' || studioPane === 'shorts' || studioPane === 'advanced' ? (
          <Collapse
            destroyOnHidden
            defaultActiveKey={
              studioPane === 'voice'
                ? ['cast']
                : studioPane === 'script'
                  ? []
                  : studioPane === 'shorts'
                    ? ['shorts']
                    : ['long']
            }
            items={[
              ...(studioPane === 'script' || studioPane === 'voice'
                ? [
              {
                key: 'pack',
                label: 'Nhận pack Story',
                children: (
                  <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={
          packMode === 'ai'
            ? 'AI đề xuất nháp vào ô dưới. Bạn sửa rồi Nhận pack — không tự khóa, không gửi I2V.'
            : 'Dán kịch bản tập. KIT bung EP → cảnh → shot → thoại. Không tự viết chuyện, chưa trừ Runway.'
        }
        description={
          shorts.length || shots.length
            ? `Sổ KIT (đã khóa): ${credits} cr. Runway đã trừ: ${spentOnRunway} cr. Short khóa ${approvedShortCount(state)}/${shorts.length}.`
            : packMode === 'ai'
              ? 'Một câu hạt giống. Gemini (Cấu hình AI) — không trừ credit Runway.'
              : 'Không cần VIDEO ID hay --- SHOT ---. Dán cả tập rồi bấm Nhận pack — chưa generate.'
        }
      />

      <Card size="small" title="Pack kịch bản" style={{ marginBottom: 0 }}>
        <Space wrap style={{ marginBottom: 12 }}>
          {famixaBrand ? (
            <Tag color={famixaBrand.brainReady ? 'success' : 'gold'}>
              Brand Brain {famixaBrand.code}
              {famixaBrand.brainReady ? '' : ' — thiếu kiến thức'}
            </Tag>
          ) : (
            <Tag color="warning">Chưa có brand Famixa</Tag>
          )}
          <Typography.Text type="secondary">
            48 tài liệu chưng vào{' '}
            <Link to="/content/brands">Thương hiệu</Link>
            {' — '}Studio chỉ lấy lát DNA/cấm + Memory + Action. Không dán docs vào I2V.
          </Typography.Text>
        </Space>
        <Segmented
          value={packMode}
          disabled={state.scriptLocked}
          onChange={(v) => setPackMode(v === 'ai' ? 'ai' : 'paste')}
          options={[
            { label: 'Tự dán', value: 'paste' },
            { label: 'AI đề xuất', value: 'ai' },
          ]}
          style={{ marginBottom: 12 }}
        />
        {packMode === 'ai' ? (
          <div style={{ marginBottom: 12 }}>
            <Space wrap style={{ marginBottom: 8 }}>
              <Tag color={keys.gemini ? 'success' : 'warning'}>{keys.gemini ? 'Đã có key Gemini' : 'Chưa có key Gemini'}</Tag>
              <Typography.Text type="secondary">0 cr Runway · phí Gemini theo Model AI</Typography.Text>
            </Space>
            <Input.TextArea
              rows={3}
              value={packSeed}
              disabled={state.scriptLocked}
              onChange={(e) => setPackSeed(e.target.value)}
              placeholder="Hạt giống — vd. Minh thất vọng vì bố không đến xem buổi thi. Giữ CHAR-001 Minh / Nam / Linh bàn cơm tối."
            />
            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              style={{ marginTop: 8 }}
              loading={draftBusy}
              disabled={state.scriptLocked || packSeed.trim().length < 12 || !keys.gemini}
              onClick={requestDraft}
            >
              Đề xuất kịch bản
            </Button>
          </div>
        ) : (
          <Typography.Paragraph type="secondary">
            Nhận <strong>kịch bản tập</strong> (FAMIXA / EPISODE / SCENE / thoại) — KIT bung cảnh và shot,
            không tự viết chuyện. Không cần 4 dòng VIDEO ID. Khối <code>--- SHORT ---</code> / shot master
            <code>VIDEO ID …SC01-SH01</code> vẫn nhận được.
          </Typography.Paragraph>
        )}
        <Input.TextArea
          rows={10}
          value={packText}
          onChange={(e) => setPackText(e.target.value)}
          spellCheck={false}
          placeholder={
            packMode === 'ai'
              ? 'Nháp sẽ hiện ở đây sau khi đề xuất. Sửa rồi bấm Nhận pack.'
              : 'Dán kịch bản tập Famixa (không cần --- SHOT ---):\n\nVIDEO TITLE: BỐ ĐỪNG HỨA NỮA\nSC01 — LỜI HỨA\nMinh:\nBố.'
          }
          style={{ fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
        />
        <Space wrap style={{ marginTop: 12 }}>
          <Button onClick={() => setPackText(FAMIXA_PACK_SKELETON)}>Chèn khung trống</Button>
          <Button
            onClick={() => {
              void import('./content-famixa-ep01-golden.txt?raw').then((m) =>
                setPackText(String(m.default ?? '').trim()),
              );
            }}
          >
            Kịch bản EP01 đã gửi
          </Button>
          <Button type="primary" disabled={!packText.trim()} onClick={applyPack}>
            Nhận pack
          </Button>
          {active ? (
            <>
              <Button icon={<CopyOutlined />} onClick={() => copyShotPack(active)}>
                Copy khối {active.id}
              </Button>
              <Button onClick={() => putShotPackToPaste(active)}>
                Đưa khối {active.id} lên ô dán để sửa
              </Button>
            </>
          ) : null}
        </Space>
        {packResult ? (
          <Alert
            type={packResult.ok ? 'success' : 'error'}
            showIcon
            style={{ marginTop: 12 }}
            message={packResult.text}
          />
        ) : null}
        {packResult?.ok && (state.scenes?.length || state.lines?.length) ? (
          <Card size="small" title="Parsed Story — Script Beat → Shot" style={{ marginTop: 12 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {[ep?.episode, ep?.title].filter(Boolean).join(' · ') || 'EP'} — kịch bản quyết định nội dung. Shot
              Decomposition chỉ cách quay. KIT không tạo SH rỗng để đủ số lượng. 70 Short là năng lực, không phải
              requirement.
            </Typography.Paragraph>
            {(state.parseWarnings ?? []).map((w) => (
              <Alert key={w} type="warning" showIcon style={{ marginBottom: 8 }} message={w} />
            ))}
            {(state.characters ?? []).map((c) => (
              <Tag key={c.id}>
                {c.id} {c.name}
                {c.role ? ` · ${c.role}` : ''}
                {c.voiceName || c.voiceId ? ` · voice ${c.voiceName || c.voiceId}` : ' · chưa voice'}
              </Tag>
            ))}
            {(state.scenes ?? []).map((sc) => {
              const sceneShots = episodeShots(state).filter(
                (s) =>
                  (s.sceneId || s.scene) === sc.id ||
                  (s.id || '').includes(`-${sc.id}-`) ||
                  (s.id || '').startsWith(`${sc.id}-`),
              );
              const beats = sc.scriptBeats?.length
                ? sc.scriptBeats
                : groupShotsByBeat(sceneShots).map((g, i) => ({
                    id: g.beatId || `${sc.id}-BEAT${String(i + 1).padStart(2, '0')}`,
                    text: g.label,
                    shotIds: g.shots.map((s) => s.id),
                  }));
              const sceneLines = linesForScene(state, sc.id);
              return (
                <div key={sc.id} style={{ marginTop: 12 }}>
                  <Typography.Text strong>
                    {sc.id}
                    {sc.title ? ` — ${sc.title}` : ''}
                  </Typography.Text>
                  <Typography.Paragraph type="secondary" style={{ margin: '2px 0 6px', fontSize: 12 }}>
                    Kịch bản: {beats.length} beats · Đề xuất {sceneShots.filter((s) => shotHasValidAction(s)).length}{' '}
                    shots
                    {(() => {
                      const d = shotPlanDensity(sceneShots);
                      return d.warn ? ` · mật độ ${d.ratio.toFixed(1)} — xem lại` : '';
                    })()}
                    {sc.environment ? ` · ${sc.environment}` : ''}
                  </Typography.Paragraph>
                  {beats.map((b, i) => {
                    const kids = sceneShots.filter((s) => b.shotIds.includes(s.id));
                    return (
                      <div key={b.id} style={{ margin: '6px 0 8px 8px', fontSize: 12 }}>
                        <div>
                          <Typography.Text strong>BEAT {String(i + 1).padStart(2, '0')}</Typography.Text>
                          <span style={{ color: '#64748b' }}> · {b.text}</span>
                        </div>
                        {kids.map((s, si) => {
                          const hold = !shotHasValidAction(s);
                          return (
                            <div key={s.id} style={{ marginLeft: 16, color: hold ? '#94a3b8' : '#334155' }}>
                              {si === kids.length - 1 ? '└─' : '├─'} {s.shot} — {s.story}
                              {s.splitReason ? ` · ${s.splitReason}` : ''}
                              {hold ? ' · HOLD' : ` · ${s.seconds || 5}s`}
                            </div>
                          );
                        })}
                      </div>
                    );
                  })}
                  {sceneLines.map((l) => {
                    const who = (state.characters ?? []).find((c) => c.id === l.characterId);
                    return (
                      <Typography.Paragraph key={l.id} style={{ margin: '2px 0 0 8px', fontSize: 12 }}>
                        {who?.name || l.characterId}: {l.text}
                      </Typography.Paragraph>
                    );
                  })}
                </div>
              );
            })}
            <Space wrap style={{ marginTop: 12 }}>
              {state.storyReviewed ? (
                <Tag color="green">Đã duyệt Parsed Story</Tag>
              ) : (
                <Button
                  type="primary"
                  onClick={() => persistState({ ...state, storyReviewed: true })}
                >
                  Parsed Story đúng
                </Button>
              )}
              {state.shotGraphLocked ? (
                <Tag color="green">SHOT GRAPH LOCKED</Tag>
              ) : (
                <Button onClick={lockShotGraph} disabled={!state.storyReviewed}>
                  Duyệt cách chia shot
                </Button>
              )}
            </Space>
          </Card>
        ) : null}
        <ContentFamixaStoryMemoryCard state={state} onChange={persistState} />
      </Card>
                  </>
                ),
              },
              {
                key: 'cast',
                label: `Vai & tập${state.roles.length ? ` · ${state.roles.length} vai` : ''}`,
                children: (
                  <>
      <Card
        size="small"
        title="Vai · Cast & Canon"
        extra={
          <Button size="small" icon={<PlusOutlined />} onClick={addRole} disabled={castFrozen}>
            Thêm vai
          </Button>
        }
        style={{ marginBottom: 16 }}
      >
        {state.roles.length === 0 ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Dán <code>ROLE: Story | An</code> trong pack, hoặc bấm Thêm vai. Gắn 1 ảnh Canon + Voice Canon / vai — shot sau kế thừa, không chọn lại Minh từng clip.
          </Typography.Paragraph>
        ) : null}
        <div className="content-video-lab-cards">
          {state.roles.map((r) => {
            const ch = characterOfRole(state, r);
            const canonOn = characterCanonReady(ch);
            const voiceOn = roleVoiceReady(state, r);
            const voiceId = (ch?.voiceId || r.voiceId || '').trim();
            const voicePick = voiceOptionsForRole(r, voiceId);
            const childLane = isChildVoiceLane(voicePick.lane);
            const voiceOnly = isVoiceOnlyRole(r, ch);
            const chosenVoice = voices.find((v) => v.voiceId === voiceId);
            const adultOnChild = Boolean(childLane && voiceId && chosenVoice && !isKidLibraryVoice(chosenVoice));
            const southOnRole = Boolean(
              (chosenVoice && !voiceSoundsNorthern(chosenVoice)) ||
                /southern|saigon|sai\s*gon|miền nam|mien nam|giọng nam bộ|giong nam/i.test(
                  `${ch?.voiceName || ''} ${r.voiceName || ''}`,
                ),
            );
            const portrait = ch ? canonDisplayOf(state, ch.id) : undefined;
            const master = famixaCanonSeedFor(ch ?? { id: r.characterId, name: r.name });
            const canonBlocked = castFrozen && canonOn;
            return (
            <div key={r.id} className="content-video-lab-card" style={{ cursor: 'default' }}>
              <Input
                placeholder="Tên vai"
                value={r.title}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { title: e.target.value })}
              />
              <Input
                style={{ marginTop: 8 }}
                placeholder="Tên người"
                value={r.name}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { name: e.target.value })}
              />
              <Space wrap style={{ marginTop: 8 }}>
                {r.characterId ? <Tag>{r.characterId}</Tag> : <Tag>Chưa gắn CHAR</Tag>}
                {voiceOnly ? (
                  <Tag>Chỉ giọng</Tag>
                ) : canonOn ? (
                  <Tag color="green">Canon ✓</Tag>
                ) : (
                  <Tag color="gold">Chưa Canon</Tag>
                )}
                {master ? <Tag>Master v1.0</Tag> : null}
                {voiceOn ? (
                  <Tag color="green">Voice {ch?.voiceName || '✓'}</Tag>
                ) : (
                  <Tag color="gold">Chưa Voice</Tag>
                )}
                {voices.some((v) => v.voiceId === voiceId && v.cloned) ? (
                  <Tag color="warning">Clone — gói chưa TTS</Tag>
                ) : null}
                {childLane ? <Tag>11 tuổi</Tag> : null}
                {adultOnChild ? <Tag color="gold">Giọng người lớn — chọn lại giọng trẻ</Tag> : null}
                {southOnRole ? <Tag color="gold">Giọng Nam/không gắn Bắc — đổi Hà Nội</Tag> : null}
              </Space>
              <div style={{ marginTop: 8 }}>
                {voiceOnly ? (
                  <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }}>
                    Lời bình không lên hình — không gắn ảnh Canon, không I2V. Chỉ chọn giọng nam miền Bắc (trầm ấm).
                  </Typography.Paragraph>
                ) : portrait ? (
                  <img
                    src={portrait}
                    alt={ch?.canonFileName || r.name || 'canon'}
                    style={{ width: 96, height: 96, objectFit: 'cover', borderRadius: 8, display: 'block' }}
                  />
                ) : (
                  <Tag>{ch?.canonFileName || 'Chưa có ảnh Canon'}</Tag>
                )}
                {voiceOnly ? null : (
                  <>
                    <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                      {ch?.canonFileName || ch?.canonLocalPath
                        ? [ch.canonFileName, ch.canonLocalPath].filter(Boolean).join(' · ')
                        : master
                          ? 'Mặt / tóc / tuổi — không dùng làm KF I2V'
                          : 'Không có Master sheet. Chọn ảnh mặt từ máy — không dùng làm KF I2V.'}
                    </Typography.Paragraph>
                    <input
                      type="file"
                      accept="image/*"
                      hidden
                      disabled={canonBlocked}
                      id={`canon-${r.id}`}
                      onChange={(e) => {
                        void onRoleCanon(r.id, e.target.files?.[0]);
                        e.target.value = '';
                      }}
                    />
                    <Space wrap style={{ marginTop: 4 }}>
                      <Button
                        size="small"
                        icon={<FolderOpenOutlined />}
                        disabled={canonBlocked}
                        onClick={() => document.getElementById(`canon-${r.id}`)?.click()}
                      >
                        {canonOn ? 'Thay Canon' : 'Gắn Canon'}
                      </Button>
                    </Space>
                  </>
                )}
              </div>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Câu thoại duyệt (1 câu)"
                value={r.line ?? ''}
                disabled={state.scriptLocked}
                onChange={(e) => patchRole(r.id, { line: e.target.value })}
              />
              <Select
                style={{ marginTop: 8, width: '100%' }}
                placeholder={
                  keys.elevenLabs
                    ? voicePick.options.length
                      ? `Chọn ${voicePick.lane.label}`
                      : 'Không lọc được giọng — dán Voice ID bên dưới'
                    : 'Chưa có key ElevenLabs — vẫn gán Voice ID'
                }
                showSearch
                allowClear
                listHeight={280}
                disabled={castFrozen}
                value={voiceId || undefined}
                filterOption={voicePick.filterOption}
                options={voicePick.options}
                notFoundContent={voicesLoading ? 'Đang tải thư viện…' : 'Trống — xóa ô tìm, hoặc dán Voice ID nam Hà Nội'}
                onChange={(id, opt) => {
                  const label = !Array.isArray(opt) && opt && typeof opt === 'object' && 'label' in opt
                    ? String(opt.label)
                    : voices.find((v) => v.voiceId === id)?.name;
                  patchRole(r.id, { voiceId: id, voiceName: id ? label : undefined });
                }}
              />
              <Button
                size="small"
                icon={<ReloadOutlined />}
                loading={voicesLoading}
                style={{ marginTop: 6 }}
                onClick={() => void loadVoiceLibrary({ force: true })}
              >
                {voices.length ? `Tải lại thư viện (${voices.length})` : 'Tải thư viện giọng'}
              </Button>
              <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                {voicesLoading
                  ? 'Đang tải thư viện giọng ElevenLabs… Voice ID trên vai vẫn giữ.'
                  : childLane
                    ? southOnRole
                      ? 'An/Minh nói giọng Bắc. Voice đang chọn nghe Nam — đổi giọng Hà Nội (young) rồi Tạo Full Voice lại. Câu thoại giữ nguyên.'
                      : adultOnChild
                      ? 'Minh/An là bé trai 11 tuổi. Đổi sang giọng nam young/bé — TTS lại mới nghe khác.'
                      : voices.length
                        ? `${voicePick.options.length} giọng trẻ 11 tuổi miền Bắc. TTS lại sau khi đổi giọng.`
                        : 'Chưa lấy được thư viện giọng — Voice ID trên vai vẫn dùng được. Thử reload.'
                    : voicePick.options.length
                      ? `${voicePick.options.length} giọng ${voicePick.lane.label}. Thêm nhân vật sau cũng chỉ hiện đúng loại giọng.`
                      : keys.elevenLabs
                        ? voices.length
                          ? voiceOnly
                            ? 'Xóa chữ trong ô tìm — hiện giọng nam miền Bắc. Hoặc dán Voice ID Hà Nội.'
                            : 'Chưa có giọng miền Bắc khớp vai — reload hoặc dán Voice ID.'
                          : 'Chưa lấy được thư viện giọng — Voice ID trên vai vẫn dùng được. Thử reload.'
                        : 'Cần key ElevenLabs để tải thư viện giọng miền Bắc.'}
              </Typography.Paragraph>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Hoặc dán Voice ID"
                value={voiceId}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { voiceId: e.target.value.trim() })}
              />
              <Input
                style={{ marginTop: 8 }}
                placeholder="Ghi chú giọng (tốc độ / cảm xúc) — không phải Voice ID"
                value={r.voiceNote ?? ''}
                disabled={state.scriptLocked}
                onChange={(e) => patchRole(r.id, { voiceNote: e.target.value })}
              />
              {ch ? (
                <Space wrap style={{ marginTop: 8 }}>
                  <Input
                    size="small"
                    style={{ width: 88 }}
                    placeholder="Tuổi (11)"
                    value={ch.voiceBible?.ageImpression ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { ageImpression: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 120 }}
                    placeholder="Tone"
                    value={ch.voiceBible?.tone ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { tone: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 140 }}
                    placeholder="Habit (ngập…)"
                    value={ch.voiceBible?.habit ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { habit: e.target.value }))}
                  />
                  <Input
                    size="small"
                    style={{ width: 120 }}
                    placeholder="Formality"
                    value={ch.voiceBible?.formality ?? ''}
                    disabled={castFrozen}
                    onChange={(e) => persistState(patchVoiceBible(state, ch.id, { formality: e.target.value }))}
                  />
                </Space>
              ) : null}
              <Button
                size="small"
                icon={<SoundOutlined />}
                loading={ttsBusy && listenCues.find((c) => c.id === playingLineId)?.characterId === (ch?.id || r.characterId)}
                disabled={!keys.elevenLabs || !voiceId || (!(r.line ?? '').trim() && !listenCues.some((c) => c.characterId === ch?.id))}
                style={{ marginTop: 4 }}
                onClick={() => {
                  const cue =
                    listenCues.find((c) => c.characterId === (ch?.id || r.characterId)) ||
                    ((r.line ?? '').trim()
                      ? {
                          id: `role-line-${r.id}`,
                          characterId: ch?.id || r.characterId || r.id,
                          name: r.name || ch?.name || r.title,
                          text: (r.line ?? '').trim(),
                          voiceId,
                        }
                      : undefined);
                  if (!cue) {
                    message.warning('Chưa có câu thoại của vai này trong kịch bản.');
                    return;
                  }
                  void playListenCue(cue);
                }}
              >
                {ttsBusy && listenCues.find((c) => c.id === playingLineId)?.characterId === (ch?.id || r.characterId)
                  ? 'Đang đọc…'
                  : 'Nghe thử câu của vai'}
              </Button>
              <Input
                style={{ marginTop: 8 }}
                placeholder="Performance Canon (tự nhiên, ít gesture…)"
                value={r.performance ?? ch?.performance ?? ''}
                disabled={castFrozen}
                onChange={(e) => patchRole(r.id, { performance: e.target.value })}
              />
              <Button
                type="link"
                size="small"
                danger
                icon={<DeleteOutlined />}
                onClick={() => removeRole(r.id)}
                style={{ paddingLeft: 0, marginTop: 4 }}
                disabled={castFrozen}
              >
                Xóa
              </Button>
            </div>
            );
          })}
        </div>
        <Space wrap style={{ marginTop: 8 }}>
          {state.castLocked ? (
            <>
              <Tag color="green">Cast & Canon đã khóa</Tag>
              <Button onClick={() => persistState({ ...state, castLocked: false })}>
                Mở khóa Cast
              </Button>
            </>
          ) : (
            <Button
              type="primary"
              icon={<LockOutlined />}
              disabled={!canLockCast(state)}
              onClick={() => {
                persistState(lockCast(state));
                message.success('Đã khóa Cast & Canon. Shot sau dùng cùng ảnh + giọng.');
              }}
            >
              Khóa Cast & Canon
            </Button>
          )}
          {ready && state.roles.every((r) => roleCanonReady(state, r) && roleVoiceReady(state, r)) ? (
            <Tag color="success">{state.roles.length} vai · Canon + Voice đủ</Tag>
          ) : ready && state.roles.some((r) => isVoiceOnlyRole(r, characterOfRole(state, r)) && !roleVoiceReady(state, r)) ? (
            <Tag color="gold">Gán Voice Canon cho Lời bình — không cần ảnh</Tag>
          ) : ready ? (
            <Tag color="gold">Thiếu ảnh Canon hoặc Voice Canon</Tag>
          ) : (
            <Tag>{state.roles.length === 0 ? 'Chưa có vai' : 'Điền đủ tên vai + người'}</Tag>
          )}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          Canon v{state.canonVersion || 1} lưu máy — tập sau vẫn đúng: Minh / Nam / Linh trong khung · An ngoài khung ·
          không tự thêm người từ heading.
        </Typography.Paragraph>
      </Card>

      <Card size="small" title="Full Script → Voice Script → Full Voice" style={{ marginBottom: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          Kịch bản gốc không đổi. KIT tách Voice Script — chỉ lời CAST nói. Không đọc mô tả, SFX, tin nhắn, câu nhớ lại.
        </Typography.Paragraph>
        {voiceScript.droppedNonSpeechCount > 0 ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 8 }}
            message={`Đã bỏ ${voiceScript.droppedNonSpeechCount} dòng action/SMS/SFX khỏi Voice Script — không TTS.`}
          />
        ) : null}
        {ttsNote ? (
          <Typography.Paragraph style={{ marginBottom: 8 }}>{ttsNote}</Typography.Paragraph>
        ) : null}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 8 }}>
          <div>
            <Typography.Text strong>FULL SCRIPT</Typography.Text>
            <div style={{ maxHeight: 220, overflow: 'auto', marginTop: 6, fontSize: 12 }}>
              {(state.scenes ?? []).map((sc) => (
                <div key={sc.id} style={{ marginBottom: 8 }}>
                  <Typography.Text type="secondary">
                    {sc.id}
                    {sc.title ? ` — ${sc.title}` : ''}
                  </Typography.Text>
                  {(sc.actions ?? []).slice(0, 3).map((a) => (
                    <Typography.Paragraph key={a} type="secondary" style={{ margin: 0, fontSize: 11 }}>
                      {a}
                    </Typography.Paragraph>
                  ))}
                  {(sc.dialogue ?? []).map((d) => (
                    <Typography.Paragraph key={d.id} style={{ margin: 0 }}>
                      {(state.characters ?? []).find((c) => c.id === d.characterId)?.name || d.characterId}: {d.text}
                    </Typography.Paragraph>
                  ))}
                </div>
              ))}
            </div>
          </div>
          <div>
            <Typography.Text strong>VOICE SCRIPT (TTS)</Typography.Text>
            <Input.TextArea
              readOnly
              value={formatVoiceScriptPreview(voiceScript) || 'Chưa có thoại — Nhận pack.'}
              rows={10}
              style={{ marginTop: 6, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
            />
          </div>
        </div>
        {listenCues.length === 0 ? null : (
          <div style={{ maxHeight: 280, overflow: 'auto', marginBottom: 8 }}>
            {listenCues.map((cue) => (
              <div
                key={cue.id}
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'flex-start',
                  padding: '4px 0',
                  background: playingLineId === cue.id ? 'rgba(22, 119, 255, 0.08)' : undefined,
                }}
              >
                <Button
                  size="small"
                  type="text"
                  icon={<SoundOutlined />}
                  loading={ttsBusy && playingLineId === cue.id}
                  disabled={!cue.voiceId || !keys.elevenLabs}
                  onClick={() => void playListenCue(cue)}
                />
                <div style={{ flex: 1 }}>
                  <Typography.Text strong>[{cue.name}]</Typography.Text>
                  {(() => {
                    const dir = resolveLinePerformance({
                      text: cue.text,
                      characterId: cue.characterId,
                      name: cue.name,
                      performance: cue.performance ?? linePerformanceOf(state, cue.id),
                    });
                    const locked = Boolean((cue.performance ?? linePerformanceOf(state, cue.id))?.locked);
                    return (
                      <Space size={4} wrap style={{ marginLeft: 6 }}>
                        <Tag color={locked ? 'green' : 'default'}>{locked ? 'LOCKED' : 'DRAFT'} {dir.label}</Tag>
                        <Select
                          size="small"
                          style={{ width: 110 }}
                          value={dir.emotion}
                          disabled={state.voiceLocked}
                          options={ACTING_EMOTIONS.map((e) => ({ value: e, label: e }))}
                          onChange={(emotion) => persistState(patchDialoguePerformance(state, cue.id, { emotion }))}
                        />
                        <Select
                          size="small"
                          style={{ width: 56 }}
                          value={dir.intensity}
                          disabled={state.voiceLocked}
                          options={[1, 2, 3, 4, 5].map((n) => ({ value: n, label: `${n}` }))}
                          onChange={(intensity) => persistState(patchDialoguePerformance(state, cue.id, { intensity }))}
                        />
                        <Select
                          size="small"
                          style={{ width: 88 }}
                          value={dir.pace}
                          disabled={state.voiceLocked}
                          options={['slow', 'natural', 'fast'].map((p) => ({ value: p, label: p }))}
                          onChange={(pace) => persistState(patchDialoguePerformance(state, cue.id, { pace }))}
                        />
                      </Space>
                    );
                  })()}
                  {!cue.voiceId ? <Tag style={{ marginLeft: 6 }}>Chưa Voice</Tag> : null}
                  <Typography.Paragraph style={{ margin: 0, fontSize: 12 }}>{cue.text}</Typography.Paragraph>
                </div>
              </div>
            ))}
          </div>
        )}
        <Space wrap style={{ marginBottom: 8 }}>
          <Tag>Dialogue lines: {voiceScript.sourceLineCount}</Tag>
          <Tag>Characters: {voiceScript.sourceCharCount.toLocaleString('vi-VN')}</Tag>
          <Tag>
            Voice generated: {(state.voicePreview?.generatedCharCount ?? 0).toLocaleString('vi-VN')}
          </Tag>
          <Tag color={state.voicePreview?.status === 'complete' ? 'green' : state.voicePreview?.status === 'incomplete' ? 'gold' : 'default'}>
            Status: {(state.voicePreview?.status ?? 'idle').toUpperCase()}
          </Tag>
          <Tag>~{voiceScript.estimatedSec}s</Tag>
        </Space>
        {(state.voicePreview?.issues ?? []).slice(0, 4).map((w) => (
          <Alert key={w} type="warning" showIcon style={{ marginBottom: 6 }} message={w} />
        ))}
        <Space wrap>
          <Button
            type="primary"
            icon={<SoundOutlined />}
            loading={ttsBusy}
            disabled={listenCues.length === 0 || !keys.elevenLabs}
            onClick={playWholeScript}
          >
            Tạo Full Voice
          </Button>
          {ttsBusy ? <Button onClick={stopScriptListen}>Dừng</Button> : null}
          <Button
            icon={<DownloadOutlined />}
            loading={ttsBusy}
            disabled={listenCues.length === 0 || !keys.elevenLabs}
            onClick={downloadFullScriptMp3}
          >
            Tải MP3 Voice Script
          </Button>
          {ttsFull ? (
            <Typography.Link href={ttsFull.url} download={ttsFull.fileName}>
              {ttsFull.fileName}
            </Typography.Link>
          ) : null}
        </Space>
      </Card>

      {ep && (ep.title || ep.episode || ep.premise) ? (
        <Card size="small" title={[ep.episode, ep.title].filter(Boolean).join(' · ') || 'Tập từ pack'} style={{ marginBottom: 16 }}>
          {ep.premise ? <Typography.Paragraph style={{ marginBottom: 8 }}>{ep.premise}</Typography.Paragraph> : null}
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {[ep.moral, ep.ctaRule].filter(Boolean).join(' · ')}
          </Typography.Paragraph>
        </Card>
      ) : null}

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="Story → Full Script → Voice Script → Full Voice LOCK → mới Image/Video. Ảnh Canon = mặt/tóc, không phải KF bàn ăn."
      />
      <Space wrap>
        {state.scriptLocked ? (
          <>
            <Tag color="green">Kịch bản đã khóa</Tag>
            <Button onClick={() => persistState({ ...state, scriptLocked: false })}>Mở khóa kịch bản</Button>
          </>
        ) : (
          <Button type="primary" disabled={!canLockScript(state)} onClick={lockScript}>
            Khóa kịch bản → duyệt thoại
          </Button>
        )}
        {state.voiceLocked ? (
          <>
            <Tag color="green">Full Voice đã khóa</Tag>
            <Button onClick={() => persistState({ ...state, voiceLocked: false })}>Mở khóa Voice</Button>
          </>
        ) : (
          <>
            <Tooltip title={canLockVoice(state) ? undefined : voiceLockHint}>
              <span>
                <Button type="primary" disabled={!canLockVoice(state)} onClick={() => lockVoice()}>
                  Khóa Full Voice
                </Button>
              </span>
            </Tooltip>
            {!canLockVoice(state) ? (
              <>
                <Button
                  type="primary"
                  onClick={() => {
                    modal.confirm({
                      title: 'Khóa Full Voice — không gọi ElevenLabs',
                      content:
                        'Dùng khi đã Tạo Full Voice rồi. F5 mất cache hoặc parser bỏ dòng action nên Status không còn COMPLETE. Không trừ phí TTS.',
                      okText: 'Khóa, không TTS lại',
                      cancelText: 'Hủy',
                      onOk: () => lockVoice({ skipRegen: true }),
                    });
                  }}
                >
                  Đã TTS — khóa không gọi lại
                </Button>
                <Button
                  icon={<SoundOutlined />}
                  loading={ttsBusy}
                  disabled={listenCues.length === 0 || !keys.elevenLabs}
                  onClick={playWholeScript}
                >
                  Tạo lại (tính phí)
                </Button>
              </>
            ) : null}
          </>
        )}
      </Space>
      {!state.voiceLocked && !canLockVoice(state) ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 12 }}
          message={voiceLockHint}
          description="Đã tạo Full Voice rồi thì đừng bấm Tạo lại — ElevenLabs tính phí ký tự. F5 / hydrate server có thể làm mất Status COMPLETE. Bấm «Đã TTS — khóa không gọi lại»."
        />
      ) : null}
                  </>
                ),
              },
                ]
              : []),
              ...(studioPane === 'shorts'
                ? [
              {
                key: 'shorts',
                label: shorts.length ? `Short 9:16 · ${shorts.length}` : 'Short 9:16',
                children: (
                  <>
      {shorts.length > 0 ? (
        <Card
          size="small"
          title="Danh sách short"
          extra={
            <Space wrap>
              <Button
                size="small"
                onClick={() => {
                  const cur = activeShort?.id;
                  const next = insertShortClip(stateRef.current, cur ? { beforeId: cur } : undefined);
                  persistState(next.state);
                  setActiveShortId(next.short.id);
                  message.success(
                    `Đã chèn ${next.short.id} trước ${cur || 'cuối danh sách'}. KF clip cũ giữ nguyên.`,
                  );
                }}
              >
                Chèn short trước clip này
              </Button>
              <Button
                size="small"
                onClick={() => {
                  const next = insertShortClip(stateRef.current, activeShort ? { afterId: activeShort.id } : undefined);
                  persistState(next.state);
                  setActiveShortId(next.short.id);
                  message.success(`Đã thêm ${next.short.id}. KF clip cũ giữ nguyên.`);
                }}
              >
                Thêm short sau
              </Button>
            <Button
              size="small"
              loading={Boolean(stillBusy)}
              disabled={!keys.gemini || shorts.length === 0}
              onClick={() => {
                const pending = shorts.filter((s) => !shortRunOf(stateRef.current, s.id).keyframeDataUrl);
                const targets = pending.length ? pending : shorts;
                modal.confirm({
                  title: `Tạo KF 9:16 cho ${targets.length} short từ Canon vai`,
                  content:
                    'Gemini vẽ cả khung cảnh (không phải crop mặt). Quota Gemini — không trừ Runway. Bạn vẫn duyệt / Gửi Turbo từng short.',
                  okText: 'Tạo ảnh',
                  cancelText: 'Hủy',
                  onOk: async () => {
                    for (const s of targets) {
                      const sc = (stateRef.current.scenes ?? []).find((x) => x.id === s.sceneId);
                      const ok = await generateKfFromCanon({
                        clipId: s.id,
                        aspect: '9:16',
                        visual: s.visual || s.hook,
                        action: s.motionPromptVi || s.hook,
                        location: s.scene || sc?.environment,
                        characterIds: s.characterIds?.length ? s.characterIds : s.characters,
                      });
                      if (!ok) break;
                    }
                  },
                });
              }}
            >
              Tạo ảnh KF từng short
            </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        >
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={shorts}
            onRow={(row) => ({
              onClick: () => setActiveShortId(row.id),
              style: {
                cursor: 'pointer',
                background: row.id === activeShort?.id ? '#f5f3ff' : undefined,
              },
            })}
            columns={[
              { title: 'Short', dataIndex: 'id', width: 120 },
              { title: 's', dataIndex: 'seconds', width: 48 },
              { title: 'Hook', dataIndex: 'hook' },
              {
                title: 'TT',
                width: 120,
                render: (_, row) => {
                  const lab = SERIES_STATUS_LABEL[shortRunOf(state, row.id).status];
                  return <Tag color={lab.color}>{lab.text}</Tag>;
                },
              },
            ]}
          />
        </Card>
      ) : (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Tập này không có short 9:16"
          description={
            voiceProductionReady(state)
              ? `Bỏ qua Short. Có ${shots.length} shot 16:9 — bấm Dựng cảnh.`
              : 'Bỏ qua Short. Khóa Full Voice ở Kịch bản rồi vào Dựng cảnh (ảnh/video vẫn chờ Voice COMPLETE).'
          }
        />
      )}
      {(shorts.length === 0 || !voiceProductionReady(state)) ? (
        <Space wrap>
          <Button onClick={() => setStudioPane('script')}>Kịch bản · Full Voice</Button>
          {canOpenStudio(state) ? (
            <Button type="primary" onClick={() => setStudioPane('studio')}>
              Dựng cảnh 16:9
            </Button>
          ) : null}
        </Space>
      ) : null}
                  </>
                ),
              },
              {
                key: 'stills',
                label: 'Ảnh nhân vật',
                children: (
                  <>
      {shorts.length > 0 || shots.length > 0 ? (
        <Card
          size="small"
          title="Ảnh nhân vật — kế thừa Character Canon"
          extra={
            <Space>
              <Checkbox checked={stillOnlyActive} onChange={(e) => setStillOnlyActive(e.target.checked)}>
                Chỉ clip đang làm
              </Checkbox>
              <Button size="small" icon={<PlusOutlined />} onClick={addStill} disabled={state.scriptLocked}>
                Thêm hàng
              </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        >
          <Typography.Paragraph type="secondary">
            Short/shot dùng cùng Canon trên vai. Không chọn lại mặt Minh từng clip. Ảnh này không gửi I2V — KF cảnh vẫn là bàn ăn.
          </Typography.Paragraph>
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={visibleStills}
            locale={{ emptyText: 'Chưa có hàng — nhận pack có CHAR/SCENE hoặc Thêm hàng.' }}
            columns={[
              {
                title: 'Short',
                width: 140,
                render: (_, row) => (
                  <Select
                    size="small"
                    style={{ width: '100%' }}
                    value={row.shortId || undefined}
                    placeholder="Short"
                    options={[
                      ...shorts.map((s) => ({ value: s.id, label: `Short ${s.id}` })),
                      ...shots.map((s) => ({ value: s.id, label: `Shot ${s.id}` })),
                    ]}
                    onChange={(shortId) => patchStill(row.id, { shortId })}
                  />
                ),
              },
              {
                title: 'Khung cảnh',
                width: 140,
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.scene}
                    placeholder="vd. Bàn cơm"
                    onChange={(e) => patchStill(row.id, { scene: e.target.value })}
                  />
                ),
              },
              {
                title: 'Nhân vật',
                width: 160,
                render: (_, row) => {
                  const opts = (state.characters ?? []).map((c) => ({
                    value: c.id,
                    label: `${c.id}${c.name ? ` ${c.name}` : ''}`,
                  }));
                  if (row.charCode && !opts.some((o) => o.value === row.charCode)) {
                    opts.unshift({ value: row.charCode, label: row.charCode });
                  }
                  return (
                    <Select
                      size="small"
                      style={{ width: '100%' }}
                      value={row.charCode || undefined}
                      placeholder="CHAR-001"
                      options={opts}
                      showSearch
                      allowClear
                      onChange={(charCode) => patchStill(row.id, { charCode: charCode ?? '' })}
                    />
                  );
                },
              },
              {
                title: 'Ảnh',
                width: 240,
                render: (_, row) => {
                  const canon = (state.characters ?? []).find((c) => c.id === row.charCode);
                  const src =
                    (row.imageDataUrl?.startsWith('data:image') ? row.imageDataUrl : undefined) ||
                    (row.charCode ? canonDisplayOf(state, row.charCode) : undefined);
                  const fromCanon = characterCanonReady(canon);
                  return (
                    <Space align="start" wrap>
                      {src ? (
                        <img
                          src={src}
                          alt={row.charCode || 'ref'}
                          style={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 8 }}
                        />
                      ) : (
                        <Tag>Chưa có</Tag>
                      )}
                      {fromCanon ? <Tag color="green">Canon</Tag> : null}
                      <Button
                        size="small"
                        disabled={!fromCanon}
                        onClick={() => persistState(applyStillFromCanon(state, row.id))}
                      >
                        Dùng Canon
                      </Button>
                    </Space>
                  );
                },
              },
              {
                title: 'Đường dẫn máy',
                width: 260,
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.localPath ?? ''}
                    placeholder="E:\Famixa\ref\CHAR-001.png"
                    disabled={state.scriptLocked}
                    onChange={(e) =>
                      patchStill(row.id, { localPath: e.target.value, fileName: row.fileName || e.target.value })
                    }
                  />
                ),
              },
              {
                title: 'Ghi chú',
                render: (_, row) => (
                  <Input
                    size="small"
                    value={row.note ?? ''}
                    placeholder="mặt / outfit / cảm xúc"
                    onChange={(e) => patchStill(row.id, { note: e.target.value })}
                  />
                ),
              },
              {
                title: '',
                width: 56,
                render: (_, row) => (
                  <Button
                    type="link"
                    danger
                    size="small"
                    icon={<DeleteOutlined />}
                    onClick={() =>
                      persistState({ ...state, stills: stills.filter((s) => s.id !== row.id) })
                    }
                  />
                ),
              },
            ]}
          />
        </Card>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Nhận pack rồi mới gắn ảnh theo cảnh.
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
              {
                key: 'shortWork',
                label: activeShort ? `Short đang làm · ${activeShort.id}` : 'Short đang làm',
                children: (
                  <>
      {shorts.length > 0 && activeShort && shortRun ? (
        <Card size="small" title={`Short · ${activeShort.id}`} style={{ marginBottom: 16 }}>
          {stillsForShort(stills, activeShort.id).some((s) => s.imageDataUrl) ? (
            <Space wrap style={{ marginBottom: 12 }}>
              {stillsForShort(stills, activeShort.id).map((s) =>
                s.imageDataUrl ? (
                  <div key={s.id} style={{ textAlign: 'center' }}>
                    <img
                      src={s.imageDataUrl}
                      alt={s.charCode}
                      style={{ width: 128, height: 128, objectFit: 'cover', borderRadius: 8 }}
                    />
                    <div style={{ fontSize: 11 }}>{s.charCode || 'CHAR'}</div>
                    {s.localPath ? (
                      <div style={{ fontSize: 10, maxWidth: 128, wordBreak: 'break-all' }}>{s.localPath}</div>
                    ) : null}
                  </div>
                ) : null,
              )}
            </Space>
          ) : (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message="Chưa gắn ảnh nhân vật cho short này — gắn ở bảng trên trước khi Turbo."
            />
          )}
          <Typography.Paragraph>
                <strong>Hook.</strong> {activeShort.hook}
              </Typography.Paragraph>
              <Typography.Paragraph type="secondary">{activeShort.visual}</Typography.Paragraph>
              <Typography.Paragraph code style={{ display: 'block', whiteSpace: 'pre-wrap' }}>
                {activeShort.motionPrompt}
              </Typography.Paragraph>
              <Alert
                type={shortRun.keyframeDataUrl ? 'success' : 'warning'}
                showIcon
                style={{ marginBottom: 12 }}
                message="Keyframe short = khung 9:16 cả cảnh, không phải crop mặt CHAR."
                description={
                  <Space align="start" wrap>
                    {shortRun.keyframeDataUrl ? (
                      <img
                        src={shortRun.keyframeDataUrl}
                        alt="KF short"
                        style={{ width: 90, height: 160, objectFit: 'cover', borderRadius: 8 }}
                      />
                    ) : null}
                    <input
                      type="file"
                      accept="image/*"
                      onChange={(e) => void onKeyframeFile(activeShort.id, e.target.files?.[0])}
                    />
                    <Button
                      loading={stillBusy === activeShort.id}
                      disabled={!keys.gemini}
                      onClick={() => {
                        const sc = (state.scenes ?? []).find((x) => x.id === activeShort.sceneId);
                        void generateKfFromCanon({
                          clipId: activeShort.id,
                          aspect: '9:16',
                          visual: activeShort.visual || activeShort.hook,
                          action: activeShort.motionPromptVi || activeShort.hook,
                          location: activeShort.scene || sc?.environment,
                          characterIds: activeShort.characterIds?.length
                            ? activeShort.characterIds
                            : activeShort.characters,
                        });
                      }}
                    >
                      Tạo KF từ Canon
                    </Button>
                  </Space>
                }
              />
              <Space wrap style={{ marginBottom: 12 }}>
                <Button
                  icon={<CopyOutlined />}
                  onClick={() => {
                    void navigator.clipboard.writeText(
                      compileRunwayPromptV1({ action: activeShort.motionPrompt }).text,
                    );
                    message.success('Đã copy I2V V1');
                  }}
                >
                  Copy prompt
                </Button>
                <Button
                  onClick={() =>
                    checkTurbo(
                      compileRunwayPromptV1({ action: activeShort.motionPrompt }).text,
                      shortRun.keyframeDataUrl,
                    )
                  }
                >
                  Kiểm tra trước (0 cr)
                </Button>
                <Button
                  icon={<ThunderboltOutlined />}
                  disabled={!ready || !shortRun.keyframeDataUrl}
                  loading={turboBusy === activeShort.id}
                  onClick={startShortTurbo}
                >
                  Gửi Turbo dọc · ~{runwayCredits(activeShort.seconds).credits} cr
                </Button>
                <InputNumber
                  min={0}
                  max={999}
                  value={shortRun.credits}
                  onChange={(n) => patchRun(activeShort.id, { credits: n ?? undefined })}
                  placeholder="credit"
                />
              </Space>
              <ClipWatch
                clipId={activeShort.id}
                url={shortRun.previewUrl}
                path={shortRun.localVideoPath}
                sessionSrc={sessionClips[activeShort.id]}
                history={shortRun.takeHistory}
                onUrl={(previewUrl) => patchRun(activeShort.id, { previewUrl })}
                onPath={(localVideoPath) => patchRun(activeShort.id, { localVideoPath })}
                onFile={(file) => onClipFile(activeShort.id, file)}
              />
              {REVIEW_AXES.map((a) => (
                <div key={a.id}>
                  <Checkbox
                    checked={Boolean(shortRun.review?.[a.id])}
                    onChange={(e) =>
                      patchRun(activeShort.id, {
                        status: 'reviewed',
                        review: { ...shortRun.review, [a.id]: e.target.checked },
                      })
                    }
                  >
                    {a.label}
                  </Checkbox>
                </div>
              ))}
              <div style={{ marginTop: 12 }}>
                <Button type="primary" icon={<CheckOutlined />} onClick={lockShort}>
                  Khóa short
                </Button>
              </div>
        </Card>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {shorts.length ? 'Bấm một short ở nhóm Short 9:16.' : 'Pack chưa có short.'}
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
                ]
              : []),
              ...(studioPane === 'advanced'
                ? [
              {
                key: 'long',
                label: active ? `I2V V1 · ${active.id}` : 'I2V V1',
                children: (
                  <>
      <Typography.Paragraph type="secondary">
        Confirm luôn gửi <strong>RUNWAY_PROMPT_V1</strong> (motion + camera). Không dán V02 / negative.
      </Typography.Paragraph>
      {active && run ? (
        <>
                  <Input.TextArea
                    rows={4}
                    value={compileI2vPrompt(state, active, run.shotAction ?? '', videoContext)}
                    readOnly
                    spellCheck={false}
                    style={{ marginTop: 4, marginBottom: 12, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
                  />
                  <Space wrap>
                    <Button
                      icon={<CopyOutlined />}
                      onClick={() => {
                        void navigator.clipboard.writeText(
                          compileI2vPrompt(state, active, run.shotAction ?? '', videoContext),
                        );
                        message.success('Đã copy I2V V1');
                      }}
                    >
                      Copy prompt
                    </Button>
                    <Button
                      onClick={() =>
                        checkTurbo(
                          compileI2vPrompt(state, active, run.shotAction ?? '', videoContext),
                          run.keyframeDataUrl,
                        )
                      }
                    >
                      Kiểm tra I2V (0 cr)
                    </Button>
                    <Button onClick={() => setStudioPane('studio')}>Về Video Studio</Button>
                  </Space>
        </>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Chọn shot trên Video Studio để xem prompt sẽ gửi.
        </Typography.Paragraph>
      )}
                  </>
                ),
              },
                ]
              : []),
            ]}
          />
        ) : null}
      </ContentFamixaStudioView>

      <Drawer
        title="Continuity Memory"
        open={memOpen}
        onClose={() => setMemOpen(false)}
        width={480}
      >
        <Typography.Paragraph>
          Continuity = quy tắc cả cảnh. Shot lock = video cụ thể. Hai thứ khác nhau.
        </Typography.Paragraph>
        <ContinuityMemoryCard
          lock={lockMem}
          prevShot={prevLocked}
          onChange={(continuity) => persistState({ ...state, continuity })}
        />
      </Drawer>
    </div>
  );
}
