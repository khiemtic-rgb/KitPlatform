/** Fal v3 LipSync HOW. OpenAPI ASD fields only. SeriesTab must not build this JSON. 0 provider calls. */

import { LIPSYNC_SPEAKER_MARK_FRAME_NUMBER } from './famixa-lipsync-speaker-target';

export const FAL_V3_LIPSYNC_SPEAKER_HOW_ID = 'FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1';
export { LIPSYNC_SPEAKER_MARK_FRAME_NUMBER };

export const FAL_V3_SOURCE_VIDEO_WIDTH = 1280;
export const FAL_V3_SOURCE_VIDEO_HEIGHT = 720;

export const FAL_V3_WHAT_LEAK_FIELDS = ['falFaceX', 'falFaceY', 'falSpeakerIndex'] as const;

export type FalV3ActiveSpeakerDetection = {
  frame_number: number;
  coordinates: [number, number];
};

export type FalV3LipsyncHow = {
  video_url?: string;
  takeTaskId?: string;
  sync_mode?: string;
  options?: {
    active_speaker_detection: FalV3ActiveSpeakerDetection;
  };
};

export function sourceVideoPixelCoordinates(
  x01: number,
  y01: number,
  width = FAL_V3_SOURCE_VIDEO_WIDTH,
  height = FAL_V3_SOURCE_VIDEO_HEIGHT,
): [number, number] {
  const w = width > 0 ? width : FAL_V3_SOURCE_VIDEO_WIDTH;
  const h = height > 0 ? height : FAL_V3_SOURCE_VIDEO_HEIGHT;
  const px = Math.round(Math.min(1, Math.max(0, x01)) * w);
  const py = Math.round(Math.min(1, Math.max(0, y01)) * h);
  return [Math.min(Math.max(0, w - 1), px), Math.min(Math.max(0, h - 1), py)];
}

function sourceVideoSize(width?: number, height?: number) {
  const w = Number(width);
  const h = Number(height);
  return {
    width: w > 1 ? Math.round(w) : FAL_V3_SOURCE_VIDEO_WIDTH,
    height: h > 1 ? Math.round(h) : FAL_V3_SOURCE_VIDEO_HEIGHT,
  };
}

export function falV3ActiveSpeakerDetectionOf(opts: {
  x: number;
  y: number;
  width?: number;
  height?: number;
  frameNumber?: number;
}): FalV3ActiveSpeakerDetection {
  const size = sourceVideoSize(opts.width, opts.height);
  const frame_number = Number.isInteger(opts.frameNumber) && Number(opts.frameNumber) >= 0
    ? Number(opts.frameNumber)
    : LIPSYNC_SPEAKER_MARK_FRAME_NUMBER;
  return {
    frame_number,
    coordinates: sourceVideoPixelCoordinates(opts.x, opts.y, size.width, size.height),
  };
}

export function mapFrozenSpeakerToFalV3How(opts: {
  videoUrl?: string;
  takeTaskId?: string;
  syncMode?: string;
  speakerCharacterId?: string;
  speakerFaceRegion?: { x: number; y: number; width?: number; height?: number };
  speakerFrameNumber?: number;
}): FalV3LipsyncHow {
  const region = opts.speakerFaceRegion;
  if (!region || !Number.isFinite(region.x) || !Number.isFinite(region.y)) {
    throw new Error('Frozen speakerFaceRegion missing — không gửi Fal.');
  }
  return {
    video_url: (opts.videoUrl || '').trim() || undefined,
    takeTaskId: (opts.takeTaskId || '').trim() || undefined,
    sync_mode: (opts.syncMode || '').trim() || undefined,
    options: {
      active_speaker_detection: falV3ActiveSpeakerDetectionOf({
        x: region.x,
        y: region.y,
        width: region.width,
        height: region.height,
        frameNumber: opts.speakerFrameNumber,
      }),
    },
  };
}

export function whatHasFalSpecificSpeakerField(body: Record<string, unknown>) {
  return FAL_V3_WHAT_LEAK_FIELDS.some((key) => key in body && body[key] != null);
}
