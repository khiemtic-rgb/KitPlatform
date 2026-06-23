import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import dayjs from 'dayjs';
import {
  Alert,
  App,
  AutoComplete,
  Button,
  Card,
  InputNumber,
  Select,
  Space,
  Table,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, PrinterOutlined } from '@ant-design/icons';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { createSale, completeDraftSale, fetchBatchModeSettings, fetchOpenShift, fetchPosStockBulk, fetchSalesOrder, lookupPosProduct, openSalesShift, previewPosAllocation, searchCustomers, searchPosProducts, updateDraftSale, type TenantBatchModeValue } from '@/shared/api/sales.api';
import {
  isShiftAlreadyOpenError,
  loadOpenShiftForWarehouse,
  shiftAlreadyOpenMessage,
} from '@/modules/sales/sales-shift-helpers';
import type { CartLine, CustomerListItem, PosCheckoutPaymentLine, SalesOrderDetail, SalesShiftDetail } from '@/shared/api/sales.types';
import { SALES_DISCOUNT_TYPES } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { PosCheckoutModal } from '@/modules/sales/PosCheckoutModal';
import { PosCartQuantityInput } from '@/modules/sales/PosCartQuantityInput';
import { formatSuggestedBatch } from '@/modules/sales/pos-batch-display';
import {
  initialBatchLabelForMode,
  showsBatchHints,
  showsBatchLabelField,
  validateCartBatchLabels,
} from '@/modules/sales/pos-batch-mode-ui';
import { applyBatchLabelScan } from '@/modules/sales/pos-batch-scan';
import { POS_CART_DISCOUNT_TYPE_OPTIONS } from '@/modules/sales/pos-cart-table-options';
import { capQuantityToStock, outOfStockWarningText, stockCapWarningText } from '@/modules/sales/pos-stock-messages';
import { buildCreateSalePayload, buildDraftUpdatePayload, buildSaleLineItems } from '@/modules/sales/pos-sale-payload';
import { OpenShiftModal } from '@/modules/sales/OpenShiftModal';
import { PosSummaryDivider, PosSummaryOrderDiscountRow, PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { printSalesInvoice } from '@/modules/sales/sales-invoice-print';
import { loadReceiptStoreSettings } from '@/modules/sales/receipt-settings';
import {
  lineNet,
  priceCart,
  validateCartDiscountPolicy,
  type OrderDiscountState,
} from '@/modules/sales/pos-pricing';
import { useSalesDiscountPolicy } from '@/modules/sales/useSalesDiscountPolicy';
import {
  clearPosDraftEdit,
  loadDraftCartLines,
  orderDiscountFromDetail,
  persistPosDraftEdit,
  readPosDraftEditId,
} from '@/modules/sales/sales-draft-helpers';
import { formatDisplayMoney, moneyInputNumberPropsAllowZeroSuffix, moneyInputNumberStyle } from '@/shared/utils/money';

export function PosPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canWrite = useHasPermission('sales.write');
  const { canDiscount, maxPercent, unlimited } = useSalesDiscountPolicy();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [customerId, setCustomerId] = useState<string>();
  const [barcode, setBarcode] = useState('');
  const [productSearchOptions, setProductSearchOptions] = useState<
    { value: string; label: string }[]
  >([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [orderDiscount, setOrderDiscount] = useState<OrderDiscountState>({});
  const [saving, setSaving] = useState(false);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [lastCompletedOrder, setLastCompletedOrder] = useState<SalesOrderDetail | null>(null);
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [openShiftModal, setOpenShiftModal] = useState(false);
  const [shiftSaving, setShiftSaving] = useState(false);
  const [editingDraftId, setEditingDraftId] = useState<string | null>(null);
  const [editingDraftNumber, setEditingDraftNumber] = useState<string | null>(null);
  const [draftLoading, setDraftLoading] = useState(false);
  const [checkoutValidating, setCheckoutValidating] = useState(false);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');

  const pricing = useMemo(() => priceCart(cart, orderDiscount), [cart, orderDiscount]);

  const resetCart = useCallback(() => {
    setCart([]);
    setOrderDiscount({});
  }, []);

  const clearDraftEdit = useCallback(() => {
    setEditingDraftId(null);
    setEditingDraftNumber(null);
    clearPosDraftEdit();
    setSearchParams({}, { replace: true });
    resetCart();
  }, [resetCart, setSearchParams]);

  const loadOpenShift = useCallback(async (whId: string) => {
    const shift = await loadOpenShiftForWarehouse(whId);
    setOpenShift(shift);
    return shift;
  }, []);

  useEffect(() => {
    if (!warehouseId) {
      setOpenShift(null);
      return;
    }
    void loadOpenShift(warehouseId).catch((error) => {
      setOpenShift(null);
      message.error(apiErrorMessage(error, 'Không tải được trạng thái ca'));
    });
  }, [warehouseId, loadOpenShift]);

  useEffect(() => {
    void (async () => {
      const wh = await fetchWarehouses();
      setWarehouses(wh);
      const defaultWh = wh.find((w) => w.isDefault) ?? wh[0];
      if (defaultWh && !warehouseId) setWarehouseId(defaultWh.id);
      setCustomers(await searchCustomers());
      void loadReceiptStoreSettings();
    })();
  }, []);

  useEffect(() => {
    void fetchBatchModeSettings()
      .then(setBatchMode)
      .catch(() => setBatchMode('suggest'));
  }, []);

  useEffect(() => {
    if (!warehouseId) {
      setProductSearchOptions([]);
      return;
    }
    const q = barcode.trim();
    if (q.length < 1) {
      setProductSearchOptions([]);
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      void (async () => {
        try {
          const items = await searchPosProducts(q, warehouseId);
          if (cancelled) return;
          setProductSearchOptions(
            items.map((p) => ({
              value: p.lookupCode,
              label: `${p.productCode} — ${p.productName} · tồn ${p.stockAvailable.toLocaleString('vi-VN')} ${p.unitName}`,
            })),
          );
        } catch {
          if (!cancelled) setProductSearchOptions([]);
        }
      })();
    }, 300);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [barcode, warehouseId]);

  const loadDraftFromUrl = useCallback(
    async (draftId: string) => {
      setDraftLoading(true);
      try {
        const order = await fetchSalesOrder(draftId);
        if (order.status !== 1) {
          message.warning('Đơn không còn ở trạng thái nháp');
          clearDraftEdit();
          return;
        }
        setWarehouseId(order.warehouseId);
        setCustomerId(order.customerId);
        setCart(await loadDraftCartLines(order));
        setOrderDiscount(orderDiscountFromDetail(order));
        setEditingDraftId(order.id);
        setEditingDraftNumber(order.orderNumber);
        persistPosDraftEdit(order.id);
        if (searchParams.get('draftId') !== order.id) {
          setSearchParams({ draftId: order.id }, { replace: true });
        }
      } catch (error) {
        message.error(apiErrorMessage(error, 'Không tải được đơn nháp'));
        clearDraftEdit();
      } finally {
        setDraftLoading(false);
      }
    },
    [clearDraftEdit, searchParams, setSearchParams],
  );

  useEffect(() => {
    const draftId = searchParams.get('draftId') ?? readPosDraftEditId();
    if (!draftId) return;
    if (editingDraftId === draftId) return;
    if (!searchParams.get('draftId')) {
      setSearchParams({ draftId }, { replace: true });
    }
    void loadDraftFromUrl(draftId);
  }, [searchParams, editingDraftId, loadDraftFromUrl, setSearchParams]);

  const validateDiscounts = useCallback(() => {
    if (!canDiscount) {
      const hasLineDiscount = cart.some((line) => (line.discountValue ?? 0) > 0);
      const hasOrderDiscount = (orderDiscount.discountValue ?? 0) > 0;
      if (hasLineDiscount || hasOrderDiscount) {
        message.error('Tài khoản không có quyền chiết khấu');
        return false;
      }
      return true;
    }

    const policyError = validateCartDiscountPolicy(cart, orderDiscount, maxPercent, unlimited);
    if (policyError) {
      message.error(policyError);
      return false;
    }

    return true;
  }, [canDiscount, cart, maxPercent, orderDiscount, unlimited]);

  const validateBatchLabels = useCallback((): boolean => {
    const error = validateCartBatchLabels(cart, batchMode);
    if (error) {
      message.warning(error);
      return false;
    }
    return true;
  }, [batchMode, cart, message]);

  const refreshCartStock = useCallback(async () => {
    if (!warehouseId || cart.length === 0) return;
    const stocks = await fetchPosStockBulk(
      warehouseId,
      cart.map((line) => line.productUnitId),
    );
    const stockByUnit = new Map(stocks.map((s) => [s.productUnitId, s]));
    setCart((prev) =>
      prev.map((line) => {
        const stock = stockByUnit.get(line.productUnitId);
        return stock ? { ...line, stockAvailable: stock.stockAvailable } : line;
      }),
    );
  }, [cart, warehouseId]);

  const validateStock = useCallback(async () => {
    if (!warehouseId) return false;
    let hadCap = false;
    const stocks = await fetchPosStockBulk(
      warehouseId,
      cart.map((line) => line.productUnitId),
    );
    const stockByUnit = new Map(stocks.map((s) => [s.productUnitId, s]));
    for (const line of cart) {
      const stock = stockByUnit.get(line.productUnitId);
      if (!stock) continue;
      if (line.quantity > stock.stockAvailable + 0.0001) {
        hadCap = true;
      }
    }
    if (hadCap) {
      setCart((prev) =>
        prev.map((line) => {
          const stock = stockByUnit.get(line.productUnitId);
          if (!stock) return line;
          const over = line.quantity > stock.stockAvailable + 0.0001;
          return {
            ...line,
            stockAvailable: stock.stockAvailable,
            quantity: over ? capQuantityToStock(stock.stockAvailable, line.quantity) : line.quantity,
            qtyWarning: over
              ? stockCapWarningText(stock.stockAvailable, stock.unitName)
              : line.qtyWarning,
          };
        }),
      );
      return false;
    }
    setCart((prev) =>
      prev.map((line) => {
        const stock = stockByUnit.get(line.productUnitId);
        return stock ? { ...line, stockAvailable: stock.stockAvailable } : line;
      }),
    );
    return true;
  }, [cart, warehouseId]);

  const validateFefoAllocation = useCallback(async () => {
    if (!warehouseId || cart.length === 0) return true;
    try {
      await previewPosAllocation({
        warehouseId,
        items: cart.map((c) => ({
          productId: c.productId,
          productUnitId: c.productUnitId,
          quantity: c.quantity,
        })),
      });
      return true;
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đủ tồn kho theo FEFO'));
      return false;
    }
  }, [cart, message, warehouseId]);

  const addByBarcode = useCallback(
    async (code?: string) => {
      const value = (code ?? barcode).trim();
      if (!warehouseId || !value) return;
      try {
        const item = await lookupPosProduct(value, warehouseId);
        if (item.stockAvailable <= 0) {
          message.warning('Sản phẩm hết tồn tại kho này');
          return;
        }
        const capWarning = stockCapWarningText(item.stockAvailable, item.unitName);
        setCart((prev) => {
          const existing = prev.find((l) => l.productUnitId === item.productUnitId);
          if (existing) {
            const nextQty = existing.quantity + 1;
            if (nextQty > item.stockAvailable + 0.0001) {
              return prev.map((l) =>
                l.productUnitId === item.productUnitId
                  ? {
                      ...l,
                      quantity: capQuantityToStock(item.stockAvailable, nextQty),
                      stockAvailable: item.stockAvailable,
                      qtyWarning: capWarning,
                      batchHints: item.batchHints ?? l.batchHints,
                      batchLabel: l.batchLabel ?? initialBatchLabelForMode(batchMode, item.batchHints ?? l.batchHints),
                      stockSourceLabel: item.stockSourceLabel ?? l.stockSourceLabel,
                    }
                  : l,
              );
            }
            return prev.map((l) =>
              l.productUnitId === item.productUnitId
                ? {
                    ...l,
                    quantity: nextQty,
                    batchHints: item.batchHints ?? l.batchHints,
                    batchLabel: l.batchLabel ?? initialBatchLabelForMode(batchMode, item.batchHints ?? l.batchHints),
                    stockSourceLabel: item.stockSourceLabel ?? l.stockSourceLabel,
                  }
                : l,
            );
          }
          return [
            ...prev,
            {
              key: item.productUnitId,
              productId: item.productId,
              productCode: item.productCode,
              productName: item.productName,
              productUnitId: item.productUnitId,
              unitName: item.unitName,
              quantity: 1,
              unitPrice: item.unitPrice,
              stockAvailable: item.stockAvailable,
              batchHints: item.batchHints,
              batchLabel: initialBatchLabelForMode(batchMode, item.batchHints),
              stockSourceLabel: item.stockSourceLabel,
            },
          ];
        });
        setBarcode('');
      } catch (error) {
        if (showsBatchLabelField(batchMode)) {
          const batchResult = applyBatchLabelScan(cart, value);
          if (batchResult) {
            setCart(batchResult.cart);
            message.success(`Đã gán lô ${batchResult.batchNumber} — ${batchResult.productName}`);
            setBarcode('');
            return;
          }
        }
        message.error(apiErrorMessage(error, 'Không tìm thấy sản phẩm'));
      }
    },
    [barcode, batchMode, cart, message, warehouseId],
  );

  const resetCartAndExitDraft = () => {
    clearDraftEdit();
  };

  const saveDraft = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho bán trước');
      return;
    }
    if (cart.length === 0) {
      message.warning('Thêm ít nhất một sản phẩm');
      return;
    }
    if (!validateDiscounts()) return;
    if (!validateBatchLabels()) return;
    const updatingExisting = Boolean(editingDraftId);
    setSaving(true);
    const hideLoading = message.loading(
      updatingExisting ? 'Đang cập nhật đơn nháp...' : 'Đang lưu đơn nháp...',
      0,
    );
    try {
      if (editingDraftId) {
        const order = await updateDraftSale(
          editingDraftId,
          buildDraftUpdatePayload(customerId, cart, orderDiscount),
        );
        hideLoading();
        message.success(
          `Đã cập nhật nháp ${order.orderNumber} — ${formatDisplayMoney(order.totalAmount)}`,
        );
        clearDraftEdit();
        navigate(`/sales/orders?orderId=${order.id}`);
      } else {
        const order = await createSale(
          buildCreateSalePayload(warehouseId, customerId, cart, orderDiscount, true),
        );
        hideLoading();
        clearPosDraftEdit();
        message.success(`Đã lưu nháp ${order.orderNumber}`);
        resetCart();
        navigate(`/sales/orders?orderId=${order.id}`);
      }
    } catch (error) {
      hideLoading();
      message.error(apiErrorMessage(error, updatingExisting ? 'Không cập nhật được đơn nháp' : 'Không lưu được đơn nháp'));
    } finally {
      setSaving(false);
    }
  };

  const confirmCheckout = async (payments: PosCheckoutPaymentLine[]) => {
    if (!warehouseId) {
      message.warning('Chọn kho bán trước');
      throw new Error('missing-warehouse');
    }
    if (cart.length === 0) {
      message.warning('Giỏ hàng trống');
      throw new Error('empty-cart');
    }
    if (!validateDiscounts()) {
      throw new Error('invalid-discount');
    }
    if (!validateBatchLabels()) {
      throw new Error('invalid-batch-label');
    }
    setSaving(true);
    const hideLoading = message.loading('Đang ghi đơn bán...', 0);
    try {
      let order: SalesOrderDetail;
      if (editingDraftId) {
        await updateDraftSale(
          editingDraftId,
          buildDraftUpdatePayload(customerId, cart, orderDiscount),
        );
        order = await completeDraftSale(editingDraftId, payments, buildSaleLineItems(cart));
      } else {
        order = await createSale(
          buildCreateSalePayload(warehouseId, customerId, cart, orderDiscount, false, payments),
        );
      }
      hideLoading();
      message.success(`Đã bán ${order.orderNumber} — ${formatDisplayMoney(order.totalAmount)}`);
      setCheckoutOpen(false);
      clearDraftEdit();
      resetCart();
      void printSalesInvoice(order).then((printed) => {
        if (!printed) {
          setLastCompletedOrder(order);
          message.warning('Trình duyệt chặn cửa sổ in — bấm In hóa đơn bên dưới.');
        }
      });
      if (warehouseId) void loadOpenShift(warehouseId);
    } catch (error) {
      hideLoading();
      await refreshCartStock();
      throw error;
    } finally {
      setSaving(false);
    }
  };

  const openCheckout = useCallback(async () => {
    if (!openShift) {
      message.warning('Mở ca trước khi thanh toán');
      return;
    }
    if (!validateDiscounts()) return;
    if (!validateBatchLabels()) return;
    setCheckoutValidating(true);
    try {
      if (!(await validateStock())) return;
      if (!(await validateFefoAllocation())) return;
      setCheckoutOpen(true);
    } finally {
      setCheckoutValidating(false);
    }
  }, [
    openShift,
    validateDiscounts,
    validateBatchLabels,
    validateFefoAllocation,
    validateStock,
  ]);

  const cartLocked = checkoutOpen || saving;

  const columns: ColumnsType<CartLine> = [
    {
      title: 'Sản phẩm',
      render: (_, row) => {
        const suggestedBatch = formatSuggestedBatch(row.batchHints);
        const stockWarning =
          row.qtyWarning ??
          (row.stockAvailable != null && row.stockAvailable <= 0
            ? outOfStockWarningText(row.unitName)
            : null);
        const body = (
          <div style={{ lineHeight: 1.45 }}>
            <Typography.Text type="secondary" style={{ fontSize: 11, lineHeight: 1.35 }}>
              Mã SP: {row.productCode} | ĐVT lẻ: {row.unitName}
            </Typography.Text>
            <div>
              <Typography.Text strong>{row.productName}</Typography.Text>
              <Typography.Text
                style={{ marginLeft: 6, fontSize: '0.88em', color: '#0d7377', fontWeight: 500 }}
              >
                (tồn {(row.stockAvailable ?? 0).toLocaleString('vi-VN')})
              </Typography.Text>
            </div>
            {stockWarning ? (
              <Typography.Text type="warning" style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                {stockWarning}
              </Typography.Text>
            ) : null}
          </div>
        );
        return showsBatchHints(batchMode) && suggestedBatch !== '—' ? (
          <Tooltip title={`Lô FEFO gợi ý: ${suggestedBatch}`}>{body}</Tooltip>
        ) : (
          body
        );
      },
    },
    ...(showsBatchLabelField(batchMode)
      ? ([
          {
            title: 'Lô',
            width: 150,
            render: (_, row) => (
              <Select
                showSearch
                allowClear={batchMode !== 'label_required'}
                placeholder="Chọn lô"
                style={{ width: 140 }}
                disabled={!canWrite || cartLocked}
                value={row.batchLabel || undefined}
                options={(row.batchHints ?? []).map((hint) => ({
                  value: hint.batchNumber,
                  label: formatSuggestedBatch([hint]),
                }))}
                onChange={(value) =>
                  setCart((prev) =>
                    prev.map((l) => (l.key === row.key ? { ...l, batchLabel: value ?? '' } : l)),
                  )
                }
              />
            ),
          },
        ] as ColumnsType<CartLine>)
      : []),
    {
      title: 'SL',
      dataIndex: 'quantity',
      width: 88,
      align: 'right',
      render: (_v: number, row) => (
        <PosCartQuantityInput
          value={row.quantity}
          stockAvailable={row.stockAvailable ?? 0}
          unitName={row.unitName}
          disabled={!canWrite || cartLocked}
          externalWarning={row.qtyWarning}
          showInlineWarning={false}
          onQtyWarningChange={(warning) =>
            setCart((prev) =>
              prev.map((l) =>
                l.key === row.key ? { ...l, qtyWarning: warning ?? undefined } : l,
              ),
            )
          }
          onChange={(quantity) =>
            setCart((prev) =>
              prev.map((l) => (l.key === row.key ? { ...l, quantity } : l)),
            )
          }
          onClearWarning={() =>
            setCart((prev) =>
              prev.map((l) => (l.key === row.key ? { ...l, qtyWarning: undefined } : l)),
            )
          }
        />
      ),
    },
    {
      title: 'Đơn giá',
      dataIndex: 'unitPrice',
      width: 100,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    ...(canDiscount
      ? ([
          {
            title: 'CK',
            width: 124,
            render: (_, row) => (
              <Space.Compact>
                <Select
                  allowClear
                  placeholder="%"
                  style={{ width: 52 }}
                  disabled={!canWrite || cartLocked}
                  value={row.discountType}
                  onChange={(discountType) =>
                    setCart((prev) =>
                      prev.map((l) =>
                        l.key === row.key
                          ? { ...l, discountType, discountValue: discountType ? l.discountValue ?? 0 : undefined }
                          : l,
                      ),
                    )
                  }
                  options={[...POS_CART_DISCOUNT_TYPE_OPTIONS]}
                />
                <InputNumber
                  disabled={!canWrite || cartLocked || !row.discountType}
                  value={row.discountValue}
                  {...(row.discountType === SALES_DISCOUNT_TYPES.Fixed
                    ? { ...moneyInputNumberPropsAllowZeroSuffix, style: { ...moneyInputNumberStyle, width: 72 } }
                    : { min: 0, max: 100, style: { ...moneyInputNumberStyle, width: 56 } })}
                  onChange={(discountValue) =>
                    setCart((prev) =>
                      prev.map((l) =>
                        l.key === row.key ? { ...l, discountValue: Number(discountValue ?? 0) } : l,
                      ),
                    )
                  }
                />
              </Space.Compact>
            ),
          },
        ] as ColumnsType<CartLine>)
      : []),
    {
      title: 'Thành tiền',
      width: 136,
      align: 'right',
      render: (_, row) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(lineNet(row))}</span>
      ),
    },
    {
      title: '',
      width: 44,
      align: 'center',
      render: (_, row) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          disabled={!canWrite || cartLocked}
          aria-label="Xóa dòng"
          onClick={() => setCart((p) => p.filter((l) => l.key !== row.key))}
        />
      ),
    },
  ];

  const handleOpenShift = async (openingCash: number) => {
    if (!warehouseId) {
      message.warning('Chọn kho trước khi mở ca');
      throw new Error('missing warehouse');
    }
    setShiftSaving(true);
    try {
      const shift = await openSalesShift({ warehouseId, openingCash });
      setOpenShift(shift);
      setOpenShiftModal(false);
      message.success(`Đã mở ca ${shift.shiftNumber}`);
    } catch (error) {
      if (isShiftAlreadyOpenError(error)) {
        const existing = await fetchOpenShift(warehouseId);
        if (existing) {
          setOpenShift(existing);
          setOpenShiftModal(false);
          message.info(shiftAlreadyOpenMessage(existing));
          return;
        }
      }
      message.error(apiErrorMessage(error, 'Không mở được ca'));
      throw error;
    } finally {
      setShiftSaving(false);
    }
  };

  const warehouseName = warehouses.find((w) => w.id === warehouseId)?.warehouseName;

  return (
    <Card title="Bán hàng (POS)" loading={draftLoading}>
      {editingDraftId && editingDraftNumber && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={`Đang sửa đơn nháp ${editingDraftNumber}`}
          description="Thêm/bớt sản phẩm rồi bấm Cập nhật nháp, hoặc Thanh toán để hoàn tất."
          action={
            <Button size="small" onClick={resetCartAndExitDraft}>
              Bỏ sửa
            </Button>
          }
        />
      )}
      {!openShift && warehouseId && canWrite && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Chưa mở ca cho kho này"
          description="Cần mở ca và nhập quỹ đầu ca trước khi thanh toán đơn bán."
          action={
            <Button size="small" type="primary" onClick={() => setOpenShiftModal(true)}>
              Mở ca
            </Button>
          }
        />
      )}
      {openShift && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={`Ca ${openShift.shiftNumber} · Quỹ đầu ${formatDisplayMoney(openShift.openingCash)}`}
          description={`Mở lúc ${dayjs(openShift.openedAt).format('DD-MM-YYYY HH:mm')} — Thu ròng ca: ${formatDisplayMoney(openShift.summary.netTotal)}`}
        />
      )}
      <Space wrap style={{ marginBottom: 16 }}>
        <Select
          style={{ width: 200 }}
          placeholder="Kho bán"
          value={warehouseId}
          disabled={!!editingDraftId}
          onChange={(id) => {
            if (editingDraftId) return;
            setWarehouseId(id);
            resetCart();
          }}
          options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          style={{ width: 220 }}
          placeholder="Khách hàng (tùy chọn)"
          value={customerId}
          onChange={setCustomerId}
          options={customers.map((c) => ({
            value: c.id,
            label: `${c.customerCode} — ${c.fullName}`,
          }))}
        />
        <Space direction="vertical" size={2}>
          <AutoComplete
            style={{ width: 320 }}
            placeholder="Quét mã vạch hoặc gõ mã / tên SP"
            value={barcode}
            options={productSearchOptions}
            onChange={setBarcode}
            onSelect={(value) => void addByBarcode(String(value))}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void addByBarcode();
            }}
            disabled={!canWrite || !warehouseId || cartLocked}
            notFoundContent="Không có sản phẩm phù hợp"
          />
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {showsBatchLabelField(batchMode)
              ? 'Quét SP hoặc quét nhãn lô sau khi thêm dòng · Demo SP: '
              : 'Demo: '}
            <Typography.Text code>8934567890012</Typography.Text>
            {showsBatchLabelField(batchMode) ? '' : ' (Paracetamol 500mg)'}
          </Typography.Text>
        </Space>
        <Button type="primary" onClick={() => void addByBarcode()} disabled={!canWrite || !warehouseId || cartLocked}>
          Thêm
        </Button>
      </Space>

      <Table rowKey="key" size="small" pagination={false} dataSource={cart} columns={columns} scroll={{ x: 880 }} />

      <div style={{ marginTop: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', gap: 16, flexWrap: 'wrap' }}>
        <PosSummaryPanel>
          <PosSummaryRow label="Tạm tính" value={formatDisplayMoney(pricing.subtotalGross)} />
          {pricing.lineDiscountTotal > 0 && (
            <PosSummaryRow
              label="Chiết khấu từng SP"
              value={`−${formatDisplayMoney(pricing.lineDiscountTotal)}`}
              danger
            />
          )}
          {canDiscount && (
            <PosSummaryOrderDiscountRow
              maxPercent={maxPercent}
              discountType={orderDiscount.discountType}
              discountValue={orderDiscount.discountValue}
              disabled={!canWrite || cart.length === 0}
              onTypeChange={(discountType) =>
                setOrderDiscount((prev) => ({
                  ...prev,
                  discountType,
                  discountValue: discountType ? prev.discountValue ?? 0 : undefined,
                }))
              }
              onValueChange={(discountValue) =>
                setOrderDiscount((prev) => ({ ...prev, discountValue }))
              }
            />
          )}
          {pricing.totalDiscountAmount > 0 && (
            <PosSummaryRow
              label="Tổng CK"
              value={`−${formatDisplayMoney(pricing.totalDiscountAmount)}`}
              danger
            />
          )}
          <PosSummaryDivider />
          <PosSummaryRow
            label="Khách phải trả"
            value={formatDisplayMoney(pricing.totalAmount)}
            strong
          />
        </PosSummaryPanel>
        <Space>
          <Button
            disabled={!canWrite || cart.length === 0 || draftLoading}
            loading={saving}
            onClick={() => void saveDraft()}
          >
            {editingDraftId ? 'Cập nhật nháp' : 'Lưu nháp'}
          </Button>
          <Button
            type="primary"
            size="large"
            disabled={!canWrite || cart.length === 0 || !openShift || cart.some((l) => l.quantity <= 0)}
            loading={checkoutValidating}
            onClick={() => void openCheckout()}
          >
            Thanh toán
          </Button>
        </Space>
      </div>

      <PosCheckoutModal
        open={checkoutOpen}
        loading={saving}
        totalAmount={pricing.totalAmount}
        subtotalGross={pricing.subtotalGross}
        lineDiscountTotal={pricing.lineDiscountTotal}
        orderDiscountAmount={pricing.orderDiscountAmount}
        onCancel={() => setCheckoutOpen(false)}
        onConfirm={(payments) => confirmCheckout(payments)}
      />

      {lastCompletedOrder && (
        <Alert
          style={{ marginTop: 16 }}
          type="success"
          showIcon
          closable
          onClose={() => setLastCompletedOrder(null)}
          message={`Đã hoàn tất ${lastCompletedOrder.orderNumber}`}
          description={
            <Space wrap>
              <Button
                icon={<PrinterOutlined />}
                onClick={() => void printSalesInvoice(lastCompletedOrder)}
              >
                In hóa đơn
              </Button>
              <Button type="link" onClick={() => navigate('/sales/orders')}>
                Xem danh sách đơn
              </Button>
            </Space>
          }
        />
      )}

      <OpenShiftModal
        open={openShiftModal}
        loading={shiftSaving}
        warehouseName={warehouseName}
        onCancel={() => setOpenShiftModal(false)}
        onConfirm={(cash) => handleOpenShift(cash)}
      />
    </Card>
  );
}
