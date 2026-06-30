import { useTranslation } from 'react-i18next';
import { AutoComplete, Button, Col, Input, Row, Select, Space } from 'antd';
import { DownloadOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import type { DefaultOptionType } from 'antd/es/select';
import type { Supplier, SupplierPaymentListFilters } from '@/shared/api/procurement.types';
import { useProcurementEnums } from '@/shared/i18n/use-procurement-enums';
import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';

interface SupplierPaymentFilterBarProps {
  filters: SupplierPaymentListFilters;
  searchInput: string;
  searchSuggestions: DefaultOptionType[];
  suppliers: Supplier[];
  loading?: boolean;
  onSearchInputChange: (value: string) => void;
  onFiltersChange: (filters: SupplierPaymentListFilters) => void;
  onApply: (filters: SupplierPaymentListFilters, search: string) => void;
  onReset: () => void;
  onExport: () => void;
}

export function SupplierPaymentFilterBar({
  filters,
  searchInput,
  searchSuggestions,
  suppliers,
  loading,
  onSearchInputChange,
  onFiltersChange,
  onApply,
  onReset,
  onExport,
}: SupplierPaymentFilterBarProps) {
  const { t } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const { supplierPaymentStatusOptions } = useProcurementEnums();
  const applyNow = (next: SupplierPaymentListFilters, search = searchInput) => onApply(next, search);

  return (
    <div style={{ marginBottom: 16 }}>
      <Row gutter={[12, 12]}>
        <Col xs={24} md={12} lg={6}>
          <AutoComplete
            style={{ width: '100%' }}
            options={searchSuggestions}
            value={searchInput}
            onSelect={(value) => {
              onSearchInputChange(String(value));
              onApply(filters, String(value));
            }}
            onChange={(value) => onSearchInputChange(value)}
          >
            <Input
              allowClear
              placeholder={t('filters.searchPayment')}
              onPressEnter={() => onApply(filters, searchInput)}
            />
          </AutoComplete>
        </Col>
        <Col xs={24} md={12} lg={6}>
          <Select
            allowClear
            showSearch
            placeholder={t('filters.supplier')}
            style={{ width: '100%' }}
            optionFilterProp="label"
            value={filters.supplierId}
            onChange={(supplierId) => applyNow({ ...filters, supplierId })}
            options={suppliers.map((s) => ({
              value: s.id,
              label: `${s.supplierCode} — ${s.supplierName}`,
            }))}
          />
        </Col>
        <Col xs={24} md={12} lg={4}>
          <Select
            allowClear
            placeholder={t('filters.status')}
            style={{ width: '100%' }}
            value={filters.status}
            onChange={(status) => applyNow({ ...filters, status })}
            options={supplierPaymentStatusOptions}
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <div style={{ display: 'flex', gap: 8, width: '100%' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <PharmaDatePicker
                placeholder={t('filters.dateFrom')}
                value={filters.dateFrom}
                onChange={(dateFrom) => onFiltersChange({ ...filters, dateFrom: dateFrom || undefined })}
              />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <PharmaDatePicker
                placeholder={t('filters.dateTo')}
                value={filters.dateTo}
                onChange={(dateTo) => onFiltersChange({ ...filters, dateTo: dateTo || undefined })}
              />
            </div>
          </div>
        </Col>
        <Col xs={24}>
          <Space wrap>
            <Button
              type="primary"
              icon={<SearchOutlined />}
              loading={loading}
              onClick={() => onApply(filters, searchInput)}
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
