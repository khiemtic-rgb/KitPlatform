import { useCallback, useEffect, useMemo, useState } from 'react';
import { App, Input, Select, Spin, Typography } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { fetchWarehouses } from '@/shared/api/sales.api';
import { fetchStockBatches, fetchStockProducts } from '@/shared/api/inventory.api';
import type { StockBatch, StockProductSummary } from '@/shared/api/inventory.types';
import type { Warehouse } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { usePosSession } from '@/modules/pos/pos-session.store';

function warehouseOptionLabel(w: Warehouse) {
  return w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName;
}

function formatQty(value: number): string {
  return value.toLocaleString('vi-VN');
}

export function StockLookupPage() {
  const { message } = App.useApp();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<StockProductSummary[]>([]);
  const [selected, setSelected] = useState<StockProductSummary | null>(null);
  const [batches, setBatches] = useState<StockBatch[]>([]);
  const [searching, setSearching] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    void fetchWarehouses()
      .then((wh) => {
        setWarehouses(wh);
        const preferred =
          posWarehouseId && wh.some((w) => w.id === posWarehouseId) ? posWarehouseId : wh[0]?.id ?? null;
        setWarehouseId(preferred);
      })
      .catch((error) => message.error(apiErrorMessage(error, 'Không tải được kho')));
  }, [message, posWarehouseId]);

  const activeWarehouse = useMemo(
    () => warehouses.find((w) => w.id === warehouseId),
    [warehouses, warehouseId],
  );

  const loadProducts = useCallback(
    async (search: string, whId: string) => {
      setSearching(true);
      setSelected(null);
      setBatches([]);
      try {
        const result = await fetchStockProducts({
          warehouseId: whId,
          search: search.trim() || undefined,
          page: 1,
          pageSize: 40,
        });
        setItems(result.items);
      } catch (error) {
        setItems([]);
        message.error(apiErrorMessage(error, 'Không tra được tồn kho'));
      } finally {
        setSearching(false);
      }
    },
    [message],
  );

  useEffect(() => {
    if (!warehouseId) {
      setItems([]);
      return;
    }
    const timer = window.setTimeout(() => {
      void loadProducts(query, warehouseId);
    }, query.trim().length === 0 ? 0 : 280);
    return () => window.clearTimeout(timer);
  }, [query, warehouseId, loadProducts]);

  const openProduct = async (product: StockProductSummary) => {
    if (!warehouseId) return;
    if (selected?.productId === product.productId) {
      setSelected(null);
      setBatches([]);
      return;
    }
    setSelected(product);
    setDetailLoading(true);
    try {
      const result = await fetchStockBatches({
        warehouseId,
        productId: product.productId,
        page: 1,
        pageSize: 50,
      });
      setBatches(result.items.filter((b) => b.quantityAvailable > 0));
    } catch (error) {
      setBatches([]);
      message.error(apiErrorMessage(error, 'Không tải được lô'));
    } finally {
      setDetailLoading(false);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader title="Tra tồn" backTo="/" />
      <main className="staff-body">
        <Select
          size="large"
          className="stock-warehouse-select"
          placeholder="Chọn kho"
          value={warehouseId ?? undefined}
          onChange={(id) => {
            setWarehouseId(id);
            setQuery('');
            setSelected(null);
            setBatches([]);
          }}
          options={warehouses.map((w) => ({ value: w.id, label: warehouseOptionLabel(w) }))}
          style={{ width: '100%', marginBottom: 12 }}
        />
        <Input
          size="large"
          allowClear
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tên, mã SP, barcode…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={!warehouseId}
        />
        <Typography.Text type="secondary" style={{ display: 'block', margin: '8px 0 12px', fontSize: 12 }}>
          {activeWarehouse
            ? `Đang xem tồn: ${warehouseOptionLabel(activeWarehouse)} · chỉ tra cứu, không thêm giỏ.`
            : 'Chọn kho để tra tồn các quầy khác (VD: quầy 2 xem tồn quầy 1).'}
        </Typography.Text>

        {searching ? (
          <div style={{ textAlign: 'center', padding: 16 }}>
            <Spin />
          </div>
        ) : null}

        {!searching && warehouseId && items.length === 0 ? (
          <Typography.Text type="secondary">
            {query.trim()
              ? 'Không tìm thấy sản phẩm có tồn khớp từ khóa.'
              : 'Kho này chưa có tồn (hoặc không có quyền xem).'}
          </Typography.Text>
        ) : null}

        {!searching
          ? items.map((item) => {
              const active = selected?.productId === item.productId;
              return (
                <div key={item.productId} className="stock-hit-wrap">
                  <button
                    type="button"
                    className={`search-hit stock-hit${active ? ' is-active' : ''}`}
                    onClick={() => void openProduct(item)}
                  >
                    <Typography.Text strong>{item.productName}</Typography.Text>
                    <div className="stock-hit-meta">
                      <span>
                        {item.productCode}
                        {item.saleUnitName ? ` · ${item.saleUnitName}` : ''}
                      </span>
                      <span className="stock-hit-qty">Tồn {formatQty(item.totalQuantity)}</span>
                    </div>
                  </button>

                  {active ? (
                    <div className="stock-hit-detail">
                      <div className="stock-hit-detail__total">
                        <strong>Tồn tổng:</strong> {formatQty(selected.totalQuantity)}
                        {selected.saleUnitName ? ` ${selected.saleUnitName}` : ''}
                      </div>
                      {detailLoading ? (
                        <div style={{ textAlign: 'center', padding: 8 }}>
                          <Spin size="small" />
                        </div>
                      ) : batches.length > 0 ? (
                        <div className="stock-hit-detail__batches">
                          <Typography.Text strong style={{ fontSize: 12 }}>
                            Lô trong kho
                          </Typography.Text>
                          {batches.map((batch) => (
                            <div key={batch.id} className="stock-batch-row">
                              <span>
                                {batch.batchNumber}
                                {batch.expiryDate
                                  ? ` · HSD ${dayjs(batch.expiryDate).format('MM/YYYY')}`
                                  : ''}
                              </span>
                              <span>{formatQty(batch.quantityAvailable)}</span>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Không có lô còn số lượng.
                        </Typography.Text>
                      )}
                    </div>
                  ) : null}
                </div>
              );
            })
          : null}
      </main>
    </div>
  );
}
