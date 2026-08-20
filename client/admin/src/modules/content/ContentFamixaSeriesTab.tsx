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
  SoundOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import goldenEp01Story from './content-famixa-ep01-golden.txt?raw';
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
  bindShotToMemory,
  bindShotToSceneKeyframe,
  primeLongShotsOnScriptLock,
  shotActionFromPack,
  applyShotLockToGraph,
  compileI2vPrompt,
  formatSeriesVideoContext,
  pickFamixaBrand,
  lockFromGraph,
  voiceCuesForShot,
  scriptListenCues,
  ensurePilotGraph,
  PILOT_SCHEMA,
  studioI2vPrecheck,
  studioShotCode,
  appendSceneShot,
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
  loadSeriesPilot,
  lockCast,
  localFileRef,
  mergeRemotePilot,
  mergeStills,
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
  previousApproved,
  reviewComplete,
  rolesReady,
  applyStillFromCanon,
  characterOfRole,
  characterCanonReady,
  canonDisplayOf,
  canonImageOf,
  canonStillRefs,
  hydratePilotCanon,
  seriesSceneStillPrompt,
  shotCharacterIds,
  roleCanonReady,
  roleVoiceReady,
  voiceLaneForRole,
  voicesForLane,
  setCharacterCanon,
  setCharacterVoice,
  seriesCanonHint,
  saveSeriesPilot,
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
  readySceneVideoShots,
  sceneCodeOfShot,
  sceneKfToGenerate,
  shotsInScene,
} from './content-famixa-batch-plan';
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

function persist(next: SeriesPilotState, setState: (s: SeriesPilotState) => void) {
  const graph = ensurePilotGraph({ ...next, schemaVersion: PILOT_SCHEMA });
  saveSeriesPilot(graph);
  const cloned: SeriesPilotState = {
    ...graph,
    roles: [...graph.roles],
    runs: { ...graph.runs },
    stills: [...(graph.stills ?? [])],
    episode: graph.episode ? { ...graph.episode, shots: [...graph.episode.shots] } : undefined,
    continuity: graph.continuity ? { ...graph.continuity } : undefined,
    characters: [...(graph.characters ?? [])],
    scenes: [...(graph.scenes ?? [])],
    lines: [...(graph.lines ?? [])],
    shorts: [...(graph.shorts ?? [])],
    storyMemory: graph.storyMemory ? { ...graph.storyMemory } : undefined,
    voicePreview: graph.voicePreview ? { ...graph.voicePreview } : undefined,
  };
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
  if (/moderation/i.test(t)) {
    return 'Runway chặn prompt (moderation). KIT không gửi cả giấy Story — chỉ dòng chuyển động bàn ăn. Gửi Turbo lại.';
  }
  if (/unexpected error/i.test(t)) {
    return 'Runway từ chối take (lỗi phía họ). Thường do tài khoản Dev chưa xong onboarding, hết credit, hoặc KF01 quá đơn giản. Gắn ảnh cả cảnh bàn ăn rồi gửi lại.';
  }
  return t;
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
  const href = (url ?? '').trim();
  if (!href) return '';
  if (/^https?:\/\//i.test(href) && /\.(mp4|webm|ogg)(\?|#|$)/i.test(href)) return href;
  return '';
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
  const [state, setState] = useState<SeriesPilotState>(() => loadSeriesPilot());
  const [packText, setPackText] = useState('');
  const [activeId, setActiveId] = useState<string | undefined>();
  const [activeShortId, setActiveShortId] = useState<string | undefined>(
    () => loadSeriesPilot().shorts?.[0]?.id,
  );
  const [stillOnlyActive, setStillOnlyActive] = useState(true);
  const [packResult, setPackResult] = useState<{ ok: boolean; text: string } | null>(null);
  const [sessionClips, setSessionClips] = useState<Record<string, string>>({});
  const [turboBusy, setTurboBusy] = useState<string | undefined>();
  const [stillBusy, setStillBusy] = useState<string | undefined>();
  const [memOpen, setMemOpen] = useState(false);
  const [studioPane, setStudioPane] = useState<'script' | 'shorts' | 'studio' | 'timeline' | 'advanced'>(() => {
    const s = loadSeriesPilot();
    if (!canOpenStudio(s)) return 'script';
    if ((s.shorts?.length ?? 0) > 0 && !canWorkScene(s)) return 'shorts';
    return 'studio';
  });
  const [engine, setEngine] = useState<'turbo' | 'wan'>(loadEngine);
  const [voiceProvider, setVoiceProvider] = useState<'elevenlabs' | 'f5'>(loadVoice);
  const [keys, setKeys] = useState({ runway: false, fal: false, elevenLabs: false, gemini: false });
  const [voices, setVoices] = useState<ContentSeriesVoice[]>([]);
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
  const [ttsFull, setTtsFull] = useState<{ url: string; fileName: string }>();
  const serverSaveTimer = useRef<ReturnType<typeof setTimeout>>();
  const queueServerSave = (next: SeriesPilotState) => {
    if (serverSaveTimer.current) clearTimeout(serverSaveTimer.current);
    serverSaveTimer.current = setTimeout(() => {
      void putContentSeriesPilot({
        seriesCode: FAMIXA_SERIES_CODE,
        graph: slimPilotForStorage(next) as unknown as Record<string, unknown>,
      }).catch(() => undefined);
    }, 800);
  };
  const persistState = (next: SeriesPilotState) => {
    const cloned = persist(next, setState);
    stateRef.current = cloned;
    queueServerSave(cloned);
    return cloned;
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
    const key = `v3-vi|${voiceId}|${cue.text}|${ch?.voiceStability ?? 0.5}|${ch?.voiceSpeed ?? 1}`;
    const hit = ttsUrls.current.get(key);
    if (hit) {
      ttsSent.current.set(cue.id, cue.text);
      return hit;
    }
    const blob = await previewContentSeriesTts({
      voiceId,
      text: cue.text,
      publicOwnerId: voices.find((v) => v.voiceId === voiceId)?.publicOwnerId || undefined,
      voiceName: voices.find((v) => v.voiceId === voiceId)?.name || ch?.voiceName,
      stability: ch?.voiceStability,
      similarityBoost: ch?.voiceSimilarity,
      style: ch?.voiceStyle,
      speed: ch?.voiceSpeed,
    });
    const url = URL.createObjectURL(blob);
    ttsUrls.current.set(key, url);
    ttsBlobs.current.set(key, blob);
    ttsBlobs.current.set(cue.id, blob);
    ttsSent.current.set(cue.id, cue.text);
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

  const saveTtsFile = (cueId: string) => {
    const file = ttsFiles[cueId];
    if (!file) return;
    const a = document.createElement('a');
    a.href = file.url;
    a.download = file.fileName;
    a.rel = 'noopener';
    a.click();
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
    if (ready.length === 0) {
      message.warning('Gán Voice Canon cho từng vai rồi mới Full Voice.');
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
    const rows = voicesForLane(voices, lane, selectedId);
    return {
      lane,
      options: rows.map((v) => ({
        value: v.voiceId,
        label: v.cloned ? `${v.name} · clone (gói chưa TTS được)` : v.name,
      })),
    };
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
    void fetchContentSeriesVoices()
      .then((rows) => setVoices(rows ?? []))
      .catch(() => undefined);
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
        if (!remote || !hasSeriesGraph(remote as SeriesPilotState)) {
          if (hasSeriesGraph(local)) queueServerSave(local);
        } else {
          persistState(mergeRemotePilot(remote, local));
        }
      } catch {
        /* keep localStorage */
      }
      if (cancelled) return;
      const hydrated = await hydratePilotCanon(stateRef.current);
      if (!cancelled && hydrated !== stateRef.current) persistState(hydrated);
    })();
    return () => {
      cancelled = true;
    };
    // hydrate once
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
      });
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
  }) => {
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon mặt.');
      return false;
    }
    const st = await hydratePilotCanon(stateRef.current);
    if (st !== stateRef.current) persistState(st);
    const refs = canonStillRefs(st, opts.characterIds);
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
          visual: opts.visual,
          action: opts.action,
          location: opts.location,
          refs,
        }),
        aspect: opts.aspect,
        references: refs,
      });
      patchRun(opts.clipId, {
        keyframeDataUrl: res.imageDataUrl,
        keyframeFileName: `kf-${opts.clipId}-canon.png`,
        keyframeInheritedFrom: undefined,
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

  const addShot = () => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trên Timeline trước khi thêm shot.');
      return;
    }
    const next = appendSceneShot(state, active?.scene);
    persistState(bindShotToMemory(next.state, next.shot));
    setActiveId(next.shot.id);
    setStudioPane('studio');
    message.success(`Đã thêm ${studioShotCode(next.shot)} trên ${next.shot.scene}.`);
  };

  const selectShot = (shot: FamixaSeriesShot) => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    if (!canProduceShot(state, shot)) {
      message.warning('Khóa shot liền trước đã.');
      return;
    }
    const bound = bindShotToMemory(state, shot);
    if (bound !== state) persistState(bound);
    setActiveId(shot.id);
  };

  const markKeyframe = () => {
    if (!ready || !active) return;
    if (!previousApproved(state, active.id)) {
      message.warning('Khóa shot trước đã.');
      return;
    }
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
      message.warning('Gắn KF01 (ảnh cả cảnh bàn ăn), đừng gửi ảnh mặt CHAR.');
      return false;
    }
    const imageDataUrl = keyframe;
    const gate = preflightTurboSend({ prompt: opts.prompt, imageDataUrl });
    if (!gate.ok) {
      message.error(`Chưa gửi (0 cr): ${gate.reasons.join(' ')}`);
      return false;
    }
    setTurboBusy(opts.clipId);
    try {
      const current = shortRunOf(stateRef.current, opts.clipId);
      const takeHistory = [...(current.takeHistory ?? [])];
      if (current.previewUrl?.trim()) {
        takeHistory.unshift({ url: current.previewUrl.trim(), taskId: current.turboTaskId });
      }
      const started = await startContentSeriesTurbo({
        clipId: opts.clipId,
        prompt: opts.prompt,
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
      const text = explainTurboError(task.error || `Turbo ${task.status}. Chưa có file.`);
      patchRun(opts.clipId, { turboStatus: task.status, turboError: text, turboTaskId: task.taskId });
      message.error(text);
      return false;
    } catch (e) {
      const text = explainTurboError(
        apiErrorMessage(e, 'Không gửi được I2V. Kiểm tra key Runway / Fal (Model AI → Video).'),
      );
      patchRun(opts.clipId, { turboError: text, turboStatus: 'FAILED' });
      message.error(text);
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
    if (!active || !run) return;
    if (!reviewComplete(run)) {
      message.warning('Tick đủ 4 câu review.');
      return;
    }
    const lockedRun = {
      ...run,
      status: 'approved' as const,
      credits: run.runwaySpent ?? run.credits ?? runwayCredits(active.seconds).credits,
    };
    let nextState: SeriesPilotState = applyShotLockToGraph(
      {
        ...state,
        runs: {
          ...state.runs,
          [active.id]: lockedRun,
        },
      },
      active,
      lockedRun.shotAction ?? run.shotAction,
    );
    const i = shots.findIndex((s) => s.id === active.id);
    const next = shots[i + 1];
    if (next) nextState = bindShotToMemory(nextState, next);
    persistState(nextState);
    if (next) {
      setActiveId(next.id);
      message.success(`Đã khóa ${studioShotCode(active)}. Shot sau kế thừa Memory + KF cảnh.`);
    } else {
      setStudioPane('timeline');
      message.success(`Đã khóa ${studioShotCode(active)}. Timeline · Final — ghi chú ghép rồi khóa cảnh.`);
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
    setStudioPane((state.shorts?.length ?? 0) > 0 ? 'shorts' : 'studio');
    message.success(
      (state.shorts?.length ?? 0) > 0
        ? 'Đã khóa kịch bản. Khóa Full Voice COMPLETE trước khi tốn credit short/I2V.'
        : 'Đã khóa kịch bản. Khóa Full Voice COMPLETE trước khi tốn credit I2V.',
    );
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
      voiceLocked: true,
      voicePreview: {
        ...(state.voicePreview ?? emptyVoicePreview(script)),
        status: 'complete',
        issues: [],
        operatorConfirmed: Boolean(opts?.skipRegen) || Boolean(state.voicePreview?.operatorConfirmed),
      },
    });
    setStudioPane((state.shorts?.length ?? 0) > 0 ? 'shorts' : 'studio');
    message.success(
      (state.shorts?.length ?? 0) > 0
        ? 'Đã khóa Full Voice. Làm short 9:16 rồi mới I2V cảnh.'
        : 'Đã khóa Full Voice. Vào Dựng cảnh 16:9.',
    );
  };

  const sceneShots = shotsInScene(shots, active?.scene || active?.sceneId);
  const sceneKfPlan = buildSceneKfPlan(state, sceneShots);
  const sceneVideo = readySceneVideoShots(state, sceneShots, lockFromGraph(state, active));
  const sceneKfNew = sceneKfToGenerate(sceneShots, sceneKfPlan, state);
  const sceneKfReuse = sceneKfPlan.filter((p) => p.mode !== 'new').length;
  const scenePlaceHint =
    active && lockFromGraph(state, active).locked
      ? continuityPlaceHint(lockFromGraph(state, active), active, shotRunOf(state, active).shotAction)
      : undefined;
  const sceneBatchCredits = sceneVideo.ready.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
  const sceneBatchLabel =
    sceneVideo.ready.length === 0
      ? sceneShots.length === 0
        ? 'Chưa có shot cảnh'
        : `${sceneVideo.blocked.length} shot chưa sẵn sàng`
      : engine === 'wan'
        ? `${sceneVideo.ready.length}/${sceneShots.length} shot · Wan`
        : `${sceneVideo.ready.length}/${sceneShots.length} shot · −${sceneBatchCredits} cr`;

  const generateSceneKf = () => {
    if (!canOpenStudio(state)) {
      message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản rồi mới dựng cảnh.');
      return;
    }
    if (!keys.gemini) {
      message.warning('Cần Gemini API key (Cấu hình AI) để vẽ KF từ Canon.');
      return;
    }
    void (async () => {
      let st = await hydratePilotCanon(stateRef.current);
      if (st !== stateRef.current) st = persistState(st);
      const pack = shotsInScene(episodeShots(st), active?.scene || active?.sceneId);
      let plan = buildSceneKfPlan(st, pack);
      st = persistState(applySceneKfReuses(st, pack, plan));
      plan = buildSceneKfPlan(st, pack);
      const todo = sceneKfToGenerate(pack, plan, st);
      if (todo.length === 0) {
        message.success(
          `KF cảnh: ${plan.filter((p) => p.mode !== 'new').length} reuse · 0 Gemini. Duyệt Contact Sheet rồi tạo video.`,
        );
        return;
      }
      modal.confirm({
        title: `Tạo KF ${sceneCodeOfShot(pack[0]!) || 'cảnh'} · ${todo.length} mới · ${plan.length - todo.length} reuse`,
        content:
          'Chỉ vẽ shot cần KF mới từ Canon. Reuse copy ảnh đã có — không gửi lưới 12 ô vào I2V. Quota Gemini, không trừ Runway.',
        okText: `Vẽ ${todo.length} KF`,
        cancelText: 'Hủy',
        onOk: async () => {
          for (const s of todo) {
            const loc = lockFromGraph(stateRef.current, s);
            const ok = await generateKfFromCanon({
              clipId: s.id,
              aspect: '16:9',
              visual: s.visual || s.story || '',
              action: shotRunOf(stateRef.current, s).shotAction || s.motionPromptVi,
              location: loc.environment || s.location,
              characterIds: shotCharacterIds(s),
            });
            if (!ok) {
              message.error(`Dừng KF tại ${studioShotCode(s)}.`);
              return;
            }
            const cur = stateRef.current;
            persistState(
              applySceneKfReuses(cur, pack, buildSceneKfPlan(cur, pack)),
            );
          }
          message.success('Đã xong KF cảnh. Duyệt Contact Sheet — chưa gửi I2V.');
        },
      });
    })();
  };

  const approveSceneKf = () => {
    const gates = Object.fromEntries(CONTINUITY_GATES.map((g) => [g.id, true]));
    const runs = { ...state.runs };
    let n = 0;
    for (const s of sceneShots) {
      const run = shotRunOf(state, s);
      if (!run.keyframeDataUrl || run.status === 'approved') continue;
      runs[s.id] = {
        ...run,
        status: run.status === 'turbo_testing' || run.status === 'reviewed' ? run.status : 'keyframe_ready',
        continuity: run.continuity ?? gates,
      };
      n += 1;
    }
    persistState({ ...state, runs });
    message.success(n ? `Đã duyệt ${n} KF cảnh.` : 'Chưa có KF để duyệt.');
  };

  const startSceneTurbo = () => {
    if (!canWorkScene(state)) {
      message.warning(sceneBlockReason(state) ?? 'Chưa mở dựng cảnh.');
      setStudioPane(studioFallbackPane(state));
      return;
    }
    if (state.sceneLocked) {
      message.warning('Scene đã Final. Mở khóa cảnh trên Timeline trước khi tạo hết shot.');
      return;
    }
    const pack = shotsInScene(episodeShots(stateRef.current), active?.scene || active?.sceneId);
    const lock = lockFromGraph(stateRef.current, active);
    const { ready, blocked } = readySceneVideoShots(stateRef.current, pack, lock);
    if (ready.length === 0) {
      message.warning(
        blocked.length
          ? `Chưa có shot sẵn sàng. Còn ${blocked.length} shot thiếu KF duyệt hoặc Action.`
          : 'Không còn shot cảnh cần I2V.',
      );
      return;
    }
    const total = ready.reduce((n, s) => n + generateCost(engine, s.seconds).credits, 0);
    const wan = engine === 'wan';
    modal.confirm({
      title: wan
        ? `${ready.length}/${pack.length} shot sẵn sàng · Wan`
        : `${ready.length}/${pack.length} shot sẵn sàng · −${total} cr`,
      content: blocked.length
        ? `${blocked.map((s) => studioShotCode(s)).join(', ')} chưa gửi (thiếu KF duyệt / Action / Continuity). Không gửi ngầm. Dừng nếu một shot fail. Không tự QC / khóa.`
        : 'Chỉ shot KF đã duyệt. Gửi tuần tự, dừng nếu fail. Không tự QC / khóa shot / Final.',
      okText: wan ? `Gửi ${ready.length} Wan` : `Gửi ${ready.length} shot (−${total} cr)`,
      cancelText: 'Không gửi',
      onOk: async () => {
        for (const shot of ready) {
          setActiveId(shot.id);
          const after = stateRef.current;
          const run = shotRunOf(after, shot);
          const pre = studioI2vPrecheck({
            lock: lockFromGraph(after, shot),
            action: run.shotAction,
            keyframeDataUrl: run.keyframeDataUrl,
            status: run.status,
            unlocked: true,
            sceneLocked: after.sceneLocked,
            scriptLocked: after.scriptLocked,
            shortsReady: canWorkScene(after),
            engine,
            hasEngineKey: engine === 'wan' ? keys.fal : keys.runway,
            state: after,
            shot,
            videoContext,
          });
          if (!pre.ok) {
            message.error(
              `Bỏ ${studioShotCode(shot)} (0 cr): ${pre.items
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
            message.error(`Dừng tại ${studioShotCode(shot)}. QC và khóa shot vẫn làm tay.`);
            return;
          }
        }
        message.success(`Đã tạo take. QC từng shot rồi khóa — không tự Final.`);
      },
    });
  };

  const lockMem = lockFromGraph(state, active);
  const prevLocked = previousLockedShot(state, active);
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
        onAddShot={addShot}
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
          sceneVideo.ready.length === 0 ||
          !canWorkScene(state) ||
          Boolean(state.sceneLocked) ||
          Boolean(turboBusy)
        }
        batchPlan={sceneKfPlan}
        batchSceneShots={sceneShots}
        batchKfNew={sceneKfNew.length}
        batchKfReuse={sceneKfReuse}
        onGenerateSceneKf={generateSceneKf}
        onApproveSceneKf={approveSceneKf}
        generateSceneKfBusy={Boolean(stillBusy)}
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
        onOpenShorts={() => {
          if ((state.shorts?.length ?? 0) === 0) {
            message.info('Pack không có short 9:16 — bỏ qua, vào Dựng cảnh.');
            if (canOpenStudio(state)) setStudioPane('studio');
            else setStudioPane('script');
            return;
          }
          if (!canWorkShorts(state)) {
            message.warning(
              !voiceProductionReady(state)
                ? 'Khóa Full Voice (thoại COMPLETE) trước khi tốn credit hình/video.'
                : 'Khóa kịch bản ở bước 1 rồi mới làm short.',
            );
            setStudioPane('script');
            return;
          }
          setStudioPane('shorts');
        }}
        onOpenStudio={() => {
          if (!canOpenStudio(state)) {
            message.warning(sceneBlockReason(state) ?? 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.');
            setStudioPane('script');
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
            onAddShot={addShot}
          />
        ) : null}
        {studioPane === 'script' || studioPane === 'shorts' || studioPane === 'advanced' ? (
          <Collapse
            defaultActiveKey={
              studioPane === 'script' ? ['pack', 'cast'] : studioPane === 'shorts' ? ['shorts', 'stills'] : ['long']
            }
            items={[
              ...(studioPane === 'script'
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
          <Button onClick={() => setPackText(goldenEp01Story.trim())}>Kịch bản EP01 đã gửi</Button>
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
          <Card size="small" title="Parsed Story — duyệt cảnh trước Shot" style={{ marginTop: 12 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {[ep?.episode, ep?.title].filter(Boolean).join(' · ') || 'EP'} — mỗi cảnh chỉ giữ thoại/action của chính nó. Continuity kế thừa, Story không copy ngang.
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
              const sceneLines = linesForScene(state, sc.id);
              return (
                <div key={sc.id} style={{ marginTop: 10 }}>
                  <Typography.Text strong>
                    {sc.id}
                    {sc.title ? ` — ${sc.title}` : ''}
                  </Typography.Text>
                  <Typography.Paragraph type="secondary" style={{ margin: '2px 0 0', fontSize: 12 }}>
                    {sceneShots.map((s) => s.shot).join(', ') || 'chưa shot'}
                    {sc.environment ? ` · ${sc.environment}` : ''}
                  </Typography.Paragraph>
                  {sc.actions?.length ? (
                    <Typography.Paragraph type="secondary" style={{ margin: '2px 0 0', fontSize: 12 }}>
                      {sc.actions.join(' · ')}
                    </Typography.Paragraph>
                  ) : null}
                  {sceneLines.map((l) => {
                    const who = (state.characters ?? []).find((c) => c.id === l.characterId);
                    return (
                      <Typography.Paragraph key={l.id} style={{ margin: '2px 0 0', fontSize: 12 }}>
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
            const portrait = ch ? canonDisplayOf(state, ch.id) : undefined;
            const master = famixaCanonSeedFor(ch ?? { id: r.characterId, name: r.name });
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
                {canonOn ? <Tag color="green">Canon ✓</Tag> : <Tag color="gold">Chưa Canon</Tag>}
                {master ? <Tag>Master v1.0</Tag> : null}
                {voiceOn ? (
                  <Tag color="green">Voice {ch?.voiceName || '✓'}</Tag>
                ) : (
                  <Tag color="gold">Chưa Voice</Tag>
                )}
                {voices.some((v) => v.voiceId === voiceId && v.cloned) ? (
                  <Tag color="warning">Clone — gói chưa TTS</Tag>
                ) : null}
              </Space>
              <div style={{ marginTop: 8 }}>
                {portrait ? (
                  <img
                    src={portrait}
                    alt={ch?.canonFileName || r.name || 'canon'}
                    style={{ width: 96, height: 96, objectFit: 'cover', borderRadius: 8, display: 'block' }}
                  />
                ) : (
                  <Tag>{ch?.canonFileName || 'Chưa có ảnh Canon'}</Tag>
                )}
                <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                  {ch?.canonFileName || ch?.canonLocalPath
                    ? [ch.canonFileName, ch.canonLocalPath].filter(Boolean).join(' · ')
                    : 'Mặt / tóc / tuổi — không dùng làm KF I2V'}
                </Typography.Paragraph>
                <input
                  type="file"
                  accept="image/*"
                  hidden
                  disabled={castFrozen}
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
                    disabled={castFrozen}
                    onClick={() => document.getElementById(`canon-${r.id}`)?.click()}
                  >
                    {canonOn ? 'Thay Canon' : 'Gắn Canon'}
                  </Button>
                </Space>
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
                placeholder={keys.elevenLabs ? `Giọng ${voicePick.lane.label}` : 'Chưa có key ElevenLabs — vẫn gán Voice ID'}
                showSearch
                allowClear
                listHeight={280}
                disabled={castFrozen}
                value={voiceId || undefined}
                optionFilterProp="label"
                options={voicePick.options}
                onChange={(id, opt) => {
                  const label = !Array.isArray(opt) && opt && typeof opt === 'object' && 'label' in opt
                    ? String(opt.label)
                    : voices.find((v) => v.voiceId === id)?.name;
                  patchRole(r.id, { voiceId: id, voiceName: id ? label : undefined });
                }}
              />
              <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
                {voicePick.options.length
                  ? `${voicePick.options.length} giọng ${voicePick.lane.label}. Thêm nhân vật sau cũng chỉ hiện đúng loại giọng.`
                  : keys.elevenLabs
                    ? 'Chưa có giọng miền Bắc khớp vai — reload hoặc dán Voice ID.'
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
              <Button
                disabled={state.scriptLocked}
                onClick={() => persistState({ ...state, castLocked: false })}
              >
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
            Khóa kịch bản → Short
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
                <Button type="primary" disabled={!canLockVoice(state)} onClick={lockVoice}>
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
