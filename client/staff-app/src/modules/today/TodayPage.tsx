import { useCallback, useEffect, useMemo, useState } from 'react';
import { App, Alert, Button, Empty, Spin, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  fetchBatchModeSettings,
  fetchOpenShift,
  fetchShiftSummary,
  fetchWarehouses,
} from '@/shared/api/sales.api';
import type { SalesShiftDetail, SalesShiftSummary, TenantBatchModeValue } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { enablesShiftFefoLotAlerts } from '@/modules/sales/tenant-batch-mode';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { CloseShiftSheet } from '@/modules/today/CloseShiftSheet';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { useCanSalesWrite } from '@/shared/auth/usePermission';
import { useNavigate } from 'react-router-dom';

const PAYMENT_LABEL: Record<number, string> = {
  1: 'Tiền mặt',
  2: 'Thẻ',
  3: 'Chuyển khoản',
  4: 'Ví điện tử',
  5: 'Ghi nợ',
};

/** Khoảng ngày lịch máy (có offset) — tránh lệch UTC khi bind DateTime trên API. */
function localDayRangeQuery() {
  const from = dayjs().startOf('day');
  const to = dayjs().add(1, 'day').startOf('day');
  return {
    from: from.format('YYYY-MM-DDTHH:mm:ssZ'),
    to: to.format('YYYY-MM-DDTHH:mm:ssZ'),
    label: from.format('DD/MM/YYYY'),
  };
}

function MethodRows({ summary }: { summary: SalesShiftSummary }) {
  if (summary.byMethod.length === 0) {
    return (
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Chưa có giao dịch theo hình thức
      </Typography.Text>
    );
  }
  return (
    <div className="today-methods">
      {summary.byMethod.map((row) => (
        <div key={row.paymentMethod} className="today-method-row">
          <span>{PAYMENT_LABEL[row.paymentMethod] ?? `HT ${row.paymentMethod}`}</span>
          <span className="today-method-row__vals">
            <span className="today-method-row__detail">
              Bán {formatMoney(row.salesAmount)}
              {row.refundAmount > 0 ? ` · Hoàn ${formatMoney(row.refundAmount)}` : ''}
            </span>
            <strong>{formatMoney(row.netAmount)}</strong>
          </span>
        </div>
      ))}
    </div>
  );
}

function SummaryPanel({
  title,
  hint,
  summary,
  emphasize,
}: {
  title: string;
  hint?: string;
  summary: SalesShiftSummary;
  emphasize?: boolean;
}) {
  return (
    <section className={`today-panel${emphasize ? ' today-panel--hero' : ''}`}>
      <div className="today-panel__head">
        <Typography.Text strong>{title}</Typography.Text>
        {hint ? (
          <Typography.Text type="secondary" className="today-panel__hint">
            {hint}
          </Typography.Text>
        ) : null}
      </div>
      <div className="today-panel__net">
        <span>Doanh thu thuần</span>
        <strong>{formatMoney(summary.netTotal)}</strong>
      </div>
      <Typography.Text type="secondary" className="today-panel__sub">
        Bán {formatMoney(summary.totalSales)} · Hoàn {formatMoney(summary.totalRefunds)}
      </Typography.Text>
      <MethodRows summary={summary} />
    </section>
  );
}

export function TodayPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanSalesWrite();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [warehouseName, setWarehouseName] = useState<string>();
  const [warehouseId, setWarehouseId] = useState<string>();
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [todaySummary, setTodaySummary] = useState<SalesShiftSummary | null>(null);
  const [dayLabel, setDayLabel] = useState(dayjs().format('DD/MM/YYYY'));
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');
  const [closeOpen, setCloseOpen] = useState(false);

  const load = useCallback(
    async (mode: 'full' | 'refresh' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else {
        setRefreshing(true);
      }
      try {
        const warehouses = await fetchWarehouses();
        const whId = posWarehouseId ?? warehouses[0]?.id;
        const wh = warehouses.find((w) => w.id === whId);
        setWarehouseId(whId);
        setWarehouseName(wh?.warehouseName);

        const range = localDayRangeQuery();
        setDayLabel(range.label);

        const [shift, daySummary, modeSetting] = await Promise.all([
          whId ? fetchOpenShift(whId) : Promise.resolve(null),
          whId
            ? fetchShiftSummary(range.from, range.to, whId)
            : fetchShiftSummary(range.from, range.to),
          fetchBatchModeSettings().catch(() => 'suggest' as TenantBatchModeValue),
        ]);
        setOpenShift(shift);
        setTodaySummary(daySummary);
        setBatchMode(modeSetting);
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được số liệu hôm nay');
        if (mode === 'full') {
          setLoadError(text);
          setOpenShift(null);
          setTodaySummary(null);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [message, posWarehouseId],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  const cashVarianceHint = useMemo(() => {
    if (!openShift?.summary) return null;
    return `Đầu ca ${formatMoney(openShift.summary.openingCash)} + bán TM ${formatMoney(openShift.summary.cashSales)} − hoàn TM ${formatMoney(openShift.summary.cashRefunds)}`;
  }, [openShift]);

  const dayVsShiftNote = useMemo(() => {
    if (!openShift?.summary || !todaySummary) return null;
    if (todaySummary.netTotal + 0.01 >= openShift.summary.netTotal) return null;
    return 'Số cả ngày thấp hơn ca — đã lọc theo kho hiện tại; bấm tải lại nếu vừa bán.';
  }, [openShift, todaySummary]);

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Hôm nay"
        subtitle={
          openShift
            ? `Ca ${openShift.shiftNumber} · ${warehouseName ?? 'Kho'}`
            : warehouseName
              ? `${warehouseName} · ${dayLabel}`
              : dayLabel
        }
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className={`staff-body today-body${openShift ? ' today-body--actions' : ''}`}>
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được số liệu"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : null}

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : null}

        {!loading && !loadError ? (
          <>
            {openShift ? (
              <Alert
                type="success"
                showIcon
                className="today-shift-alert"
                message={`Ca ${openShift.shiftNumber} đang mở`}
                description={
                  <>
                    {openShift.warehouseName ?? warehouseName ?? 'Kho'}
                    {openShift.openedAt
                      ? ` · từ ${dayjs(openShift.openedAt).format('HH:mm DD/MM')}`
                      : ''}
                    {' · đóng ca khi kết thúc phiên quầy'}
                  </>
                }
              />
            ) : (
              <Alert
                type="warning"
                showIcon
                className="today-shift-alert"
                message="Chưa mở ca"
                description="Cần mở ca tại Bán hàng trước khi bán / trả / thu. Số liệu ca chỉ có khi đang mở ca."
                action={
                  <Button size="small" type="primary" onClick={() => navigate('/pos')}>
                    Vào POS
                  </Button>
                }
              />
            )}

            {!canWrite ? (
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 12 }}
                message="Chỉ xem"
                description="Cần quyền bán hàng (sales.write) để đóng ca."
              />
            ) : null}

            {enablesShiftFefoLotAlerts(batchMode) &&
            openShift?.lotAlerts &&
            openShift.lotAlerts.length > 0 ? (
              <Alert
                type="warning"
                showIcon
                message={`Cảnh báo lô FEFO (${openShift.lotAlerts[0]?.stockSourceLabel ?? 'Hệ thống'})`}
                description={
                  <ul className="today-fefo-list">
                    {openShift.lotAlerts.map((alert) => (
                      <li key={`${alert.productId}-${alert.soldBatchNumber}-${alert.earlierBatchNumber}`}>
                        <strong>{alert.productCode}</strong>: bán lô {alert.soldBatchNumber}
                        {alert.soldExpiryDate ? ` (HSD ${dayjs(alert.soldExpiryDate).format('MM/YYYY')})` : ''}
                        {' — còn lô '}
                        {alert.earlierBatchNumber}
                        {alert.earlierExpiryDate
                          ? ` (HSD ${dayjs(alert.earlierExpiryDate).format('MM/YYYY')})`
                          : ''}{' '}
                        tồn {alert.earlierBookQuantity.toLocaleString()}
                      </li>
                    ))}
                  </ul>
                }
                style={{ marginBottom: 12 }}
              />
            ) : null}

            {openShift?.summary ? (
              <SummaryPanel
                emphasize
                title="Trong ca hiện tại"
                hint="Theo đơn gắn ca · đúng két quầy"
                summary={openShift.summary}
              />
            ) : !openShift ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="Chưa có ca mở — mở ca để theo dõi doanh thu quầy"
                style={{ marginBottom: 12 }}
              />
            ) : null}

            {todaySummary ? (
              <>
                <SummaryPanel
                  title="Cả ngày theo lịch"
                  hint={`${dayLabel}${warehouseName ? ` · ${warehouseName}` : ''} · giờ máy`}
                  summary={todaySummary}
                />
                {dayVsShiftNote ? (
                  <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
                    {dayVsShiftNote}
                  </Typography.Text>
                ) : null}
              </>
            ) : null}

            {openShift?.summary ? (
              <section className="today-panel today-panel--cash">
                <div className="today-panel__head">
                  <Typography.Text strong>Tiền mặt trong ca</Typography.Text>
                </div>
                <div className="today-panel__net">
                  <span>Dự kiến trong két</span>
                  <strong>{formatMoney(openShift.summary.expectedCash)}</strong>
                </div>
                {cashVarianceHint ? (
                  <Typography.Text type="secondary" className="today-panel__sub">
                    {cashVarianceHint}
                  </Typography.Text>
                ) : null}
                <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                  Khi đóng ca: đếm tiền thật → hệ thống ghi chênh lệch so với dự kiến.
                </Typography.Text>
              </section>
            ) : null}

            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 8 }}>
              Kho đang xem: {warehouseName ?? warehouseId ?? '—'} · làm mới để cập nhật sau mỗi lần bán.
            </Typography.Text>
          </>
        ) : null}
      </main>

      {openShift && canWrite ? (
        <footer className="staff-footer today-footer">
          <Typography.Text type="secondary" className="today-footer__hint">
            Đóng ca khóa phiên · kiểm tra két trước khi xác nhận
          </Typography.Text>
          <Button danger block size="large" onClick={() => setCloseOpen(true)}>
            Đóng ca {openShift.shiftNumber}
          </Button>
        </footer>
      ) : null}

      <CloseShiftSheet
        open={closeOpen}
        shift={openShift}
        onClose={() => setCloseOpen(false)}
        onClosed={() => void load('refresh')}
      />
    </div>
  );
}
