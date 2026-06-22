import { Button, Checkbox, Col, Input, Row, Select, Space } from 'antd';

import { DownloadOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';

import type { ProductListItem } from '@/shared/api/catalog.types';

import type { Warehouse } from '@/shared/api/inventory.types';

import type {

  GoodsReceiptListFilters,

  PurchaseOrderListItem,

  Supplier,

} from '@/shared/api/procurement.types';

import { GRN_STATUS_LABELS } from '@/shared/api/procurement.types';

import { PharmaDatePicker } from '@/shared/ui/PharmaDatePicker';



interface GoodsReceiptFilterBarProps {

  filters: GoodsReceiptListFilters;

  searchInput: string;

  suppliers: Supplier[];

  warehouses: Warehouse[];

  products: ProductListItem[];

  purchaseOrders: PurchaseOrderListItem[];

  loading?: boolean;

  onSearchInputChange: (value: string) => void;

  onApply: (filters: GoodsReceiptListFilters, search: string) => void;

  onReset: () => void;

  onExport: () => void;

}



export function GoodsReceiptFilterBar({

  filters,

  searchInput,

  suppliers,

  warehouses,

  products,

  purchaseOrders,

  loading,

  onSearchInputChange,

  onApply,

  onReset,

  onExport,

}: GoodsReceiptFilterBarProps) {

  const apply = (next: GoodsReceiptListFilters, search = searchInput) => onApply(next, search);



  return (

    <div style={{ marginBottom: 16 }}>

      <Row gutter={[12, 12]}>

        <Col xs={24} md={12} lg={6}>

          <Input

            allowClear

            placeholder="Số phiếu, số PO, NCC"

            value={searchInput}

            onChange={(e) => onSearchInputChange(e.target.value)}

            onPressEnter={() => onApply(filters, searchInput)}

          />

        </Col>

        <Col xs={24} md={12} lg={4}>

          <Select

            allowClear

            placeholder="Trạng thái"

            style={{ width: '100%' }}

            value={filters.status}

            onChange={(status) => apply({ ...filters, status })}

            options={Object.entries(GRN_STATUS_LABELS).map(([value, label]) => ({

              value: Number(value),

              label,

            }))}

          />

        </Col>

        <Col xs={24} md={12} lg={6}>

          <Select

            allowClear

            showSearch

            optionFilterProp="label"

            placeholder="Nhà cung cấp"

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

            placeholder="Kho nhận"

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

            placeholder="Theo đơn đặt hàng"

            style={{ width: '100%' }}

            value={filters.purchaseOrderId}

            onChange={(purchaseOrderId) => apply({ ...filters, purchaseOrderId })}

            options={purchaseOrders.map((po) => ({

              value: po.id,

              label: `${po.poNumber} — ${po.supplierName}`,

            }))}

          />

        </Col>

        <Col xs={24} lg={8}>

          <Select

            allowClear

            showSearch

            optionFilterProp="label"

            placeholder="Sản phẩm trong phiếu"

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

                placeholder="Từ ngày"

                value={filters.dateFrom}

                onChange={(dateFrom) => apply({ ...filters, dateFrom: dateFrom || undefined })}

              />

            </div>

            <div style={{ flex: 1, minWidth: 0 }}>

              <PharmaDatePicker

                placeholder="Đến ngày"

                value={filters.dateTo}

                onChange={(dateTo) => apply({ ...filters, dateTo: dateTo || undefined })}

              />

            </div>

          </div>

        </Col>

        <Col xs={24} lg={8}>

          <Checkbox

            checked={!!filters.includeArchived}

            onChange={(e) => apply({ ...filters, includeArchived: e.target.checked })}

          >

            Hiện bản ghi đã ẩn

          </Checkbox>

        </Col>

        <Col xs={24}>

          <Space wrap>

            <Button

              type="primary"

              icon={<SearchOutlined />}

              onClick={() => onApply(filters, searchInput)}

              loading={loading}

            >

              Lọc

            </Button>

            <Button onClick={onReset}>Xóa lọc</Button>

            <Button icon={<ReloadOutlined />} onClick={() => onApply(filters, searchInput)} loading={loading}>

              Tải lại

            </Button>

            <Button icon={<DownloadOutlined />} onClick={onExport} disabled={loading}>

              Xuất Excel

            </Button>

          </Space>

        </Col>

      </Row>

    </div>

  );

}

