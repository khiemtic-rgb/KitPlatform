import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Popconfirm,
  Select,
  Spin,
  Tag,
  Typography,
} from 'antd';
import { PlusOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  cancelAdjustment,
  createCountingSession,
  fetchActiveCountingSession,
  fetchAdjustments,
} from '@/shared/api/inventory.api';
import {
  ADJUSTMENT_STATUS,
  ADJUSTMENT_STATUS_COLORS,
  ADJUSTMENT_STATUS_LABELS,
  type AdjustmentListItem,
} from '@/shared/api/inventory.types';
import { fetchWarehouses } from '@/shared/api/sales.api';
import type { Warehouse } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanInventoryWrite } from '@/shared/auth/usePermission';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';
import { usePosSession } from '@/modules/pos/pos-session.store';

function warehouseOptionLabel(w: Warehouse) {
  return w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName;
}

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

export function StocktakePage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanInventoryWrite();
  const posWarehouseId = usePosSession((s) => s.warehouseId);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [sessions, setSessions] = useState<AdjustmentListItem[]>([]);
  const [activeForWarehouse, setActiveForWarehouse] = useState<AdjustmentListItem | null>(null);
  const [creating, setCreating] = useState(false);
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  const load = useCallback(
    async (mode: 'full' | 'refresh' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else {
        setRefreshing(true);
      }
      try {
        const [wh, paged] = await Promise.all([
          fetchWarehouses(),
          fetchAdjustments({ page: 1, pageSize: 100 }),
        ]);
        setWarehouses(wh);
        setWarehouseId((prev) => {
          if (prev && wh.some((w) => w.id === prev)) return prev;
          if (posWarehouseId && wh.some((w) => w.id === posWarehouseId)) return posWarehouseId;
          return wh[0]?.id;
        });
        setSessions(
          paged.items.filter(
            (row) =>
              row.status === ADJUSTMENT_STATUS.Counting || row.status === ADJUSTMENT_STATUS.Draft,
          ),
        );
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được phiên kiểm kê');
        if (mode === 'full') {
          setSessions([]);
          setLoadError(text);
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

  useEffect(() => {
    if (!warehouseId) {
      setActiveForWarehouse(null);
      return;
    }
    let cancelled = false;
    void fetchActiveCountingSession(warehouseId)
      .then((active) => {
        if (!cancelled) setActiveForWarehouse(active);
      })
      .catch(() => {
        if (!cancelled) setActiveForWarehouse(null);
      });
    return () => {
      cancelled = true;
    };
  }, [warehouseId, sessions]);

  const selectedWarehouse = useMemo(
    () => warehouses.find((w) => w.id === warehouseId),
    [warehouses, warehouseId],
  );

  const sessionsForWarehouse = useMemo(() => {
    if (!warehouseId) return sessions;
    return sessions.filter((s) => s.warehouseId === warehouseId);
  }, [sessions, warehouseId]);

  const startSession = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho cần kiểm kê');
      return;
    }
    setCreating(true);
    try {
      const active = await fetchActiveCountingSession(warehouseId);
      if (active) {
        message.info(`Đã có phiên ${active.adjustmentNumber} đang mở — tiếp tục đếm`);
        navigate(`/stocktake/${active.id}`);
        return;
      }
      const created = await createCountingSession({
        warehouseId,
        reason: 'Kiểm kê tại quầy (app)',
      });
      message.success(`Đã mở phiên ${created.adjustmentNumber}`);
      navigate(`/stocktake/${created.id}`);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được phiên kiểm'));
    } finally {
      setCreating(false);
    }
  };

  const cancelSession = async (row: AdjustmentListItem) => {
    setCancellingId(row.id);
    try {
      await cancelAdjustment(row.id);
      message.success(`Đã hủy ${row.adjustmentNumber} — tồn không đổi`);
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiên'));
    } finally {
      setCancellingId(null);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Kiểm kê kho"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : sessionsForWarehouse.length > 0
              ? `${sessionsForWarehouse.length} phiên đang mở`
              : 'Chưa có phiên đang mở'
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
      <main className="staff-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được kiểm kê"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
            Quy trình: mở phiên → quét/đếm trên điện thoại → duyệt chênh lệch.
          </Typography.Text>
        )}

        <section className="stocktake-panel">
          <Typography.Text type="secondary" className="stocktake-field-label">
            Kho cần kiểm kê
          </Typography.Text>
          <Select
            size="large"
            style={{ width: '100%' }}
            placeholder="Chọn kho"
            value={warehouseId}
            disabled={Boolean(loadError) && warehouses.length === 0}
            options={warehouses.map((w) => ({
              value: w.id,
              label: warehouseOptionLabel(w),
            }))}
            onChange={setWarehouseId}
          />
          {selectedWarehouse ? (
            <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
              {activeForWarehouse
                ? `Kho này đang có phiên ${activeForWarehouse.adjustmentNumber} (${ADJUSTMENT_STATUS_LABELS[activeForWarehouse.status] ?? activeForWarehouse.status}).`
                : 'Kho này chưa có phiên kiểm đang mở.'}
            </Typography.Text>
          ) : null}
        </section>

        {!canWrite ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chỉ xem"
            description="Cần quyền kho (inventory.write) để mở phiên / quét / duyệt kiểm kê."
          />
        ) : (
          <div className="stocktake-actions">
            {activeForWarehouse ? (
              <Button
                type="primary"
                block
                size="large"
                onClick={() => navigate(`/stocktake/${activeForWarehouse.id}`)}
              >
                Tiếp tục {activeForWarehouse.adjustmentNumber}
              </Button>
            ) : (
              <Button
                type="primary"
                block
                size="large"
                icon={<PlusOutlined />}
                loading={creating}
                disabled={!warehouseId}
                onClick={() => void startSession()}
              >
                Mở phiên kiểm kê mới
              </Button>
            )}
          </div>
        )}

        <Typography.Text strong style={{ display: 'block', margin: '4px 0 10px' }}>
          Phiên đang mở
          {selectedWarehouse ? ` · ${selectedWarehouse.warehouseName}` : ''}
        </Typography.Text>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && sessions.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bấm Thử lại khi mạng ổn" />
        ) : sessionsForWarehouse.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description="Chưa có phiên kiểm đang mở cho kho này. Bấm mở phiên mới để bắt đầu đếm."
          />
        ) : (
          sessionsForWarehouse.map((row) => (
            <div key={row.id} className="stocktake-session-card">
              <button
                type="button"
                className="stocktake-session-card__main"
                onClick={() => navigate(`/stocktake/${row.id}`)}
              >
                <div className="stocktake-session-card__head">
                  <div>
                    <Typography.Text strong className="stocktake-session-card__number">
                      {row.adjustmentNumber}
                    </Typography.Text>
                    <Typography.Text type="secondary" className="stocktake-session-card__when">
                      {formatWhen(row.adjustmentDate)}
                    </Typography.Text>
                  </div>
                  <Tag color={ADJUSTMENT_STATUS_COLORS[row.status] ?? 'default'}>
                    {ADJUSTMENT_STATUS_LABELS[row.status] ?? row.status}
                  </Tag>
                </div>
                <div className="stocktake-session-card__meta">
                  <span>{row.warehouseName}</span>
                  <span>· {row.itemCount} dòng đếm</span>
                </div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Bấm để quét mã / ghi nhận số lượng
                </Typography.Text>
              </button>
              {canWrite ? (
                <div className="stocktake-session-card__actions">
                  <Popconfirm
                    title={`Hủy ${row.adjustmentNumber}?`}
                    description="Xóa phiên và dòng đếm. Tồn kho không đổi."
                    okText="Hủy phiên"
                    cancelText="Giữ lại"
                    okButtonProps={{ danger: true, loading: cancellingId === row.id }}
                    onConfirm={() => void cancelSession(row)}
                  >
                    <Button
                      danger
                      type="text"
                      size="small"
                      icon={<StopOutlined />}
                      loading={cancellingId === row.id}
                      onClick={(e) => e.stopPropagation()}
                    >
                      Hủy phiên
                    </Button>
                  </Popconfirm>
                </div>
              ) : null}
            </div>
          ))
        )}
      </main>
    </div>
  );
}
