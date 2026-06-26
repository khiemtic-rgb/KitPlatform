import { useCallback, useEffect, useState } from 'react';
import { Button, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchCustomerOrders } from '@/shared/api/customer-admin.api';
import type { CustomerOrderListItem } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { SALE_STATUS_COLORS, SALE_STATUS_LABELS } from '@/modules/sales/sales-order-status';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

interface CustomerOrdersPanelProps {
  customerId: string;
}

export function CustomerOrdersPanel({ customerId }: CustomerOrdersPanelProps) {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerOrderListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchCustomerOrders(customerId, page, 10);
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn hàng'));
    } finally {
      setLoading(false);
    }
  }, [customerId, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: ColumnsType<CustomerOrderListItem> = [
    {
      title: 'Số đơn',
      dataIndex: 'orderNumber',
      width: 140,
    },
    {
      title: 'Ngày',
      dataIndex: 'orderDate',
      width: 120,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (status: number) => (
        <Tag color={SALE_STATUS_COLORS[status] ?? 'default'}>
          {SALE_STATUS_LABELS[status] ?? status}
        </Tag>
      ),
    },
    {
      title: 'SL dòng',
      dataIndex: 'itemCount',
      width: 80,
    },
    {
      title: 'Tổng tiền',
      dataIndex: 'totalAmount',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: '',
      width: 80,
      render: (_, row) => (
        <Button
          type="link"
          icon={<EyeOutlined />}
          onClick={() => navigate(`/sales/orders?orderId=${row.id}`)}
        >
          Xem
        </Button>
      ),
    },
  ];

  return (
    <Table
      rowKey="id"
      size="small"
      loading={loading}
      columns={columns}
      dataSource={items}
      pagination={{
        current: page,
        pageSize: 10,
        total,
        showSizeChanger: false,
        onChange: (nextPage) => setPage(nextPage),
      }}
    />
  );
}
