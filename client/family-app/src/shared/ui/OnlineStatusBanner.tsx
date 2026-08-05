import { useEffect, useState } from 'react';

/** v1 Family OS online-only — hiện khi mất mạng. */
export function OnlineStatusBanner() {
  const [online, setOnline] = useState(
    typeof navigator === 'undefined' ? true : navigator.onLine,
  );

  useEffect(() => {
    const on = () => setOnline(true);
    const off = () => setOnline(false);
    window.addEventListener('online', on);
    window.addEventListener('offline', off);
    return () => {
      window.removeEventListener('online', on);
      window.removeEventListener('offline', off);
    };
  }, []);

  if (online) return null;

  return (
    <div className="famixa-offline-banner" role="status">
      Không có mạng — Famixa cần kết nối để tải lịch / lưu việc. Kiểm tra Wi‑Fi hoặc 4G rồi thử
      lại.
    </div>
  );
}
