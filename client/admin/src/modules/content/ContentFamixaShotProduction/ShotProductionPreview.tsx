import { useEffect, useRef, useState, type Ref } from 'react';

export type ShotPreviewKind = 'final' | 'video' | 'lipsync' | 'motion' | 'keyframe' | 'empty';

export const SHOT_PREVIEW_LABEL: Record<ShotPreviewKind, string> = {
  final: 'Video hoàn chỉnh',
  video: 'Bản chuyển động',
  lipsync: 'Preview Lip-sync',
  motion: 'Bản chuyển động',
  keyframe: 'Hình ảnh',
  empty: 'Shot chưa có hình ảnh',
};

/** Leftover still only while reviewing a new picture. A finished short stays on screen. */
export function directorShowsStillPreview(opts: {
  phase: string;
  picturePendingApproval?: boolean;
  pictureUnusable?: boolean;
  hasUsableVideo?: boolean;
  takeFromOtherPicture?: boolean;
}): boolean {
  if (opts.picturePendingApproval || opts.takeFromOtherPicture) return true;
  if (opts.hasUsableVideo) return false;
  return opts.phase === 'picture';
}

export function directorMotionCaption(opts: { current?: boolean; takeN?: number }) {
  if (!opts.current) return 'Video cũ — không khớp hình hiện tại';
  return opts.takeN ? `Video · take ${opts.takeN}` : 'Video';
}

/** Caption describes the file in preview.src. Not acceptedTake / lastSuccess. */
export function directorProductionCaption(opts: { kind: ShotPreviewKind; takeN?: number }) {
  if (opts.kind === 'final') return SHOT_PREVIEW_LABEL.final;
  if (opts.kind === 'lipsync') return SHOT_PREVIEW_LABEL.lipsync;
  if (opts.kind === 'keyframe') return SHOT_PREVIEW_LABEL.keyframe;
  if (opts.kind === 'empty') return SHOT_PREVIEW_LABEL.empty;
  return directorMotionCaption({ current: true, takeN: opts.takeN });
}

/** Old-pipeline stills stay off the hero. Pending or approved stills on this shot stay visible. */
export function directorPreviewStillUrl(opts: {
  pictureUnusable?: boolean;
  picturePendingApproval?: boolean;
  keyframeApproved?: boolean;
  rawKf?: string;
  allowedStill?: string;
}): string | undefined {
  if (opts.pictureUnusable) return undefined;
  return opts.rawKf || opts.allowedStill || undefined;
}

export function resolveShotPreviewKind(opts: {
  finalUrl?: string;
  finalReady?: boolean;
  lipsyncUrl?: string;
  takeUrl?: string;
  keyframeUrl?: string;
  preferKeyframe?: boolean;
}): { kind: ShotPreviewKind; src?: string; label: string; isFinal: boolean } {
  if (opts.preferKeyframe && opts.keyframeUrl) {
    return { kind: 'keyframe', src: opts.keyframeUrl, label: SHOT_PREVIEW_LABEL.keyframe, isFinal: false };
  }
  if (opts.finalUrl) {
    return { kind: 'final', src: opts.finalUrl, label: SHOT_PREVIEW_LABEL.final, isFinal: true };
  }
  if (opts.lipsyncUrl) {
    return { kind: 'lipsync', src: opts.lipsyncUrl, label: SHOT_PREVIEW_LABEL.lipsync, isFinal: false };
  }
  if (opts.takeUrl) {
    return { kind: 'motion', src: opts.takeUrl, label: SHOT_PREVIEW_LABEL.motion, isFinal: false };
  }
  if (opts.keyframeUrl) {
    return { kind: 'keyframe', src: opts.keyframeUrl, label: SHOT_PREVIEW_LABEL.keyframe, isFinal: false };
  }
  return { kind: 'empty', label: SHOT_PREVIEW_LABEL.empty, isFinal: false };
}

function normFromContainClick(
  el: HTMLElement,
  e: { clientX: number; clientY: number },
  mediaW?: number,
  mediaH?: number,
) {
  const rect = el.getBoundingClientRect();
  const vw = mediaW && mediaW > 0 ? mediaW : rect.width;
  const vh = mediaH && mediaH > 0 ? mediaH : rect.height;
  if (vw <= 0 || vh <= 0) return undefined;
  const scale = Math.min(rect.width / vw, rect.height / vh);
  const dw = vw * scale;
  const dh = vh * scale;
  const ox = rect.left + (rect.width - dw) / 2;
  const oy = rect.top + (rect.height - dh) / 2;
  const x = (e.clientX - ox) / dw;
  const y = (e.clientY - oy) / dh;
  if (x < 0 || x > 1 || y < 0 || y > 1) return undefined;
  return { x, y, width: Math.round(vw), height: Math.round(vh) };
}

/** Overlay ElevenLabs only on a mute Veo take. Lipsync / mix already carry voice. */
export function muteTakeVoiceOverlay(kind: ShotPreviewKind, voiceSrc?: string) {
  return Boolean(voiceSrc?.trim()) && (kind === 'motion' || kind === 'video');
}

export function ShotProductionPreview({
  kind,
  src,
  label,
  title,
  pendingApproval,
  emptyLabel,
  videoRef,
  tone = 'staff',
  speakerMark,
  pickSpeaker,
  onPickSpeaker,
  voiceSrc,
}: {
  kind: ShotPreviewKind;
  src?: string;
  label: string;
  title?: string;
  pendingApproval?: boolean;
  emptyLabel?: string;
  videoRef?: Ref<HTMLVideoElement>;
  tone?: 'director' | 'staff';
  speakerMark?: { x: number; y: number };
  pickSpeaker?: boolean;
  onPickSpeaker?: (pt: { x: number; y: number; width: number; height: number }) => void;
  voiceSrc?: string;
}) {
  const localRef = useRef<HTMLVideoElement>(null);
  const audioRef = useRef<HTMLAudioElement>(null);
  const [videoEl, setVideoEl] = useState<HTMLVideoElement | null>(null);
  const overlay = muteTakeVoiceOverlay(kind, voiceSrc);
  const bindVideo = (el: HTMLVideoElement | null) => {
    localRef.current = el;
    setVideoEl(el);
    if (videoRef && typeof videoRef !== 'function') videoRef.current = el;
  };
  useEffect(() => {
    const audio = audioRef.current;
    if (!overlay || !voiceSrc?.trim() || !videoEl || !audio) return;
    const sync = () => {
      try {
        if (!Number.isFinite(audio.duration) || audio.duration <= 0) return;
        audio.currentTime = Math.min(Math.max(0, videoEl.currentTime), audio.duration);
      } catch {
        /* metadata not ready */
      }
    };
    const onPlay = () => {
      void audio.play().then(sync).catch(() => undefined);
    };
    const onPause = () => audio.pause();
    videoEl.addEventListener('play', onPlay);
    videoEl.addEventListener('pause', onPause);
    videoEl.addEventListener('seeking', sync);
    videoEl.addEventListener('ended', onPause);
    if (!videoEl.paused) onPlay();
    return () => {
      videoEl.removeEventListener('play', onPlay);
      videoEl.removeEventListener('pause', onPause);
      videoEl.removeEventListener('seeking', sync);
      videoEl.removeEventListener('ended', onPause);
      audio.pause();
    };
  }, [overlay, voiceSrc, src, kind, videoEl]);
  const director = tone === 'director';
  const caption = pendingApproval && kind === 'keyframe'
    ? 'Chờ duyệt'
    : director
      ? kind === 'keyframe'
        ? 'Hình'
        : label || 'Video'
      : label;
  if (kind === 'empty' || !src) {
    return (
      <div className="fx-media-card" style={{ minHeight: 280, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <p className="fx-desk__note">{emptyLabel || (director ? 'Chưa có hình' : SHOT_PREVIEW_LABEL.empty)}</p>
      </div>
    );
  }
  const mark = speakerMark ? (
    <span
      aria-hidden
      style={{
        position: 'absolute',
        left: `${speakerMark.x * 100}%`,
        top: `${speakerMark.y * 100}%`,
        width: 14,
        height: 14,
        marginLeft: -7,
        marginTop: -7,
        border: '2px solid #1677ff',
        borderRadius: 99,
        boxShadow: '0 0 0 2px #fff',
        pointerEvents: 'none',
      }}
    />
  ) : null;
  if (kind === 'keyframe') {
    return (
      <figure className="fx-media-card" style={{ margin: 0 }}>
        <div style={{ position: 'relative' }}>
          <img
            src={src}
            alt={title || 'Hình'}
            className="fx-media-card__still"
            style={pickSpeaker ? { cursor: 'crosshair' } : undefined}
            onClick={(e) => {
              if (!pickSpeaker || !onPickSpeaker) return;
              const img = e.currentTarget;
              const pt = normFromContainClick(img, e, img.naturalWidth, img.naturalHeight);
              if (pt) onPickSpeaker(pt);
            }}
          />
          {mark}
        </div>
        <figcaption className="fx-desk__note">
          {director ? 'Hình' : 'Hình ảnh Keyframe'}{pendingApproval ? ' · Chờ duyệt' : ''}
          {pickSpeaker ? ' · Bấm mặt người nói trên hình' : ''}
        </figcaption>
      </figure>
    );
  }
  return (
    <figure className="fx-media-card" style={{ margin: 0 }}>
      <div style={{ position: 'relative' }}>
        <video
          key={src}
          ref={bindVideo}
          src={src}
          controls
          playsInline
          muted={overlay}
          preload="metadata"
          className="fx-media-card__clip"
        />
        {pickSpeaker && onPickSpeaker ? (
          <button
            type="button"
            aria-label="Chấm mặt người nói"
            style={{
              position: 'absolute',
              inset: '0 0 44px 0',
              cursor: 'crosshair',
              background: 'transparent',
              border: 0,
              padding: 0,
            }}
            onClick={(e) => {
              const el = (videoRef && 'current' in videoRef ? videoRef.current : null) || localRef.current;
              if (!el) return;
              const pt = normFromContainClick(el, e, el.videoWidth, el.videoHeight);
              if (pt) onPickSpeaker(pt);
            }}
          />
        ) : null}
        {mark}
      </div>
      {overlay && voiceSrc ? (
        <audio
          ref={audioRef}
          src={voiceSrc}
          preload="auto"
          aria-hidden
          style={{ display: 'none' }}
        />
      ) : null}
      <figcaption className="fx-desk__note">
        {caption}
        {overlay ? ' · thoại phát cùng Play video (miệng chưa khớp)' : ''}
        {pickSpeaker ? ' · Bấm mặt người nói trên video (không bấm Minh)' : ''}
      </figcaption>
    </figure>
  );
}
