import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppstoreOutlined,
  CalculatorOutlined,
  CloudSyncOutlined,
  ExperimentOutlined,
  FolderOutlined,
  TagOutlined,
} from '@ant-design/icons';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

export function CatalogLayout() {
  const { t } = useTranslation('catalog', { keyPrefix: 'catalogLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      {
        key: 'products',
        label: t('products'),
        path: '/catalog/products',
        icon: <AppstoreOutlined />,
      },
      {
        key: 'categories',
        label: t('categories'),
        path: '/catalog/categories',
        icon: <FolderOutlined />,
      },
      {
        key: 'brands',
        label: t('brands'),
        path: '/catalog/brands',
        icon: <TagOutlined />,
        feature: 'catalog.brands',
      },
      {
        key: 'units',
        label: t('units'),
        path: '/catalog/units',
        icon: <CalculatorOutlined />,
      },
      {
        key: 'ingredients',
        label: t('ingredients'),
        path: '/catalog/ingredients',
        icon: <ExperimentOutlined />,
        feature: 'catalog.ingredients',
      },
      {
        key: 'national-drugs',
        label: t('nationalDrugs'),
        path: '/catalog/national-drugs',
        icon: <CloudSyncOutlined />,
        feature: 'catalog.nationalDrug',
      },
    ],
    [t],
  );

  const tabs = useProductNavGuard(allTabs, '/catalog/products');

  useEffect(() => {
    if (location.pathname === '/catalog' || location.pathname === '/catalog/') {
      navigate('/catalog/products', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    location.pathname.startsWith('/catalog/import')
      ? 'products'
      : tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'products';

  useRegisterProductNavSubnav(tabs, activeKey, (tab) => navigate(tab.path));

  return <Outlet />;
}
