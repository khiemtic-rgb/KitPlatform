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
  App,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CreditCardOutlined, DownloadOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { fetchSupplierPayables, fetchSupplierPayablesDetail } from '@/shared/api/procurement.api';
import type { SupplierPayablesDetail, SupplierPayablesDetailLine, SupplierPayablesRow } from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { buildSupplierPaymentCreateUrl } from '@/modules/procurement/supplier-payment-nav';
import { usePersistedFilters } from '@/shared/hooks/usePersistedFilters';
import { useProcurementWrite } from '@/shared/auth/usePermission';
import { ListFilterBar } from '@/shared/ui/ListFilterBar';
import { downloadCsv } from '@/shared/utils/download-csv';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

type AgingFilter = 'all' | 'current' | '31_60' | '61_90' | 'over_90';
type BalanceFilter =
  | 'all'
  | 'has_payable'
  | 'has_credit'
  | 'over_100k'
  | 'over_500k'
  | 'over_1m';

type PayablesListFilters = {
  search: string;
  warehouseId?: string;
  agingFilter: AgingFilter;
  balanceFilter: BalanceFilter;
};

const PAYABLES_FILTER_DEFAULTS: PayablesListFilters = {
  search: '',
  warehouseId: undefined,
  agingFilter: 'all',
  balanceFilter: 'all',
};

function matchesAging(row: SupplierPayablesRow, aging: AgingFilter): boolean {
  if (aging === 'all') return true;
  if (aging === 'current') return row.aging.current > 0.009;
  if (aging === '31_60') return row.aging.days31To60 > 0.009;
  if (aging === '61_90') return row.aging.days61To90 > 0.009;
  return row.aging.over90 > 0.009;
}

function matchesBalance(row: SupplierPayablesRow, balance: BalanceFilter): boolean {
  if (balance === 'all') return true;
  if (balance === 'has_payable') return row.totalPayable > 0.009;
  if (balance === 'has_credit') return row.unappliedCredit > 0.009;
  if (balance === 'over_100k') return row.totalPayable >= 100_000;
  if (balance === 'over_500k') return row.totalPayable >= 500_000;
  return row.totalPayable >= 1_000_000;
}

function matchesSupplierSearch(q: string, code: string, name: string): boolean {
  const needle = q.trim().toLowerCase();
  if (!needle) return true;
  return code.toLowerCase().includes(needle) || name.toLowerCase().includes(needle);
}

function buildSupplierSearchSuggestions(rows: SupplierPayablesRow[], search: string) {
  const needle = search.trim().toLowerCase();
  const seen = new Set<string>();
  const options: { value: string; label: string }[] = [];
  for (const row of rows) {
    const label = row.supplierCode ? `${row.supplierCode} — ${row.supplierName}` : row.supplierName;
    const hay = `${row.supplierCode} ${row.supplierName}`.toLowerCase();
    if (needle && !hay.includes(needle)) continue;
    if (seen.has(row.supplierId)) continue;
    seen.add(row.supplierId);
    options.push({ value: row.supplierName, label });
    if (options.length >= 12) break;
  }
  return options;
}

export function SupplierPayablesPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'supplierPayables' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { message } = App.useApp();
  const canWrite = useProcurementWrite();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [rows, setRows] = useState<SupplierPayablesRow[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters, resetFilters] = usePersistedFilters(
    'admin.listFilters.supplierPayables',
    PAYABLES_FILTER_DEFAULTS,
  );
  const { search, warehouseId, agingFilter, balanceFilter } = filters;
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState<SupplierPayablesDetail | null>(null);
  const emDash = tShared('emDash');

  const agingCell = useCallback(
    (value: number) => (value > 0.009 ? formatDisplayMoney(value) : emDash),
    [emDash],
  );

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
      setRows(await fetchSupplierPayables({ warehouseId }));
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
    () => buildSupplierSearchSuggestions(rows, search),
    [rows, search],
  );

  const filteredRows = useMemo(() => {
    const q = search.trim();
    return rows.filter((row) => {
      if (q && !matchesSupplierSearch(q, row.supplierCode, row.supplierName)) return false;
      if (!matchesAging(row, agingFilter)) return false;
      if (!matchesBalance(row, balanceFilter)) return false;
      return true;
    });
  }, [rows, search, agingFilter, balanceFilter]);

  const totals = useMemo(
    () =>
      filteredRows.reduce(
        (acc, row) => ({
          payable: acc.payable + row.totalPayable,
          current: acc.current + row.aging.current,
          days31To60: acc.days31To60 + row.aging.days31To60,
          days61To90: acc.days61To90 + row.aging.days61To90,
          over90: acc.over90 + row.aging.over90,
        }),
        { payable: 0, current: 0, days31To60: 0, days61To90: 0, over90: 0 },
      ),
    [filteredRows],
  );

  const exportCsv = () => {
    const stamp = new Date().toISOString().slice(0, 10);
    downloadCsv(
      `supplier-payables-${stamp}.csv`,
      [
        tShared('columns.supplierCode'),
        tShared('columns.supplierName'),
        t('columns.totalPayable'),
        t('columns.aging0To30'),
        t('columns.aging31To60'),
        t('columns.aging61To90'),
        t('columns.agingOver90'),
        t('columns.openDocuments'),
      ],
      filteredRows.map((row) => [
        row.supplierCode,
        row.supplierName,
        String(row.totalPayable),
        String(row.aging.current),
        String(row.aging.days31To60),
        String(row.aging.days61To90),
        String(row.aging.over90),
        String(row.openDocumentCount),
      ]),
    );
  };

  const openDetail = async (supplierId: string) => {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetail(null);
    try {
      setDetail(await fetchSupplierPayablesDetail(supplierId, { warehouseId }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const goToPayment = (prefill: { supplierId: string; goodsReceiptId?: string; amount?: number }) => {
    navigate(buildSupplierPaymentCreateUrl(prefill));
  };

  const detailColumns: ColumnsType<SupplierPayablesDetailLine> = useMemo(() => {
    const base: ColumnsType<SupplierPayablesDetailLine> = [
      { title: tShared('columns.grnNumber'), dataIndex: 'grnNumber', width: 130 },
      {
        title: tShared('columns.receiptDate'),
        dataIndex: 'receiptDate',
        width: 120,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.grnValue'),
        dataIndex: 'grnTotal',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.paid'),
        dataIndex: 'paidAmount',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.outstanding'),
        dataIndex: 'outstanding',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.agingDays'),
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
        width: 110,
        render: (_, line) =>
          line.outstanding > 0.009 && detail ? (
            <Button
              type="link"
              size="small"
              icon={<CreditCardOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                goToPayment({
                  supplierId: detail.supplierId,
                  goodsReceiptId: line.goodsReceiptId,
                  amount: line.outstanding,
                });
              }}
            >
              {t('pay')}
            </Button>
          ) : null,
      },
    ];
  }, [canWrite, detail, navigate, t, tShared]);

  const columns: ColumnsType<SupplierPayablesRow> = useMemo(
    () => [
      {
        title: tShared('columns.supplierCode'),
        dataIndex: 'supplierCode',
        width: 110,
      },
      {
        title: tShared('columns.supplierName'),
        dataIndex: 'supplierName',
        width: 280,
        ellipsis: { showTitle: true },
      },
      {
        title: tShared('columns.paymentTermsDays'),
        dataIndex: 'paymentTerms',
        width: 110,
        align: 'center',
      },
      {
        title: t('columns.totalPayable'),
        dataIndex: 'totalPayable',
        width: 140,
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('columns.aging0To30'),
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
    [agingCell, t, tShared],
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
            { value: 'has_payable', label: t('filters.balancePayable') },
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
        rowKey="supplierId"
        loading={loading}
        columns={columns}
        dataSource={filteredRows}
        pagination={{ pageSize: 20, showTotal: (total) => tShared('pagination.suppliers', { count: total }) }}
        scroll={{ x: 1180 }}
        summary={() =>
          filteredRows.length > 0 ? (
            <Table.Summary fixed>
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={3}>
                  <strong>{tShared('columns.total')}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={1} align="right">
                  <strong>{formatDisplayMoney(totals.payable)}</strong>
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
          onClick: () => void openDetail(record.supplierId),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={detail ? t('detailDrawerWithName', { supplierName: detail.supplierName }) : t('detailDrawer')}
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        destroyOnClose
        extra={
          detail && canWrite && detail.totalPayable > 0.009 ? (
            <Button
              type="primary"
              icon={<CreditCardOutlined />}
              onClick={() =>
                goToPayment({
                  supplierId: detail.supplierId,
                  amount: detail.totalPayable,
                })
              }
            >
              {t('createPayment')}
            </Button>
          ) : undefined
        }
      >
        {detailLoading ? (
          <Spin tip={tShared('messages.loadingDetail')} />
        ) : detail ? (
          <>
            <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
              <Descriptions.Item label={tShared('columns.supplierCode')}>{detail.supplierCode}</Descriptions.Item>
              <Descriptions.Item label={tShared('columns.paymentTermsFull')}>
                {t('paymentTermsDays', { days: detail.paymentTerms })}
              </Descriptions.Item>
              <Descriptions.Item label={t('columns.totalPayable')}>{formatDisplayMoney(detail.totalPayable)}</Descriptions.Item>
              <Descriptions.Item label={t('columns.unappliedCredit')}>
                {detail.unappliedCredit > 0.009 ? formatDisplayMoney(detail.unappliedCredit) : emDash}
              </Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="goodsReceiptId"
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
