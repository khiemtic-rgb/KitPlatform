import { ORCH_GATE } from './ShotProductionErrors';

export type ShotVoiceCue = {
  id: string;
  voiceId?: string;
  text: string;
  name: string;
  characterId?: string;
};

/** Reuse existing TTS action. Does not invent Voice ID. Hydrate IDB before generate. */
export async function ensureShotVoiceAssets(opts: {
  lines: ShotVoiceCue[];
  loadCueAudio: (cue: ShotVoiceCue) => Promise<string>;
  alreadyReady: (lineId: string) => boolean;
  hydrateCueAudio?: (cue: ShotVoiceCue) => Promise<string | undefined>;
}) {
  for (const line of opts.lines) {
    if (!(line.voiceId || '').trim()) {
      throw new Error(ORCH_GATE.VOICE_NOT_ASSIGNED);
    }
    if (opts.alreadyReady(line.id)) continue;
    const existing = await opts.hydrateCueAudio?.(line);
    if (existing || opts.alreadyReady(line.id)) continue;
    await opts.loadCueAudio(line);
  }
}
