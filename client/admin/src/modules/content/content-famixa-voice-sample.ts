export type VoiceSampleOption = {
  value: string;
  label: string;
  previewUrl?: string | null;
};

export function voiceSampleUrl(voices: VoiceSampleOption[] | undefined, voiceId?: string | null) {
  const id = (voiceId || '').trim();
  if (!id) return undefined;
  return (voices ?? []).find((v) => v.value === id)?.previewUrl?.trim() || undefined;
}

export function voiceLibraryStamp(voices?: VoiceSampleOption[]) {
  return (voices ?? []).map((v) => `${v.value}:${(v.previewUrl || '').trim()}`).join('|');
}

export function voicesNeedSampleRefresh(voices?: VoiceSampleOption[]) {
  return Boolean(voices?.length) && !(voices ?? []).some((v) => (v.previewUrl || '').trim());
}

export function mergeVoiceSamples(current: VoiceSampleOption[], incoming: VoiceSampleOption[]) {
  if (!incoming.length) return current;
  const byId = new Map(current.map((v) => [v.value, v]));
  for (const row of incoming) {
    const prev = byId.get(row.value);
    const previewUrl = row.previewUrl?.trim() || prev?.previewUrl;
    byId.set(row.value, { ...prev, ...row, previewUrl });
  }
  return [...byId.values()];
}

let currentSample: HTMLAudioElement | undefined;
let currentBlobUrl: string | undefined;

export function playVoiceSample(url?: string | null) {
  const src = (url || '').trim();
  if (!src) return false;
  stopVoiceSample();
  const audio = new Audio(src);
  audio.preload = 'auto';
  currentSample = audio;
  void audio.play().catch(() => undefined);
  return true;
}

export async function playVoiceBlob(blob: Blob) {
  if (!blob || blob.size === 0) return false;
  stopVoiceSample();
  currentBlobUrl = URL.createObjectURL(blob);
  const audio = new Audio(currentBlobUrl);
  audio.preload = 'auto';
  currentSample = audio;
  try {
    await audio.play();
    return true;
  } catch {
    stopVoiceSample();
    return false;
  }
}

export function stopVoiceSample() {
  if (currentSample) {
    currentSample.pause();
    currentSample.removeAttribute('src');
    currentSample = undefined;
  }
  if (currentBlobUrl) {
    URL.revokeObjectURL(currentBlobUrl);
    currentBlobUrl = undefined;
  }
}
