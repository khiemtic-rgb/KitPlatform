import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, DatePicker, Segmented, Select, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchEmployees } from '@/shared/api/identity-admin.api';
import type { EmployeeLookup } from '@/shared/api/identity-admin.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import { fetchReportSalesShift } from '@/shared/api/sales.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import type { SalesShiftDetail, SalesShiftSummary } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHODS } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatReportCell, exportReportCsv } from '@/modules/reports/report-export';
import { ShiftCloseSheetDrawer } from '@/modules/sales/ShiftCloseSheet';
import './report-view.css';

const { RangePicker } = DatePicker;

type CloseRow = Record<string, unknown> & { key: string };

function todayRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('day'), dayjs().endOf('day')];
}

function weekRange(): [Dayjs, Dayjs] {
  return [dayjs().subtract(6, 'day').startOf('day'), dayjs().endOf('day')];
}

function rangeFromSearch(params: URLSearchParams): [Dayjs, Dayjs] {
  const from = params.get('from');
  const to = params.get('to');
  if (from && to) {
    const start = dayjs(from);
    const end = dayjs(to);
    if (start.isValid() && end.isValid()) return [start.startOf('day'), end.endOf('day')];
  }
  return weekRange();
}

function toIsoRange(range: [Dayjs, Dayjs]) {
  return {
    from: range[0].startOf('day').toISOString(),
    to: range[1].add(1, 'day').startOf('day').toISOString(),
  };
}

function staffQuery(opts: {
  employeeId?: string;
  from?: Dayjs;
  to?: Dayjs;
  warehouseId?: string;
  branchId?: string;
}) {
  const q = new URLSearchParams();
  if (opts.employeeId) q.set('employeeId', opts.employeeId);
  if (opts.from) q.set('from', opts.from.format('YYYY-MM-DD'));
  if (opts.to) q.set('to', opts.to.format('YYYY-MM-DD'));
  if (opts.warehouseId) q.set('warehouseId', opts.warehouseId);
  if (opts.branchId) q.set('branchId', opts.branchId);
  const qs = q.toString();
  return qs ? `?${qs}` : '';
}

function asNumber(value: unknown): number {
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
}

function fieldOf(row: Record<string, unknown>, key: string): string {
  const direct = row[key];
  if (direct != null && String(direct) !== '') return String(direct);
  const found = Object.keys(row).find((k) => k.toLowerCase() === key.toLowerCase());
  return found != null && row[found] != null ? String(row[found]) : '';
}

function sameId(left?: string, right?: string) {
  return Boolean(left && right && left.toLowerCase() === right.toLowerCase());
}

function filterCloseRows(
  rows: Record<string, unknown>[],
  opts: { employeeId?: string; employeeName?: string; branchId?: string; warehouseId?: string },
) {
  return rows.filter((row) => {
    if (opts.employeeId) {
      const matchesId = sameId(fieldOf(row, 'employeeId'), opts.employeeId);
      const matchesName = Boolean(opts.employeeName) && fieldOf(row, 'employeeName') === opts.employeeName;
      if (!matchesId && !matchesName) return false;
    }
    if (opts.branchId && !sameId(fieldOf(row, 'branchId'), opts.branchId)) return false;
    if (opts.warehouseId && !sameId(fieldOf(row, 'warehouseId'), opts.warehouseId)) return false;
    return true;
  });
}

const CLOSE_TOTAL_KEYS = [
  'orderCount',
  'revenueAmount',
  'newDebt',
  'collectionAmount',
  'salesAmount',
  'refundAmount',
  'cashNet',
  'transferNet',
  'otherNet',
  'netAmount',
] as const;

function sumCloseTotals(rows: Record<string, unknown>[]): Record<string, unknown> {
  const totals: Record<string, unknown> = {};
  for (const key of CLOSE_TOTAL_KEYS) {
    totals[key] = rows.reduce((sum, row) => sum + asNumber(fieldOf(row, key)), 0);
  }
  return totals;
}

function summaryFromCloseRow(row: Record<string, unknown>): SalesShiftSummary {
  const cash = asNumber(row.cashNet);
  const transfer = asNumber(row.transferNet);
  const other = asNumber(row.otherNet);
  const sales = asNumber(row.salesAmount);
  const refunds = asNumber(row.refundAmount);
  return {
    from: String(row.openedAt ?? ''),
    to: String(row.closedAt ?? row.openedAt ?? ''),
    totalSales: sales,
    totalRefunds: refunds,
    netTotal: asNumber(row.netAmount),
    byMethod: [
      { paymentMethod: SALES_PAYMENT_METHODS.Cash, salesAmount: Math.max(cash, 0), refundAmount: 0, netAmount: cash },
      { paymentMethod: SALES_PAYMENT_METHODS.Transfer, salesAmount: Math.max(transfer, 0), refundAmount: 0, netAmount: transfer },
      { paymentMethod: SALES_PAYMENT_METHODS.Card, salesAmount: Math.max(other, 0), refundAmount: 0, netAmount: other },
    ].filter((m) => m.netAmount !== 0 || m.salesAmount !== 0),
    openingCash: 0,
    cashSales: Math.max(cash, 0),
    cashRefunds: 0,
    expectedCash: Math.max(cash, 0),
  };
}

export function ShiftCloseByEmployeePage() {
  const { t } = useTranslation('reports', { keyPrefix: 'shiftClose' });
  const { t: tv } = useTranslation('reports', { keyPrefix: 'view' });
  const canExport = useCanReportsExport();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => rangeFromSearch(searchParams));
  const [branchId, setBranchId] = useState<string | undefined>(
    () => searchParams.get('branchId') || undefined,
  );
  const [warehouseId, setWarehouseId] = useState<string | undefined>(
    () => searchParams.get('warehouseId') || undefined,
  );
  const [employeeId, setEmployeeId] = useState<string | undefined>(
    () => searchParams.get('employeeId') || undefined,
  );
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [employees, setEmployees] = useState<EmployeeLookup[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ReportTableResult | null>(null);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [sheetLoading, setSheetLoading] = useState(false);
  const [sheetShift, setSheetShift] = useState<SalesShiftDetail | null>(null);
  const [sheetFallback, setSheetFallback] = useState<SalesShiftSummary | null>(null);
  const [sheetTitle, setSheetTitle] = useState<string>();
  const loadGen = useRef(0);

  useEffect(() => {
    void Promise.all([fetchWarehouses(), fetchEmployees()])
      .then(([wh, emp]) => {
        setWarehouses(wh);
        setEmployees(emp);
      })
      .catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    const gen = ++loadGen.current;
    setLoading(true);
    try {
      const next = await runReport('sales/shift-close-by-employee', {
        ...toIsoRange(range),
        ...(branchId ? { branchId } : {}),
        ...(warehouseId ? { warehouseId } : {}),
        ...(employeeId ? { employeeId } : {}),
      });
      if (gen !== loadGen.current) return;
      setResult(next);
    } catch (error) {
      if (gen !== loadGen.current) return;
      setResult(null);
      message.error(apiErrorMessage(error, tv('loadFailed')));
    } finally {
      if (gen === loadGen.current) setLoading(false);
    }
  }, [range, branchId, warehouseId, employeeId, tv]);

  useEffect(() => {
    void load();
  }, [load]);

  const syncParams = (
    nextEmployee = employeeId,
    nextWarehouse = warehouseId,
    nextRange = range,
    nextBranch = branchId,
  ) => {
    const next = new URLSearchParams();
    if (nextEmployee) next.set('employeeId', nextEmployee);
    if (nextBranch) next.set('branchId', nextBranch);
    if (nextWarehouse) next.set('warehouseId', nextWarehouse);
    next.set('from', nextRange[0].format('YYYY-MM-DD'));
    next.set('to', nextRange[1].format('YYYY-MM-DD'));
    setSearchParams(next, { replace: true });
  };

  const openSheet = async (row: Record<string, unknown>) => {
    const shiftId = row.shiftId ? String(row.shiftId) : '';
    setSheetOpen(true);
    setSheetShift(null);
    setSheetFallback(summaryFromCloseRow(row));
    setSheetTitle(
      row.shiftNumber
        ? t('sheetTitle', { number: String(row.shiftNumber), name: String(row.employeeName ?? '') })
        : t('sheetTitleLoose', { name: String(row.employeeName ?? '') }),
    );
    if (!shiftId) return;
    setSheetLoading(true);
    try {
      setSheetShift(await fetchReportSalesShift(shiftId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('openSheetFailed')));
    } finally {
      setSheetLoading(false);
    }
  };

  const branches = useMemo(() => {
    const map = new Map<string, string>();
    for (const w of warehouses) {
      if (w.branchId) map.set(w.branchId, w.branchName || w.branchId);
    }
    return [...map.entries()].map(([id, name]) => ({ id, name }));
  }, [warehouses]);

  const warehousesInScope = useMemo(
    () => (branchId ? warehouses.filter((w) => w.branchId === branchId) : warehouses),
    [warehouses, branchId],
  );

  const applyBranch = (nextBranch?: string) => {
    const warehouseStillInBranch =
      warehouseId &&
      warehouses.some((w) => w.id === warehouseId && (!nextBranch || w.branchId === nextBranch));
    const nextWarehouse = warehouseStillInBranch ? warehouseId : undefined;
    setBranchId(nextBranch);
    setWarehouseId(nextWarehouse);
    syncParams(employeeId, nextWarehouse, range, nextBranch);
  };

  const columns: ColumnsType<CloseRow> = useMemo(
    () =>
      (result?.columns ?? []).map((col) => ({
        title: col.title,
        dataIndex: col.key,
        key: col.key,
        align: col.align,
        render: (value: unknown) => (
          <span style={{ fontVariantNumeric: col.align === 'right' ? 'tabular-nums' : undefined }}>
            {formatReportCell(value, col.format)}
          </span>
        ),
      })),
    [result],
  );

  const selectedEmployeeName = employees.find((e) => e.id === employeeId)?.fullName;
  const visibleRows = useMemo(
    () =>
      filterCloseRows(result?.rows ?? [], {
        employeeId,
        employeeName: selectedEmployeeName,
        branchId,
        warehouseId,
      }),
    [result, employeeId, selectedEmployeeName, branchId, warehouseId],
  );
  const visibleTotals = useMemo(() => (visibleRows.length ? sumCloseTotals(visibleRows) : null), [visibleRows]);
  const dataSource: CloseRow[] = useMemo(
    () =>
      visibleRows.map((row, index) => ({
        ...row,
        key: String(
          fieldOf(row, 'shiftId') ||
            `${fieldOf(row, 'employeeId') || 'none'}-${fieldOf(row, 'warehouseId') || index}-${index}`,
        ),
      })),
    [visibleRows],
  );

  const presets = [
    { key: 'today', label: t('period.today'), value: todayRange() },
    {
      key: 'yesterday',
      label: t('period.yesterday'),
      value: [dayjs().subtract(1, 'day').startOf('day'), dayjs().subtract(1, 'day').endOf('day')] as [Dayjs, Dayjs],
    },
    { key: 'week', label: t('period.week'), value: weekRange() },
  ];

  const sameDayRange = (a: [Dayjs, Dayjs], b: [Dayjs, Dayjs]) =>
    a[0].isSame(b[0], 'day') && a[1].isSame(b[1], 'day');

  return (
    <div className="report-view">
      <div className="report-view__head">
        <div>
          <Typography.Title level={4} className="report-view__title">
            {t('title')}
            <span className="report-view__code">SALES-09</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="report-view__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        {result && canExport ? (
          <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(result)}>
            {tv('exportCsv')}
          </Button>
        ) : null}
      </div>

      <div className="report-view__toolbar" style={{ marginBottom: 16 }}>
        <Segmented
          value="close"
          onChange={(value) => {
            if (value === 'product') {
              navigate(
                `/reports/sales/revenue-by-employee-product${staffQuery({
                  employeeId,
                  from: range[0],
                  to: range[1],
                  warehouseId,
                  branchId,
                })}`,
              );
            }
          }}
          options={[
            { label: t('tabs.close'), value: 'close' },
            { label: t('tabs.product'), value: 'product' },
          ]}
        />
      </div>

      <div className="report-view__filters" style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
        <div>
          {presets.map((preset) => (
            <Button
              key={preset.key}
              size="small"
              type={sameDayRange(range, preset.value) ? 'primary' : 'default'}
              ghost={sameDayRange(range, preset.value)}
              style={{ marginRight: 8 }}
              onClick={() => {
                setRange(preset.value);
                syncParams(employeeId, warehouseId, preset.value, branchId);
              }}
            >
              {preset.label}
            </Button>
          ))}
        </div>
        <RangePicker
          value={range}
          allowClear={false}
          format="DD/MM/YYYY"
          onChange={(vals) => {
            if (vals?.[0] && vals[1]) {
              const next: [Dayjs, Dayjs] = [vals[0].startOf('day'), vals[1].endOf('day')];
              setRange(next);
              syncParams(employeeId, warehouseId, next, branchId);
            }
          }}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={t('branchAll')}
          style={{ minWidth: 200 }}
          value={branchId}
          options={branches.map((b) => ({ value: b.id, label: b.name }))}
          onChange={(id) => applyBranch(id)}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={t('warehouseAll')}
          style={{ minWidth: 200 }}
          value={warehouseId}
          options={warehousesInScope.map((w) => ({ value: w.id, label: w.warehouseName }))}
          onChange={(id) => {
            const selected = warehouses.find((w) => w.id === id);
            const nextBranch = selected?.branchId || branchId;
            if (selected?.branchId && selected.branchId !== branchId) setBranchId(selected.branchId);
            setWarehouseId(id);
            syncParams(employeeId, id, range, nextBranch);
          }}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={t('employeeAll')}
          style={{ minWidth: 200 }}
          value={employeeId}
          options={employees.map((e) => ({ value: e.id, label: e.fullName }))}
          onChange={(id) => {
            setEmployeeId(id);
            syncParams(id, warehouseId, range, branchId);
          }}
        />
        <Button type="primary" icon={<ReloadOutlined />} loading={loading} onClick={() => void load()}>
          {tv('runReport')}
        </Button>
      </div>

      <Table<CloseRow>
        rowKey="key"
        size="middle"
        loading={loading}
        columns={columns}
        dataSource={dataSource}
        locale={{ emptyText: t('empty') }}
        pagination={{ pageSize: 50, showSizeChanger: false, showTotal: (total) => tv('paginationTotal', { count: total }) }}
        scroll={{ x: true }}
        onRow={(row) => ({
          onClick: () => void openSheet(row),
          style: { cursor: 'pointer' },
        })}
        summary={() =>
          visibleTotals ? (
            <Table.Summary fixed>
              <Table.Summary.Row>
                {(result?.columns ?? []).map((col, index) => (
                  <Table.Summary.Cell key={col.key} index={index} align={col.align}>
                    <Typography.Text strong>
                      {index === 0 ? t('total') : formatReportCell(visibleTotals[col.key], col.format)}
                    </Typography.Text>
                  </Table.Summary.Cell>
                ))}
              </Table.Summary.Row>
            </Table.Summary>
          ) : null
        }
      />

      <ShiftCloseSheetDrawer
        open={sheetOpen}
        loading={sheetLoading}
        shift={sheetShift}
        fallbackSummary={sheetShift ? null : sheetFallback}
        fallbackTitle={sheetTitle}
        onClose={() => {
          setSheetOpen(false);
          setSheetShift(null);
          setSheetFallback(null);
        }}
      />
    </div>
  );
}
