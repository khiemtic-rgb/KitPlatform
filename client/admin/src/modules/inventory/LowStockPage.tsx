import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined, ShoppingCartOutlined, WarningOutlined } from '@ant-design/icons';
import { fetchLowStockProducts, fetchWarehouses } from '@/shared/api/inventory.api';
import type { LowStockProduct, Warehouse } from '@/shared/api/inventory.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { LowStockSettingsPanel } from '@/modules/inventory/LowStockSettingsPanel';
import { savePoRequisitionDraft } from '@/modules/inventory/po-requisition-draft';

export function LowStockPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'lowStock' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const navigate = useNavigate();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [warehouseReady, setWarehouseReady] = useState(false);
  const [items, setItems] = useState<LowStockProduct[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);

  useEffect(() => {
    void fetchWarehouses()
      .then((list) => {
        setWarehouses(list);
        setWarehouseId(list.find((w) => w.isDefault)?.id ?? list[0]?.id);
      })
      .catch(() => undefined)
      .finally(() => setWarehouseReady(true));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchLowStockProducts({ warehouseId }));
      setSelectedRowKeys([]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [warehouseId, t]);

  useEffect(() => {
    if (!warehouseReady) return;
    void load();
  }, [load, warehouseReady]);

  const suggestRequisition = () => {
    const rowKey = (row: LowStockProduct) => `${row.productId}-${row.warehouseId}`;
    const source =
      selectedRowKeys.length > 0
        ? items.filter((row) => selectedRowKeys.includes(rowKey(row)))
        : items;
    if (source.length === 0) {
      message.warning(t('requisition.empty'));
      return;
    }
    const warehouseIds = new Set(source.map((row) => row.warehouseId));
    if (!warehouseId && warehouseIds.size > 1) {
      message.warning(t('requisition.pickWarehouse'));
      return;
    }
    const targetWarehouseId = warehouseId ?? source[0].warehouseId;
    const targetWarehouseName =
      warehouses.find((w) => w.id === targetWarehouseId)?.warehouseName ?? source[0].warehouseName;
    savePoRequisitionDraft({
      warehouseId: targetWarehouseId,
      notes: t('requisition.autoNote', { warehouse: targetWarehouseName }),
      createdAt: new Date().toISOString(),
      lines: source
        .filter((row) => row.warehouseId === targetWarehouseId)
        .map((row) => ({
          productId: row.productId,
          productCode: row.productCode,
          productName: row.productName,
          warehouseId: row.warehouseId,
          warehouseName: row.warehouseName,
          suggestedQty: Math.max(1, Math.ceil(row.minStockQty - row.totalQuantity)),
        })),
    });
    message.success(t('requisition.saved'));
    navigate('/procurement/purchase-orders?requisition=1');
  };

  const columns: ColumnsType<LowStockProduct> = [
    { title: ts('productCode'), dataIndex: 'productCode', width: 110 },
    { title: ts('productName'), dataIndex: 'productName' },
    ...(warehouseId
      ? []
      : [
          {
            title: ts('warehouse'),
            dataIndex: 'warehouseName',
            width: 140,
          } as const,
          {
            title: ts('branch'),
            dataIndex: 'branchName',
            width: 120,
            render: (v: string | undefined) => v ?? '—',
          } as const,
        ]),
    { title: ts('unit'), dataIndex: 'saleUnitName', width: 80 },
    {
      title: t('columns.currentStock'),
      dataIndex: 'totalQuantity',
      width: 110,
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: t('columns.threshold'),
      dataIndex: 'minStockQty',
      width: 90,
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: ts('batchCount'),
      dataIndex: 'batchCount',
      width: 80,
      render: (v: number) => <Tag>{v}</Tag>,
    },
    {
      title: t('columns.level'),
      key: 'level',
      width: 100,
      render: (_, row) =>
        row.totalQuantity <= 0 ? (
          <Tag color="red">{t('levels.outOfStock')}</Tag>
        ) : row.totalQuantity <= row.minStockQty / 2 ? (
          <Tag color="orange">{t('levels.veryLow')}</Tag>
        ) : (
          <Tag color="gold">{t('levels.low')}</Tag>
        ),
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          <WarningOutlined style={{ color: '#faad14', marginRight: 8 }} />
          {t('title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('description')}
        </Typography.Paragraph>
      </div>

      <LowStockSettingsPanel onChanged={() => void load()} />

      <Card size="small">
        <Space wrap>
          <Select
            allowClear
            placeholder={t('allWarehouses')}
            style={{ minWidth: 220 }}
            value={warehouseId}
            onChange={(v) => setWarehouseId(v)}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {tc('actions.reload')}
          </Button>
          <Button
            type="primary"
            icon={<ShoppingCartOutlined />}
            disabled={items.length === 0}
            onClick={suggestRequisition}
          >
            {t('requisition.button')}
          </Button>
        </Space>
      </Card>

      <Table
        rowKey={(row) => `${row.productId}-${row.warehouseId}`}
        rowSelection={{
          selectedRowKeys,
          onChange: (keys) => setSelectedRowKeys(keys as string[]),
        }}
        size="small"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 25, showTotal: (total) => t('paginationTotal', { count: total }) }}
      />
    </Space>
  );
}
