import { useEffect, useRef, useState } from 'react';
import { Result, Spin, Typography } from 'antd';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { completeFacebookOAuth } from '@/shared/api/content.api';

const PENDING_KEY = 'kit.content.fbPending';
export const FB_RETURN_KEY = 'kit.content.fbReturn';

type FacebookPending = { sessionId?: string; brandId?: string };

function readPending(): FacebookPending | null {
  const raw = sessionStorage.getItem(PENDING_KEY);
  if (!raw) return null;
  try {
    const pending = JSON.parse(raw) as FacebookPending;
    if (pending.sessionId && pending.brandId) return pending;
  } catch {
    return null;
  }
  return null;
}

export function ContentFacebookCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  // Mã OAuth chỉ dùng được một lần. StrictMode chạy effect lần hai sẽ báo phiên hết hạn.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    const existing = readPending();
    if (existing?.brandId) {
      navigate(`/content/brands?fbBrand=${existing.brandId}`, { replace: true });
      return;
    }

    const denied = params.get('error_description') || params.get('error');
    if (denied) {
      setError(denied);
      return;
    }
    const code = params.get('code');
    const state = params.get('state');
    if (!code || !state) {
      setError('Facebook không trả mã OAuth.');
      return;
    }
    void (async () => {
      try {
        const pending = await completeFacebookOAuth(code, state);
        sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));
        navigate(`/content/brands?fbBrand=${pending.brandId}`, { replace: true });
      } catch (e) {
        const saved = readPending();
        if (saved?.brandId) {
          navigate(`/content/brands?fbBrand=${saved.brandId}`, { replace: true });
          return;
        }
        setError(apiErrorMessage(e, 'Không hoàn tất kết nối Facebook.'));
      }
    })();
  }, [navigate, params]);

  if (error) {
    return (
      <Result
        status="warning"
        title="Chưa kết nối được Facebook"
        subTitle={error}
        extra={
          <Typography.Link onClick={() => navigate('/content/brands')}>Về Thương hiệu</Typography.Link>
        }
      />
    );
  }

  return (
    <div style={{ padding: 48, textAlign: 'center' }}>
      <Spin size="large" />
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
        Đang đổi token và lấy danh sách Page…
      </Typography.Paragraph>
    </div>
  );
}

export const FB_PENDING_KEY = PENDING_KEY;
