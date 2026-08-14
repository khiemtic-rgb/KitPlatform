import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  Popconfirm,
  Segmented,
  Spin,
  Tag,
  Typography,
} from 'antd';
import { PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { completeGoodsReceipt, cancelGoodsReceipt, fetchGoodsReceipts } from '@/shared/api/procurement.api';
import {
  GRN_STATUS,
  GRN_STATUS_LABELS,
  GRN_STATUS_TAG,
  type GoodsReceiptListItem,
} from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { useCanProcurementWrite } from '@/shared/auth/usePermission';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type StatusFilter = 'all' | 'pending' | 'completed' | 'cancelled';

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM/YYYY');
}

function statusToApi(filter: StatusFilter): number | undefined {
  if (filter === 'pending') return GRN_STATUS.Pending;
  if (filter === 'completed') return GRN_STATUS.Completed;
  if (filter === 'cancelled') return GRN_STATUS.Cancelled;
  return undefined;
}

export function GoodsReceiptListPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanProcurementWrite();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<GoodsReceiptListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState('');
  const [searchApplied, setSearchApplied] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [completingId, setCompletingId] = useState<string | null>(null);
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
        const result = await fetchGoodsReceipts({
          page: 1,
          pageSize: 50,
          search: searchApplied || undefined,
          status: statusToApi(statusFilter),
        });
        setItems(result.items);
        setTotal(result.total);
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được phiếu nhập');
        if (mode === 'full') {
          setItems([]);
          setTotal(0);
          setLoadError(text);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [message, searchApplied, statusFilter],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  const pendingCount = useMemo(
    () => items.filter((row) => row.status === GRN_STATUS.Pending).length,
    [items],
  );

  const completeReceipt = async (id: string, grnNumber: string) => {
    setCompletingId(id);
    try {
      await completeGoodsReceipt(id);
      message.success(`Đã hoàn tất ${grnNumber} — tồn đã cập nhật`);
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được phiếu nhập'));
    } finally {
      setCompletingId(null);
    }
  };

  const cancelReceipt = async (id: string, grnNumber: string) => {
    setCancellingId(id);
    try {
      await cancelGoodsReceipt(id);
      message.success(`Đã hủy ${grnNumber} — tồn không đổi`);
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu nhập'));
    } finally {
      setCancellingId(null);
    }
  };

  const subtitle = loadError
    ? 'Không tải được danh sách'
    : statusFilter === 'pending'
      ? `${items.length} phiếu chờ nhập`
      : pendingCount > 0
        ? `${total} phiếu · ${pendingCount} chờ nhập`
        : `${total} phiếu nhập`;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Nhập hàng"
        subtitle={subtitle}
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
            message="Không tải được phiếu nhập"
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
            Lưu nháp chưa đụng tồn · Hoàn tất mới cộng kho. Cần lô + HSD từng dòng.
          </Typography.Text>
        )}

        {!canWrite ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chỉ xem"
            description="Cần quyền mua hàng (procurement.write) để tạo / hoàn tất / hủy phiếu nhập."
          />
        ) : (
          <Button
            type="primary"
            block
            size="large"
            icon={<PlusOutlined />}
            className="grn-primary-cta"
            onClick={() => navigate('/goods-receipt/new')}
          >
            Nhập hàng mới
          </Button>
        )}

        <Input
          allowClear
          size="large"
          className="grn-search"
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm số phiếu, NCC, PO…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onPressEnter={() => setSearchApplied(search.trim())}
          onBlur={() => {
            if (search.trim() !== searchApplied) setSearchApplied(search.trim());
          }}
        />

        <Segmented
          block
          className="grn-status-filter"
          value={statusFilter}
          onChange={(v) => setStatusFilter(v as StatusFilter)}
          options={[
            { label: 'Tất cả', value: 'all' },
            { label: 'Chờ nhập', value: 'pending' },
            { label: 'Hoàn tất', value: 'completed' },
            { label: 'Đã hủy', value: 'cancelled' },
          ]}
        />

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && items.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bấm Thử lại khi mạng ổn" />
        ) : items.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              searchApplied || statusFilter !== 'all'
                ? 'Không có phiếu khớp bộ lọc'
                : 'Chưa có phiếu nhập. Bấm Nhập hàng mới để bắt đầu.'
            }
          />
        ) : (
          items.map((item) => {
            const isPending = item.status === GRN_STATUS.Pending;
            return (
              <article key={item.id} className="grn-card">
                <button
                  type="button"
                  className="grn-card__main"
                  onClick={() => navigate(`/goods-receipt/${item.id}`)}
                >
                  <div className="grn-card__head">
                    <div>
                      <Typography.Text strong className="grn-card__number">
                        {item.grnNumber}
                      </Typography.Text>
                      <Typography.Text type="secondary" className="grn-card__when">
                        {formatWhen(item.receiptDate)}
                      </Typography.Text>
                    </div>
                    <Tag color={GRN_STATUS_TAG[item.status] ?? 'default'}>
                      {GRN_STATUS_LABELS[item.status] ?? item.status}
                    </Tag>
                  </div>
                  <div className="grn-card__meta">
                    <span>{item.supplierName}</span>
                    <span>· {item.warehouseName}</span>
                  </div>
                  <div className="grn-card__meta">
                    <span>{item.itemCount} dòng</span>
                    {item.poNumber ? <span>· PO {item.poNumber}</span> : null}
                  </div>
                  {item.totalAmount != null && item.totalAmount > 0 ? (
                    <div className="grn-card__amount">{formatMoney(item.totalAmount)}</div>
                  ) : null}
                  {isPending ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 6 }}>
                      Chưa cộng tồn — bấm Hoàn tất sau khi kiểm lô/HSD
                    </Typography.Text>
                  ) : null}
                </button>
                <div className="grn-card__actions">
                  <Button size="middle" onClick={() => navigate(`/goods-receipt/${item.id}`)}>
                    Chi tiết
                  </Button>
                  {canWrite && isPending ? (
                    <>
                      <Popconfirm
                        title={`Hoàn tất ${item.grnNumber}?`}
                        description="Tồn kho sẽ tăng theo số lượng trên phiếu. Kiểm tra lô/HSD trước."
                        okText="Hoàn tất"
                        cancelText="Đóng"
                        onConfirm={() => void completeReceipt(item.id, item.grnNumber)}
                      >
                        <Button
                          size="middle"
                          type="primary"
                          loading={completingId === item.id}
                          onClick={(e) => e.stopPropagation()}
                        >
                          Hoàn tất
                        </Button>
                      </Popconfirm>
                      <Popconfirm
                        title={`Hủy ${item.grnNumber}?`}
                        description="Chỉ hủy được khi còn chờ nhập. Tồn không đổi."
                        okText="Hủy phiếu"
                        cancelText="Giữ"
                        okButtonProps={{ danger: true }}
                        onConfirm={() => void cancelReceipt(item.id, item.grnNumber)}
                      >
                        <Button
                          size="middle"
                          danger
                          loading={cancellingId === item.id}
                          onClick={(e) => e.stopPropagation()}
                        >
                          Hủy
                        </Button>
                      </Popconfirm>
                    </>
                  ) : null}
                </div>
              </article>
            );
          })
        )}
      </main>
    </div>
  );
}
