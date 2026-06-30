import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchCustomerOrders } from '@/shared/api/customer-admin.api';
import type { CustomerOrderListItem } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { SALE_STATUS_COLORS } from '@/modules/sales/sales-order-status';
import { useSaleStatusLabels } from '@/shared/i18n/use-sale-status-labels';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

interface CustomerOrdersPanelProps {
  customerId: string;
}

export function CustomerOrdersPanel({ customerId }: CustomerOrdersPanelProps) {
  const { t } = useTranslation('customer', { keyPrefix: 'ordersPanel' });
  const { t: tc } = useTranslation('common');
  const { saleStatusLabel } = useSaleStatusLabels();
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [customerId, page, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: ColumnsType<CustomerOrderListItem> = useMemo(
    () => [
      {
        title: t('columns.orderNumber'),
        dataIndex: 'orderNumber',
        width: 140,
      },
      {
        title: t('columns.orderDate'),
        dataIndex: 'orderDate',
        width: 120,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (status: number) => (
          <Tag color={SALE_STATUS_COLORS[status] ?? 'default'}>{saleStatusLabel(status)}</Tag>
        ),
      },
      {
        title: t('columns.itemCount'),
        dataIndex: 'itemCount',
        width: 80,
      },
      {
        title: t('columns.totalAmount'),
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
            {tc('actions.view')}
          </Button>
        ),
      },
    ],
    [navigate, saleStatusLabel, t, tc],
  );

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
