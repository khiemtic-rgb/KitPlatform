/** Timeline reads FINAL_SOURCE only. Does not guess Fal vs TTS overlay. */

export type FinalSource = 'FAL' | 'RUNWAY_TTS' | 'RUNWAY' | 'NONE';

export type FinalSourceRun = {
  finalSource?: FinalSource;
  lipsynced?: boolean;
  lipsyncUrl?: string;
  previewUrl?: string;
  /** Mute Runway take — never overwritten by Fal FINAL. */
  takeUrl?: string;
  takeHistory?: { url: string; taskId?: string }[];
};

export function shotKeepsFal(run?: FinalSourceRun) {
  return Boolean(run?.lipsynced || run?.lipsyncUrl?.trim() || run?.finalSource === 'FAL');
}

export function resolveTakeUrl(run?: FinalSourceRun) {
  const take = run?.takeUrl?.trim();
  if (take) return take;
  const lip = run?.lipsyncUrl?.trim();
  const hist = run?.takeHistory?.find((h) => h.url?.trim() && h.url.trim() !== lip)?.url?.trim();
  if (hist) return hist;
  const prev = run?.previewUrl?.trim();
  if (prev && prev !== lip) return prev;
  return undefined;
}

export function resolveFinalSource(run?: FinalSourceRun, silent = false): FinalSource {
  if (shotKeepsFal(run)) return 'FAL';
  const hasVideo = Boolean(resolveTakeUrl(run) || run?.previewUrl?.trim() || run?.lipsyncUrl?.trim());
  if (!hasVideo) return 'NONE';
  if (silent) return 'RUNWAY';
  return 'RUNWAY_TTS';
}

/** Mute I2V take. Fal must not call this. */
export function stampMuteTake(url: string, silent = false): Partial<FinalSourceRun> {
  return {
    takeUrl: url,
    previewUrl: url,
    lipsynced: false,
    lipsyncUrl: undefined,
    finalSource: silent ? 'RUNWAY' : 'RUNWAY_TTS',
  };
}

/** Fal FINAL. Keeps TAKE. Does not overwrite previewUrl/takeUrl. */
export function stampFalFinal(run: FinalSourceRun, falUrl: string): Partial<FinalSourceRun> {
  const take = resolveTakeUrl(run) || (run.previewUrl && run.previewUrl !== falUrl ? run.previewUrl : undefined);
  return {
    lipsyncUrl: falUrl,
    lipsynced: true,
    finalSource: 'FAL',
    takeUrl: take,
  };
}
export function stampFinalSource<T extends FinalSourceRun>(run: T, silent = false): T {
  return { ...run, finalSource: resolveFinalSource(run, silent) };
}

export function assembleVideoUrl(run?: FinalSourceRun) {
  if (resolveFinalSource(run) === 'FAL') {
    return run?.lipsyncUrl?.trim() || run?.previewUrl?.trim() || undefined;
  }
  return run?.previewUrl?.trim() || run?.lipsyncUrl?.trim() || undefined;
}

export function isFalSource(src?: FinalSource) {
  return src === 'FAL';
}

export function isPreviewTempSource(src?: FinalSource) {
  return src === 'RUNWAY_TTS';
}

export type FinalSourceItem = {
  code: string;
  silent?: boolean;
  finalSource?: FinalSource;
  lipsynced?: boolean;
};

/** Spoken shots without Fal cannot Final — RUNWAY_TTS is preview-only. */
export function finalSourceBlockReason(items: FinalSourceItem[]) {
  const temp = items.filter((i) => !i.silent && (i.finalSource === 'RUNWAY_TTS' || i.finalSource === 'NONE' || !i.finalSource));
  const falMissing = items.filter((i) => !i.silent && i.finalSource !== 'FAL' && !i.lipsynced);
  const block = falMissing.length ? falMissing : temp.filter((i) => i.finalSource !== 'FAL');
  if (!block.length) return undefined;
  return `${block.map((i) => i.code).join(', ')} chưa FINAL_SOURCE=FAL — Preview tạm (TTS overlay), không phải Final.`;
}

export function mergeKeepFinalSource(rem?: FinalSourceRun, old?: FinalSourceRun): Partial<FinalSourceRun> {
  const takeUrl = rem?.takeUrl || old?.takeUrl || resolveTakeUrl(old) || resolveTakeUrl(rem);
  if (shotKeepsFal(rem)) {
    return { finalSource: 'FAL', lipsynced: rem?.lipsynced ?? true, lipsyncUrl: rem?.lipsyncUrl, takeUrl };
  }
  if (shotKeepsFal(old)) {
    return { finalSource: 'FAL', lipsynced: true, lipsyncUrl: old?.lipsyncUrl || old?.previewUrl, takeUrl };
  }
  return { finalSource: rem?.finalSource || old?.finalSource, takeUrl };
}
