import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  App,
  AutoComplete,
  Button,
  Card,
  Input,
  InputNumber,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  AppstoreOutlined,
  ArrowLeftOutlined,
  CheckOutlined,
  DeleteOutlined,
  PlusOutlined,
  PrinterOutlined,
  ReloadOutlined,
  ScanOutlined,
  StopOutlined,
} from '@ant-design/icons';
import { isAxiosError } from 'axios';
import { fetchProducts } from '@/shared/api/catalog.api';
import {
  addCountEntries,
  approveAdjustment,
  cancelAdjustment,
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
import { inventoryT } from '@/shared/i18n';
import { useInventoryEnums } from '@/shared/i18n/use-inventory-enums';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayQuantity, quantityInputNumberProps } from '@/shared/utils/money';
import { InventoryCountApproveModal } from '@/modules/inventory/InventoryCountApproveModal';
import { InventoryCountBatchPickModal } from '@/modules/inventory/InventoryCountBatchPickModal';
import { InventoryCountWorkflowSteps } from '@/modules/inventory/InventoryCountWorkflowSteps';
import { countVarianceSummary } from '@/modules/inventory/inventory-count-workflow';
import {
  expiryToneColor,
  getExpiryTone,
  sortBatchesForCount,
} from '@/modules/inventory/inventory-count-batch-sort';
import { printInventoryCountSheet } from '@/shared/print/inventory-count-print';
import { useAuthStore } from '@/shared/auth/auth.store';
import type { StockBatch } from '@/shared/api/inventory.types';

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
  const { message, modal } = App.useApp();
  const tenantCode = useAuthStore((s) => s.user?.tenantCode);
  const { t } = useTranslation('inventory', { keyPrefix: 'inventoryCount' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { adjustmentStatusLabel } = useInventoryEnums();

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
  const [productBatches, setProductBatches] = useState<StockBatch[]>([]);
  const [batchPickOpen, setBatchPickOpen] = useState(false);
  const [productOptions, setProductOptions] = useState<ProductSearchOption[]>([]);
  const [quantity, setQuantity] = useState(1);
  const [zone, setZone] = useState('');
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [approving, setApproving] = useState(false);
  const [resolving, setResolving] = useState(false);
  const [approveModalOpen, setApproveModalOpen] = useState(false);
  const [approveAttempted, setApproveAttempted] = useState(false);
  const [batchProbeDone, setBatchProbeDone] = useState(false);

  const entriesMissingBatch = entries.filter((e) => !e.batchId);
  const canApprove = previewByBatch.length > 0 && entriesMissingBatch.length === 0;
  const approveBlockReason = useMemo(() => {
    if (entriesMissingBatch.length > 0) {
      return t('approveBlock.missingBatch', { count: entriesMissingBatch.length });
    }
    if (previewByBatch.length === 0) {
      return t('approveBlock.noEntries');
    }
    return null;
  }, [entriesMissingBatch.length, previewByBatch.length, t]);

  const showNoBatchHint =
    Boolean(activeProductId) && batchProbeDone && batchOptions.length === 0;

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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [id, t]);

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
        setProductBatches([]);
        setSelectedBatchId(undefined);
        setBatchProbeDone(true);
        return {};
      }
      setBatchProbeDone(false);
      try {
        const result = await fetchStockBatches({
          warehouseId: detail.warehouseId,
          productId,
          page: 1,
          pageSize: 100,
        });
        const sorted = sortBatchesForCount(result.items);
        const inv = inventoryT();
        const options = sorted.map((b) => {
          const expiry = b.expiryDate
            ? inv('shared.expirySuffix', { date: formatDisplayDate(b.expiryDate) })
            : '';
          return {
            value: b.id,
            label: inv('shared.batchWithExpiry', {
              number: b.batchNumber,
              expiry,
              qty: formatDisplayQuantity(b.quantityAvailable),
            }),
          };
        });
        setProductBatches(sorted);
        setBatchOptions(options);

        const preferInStock =
          preferredBatchId && sorted.some((b) => b.id === preferredBatchId)
            ? preferredBatchId
            : sorted.find((b) => b.quantityAvailable > 0)?.id ?? sorted[0]?.id;
        setSelectedBatchId(preferInStock);
        const batchLabel = preferInStock
          ? options.find((o) => o.value === preferInStock)?.label
          : undefined;
        return { batchId: preferInStock, batchLabel };
      } catch {
        setBatchOptions([]);
        setProductBatches([]);
        setSelectedBatchId(undefined);
        return {};
      } finally {
        setBatchProbeDone(true);
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

  const clearProductInput = () => {
    setProductSearch('');
    setActiveProductId(null);
    setActiveUnitName(null);
    setSelectedBatchId(undefined);
    setBatchOptions([]);
    setProductBatches([]);
    setProductOptions([]);
    setBatchProbeDone(false);
    setQuantity(1);
  };

  const selectedBatchSummary = useMemo(() => {
    if (!selectedBatchId) return null;
    const batch = productBatches.find((b) => b.id === selectedBatchId);
    if (batch) return batch.batchNumber;
    const optLabel = batchOptions.find((b) => b.value === selectedBatchId)?.label;
    // Options label may be "3333 · HSD … · tồn …" — only show the batch number on the button.
    if (optLabel) return optLabel.split('·')[0]?.trim() || optLabel;
    return selectedBatchId;
  }, [selectedBatchId, productBatches, batchOptions]);

  const handleBatchPickConfirm = (selected: StockBatch[]) => {
    setBatchPickOpen(false);
    if (selected.length === 0) return;

    const inv = inventoryT();
    const toLabel = (b: StockBatch) => {
      const expiry = b.expiryDate
        ? inv('shared.expirySuffix', { date: formatDisplayDate(b.expiryDate) })
        : '';
      return inv('shared.batchWithExpiry', {
        number: b.batchNumber,
        expiry,
        qty: formatDisplayQuantity(b.quantityAvailable),
      });
    };

    // Một lô: gắn vào dòng đang đếm (Lưu đếm).
    if (selected.length === 1) {
      setSelectedBatchId(selected[0].id);
      message.success(t('messages.batchSelected', { batch: selected[0].batchNumber }));
      return;
    }

    // Nhiều lô: mỗi lô → 1 dòng chờ, SL mặc định = tồn hệ thống (chỉnh trên bảng trước khi Lưu dòng chờ).
    if (!activeProductId) {
      message.warning(t('messages.scanOrSelectProduct'));
      return;
    }

    const lines: DraftLine[] = selected.map((b) => ({
      key: nextDraftKey(),
      productId: activeProductId,
      productLabel: productSearch,
      batchId: b.id,
      batchLabel: toLabel(b),
      quantity: Math.max(0, Number(b.quantityAvailable) || 0),
      unitName: activeUnitName ?? undefined,
      zone: zone.trim() || undefined,
    }));
    setDraftLines((prev) => [...lines, ...prev]);
    setSelectedBatchId(selected[0].id);
    message.success(t('messages.batchesAddedToDraft', { count: lines.length }));
  };

  const buildResolvedLine = async (): Promise<DraftLine | null> => {
    if (!productSearch.trim()) {
      message.warning(t('messages.scanOrSelectProduct'));
      return null;
    }
    if (quantity < 0) {
      message.warning(t('messages.quantityMustBeNonNegative'));
      return null;
    }

    const resolved = await resolveActiveProduct();
    if (!resolved) {
      message.warning(t('messages.selectFromSuggestions'));
      return null;
    }

    const batchId = resolved.batchId ?? selectedBatchId;
    if (!batchId) {
      message.warning(t('messages.noBatchAtWarehouse'));
      return null;
    }

    const batchLabel =
      batchOptions.find((b) => b.value === batchId)?.label ?? resolved.batchLabel ?? batchId;
    return {
      key: nextDraftKey(),
      productId: resolved.productId,
      productLabel: resolved.label,
      batchId,
      batchLabel,
      quantity,
      unitName: resolved.unitName ?? undefined,
      zone: zone.trim() || undefined,
      scannedBarcode: resolved.scannedBarcode,
    };
  };

  const handleAddDraftLine = async () => {
    const line = await buildResolvedLine();
    if (!line) return;
    setDraftLines((prev) => [line, ...prev]);
    setQuantity(1);
    message.success(t('messages.addedToDraft'));
  };

  const handleRecordNow = async () => {
    if (!id) return;
    const line = await buildResolvedLine();
    if (!line) return;

    setSubmitting(true);
    try {
      await addCountEntries(id, [
        {
          productId: line.productId,
          batchId: line.batchId,
          quantity: line.quantity,
          scannedBarcode: line.scannedBarcode,
          zone: line.zone,
        },
      ]);
      clearProductInput();
      message.success(t('messages.entryRecordedNow'));
      await load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.recordFailed')));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleRemoveDraftLine = (key: string) => {
    setDraftLines((prev) => prev.filter((line) => line.key !== key));
  };

  const handleDraftQuantityChange = (key: string, next: number | null) => {
    const qty = Number(next ?? 0);
    setDraftLines((prev) =>
      prev.map((line) => (line.key === key ? { ...line, quantity: qty } : line)),
    );
  };

  const handleSubmitDraft = async () => {
    if (!id || draftLines.length === 0) {
      message.warning(t('messages.noDraftLines'));
      return;
    }
    if (draftLines.some((line) => line.quantity < 0)) {
      message.warning(t('messages.quantityMustBeNonNegative'));
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
      clearProductInput();
      message.success(t('messages.entriesRecorded', { count: lineCount }));
      await load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.recordFailed')));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeleteEntry = async (entryId: string) => {
    if (!id) return;
    try {
      await deleteCountEntry(id, entryId);
      message.success(tc('messages.deleted'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, tc('messages.deleteFailed')));
    }
  };

  const handleApprove = async () => {
    if (!id) return;
    if (!canApprove) {
      message.warning(approveBlockReason ?? t('approveBlock.cannotApprove'));
      return;
    }

    setApproving(true);
    try {
      await approveAdjustment(id);
      setApproveModalOpen(false);
      message.success(t('messages.approveSuccess'));
      navigate('/inventory/adjustments');
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.approveFailed')));
    } finally {
      setApproving(false);
    }
  };

  const openApproveConfirm = () => {
    if (!canApprove) {
      setApproveAttempted(true);
      message.warning(approveBlockReason ?? t('approveBlock.cannotApprove'));
      return;
    }
    setApproveAttempted(false);
    setApproveModalOpen(true);
  };

  const handleCancelSession = () => {
    if (!id || !detail) return;
    modal.confirm({
      title: t('messages.cancelConfirmTitle'),
      content: t('messages.cancelConfirmBody'),
      okText: t('messages.cancelConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('messages.cancelConfirmKeep'),
      centered: true,
      onOk: async () => {
        try {
          await cancelAdjustment(id);
          message.success(t('messages.cancelSuccess'));
          navigate('/inventory/adjustments');
        } catch (error) {
          message.error(apiErrorMessage(error, t('messages.cancelFailed')));
          throw error;
        }
      },
    });
  };

  const varianceSummary = countVarianceSummary(previewByBatch);

  const previewColumns: ColumnsType<AdjustmentCountPreviewLine> = [
    { title: ts('productAbbr'), dataIndex: 'productName', ellipsis: true },
    { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 96, render: (v) => v ?? '—' },
    { title: ts('systemQtyAbbr'), dataIndex: 'systemQuantity', width: 72, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    { title: ts('countAbbr'), dataIndex: 'countedQuantity', width: 72, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: ts('varianceAbbr'),
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
    { title: ts('productAbbr'), dataIndex: 'productName', ellipsis: true },
    { title: ts('systemQtyAbbr'), dataIndex: 'systemQuantity', width: 80, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    { title: ts('countAbbr'), dataIndex: 'countedQuantity', width: 80, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: ts('varianceAbbr'),
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
    { title: ts('productAbbr'), dataIndex: 'productLabel', ellipsis: true },
    { title: ts('batchAbbr'), dataIndex: 'batchLabel', ellipsis: true },
    {
      title: ts('quantityAbbr'),
      dataIndex: 'quantity',
      width: 110,
      align: 'right',
      render: (v: number, row) => (
        <InputNumber
          size="small"
          {...quantityInputNumberProps}
          min={0}
          value={v}
          disabled={submitting}
          style={{ width: '100%' }}
          onChange={(next) => handleDraftQuantityChange(row.key, next)}
        />
      ),
    },
    { title: ts('zone'), dataIndex: 'zone', width: 120, ellipsis: true, render: (v) => v ?? '—' },
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
    { title: ts('productAbbr'), dataIndex: 'productName', ellipsis: true },
    { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 96, render: (v) => v ?? '—' },
    { title: ts('quantityAbbr'), dataIndex: 'quantity', width: 88, align: 'right', render: (v: number) => formatDisplayQuantity(v) },
    {
      title: ts('counter'),
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
    <div style={{ maxWidth: 1200, margin: '0 auto' }}>
      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Space wrap>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/inventory/adjustments')}>
            {t('backToList')}
          </Button>
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            {tc('actions.reload')}
          </Button>
          {detail && previewByBatch.length > 0 && (
            <Button
              icon={<PrinterOutlined />}
              onClick={() => printInventoryCountSheet(detail, previewByBatch, tenantCode)}
            >
              {t('printA4')}
            </Button>
          )}
          {detail && detail.status === 2 && (
            <Tooltip title={approveBlockReason ?? t('approveTooltip')}>
              <Button
                type="primary"
                icon={<CheckOutlined />}
                loading={approving}
                onClick={openApproveConfirm}
              >
                {tc('actions.approve')}
              </Button>
            </Tooltip>
          )}
          {detail && detail.status === 2 && (
            <Button danger icon={<StopOutlined />} onClick={handleCancelSession}>
              {ts('cancel')}
            </Button>
          )}
        </Space>

        {detail && (
          <Card size="small" title={t('workflowTitle')} loading={loading}>
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
                <strong>{ts('warehouse')}:</strong> {detail.warehouseName}
              </span>
              <Tag>{adjustmentStatusLabel(detail.status)}</Tag>
              <span>{formatDisplayDate(detail.adjustmentDate)}</span>
            </Space>
            {detail.status === 2 && approveAttempted && approveBlockReason && (
              <Typography.Paragraph type="danger" style={{ marginBottom: 0, marginTop: 12 }}>
                {approveBlockReason}{t('approveBlock.beforeApprove')}
              </Typography.Paragraph>
            )}
            {detail.status === 2 && entries.length > 0 && (
              <Typography.Paragraph style={{ marginBottom: 0, marginTop: 12 }}>
                <strong>{t('reconcile.title')}</strong>{' '}
                {varianceSummary.varianceLines > 0
                  ? t('reconcile.varianceLines', { count: varianceSummary.varianceLines })
                  : t('reconcile.noVariance')}
              </Typography.Paragraph>
            )}
            {detail.status === 2 && canApprove && (
              <Typography.Paragraph type="success" style={{ marginBottom: 0, marginTop: 12 }}>
                <Trans i18nKey="readyToApprove" ns="inventory" t={t} />
              </Typography.Paragraph>
            )}
          </Card>
        )}

        {detail?.status === 2 && (
          <Card title={<><ScanOutlined /> {t('step2Title')}</>} size="small">
            <Space direction="vertical" style={{ width: '100%' }} size="middle">
              <Typography.Text type="secondary">{t('countHint')}</Typography.Text>
              <div
                style={{
                  display: 'flex',
                  gap: 8,
                  width: '100%',
                  alignItems: 'stretch',
                  flexWrap: 'nowrap',
                }}
              >
                <AutoComplete
                  style={{ flex: '1 1 auto', minWidth: 280 }}
                  size="large"
                  placeholder={t('scanPlaceholder')}
                  value={productSearch}
                  options={productOptions}
                  onChange={(value) => {
                    setProductSearch(value);
                    setActiveProductId(null);
                    setActiveUnitName(null);
                    setSelectedBatchId(undefined);
                    setBatchOptions([]);
                    setProductBatches([]);
                    setBatchProbeDone(false);
                  }}
                  onSelect={(value, option) => {
                    const picked = option as ProductSearchOption;
                    void applyProductContext(String(value), String(picked.label ?? value), picked.unitName ?? null);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') void handleRecordNow();
                  }}
                  disabled={submitting || resolving}
                  notFoundContent={
                    productSearch.trim() ? t('notFound.noProducts') : t('notFound.typeToSearch')
                  }
                />
                {activeProductId && batchOptions.length > 0 && (
                  <Button
                    size="large"
                    icon={<AppstoreOutlined />}
                    onClick={() => setBatchPickOpen(true)}
                    disabled={submitting}
                    title={t('pickBatchesTooltip')}
                    style={{ flex: '0 0 420px', width: 420, textAlign: 'left' }}
                  >
                    {selectedBatchSummary ? (
                      <span>
                        <strong
                          style={{
                            color: expiryToneColor(
                              getExpiryTone(
                                productBatches.find((b) => b.id === selectedBatchId)?.expiryDate,
                              ),
                            ),
                          }}
                        >
                          {t('selectedBatchLabel', { batch: selectedBatchSummary })}
                        </strong>
                      </span>
                    ) : (
                      <span>
                        {t('pickBatchesShort')}
                        {productBatches.length > 0 ? (
                          <span style={{ color: '#64748b', fontWeight: 400, marginLeft: 6 }}>
                            {t('batchesAvailableHint', { count: productBatches.length })}
                          </span>
                        ) : null}
                      </span>
                    )}
                  </Button>
                )}
              </div>
              {showNoBatchHint && (
                <Alert
                  type="warning"
                  showIcon
                  message={t('noBatchHintTitle')}
                  description={
                    <Space direction="vertical" size={4}>
                      <span>{t('noBatchHintBody')}</span>
                      <Space wrap>
                        <Link to="/inventory/opening-balance">{t('goOpeningBalance')}</Link>
                        <Link to="/procurement/goods-receipts">{t('goGoodsReceipt')}</Link>
                      </Space>
                    </Space>
                  }
                />
              )}
              <div
                style={{
                  display: 'flex',
                  gap: 8,
                  width: '100%',
                  alignItems: 'stretch',
                  flexWrap: 'nowrap',
                }}
              >
                <InputNumber
                  size="large"
                  value={quantity}
                  onChange={(v) => setQuantity(Number(v ?? 0))}
                  onPressEnter={() => void handleRecordNow()}
                  style={{ width: 180, flex: '0 0 auto' }}
                  addonBefore={ts('quantityAbbr')}
                  addonAfter={activeUnitName ?? '—'}
                  {...quantityInputNumberProps}
                  min={0}
                />
                <Input
                  size="large"
                  placeholder={t('zonePlaceholder')}
                  value={zone}
                  onChange={(e) => setZone(e.target.value)}
                  onPressEnter={() => void handleRecordNow()}
                  style={{ flex: '1 1 auto', minWidth: 160 }}
                />
                <Button
                  type="primary"
                  size="large"
                  icon={<CheckOutlined />}
                  loading={submitting || resolving}
                  disabled={showNoBatchHint}
                  onClick={() => void handleRecordNow()}
                  style={{ flex: '0 0 auto' }}
                >
                  {t('recordNow')}
                </Button>
                <Button
                  size="large"
                  icon={<PlusOutlined />}
                  loading={resolving}
                  disabled={submitting || showNoBatchHint}
                  onClick={() => void handleAddDraftLine()}
                  style={{ flex: '0 0 auto' }}
                >
                  {t('addToDraft')}
                </Button>
                {draftLines.length > 0 && (
                  <Button
                    size="large"
                    loading={submitting}
                    onClick={() => void handleSubmitDraft()}
                    style={{ flex: '0 0 auto' }}
                  >
                    {t('recordEntries', { count: draftLines.length })}
                  </Button>
                )}
              </div>
            </Space>
          </Card>
        )}

        {draftLines.length > 0 && (
          <Card title={t('draftTitle')} size="small">
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

        <Card title={t('step3BatchTitle')} size="small" loading={loading}>
          <Table
            rowKey={(r) => `${r.productId}-${r.batchId ?? 'batch'}`}
            size="small"
            pagination={false}
            columns={previewColumns}
            dataSource={previewByBatch}
            scroll={{ x: 520 }}
          />
        </Card>

        <Card title={t('step3ProductTitle')} size="small" loading={loading}>
          <Table
            rowKey="productId"
            size="small"
            pagination={false}
            columns={productPreviewColumns}
            dataSource={previewByProduct}
            scroll={{ x: 480 }}
          />
        </Card>

        <Card title={t('recentEntriesTitle')} size="small" loading={loading}>
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

      <InventoryCountBatchPickModal
        open={batchPickOpen}
        productLabel={productSearch || t('step2Title')}
        batches={productBatches}
        initialSelectedIds={selectedBatchId ? [selectedBatchId] : []}
        onCancel={() => setBatchPickOpen(false)}
        onConfirm={handleBatchPickConfirm}
      />
    </div>
  );
}
