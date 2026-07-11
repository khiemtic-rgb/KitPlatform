import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { fetchProduct } from '@/shared/api/catalog.api';
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
  const { t } = useTranslation('inventory', { keyPrefix: 'stockList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
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
  const [searchSuggestions, setSearchSuggestions] = useState<{ value: string; label: string }[]>([]);
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [activeTab, warehouseId, search, fefoProductId, page, pageSize, t]);

  useEffect(() => {
    void loadWarehouses();
  }, [loadWarehouses]);

  useEffect(() => {
    const q = searchInput.trim();
    if (q.length < 2) {
      setSearchSuggestions([]);
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void (async () => {
        try {
          if (activeTab === 'summary') {
            const result = await fetchStockProducts({
              warehouseId,
              search: q,
              page: 1,
              pageSize: 12,
            });
            if (cancelled) return;
            setSearchSuggestions(
              result.items.map((p) => ({
                value: p.productCode || p.productName,
                label: `${p.productCode} — ${p.productName}`,
              })),
            );
          } else {
            const result = await fetchStockBatches({
              warehouseId,
              search: q,
              page: 1,
              pageSize: 12,
            });
            if (cancelled) return;
            const seen = new Set<string>();
            setSearchSuggestions(
              result.items
                .filter((b) => {
                  if (seen.has(b.batchNumber)) return false;
                  seen.add(b.batchNumber);
                  return true;
                })
                .slice(0, 12)
                .map((b) => ({
                  value: b.batchNumber,
                  label: `${ts('batchLabel', { number: b.batchNumber })} — ${b.productName}`,
                })),
            );
          }
        } catch {
          if (!cancelled) setSearchSuggestions([]);
        }
      })();
    }, 250);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [searchInput, activeTab, warehouseId, ts]);

  useEffect(() => {
    const tab = searchParams.get('tab');
    if (tab === 'fefo') setActiveTab('fefo');
    else if (tab === 'summary') setActiveTab('summary');

    const productId = searchParams.get('productId');
    if (productId) {
      setActiveTab('fefo');
      setFefoProductId(productId);
      setPage(1);
      void (async () => {
        try {
          const product = await fetchProduct(productId);
          setFefoProductLabel(`${product.productCode} — ${product.productName}`);
        } catch {
          /* optional label */
        }
      })();
    }
  }, [searchParams]);

  useEffect(() => {
    void load();
  }, [load]);

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
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
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
    { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 110 },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 100,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 100,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: ts('stockQty'),
      dataIndex: 'quantityAvailable',
      width: 80,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatQty(v)}</span>
      ),
    },
  ];

  const summaryColumns: ColumnsType<StockProductSummary> = [
    { title: ts('productCode'), dataIndex: 'productCode', width: 110 },
    { title: ts('productName'), dataIndex: 'productName' },
    {
      title: ts('unit'),
      dataIndex: 'saleUnitName',
      width: 72,
      render: (v?: string) => v ?? '—',
    },
    {
      title: ts('totalStock'),
      dataIndex: 'totalQuantity',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums', fontWeight: 500 }}>{formatQty(v)}</span>
      ),
    },
    {
      title: ts('warehouseCount'),
      dataIndex: 'warehouseCount',
      width: 90,
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: ts('batchCount'),
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
          {tc('actions.view')}
        </Button>
      ),
    },
  ];

  const fefoColumns: ColumnsType<StockBatch> = [
    { title: ts('warehouse'), dataIndex: 'warehouseName', width: 140 },
    { title: ts('productCode'), dataIndex: 'productCode', width: 110 },
    { title: ts('productName'), dataIndex: 'productName' },
    { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 120 },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: ts('stockQty'),
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
        placeholder={t('filterByWarehouse')}
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
            placeholder={activeTab === 'summary' ? t('searchSummary') : t('searchFefo')}
            prefix={<SearchOutlined />}
            allowClear
            onPressEnter={() => applySearch()}
          />
        </AutoComplete>
        <Button type="primary" icon={<SearchOutlined />} onClick={() => applySearch()}>
          {tc('actions.search')}
        </Button>
      </Space.Compact>
      {activeTab === 'fefo' && fefoProductLabel && (
        <Tag closable onClose={clearFefoProductFilter}>
          {ts('productFilterTag', { label: fefoProductLabel })}
        </Tag>
      )}
      <Button type="primary" ghost icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
        {tc('actions.reload')}
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
    <Card title={t('title')}>
      <Tabs
        activeKey={activeTab}
        onChange={(key) => {
          setActiveTab(key as StockTab);
          setPage(1);
        }}
        items={[
          {
            key: 'summary',
            label: t('tabs.summary'),
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
            label: t('tabs.fefo'),
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
        title={
          detailProduct
            ? t('detailTitleWithProduct', { name: detailProduct.productName })
            : t('detailTitle')
        }
        width={640}
        open={detailProduct !== null}
        onClose={() => setDetailProduct(null)}
        extra={
          detailProduct && (
            <Button type="primary" onClick={() => jumpToFefoTab(detailProduct)}>
              {t('viewFefo')}
            </Button>
          )
        }
      >
        {detailProduct && (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label={ts('productCode')}>{detailProduct.productCode}</Descriptions.Item>
              <Descriptions.Item label={ts('unit')}>{detailProduct.saleUnitName ?? '—'}</Descriptions.Item>
              <Descriptions.Item label={ts('totalStock')}>{formatQty(detailProduct.totalQuantity)}</Descriptions.Item>
              <Descriptions.Item label={ts('warehouseCount')}>{detailProduct.warehouseCount}</Descriptions.Item>
              <Descriptions.Item label={ts('batchCount')}>{detailProduct.batchCount}</Descriptions.Item>
            </Descriptions>

            {detailLoading ? (
              <Typography.Text type="secondary">{t('loadingBatches')}</Typography.Text>
            ) : batchesByWarehouse.length === 0 ? (
              <Typography.Text type="secondary">{t('noBatches')}</Typography.Text>
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
