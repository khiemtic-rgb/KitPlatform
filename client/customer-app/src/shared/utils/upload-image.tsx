import { useEffect, useState } from 'react';
import { useAuthStore } from '@/shared/auth/auth.store';
import { customerUploadFetchPlan } from '@/shared/utils/upload-url';

export function useCustomerUploadUrl(url: string | null | undefined): string | undefined {
  const token = useAuthStore((s) => s.accessToken);
  const [src, setSrc] = useState<string | undefined>();

  useEffect(() => {
    if (!url?.trim()) {
      setSrc(undefined);
      return;
    }

    const plan = customerUploadFetchPlan(url, token);
    if (!plan.authenticated || !plan.headers) {
      setSrc(plan.url);
      return;
    }

    let cancelled = false;
    let objectUrl: string | undefined;
    void fetch(plan.url, { headers: plan.headers, credentials: 'same-origin' })
      .then(async (response) => {
        if (!response.ok) throw new Error('upload');
        const blob = await response.blob();
        objectUrl = URL.createObjectURL(blob);
        if (cancelled) URL.revokeObjectURL(objectUrl);
        else setSrc(objectUrl);
      })
      .catch(() => {
        if (!cancelled) setSrc(undefined);
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [url, token]);

  return src;
}

export function CustomerUploadImage(props: { url?: string | null; className?: string; alt?: string }) {
  const src = useCustomerUploadUrl(props.url);
  if (!src) return null;
  return <img className={props.className} src={src} alt={props.alt ?? ''} />;
}
