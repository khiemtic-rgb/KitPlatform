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
  getContentSeriesTurbo,
  previewContentSeriesTts,
  putContentSeriesPilot,
  fetchContentSeriesBuilds,
  fetchContentSeriesBuild,
  putContentSeriesBuild,
  deleteContentSeriesBuild,
  type ContentSeriesBuildSummary,
  assembleContentSeriesCut,
  startContentSeriesTurbo,
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
  groupShotsByBeat,
  primeLongShotsOnScriptLock,
  applyShotLockToGraph,
  compileI2vPrompt,
  formatSeriesVideoContext,
  pickFamixaBrand,
  lockFromGraph,
  voiceCuesForShot,
  scriptListenCues,
  ensurePilotGraph,
  ensureScriptFollowsVoice,
  PILOT_SCHEMA,
  studioI2vPrecheck,
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
  newRoleRow,
  newStillRow,
  packForShotEdit,
  parseFamixaPack,
  preflightTurboSend,
  RUNWAY_SH02_V02_MOTION,
  RUNWAY_SH02_V02_NEGATIVE,
  RUNWAY_V02_MOTION,
  RUNWAY_V02_NEGATIVE,
  turboI2vPrompt,
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
  shotCharacterIds,
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
  sceneKfToGenerate,
} from './content-famixa-batch-plan';
import { mapPreviewCut, shotsInInclusiveRange } from './content-famixa-preview-cut';
import { productionShorts, setShortSeconds, canWorkV2Scene, v2SceneBlockReason, readyV2VideoShots, isOperatorSuppliedKf, visualLockShot } from './content-famixa-prod-v2';
import {
  assembleFileStem,
  buildAssembleTimeline,
  formatSrt,
  looksLikeVideoUrl,
  planWithExistingTakes,
  takeDownloadName,
} from './content-famixa-assemble';
import { englishI2vRetry } from './content-famixa-i2v-en';
import { blobToBase64, recordAssembledCut, takeBlobFromUrl, triggerDownload } from './content-famixa-assemble-render';
import { deleteTtsScope, loadTtsBlob, loadTtsBlobAny, measureAudioSec, saveTtsBlob, ttsLineKey, ttsTextKey } from './content-famixa-tts-store';
import { applyContinuityChain } from './content-famixa-continuity-chain';
import {
  applyEditDurations,
  continueScenePrompt,
  deriveSceneMaster,
  lockSceneMaster,
  pickShots,
  previousSceneKf,
  sceneIdOfShot,
  sceneMasterOf,
  sequentialKfIds,
  unlockSceneMaster,
  upsertSceneMaster,
} from './content-famixa-scene-first';
import { applyDialogueMap, coverageOf } from './content-famixa-dialogue-map';
import { actingTtsPerformText, actingTtsVoiceSettings, inferActingDirection } from './content-famixa-acting-law';
import { shrinkStillDataUrl } from './content-famixa-still-ref';
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
  if (!t) return 'Runway chưa trả take.';
  if (/moderation|safety|content.?policy/i.test(t)) {
    return 'Runway chặn nội dung (moderation). KIT chỉ gửi English motion + KF cảnh — gửi lại đúng clip này.';
  }
  if (/429|rate.?limit|too many/i.test(t)) {
    return 'Runway giới hạn tốc độ. Đợi rồi bấm Gửi cho Short còn thiếu — take đã có giữ nguyên.';
  }
  if (/insufficient|quota|payment|billing|credits?/i.test(t)) {
    return 'Runway hết credit hoặc Dev chưa thanh toán. Kiểm tra tài khoản, rồi gửi nốt clip thiếu.';
  }
  if (/unexpected error/i.test(t)) {
    return 'Runway lỗi phía họ (unexpected). Không đụng take đã có. Gửi lại đúng Short còn thiếu; nếu lặp lại thì đợi 1 phút.';
  }
  return t.length > 180 ? `${t.slice(0, 180)}…` : t;
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
  const [assembleBusy, setAssembleBusy] = useState(false);
  const [assembleAspect, setAssembleAspect] = useState<'16:9' | '9:16'>('9:16');
  const [stillBusy, setStillBusy] = useState<string | undefined>();
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

  const clearSessionTts = () => {
    ttsUrls.current.forEach((url) => URL.revokeObjectURL(url));
    ttsUrls.current.clear();
    ttsBlobs.current.clear();
    ttsSent.current.clear();
    setTtsFiles({});
  };

  const hydrateSessionTts = async (graph: SeriesPilotState) => {
    const lines = deriveVoiceScript(graph).lines;
    for (const line of lines) {
      if (ttsBlobs.current.has(line.id)) continue;
      const blob =
        (await loadTtsBlob(ttsLineKey(line.id, line.voiceId))) || (await loadTtsBlob(ttsTextKey(line.text, line.voiceId)));
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

  const loadCueAudio = async (cue: { id: string; voiceId?: string; text: string; name: string; characterId?: string }) => {
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
    const dir = inferActingDirection({ text: cue.text, characterId: cue.characterId, name: cue.name });
    const child = isChildVoiceLane(voiceLaneForRole({ characterId: cue.characterId, name: cue.name, title: '' }, ch));
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
    if (studioPane !== 'voice') return;
    let cancelled = false;
    void (async () => {
      const lines = deriveVoiceScript(stateRef.current).lines;
      for (const line of lines) {
        if (cancelled) return;
        if (ttsBlobs.current.has(line.id)) continue;
        const blob =
          (await loadTtsBlob(ttsLineKey(line.id, line.voiceId))) || (await loadTtsBlob(ttsTextKey(line.text, line.voiceId)));
        if (!blob) continue;
        ttsBlobs.current.set(line.id, blob);
        ttsBlobs.current.set(ttsTextKey(line.text, line.voiceId), blob);
        const url = URL.createObjectURL(blob);
        ttsUrls.current.set(line.id, url);
        setTtsFiles((m) => ({ ...m, [line.id]: { url, fileName: `${line.id}.mp3` } }));
      }
    })();
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
      patchRun(id, {
        keyframeDataUrl,
        keyframeFileName: ref.fileName,
        keyframePath: ref.localPath,
        keyframeInheritedFrom: undefined,
        kfApproved: true,
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
  }) => {
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon mặt.');
      return false;
    }
    const st = await hydratePilotCanon(stateRef.current);
    if (st !== stateRef.current) persistState(st);
    const faces: { name: string; role?: string; imageDataUrl: string }[] = [];
    const visualIds = (opts.characterIds ?? []).filter((id) => !/^CHAR-VO$/i.test(id) && !/loi binh|narrator/i.test(id));
    for (const row of canonStillRefs(st, visualIds).slice(0, opts.prevKfUrl ? 3 : 4)) {
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
    const refs = prevUrl
      ? [...faces, { name: 'PREV-SHOT', role: 'scene', imageDataUrl: prevUrl }]
      : faces;
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
    try {
      const res = await generateContentSeriesStill({
        prompt: seriesSceneStillPrompt({
          aspect: opts.aspect,
          visual: looksLikePackHeading(opts.visual) ? '' : opts.visual,
          action: looksLikePackHeading(opts.action) ? '' : opts.action,
          location: opts.location,
          refs,
          continuityNote: opts.continuityNote,
        }),
        aspect: opts.aspect,
        references: refs,
      });
      patchRun(opts.clipId, {
        keyframeDataUrl: res.imageDataUrl,
        keyframeFileName: `kf-${opts.clipId}-canon.png`,
        keyframeInheritedFrom: undefined,
        kfApproved: false,
        kfTechNote: opts.continuityNote?.trim() || undefined,
        status: 'keyframe_ready',
      });
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
      message.success(`Đã vẽ KF cảnh (${res.model}) — duyệt rồi mới I2V. Không dùng crop mặt.`);
      return true;
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không vẽ được KF từ Canon.'));
      return false;
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
    message.success(
      `SHOT GRAPH LOCKED — ${n} Short · thoại ${cov.spoken} · câm ${cov.silent}` +
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
  }) => {
    const keyframe = shortRunOf(stateRef.current, opts.clipId).keyframeDataUrl;
    if (!keyframe) {
      message.warning('Gắn KF cảnh (cả khung hình), đừng gửi ảnh mặt CHAR.');
      return false;
    }
    const imageDataUrl = keyframe;
    setTurboBusy(opts.clipId);
    try {
      let lastText = '';
      for (let attempt = 0; attempt < 2; attempt++) {
        const prompt = attempt === 0 ? opts.prompt : englishI2vRetry(opts.prompt);
        const gate = preflightTurboSend({ prompt, imageDataUrl });
        if (!gate.ok) {
          message.error(`Chưa gửi (0 cr): ${gate.reasons.join(' ')}`);
          return false;
        }
        try {
          const current = shortRunOf(stateRef.current, opts.clipId);
          const takeHistory = [...(current.takeHistory ?? [])];
          if (current.previewUrl?.trim()) {
            takeHistory.unshift({ url: current.previewUrl.trim(), taskId: current.turboTaskId });
          }
          const started = await startContentSeriesTurbo({
            clipId: opts.clipId,
            prompt,
            negativePrompt: opts.negative,
            imageDataUrl,
            seconds: opts.seconds,
            ratio: opts.ratio,
            engine: opts.engine,
          });
          patchRun(opts.clipId, {
            status: 'turbo_testing',
            model: started.model,
            previewUrl: undefined,
            takeHistory: takeHistory.slice(0, 6),
            turboTaskId: started.taskId,
            turboStatus: started.status,
            turboError: undefined,
          });
          if (started.usedPlaceholderImage) {
            message.warning('Không nhận được KF01 — gửi lại sau khi gắn ảnh cảnh.');
          }
          let task = started;
          const t0 = Date.now();
          while (
            task.status !== 'SUCCEEDED' &&
            task.status !== 'FAILED' &&
            task.status !== 'CANCELLED' &&
            Date.now() - t0 < 240_000
          ) {
            await new Promise((r) => setTimeout(r, 4000));
            task = await getContentSeriesTurbo(task.taskId);
            patchRun(opts.clipId, { turboStatus: task.status, turboTaskId: task.taskId });
          }
          if (task.status === 'SUCCEEDED' && task.videoUrl) {
            const wan = opts.engine === 'wan' || (task.model ?? '').startsWith('wan');
            patchRun(opts.clipId, {
              status: 'turbo_testing',
              previewUrl: task.videoUrl,
              runwaySpent: wan ? 0 : runwayCredits(task.seconds || opts.seconds).credits,
              model: task.model,
              turboStatus: task.status,
              turboError: undefined,
            });
            if (!opts.silent) {
              message.success(
                wan
                  ? `Take Wan sẵn. Fal đã trừ ~1 đơn vị 720p — sổ KIT không ghi Runway cr.`
                  : `Take sẵn. Runway đã trừ ~${runwayCredits(task.seconds || opts.seconds).credits} cr — sổ KIT chỉ ghi khi khóa take đạt.`,
              );
            }
            return true;
          }
          lastText = explainTurboError(task.error || `Turbo ${task.status}. Chưa có file.`);
          const transient = /unexpected|429|rate.?limit|too many/i.test(task.error || lastText);
          if (transient && attempt === 0) {
            patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: 'Runway lỗi tạm — gửi lại cùng clip…' });
            await new Promise((r) => setTimeout(r, 8000));
            continue;
          }
          patchRun(opts.clipId, { turboStatus: task.status, turboError: lastText, turboTaskId: task.taskId });
          if (!opts.silent) message.error(lastText);
          return false;
        } catch (e) {
          lastText = explainTurboError(
            apiErrorMessage(e, 'Không gửi được I2V. Kiểm tra key Runway / Fal (Model AI → Video).'),
          );
          const transient = /unexpected|429|rate.?limit|too many|502|503|timeout/i.test(lastText);
          if (transient && attempt === 0) {
            patchRun(opts.clipId, { turboStatus: 'RETRY', turboError: 'Runway lỗi tạm — gửi lại cùng clip…' });
            await new Promise((r) => setTimeout(r, 8000));
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
    }
  };

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
      title: engine === 'wan' ? 'Pre-check đạt · gửi Wan (Fal)' : `Pre-check đạt · gửi Turbo −${cost.credits} cr`,
      content: `${pre.warnings.length ? `${pre.warnings.join(' ')} ` : ''}Check máy: 0 cr. ${cost.label} Họ trừ khi bấm Gửi.`,
      okText: engine === 'wan' ? 'Gửi Wan' : `Gửi (−${cost.credits} cr)`,
      cancelText: 'Không gửi',
      onOk: () =>
        sendTurbo({
          clipId: active.id,
          prompt: pre.prompt,
          negative: undefined,
          seconds: active.seconds,
          ratio: '16:9',
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
    const prompt = turboI2vPrompt(activeShort.motionPrompt);
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
      ...state,
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
          : `Gửi Runway · ${sceneVideo.ready.length} Short · −${sceneBatchCredits} cr · xác nhận`;

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
      st = persistState(applySceneKfReuses(st, pack, plan));
      plan = buildSceneKfPlan(st, pack);
      let todo = sceneKfToGenerate(pack, plan, st);
      if (onlyIds?.length) {
        todo = pack.filter((s) => onlyIds.includes(s.id) && shotHasValidAction(s, shotRunOf(st, s)));
        const lock = visualLockShot(st, pack);
        if (lock && isOperatorSuppliedKf(shotRunOf(st, lock))) {
          todo = todo.filter((s) => s.id !== lock.id);
        }
      } else {
        const seq = new Set(sequentialKfIds(st, pack));
        todo = todo.filter((s) => seq.has(s.id) && shotHasValidAction(s, shotRunOf(st, s)));
      }
      if (todo.length === 0) {
        message.warning(
          onlyIds?.length
            ? 'Chọn Short sau mẫu hình (không chọn lại SH đã gắn ảnh).'
            : `Không có Short nào cần tạo hình (${plan.filter((p) => p.eligible).length} Short hợp lệ).`,
        );
        return;
      }
      modal.confirm({
        title: `Tạo hình tuần tự ${todo.length} Shot`,
        content: `Master → SH01 → SH02 từ KF01 → tiếp. Gemini từng tấm, không song song. Short rỗng không vẽ. Chưa gửi I2V.`,
        okText: 'CONFIRM & GENERATE',
        cancelText: 'Hủy',
        onOk: async () => {
          for (const s of todo) {
            if (!shotHasValidAction(s, shotRunOf(stateRef.current, s))) continue;
            const loc = lockFromGraph(stateRef.current, s);
            const master = sceneMasterOf(stateRef.current, sceneIdOfShot(s));
            const prev = previousSceneKf(stateRef.current, s, pack);
            const runNow = shotRunOf(stateRef.current, s);
            const action = effectiveShotAction(s, runNow);
            if (action && looksLikePackHeading(runNow.shotAction)) patchRun(s.id, { shotAction: action });
            const ok = await generateKfFromCanon({
              clipId: s.id,
              aspect: '16:9',
              visual: continueScenePrompt(master, prev ? shCode(stateRef.current, prev.shot) : undefined, action),
              action,
              location: master.location || loc.environment || s.location,
              characterIds: shotCharacterIds(s).filter((id) => id !== 'CHAR-VO'),
              prevKfUrl: prev?.run.keyframeDataUrl,
              inheritFromId: prev?.shot.id,
            });
            if (!ok) {
              message.error(`Dừng KF tại ${shCode(stateRef.current, s)}.`);
              return;
            }
          }
          persistState(applySceneKfReuses(stateRef.current, pack, buildSceneKfPlan(stateRef.current, pack)));
          message.success('Đã xong KF tuần tự. Duyệt từng tấm — chưa gửi I2V.');
        },
      });
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
      const targets = pack.filter(
        (s) => ids.includes(s.id) && shotHasValidAction(s, shotRunOf(st, s)) && !shotRunOf(st, s).prodSkip,
      );
      if (!targets.length) {
        message.warning('Shot đã chọn đang HOLD/SKIP — không vẽ KF.');
        return;
      }
      modal.confirm({
        title: `Tạo lại ${targets.length} KF đã chọn`,
        content: 'Gemini vẽ lại shot đã chọn. Không trừ Runway. Không gửi I2V.',
        okText: `Vẽ ${targets.length} KF`,
        cancelText: 'Hủy',
        onOk: async () => {
          for (const s of targets) {
            patchRun(s.id, {
              kfForceNew: true,
              keyframeDataUrl: undefined,
              keyframeInheritedFrom: undefined,
              kfTechNote: continuityNote?.trim() || undefined,
            });
            const loc = lockFromGraph(stateRef.current, s);
            const master = sceneMasterOf(stateRef.current, sceneIdOfShot(s));
            const prev = previousSceneKf(stateRef.current, s, pack);
            const runNow = shotRunOf(stateRef.current, s);
            const action = effectiveShotAction(s, runNow);
            if (action && looksLikePackHeading(runNow.shotAction)) patchRun(s.id, { shotAction: action });
            const ok = await generateKfFromCanon({
              clipId: s.id,
              aspect: '16:9',
              visual: continueScenePrompt(master, prev ? shCode(stateRef.current, prev.shot) : undefined, action),
              action,
              location: master.location || loc.environment || s.location,
              characterIds: shotCharacterIds(s).filter((id) => id !== 'CHAR-VO'),
              prevKfUrl: prev?.run.keyframeDataUrl,
              inheritFromId: prev?.shot.id,
              continuityNote,
            });
            if (!ok) {
              message.error(`Dừng KF tại ${shCode(stateRef.current, s)}.`);
              return;
            }
          }
          const cur = stateRef.current;
          persistState(applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)));
          message.success('Đã vẽ lại KF đã chọn. Duyệt Contact Sheet — chưa gửi I2V.');
        },
      });
    })();
  };

  const approveSceneKf = (ids?: string[]) => {
    const gates = Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true]));
    const runs = { ...state.runs };
    let n = 0;
    const range = shotsInInclusiveRange(prodQueue, cutFrom, cutTo);
    const pack = ids?.length ? prodQueue.filter((s) => ids.includes(s.id)) : range;
    for (const s of pack) {
      const run = shotRunOf(state, s);
      if (!run.keyframeDataUrl || run.status === 'approved') continue;
      runs[s.id] = {
        ...run,
        status: run.status === 'turbo_testing' || run.status === 'reviewed' ? run.status : 'keyframe_ready',
        kfApproved: true,
        continuity: run.continuity ?? gates,
      };
      n += 1;
    }
    persistState({ ...state, runs });
    message.success(n ? `Đã duyệt ${n} KF cảnh.` : 'Chưa có KF để duyệt.');
  };

  const startSceneTurbo = (onlyIds?: string[]) => {
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
    const total = ready.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
    const wan = engine === 'wan';
    modal.confirm({
      title: wan
        ? `Tạo video ${ready.length} Short · Wan`
        : `Gửi Runway ${ready.length} Short · ước tính −${total} cr`,
      content: blocked.length
        ? `Chỉ gửi Short đã có KF. Bỏ qua: ${blocked.map((b) => shCode(stateRef.current, b.shot)).join(', ')}. Xác nhận trước khi trừ credit.`
        : `Selected ${ready.length} Short. Mỗi Short = 1 clip 5s hoặc 10s từ KF + Action. Xác nhận để generate.`,
      okText: 'CONFIRM & GENERATE',
      cancelText: 'Hủy',
      onOk: async () => {
        let sent = 0;
        const failed: string[] = [];
        for (const shot of ready) {
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
            action: run.shotAction || shot.story || shot.motionPromptVi,
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
          const ok = await sendTurbo({
            clipId: shot.id,
            prompt: pre.prompt,
            seconds: shot.seconds,
            ratio: '16:9',
            engine,
            silent: true,
          });
          if (!ok) {
            failed.push(shCode(stateRef.current, shot));
            await new Promise((r) => window.setTimeout(r, 8000));
            continue;
          }
          sent += 1;
          await new Promise((r) => window.setTimeout(r, 5000));
        }
        if (sent && failed.length) {
          message.warning(
            `Được ${sent} take. Còn lỗi ${failed.join(', ')} — Gửi Runway nốt phần thiếu, hoặc Gửi lại từng hàng.`,
          );
        } else if (sent) {
          message.success(`Đã gửi ${sent} take. QC từng shot — không tự Final.`);
        } else {
          message.warning('Không gửi clip nào (0 cr). Sửa prompt/KF rồi CONFIRM lại.');
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

  const resolveLineAudio = async (line: { id: string; text: string; voiceId?: string }) => {
    const hit = ttsBlobs.current.get(line.id);
    if (hit) {
      rememberLineAudio(line.id, hit);
      return hit;
    }
    const same = (a: string, b: string) => a.replace(/\s+/g, ' ').trim() === b.replace(/\s+/g, ' ').trim();
    const vid = line.voiceId;
    const alts = [ttsLineKey(line.id, vid), ttsTextKey(line.text, vid)];
    for (const g of stateRef.current.voicePreview?.generated ?? []) {
      if (g.id !== line.id && same(g.text, line.text)) alts.push(ttsLineKey(g.id, vid));
    }
    const blob = await loadTtsBlobAny(alts);
    if (!blob) return undefined;
    rememberLineAudio(line.id, blob);
    rememberLineAudio(ttsTextKey(line.text, vid), blob);
    return blob;
  };

  const cutPlan = mapPreviewCut(state, cutRange, {
    hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
  });

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

  const downloadTakes = async (ids?: string[]) => {
    const pack = productionShorts(stateRef.current);
    const range = ids?.length ? pack.filter((s) => ids.includes(s.id)) : cutRange;
    const ready = range.filter((s) => shotRunOf(stateRef.current, s).previewUrl?.trim());
    if (!ready.length) {
      message.warning('Chưa có take trên dải đã chọn. Gửi Runway ở bước 5 Video trước.');
      return;
    }
    setAssembleBusy(true);
    try {
        let saved = 0;
        for (let i = 0; i < ready.length; i++) {
          const s = ready[i]!;
          const url = shotRunOf(stateRef.current, s).previewUrl!.trim();
          message.loading({ content: `Tải ${studioShotCode(s, pack)} (${i + 1}/${ready.length})`, key: 'take-dl', duration: 0 });
          try {
            const blob = await takeBlobFromUrl(url);
            triggerDownload(blob, takeDownloadName(s, pack));
            saved += 1;
          } catch (e) {
            window.open(url, '_blank', 'noopener,noreferrer');
            message.warning(
              `${studioShotCode(s, pack)}: ${e instanceof Error ? e.message : 'Không tải qua API'} — đã mở link Runway, lưu MP4 từ tab đó.`,
            );
          }
          await new Promise((r) => window.setTimeout(r, 400));
        }
        message.success({
          content: saved === ready.length ? `Đã tải ${saved} take.` : `Tải ${saved}/${ready.length} take. Phần còn lại mở tab Runway.`,
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

  const assembleCut = async (opts?: { resynth?: boolean }) => {
    persistState(applyContinuityChain(stateRef.current, productionShorts(stateRef.current)));
    if ((stateRef.current.episode?.shots ?? []).some((s) => !Array.isArray(s.dialogueSegmentIds))) {
      persistState(applyDialogueMap(stateRef.current));
    }
    await hydrateCutVoice();
    const readyPlan = planWithExistingTakes(cutPlan);
    if (!readyPlan.items.length) {
      message.warning('Chưa có take trên dải. Gửi Runway rồi ghép — không tạo ảnh mới chỉ để test tiếng.');
      return;
    }
    const script = deriveVoiceScript(stateRef.current);
    const cov = coverageOf(stateRef.current, cutRange, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id)),
    });
    if (cov.extraUnmapped.length) {
      const sample = cov.extraUnmapped[0];
      message.error(
        `Chưa ghép — ${cov.extraUnmapped.length} câu chưa gắn Short (vd. ${sample?.name}: “${(sample?.text ?? '').slice(0, 48)}”). Duyệt map ở bước Shorts — không đoán file.`,
      );
      return;
    }
    const spoken = readyPlan.items.flatMap((i) => (i.lines.length ? i.lines : i.line ? [i.line] : []));
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
        onOk: () => void assembleCut({ resynth: true }),
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
      const blob =
        ttsBlobs.current.get(line.id) ||
        ttsBlobs.current.get(ttsTextKey(line.text, line.voiceId)) ||
        (await loadTtsBlob(ttsLineKey(line.id, line.voiceId))) ||
        (await loadTtsBlob(ttsTextKey(line.text, line.voiceId)));
      if (!blob) continue;
      const sec = (await measureAudioSec(blob)) || assets[line.id]?.duration || 0;
      if (sec > 0.2) measured[line.id] = Number(sec.toFixed(2));
    }
    const tl = buildAssembleTimeline(readyPlan, {
      hasVoiceFile: (id) => Boolean(ttsFiles[id] || ttsBlobs.current.has(id) || measured[id]),
      voiceSecOf: (id) => measured[id] || assets[id]?.duration,
      fit: 'speech',
    });
    const stem = assembleFileStem(readyPlan, ep?.episode, ep?.title);
    triggerDownload(new Blob([formatSrt(tl.cues)], { type: 'text/plain;charset=utf-8' }), `${stem}.srt`);
    setAssembleBusy(true);
    try {
      message.loading({ content: 'FFmpeg: normalize + mix thoại → một MP4…', key: 'assemble', duration: 0 });
      const pack = productionShorts(stateRef.current);
      const clips = [];
      for (const clip of tl.clips) {
        const shot = pack.find((s) => s.id === clip.shotId);
        const videoUrl = shot ? shotRunOf(stateRef.current, shot).previewUrl?.trim() : '';
        if (!videoUrl) throw new Error(`Thiếu link take ${clip.code}.`);
        const voices = [];
        for (const cue of clip.cues) {
          const vid = script.lines.find((l) => l.id === cue.lineId)?.voiceId;
          const blob =
            ttsBlobs.current.get(cue.lineId) ||
            ttsBlobs.current.get(ttsTextKey(cue.text, vid)) ||
            (await loadTtsBlob(ttsLineKey(cue.lineId, vid))) ||
            (await loadTtsBlob(ttsTextKey(cue.text, vid)));
          if (!blob) throw new Error(`Thiếu TTS ${cue.code}: “${cue.text.slice(0, 40)}”.`);
          voices.push({
            lineId: cue.lineId,
            startSec: Math.max(0, cue.startSec - clip.startSec),
            audioBase64: await blobToBase64(blob),
            mime: blob.type || 'audio/mpeg',
          });
        }
        clips.push({ code: clip.code, videoUrl, seconds: clip.seconds, voices });
      }
      const voiceCount = clips.reduce((n, c) => n + c.voices.length, 0);
      if (spoken.length && voiceCount === 0) {
        throw new Error('Không gửi được file thoại lên API. Không tải file câm.');
      }
      try {
        const mp4 = await assembleContentSeriesCut({ fileStem: stem, aspect: assembleAspect, clips });
        triggerDownload(mp4, `${stem}.mp4`);
        message.success({
          content: `Đã ghép ${tl.clips.length} Short + ${voiceCount} câu thoại → ${stem}.mp4`,
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
          const url = shot ? shotRunOf(stateRef.current, shot).previewUrl?.trim() : '';
          if (!url) throw new Error('Thiếu link take.');
          return takeBlobFromUrl(url);
        },
        audioOf: async (lineId) => ttsBlobs.current.get(lineId) || loadTtsBlob(lineId),
        onProgress: (msg) => message.loading({ content: `Ghép ${msg}`, key: 'assemble', duration: 0 }),
      });
      triggerDownload(blob, `${stem}.webm`);
      message.success({ content: `Đã ghép ${tl.clips.length} clip (WebM) + SRT.`, key: 'assemble' });
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
        turboBusy={Boolean(turboBusy)}
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
        actionOf={(s) => shotRunOf(state, s).shotAction}
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
          const loc = lockFromGraph(stateRef.current, active);
          void generateKfFromCanon({
            clipId: active.id,
            aspect: '16:9',
            visual: active.visual || active.story || '',
            action: shotRunOf(stateRef.current, active).shotAction || active.motionPromptVi,
            location: loc.environment || active.location,
            characterIds: shotCharacterIds(active),
          });
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
        sceneBatchLabel={sceneBatchLabel}
        sceneBatchDisabled={
          !hasVideoKey ||
          sceneVideo.ready.length === 0 ||
          !canWorkV2Scene(state) ||
          Boolean(state.sceneLocked) ||
          Boolean(turboBusy)
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
        onRegenerateSelectedKf={regenerateSelectedKf}
        generateSceneKfBusy={Boolean(stillBusy)}
        sceneVideoReady={sceneVideo.ready.length}
        sceneVideoBlocked={sceneVideo.blocked.length}
        sceneBatchCredits={sceneBatchCredits}
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
          persistState(locked ? lockSceneMaster(state, sceneId) : unlockSceneMaster(state, sceneId));
        }}
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
        onAssembleAspect={setAssembleAspect}
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
            actionOf={(s) => shotRunOf(state, s).shotAction}
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
                        {kids.map((s, si) => (
                          <div key={s.id} style={{ marginLeft: 16, color: '#334155' }}>
                            {si === kids.length - 1 ? '└─' : '├─'} {s.shot} — {s.story}
                          </div>
                        ))}
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
          <div style={{ maxHeight: 160, overflow: 'auto', marginBottom: 8 }}>
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
                  <Tag style={{ marginLeft: 6 }}>
                    {inferActingDirection({ text: cue.text, characterId: cue.characterId, name: cue.name }).label}
                  </Tag>
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
                    void navigator.clipboard.writeText(activeShort.motionPrompt);
                    message.success('Đã copy prompt');
                  }}
                >
                  Copy prompt
                </Button>
                <Button
                  onClick={() =>
                    checkTurbo(turboI2vPrompt(activeShort.motionPrompt), shortRun.keyframeDataUrl)
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
                label: active ? `Prompt máy · ${active.id}` : 'Prompt máy (I2V)',
                children: (
                  <>
      <Typography.Paragraph type="secondary">
        <strong>Video Studio</strong> luôn gửi prompt từ Memory + Shot Action. Ô dưới chỉ để copy / check tay —
        không đổi đường gửi khi bấm Tạo video.
      </Typography.Paragraph>
      {active && run ? (
        <>
                  <Typography.Text strong>RUNWAY MOTION PROMPT</Typography.Text>
                  <Input.TextArea
                    rows={10}
                    value={run.runwayMotion ?? ''}
                    onChange={(e) => patchRun(active.id, { runwayMotion: e.target.value })}
                    spellCheck={false}
                    placeholder="Dán motion, hoặc bấm Rút gọn an toàn (V02) nếu check báo dài / tuổi trẻ."
                    style={{ marginTop: 4, marginBottom: 12, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
                  />
                  <Typography.Text strong>NEGATIVE PROMPT</Typography.Text>
                  <Input.TextArea
                    rows={8}
                    value={run.runwayNegative ?? ''}
                    onChange={(e) => patchRun(active.id, { runwayNegative: e.target.value })}
                    spellCheck={false}
                    placeholder="Xóa rồi dán nguyên khối negative (No looking at camera…)"
                    style={{ marginTop: 4, marginBottom: 12, fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 }}
                  />
                  <Space wrap>
                    <Button
                      onClick={() => {
                        patchRun(active.id, {
                          runwayMotion: RUNWAY_SH02_V02_MOTION,
                          runwayNegative: RUNWAY_SH02_V02_NEGATIVE,
                        });
                        message.success('Đã dán prompt SH02 V02.');
                      }}
                    >
                      Prompt SH02 V02
                    </Button>
                    <Button
                      onClick={() => {
                        patchRun(active.id, {
                          runwayMotion: RUNWAY_V02_MOTION,
                          runwayNegative: RUNWAY_V02_NEGATIVE,
                        });
                        message.success('Đã rút gọn — Nam nói, Minh nghe.');
                      }}
                    >
                      Prompt Nam nói
                    </Button>
                    <Button
                      icon={<CopyOutlined />}
                      onClick={() => {
                        const motion = (run.runwayMotion ?? '').trim();
                        const neg = (run.runwayNegative ?? '').trim();
                        const text = neg ? `${motion}\n\nNEGATIVE:\n${neg}` : motion;
                        void navigator.clipboard.writeText(text || turboI2vPrompt(motion, neg));
                        message.success('Đã copy Motion (+ Negative nếu có)');
                      }}
                    >
                      Copy prompt
                    </Button>
                    <Button
                      onClick={() =>
                        checkTurbo(
                          active ? compileI2vPrompt(state, active, run.shotAction ?? '', videoContext) : '',
                          run.keyframeDataUrl,
                        )
                      }
                    >
                      Kiểm tra prompt Studio (0 cr)
                    </Button>
                    <Button
                      onClick={() =>
                        checkTurbo(turboI2vPrompt(run.runwayMotion, run.runwayNegative), run.keyframeDataUrl)
                      }
                    >
                      Kiểm tra prompt máy (0 cr)
                    </Button>
                    <Button onClick={() => setStudioPane('studio')}>Về Video Studio</Button>
                  </Space>
        </>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Chọn shot trên Video Studio rồi quay lại đây nếu cần sửa prompt máy.
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
