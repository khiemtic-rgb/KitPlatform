import { http } from '@/shared/api/http';

/** M1 "Dựng tập từ bảng câu" — API `api/content/famixa/linesheet`. Tenant lấy từ phiên đăng nhập, không gửi kèm. */
const base = '/content/famixa/linesheet';
const LONG = { timeout: 15 * 60_000 };

export type LinesheetProduction = {
  id: string;
  universeCode: string;
  episodeCode: string;
  title: string;
  updatedAt: string;
};

export type LinesheetStatus = {
  production: LinesheetProduction;
  scriptVersion: number | null;
  rowCount: number;
  lineCount: number;
  settingsReady: boolean;
  settingsErrors: string[];
  fileCounts: Record<string, number>;
  missingAudio: string[];
  missingCues: string[];
  missingLayers: string[];
  timelineTotalMs: number | null;
  timelineEstimatedCount: number;
  shotCount: number | null;
  shotsEdited: boolean;
};

export type LinesheetShotLog = {
  shotId: string;
  setting: string;
  preset: string;
  startMs: number;
  endMs: number;
  frames: number;
  cacheKey: string;
  cached: boolean;
  renderMs: number;
  costUsd: number;
};

export type LinesheetRun = {
  id: string;
  kind: string;
  format: '16x9' | '9x16';
  width: number;
  height: number;
  fps: number;
  rangeStartMs: number;
  rangeEndMs: number;
  textLang: 'vi' | 'en';
  burnSubs: 'none' | 'vi' | 'en';
  status: 'QUEUED' | 'RUNNING' | 'SUCCEEDED' | 'FAILED' | 'CANCELLED';
  nextStep: number;
  stepCount: number;
  framesExpected: number | null;
  framesActual: number | null;
  hasOutput: boolean;
  error: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  shotLog: LinesheetShotLog[];
};

export type LinesheetQueueRender = {
  format: '16x9' | '9x16';
  width?: number;
  height?: number;
  fps?: number;
  rangeStartMs?: number;
  rangeEndMs?: number;
  textLang: 'vi' | 'en';
  burnSubs: 'none' | 'vi' | 'en';
};

export type LinesheetTtsLine = { rowId: string; speaker: string; tags: string; text: string; chars: number; pending: boolean; take: number };
export type LinesheetTtsPreview = {
  model: string;
  lineCount: number;
  totalChars: number;
  pendingCount: number;
  pendingChars: number;
  missingVoices: string[];
  lines: LinesheetTtsLine[];
};
export type LinesheetTtsResult = { generated: number; charsSent: number; failed: string[] };

/** Ảnh cảnh, chuyển động, ghép môi (thêm 2026-11-06): xem trước miễn phí, chạy thì phải xác nhận đúng số tiền. */
export type LinesheetKeyframeItem = { name: string; model: string; ratio: string; credits: number; refs: string[]; prompt: string; has: boolean };
export type LinesheetKeyframePreview = { count: number; totalCredits: number; pendingCount: number; pendingCredits: number; keyframes: LinesheetKeyframeItem[] };
export type LinesheetMotionShot = { shotId: string; scene: string; setting: string; durationMs: number; clipSeconds: number; credits: number; prompt: string; has: boolean };
export type LinesheetMotionPreview = { model: string; shotCount: number; totalCredits: number; pendingCount: number; pendingCredits: number; shots: LinesheetMotionShot[] };
export type LinesheetLipsyncLine = { rowId: string; shotId: string; speaker: string; seconds: number; usd: number; has: boolean };
export type LinesheetLipsyncPreview = { model: string; lineCount: number; totalUsd: number; pendingCount: number; pendingUsd: number; lines: LinesheetLipsyncLine[]; notes: string[] };
export type LinesheetCreditResult = { generated: number; creditsUsed: number; failed: string[] };
export type LinesheetLipsyncResult = { generated: number; usdUsed: number; failed: string[] };

export type LinesheetUploadResult = { accepted: number; skipped: string[]; byKind: Record<string, number> };

function form(file: File) {
  const f = new FormData();
  f.append('file', file);
  return f;
}

export const listLinesheetProductions = () =>
  http.get<LinesheetProduction[]>(`${base}/productions`).then((r) => r.data);

export const createLinesheetProduction = (body: { universeCode: string; episodeCode: string; title?: string }) =>
  http.post<LinesheetProduction>(`${base}/productions`, body).then((r) => r.data);

export const getLinesheetStatus = (id: string) =>
  http.get<LinesheetStatus>(`${base}/productions/${id}/status`).then((r) => r.data);

export const importLinesheetScript = (id: string, file: File) =>
  http.post<{ version: number }>(`${base}/productions/${id}/script`, form(file)).then((r) => r.data);

export const getLinesheetSettings = (id: string) =>
  http.get<unknown>(`${base}/productions/${id}/settings`).then((r) => r.data);

export const putLinesheetSettings = (id: string, settings: unknown) =>
  http.put<LinesheetStatus>(`${base}/productions/${id}/settings`, settings).then((r) => r.data);

export const uploadLinesheetFiles = (id: string, zip: File, onProgress?: (pct: number) => void) =>
  http
    .post<LinesheetUploadResult>(`${base}/productions/${id}/files`, form(zip), {
      ...LONG,
      onUploadProgress: (e) => {
        if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100));
      },
    })
    .then((r) => r.data);

/** Xem trước giọng ElevenLabs (không tốn tiền). */
export const previewLinesheetTts = (id: string) =>
  http.get<LinesheetTtsPreview>(`${base}/productions/${id}/tts`).then((r) => r.data);

/** Gọi ElevenLabs (tốn tiền): confirmChars phải bằng số ký tự đã xem trước. */
/** newTake: cùng rowIds, tạo bản đọc mới (seed khác) thay vì đọc lại bản cũ. */
export const generateLinesheetTts = (id: string, body: { rowIds?: string[]; onlyPending: boolean; confirmChars: number; newTake?: boolean }) =>
  http.post<LinesheetTtsResult>(`${base}/productions/${id}/tts`, body, LONG).then((r) => r.data);

export const buildLinesheetTimeline = (id: string, allowMissing: boolean) =>
  http.post<LinesheetStatus>(`${base}/productions/${id}/timeline`, null, { params: { allowMissing } }).then((r) => r.data);

export const draftLinesheetShots = (id: string, force: boolean) =>
  http.post<{ shots: number }>(`${base}/productions/${id}/shots/draft`, null, { params: { force } }).then((r) => r.data);

export const putLinesheetShots = (id: string, file: File) =>
  http.put<{ shots: number }>(`${base}/productions/${id}/shots`, form(file)).then((r) => r.data);

export const queueLinesheetRender = (id: string, body: LinesheetQueueRender) =>
  http.post<LinesheetRun>(`${base}/productions/${id}/renders`, body).then((r) => r.data);

export const listLinesheetRuns = (id: string) =>
  http.get<LinesheetRun[]>(`${base}/productions/${id}/renders`).then((r) => r.data);

export const cancelLinesheetRun = (runId: string) =>
  http.post<{ cancelled: boolean }>(`${base}/renders/${runId}/cancel`).then((r) => r.data);

export const previewLinesheetKeyframes = (id: string) =>
  http.get<LinesheetKeyframePreview>(`${base}/productions/${id}/keyframes`).then((r) => r.data);

/** Gọi Runway (tốn credit). Mỗi lần một ảnh: không chạy hai lượt tạo ảnh cùng lúc. */
export const generateLinesheetKeyframes = (id: string, body: { names: string[]; confirmCredits: number; force?: boolean }) =>
  http.post<LinesheetCreditResult>(`${base}/productions/${id}/keyframes`, body, LONG).then((r) => r.data);

export const previewLinesheetMotion = (id: string) =>
  http.get<LinesheetMotionPreview>(`${base}/productions/${id}/motion`).then((r) => r.data);

/** Gọi Runway (tốn credit). Mô hình lấy từ settings.motion_model. */
export const generateLinesheetMotion = (id: string, body: { shotIds: string[]; confirmCredits: number; force?: boolean; prompts?: Record<string, string> }) =>
  http.post<LinesheetCreditResult>(`${base}/productions/${id}/motion`, body, LONG).then((r) => r.data);

export const previewLinesheetLipsync = (id: string) =>
  http.get<LinesheetLipsyncPreview>(`${base}/productions/${id}/lipsync`).then((r) => r.data);

/** Gọi fal (tốn USD). confirmUsd làm tròn 2 chữ số. */
export const generateLinesheetLipsync = (id: string, body: { rowIds: string[]; confirmUsd: number; force?: boolean }) =>
  http.post<LinesheetLipsyncResult>(`${base}/productions/${id}/lipsync`, body, LONG).then((r) => r.data);

/** Tải file về dạng Blob (cần token, nên không dùng thẳng đường dẫn trong thẻ video). */
export async function fetchLinesheetBlob(path: string) {
  const r = await http.get<Blob>(path, { ...LONG, responseType: 'blob' });
  const disposition = String(r.headers['content-disposition'] || '');
  const name = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1] ?? /filename="?([^";]+)"?/i.exec(disposition)?.[1] ?? 'download';
  return { blob: r.data, name: decodeURIComponent(name) };
}

export const linesheetPaths = {
  timelineCsv: (id: string) => `${base}/productions/${id}/timeline.csv`,
  subtitles: (id: string, lang: 'vi' | 'en') => `${base}/productions/${id}/subtitles/${lang}`,
  shotsCsv: (id: string) => `${base}/productions/${id}/shots.csv`,
  runFile: (runId: string, what: 'video' | 'srt-vi' | 'srt-en') => `${base}/renders/${runId}/download/${what}`,
  keyframeImage: (id: string, name: string) => `${base}/productions/${id}/keyframes/${encodeURIComponent(name)}`,
  motionClip: (id: string, shotId: string) => `${base}/productions/${id}/motion/${encodeURIComponent(shotId)}`,
  lineAudio: (id: string, rowId: string) => `${base}/productions/${id}/audio/${encodeURIComponent(rowId)}`,
};
