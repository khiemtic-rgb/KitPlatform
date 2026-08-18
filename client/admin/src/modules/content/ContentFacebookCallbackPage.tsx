import { useEffect, useState } from 'react';
import { Result, Spin, Typography } from 'antd';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import { completeFacebookOAuth } from '@/shared/api/content.api';

const PENDING_KEY = 'kit.content.fbPending';
export const FB_RETURN_KEY = 'kit.content.fbReturn';

export function ContentFacebookCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
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
    let cancelled = false;
    void (async () => {
      try {
        const pending = await completeFacebookOAuth(code, state);
        sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));
        if (!cancelled) navigate(`/content/brands?fbBrand=${pending.brandId}`, { replace: true });
      } catch (e) {
        if (!cancelled) setError(apiErrorMessage(e, 'Không hoàn tất kết nối Facebook.'));
      }
    })();
    return () => {
      cancelled = true;
    };
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
