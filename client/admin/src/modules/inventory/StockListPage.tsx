import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  AutoComplete,
  Button,
  Card,
  Descriptions,
  Drawer,
  Input,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchStockBatches, fetchStockProducts, fetchWarehouses } from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { StockBatch, StockProductSummary, Warehouse } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

type StockTab = 'summary' | 'fefo';

function formatQty(value: number): string {
  return value.toLocaleString('vi-VN');
}

export function StockListPage() {
  const [searchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState<StockTab>(() =>
    searchParams.get('tab') === 'fefo' ? 'fefo' : 'summary',
  );
  const [loading, setLoading] = useState(false);
  const [summaryItems, setSummaryItems] = useState<StockProductSummary[]>([]);
  const [fefoItems, setFefoItems] = useState<StockBatch[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string | undefined>();
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [fefoProductId, setFefoProductId] = useState<string | undefined>();
  const [fefoProductLabel, setFefoProductLabel] = useState<string | undefined>();
  const [suggestionProducts, setSuggestionProducts] = useState<ProductListItem[]>([]);
  const [suggestionBatches, setSuggestionBatches] = useState<StockBatch[]>([]);
  const [detailProduct, setDetailProduct] = useState<StockProductSummary | null>(null);
  const [detailBatches, setDetailBatches] = useState<StockBatch[]>([]);
  const [detailLoading, setDetailLoading] = useState(false);

  const loadWarehouses = useCallback(async () => {
    try {
      setWarehouses(await fetchWarehouses());
    } catch {
      /* optional */
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      if (activeTab === 'summary') {
        const result = await fetchStockProducts({
          warehouseId,
          search: search || undefined,
          page,
          pageSize,
        });
        setSummaryItems(result.items);
        setTotal(result.total);
      } else {
        const result = await fetchStockBatches({
          warehouseId,
          productId: fefoProductId,
          search: fefoProductId ? undefined : search || undefined,
          page,
          pageSize,
        });
        setFefoItems(result.items);
        setTotal(result.total);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tồn kho'));
    } finally {
      setLoading(false);
    }
  }, [activeTab, warehouseId, search, fefoProductId, page, pageSize]);

  useEffect(() => {
    loadWarehouses();
    void (async () => {
      try {
        const [catalog, batches] = await Promise.all([
          fetchProducts({ page: 1, pageSize: 200 }),
          fetchStockBatches({ page: 1, pageSize: 200 }),
        ]);
        setSuggestionProducts(catalog.items ?? []);
        setSuggestionBatches(batches.items ?? []);
      } catch {
        /* gợi ý tùy chọn */
      }
    })();
  }, [loadWarehouses]);

  useEffect(() => {
    const tab = searchParams.get('tab');
    if (tab === 'fefo') setActiveTab('fefo');
    else if (tab === 'summary') setActiveTab('summary');
  }, [searchParams]);

  useEffect(() => {
    void load();
  }, [load]);

  const searchSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const productOpts = suggestionProducts
      .filter((p) => {
        if (!q) return true;
        return (
          p.productCode.toLowerCase().includes(q) ||
          p.productName.toLowerCase().includes(q) ||
          (p.primaryBarcode?.toLowerCase().includes(q) ?? false)
        );
      })
      .slice(0, 12)
      .map((p) => ({
        value: p.primaryBarcode || p.productCode || p.productName,
        label: `${p.productCode} — ${p.productName}${p.primaryBarcode ? ` · ${p.primaryBarcode}` : ''}`,
      }));

    const seenBatches = new Set<string>();
    const batchOpts = suggestionBatches
      .filter((b) => {
        if (seenBatches.has(b.batchNumber)) return false;
        seenBatches.add(b.batchNumber);
        if (!q) return true;
        return (
          b.batchNumber.toLowerCase().includes(q) ||
          b.productCode.toLowerCase().includes(q) ||
          b.productName.toLowerCase().includes(q)
        );
      })
      .slice(0, 8)
      .map((b) => ({
        value: b.batchNumber,
        label: `Lô ${b.batchNumber} — ${b.productName}`,
      }));

    return [...productOpts, ...(activeTab === 'fefo' ? batchOpts : [])].slice(0, 20);
  }, [suggestionProducts, suggestionBatches, searchInput, activeTab]);

  const applySearch = (value?: string) => {
    const text = (value ?? searchInput).trim();
    setSearchInput(text);
    setPage(1);
    setSearch(text);
    if (activeTab === 'fefo') {
      setFefoProductId(undefined);
      setFefoProductLabel(undefined);
    }
  };

  const openProductDetail = async (product: StockProductSummary) => {
    setDetailProduct(product);
    setDetailLoading(true);
    setDetailBatches([]);
    try {
      const result = await fetchStockBatches({
        warehouseId,
        productId: product.productId,
        page: 1,
        pageSize: 100,
      });
      setDetailBatches(result.items);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết lô'));
    } finally {
      setDetailLoading(false);
    }
  };

  const jumpToFefoTab = (product: StockProductSummary) => {
    setFefoProductId(product.productId);
    setFefoProductLabel(`${product.productCode} — ${product.productName}`);
    setSearch('');
    setSearchInput('');
    setPage(1);
    setActiveTab('fefo');
    setDetailProduct(null);
  };

  const clearFefoProductFilter = () => {
    setFefoProductId(undefined);
    setFefoProductLabel(undefined);
    setPage(1);
  };

  const batchesByWarehouse = useMemo(() => {
    const map = new Map<
      string,
      { warehouseName: string; batches: StockBatch[]; subtotal: number }
    >();
    for (const batch of detailBatches) {
      const group = map.get(batch.warehouseId);
      if (group) {
        group.batches.push(batch);
        group.subtotal += batch.quantityAvailable;
      } else {
        map.set(batch.warehouseId, {
          warehouseName: batch.warehouseName,
          batches: [batch],
          subtotal: batch.quantityAvailable,
        });
      }
    }
    return [...map.values()].sort((a, b) => a.warehouseName.localeCompare(b.warehouseName, 'vi'));
  }, [detailBatches]);

  const detailBatchColumns: ColumnsType<StockBatch> = [
    { title: 'Số lô', dataIndex: 'batchNumber', width: 110 },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 100,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 100,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'Tồn',
      dataIndex: 'quantityAvailable',
      width: 80,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatQty(v)}</span>
      ),
    },
  ];

  const summaryColumns: ColumnsType<StockProductSummary> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 110 },
    { title: 'Tên SP', dataIndex: 'productName' },
    {
      title: 'Tổng tồn',
      dataIndex: 'totalQuantity',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums', fontWeight: 500 }}>{formatQty(v)}</span>
      ),
    },
    {
      title: 'Số kho',
      dataIndex: 'warehouseCount',
      width: 90,
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: 'Số lô',
      dataIndex: 'batchCount',
      width: 90,
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: '',
      width: 90,
      render: (_, row) => (
        <Button
          type="link"
          size="small"
          icon={<EyeOutlined />}
          onClick={(e) => {
            e.stopPropagation();
            void openProductDetail(row);
          }}
        >
          Xem
        </Button>
      ),
    },
  ];

  const fefoColumns: ColumnsType<StockBatch> = [
    { title: 'Kho', dataIndex: 'warehouseName', width: 140 },
    { title: 'Mã SP', dataIndex: 'productCode', width: 110 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'Số lô', dataIndex: 'batchNumber', width: 120 },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'Tồn',
      dataIndex: 'quantityAvailable',
      width: 90,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatQty(v)}</span>
      ),
    },
  ];

  const filterBar = (
    <Space style={{ marginBottom: 16 }} wrap>
      <Select
        allowClear
        placeholder="Lọc theo kho"
        style={{ width: 220 }}
        value={warehouseId}
        onChange={(id) => {
          setWarehouseId(id);
          setPage(1);
        }}
        options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
      />
      <Space.Compact>
        <AutoComplete
          style={{ width: 280 }}
          options={searchSuggestions}
          value={searchInput}
          onSelect={(value) => applySearch(String(value))}
          onChange={(value) => {
            setSearchInput(value);
            if (!value) {
              setSearch('');
              setPage(1);
              if (activeTab === 'fefo') {
                setFefoProductId(undefined);
                setFefoProductLabel(undefined);
              }
            }
          }}
        >
          <Input
            placeholder={activeTab === 'summary' ? 'Tìm SP / mã' : 'Tìm SP / mã / số lô'}
            prefix={<SearchOutlined />}
            allowClear
            onPressEnter={() => applySearch()}
          />
        </AutoComplete>
        <Button type="primary" icon={<SearchOutlined />} onClick={() => applySearch()}>
          Tìm
        </Button>
      </Space.Compact>
      {activeTab === 'fefo' && fefoProductLabel && (
        <Tag closable onClose={clearFefoProductFilter}>
          SP: {fefoProductLabel}
        </Tag>
      )}
      <Button type="primary" ghost icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
        Tải lại
      </Button>
    </Space>
  );

  const pagination = {
    current: page,
    pageSize,
    total,
    showSizeChanger: true,
    onChange: (p: number, ps: number) => {
      setPage(p);
      setPageSize(ps);
    },
  };

  return (
    <Card title="Tồn kho">
      <Tabs
        activeKey={activeTab}
        onChange={(key) => {
          setActiveTab(key as StockTab);
          setPage(1);
        }}
        items={[
          {
            key: 'summary',
            label: 'Tổng hợp SP',
            children: (
              <>
                {filterBar}
                <Table
                  rowKey="productId"
                  loading={loading}
                  columns={summaryColumns}
                  dataSource={summaryItems}
                  pagination={pagination}
                  onRow={(record) => ({
                    onClick: () => void openProductDetail(record),
                    style: { cursor: 'pointer' },
                  })}
                />
              </>
            ),
          },
          {
            key: 'fefo',
            label: 'Theo lô (FEFO)',
            children: (
              <>
                {filterBar}
                <Table
                  rowKey="id"
                  loading={loading}
                  columns={fefoColumns}
                  dataSource={fefoItems}
                  pagination={pagination}
                />
              </>
            ),
          },
        ]}
      />

      <Drawer
        title={detailProduct ? `Chi tiết tồn — ${detailProduct.productName}` : 'Chi tiết tồn'}
        width={640}
        open={detailProduct !== null}
        onClose={() => setDetailProduct(null)}
        extra={
          detailProduct && (
            <Button type="primary" onClick={() => jumpToFefoTab(detailProduct)}>
              Xem theo lô FEFO
            </Button>
          )
        }
      >
        {detailProduct && (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Mã SP">{detailProduct.productCode}</Descriptions.Item>
              <Descriptions.Item label="Tổng tồn">{formatQty(detailProduct.totalQuantity)}</Descriptions.Item>
              <Descriptions.Item label="Số kho">{detailProduct.warehouseCount}</Descriptions.Item>
              <Descriptions.Item label="Số lô">{detailProduct.batchCount}</Descriptions.Item>
            </Descriptions>

            {detailLoading ? (
              <Typography.Text type="secondary">Đang tải chi tiết lô…</Typography.Text>
            ) : batchesByWarehouse.length === 0 ? (
              <Typography.Text type="secondary">Không có lô tồn.</Typography.Text>
            ) : (
              batchesByWarehouse.map((group) => (
                <div key={group.warehouseName} style={{ marginBottom: 20 }}>
                  <Typography.Text strong>
                    {group.warehouseName} — {formatQty(group.subtotal)}
                  </Typography.Text>
                  <Table
                    rowKey="id"
                    size="small"
                    style={{ marginTop: 8 }}
                    pagination={false}
                    columns={detailBatchColumns}
                    dataSource={group.batches}
                  />
                </div>
              ))
            )}
          </>
        )}
      </Drawer>
    </Card>
  );
}
