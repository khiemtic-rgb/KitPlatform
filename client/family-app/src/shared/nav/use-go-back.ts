import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

/** React Router giữ `idx` trong history.state; idx > 0 nghĩa là còn trang trước trong phiên app. */
export function hasInAppHistory(): boolean {
  if (typeof window === 'undefined') return false;
  const idx = (window.history.state as { idx?: number } | null)?.idx;
  return typeof idx === 'number' && idx > 0;
}

/** Nút "Quay lại": ưu tiên trở về đúng trang trước, chỉ dùng `fallback` khi vào thẳng bằng link. */
export function useGoBack(fallback: string) {
  const navigate = useNavigate();
  return useCallback(() => {
    if (hasInAppHistory()) navigate(-1);
    else navigate(fallback, { replace: true });
  }, [navigate, fallback]);
}
