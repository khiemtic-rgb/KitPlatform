import { useCallback, useEffect, useState } from 'react';
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
  fetchOpenShift,
  fetchSalesShiftSummary,
  fetchSalesShifts,
  openSalesShift,
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
import { formatDisplayMoney } from '@/shared/utils/money';
import { formatDisplayDate } from '@/shared/utils/date';

const { RangePicker } = DatePicker;

export function SalesShiftReportPage() {
  const canWrite = useHasPermission('sales.write');
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [shifts, setShifts] = useState<SalesShiftListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [openModal, setOpenModal] = useState(false);
  const [closeModal, setCloseModal] = useState(false);
  const [saving, setSaving] = useState(false);

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
    })();
  }, []);

  const loadShiftState = useCallback(async () => {
    if (!warehouseId) return;
    setLoading(true);
    try {
      setOpenShift(await loadOpenShiftForWarehouse(warehouseId));
    } catch (error) {
      setOpenShift(null);
      message.error(apiErrorMessage(error, 'Không tải ca hiện tại'));
    }
    try {
      setShifts(await fetchSalesShifts(30));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải lịch sử ca'));
    } finally {
      setLoading(false);
    }
  }, [warehouseId]);

  useEffect(() => {
    void loadShiftState();
  }, [loadShiftState]);

  const loadRangeSummary = useCallback(async () => {
    setRangeLoading(true);
    try {
      const data = await fetchSalesShiftSummary(range[0].toISOString(), range[1].toISOString());
      setRangeSummary(data);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được báo cáo'));
    } finally {
      setRangeLoading(false);
    }
  }, [range]);

  useEffect(() => {
    void loadRangeSummary();
  }, [loadRangeSummary]);

  const handleOpenShift = async (openingCash: number) => {
    if (!warehouseId) {
      message.warning('Chọn kho trước khi mở ca');
      throw new Error('missing warehouse');
    }
    setSaving(true);
    try {
      const shift = await openSalesShift({ warehouseId, openingCash });
      setOpenShift(shift);
      setOpenModal(false);
      message.success(`Đã mở ca ${shift.shiftNumber}`);
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
      message.error(apiErrorMessage(error, 'Không mở được ca'));
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
      message.success('Đã đóng ca');
      await loadShiftState();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đóng được ca'));
    } finally {
      setSaving(false);
    }
  };

  const shiftColumns: ColumnsType<SalesShiftListItem> = [
    { title: 'Số ca', dataIndex: 'shiftNumber' },
    { title: 'Kho', dataIndex: 'warehouseName' },
    { title: 'Mở bởi', dataIndex: 'openedByUserName' },
    {
      title: 'Mở lúc',
      dataIndex: 'openedAt',
      render: (v: string) => dayjs(v).format('DD-MM-YYYY HH:mm'),
    },
    {
      title: 'Quỹ đầu',
      dataIndex: 'openingCash',
      align: 'right',
      render: (v: number) => formatDisplayMoney(v),
    },
    {
      title: 'Chênh lệch TM',
      dataIndex: 'cashVariance',
      align: 'right',
      render: (v?: number) => (v != null ? formatDisplayMoney(v) : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      render: (s: number) =>
        s === SALES_SHIFT_STATUSES.Open ? (
          <Tag color="processing">Đang mở</Tag>
        ) : (
          <Tag color="default">Đã đóng</Tag>
        ),
    },
  ];

  const warehouseName = warehouses.find((w) => w.id === warehouseId)?.warehouseName;

  return (
    <>
      <Card title="Ca làm việc" loading={loading}>
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            style={{ minWidth: 220 }}
            placeholder="Chọn kho"
            value={warehouseId}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
            onChange={setWarehouseId}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void loadShiftState()}>
            Tải lại
          </Button>
          {canWrite && !openShift && (
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpenModal(true)}>
              Mở ca
            </Button>
          )}
          {canWrite && openShift && (
            <Button danger icon={<StopOutlined />} onClick={() => setCloseModal(true)}>
              Đóng ca
            </Button>
          )}
        </Space>

        {openShift ? (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Descriptions size="small" bordered column={2}>
              <Descriptions.Item label="Số ca">{openShift.shiftNumber}</Descriptions.Item>
              <Descriptions.Item label="Kho">{openShift.warehouseName}</Descriptions.Item>
              <Descriptions.Item label="Mở bởi">{openShift.openedByUserName}</Descriptions.Item>
              <Descriptions.Item label="Mở lúc">
                {dayjs(openShift.openedAt).format('DD-MM-YYYY HH:mm')}
              </Descriptions.Item>
              <Descriptions.Item label="Quỹ đầu ca">
                {formatDisplayMoney(openShift.openingCash)}
              </Descriptions.Item>
              <Descriptions.Item label="Trạng thái">
                <Tag color="processing">Đang mở</Tag>
              </Descriptions.Item>
            </Descriptions>
            {openShift.lotAlerts && openShift.lotAlerts.length > 0 && (
              <Alert
                type="warning"
                showIcon
                message={`Cảnh báo lô FEFO (${openShift.lotAlerts[0]?.stockSourceLabel ?? 'Tồn theo hệ thống'})`}
                description={
                  <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>
                    {openShift.lotAlerts.map((alert) => (
                      <li key={`${alert.productId}-${alert.soldBatchNumber}-${alert.earlierBatchNumber}`}>
                        {alert.productCode} — đã bán lô {alert.soldBatchNumber}
                        {alert.soldExpiryDate ? ` (HSD ${formatDisplayDate(alert.soldExpiryDate)})` : ''}
                        {' '}trong khi lô {alert.earlierBatchNumber}
                        {alert.earlierExpiryDate ? ` (HSD ${formatDisplayDate(alert.earlierExpiryDate)})` : ''}
                        {' '}còn {alert.earlierBookQuantity.toLocaleString('vi-VN')} trên sổ
                      </li>
                    ))}
                  </ul>
                }
              />
            )}
            <ShiftSummaryPanel summary={openShift.summary} showCashReconciliation />
          </Space>
        ) : (
          <Typography.Paragraph type="secondary">
            Chưa có ca mở cho kho này. Mở ca và nhập quỹ đầu ca trước khi bán hàng trên POS.
          </Typography.Paragraph>
        )}
      </Card>

      <Card title="Lịch sử ca" style={{ marginTop: 16 }}>
        <Table
          rowKey="id"
          size="small"
          loading={loading}
          dataSource={shifts}
          columns={shiftColumns}
          pagination={{ pageSize: 10 }}
        />
      </Card>

      <Card title="Báo cáo theo khoảng thời gian" style={{ marginTop: 16 }}>
        <Space wrap style={{ marginBottom: 16 }}>
          <RangePicker
            showTime
            value={range}
            onChange={(values) => {
              if (values?.[0] && values[1]) setRange([values[0], values[1]]);
            }}
          />
          <Button type="primary" ghost loading={rangeLoading} onClick={() => void loadRangeSummary()}>
            Tải lại
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
