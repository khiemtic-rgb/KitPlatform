import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { filterProductNavTabs } from '@/shared/product/product-phases';

/** Ẩn tab disabled + redirect khi truy cập URL feature đã tắt */
export function useProductNavGuard(allTabs: ProductNavTab[], fallbackPath: string) {
  const location = useLocation();
  const navigate = useNavigate();
  const visibleTabs = filterProductNavTabs(allTabs);

  useEffect(() => {
    const matched = [...allTabs]
      .filter((t) => location.pathname.startsWith(t.path))
      .sort((a, b) => b.path.length - a.path.length)[0];
    if (matched?.feature && !visibleTabs.some((t) => t.key === matched.key)) {
      navigate(fallbackPath, { replace: true });
    }
  }, [allTabs, visibleTabs, location.pathname, navigate, fallbackPath]);

  return visibleTabs;
}
