import { useState } from 'react';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';

/** Soft illustration when evidence / memory photo URL is missing or 404s. */
export const MEMORY_PLACEHOLDER_SRC = '/mascot/family-moment-1.png';

/** Image that falls back to a soft photo (or emoji) when the evidence URL 404s or is broken. */
export function SoftEvidenceImg(props: {
  url?: string | null;
  /** Emoji used only when `emojiFallback` is true. */
  fallback?: string;
  /** Override placeholder illustration. Default: family moment soft art. */
  fallbackSrc?: string;
  /**
   * Prefer emoji span instead of the soft placeholder photo.
   * Default false — missing/broken URLs still look like a real thumbnail.
   */
  emojiFallback?: boolean;
  className?: string;
  fallbackClassName?: string;
  auth?: (url?: string | null) => string | undefined;
}) {
  const resolve = props.auth ?? withEvidenceAuth;
  const src = props.url ? resolve(props.url) : undefined;
  const [failed, setFailed] = useState(false);
  const [seenSrc, setSeenSrc] = useState(src);
  if (seenSrc !== src) {
    setSeenSrc(src);
    setFailed(false);
  }

  const imgClass = props.className ?? props.fallbackClassName;

  if (!src || failed) {
    if (props.emojiFallback) {
      return (
        <span className={props.fallbackClassName ?? props.className} aria-hidden>
          {props.fallback ?? '📷'}
        </span>
      );
    }
    return (
      <img
        src={props.fallbackSrc ?? MEMORY_PLACEHOLDER_SRC}
        alt=""
        className={imgClass}
        decoding="async"
      />
    );
  }

  return (
    <img
      src={src}
      alt=""
      className={imgClass}
      decoding="async"
      onError={() => setFailed(true)}
    />
  );
}
