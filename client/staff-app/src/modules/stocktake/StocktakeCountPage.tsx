import { useCallback, useEffect, useRef, useState } from 'react';
import {
  App,
  Alert,
  AutoComplete,
  Button,
  Empty,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Spin,
  Tag,
  Typography,
} from 'antd';
import type { InputRef } from 'antd/es/input';
import { CheckOutlined, DeleteOutlined, ScanOutlined, StopOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate, useParams } from 'react-router-dom';
import {
  addCountEntries,
  approveAdjustment,
  cancelAdjustment,
  deleteCountEntry,
  fetchAdjustment,
  fetchCountEntries,
  fetchStockBatches,
  fetchStockProducts,
  resolveInventoryBarcode,
} from '@/shared/api/inventory.api';
import {
  ADJUSTMENT_STATUS_COLORS,
  ADJUSTMENT_STATUS_LABELS,
  type AdjustmentCountEntry,
  type AdjustmentListItem,
  type StockBatch,
} from '@/shared/api/inventory.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanInventoryWrite } from '@/shared/auth/usePermission';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type ProductOption = { value: string; label: string; productId: string };

function batchLabel(b: StockBatch): string {
  const hsd = b.expiryDate ? ` · HSD ${dayjs(b.expiryDate).format('MM/YYYY')}` : '';
  return `${b.batchNumber}${hsd} · tồn ${b.quantityAvailable}`;
}

export function StocktakeCountPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const canWrite = useCanInventoryWrite();
  const barcodeRef = useRef<InputRef>(null);
  const searchTimer = useRef<number | undefined>(undefined);

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [header, setHeader] = useState<AdjustmentListItem | null>(null);
  const [entries, setEntries] = useState<AdjustmentCountEntry[]>([]);
  const [query, setQuery] = useState('');
  const [productOptions, setProductOptions] = useState<ProductOption[]>([]);
  const [searching, setSearching] = useState(false);
  const [quantity, setQuantity] = useState(1);
  const [resolvedLabel, setResolvedLabel] = useState('');
  const [resolvedProductId, setResolvedProductId] = useState<string>();
  const [resolvedBatchId, setResolvedBatchId] = useState<string>();
  const [batchOptions, setBatchOptions] = useState<{ value: string; label: string }[]>([]);
  const [loadingBatches, setLoadingBatches] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [approving, setApproving] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [deletingEntryId, setDeletingEntryId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setLoadError(null);
    try {
      const detail = await fetchAdjustment(id);
      setHeader(detail);
      setEntries(await fetchCountEntries(id));
    } catch (error) {
      setHeader(null);
      setEntries([]);
      setLoadError(apiErrorMessage(error, 'Không tải được phiên kiểm'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!loading && canWrite) {
      window.setTimeout(() => barcodeRef.current?.focus(), 200);
    }
  }, [loading, canWrite]);

  const clearResolved = () => {
    setResolvedProductId(undefined);
    setResolvedBatchId(undefined);
    setResolvedLabel('');
    setBatchOptions([]);
  };

  const loadBatchesForProduct = async (
    warehouseId: string,
    productId: string,
    productCode: string,
    productName: string,
    preferredBatchId?: string,
    preferredBatchNumber?: string,
  ): Promise<boolean> => {
    setLoadingBatches(true);
    try {
      const result = await fetchStockBatches({
        warehouseId,
        productId,
        page: 1,
        pageSize: 50,
      });
      const options = result.items.map((b) => ({
        value: b.id,
        label: batchLabel(b),
      }));
      setBatchOptions(options);

      const preferred =
        (preferredBatchId && options.find((o) => o.value === preferredBatchId)?.value) ||
        (preferredBatchNumber &&
          result.items.find((b) => b.batchNumber === preferredBatchNumber)?.id) ||
        result.items.find((b) => b.quantityAvailable > 0)?.id ||
        options[0]?.value;

      if (!preferred) {
        clearResolved();
        message.warning('SP chưa có lô tại kho — không ghi được đếm');
        return false;
      }

      setResolvedProductId(productId);
      setResolvedBatchId(preferred);
      const batch = result.items.find((b) => b.id === preferred);
      setResolvedLabel(
        `${productCode} · ${productName} · lô ${batch?.batchNumber ?? preferredBatchNumber ?? '—'}`,
      );
      return true;
    } catch (error) {
      clearResolved();
      message.error(apiErrorMessage(error, 'Không tải được lô'));
      return false;
    } finally {
      setLoadingBatches(false);
    }
  };

  const searchProducts = useCallback(
    (text: string) => {
      if (!header?.warehouseId) {
        setProductOptions([]);
        return;
      }
      window.clearTimeout(searchTimer.current);
      searchTimer.current = window.setTimeout(() => {
        void (async () => {
          setSearching(true);
          try {
            const result = await fetchStockProducts({
              warehouseId: header.warehouseId,
              search: text.trim() || undefined,
              page: 1,
              pageSize: 20,
            });
            setProductOptions(
              result.items.map((p) => ({
                value: p.productId,
                productId: p.productId,
                label: `${p.productCode} · ${p.productName} · tồn ${p.totalQuantity}`,
              })),
            );
          } catch {
            setProductOptions([]);
          } finally {
            setSearching(false);
          }
        })();
      }, 280);
    },
    [header?.warehouseId],
  );

  useEffect(() => {
    return () => window.clearTimeout(searchTimer.current);
  }, []);

  const pickProduct = async (productId: string, optionLabel?: string) => {
    if (!header) return;
    const fromOptions = productOptions.find((o) => o.productId === productId);
    const label = String(optionLabel || fromOptions?.label || '');
    const parts = label.split(' · ');
    const productCode = parts[0] ?? '';
    const productName = parts[1] ?? label;
    setQuery(productCode && productName ? `${productCode} · ${productName}` : label);
    setProductOptions([]);
    await loadBatchesForProduct(header.warehouseId, productId, productCode, productName);
    message.success('Đã chọn SP — kiểm tra lô rồi nhập SL');
  };

  const resolveQuery = async () => {
    if (!header || !query.trim()) {
      message.warning('Nhập mã / tên SP hoặc quét barcode');
      return;
    }
    if (resolvedProductId && resolvedBatchId) {
      message.info('Đã khớp SP — nhập SL rồi bấm Ghi nhận đếm');
      return;
    }

    const raw = query.trim();
    const looksLikeLabel = raw.includes(' · ');
    const searchKey = looksLikeLabel ? (raw.split(' · ')[0]?.trim() ?? raw) : raw;

    setSubmitting(true);
    try {
      if (!looksLikeLabel) {
        try {
          const hit = await resolveInventoryBarcode(header.warehouseId, raw);
          setQuery(`${hit.productCode} · ${hit.productName}`);
          setProductOptions([]);
          const ok = await loadBatchesForProduct(
            header.warehouseId,
            hit.productId,
            hit.productCode,
            hit.productName,
            hit.suggestedBatchId,
            hit.suggestedBatchNumber,
          );
          if (ok) message.success('Đã khớp barcode — nhập SL rồi Ghi nhận');
          return;
        } catch {
          // Not an exact barcode — fall through to stock search
        }
      }

      const result = await fetchStockProducts({
        warehouseId: header.warehouseId,
        search: searchKey,
        page: 1,
        pageSize: 10,
      });
      if (result.items.length === 0) {
        clearResolved();
        message.warning('Không tìm thấy sản phẩm / barcode tại kho này');
        return;
      }
      if (result.items.length === 1) {
        const only = result.items[0]!;
        setQuery(`${only.productCode} · ${only.productName}`);
        setProductOptions([]);
        const ok = await loadBatchesForProduct(
          header.warehouseId,
          only.productId,
          only.productCode,
          only.productName,
        );
        if (ok) message.success('Đã khớp SP — kiểm tra lô rồi nhập SL');
        return;
      }
      setProductOptions(
        result.items.map((p) => ({
          value: p.productId,
          productId: p.productId,
          label: `${p.productCode} · ${p.productName} · tồn ${p.totalQuantity}`,
        })),
      );
      clearResolved();
      message.info(`Có ${result.items.length} SP — chọn trong danh sách gợi ý`);
    } catch (error) {
      clearResolved();
      message.error(apiErrorMessage(error, 'Không tìm được sản phẩm'));
    } finally {
      setSubmitting(false);
    }
  };

  const addLine = async () => {
    if (!id || !resolvedProductId || !resolvedBatchId || quantity <= 0) {
      message.warning('Chọn sản phẩm/lô và nhập số lượng trước');
      return;
    }
    setSubmitting(true);
    try {
      const raw = query.trim();
      const scannedBarcode = raw && !raw.includes(' · ') ? raw : undefined;
      await addCountEntries(id, [
        {
          productId: resolvedProductId,
          batchId: resolvedBatchId,
          quantity,
          scannedBarcode,
        },
      ]);
      setEntries(await fetchCountEntries(id));
      setQuery('');
      clearResolved();
      setQuantity(1);
      setProductOptions([]);
      message.success('Đã ghi nhận đếm');
      window.setTimeout(() => barcodeRef.current?.focus(), 100);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thêm được dòng đếm'));
    } finally {
      setSubmitting(false);
    }
  };

  const removeEntry = async (entryId: string) => {
    if (!id) return;
    setDeletingEntryId(entryId);
    try {
      await deleteCountEntry(id, entryId);
      setEntries((prev) => prev.filter((e) => e.id !== entryId));
      message.success('Đã xóa dòng đếm');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được dòng đếm'));
    } finally {
      setDeletingEntryId(null);
    }
  };

  const cancelSession = async () => {
    if (!id) return;
    setCancelling(true);
    try {
      await cancelAdjustment(id);
      message.success('Đã hủy phiên kiểm kê — tồn không đổi');
      navigate('/stocktake', { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được phiên'));
    } finally {
      setCancelling(false);
    }
  };

  const approve = async () => {
    if (!id) return;
    setApproving(true);
    try {
      await approveAdjustment(id);
      message.success('Đã duyệt kiểm kê');
      navigate('/stocktake', { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không duyệt được — kiểm tra đủ lô và quyền'));
    } finally {
      setApproving(false);
    }
  };

  if (!id) return null;

  const canApprove = canWrite && entries.length > 0;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title={header?.adjustmentNumber ?? 'Kiểm kê'}
        subtitle={
          header
            ? `${header.warehouseName} · ${entries.length} dòng đếm`
            : loadError
              ? 'Không tải được phiên'
              : 'Đang tải…'
        }
        backTo="/stocktake"
        right={
          canWrite && header && !loading ? (
            <Popconfirm
              title="Hủy phiên kiểm kê?"
              description="Xóa phiên và toàn bộ dòng đếm. Tồn kho không đổi."
              okText="Hủy phiên"
              cancelText="Giữ lại"
              okButtonProps={{ danger: true, loading: cancelling }}
              onConfirm={() => void cancelSession()}
            >
              <Button
                type="text"
                danger
                size="small"
                icon={<StopOutlined />}
                loading={cancelling}
                aria-label="Hủy phiên"
              >
                Hủy
              </Button>
            </Popconfirm>
          ) : undefined
        }
      />
      <main className="staff-body stocktake-count-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được phiên kiểm"
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

        {header && !loading ? (
          <div className="stocktake-count-status">
            <Tag color={ADJUSTMENT_STATUS_COLORS[header.status] ?? 'default'}>
              {ADJUSTMENT_STATUS_LABELS[header.status] ?? header.status}
            </Tag>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Tìm/quét SP → chọn lô → nhập SL → Ghi nhận. Duyệt khi đếm xong.
            </Typography.Text>
          </div>
        ) : null}

        {canWrite && header && !loading ? (
          <section className="stocktake-scan-panel">
            <Typography.Text type="secondary" className="stocktake-field-label">
              Tìm / quét sản phẩm
            </Typography.Text>
            <div className="stocktake-scan-row">
              <AutoComplete
                style={{ flex: 1 }}
                options={productOptions.map((o) => ({ value: o.value, label: o.label }))}
                value={query}
                onSearch={(text) => {
                  setQuery(text);
                  if (resolvedBatchId) clearResolved();
                  searchProducts(text);
                }}
                onSelect={(value, option) => {
                  void pickProduct(String(value), String(option.label ?? ''));
                }}
                notFoundContent={searching ? <Spin size="small" /> : 'Không có SP khớp'}
              >
                <Input
                  ref={barcodeRef}
                  size="large"
                  placeholder="Mã / tên / barcode…"
                  allowClear
                  onPressEnter={() => void resolveQuery()}
                />
              </AutoComplete>
              <Button
                size="large"
                type="default"
                icon={<ScanOutlined />}
                loading={submitting}
                onClick={() => void resolveQuery()}
              >
                Nhận
              </Button>
            </div>

            {resolvedLabel ? (
              <Alert
                type="success"
                showIcon
                style={{ marginTop: 10, marginBottom: 10 }}
                message="Đã khớp sản phẩm"
                description={resolvedLabel}
              />
            ) : (
              <Typography.Text type="secondary" style={{ display: 'block', margin: '8px 0', fontSize: 12 }}>
                Gõ vài ký tự để hiện gợi ý, hoặc quét barcode rồi bấm Nhận / Enter.
              </Typography.Text>
            )}

            {resolvedProductId ? (
              <>
                <Typography.Text type="secondary" className="stocktake-field-label">
                  Lô đếm
                </Typography.Text>
                <Select
                  size="large"
                  style={{ width: '100%', marginBottom: 10 }}
                  loading={loadingBatches}
                  placeholder="Chọn lô"
                  value={resolvedBatchId}
                  options={batchOptions}
                  onChange={(batchId) => {
                    setResolvedBatchId(batchId);
                    const opt = batchOptions.find((o) => o.value === batchId);
                    if (opt && resolvedLabel) {
                      const base = resolvedLabel.replace(/ · lô .+$/, '');
                      setResolvedLabel(
                        `${base} · ${opt.label.split(' · ')[0]?.replace(/^Lô /, 'lô ') ?? opt.label}`,
                      );
                    }
                  }}
                />
              </>
            ) : null}

            <Typography.Text type="secondary" className="stocktake-field-label">
              Số lượng đếm
            </Typography.Text>
            <InputNumber
              size="large"
              min={0.001}
              step={1}
              style={{ width: '100%', marginBottom: 10 }}
              value={quantity}
              onChange={(v) => setQuantity(Number(v ?? 1))}
            />
            <Button
              type="primary"
              block
              size="large"
              loading={submitting}
              disabled={!resolvedBatchId}
              onClick={() => void addLine()}
            >
              Ghi nhận đếm
            </Button>
          </section>
        ) : null}

        {!canWrite && !loading ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chỉ xem"
            description="Không có quyền ghi — không quét/duyệt được trên phiên này."
          />
        ) : null}

        <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
          Đã đếm ({entries.length})
        </Typography.Text>
        {!loading && entries.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description="Chưa có dòng đếm. Tìm hoặc quét mã sản phẩm để bắt đầu."
          />
        ) : (
          entries.slice(0, 80).map((row) => (
            <article key={row.id} className="stocktake-entry-card">
              <div className="stocktake-entry-card__head">
                <Typography.Text strong>{row.productName ?? row.productCode ?? '—'}</Typography.Text>
                {canWrite ? (
                  <Popconfirm
                    title="Xóa dòng đếm này?"
                    description={`${row.productCode ?? ''} · Lô ${row.batchNumber ?? '—'} · SL ${row.quantity}`}
                    okText="Xóa"
                    cancelText="Giữ"
                    okButtonProps={{ danger: true, loading: deletingEntryId === row.id }}
                    onConfirm={() => void removeEntry(row.id)}
                  >
                    <Button
                      type="text"
                      danger
                      size="small"
                      icon={<DeleteOutlined />}
                      loading={deletingEntryId === row.id}
                      aria-label="Xóa dòng đếm"
                    />
                  </Popconfirm>
                ) : null}
              </div>
              <div className="stocktake-entry-card__meta">
                <span>{row.productCode ?? '—'}</span>
                <span>· Lô {row.batchNumber ?? '—'}</span>
                {row.zone ? <span>· Khu {row.zone}</span> : null}
              </div>
              <div className="stocktake-entry-card__qty">SL {row.quantity}</div>
            </article>
          ))
        )}
      </main>

      {canWrite ? (
        <footer className="staff-footer stocktake-count-footer">
          <Typography.Text type="secondary" className="stocktake-count-footer__hint">
            {entries.length === 0
              ? 'Cần ít nhất 1 dòng đếm để duyệt · có thể Hủy phiên nếu mở nhầm'
              : `${entries.length} dòng · duyệt sẽ chốt chênh lệch tồn`}
          </Typography.Text>
          <div className="stocktake-count-footer__actions">
            <Popconfirm
              title="Hủy phiên kiểm kê?"
              description="Xóa phiên và toàn bộ dòng đếm. Tồn kho không đổi."
              okText="Hủy phiên"
              cancelText="Giữ lại"
              okButtonProps={{ danger: true, loading: cancelling }}
              onConfirm={() => void cancelSession()}
            >
              <Button danger block size="large" icon={<StopOutlined />} loading={cancelling}>
                Hủy phiên
              </Button>
            </Popconfirm>
            <Popconfirm
              title="Duyệt kiểm kê?"
              description="Hệ thống sẽ cập nhật tồn theo số đã đếm. Không hoàn tác dễ dàng."
              okText="Duyệt"
              cancelText="Đóng"
              disabled={!canApprove}
              onConfirm={() => void approve()}
            >
              <Button
                type="primary"
                block
                size="large"
                icon={<CheckOutlined />}
                loading={approving}
                disabled={!canApprove}
              >
                Duyệt kiểm kê
              </Button>
            </Popconfirm>
          </div>
        </footer>
      ) : null}
    </div>
  );
}
