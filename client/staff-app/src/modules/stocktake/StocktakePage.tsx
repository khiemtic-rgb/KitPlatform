import { useCallback, useEffect, useState } from 'react';
import { App, Button, Select, Space, Spin, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import {
  createCountingSession,
  fetchActiveCountingSession,
  fetchAdjustments,
} from '@/shared/api/inventory.api';
import { ADJUSTMENT_STATUS, ADJUSTMENT_STATUS_LABELS } from '@/shared/api/inventory.types';
import { fetchWarehouses } from '@/shared/api/sales.api';
import type { Warehouse } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanInventoryWrite } from '@/shared/auth/usePermission';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

export function StocktakePage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanInventoryWrite();
  const [loading, setLoading] = useState(true);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [sessions, setSessions] = useState<
    Awaited<ReturnType<typeof fetchAdjustments>>['items']
  >([]);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [wh, paged] = await Promise.all([
        fetchWarehouses(),
        fetchAdjustments({ page: 1, pageSize: 100 }),
      ]);
      setWarehouses(wh);
      setWarehouseId((prev) => prev ?? wh[0]?.id);
      setSessions(
        paged.items.filter(
          (row) => row.status === ADJUSTMENT_STATUS.Counting || row.status === ADJUSTMENT_STATUS.Draft,
        ),
      );
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiên kiểm kê'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const startSession = async () => {
    if (!warehouseId) return;
    setCreating(true);
    try {
      const active = await fetchActiveCountingSession(warehouseId);
      if (active) {
        message.info('Đã có phiên kiểm đang mở');
        navigate(`/stocktake/${active.id}`);
        return;
      }
      const created = await createCountingSession({
        warehouseId,
        reason: 'Kiểm kê tại quầy (app)',
      });
      message.success('Đã mở phiên kiểm kê');
      navigate(`/stocktake/${created.id}`);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được phiên kiểm'));
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader title="Kiểm kê kho" backTo="/" />
      <main className="staff-body">
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 13 }}>
          Đếm tồn trên điện thoại · quét mã · duyệt chênh lệch.
        </Typography.Text>

        <Select
          size="large"
          style={{ width: '100%', marginBottom: 12 }}
          placeholder="Chọn kho"
          value={warehouseId}
          options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          onChange={setWarehouseId}
        />

        {canWrite ? (
          <Button
            type="primary"
            block
            size="large"
            icon={<PlusOutlined />}
            loading={creating}
            onClick={() => void startSession()}
            style={{ marginBottom: 16 }}
          >
            Mở phiên kiểm kê mới
          </Button>
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
            Bạn chỉ có quyền xem. Cần quyền kho (inventory.write) để mở/duyệt kiểm kê.
          </Typography.Text>
        )}

        {loading ? <Spin /> : null}

        {!loading && sessions.length === 0 ? (
          <Typography.Text type="secondary">Chưa có phiên kiểm đang mở.</Typography.Text>
        ) : (
          sessions.map((row) => (
            <button
              key={row.id}
              type="button"
              className="search-hit"
              onClick={() => navigate(`/stocktake/${row.id}`)}
            >
              <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                <div style={{ textAlign: 'left' }}>
                  <Typography.Text strong>{row.adjustmentNumber}</Typography.Text>
                  <div style={{ fontSize: 12, color: '#64748b' }}>
                    {row.warehouseName} · {row.itemCount} dòng
                  </div>
                </div>
                <Tag>{ADJUSTMENT_STATUS_LABELS[row.status] ?? row.status}</Tag>
              </Space>
            </button>
          ))
        )}
      </main>
    </div>
  );
}
