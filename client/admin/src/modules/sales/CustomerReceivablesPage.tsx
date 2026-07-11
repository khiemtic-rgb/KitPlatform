import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AutoComplete,
  Button,
  Card,
  Descriptions,
  Drawer,
  Input,
  Space,
  Spin,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DollarOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import {
  fetchCustomerReceivables,
  fetchCustomerReceivablesDetail,
} from '@/shared/api/sales.api';
import type {
  CustomerReceivablesDetail,
  CustomerReceivablesDetailLine,
  CustomerReceivablesRow,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { buildCustomerPaymentCreateUrl } from '@/modules/sales/customer-payment-nav';
import {
  buildCustomerSearchSuggestions,
  matchesCustomerNameOrPhone,
} from '@/modules/sales/sales-list-customer-search';
import { filterBarStyle } from '@/modules/sales/sales-ui-styles';
import { useHasPermission } from '@/shared/auth/usePermission';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

function agingCell(value: number) {
  return value > 0.009 ? formatDisplayMoney(value) : '—';
}

export function CustomerReceivablesPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerReceivables' });
  const canWrite = useHasPermission('sales.write');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<CustomerReceivablesRow[]>([]);
  const [search, setSearch] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<CustomerReceivablesDetail | null>(null);

  const loadSummary = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await fetchCustomerReceivables());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);

  const searchSuggestions = useMemo(
    () => buildCustomerSearchSuggestions(rows, search),
    [rows, search],
  );

  const filteredRows = useMemo(() => {
    const q = search.trim();
    if (!q) return rows;
    return rows.filter((row) =>
      matchesCustomerNameOrPhone(q, row.customerName, row.customerPhone),
    );
  }, [rows, search]);

  const totals = useMemo(
    () =>
      filteredRows.reduce(
        (acc, row) => ({
          receivable: acc.receivable + row.totalReceivable,
          current: acc.current + row.aging.current,
          days31To60: acc.days31To60 + row.aging.days31To60,
          days61To90: acc.days61To90 + row.aging.days61To90,
          over90: acc.over90 + row.aging.over90,
        }),
        { receivable: 0, current: 0, days31To60: 0, days61To90: 0, over90: 0 },
      ),
    [filteredRows],
  );

  const goToPayment = useCallback(
    (prefill: { customerId: string; salesOrderId?: string; amount?: number }) => {
      navigate(buildCustomerPaymentCreateUrl(prefill));
    },
    [navigate],
  );

  const openDetail = async (customerId: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetail(null);
    try {
      setDetail(await fetchCustomerReceivablesDetail(customerId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const detailColumns: ColumnsType<CustomerReceivablesDetailLine> = useMemo(() => {
    const base: ColumnsType<CustomerReceivablesDetailLine> = [
      { title: t('detail.columns.orderNumber'), dataIndex: 'orderNumber', width: 130 },
      {
        title: t('detail.columns.orderDate'),
        dataIndex: 'orderDate',
        width: 120,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('detail.columns.orderTotal'),
        dataIndex: 'orderTotal',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('detail.columns.paidAmount'),
        dataIndex: 'paidAmount',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('detail.columns.outstanding'),
        dataIndex: 'outstanding',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('detail.columns.daysOutstanding'),
        dataIndex: 'daysOutstanding',
        width: 120,
        align: 'center',
      },
    ];

    if (!canWrite) return base;

    return [
      ...base,
      {
        title: '',
        width: 100,
        render: (_, line) =>
          line.outstanding > 0.009 && detail ? (
            <Button
              type="link"
              size="small"
              icon={<DollarOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                goToPayment({
                  customerId: detail.customerId,
                  salesOrderId: line.salesOrderId,
                  amount: line.outstanding,
                });
              }}
            >
              {t('detail.collect')}
            </Button>
          ) : null,
      },
    ];
  }, [canWrite, detail, goToPayment, t]);

  const columns: ColumnsType<CustomerReceivablesRow> = useMemo(
    () => [
      { title: t('columns.customerCode'), dataIndex: 'customerCode', width: 110 },
      {
        title: t('columns.customerName'),
        dataIndex: 'customerName',
        width: 280,
        ellipsis: { showTitle: true },
      },
      {
        title: t('columns.totalReceivable'),
        dataIndex: 'totalReceivable',
        width: 140,
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.agingCurrent'),
        width: 120,
        align: 'right',
        render: (_, row) => agingCell(row.aging.current),
      },
      {
        title: t('columns.aging31To60'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.days31To60),
      },
      {
        title: t('columns.aging61To90'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.days61To90),
      },
      {
        title: t('columns.agingOver90'),
        width: 110,
        align: 'right',
        render: (_, row) => agingCell(row.aging.over90),
      },
      {
        title: t('columns.openDocuments'),
        dataIndex: 'openDocumentCount',
        width: 90,
        align: 'center',
      },
    ],
    [t],
  );

  return (
    <Card title={t('title')} bordered={false}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('intro')}
      </Typography.Paragraph>

      <Space wrap style={filterBarStyle}>
        <AutoComplete
          style={{ width: 280 }}
          options={searchSuggestions}
          value={search}
          filterOption={false}
          onSelect={(value) => setSearch(String(value))}
          onChange={(value) => setSearch(value)}
        >
          <Input
            allowClear
            placeholder={t('searchPlaceholder')}
            prefix={<SearchOutlined />}
          />
        </AutoComplete>
        {search ? <Button onClick={() => setSearch('')}>{t('clear')}</Button> : null}
      </Space>

      <Table
        rowKey="customerId"
        loading={loading}
        columns={columns}
        dataSource={filteredRows}
        pagination={{ pageSize: 20, showTotal: (total) => t('paginationTotal', { count: total }) }}
        scroll={{ x: 1100 }}
        summary={() =>
          filteredRows.length > 0 ? (
            <Table.Summary fixed>
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={2}>
                  <strong>{t('summaryTotal')}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={1} align="right">
                  <strong>{formatDisplayMoney(totals.receivable)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={2} align="right">
                  {agingCell(totals.current)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={3} align="right">
                  {agingCell(totals.days31To60)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={4} align="right">
                  {agingCell(totals.days61To90)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5} align="right">
                  {agingCell(totals.over90)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={6} />
              </Table.Summary.Row>
            </Table.Summary>
          ) : null
        }
        onRow={(record) => ({
          onClick: () => void openDetail(record.customerId),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={
          detail
            ? t('detail.drawerTitle', { customerName: detail.customerName })
            : t('detail.drawerTitleDefault')
        }
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        destroyOnClose
        extra={
          detail && canWrite && detail.totalReceivable > 0.009 ? (
            <Button
              type="primary"
              icon={<DollarOutlined />}
              onClick={() =>
                goToPayment({
                  customerId: detail.customerId,
                  amount: detail.totalReceivable,
                })
              }
            >
              {t('detail.createPayment')}
            </Button>
          ) : undefined
        }
      >
        {detailLoading ? (
          <Spin tip={t('detail.loading')} />
        ) : detail ? (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label={t('detail.customerCode')}>
                {detail.customerCode}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.totalReceivable')}>
                {formatDisplayMoney(detail.totalReceivable)}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.unappliedCredit')} span={2}>
                {detail.unappliedCredit > 0.009 ? formatDisplayMoney(detail.unappliedCredit) : '—'}
              </Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="salesOrderId"
              size="small"
              pagination={false}
              dataSource={detail.lines.filter((line) => line.outstanding > 0.009)}
              columns={detailColumns}
            />
          </>
        ) : null}
      </Drawer>
    </Card>
  );
}
