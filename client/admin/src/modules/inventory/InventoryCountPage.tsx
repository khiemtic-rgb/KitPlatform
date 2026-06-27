import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AutoComplete,
  Button,
  Card,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  ArrowLeftOutlined,
  CheckOutlined,
  DeleteOutlined,
  PlusOutlined,
  PrinterOutlined,
  ReloadOutlined,
  ScanOutlined,
} from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { fetchProducts } from '@/shared/api/catalog.api';
import {
  addCountEntries,
  approveAdjustment,
  deleteCountEntry,
  fetchAdjustment,
  fetchCountEntries,
  fetchCountPreview,
  fetchStockBatches,
  resolveInventoryBarcode,
} from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  AdjustmentCountEntry,
  AdjustmentCountPreviewLine,
  AdjustmentDetail,
} from '@/shared/api/inventory.types';
import { ADJUSTMENT_STATUS_LABELS } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity, quantityInputNumberProps } from '@/shared/utils/money';
import { InventoryCountApproveModal } from '@/modules/inventory/InventoryCountApproveModal';
import { InventoryCountWorkflowSteps } from '@/modules/inventory/InventoryCountWorkflowSteps';
import { countVarianceSummary } from '@/modules/inventory/inventory-count-workflow';
import { printInventoryCountSheet } from '@/shared/print/inventory-count-print';
import { useAuthStore } from '@/shared/auth/auth.store';

interface ProductSearchOption {
  value: string;
  label: string;
  unitName?: string;
}

interface DraftLine {
  key: string;
  productId: string;
  productLabel: string;
  batchId: string;
  batchLabel: string;
  quantity: number;
  unitName?: string;
  zone?: string;
  scannedBarcode?: string;
}

function nextDraftKey() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export function InventoryCountPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const tenantCode = useAuthStore((s) => s.user?.tenantCode);

  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<AdjustmentDetail | null>(null);
  const [previewByBatch, setPreviewByBatch] = useState<AdjustmentCountPreviewLine[]>([]);
  const [previewByProduct, setPreviewByProduct] = useState<AdjustmentCountPreviewLine[]>([]);
  const [entries, setEntries] = useState<AdjustmentCountEntry[]>([]);
  const [productSearch, setProductSearch] = useState('');
  const [activeProductId, setActiveProductId] = useState<string | null>(null);
  const [activeUnitName, setActiveUnitName] = useState<string | null>(null);
  const [selectedBatchId, setSelectedBatchId] = useState<string | undefined>();
  const [batchOptions, setBatchOptions] = useState<{ value: string; label: string }[]>([]);
  const [productOptions, setProductOptions] = useState<ProductSearchOption[]>([]);
  const [quantity, setQuantity] = useState(1);
  const [zone, setZone] = useState('');
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [approving, setApproving] = useState(false);
  const [resolving, setResolving] = useState(false);
  const [approveModalOpen, setApproveModalOpen] = useState(false);

  const entriesMissingBatch = entries.filter((e) => !e.batchId);
  const canApprove = previewByBatch.length > 0 && entriesMissingBatch.length === 0;
  const approveBlockReason =
    entriesMissingBatch.length > 0
      ? `Còn ${entriesMissingBatch.length} dòng đếm chưa có lô — xóa ở bảng "Dòng đếm gần đây"`
      : previewByBatch.length === 0
        ? 'Chưa có dòng đếm nào được ghi nhận'
        : null;

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const [adj, prev, ents] = await Promise.all([
        fetchAdjustment(id),
        fetchCountPreview(id),
        fetchCountEntries(id),
      ]);
      setDetail(adj);
      setPreviewByBatch(prev.byBatch);
      setPreviewByProduct(prev.byProduct);
      setEntries(ents);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiên kiểm kê'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    const q = productSearch.trim();
    if (q.length < 1) {
      setProductOptions([]);
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void (async () => {
        try {
          const result = await fetchProducts({ search: q, page: 1, pageSize: 20 });
          if (cancelled) return;
          setProductOptions(
            result.items.map((p) => ({
              value: p.id,
              label: p.saleUnitName
                ? `${p.productCode} — ${p.productName} · ${p.saleUnitName}`
                : `${p.productCode} — ${p.productName}`,
              unitName: p.saleUnitName ?? undefined,
            })),
          );
        } catch {
          if (!cancelled) setProductOptions([]);
        }
      })();
    }, 300);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [productSearch]);

  const loadBatchesForProduct = useCallback(
    async (
      productId: string,
      preferredBatchId?: string,
    ): Promise<{ batchId?: string; batchLabel?: string }> => {
      if (!detail?.warehouseId) {
        setBatchOptions([]);
        setSelectedBatchId(undefined);
        return {};
      }
      try {
        const result = await fetchStockBatches({
          warehouseId: detail.warehouseId,
          productId,
          page: 1,
          pageSize: 50,
        });
        const options = result.items.map((b) => ({
          value: b.id,
          label: `${b.batchNumber}${b.expiryDate ? ` · HSD ${formatDisplayDate(b.expiryDate)}` : ''} · tồn ${formatDisplayQuantity(b.quantityAvailable)}`,
        }));
        setBatchOptions(options);

        const batchId = preferredBatchId && options.some((o) => o.value === preferredBatchId)
          ? preferredBatchId
          : options[0]?.value;
        setSelectedBatchId(batchId);
        const batchLabel = batchId ? options.find((o) => o.value === batchId)?.label : undefined;
        return { batchId, batchLabel };
      } catch {
        setBatchOptions([]);
        setSelectedBatchId(undefined);
        return {};
      }
    },
    [detail?.warehouseId],
  );

  const applyProductContext = useCallback(
    async (productId: string, label: string, unitName: string | null, suggestedBatchId?: string) => {
      setActiveProductId(productId);
      setProductSearch(label);
      setActiveUnitName(unitName);
      await loadBatchesForProduct(productId, suggestedBatchId);
    },
    [loadBatchesForProduct],
  );

  const resolveActiveProduct = async (): Promise<{
    productId: string;
    label: string;
    unitName: string | null;
    batchId?: string;
    batchLabel?: string;
    scannedBarcode?: string;
  } | null> => {
    if (activeProductId) {
      const batchId = selectedBatchId;
      return {
        productId: activeProductId,
        label: productSearch,
        unitName: activeUnitName,
        batchId,
        batchLabel: batchId ? batchOptions.find((b) => b.value === batchId)?.label : undefined,
      };
    }

    const q = productSearch.trim();
    if (!q) return null;

    if (detail?.warehouseId && /^\d{8,}$/.test(q)) {
      setResolving(true);
      try {
        const resolved = await resolveInventoryBarcode(detail.warehouseId, q);
        if (resolved) {
          const label = resolved.saleUnitName
            ? `${resolved.productCode} — ${resolved.productName} · ${resolved.saleUnitName}`
            : `${resolved.productCode} — ${resolved.productName}`;
          const { batchId, batchLabel } = await loadBatchesForProduct(
            resolved.productId,
            resolved.suggestedBatchId,
          );
          setActiveProductId(resolved.productId);
          setProductSearch(label);
          setActiveUnitName(resolved.saleUnitName ?? null);
          return {
            productId: resolved.productId,
            label,
            unitName: resolved.saleUnitName ?? null,
            batchId,
            batchLabel: batchLabel ?? resolved.suggestedBatchNumber,
            scannedBarcode: q,
          };
        }
      } finally {
        setResolving(false);
      }
    }

    if (productOptions.length === 1) {
      const picked = productOptions[0];
      const { batchId } = await loadBatchesForProduct(picked.value);
      setActiveProductId(picked.value);
      setProductSearch(picked.label);
      setActiveUnitName(picked.unitName ?? null);
      return {
        productId: picked.value,
        label: picked.label,
        unitName: picked.unitName ?? null,
        batchId,
      };
    }

    const exact = productOptions.find((o) => o.label.toLowerCase().startsWith(q.toLowerCase()));
    if (exact) {
      const { batchId } = await loadBatchesForProduct(exact.value);
      setActiveProductId(exact.value);
      setProductSearch(exact.label);
      setActiveUnitName(exact.unitName ?? null);
      return {
        productId: exact.value,
        label: exact.label,
        unitName: exact.unitName ?? null,
        batchId,
      };
    }

    return null;
  };

  const handleAddDraftLine = async () => {
    if (!productSearch.trim()) {
      message.warning('Quét barcode hoặc chọn sản phẩm từ danh sách');
      return;
    }
    if (quantity <= 0) {
      message.warning('Số lượng phải lớn hơn 0');
      return;
    }

    const resolved = await resolveActiveProduct();
    if (!resolved) {
      message.warning('Chọn sản phẩm từ danh sách gợi ý hoặc quét đúng barcode');
      return;
    }

    const batchId = resolved.batchId ?? selectedBatchId;
    if (!batchId) {
      message.warning('SP này chưa có lô tại kho — không thể ghi nhận');
      return;
    }

    const batchLabel =
      batchOptions.find((b) => b.value === batchId)?.label ?? resolved.batchLabel ?? batchId;
    setDraftLines((prev) => [
      ...prev,
      {
        key: nextDraftKey(),
        productId: resolved.productId,
        productLabel: resolved.label,
        batchId,
        batchLabel,
        quantity,
        unitName: resolved.unitName ?? undefined,
        zone: zone.trim() || undefined,
        scannedBarcode: resolved.scannedBarcode,
      },
    ]);
    setQuantity(1);
    message.success('Đã thêm vào danh sách chờ ghi nhận');
  };

  const handleRemoveDraftLine = (key: string) => {
    setDraftLines((prev) => prev.filter((line) => line.key !== key));
  };

  const handleSubmitDraft = async () => {
    if (!id || draftLines.length === 0) {
      message.warning('Chưa có dòng nào để ghi nhận');
      return;
    }

    setSubmitting(true);
    const lineCount = draftLines.length;
    try {
      await addCountEntries(
        id,
        draftLines.map((line) => ({
          productId: line.productId,
          batchId: line.batchId,
          quantity: line.quantity,
          scannedBarcode: line.scannedBarcode,
          zone: line.zone,
        })),
      );
      setDraftLines([]);
      setProductSearch('');
      setActiveProductId(null);
      setActiveUnitName(null);
      setSelectedBatchId(undefined);
      setBatchOptions([]);
      setProductOptions([]);
      setQuantity(1);
      message.success(`Đã ghi nhận ${lineCount} dòng`);
      await load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không ghi nhận được dòng đếm'));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeleteEntry = async (entryId: string) => {
    if (!id) return;
    try {
      await deleteCountEntry(id, entryId);
      message.success('Đã xóa dòng');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được dòng'));
    }
  };

  const handleApprove = async () => {
    if (!id) return;
    if (!canApprove) {
      message.warning(approveBlockReason ?? 'Không thể duyệt phiên này');
      return;
    }

    setApproving(true);
    try {
      await approveAdjustment(id);
      setApproveModalOpen(false);
      message.success('Đã duyệt kiểm kê — tồn kho đã được cập nhật');
      navigate('/inventory/adjustments');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không duyệt được phiếu'));
    } finally {
      setApproving(false);
    }
  };

  const openApproveConfirm = () => {
    if (!canApprove) {
      message.warning(approveBlockReason ?? 'Không thể duyệt phiên này');
      return;
    }
    setApproveModalOpen(true);
  };

  const varianceSummary = countVarianceSummary(previewByBatch);

  const previewColumns: ColumnsType<AdjustmentCountPreviewLine> = [
    { title: 'SP', dataIndex: 'productName', ellipsis: true },
    { title: 'Lô', dataIndex: 'batchNumber', width: 96, render: (v) => v ?? '—' },
    { title: 'HT', dataIndex: 'systemQuantity', width: 72, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    { title: 'Đếm', dataIndex: 'countedQuantity', width: 72, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: 'Lệch',
      dataIndex: 'differenceQuantity',
      width: 72,
      align: 'right',
      render: (v: number) => (
        <span style={{ color: v === 0 ? undefined : v > 0 ? '#389e0d' : '#cf1322' }}>
          {formatDisplayQuantity(v)}
        </span>
      ),
    },
  ];

  const productPreviewColumns: ColumnsType<AdjustmentCountPreviewLine> = [
    { title: 'SP', dataIndex: 'productName', ellipsis: true },
    { title: 'HT', dataIndex: 'systemQuantity', width: 80, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    { title: 'Đếm', dataIndex: 'countedQuantity', width: 80, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: 'Lệch',
      dataIndex: 'differenceQuantity',
      width: 80,
      align: 'right',
      render: (v: number) => (
        <span style={{ color: v === 0 ? undefined : v > 0 ? '#389e0d' : '#cf1322' }}>
          {formatDisplayQuantity(v)}
        </span>
      ),
    },
  ];

  const draftColumns: ColumnsType<DraftLine> = [
    { title: 'SP', dataIndex: 'productLabel', ellipsis: true },
    { title: 'Lô', dataIndex: 'batchLabel', ellipsis: true },
    { title: 'SL', dataIndex: 'quantity', width: 72, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    { title: 'Khu vực', dataIndex: 'zone', width: 120, ellipsis: true, render: (v) => v ?? '—' },
    {
      title: '',
      key: 'actions',
      width: 40,
      render: (_, row) => (
        <Button type="text" danger size="small" icon={<DeleteOutlined />} onClick={() => handleRemoveDraftLine(row.key)} />
      ),
    },
  ];

  const entryColumns: ColumnsType<AdjustmentCountEntry> = [
    { title: 'SP', dataIndex: 'productName', ellipsis: true },
    { title: 'Lô', dataIndex: 'batchNumber', width: 96, render: (v) => v ?? '—' },
    { title: 'SL', dataIndex: 'quantity', width: 88, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: 'Người kiểm',
      dataIndex: 'counterUserName',
      width: 200,
      ellipsis: true,
      render: (v) => v ?? '—',
    },
    {
      title: '',
      key: 'actions',
      width: 40,
      render: (_, row) =>
        detail?.status === 2 ? (
          <Button type="text" danger size="small" icon={<DeleteOutlined />} onClick={() => handleDeleteEntry(row.id)} />
        ) : null,
    },
  ];

  if (!id) return null;

  return (
    <div style={{ maxWidth: 720, margin: '0 auto' }}>
      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Space wrap>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/inventory/adjustments')}>
            Danh sách
          </Button>
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            Tải lại
          </Button>
          {detail && previewByBatch.length > 0 && (
            <Button
              icon={<PrinterOutlined />}
              onClick={() => printInventoryCountSheet(detail, previewByBatch, tenantCode)}
            >
              In biên bản A4
            </Button>
          )}
          {detail && detail.status === 2 && (
            <Tooltip title={approveBlockReason ?? 'Chốt phiên và cập nhật tồn kho'}>
              <Button
                type="primary"
                icon={<CheckOutlined />}
                loading={approving}
                onClick={openApproveConfirm}
              >
                Duyệt
              </Button>
            </Tooltip>
          )}
        </Space>

        {detail && (
          <Card size="small" title="Quy trình kiểm kê" loading={loading}>
            <InventoryCountWorkflowSteps
              status={detail.status}
              entryCount={entries.length}
              canApprove={canApprove}
            />
          </Card>
        )}

        {detail && (
          <Card size="small" loading={loading}>
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {detail.adjustmentNumber}
            </Typography.Title>
            <Space wrap size="middle">
              <span>
                <strong>Kho:</strong> {detail.warehouseName}
              </span>
              <Tag>{ADJUSTMENT_STATUS_LABELS[detail.status] ?? detail.status}</Tag>
              <span>{formatDisplayDate(detail.adjustmentDate)}</span>
            </Space>
            {detail.status === 2 && approveBlockReason && (
              <Typography.Paragraph type="danger" style={{ marginBottom: 0, marginTop: 12 }}>
                {approveBlockReason} trước khi duyệt.
              </Typography.Paragraph>
            )}
            {detail.status === 2 && entries.length > 0 && (
              <Typography.Paragraph style={{ marginBottom: 0, marginTop: 12 }}>
                <strong>Bước 3 — Đối chiếu:</strong>{' '}
                {varianceSummary.varianceLines > 0
                  ? `${varianceSummary.varianceLines} nhóm lệch — kiểm tra GRN/đơn bán/chuyển kho trước khi duyệt.`
                  : 'Không có lệch — có thể duyệt để khóa phiên.'}
              </Typography.Paragraph>
            )}
            {detail.status === 2 && canApprove && (
              <Typography.Paragraph type="success" style={{ marginBottom: 0, marginTop: 12 }}>
                Sẵn sàng bước 4 — bấm <strong>Duyệt</strong> và hoàn tất checklist.
              </Typography.Paragraph>
            )}
          </Card>
        )}

        {detail?.status === 2 && (
          <Card title={<><ScanOutlined /> Bước 2 — Quét / tìm sản phẩm</>} size="small">
            <Space direction="vertical" style={{ width: '100%' }} size="middle">
              <AutoComplete
                style={{ width: '100%' }}
                size="large"
                placeholder="Quét barcode hoặc gõ mã / tên thuốc"
                value={productSearch}
                options={productOptions}
                onChange={(value) => {
                  setProductSearch(value);
                  setActiveProductId(null);
                  setActiveUnitName(null);
                  setSelectedBatchId(undefined);
                  setBatchOptions([]);
                }}
                onSelect={(value, option) => {
                  const picked = option as ProductSearchOption;
                  void applyProductContext(String(value), String(picked.label ?? value), picked.unitName ?? null);
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void handleAddDraftLine();
                }}
                disabled={submitting || resolving}
                notFoundContent={productSearch.trim() ? 'Không có sản phẩm phù hợp' : 'Gõ mã hoặc tên để tìm'}
              />
              {activeProductId && (
                <Select
                  size="large"
                  showSearch
                  optionFilterProp="label"
                  placeholder="Chọn lô (mặc định FEFO — có thể đổi)"
                  value={selectedBatchId}
                  options={batchOptions}
                  onChange={(value) => setSelectedBatchId(value)}
                  style={{ width: '100%' }}
                  disabled={submitting || batchOptions.length === 0}
                  notFoundContent="SP này chưa có lô tại kho"
                />
              )}
              <div style={{ display: 'flex', gap: 8, width: '100%', alignItems: 'stretch', flexWrap: 'wrap' }}>
                <InputNumber
                  size="large"
                  value={quantity}
                  onChange={(v) => setQuantity(Number(v ?? 1))}
                  style={{ width: 200, flex: '0 0 auto' }}
                  addonBefore="SL"
                  addonAfter={activeUnitName ?? '—'}
                  {...quantityInputNumberProps}
                />
                <Input
                  size="large"
                  placeholder="Ghi chú (Khu vực đang kiểm kê)"
                  value={zone}
                  onChange={(e) => setZone(e.target.value)}
                  style={{ flex: '1 1 200px', minWidth: 200 }}
                />
                <Button
                  size="large"
                  icon={<PlusOutlined />}
                  loading={resolving}
                  onClick={() => void handleAddDraftLine()}
                  style={{ flex: '0 0 auto' }}
                >
                  Thêm dòng
                </Button>
                <Button
                  type="primary"
                  size="large"
                  loading={submitting}
                  disabled={draftLines.length === 0}
                  onClick={() => void handleSubmitDraft()}
                  style={{ flex: '0 0 auto' }}
                >
                  Ghi nhận ({draftLines.length})
                </Button>
              </div>
            </Space>
          </Card>
        )}

        {draftLines.length > 0 && (
          <Card title="Dòng đang nhập" size="small">
            <Table
              rowKey="key"
              size="small"
              pagination={false}
              columns={draftColumns}
              dataSource={draftLines}
              scroll={{ x: 520 }}
            />
          </Card>
        )}

        <Card title="Bước 3 — Tổng hợp theo lô" size="small" loading={loading}>
          <Table
            rowKey={(r) => `${r.productId}-${r.batchId ?? 'batch'}`}
            size="small"
            pagination={false}
            columns={previewColumns}
            dataSource={previewByBatch}
            scroll={{ x: 520 }}
          />
        </Card>

        <Card title="Tổng theo sản phẩm" size="small" loading={loading}>
          <Table
            rowKey="productId"
            size="small"
            pagination={false}
            columns={productPreviewColumns}
            dataSource={previewByProduct}
            scroll={{ x: 480 }}
          />
        </Card>

        <Card title="Dòng đếm gần đây" size="small" loading={loading}>
          <Table
            rowKey="id"
            size="small"
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            columns={entryColumns}
            dataSource={entries}
            scroll={{ x: 560 }}
          />
        </Card>
      </Space>

      <InventoryCountApproveModal
        open={approveModalOpen}
        loading={approving}
        previewLines={previewByBatch}
        onCancel={() => setApproveModalOpen(false)}
        onConfirm={() => void handleApprove()}
      />
    </div>
  );
}
