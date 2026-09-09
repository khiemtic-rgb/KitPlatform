import { useRef, type Ref } from 'react';

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

/** Old-pipeline stills stay off the hero. Pending or approved stills on this shot stay visible. */
export function directorPreviewStillUrl(opts: {
  pictureUnusable?: boolean;
  picturePendingApproval?: boolean;
  keyframeApproved?: boolean;
  rawKf?: string;
  allowedStill?: string;
}): string | undefined {
  if (opts.pictureUnusable) return undefined;
  if (opts.picturePendingApproval || opts.keyframeApproved) return opts.rawKf || opts.allowedStill || undefined;
  return opts.allowedStill || undefined;
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

export function ShotProductionPreview({
  kind,
  src,
  label,
  title,
  pendingApproval,
  videoRef,
  tone = 'staff',
}: {
  kind: ShotPreviewKind;
  src?: string;
  label: string;
  title?: string;
  pendingApproval?: boolean;
  videoRef?: Ref<HTMLVideoElement>;
  tone?: 'director' | 'staff';
}) {
  const localRef = useRef<HTMLVideoElement>(null);
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
        <p className="fx-desk__note">{director ? 'Chưa có hình' : SHOT_PREVIEW_LABEL.empty}</p>
      </div>
    );
  }
  if (kind === 'keyframe') {
    return (
      <figure className="fx-media-card" style={{ margin: 0 }}>
        <img src={src} alt={title || 'Hình'} className="fx-media-card__still" />
        <figcaption className="fx-desk__note">
          {director ? 'Hình' : 'Hình ảnh Keyframe'}{pendingApproval ? ' · Chờ duyệt' : ''}
        </figcaption>
      </figure>
    );
  }
  return (
    <figure className="fx-media-card" style={{ margin: 0 }}>
      <video
        key={src}
        ref={videoRef || localRef}
        src={src}
        controls
        playsInline
        preload="metadata"
        className="fx-media-card__clip"
      />
      <figcaption className="fx-desk__note">{caption}</figcaption>
    </figure>
  );
}
