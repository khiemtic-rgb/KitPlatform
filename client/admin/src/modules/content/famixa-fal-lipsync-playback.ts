/** Fal lipsync take is a local API file after persist. Do not POST a new job. */

export const FAL_VIDEO_ROUTE = '/api/content/series/fal-video';

export function isFalLipsyncTaskId(taskId?: string) {
  return (taskId || '').trim().toLowerCase().startsWith('lipsync_');
}

export function falLipsyncPlaybackUrl(taskId?: string) {
  const id = (taskId || '').trim();
  if (!isFalLipsyncTaskId(id)) return undefined;
  return `${FAL_VIDEO_ROUTE}?taskId=${encodeURIComponent(id)}`;
}

export function isFalPlaybackSrc(url?: string) {
  const src = (url || '').trim();
  return src.includes('/content/series/fal-video');
}

/** Provider CDN / https mp4. A bare /fal-video pointer is not playable until the API file is proven. */
export function isPlayableFalOutputUrl(url?: string) {
  const raw = (url || '').trim();
  if (!raw || isFalPlaybackSrc(raw)) return false;
  if (raw.startsWith('blob:') || raw.startsWith('data:video')) return true;
  if (!/^https:\/\//i.test(raw)) return false;
  return /fal\.media/i.test(raw) || /\.mp4(\?|#|$)/i.test(raw);
}

/** Hero <video> only. Auth blob / data — never /fal-video, /veo-video, or expired fal.media. */
export function deskAuthVideoSrc(url?: string) {
  const src = (url || '').trim();
  if (src.startsWith('blob:') || src.startsWith('data:video')) return src;
  return undefined;
}

/** Local fal-video needs an auth blob or a verified API file. Unverified pointer is not MP4. */
export function playableFalLipsyncSrc(opts: { blobUrl?: string; lipsyncUrl?: string; localFileReady?: boolean }) {
  const blob = (opts.blobUrl || '').trim();
  if (blob) return blob;
  const raw = (opts.lipsyncUrl || '').trim();
  if (!raw) return undefined;
  if (isFalPlaybackSrc(raw)) return opts.localFileReady ? raw : undefined;
  return isPlayableFalOutputUrl(raw) ? raw : undefined;
}

/** SH02 must not play SH01's leftover Fal blob. */
export function deskFalPlayableForShot(opts: {
  shotId?: string;
  taskId?: string;
  fetchedShotId?: string;
  fetchedTaskId?: string;
  blobUrl?: string;
}) {
  const shotId = (opts.shotId || '').trim();
  const taskId = (opts.taskId || '').trim();
  if (!shotId || !isFalLipsyncTaskId(taskId)) return undefined;
  if ((opts.fetchedShotId || '').trim() !== shotId) return undefined;
  if ((opts.fetchedTaskId || '').trim() !== taskId) return undefined;
  return playableFalLipsyncSrc({ blobUrl: deskAuthVideoSrc(opts.blobUrl) });
}

export function canStampFalLipsyncFinal(url?: string, localFileReady?: boolean) {
  return Boolean(playableFalLipsyncSrc({ lipsyncUrl: url, localFileReady }));
}

/** Claimed Fal take whose URL is empty or a bare /fal-video pointer — poll/attach, never a new charge. */
export function lipsyncRunFileMissing(run?: {
  lipsynced?: boolean;
  lipsyncUrl?: string;
  lipsyncTaskId?: string;
}) {
  if (!run) return false;
  const claimed = Boolean(run.lipsynced || (run.lipsyncUrl || '').trim());
  if (!claimed) return false;
  return !playableFalLipsyncSrc({ lipsyncUrl: run.lipsyncUrl });
}

function sameFalPointer(url: string | undefined, taskId: string) {
  const src = (url || '').trim();
  if (!src) return true;
  if (isFalPlaybackSrc(src)) return true;
  const id = (taskId || '').trim();
  return Boolean(id) && src.includes(id) && !src.includes('fal.media');
}

/** Keep a short Fal path after persist. Does not send a new job. */
export function rewriteFalLipsyncPointers<
  T extends {
    lipsyncTaskId?: string;
    lipsyncUrl?: string;
  },
>(run: T): T {
  const taskId = (run.lipsyncTaskId || '').trim();
  const play = falLipsyncPlaybackUrl(taskId);
  if (!play) return run;
  const lip = (run.lipsyncUrl || '').trim();
  if (!lip) return run;
  if (lip.includes('fal.media') || (lip.includes('.mp4') && !isFalPlaybackSrc(lip) && !lip.includes(taskId))) {
    return run;
  }
  if (isFalPlaybackSrc(lip) && sameFalPointer(lip, taskId)) return run;
  const next = sameFalPointer(lip, taskId) ? play : lip;
  return next === lip ? run : { ...run, lipsyncUrl: next };
}
