/** Veo take is a local API file, not a data-URI in poll JSON. */

export const VEO_VIDEO_ROUTE = '/api/content/series/veo-video';

export function isVeoMotionTaskId(taskId?: string) {
  return (taskId || '').trim().toLowerCase().startsWith('veo_');
}

export function veoTakePlaybackUrl(taskId?: string) {
  const id = (taskId || '').trim();
  if (!isVeoMotionTaskId(id)) return undefined;
  return `${VEO_VIDEO_ROUTE}?taskId=${encodeURIComponent(id)}`;
}

export function isVeoPlaybackSrc(url?: string) {
  const src = (url || '').trim();
  return src.startsWith('data:video') || src.includes('/content/series/veo-video') || src.includes('/content/series/veo-takes/');
}

/** Veo only accepts 4 | 6 | 8. Timing 5 + thoại dài hơn 4s must map to 6 — do not clip speech. */
export function veoMappedDurationSec(sent: number, floorSec = 0) {
  if (sent >= 8 || floorSec > 6.02) return 8;
  if (sent >= 6 || floorSec > 4.02) return 6;
  return 4;
}

/** Mix/export clock. Speech floor wins over a silent 4s map so a 6s take is not cut again. */
export function veoAssembleDurationSec(asked: number, floorSec = 0) {
  const want = Number(asked) || 0;
  const floor = Number(floorSec) || 0;
  const mapped = veoMappedDurationSec(want, floor);
  if (floor > 4.02 && mapped >= 6) return Math.max(want, mapped, floor);
  if (mapped > 0 && mapped + 0.001 < want - 0.12) return mapped;
  return want;
}

/** Take shorter than spoken end cuts the last words. 0 provider calls. */
export function veoSpeechOverflowReason(opts: { takeSec: number; dialogueEndSec: number; floorSec?: number }) {
  const take = Number(opts.takeSec) || 0;
  const end = Number(opts.dialogueEndSec) || 0;
  if (take <= 0 || end <= 0 || end <= take + 0.021) return undefined;
  const next = veoMappedDurationSec(take > 5.5 ? 10 : 5, opts.floorSec ?? end);
  return `Thoại hết ${end.toFixed(2)}s, take ${take.toFixed(2)}s — cuối câu bị cắt. Tạo lại video ${next}s rồi khớp môi.`;
}

function sameVeoPointer(url: string | undefined, taskId: string) {
  const src = (url || '').trim();
  if (!src || src.startsWith('data:video')) return true;
  if (src.includes('/content/series/veo-video') || src.includes('/content/series/veo-takes/')) return true;
  const id = (taskId || '').trim();
  return Boolean(id) && src.includes(id);
}

/** Keep a short Veo path. Do not persist data:video. Does not send a new job. */
export function rewriteVeoTakePointers<
  T extends {
    turboTaskId?: string;
    takeUrl?: string;
    previewUrl?: string;
    runwayAttempts?: Array<{ taskId?: string; outputUrl?: string }>;
  },
>(run: T): T {
  const taskId = (run.turboTaskId || '').trim();
  const play = veoTakePlaybackUrl(taskId);
  if (!play) return run;
  const take = (run.takeUrl || '').trim();
  const preview = (run.previewUrl || '').trim();
  const nextTake = sameVeoPointer(take, taskId) || !take ? play : take;
  const nextPreview = sameVeoPointer(preview, taskId) || !preview ? play : preview;
  const attempts = (run.runwayAttempts ?? []).map((row) => {
    const rowTask = (row.taskId || '').trim();
    if (rowTask && rowTask !== taskId) return row;
    const out = (row.outputUrl || '').trim();
    if (rowTask === taskId && (sameVeoPointer(out, taskId) || !out)) {
      return { ...row, outputUrl: play };
    }
    return row;
  });
  if (nextTake === take && nextPreview === preview && attempts === run.runwayAttempts) return run;
  return { ...run, takeUrl: nextTake, previewUrl: nextPreview, runwayAttempts: attempts };
}
