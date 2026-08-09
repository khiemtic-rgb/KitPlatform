import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AutoComplete,
  Button,
  Card,
  Descriptions,
  Drawer,
  Input,
  Select,
  Spin,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DollarOutlined, DownloadOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
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
import { usePersistedFilters } from '@/shared/hooks/usePersistedFilters';
import { useHasPermission } from '@/shared/auth/usePermission';
import { ListFilterBar } from '@/shared/ui/ListFilterBar';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

type AgingFilter = 'all' | 'current' | '31_60' | '61_90' | 'over_90';
type BalanceFilter =
  | 'all'
  | 'has_receivable'
  | 'has_credit'
  | 'over_100k'
  | 'over_500k'
  | 'over_1m';

type ReceivablesListFilters = {
  search: string;
  warehouseId?: string;
  agingFilter: AgingFilter;
  balanceFilter: BalanceFilter;
};

const RECEIVABLES_FILTER_DEFAULTS: ReceivablesListFilters = {
  search: '',
  warehouseId: undefined,
  agingFilter: 'all',
  balanceFilter: 'all',
};

function agingCell(value: number) {
  return value > 0.009 ? formatDisplayMoney(value) : '—';
}

function matchesAging(row: CustomerReceivablesRow, aging: AgingFilter): boolean {
  if (aging === 'all') return true;
  if (aging === 'current') return row.aging.current > 0.009;
  if (aging === '31_60') return row.aging.days31To60 > 0.009;
  if (aging === '61_90') return row.aging.days61To90 > 0.009;
  return row.aging.over90 > 0.009;
}

function matchesBalance(row: CustomerReceivablesRow, balance: BalanceFilter): boolean {
  if (balance === 'all') return true;
  if (balance === 'has_receivable') return row.totalReceivable > 0.009;
  if (balance === 'has_credit') return row.unappliedCredit > 0.009;
  if (balance === 'over_100k') return row.totalReceivable >= 100_000;
  if (balance === 'over_500k') return row.totalReceivable >= 500_000;
  return row.totalReceivable >= 1_000_000;
}

export function CustomerReceivablesPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerReceivables' });
  const canWrite = useHasPermission('sales.write');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<CustomerReceivablesRow[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters, resetFilters] = usePersistedFilters(
    'admin.listFilters.customerReceivables',
    RECEIVABLES_FILTER_DEFAULTS,
  );
  const { search, warehouseId, agingFilter, balanceFilter } = filters;
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<CustomerReceivablesDetail | null>(null);

  useEffect(() => {
    void fetchWarehouses()
      .then(setWarehouses)
      .catch(() => {
        /* optional */
      });
  }, []);

  const loadSummary = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await fetchCustomerReceivables({ warehouseId }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t, warehouseId]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);

  const searchSuggestions = useMemo(
    () => buildCustomerSearchSuggestions(rows, search),
    [rows, search],
  );

  const filteredRows = useMemo(() => {
    const q = search.trim();
    return rows.filter((row) => {
      if (q && !matchesCustomerNameOrPhone(q, row.customerName, row.customerPhone)) return false;
      if (!matchesAging(row, agingFilter)) return false;
      if (!matchesBalance(row, balanceFilter)) return false;
      return true;
    });
  }, [rows, search, agingFilter, balanceFilter]);

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

  const exportCsv = () => {
    const stamp = new Date().toISOString().slice(0, 10);
    downloadCsv(
      `customer-receivables-${stamp}.csv`,
      [
        t('columns.customerCode'),
        t('columns.customerName'),
        t('columns.totalReceivable'),
        t('columns.agingCurrent'),
        t('columns.aging31To60'),
        t('columns.aging61To90'),
        t('columns.agingOver90'),
      ],
      filteredRows.map((row) => [
        row.customerCode,
        row.customerName,
        String(row.totalReceivable),
        String(row.aging.current),
        String(row.aging.days31To60),
        String(row.aging.days61To90),
        String(row.aging.over90),
      ]),
    );
  };

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
      setDetail(await fetchCustomerReceivablesDetail(customerId, { warehouseId }));
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
    <Card
      title={t('title')}
      bordered={false}
      extra={
        <Button icon={<DownloadOutlined />} onClick={exportCsv} disabled={filteredRows.length === 0}>
          {t('filters.export')}
        </Button>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('intro')}
      </Typography.Paragraph>

      <ListFilterBar>
        <AutoComplete
          style={{ width: 260 }}
          options={searchSuggestions}
          value={search}
          filterOption={false}
          onSelect={(value) => setFilters((prev) => ({ ...prev, search: String(value) }))}
          onChange={(value) => setFilters((prev) => ({ ...prev, search: value }))}
        >
          <Input
            allowClear
            placeholder={t('searchPlaceholder')}
            prefix={<SearchOutlined />}
          />
        </AutoComplete>
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={t('filters.warehouse')}
          style={{ width: 200 }}
          value={warehouseId}
          onChange={(value) => setFilters((prev) => ({ ...prev, warehouseId: value }))}
          options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
        />
        <Select
          style={{ width: 200 }}
          value={agingFilter}
          onChange={(value: AgingFilter) => setFilters((prev) => ({ ...prev, agingFilter: value }))}
          options={[
            { value: 'all', label: t('filters.agingAll') },
            { value: 'current', label: t('filters.agingCurrent') },
            { value: '31_60', label: t('filters.aging31To60') },
            { value: '61_90', label: t('filters.aging61To90') },
            { value: 'over_90', label: t('filters.agingOver90') },
          ]}
        />
        <Select
          style={{ width: 200 }}
          value={balanceFilter}
          onChange={(value: BalanceFilter) => setFilters((prev) => ({ ...prev, balanceFilter: value }))}
          options={[
            { value: 'all', label: t('filters.balanceAll') },
            { value: 'has_receivable', label: t('filters.balanceReceivable') },
            { value: 'has_credit', label: t('filters.balanceCredit') },
            { value: 'over_100k', label: t('filters.balanceOver100k') },
            { value: 'over_500k', label: t('filters.balanceOver500k') },
            { value: 'over_1m', label: t('filters.balanceOver1m') },
          ]}
        />
        <Button onClick={resetFilters}>{t('filters.clear')}</Button>
        <Button
          type="primary"
          ghost
          icon={<ReloadOutlined />}
          loading={loading}
          onClick={() => void loadSummary()}
        >
          {t('filters.reload')}
        </Button>
      </ListFilterBar>

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
