import { useTranslation } from 'react-i18next';
import { Button, Checkbox, Col, Input, Row, Select, Space } from 'antd';
import { DownloadOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import type { ProductListItem } from '@/shared/api/catalog.types';
import type { Warehouse } from '@/shared/api/inventory.types';
import type { PurchaseOrderListFilters, Supplier } from '@/shared/api/procurement.types';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

interface PurchaseOrderFilterBarProps {
  filters: PurchaseOrderListFilters;
  searchInput: string;
  suppliers: Supplier[];
  warehouses: Warehouse[];
  products: ProductListItem[];
  loading?: boolean;
  onSearchInputChange: (value: string) => void;
  onApply: (filters: PurchaseOrderListFilters, search: string) => void;
  onReset: () => void;
  onExport: () => void;
}

export function PurchaseOrderFilterBar({
  filters,
  searchInput,
  suppliers,
  warehouses,
  products,
  loading,
  onSearchInputChange,
  onApply,
  onReset,
  onExport,
}: PurchaseOrderFilterBarProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { poStatusOptions } = useProcurementEnums();
  const apply = (next: PurchaseOrderListFilters, search = searchInput) => onApply(next, search);

  return (
    <div style={{ marginBottom: 16 }}>
      <Row gutter={[12, 12]}>
        <Col xs={24} md={12} lg={6}>
          <Input
            allowClear
            placeholder={t('filters.searchPo')}
            value={searchInput}
            onChange={(e) => onSearchInputChange(e.target.value)}
            onPressEnter={() => onApply(filters, searchInput)}
          />
        </Col>
        <Col xs={24} md={12} lg={4}>
          <Select
            allowClear
            placeholder={t('filters.status')}
            style={{ width: '100%' }}
            value={filters.status}
            onChange={(status) => apply({ ...filters, status })}
            options={poStatusOptions}
          />
        </Col>
        <Col xs={24} md={12} lg={6}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.supplier')}
            style={{ width: '100%' }}
            value={filters.supplierId}
            onChange={(supplierId) => apply({ ...filters, supplierId })}
            options={suppliers.map((s) => ({
              value: s.id,
              label: `${s.supplierCode} — ${s.supplierName}`,
            }))}
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.warehouse')}
            style={{ width: '100%' }}
            value={filters.warehouseId}
            onChange={(warehouseId) => apply({ ...filters, warehouseId })}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
        </Col>
        <Col xs={24} lg={8}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.productInOrder')}
            style={{ width: '100%' }}
            value={filters.productId}
            onChange={(productId) => apply({ ...filters, productId })}
            options={products.map((p) => ({
              value: p.id,
              label: `${p.productCode} — ${p.productName}`,
            }))}
          />
        </Col>
        <Col xs={24} lg={8}>
          <div style={{ display: 'flex', gap: 8, width: '100%' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <PharmaDatePicker
                placeholder={t('filters.dateFrom')}
                value={filters.dateFrom}
                onChange={(dateFrom) => apply({ ...filters, dateFrom: dateFrom || undefined })}
              />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <PharmaDatePicker
                placeholder={t('filters.dateTo')}
                value={filters.dateTo}
                onChange={(dateTo) => apply({ ...filters, dateTo: dateTo || undefined })}
              />
            </div>
          </div>
        </Col>
        <Col xs={24} lg={8}>
          <Space wrap size={[20, 8]} align="center" style={{ minHeight: 32 }}>
            <Checkbox
              checked={!!filters.pendingReceiptOnly}
              onChange={(e) => apply({ ...filters, pendingReceiptOnly: e.target.checked })}
            >
              {t('filters.pendingReceiptOnly')}
            </Checkbox>
            <Checkbox
              checked={!!filters.includeArchived}
              onChange={(e) => apply({ ...filters, includeArchived: e.target.checked })}
            >
              {t('showArchived')}
            </Checkbox>
          </Space>
        </Col>
        <Col xs={24}>
          <Space wrap>
            <Button
              type="primary"
              icon={<SearchOutlined />}
              onClick={() => onApply(filters, searchInput)}
              loading={loading}
            >
              {tCommon('filter')}
            </Button>
            <Button onClick={onReset}>{t('clearFilters')}</Button>
            <Button icon={<ReloadOutlined />} onClick={() => onApply(filters, searchInput)} loading={loading}>
              {tCommon('reload')}
            </Button>
            <Button icon={<DownloadOutlined />} onClick={onExport} disabled={loading}>
              {t('exportExcel')}
            </Button>
          </Space>
        </Col>
      </Row>
    </div>
  );
}
