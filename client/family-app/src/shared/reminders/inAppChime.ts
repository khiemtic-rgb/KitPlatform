const CHIME_PREF_KEY = 'familyos.inapp_chime';

/** Default ON — parents hear a soft chime while Daily Flow is open. */
export function isInAppChimeEnabled(): boolean {
  if (typeof window === 'undefined') return true;
  const raw = localStorage.getItem(CHIME_PREF_KEY);
  if (raw == null) return true;
  return raw === '1' || raw === 'true';
}

export function setInAppChimeEnabled(enabled: boolean): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(CHIME_PREF_KEY, enabled ? '1' : '0');
}

let sharedCtx: AudioContext | null = null;

function getAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  const AC =
    window.AudioContext ||
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AC) return null;
  if (!sharedCtx || sharedCtx.state === 'closed') {
    sharedCtx = new AC();
  }
  return sharedCtx;
}

/** Soft two-tone chime (no asset file). Safe to call often — short & quiet. */
export async function playInAppDueChime(): Promise<boolean> {
  if (!isInAppChimeEnabled()) return false;
  const ctx = getAudioContext();
  if (!ctx) return false;

  try {
    if (ctx.state === 'suspended') await ctx.resume();
    const now = ctx.currentTime;
    const master = ctx.createGain();
    master.gain.setValueAtTime(0.0001, now);
    master.gain.exponentialRampToValueAtTime(0.18, now + 0.02);
    master.gain.exponentialRampToValueAtTime(0.0001, now + 0.55);
    master.connect(ctx.destination);

    const tone = (freq: number, start: number, dur: number) => {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, start);
      g.gain.setValueAtTime(0.0001, start);
      g.gain.exponentialRampToValueAtTime(0.9, start + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
      osc.connect(g);
      g.connect(master);
      osc.start(start);
      osc.stop(start + dur + 0.02);
    };

    tone(880, now, 0.22);
    tone(1175, now + 0.16, 0.28);
    return true;
  } catch {
    return false;
  }
}
