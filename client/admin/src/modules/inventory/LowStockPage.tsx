import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import { fetchLowStockProducts, fetchWarehouses } from '@/shared/api/inventory.api';
import type { LowStockProduct, Warehouse } from '@/shared/api/inventory.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayQuantity } from '@/shared/utils/money';
import { LowStockSettingsPanel } from '@/modules/inventory/LowStockSettingsPanel';

export function LowStockPage() {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [items, setItems] = useState<LowStockProduct[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void fetchWarehouses().then(setWarehouses).catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchLowStockProducts({ warehouseId }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách tồn thấp'));
    } finally {
      setLoading(false);
    }
  }, [warehouseId]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: ColumnsType<LowStockProduct> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 110 },
    { title: 'Tên SP', dataIndex: 'productName' },
    ...(warehouseId
      ? []
      : [
          {
            title: 'Kho',
            dataIndex: 'warehouseName',
            width: 140,
          } as const,
          {
            title: 'Chi nhánh',
            dataIndex: 'branchName',
            width: 120,
            render: (v: string | undefined) => v ?? '—',
          } as const,
        ]),
    { title: 'ĐVT', dataIndex: 'saleUnitName', width: 80 },
    {
      title: 'Tồn hiện tại',
      dataIndex: 'totalQuantity',
      width: 110,
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: 'Ngưỡng',
      dataIndex: 'minStockQty',
      width: 90,
      render: (v: number) => formatDisplayQuantity(v),
    },
    {
      title: 'Số lô',
      dataIndex: 'batchCount',
      width: 80,
      render: (v: number) => <Tag>{v}</Tag>,
    },
    {
      title: 'Mức độ',
      key: 'level',
      width: 100,
      render: (_, row) =>
        row.totalQuantity <= 0 ? (
          <Tag color="red">Hết hàng</Tag>
        ) : row.totalQuantity <= row.minStockQty / 2 ? (
          <Tag color="orange">Rất thấp</Tag>
        ) : (
          <Tag color="gold">Thấp</Tag>
        ),
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          <WarningOutlined style={{ color: '#faad14', marginRight: 8 }} />
          Cảnh báo tồn thấp
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Ngưỡng: SP → kho → danh mục → cài đặt chung → mặc định 10. Khi xem tất cả kho, mỗi dòng là một cặp SP–kho.
        </Typography.Paragraph>
      </div>

      <LowStockSettingsPanel onChanged={() => void load()} />

      <Card size="small">
        <Space wrap>
          <Select
            allowClear
            placeholder="Tất cả kho"
            style={{ minWidth: 220 }}
            value={warehouseId}
            onChange={(v) => setWarehouseId(v)}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        </Space>
      </Card>

      <Table
        rowKey={(row) => `${row.productId}-${row.warehouseId}`}
        size="small"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 25, showTotal: (t) => `${t} dòng` }}
      />
    </Space>
  );
}
