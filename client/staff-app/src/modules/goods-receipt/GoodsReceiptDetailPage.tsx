import { useCallback, useEffect, useState } from 'react';
import { App, Alert, Button, Empty, Popconfirm, Spin, Tag, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate, useParams } from 'react-router-dom';
import { completeGoodsReceipt, cancelGoodsReceipt, fetchGoodsReceipt } from '@/shared/api/procurement.api';
import {
  GRN_STATUS,
  GRN_STATUS_LABELS,
  GRN_STATUS_TAG,
  type GoodsReceiptDetail,
} from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { useCanProcurementWrite } from '@/shared/auth/usePermission';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

function expiryHint(expiryDate?: string): { text: string; tone: 'ok' | 'warn' | 'bad' } | null {
  if (!expiryDate) return null;
  const parsed = dayjs(expiryDate);
  if (!parsed.isValid()) return null;
  const days = parsed.startOf('day').diff(dayjs().startOf('day'), 'day');
  if (days < 0) return { text: `Hết hạn ${Math.abs(days)} ngày`, tone: 'bad' };
  if (days <= 90) return { text: `Còn ${days} ngày`, tone: 'warn' };
  return { text: parsed.format('DD/MM/YYYY'), tone: 'ok' };
}

export function GoodsReceiptDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanProcurementWrite();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [detail, setDetail] = useState<GoodsReceiptDetail | null>(null);
  const [completing, setCompleting] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setLoadError(null);
    try {
      setDetail(await fetchGoodsReceipt(id));
    } catch (error) {
      setDetail(null);
      setLoadError(apiErrorMessage(error, 'Không tải được chi tiết phiếu nhập'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const complete = async () => {
    if (!id || !detail) return;
    setCompleting(true);
    try {
      await completeGoodsReceipt(id);
      message.success(`Đã hoàn tất ${detail.grnNumber} — tồn đã cập nhật`);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được phiếu nhập'));
    } finally {
      setCompleting(false);
    }
  };

  const cancel = async () => {
    if (!id || !detail) return;
    setCancelling(true);
    try {
      await cancelGoodsReceipt(id);
      message.success(`Đã hủy ${detail.grnNumber} — tồn không đổi`);
      navigate('/goods-receipt', { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiếu nhập'));
    } finally {
      setCancelling(false);
    }
  };

  const isPending = detail?.status === GRN_STATUS.Pending;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title={detail?.grnNumber ?? 'Phiếu nhập'}
        subtitle={
          detail
            ? `${detail.warehouseName} · ${detail.items.length} dòng`
            : loadError
              ? 'Không tải được phiếu'
              : 'Đang tải…'
        }
        backTo="/goods-receipt"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={loading} />}
            aria-label="Tải lại"
            onClick={() => void load()}
          />
        }
      />
      <main className={`staff-body grn-detail-body${canWrite && isPending ? ' grn-detail-body--actions' : ''}`}>
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được phiếu nhập"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load()}>
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

        {!loading && !detail && !loadError ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không tìm thấy phiếu nhập" />
        ) : null}

        {detail && !loading ? (
          <>
            <section className="grn-detail-hero">
              <div className="grn-detail-hero__row">
                <Typography.Text strong>{detail.supplierName}</Typography.Text>
                <Tag color={GRN_STATUS_TAG[detail.status] ?? 'default'}>
                  {GRN_STATUS_LABELS[detail.status] ?? detail.status}
                </Tag>
              </div>
              <div className="grn-detail-hero__meta">
                <span>{detail.warehouseName}</span>
                <span>· {dayjs(detail.receiptDate).format('DD/MM/YYYY')}</span>
                {detail.poNumber ? <span>· PO {detail.poNumber}</span> : null}
              </div>
              {detail.notes ? (
                <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, marginTop: 8 }}>
                  Ghi chú: {detail.notes}
                </Typography.Paragraph>
              ) : null}
              {isPending ? (
                <Alert
                  type="warning"
                  showIcon
                  style={{ marginTop: 10 }}
                  message="Chưa cộng tồn"
                  description="Kiểm tra lô/HSD từng dòng rồi bấm Hoàn tất nhập kho."
                />
              ) : null}
              {detail.status === GRN_STATUS.Completed ? (
                <Alert
                  type="success"
                  showIcon
                  style={{ marginTop: 10 }}
                  message="Đã nhập kho"
                  description="Tồn đã tăng theo phiếu này. Không hủy được từ trạng thái hoàn tất."
                />
              ) : null}
              {detail.status === GRN_STATUS.Cancelled ? (
                <Alert
                  type="info"
                  showIcon
                  style={{ marginTop: 10 }}
                  message="Phiếu đã hủy"
                  description="Không ảnh hưởng tồn kho."
                />
              ) : null}
            </section>

            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              Dòng hàng ({detail.items.length})
            </Typography.Text>

            {detail.items.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Phiếu không có dòng hàng" />
            ) : (
              detail.items.map((line) => {
                const hint = expiryHint(line.expiryDate);
                return (
                  <article key={line.id} className="grn-line-card">
                    <Typography.Text strong>
                      {line.productCode} · {line.productName}
                    </Typography.Text>
                    <div className="grn-line-card__meta">
                      <span>
                        {line.quantity} {line.unitName}
                      </span>
                      <span>· Lô {line.batchNumber || '—'}</span>
                    </div>
                    <div className="grn-line-card__meta">
                      <span>Giá {formatMoney(line.unitCost)}</span>
                      <span>· {formatMoney(line.lineTotal)}</span>
                    </div>
                    {hint ? (
                      <div className={`grn-line-card__hsd grn-line-card__hsd--${hint.tone}`}>
                        HSD {hint.text}
                      </div>
                    ) : null}
                  </article>
                );
              })
            )}

            <div className="grn-detail-total">
              <span>Tổng phiếu</span>
              <strong>{formatMoney(detail.totalAmount)}</strong>
            </div>
          </>
        ) : null}
      </main>

      {canWrite && isPending && detail ? (
        <footer className="staff-footer grn-detail-footer">
          <Typography.Text type="secondary" className="grn-detail-footer__hint">
            Hoàn tất mới cộng tồn · Hủy chỉ khi còn chờ nhập
          </Typography.Text>
          <div className="grn-detail-footer__actions">
            <Popconfirm
              title="Hủy phiếu nhập?"
              description="Tồn kho không đổi. Không hoàn tác dễ dàng."
              okText="Hủy phiếu"
              cancelText="Giữ"
              okButtonProps={{ danger: true, loading: cancelling }}
              onConfirm={() => void cancel()}
            >
              <Button danger block size="large" loading={cancelling}>
                Hủy phiếu
              </Button>
            </Popconfirm>
            <Popconfirm
              title="Hoàn tất nhập kho?"
              description={`${detail.items.length} dòng · tổng ${formatMoney(detail.totalAmount)}. Tồn sẽ tăng ngay.`}
              okText="Hoàn tất"
              cancelText="Đóng"
              onConfirm={() => void complete()}
            >
              <Button type="primary" block size="large" loading={completing}>
                Hoàn tất nhập kho
              </Button>
            </Popconfirm>
          </div>
        </footer>
      ) : null}
    </div>
  );
}
