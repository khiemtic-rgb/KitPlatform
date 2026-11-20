import { useEffect, useState } from 'react';
import { useSessionStore } from '@/shared/auth/session.store';
import { familyEvidenceFetchPlan } from '@/shared/upload/evidence-url';

export function useEvidenceMediaUrl(url: string | null | undefined): {
  src?: string;
  pending: boolean;
} {
  const token = useSessionStore((s) => s.accessToken);
  const [src, setSrc] = useState<string | undefined>();
  const [pending, setPending] = useState(false);

  useEffect(() => {
    if (!url?.trim()) {
      setSrc(undefined);
      setPending(false);
      return;
    }

    const plan = familyEvidenceFetchPlan(url, token);
    if (!plan.authenticated || !plan.headers) {
      setSrc(plan.url);
      setPending(false);
      return;
    }

    setPending(true);
    setSrc(undefined);
    let cancelled = false;
    let objectUrl: string | undefined;
    void fetch(plan.url, { headers: plan.headers, credentials: 'same-origin' })
      .then(async (response) => {
        if (!response.ok) throw new Error('evidence');
        const blob = await response.blob();
        objectUrl = URL.createObjectURL(blob);
        if (cancelled) URL.revokeObjectURL(objectUrl);
        else {
          setSrc(objectUrl);
          setPending(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSrc(undefined);
          setPending(false);
        }
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [url, token]);

  return { src, pending };
}

export function EvidenceAudio(props: { url?: string | null; className?: string }) {
  const media = useEvidenceMediaUrl(props.url);
  if (!media.src) return null;
  return <audio controls src={media.src} className={props.className} />;
}
