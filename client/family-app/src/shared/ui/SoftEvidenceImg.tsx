import { useState } from 'react';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';

/** Image that falls back to emoji/icon when the evidence URL 404s or is broken. */
export function SoftEvidenceImg(props: {
  url?: string | null;
  fallback: string;
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
  if (!src || failed) {
    return (
      <span className={props.fallbackClassName} aria-hidden>
        {props.fallback}
      </span>
    );
  }
  return (
    <img
      src={src}
      alt=""
      className={props.className}
      onError={() => setFailed(true)}
    />
  );
}
