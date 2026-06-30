import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined, PrinterOutlined, ReloadOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchSalesReturn, fetchSalesReturns, searchCustomers } from '@/shared/api/sales.api';
import type { CustomerListItem, SalesReturnListItem } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { SalesReturnDetailDrawer } from '@/modules/sales/SalesReturnDetailDrawer';
import {
  buildCustomerSearchSuggestions,
  buildDocumentSearchSuggestions,
} from '@/modules/sales/sales-list-customer-search';
import { SalesListDualSearchBar, SalesListDualSearchWrap } from '@/modules/sales/SalesListDualSearchBar';
import { TabularMoney } from '@/modules/sales/sales-ui-styles';
import { printSalesReturn } from '@/modules/sales/sales-return-print';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

export function SalesReturnListPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'returns.list' });
  const canRead = useHasPermission('sales.read');
  const { returnStatusLabel } = useSalesEnums();
  const navigate = useNavigate();
  const [items, setItems] = useState<SalesReturnListItem[]>([]);
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [customerQuery, setCustomerQuery] = useState('');
  const [documentQuery, setDocumentQuery] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailReturnId, setDetailReturnId] = useState<string | null>(null);

  const load = useCallback(
    async (nextCustomerSearch: string, nextDocumentSearch: string) => {
      setLoading(true);
      try {
        setItems(
          await fetchSalesReturns({
            customerSearch: nextCustomerSearch.trim() || undefined,
            documentSearch: nextDocumentSearch.trim() || undefined,
          }),
        );
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.loadFailed')));
      } finally {
        setLoading(false);
      }
    },
    [t],
  );

  useEffect(() => {
    void load('', '');
  }, [load]);

  useEffect(() => {
    void searchCustomers()
      .then(setCustomers)
      .catch(() => {
        /* gợi ý KH tùy chọn */
      });
  }, []);

  const customerSuggestions = useMemo(
    () =>
      buildCustomerSearchSuggestions(
        customers.map((customer) => ({
          customerName: customer.fullName,
          customerPhone: customer.phone,
        })),
        customerQuery,
      ),
    [customers, customerQuery],
  );

  const documentSuggestions = useMemo(() => {
    const numbers = items.flatMap((row) => [row.returnNumber, row.orderNumber]);
    return buildDocumentSearchSuggestions(numbers, documentQuery);
  }, [items, documentQuery]);

  const applySearch = (values: { customer: string; document: string }) => {
    const customer = values.customer.trim();
    const document = values.document.trim();
    setCustomerQuery(customer);
    setDocumentQuery(document);
    void load(customer, document);
  };

  const resetFilters = () => {
    setCustomerQuery('');
    setDocumentQuery('');
    void load('', '');
  };

  const openDetail = (id: string) => {
    setDetailReturnId(id);
    setDetailOpen(true);
  };

  const printReturnById = async (id: string) => {
    try {
      if (!(await printSalesReturn(await fetchSalesReturn(id)))) {
        message.warning(t('messages.printBlocked'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.printFailed')));
    }
  };

  const columns: ColumnsType<SalesReturnListItem> = useMemo(
    () => [
      {
        title: t('columns.returnNumber'),
        dataIndex: 'returnNumber',
        width: 130,
        render: (value: string, row) => (
          <Button type="link" size="small" onClick={() => openDetail(row.id)}>
            {value}
          </Button>
        ),
      },
      {
        title: t('columns.orderNumber'),
        dataIndex: 'orderNumber',
        width: 130,
        render: (value: string, row) => (
          <Button
            type="link"
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              navigate(`/sales/orders?orderId=${row.salesOrderId}`);
            }}
          >
            {value}
          </Button>
        ),
      },
      {
        title: t('columns.returnDate'),
        dataIndex: 'returnDate',
        width: 110,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 100,
        render: (status: number) => <Tag>{returnStatusLabel(status)}</Tag>,
      },
      {
        title: t('columns.shift'),
        dataIndex: 'shiftNumber',
        width: 100,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.refundTotal'),
        dataIndex: 'totalRefund',
        width: 120,
        align: 'right',
        render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
      },
      {
        title: t('columns.actions'),
        width: 130,
        render: (_, row) =>
          canRead ? (
            <Space size="small" onClick={(e) => e.stopPropagation()}>
              <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openDetail(row.id)}>
                {t('columns.view')}
              </Button>
              <Button
                type="link"
                size="small"
                icon={<PrinterOutlined />}
                onClick={() => void printReturnById(row.id)}
              >
                {t('columns.print')}
              </Button>
            </Space>
          ) : null,
      },
    ],
    [canRead, navigate, printReturnById, returnStatusLabel, t],
  );

  return (
    <Card title={t('title')}>
      <SalesListDualSearchWrap>
        <SalesListDualSearchBar
          customerValue={customerQuery}
          documentValue={documentQuery}
          onCustomerChange={setCustomerQuery}
          onDocumentChange={setDocumentQuery}
          onApply={applySearch}
          onClear={() => {
            setCustomerQuery('');
            setDocumentQuery('');
          }}
          customerSuggestions={customerSuggestions}
          documentSuggestions={documentSuggestions}
          documentPlaceholder={t('filters.documentPlaceholder')}
        />
        <Button onClick={resetFilters}>{t('filters.clear')}</Button>
        <Button
          type="primary"
          ghost
          icon={<ReloadOutlined />}
          onClick={() => void load(customerQuery, documentQuery)}
          loading={loading}
        >
          {t('filters.reload')}
        </Button>
      </SalesListDualSearchWrap>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={items}
        columns={columns}
        pagination={{ pageSize: 20, showTotal: (total) => t('paginationTotal', { count: total }) }}
        onRow={(record) => ({
          onClick: () => openDetail(record.id),
          style: { cursor: 'pointer' },
        })}
      />

      <SalesReturnDetailDrawer
        open={detailOpen}
        returnId={detailReturnId}
        onClose={() => {
          setDetailOpen(false);
          setDetailReturnId(null);
        }}
        onOpenOrder={(orderId) => {
          setDetailOpen(false);
          setDetailReturnId(null);
          navigate(`/sales/orders?orderId=${orderId}`);
        }}
      />
    </Card>
  );
}
