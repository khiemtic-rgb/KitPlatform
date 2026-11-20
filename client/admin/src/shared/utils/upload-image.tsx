import { useEffect, useState } from 'react';
import { Avatar } from 'antd';
import { useAuthStore } from '@/shared/auth/auth.store';
import { adminUploadFetchPlan } from '@/shared/utils/upload-url';

export function useAuthedResourceUrl(url: string | null | undefined): string | undefined {
  const token = useAuthStore((s) => s.accessToken);
  const [src, setSrc] = useState<string | undefined>();

  useEffect(() => {
    if (!url?.trim()) {
      setSrc(undefined);
      return;
    }

    const plan = adminUploadFetchPlan(url, token);
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

export function AuthedUploadAvatar(props: {
  url?: string | null;
  size?: number;
  shape?: 'circle' | 'square';
  emptyLabel?: string;
}) {
  const src = useAuthedResourceUrl(props.url);
  const shape = props.shape ?? 'square';
  const size = props.size ?? 40;
  if (!props.url?.trim()) {
    return (
      <Avatar shape={shape} size={size}>
        {props.emptyLabel ?? '—'}
      </Avatar>
    );
  }
  return <Avatar shape={shape} size={size} src={src} alt="" />;
}
