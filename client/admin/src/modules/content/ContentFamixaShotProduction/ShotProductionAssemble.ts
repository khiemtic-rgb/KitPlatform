/** One-shot body for existing POST /content/series/assemble. Not a new mix pipeline. */

export type OneShotAssembleClip = {
  shotCode: string;
  seconds: number;
  videoUrl?: string;
  spoken: boolean;
  lipsynced: boolean;
  voices: { lineId: string; startSec: number; audioBase64: string; mime?: string }[];
  aspect?: '16:9' | '9:16';
  grade?: boolean;
  interpolate?: boolean;
};

export function oneShotAssembleBody(clip: OneShotAssembleClip) {
  return {
    fileStem: `famixa-shot-${clip.shotCode.replace(/\s+/g, '')}`,
    aspect: clip.aspect,
    clips: [
      {
        code: clip.shotCode,
        videoUrl: clip.videoUrl,
        seconds: clip.seconds,
        voices: clip.lipsynced ? [] : clip.voices,
        useVideoAudio: clip.lipsynced,
        requireVoice: clip.spoken && !clip.lipsynced,
      },
    ],
    mix: {
      room: true,
      music: true,
      loudnorm: true,
      grade: clip.grade !== false,
      colorMatch: false,
      interpolate: clip.interpolate === true,
    },
  };
}
