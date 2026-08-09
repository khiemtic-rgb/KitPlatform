import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Spin, Typography, message } from 'antd';
import { createSubmission, rememberPartnerRef } from '@/shared/api/assessment.api';

const { Text } = Typography;

/** Deep-link entry: create a draft submission then open the questionnaire. */
export function StartPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [failed, setFailed] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    rememberPartnerRef(searchParams.get('ref') ?? searchParams.get('partner'));
  }, [searchParams]);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    let cancelled = false;
    (async () => {
      try {
        const sub = await createSubmission();
        if (cancelled) return;
        navigate(`/survey/${sub.id}`, { replace: true });
      } catch {
        if (cancelled) return;
        message.error('Không thể bắt đầu. Vui lòng thử lại.');
        setFailed(true);
        navigate('/', { replace: true });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [navigate]);

  if (failed) return null;

  return (
    <div className="page-shell" style={{ display: 'grid', placeItems: 'center', minHeight: '60vh' }}>
      <div style={{ textAlign: 'center' }}>
        <Spin size="large" />
        <Text type="secondary" style={{ display: 'block', marginTop: 16 }}>
          Đang mở bài đánh giá…
        </Text>
      </div>
    </div>
  );
}
