import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Descriptions,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import {
  closeSalesShift,
  fetchBatchModeSettings,
  fetchOpenShift,
  fetchSalesShiftSummary,
  fetchSalesShifts,
  openSalesShift,
  type TenantBatchModeValue,
} from '@/shared/api/sales.api';
import type { SalesShiftDetail, SalesShiftListItem, SalesShiftSummary } from '@/shared/api/sales.types';
import { SALES_SHIFT_STATUSES } from '@/shared/api/sales.types';
import {
  isShiftAlreadyOpenError,
  loadOpenShiftForWarehouse,
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

export function SalesShiftReportPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftReport' });
  const { t: tPos } = useTranslation('sales', { keyPrefix: 'pos.messages' });
  const canWrite = useHasPermission('sales.write');
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [shifts, setShifts] = useState<SalesShiftListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [openModal, setOpenModal] = useState(false);
  const [closeModal, setCloseModal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');

  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [
    dayjs().startOf('day'),
    dayjs().endOf('day'),
  ]);
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

  const loadShiftState = useCallback(async () => {
    if (!warehouseId) return;
    setLoading(true);
    try {
      setOpenShift(await loadOpenShiftForWarehouse(warehouseId));
    } catch (error) {
      setOpenShift(null);
      message.error(apiErrorMessage(error, t('messages.loadCurrentFailed')));
    }
    try {
      setShifts(await fetchSalesShifts(30));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadHistoryFailed')));
    } finally {
      setLoading(false);
    }
  }, [warehouseId, t]);

  useEffect(() => {
    void loadShiftState();
  }, [loadShiftState]);

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
  }, [range, t]);

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
      await loadShiftState();
    } catch (error) {
      if (isShiftAlreadyOpenError(error)) {
        const existing = await fetchOpenShift(warehouseId);
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
      await loadShiftState();
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
        <Table
          rowKey="id"
          size="small"
          loading={loading}
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
