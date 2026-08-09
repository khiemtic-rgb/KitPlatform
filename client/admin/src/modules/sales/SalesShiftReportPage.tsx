import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  DatePicker,
  Descriptions,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import {
  closeSalesShift,
  fetchBatchModeSettings,
  fetchSalesShiftSummary,
  fetchSalesShifts,
  openSalesShift,
  type TenantBatchModeValue,
} from '@/shared/api/sales.api';
import type { SalesShiftDetail, SalesShiftListItem, SalesShiftSummary } from '@/shared/api/sales.types';
import { SALES_SHIFT_STATUSES } from '@/shared/api/sales.types';
import {
  isShiftAlreadyOpenError,
  resolveOpenShiftForWarehouse,
  shiftAlreadyOpenMessage,
} from '@/modules/sales/sales-shift-helpers';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { CloseShiftModal } from '@/modules/sales/CloseShiftModal';
import { OpenShiftModal } from '@/modules/sales/OpenShiftModal';
import { ShiftSummaryPanel } from '@/modules/sales/shift-summary-ui';
import { enablesShiftFefoLotAlerts } from '@/modules/sales/tenant-batch-mode';
import { formatDisplayMoney } from '@/shared/utils/money';
import { formatDisplayDate } from '@/shared/utils/date';

const { RangePicker } = DatePicker;

function todayRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('day'), dayjs().endOf('day')];
}

export function SalesShiftReportPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftReport' });
  const { t: tPos } = useTranslation('sales', { keyPrefix: 'pos.messages' });
  const { message } = App.useApp();
  const canWrite = useHasPermission('sales.write');
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [shifts, setShifts] = useState<SalesShiftListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [openModal, setOpenModal] = useState(false);
  const [closeModal, setCloseModal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');

  const [historySearchInput, setHistorySearchInput] = useState('');
  const [historySearch, setHistorySearch] = useState('');
  const [historyWarehouseId, setHistoryWarehouseId] = useState<string | undefined>();
  const [historyStatus, setHistoryStatus] = useState<number | undefined>();
  const [historyDateRange, setHistoryDateRange] = useState<[Dayjs, Dayjs] | null>(null);

  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => todayRange());
  const [rangeSummary, setRangeSummary] = useState<SalesShiftSummary | null>(null);
  const [rangeLoading, setRangeLoading] = useState(false);

  useEffect(() => {
    void (async () => {
      const wh = await fetchWarehouses();
      setWarehouses(wh);
      const defaultWh = wh.find((w) => w.isDefault) ?? wh[0];
      if (defaultWh) setWarehouseId(defaultWh.id);
      try {
        setBatchMode(await fetchBatchModeSettings());
      } catch {
        /* giữ suggest */
      }
    })();
  }, []);

  const loadHistory = useCallback(async () => {
    setHistoryLoading(true);
    try {
      const hasSearch = Boolean(historySearch.trim());
      const useDate = !hasSearch && historyDateRange != null;
      setShifts(
        await fetchSalesShifts({
          search: historySearch.trim() || undefined,
          status: historyStatus,
          warehouseId: historyWarehouseId,
          from: useDate ? historyDateRange![0].startOf('day').toISOString() : undefined,
          to: useDate ? historyDateRange![1].startOf('day').add(1, 'day').toISOString() : undefined,
          limit: 100,
        }),
      );
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadHistoryFailed')));
    } finally {
      setHistoryLoading(false);
    }
  }, [historySearch, historyStatus, historyWarehouseId, historyDateRange, t, message]);

  const loadShiftState = useCallback(async () => {
    if (!warehouseId) return;
    setLoading(true);
    try {
      setOpenShift(await resolveOpenShiftForWarehouse(warehouseId));
    } catch (error) {
      setOpenShift(null);
      message.error(apiErrorMessage(error, t('messages.loadCurrentFailed')));
    } finally {
      setLoading(false);
    }
  }, [warehouseId, t, message]);

  useEffect(() => {
    void loadShiftState();
  }, [loadShiftState]);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  const resetHistoryFilters = () => {
    setHistorySearchInput('');
    setHistorySearch('');
    setHistoryWarehouseId(undefined);
    setHistoryStatus(undefined);
    setHistoryDateRange(null);
  };

  const loadRangeSummary = useCallback(async () => {
    setRangeLoading(true);
    try {
      const data = await fetchSalesShiftSummary(range[0].toISOString(), range[1].toISOString());
      setRangeSummary(data);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadReportFailed')));
    } finally {
      setRangeLoading(false);
    }
  }, [range, t, message]);

  useEffect(() => {
    void loadRangeSummary();
  }, [loadRangeSummary]);

  const handleOpenShift = async (openingCash: number) => {
    if (!warehouseId) {
      message.warning(tPos('selectWarehouseForShift'));
      throw new Error('missing warehouse');
    }
    setSaving(true);
    try {
      const shift = await openSalesShift({ warehouseId, openingCash });
      setOpenShift(shift);
      setOpenModal(false);
      message.success(tPos('shiftOpened', { number: shift.shiftNumber }));
      await Promise.all([loadShiftState(), loadHistory()]);
    } catch (error) {
      if (isShiftAlreadyOpenError(error)) {
        const existing = await resolveOpenShiftForWarehouse(warehouseId);
        if (existing) {
          setOpenShift(existing);
          setOpenModal(false);
          message.info(shiftAlreadyOpenMessage(existing));
          return;
        }
      }
      message.error(apiErrorMessage(error, tPos('openShiftFailed')));
      throw error;
    } finally {
      setSaving(false);
    }
  };

  const handleCloseShift = async (closingCash: number, closeNotes?: string) => {
    if (!openShift) return;
    setSaving(true);
    try {
      await closeSalesShift(openShift.id, { closingCash, closeNotes });
      setCloseModal(false);
      setOpenShift(null);
      message.success(t('messages.closeSuccess'));
      await Promise.all([loadShiftState(), loadHistory()]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.closeFailed')));
    } finally {
      setSaving(false);
    }
  };

  const shiftColumns: ColumnsType<SalesShiftListItem> = useMemo(
    () => [
      { title: t('history.columns.shiftNumber'), dataIndex: 'shiftNumber' },
      { title: t('history.columns.warehouse'), dataIndex: 'warehouseName' },
      { title: t('history.columns.openedBy'), dataIndex: 'openedByUserName' },
      {
        title: t('history.columns.openedAt'),
        dataIndex: 'openedAt',
        render: (v: string) => dayjs(v).format('DD-MM-YYYY HH:mm'),
      },
      {
        title: t('history.columns.openingCash'),
        dataIndex: 'openingCash',
        align: 'right',
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('history.columns.cashVariance'),
        dataIndex: 'cashVariance',
        align: 'right',
        render: (v?: number) => (v != null ? formatDisplayMoney(v) : '—'),
      },
      {
        title: t('history.columns.status'),
        dataIndex: 'status',
        render: (s: number) =>
          s === SALES_SHIFT_STATUSES.Open ? (
            <Tag color="processing">{t('currentShift.statusOpen')}</Tag>
          ) : (
            <Tag color="default">{t('currentShift.statusClosed')}</Tag>
          ),
      },
    ],
    [t],
  );

  const warehouseName = warehouses.find((w) => w.id === warehouseId)?.warehouseName;

  const openShiftFromHistory = useMemo(
    () =>
      warehouseId
        ? shifts.find(
            (row) => row.warehouseId === warehouseId && row.status === SALES_SHIFT_STATUSES.Open,
          )
        : undefined,
    [shifts, warehouseId],
  );

  return (
    <>
      <Card title={t('currentShift.title')} loading={loading}>
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            style={{ minWidth: 220 }}
            placeholder={t('currentShift.warehousePlaceholder')}
            value={warehouseId}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
            onChange={setWarehouseId}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void loadShiftState()}>
            {t('currentShift.reload')}
          </Button>
          {canWrite && !openShift && (
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpenModal(true)}>
              {t('currentShift.openShift')}
            </Button>
          )}
          {canWrite && openShift && (
            <Button danger icon={<StopOutlined />} onClick={() => setCloseModal(true)}>
              {t('currentShift.closeShift')}
            </Button>
          )}
        </Space>

        {!openShift && openShiftFromHistory && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('currentShift.staleOpenHint', {
              shiftNumber: openShiftFromHistory.shiftNumber,
              defaultValue: `Đã có ca ${openShiftFromHistory.shiftNumber} đang mở. Bấm Tải lại hoặc Mở ca để đồng bộ.`,
            })}
            action={
              <Button size="small" onClick={() => void loadShiftState()}>
                {t('currentShift.reload')}
              </Button>
            }
          />
        )}

        {openShift ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Descriptions size="small" bordered column={2}>
              <Descriptions.Item label={t('currentShift.descriptions.shiftNumber')}>
                {openShift.shiftNumber}
              </Descriptions.Item>
              <Descriptions.Item label={t('currentShift.descriptions.warehouse')}>
                {openShift.warehouseName}
              </Descriptions.Item>
              <Descriptions.Item label={t('currentShift.descriptions.openedBy')}>
                {openShift.openedByUserName}
              </Descriptions.Item>
              <Descriptions.Item label={t('currentShift.descriptions.openedAt')}>
                {dayjs(openShift.openedAt).format('DD-MM-YYYY HH:mm')}
              </Descriptions.Item>
              <Descriptions.Item label={t('currentShift.descriptions.openingCash')}>
                {formatDisplayMoney(openShift.openingCash)}
              </Descriptions.Item>
              <Descriptions.Item label={t('currentShift.descriptions.status')}>
                <Tag color="processing">{t('currentShift.statusOpen')}</Tag>
              </Descriptions.Item>
            </Descriptions>
            {enablesShiftFefoLotAlerts(batchMode) &&
              openShift.lotAlerts &&
              openShift.lotAlerts.length > 0 && (
                <Alert
                  type="warning"
                  showIcon
                  message={t('currentShift.fefoAlert.title', {
                    source:
                      openShift.lotAlerts[0]?.stockSourceLabel ??
                      t('currentShift.fefoAlert.defaultSource'),
                  })}
                  description={
                    <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>
                      {openShift.lotAlerts.map((alert) => (
                        <li key={`${alert.productId}-${alert.soldBatchNumber}-${alert.earlierBatchNumber}`}>
                          {t('currentShift.fefoAlert.line', {
                            productCode: alert.productCode,
                            soldBatch: alert.soldBatchNumber,
                            soldExpiry: alert.soldExpiryDate
                              ? t('currentShift.fefoAlert.expirySuffix', {
                                  date: formatDisplayDate(alert.soldExpiryDate),
                                })
                              : '',
                            earlierBatch: alert.earlierBatchNumber,
                            earlierExpiry: alert.earlierExpiryDate
                              ? t('currentShift.fefoAlert.expirySuffix', {
                                  date: formatDisplayDate(alert.earlierExpiryDate),
                                })
                              : '',
                            qty: alert.earlierBookQuantity.toLocaleString(),
                          })}
                        </li>
                      ))}
                    </ul>
                  }
                />
              )}
            <ShiftSummaryPanel summary={openShift.summary} showCashReconciliation />
          </Space>
        ) : (
          <Typography.Paragraph type="secondary">{t('currentShift.noOpenShift')}</Typography.Paragraph>
        )}
      </Card>

      <Card title={t('history.title')} style={{ marginTop: 16 }}>
        <Space wrap style={{ marginBottom: 12, width: '100%' }} size={8}>
          <Input.Search
            allowClear
            placeholder={t('history.filters.searchPlaceholder')}
            value={historySearchInput}
            onChange={(e) => setHistorySearchInput(e.target.value)}
            onSearch={(v) => setHistorySearch(v.trim())}
            style={{ width: 200 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('history.filters.warehouse')}
            style={{ width: 200 }}
            value={historyWarehouseId}
            onChange={setHistoryWarehouseId}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
          <Select
            allowClear
            placeholder={t('history.filters.status')}
            style={{ width: 140 }}
            value={historyStatus}
            onChange={setHistoryStatus}
            options={[
              { value: SALES_SHIFT_STATUSES.Open, label: t('currentShift.statusOpen') },
              { value: SALES_SHIFT_STATUSES.Closed, label: t('currentShift.statusClosed') },
            ]}
          />
          <RangePicker
            allowClear
            value={historyDateRange}
            onChange={(value) => {
              if (value?.[0] && value[1]) {
                setHistoryDateRange([value[0].startOf('day'), value[1].endOf('day')]);
              } else {
                setHistoryDateRange(null);
              }
            }}
            style={{ width: 260 }}
            format="DD/MM/YYYY"
            placeholder={[t('history.filters.dateFrom'), t('history.filters.dateTo')]}
          />
          <Button onClick={resetHistoryFilters}>{t('history.filters.clear')}</Button>
          <Button
            type="primary"
            ghost
            icon={<ReloadOutlined />}
            loading={historyLoading}
            onClick={() => void loadHistory()}
          >
            {t('history.filters.reload')}
          </Button>
        </Space>
        <Table
          rowKey="id"
          size="small"
          loading={historyLoading}
          dataSource={shifts}
          columns={shiftColumns}
          pagination={{ pageSize: 10 }}
        />
      </Card>

      <Card title={t('rangeReport.title')} style={{ marginTop: 16 }}>
        <Space wrap style={{ marginBottom: 16 }}>
          <RangePicker
            showTime
            value={range}
            onChange={(values) => {
              if (values?.[0] && values[1]) setRange([values[0], values[1]]);
            }}
          />
          <Button type="primary" ghost loading={rangeLoading} onClick={() => void loadRangeSummary()}>
            {t('rangeReport.reload')}
          </Button>
        </Space>
        {rangeSummary && <ShiftSummaryPanel summary={rangeSummary} loading={rangeLoading} />}
      </Card>

      <OpenShiftModal
        open={openModal}
        loading={saving}
        warehouseName={warehouseName}
        onCancel={() => setOpenModal(false)}
        onConfirm={(cash) => handleOpenShift(cash)}
      />

      <CloseShiftModal
        open={closeModal}
        loading={saving}
        shift={openShift}
        onCancel={() => setCloseModal(false)}
        onConfirm={(cash, notes) => void handleCloseShift(cash, notes)}
      />
    </>
  );
}
