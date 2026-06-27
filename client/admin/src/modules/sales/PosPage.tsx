import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
  Switch,
  Table,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, CreditCardOutlined, ClockCircleOutlined, PlusOutlined, PrinterOutlined, RollbackOutlined, SaveOutlined, SendOutlined, ShoppingCartOutlined, UnorderedListOutlined, UserAddOutlined } from '@ant-design/icons';
import { fetchWarehouses, fetchActiveCountingSession } from '@/shared/api/inventory.api';
import type { Warehouse, AdjustmentListItem } from '@/shared/api/inventory.types';
import { createSale, completeDraftSale, fetchBatchModeSettings, fetchOpenShift, fetchPosCustomerLoyalty, fetchPosCustomerVouchers, fetchPosStockBulk, fetchSalesOrder, lookupPosProduct, openSalesShift, previewPosAllocation, searchCustomers, searchPosProducts, updateDraftSale, type TenantBatchModeValue } from '@/shared/api/sales.api';
import {
  isShiftAlreadyOpenError,
  loadOpenShiftForWarehouse,
  shiftAlreadyOpenMessage,
} from '@/modules/sales/sales-shift-helpers';
import type { CartLine, CustomerListItem, PosCheckoutConfirm, PosCustomerLoyalty, PosCustomerVoucher, SalesOrderDetail, SalesShiftDetail } from '@/shared/api/sales.types';
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
import { buildCreateSalePayload, buildDraftCompletePayload, buildDraftUpdatePayload } from '@/modules/sales/pos-sale-payload';
import { OpenShiftModal } from '@/modules/sales/OpenShiftModal';
import { PosSummaryDivider, PosSummaryOrderDiscountRow, PosSummaryPanel, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { printSalesInvoice } from '@/modules/sales/sales-invoice-print';
import { formatPosCheckoutSuccessMessage } from '@/modules/sales/pos-checkout-message';
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
import {
  createCustomerDraftOrder,
  fetchCustomerDraftOrder,
  fetchCustomerDraftOrders,
  linkCustomerDraftOrderSale,
  loadCustomerDraftOrderForPos,
  sendCustomerDraftOrder,
  updateCustomerDraftOrder,
  CUSTOMER_DRAFT_ORDER_STATUS,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
} from '@/shared/api/customer-draft-orders.api';
import {
  loadCustomerDraftCartLines,
  orderDiscountFromCustomerDraft,
} from '@/modules/sales/customer-draft-order-helpers';
import {
  linkCustomerReservationSale,
  loadCustomerReservationForPos,
} from '@/shared/api/customer-reservations.api';
import { loadCustomerReservationCartLines } from '@/modules/sales/customer-reservation-helpers';
import { buildCustomerDraftOrderPayload } from '@/modules/sales/pos-customer-draft-payload';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { CustomerDraftOrderStatusBar } from '@/modules/sales/CustomerDraftOrderStatusBar';
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
  const [customerDraftOrders, setCustomerDraftOrders] = useState<CustomerDraftOrderListItem[]>([]);
  const [loadedCustomerDraftOrderId, setLoadedCustomerDraftOrderId] = useState<string>();
  const [loadedCustomerReservationId, setLoadedCustomerReservationId] = useState<string>();
  const [activeCustomerDraft, setActiveCustomerDraft] = useState<CustomerDraftOrder | null>(null);
  const [customerLoyalty, setCustomerLoyalty] = useState<PosCustomerLoyalty | null>(null);
  const [customerVouchers, setCustomerVouchers] = useState<PosCustomerVoucher[]>([]);
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
  const [customerAppDraftMode, setCustomerAppDraftMode] = useState(false);
  const [quickCustomerOpen, setQuickCustomerOpen] = useState(false);
  const pendingAutoCheckoutRef = useRef(false);
  const [autoCheckoutTick, setAutoCheckoutTick] = useState(0);
  const [activeCountSession, setActiveCountSession] = useState<AdjustmentListItem | null>(null);

  const pricing = useMemo(() => priceCart(cart, orderDiscount), [cart, orderDiscount]);

  useEffect(() => {
    if (!customerId || pricing.totalAmount <= 0) {
      setCustomerLoyalty(null);
      setCustomerVouchers([]);
      return;
    }
    let cancelled = false;
    void (async () => {
      const loyalty = await fetchPosCustomerLoyalty(customerId, pricing.totalAmount);
      if (!cancelled) setCustomerLoyalty(loyalty);
    })();
    return () => {
      cancelled = true;
    };
  }, [customerId, pricing.totalAmount]);

  useEffect(() => {
    if (!customerId) {
      setCustomerDraftOrders([]);
      setLoadedCustomerDraftOrderId(undefined);
      setActiveCustomerDraft(null);
      return;
    }
    void fetchCustomerDraftOrders(customerId, [
      CUSTOMER_DRAFT_ORDER_STATUS.Sent,
      CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
    ])
      .then(setCustomerDraftOrders)
      .catch(() => setCustomerDraftOrders([]));
  }, [customerId]);

  useEffect(() => {
    if (!loadedCustomerDraftOrderId) {
      setActiveCustomerDraft(null);
      return;
    }
    let cancelled = false;
    void fetchCustomerDraftOrder(loadedCustomerDraftOrderId)
      .then((draft) => {
        if (!cancelled) setActiveCustomerDraft(draft);
      })
      .catch(() => {
        if (!cancelled) setActiveCustomerDraft(null);
      });
    return () => {
      cancelled = true;
    };
  }, [loadedCustomerDraftOrderId]);

  useEffect(() => {
    if (!loadedCustomerDraftOrderId || activeCustomerDraft?.status !== CUSTOMER_DRAFT_ORDER_STATUS.Sent) {
      return;
    }
    const timer = window.setInterval(() => {
      void fetchCustomerDraftOrder(loadedCustomerDraftOrderId)
        .then((draft) => {
          setActiveCustomerDraft(draft);
          if (draft.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed) {
            setCustomerDraftOrders((prev) =>
              prev.map((item) =>
                item.id === draft.id
                  ? { ...item, status: draft.status, confirmedAt: draft.confirmedAt }
                  : item,
              ),
            );
          }
        })
        .catch(() => {});
    }, 8000);
    return () => window.clearInterval(timer);
  }, [loadedCustomerDraftOrderId, activeCustomerDraft?.status]);

  const loadCustomerDraftIntoPos = async (
    draftOrderId: string,
    options?: { autoCheckout?: boolean },
  ) => {
    try {
      const payload = await loadCustomerDraftOrderForPos(draftOrderId);
      setWarehouseId(payload.warehouseId);
      setCustomerId(payload.customerId);
      setCart(await loadCustomerDraftCartLines(payload));
      setOrderDiscount(orderDiscountFromCustomerDraft(payload));
      setLoadedCustomerDraftOrderId(payload.draftOrderId);
      setLoadedCustomerReservationId(undefined);
      setCustomerAppDraftMode(false);
      setEditingDraftId(null);
      setEditingDraftNumber(null);
      clearPosDraftEdit();
      if (options?.autoCheckout) {
        setOpenShift(await loadOpenShiftForWarehouse(payload.warehouseId));
        pendingAutoCheckoutRef.current = true;
        setAutoCheckoutTick((t) => t + 1);
        message.success(`Đã nạp ${payload.draftNumber} — mở thanh toán...`);
      } else {
        message.success(`Đã nạp ${payload.draftNumber} vào giỏ — bấm Thanh toán khi sẵn sàng`);
      }
    } catch (error) {
      pendingAutoCheckoutRef.current = false;
      message.error(apiErrorMessage(error, 'Không nạp được đơn tạm'));
    }
  };

  const loadCustomerReservationIntoPos = async (
    reservationId: string,
    options?: { autoCheckout?: boolean },
  ) => {
    try {
      const payload = await loadCustomerReservationForPos(reservationId);
      setWarehouseId(payload.warehouseId);
      setCustomerId(payload.customerId);
      setCart(await loadCustomerReservationCartLines(payload));
      setOrderDiscount({});
      setLoadedCustomerReservationId(payload.reservationId);
      setLoadedCustomerDraftOrderId(undefined);
      setActiveCustomerDraft(null);
      setCustomerAppDraftMode(false);
      setEditingDraftId(null);
      setEditingDraftNumber(null);
      clearPosDraftEdit();
      if (options?.autoCheckout) {
        setOpenShift(await loadOpenShiftForWarehouse(payload.warehouseId));
        pendingAutoCheckoutRef.current = true;
        setAutoCheckoutTick((t) => t + 1);
        message.success(`Đã nạp ${payload.reservationNumber} — mở thanh toán...`);
      } else {
        message.success(`Đã nạp ${payload.reservationNumber} vào giỏ — bấm Thanh toán khi sẵn sàng`);
      }
    } catch (error) {
      pendingAutoCheckoutRef.current = false;
      message.error(apiErrorMessage(error, 'Không nạp được đặt trước'));
    }
  };

  const resetCart = useCallback(() => {
    setCart([]);
    setOrderDiscount({});
    setLoadedCustomerDraftOrderId(undefined);
    setLoadedCustomerReservationId(undefined);
    setActiveCustomerDraft(null);
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
    if (!warehouseId) {
      setActiveCountSession(null);
      return;
    }
    let cancelled = false;
    void fetchActiveCountingSession(warehouseId)
      .then((session) => {
        if (!cancelled) setActiveCountSession(session);
      })
      .catch(() => {
        if (!cancelled) setActiveCountSession(null);
      });
    return () => {
      cancelled = true;
    };
  }, [warehouseId]);

  const handleQuickCustomerSaved = useCallback((customer: CustomerDetail) => {
    const listItem: CustomerListItem = {
      id: customer.id,
      customerCode: customer.customerCode,
      fullName: customer.fullName,
      phone: customer.phone,
    };
    setCustomers((prev) => {
      if (prev.some((row) => row.id === customer.id)) {
        return prev.map((row) => (row.id === customer.id ? listItem : row));
      }
      return [...prev, listItem].sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi'));
    });
    setCustomerId(customer.id);
    message.success(`Đã chọn ${customer.fullName} (${customer.customerCode})`);
  }, [message]);

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

  const deepLinkHandled = useRef<string | null>(null);

  useEffect(() => {
    const customerDraftId = searchParams.get('customerDraftId');
    if (!customerDraftId) {
      deepLinkHandled.current = null;
      return;
    }
    if (deepLinkHandled.current === customerDraftId) return;
    deepLinkHandled.current = customerDraftId;
    const autoCheckout = searchParams.get('checkout') === '1';
    void loadCustomerDraftIntoPos(customerDraftId, { autoCheckout }).finally(() => {
      deepLinkHandled.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [searchParams, setSearchParams]);

  const reservationDeepLinkHandled = useRef<string | null>(null);

  useEffect(() => {
    const customerReservationId = searchParams.get('customerReservationId');
    if (!customerReservationId) {
      reservationDeepLinkHandled.current = null;
      return;
    }
    if (reservationDeepLinkHandled.current === customerReservationId) return;
    reservationDeepLinkHandled.current = customerReservationId;
    const autoCheckout = searchParams.get('checkout') === '1';
    void loadCustomerReservationIntoPos(customerReservationId, { autoCheckout }).finally(() => {
      reservationDeepLinkHandled.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [searchParams, setSearchParams]);

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

  const saveCustomerDraftTemp = async (): Promise<CustomerDraftOrder> => {
    if (!customerId) throw new Error('missing-customer');
    const payload = buildCustomerDraftOrderPayload(customerId, warehouseId, cart, orderDiscount);
    if (loadedCustomerDraftOrderId) {
      return updateCustomerDraftOrder(loadedCustomerDraftOrderId, payload);
    }
    const created = await createCustomerDraftOrder(payload);
    setLoadedCustomerDraftOrderId(created.id);
    return created;
  };

  const refreshCustomerDraftList = () => {
    if (!customerId) return;
    void fetchCustomerDraftOrders(customerId, [
      CUSTOMER_DRAFT_ORDER_STATUS.Sent,
      CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
    ])
      .then(setCustomerDraftOrders)
      .catch(() => setCustomerDraftOrders([]));
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
    const useCustomerDraft = Boolean(customerAppDraftMode && customerId && !editingDraftId);
    setSaving(true);
    const hideLoading = message.loading(
      updatingExisting ? 'Đang cập nhật đơn tạm...' : 'Đang lưu đơn tạm...',
      0,
    );
    try {
      if (useCustomerDraft) {
        const order = await saveCustomerDraftTemp();
        hideLoading();
        message.success(`Đã lưu tạm ${order.draftNumber} — ${formatDisplayMoney(order.totalAmount)}`);
        setActiveCustomerDraft(order);
        refreshCustomerDraftList();
        return;
      }
      if (editingDraftId) {
        const order = await updateDraftSale(
          editingDraftId,
          buildDraftUpdatePayload(customerId, cart, orderDiscount),
        );
        hideLoading();
        message.success(
          `Đã cập nhật tạm ${order.orderNumber} — ${formatDisplayMoney(order.totalAmount)}`,
        );
        clearDraftEdit();
        navigate(`/sales/orders?orderId=${order.id}`);
      } else {
        const order = await createSale(
          buildCreateSalePayload(warehouseId, customerId, cart, orderDiscount, true),
        );
        hideLoading();
        clearPosDraftEdit();
        message.success(`Đã lưu tạm ${order.orderNumber}`);
        resetCart();
        navigate(`/sales/orders?orderId=${order.id}`);
      }
    } catch (error) {
      hideLoading();
      message.error(apiErrorMessage(error, updatingExisting ? 'Không cập nhật được đơn tạm' : 'Không lưu được đơn tạm'));
    } finally {
      setSaving(false);
    }
  };

  const sendCustomerDraftToApp = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho bán trước');
      return;
    }
    if (!customerId) {
      message.warning('Chọn khách hàng trước khi gửi app');
      return;
    }
    if (cart.length === 0) {
      message.warning('Thêm ít nhất một sản phẩm');
      return;
    }
    if (!validateDiscounts()) return;
    if (!validateBatchLabels()) return;
    setSaving(true);
    const hideLoading = message.loading('Đang gửi đơn tạm lên app khách...', 0);
    try {
      let order = await saveCustomerDraftTemp();
      if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Draft) {
        order = await sendCustomerDraftOrder(order.id);
        hideLoading();
        message.success(`Đã gửi ${order.draftNumber} lên app khách`);
      } else if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent) {
        hideLoading();
        message.info(`${order.draftNumber} đã gửi app — chờ khách xác nhận tạm`);
      } else if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed) {
        hideLoading();
        message.success(`${order.draftNumber} — khách đã xác nhận tạm trên app`);
      } else {
        hideLoading();
        message.success(`Đã cập nhật ${order.draftNumber}`);
      }
      setActiveCustomerDraft(order);
      setLoadedCustomerDraftOrderId(order.id);
      refreshCustomerDraftList();
    } catch (error) {
      hideLoading();
      message.error(apiErrorMessage(error, 'Không gửi được đơn tạm'));
    } finally {
      setSaving(false);
    }
  };

  const confirmCheckout = async ({ payments, loyaltyDiscountAmount, customerVoucherId }: PosCheckoutConfirm) => {
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
        order = await completeDraftSale(editingDraftId, {
          payments,
          ...buildDraftCompletePayload(customerId, cart, orderDiscount, undefined, loyaltyDiscountAmount, customerVoucherId),
        });
      } else {
        order = await createSale(
          buildCreateSalePayload(
            warehouseId,
            customerId,
            cart,
            orderDiscount,
            false,
            payments,
            loyaltyDiscountAmount,
            customerVoucherId,
          ),
        );
      }
      hideLoading();
      message.success(formatPosCheckoutSuccessMessage(order));
      if (loadedCustomerDraftOrderId) {
        try {
          await linkCustomerDraftOrderSale(loadedCustomerDraftOrderId, order.id);
        } catch {
          message.warning('Đã bán nhưng chưa liên kết được đơn tạm — liên hệ IT nếu cần.');
        }
      }
      if (loadedCustomerReservationId) {
        try {
          await linkCustomerReservationSale(loadedCustomerReservationId, order.id);
        } catch {
          message.warning('Đã bán nhưng chưa liên kết được đặt trước — liên hệ IT nếu cần.');
        }
      }
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
      if (customerId && pricing.totalAmount > 0) {
        const [loyalty, vouchers] = await Promise.all([
          fetchPosCustomerLoyalty(customerId, pricing.totalAmount),
          fetchPosCustomerVouchers(customerId, pricing.totalAmount),
        ]);
        setCustomerLoyalty(loyalty);
        setCustomerVouchers(vouchers);
      } else {
        setCustomerLoyalty(null);
        setCustomerVouchers([]);
      }
      setCheckoutOpen(true);
    } finally {
      setCheckoutValidating(false);
    }
  }, [
    customerId,
    openShift,
    pricing.totalAmount,
    validateDiscounts,
    validateBatchLabels,
    validateFefoAllocation,
    validateStock,
  ]);

  useEffect(() => {
    if (!pendingAutoCheckoutRef.current) return;
    if (draftLoading || cart.length === 0 || !warehouseId) return;
    pendingAutoCheckoutRef.current = false;
    void openCheckout();
  }, [autoCheckoutTick, cart.length, warehouseId, draftLoading, openCheckout]);

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
          message={`Đang sửa đơn tạm ${editingDraftNumber}`}
          description="Thêm/bớt sản phẩm rồi bấm Cập nhật tạm, hoặc Thanh toán để hoàn tất."
          action={
            <Button size="small" icon={<RollbackOutlined />} onClick={resetCartAndExitDraft}>
              Bỏ sửa
            </Button>
          }
        />
      )}
      {activeCountSession && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={`Kho ${warehouseName ?? ''} đang kiểm kê (${activeCountSession.adjustmentNumber})`}
          description="Theo quy trình chuẩn: hạn chế bán/nhập xuất trong lúc đếm để tránh lệch tồn. Hoàn tất kiểm kê trước khi chốt số."
          action={
            <Button
              size="small"
              onClick={() => navigate(`/inventory/adjustments/${activeCountSession.id}/count`)}
            >
              Màn kiểm kê
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
            <Button size="small" type="primary" icon={<ClockCircleOutlined />} onClick={() => setOpenShiftModal(true)}>
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
      {customerDraftOrders.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Khách có đơn tạm chờ lấy"
          description={
            <Space direction="vertical" size={4}>
              {customerDraftOrders.map((draft) => (
                <Space key={draft.id} wrap>
                  <span>
                    {draft.draftNumber} · {draft.totalAmount.toLocaleString('vi-VN')}đ
                    {draft.confirmedAt ? ' · Khách đã xác nhận tạm' : ''}
                  </span>
                  <Button
                    size="small"
                    type="primary"
                    icon={<ShoppingCartOutlined />}
                    onClick={() => void loadCustomerDraftIntoPos(draft.id, { autoCheckout: true })}
                  >
                    Nạp POS
                  </Button>
                </Space>
              ))}
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Hỏi khách xác nhận lại tại quầy. Bấm Nạp POS để mở thanh toán và in hóa đơn.
              </Typography.Text>
            </Space>
          }
        />
      ) : null}
      {customerAppDraftMode && !editingDraftId ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="Chế độ gửi đơn qua app khách"
          description="Soạn giỏ → bấm Gửi khách hàng. Khách xem/xác nhận trên app; thanh toán tại quầy khi khách đến lấy."
        />
      ) : null}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 16, alignItems: 'flex-start' }}>
        <Select
          style={{ width: 180, flex: '0 0 auto' }}
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
        <Space.Compact block style={{ flex: '1 1 340px', minWidth: 320, maxWidth: 520 }}>
          <Select
            allowClear={!customerAppDraftMode}
            showSearch
            optionFilterProp="label"
            style={{ width: 'calc(100% - 32px)' }}
            placeholder={customerAppDraftMode ? 'Chọn khách hàng' : 'Khách hàng (tùy chọn)'}
            value={customerId}
            onChange={setCustomerId}
            options={customers.map((c) => ({
              value: c.id,
              label: `${c.customerCode} — ${c.fullName}`,
            }))}
          />
          {canWrite && !editingDraftId ? (
            <Tooltip title="Thêm khách nhanh">
              <Button icon={<UserAddOutlined />} onClick={() => setQuickCustomerOpen(true)} />
            </Tooltip>
          ) : null}
        </Space.Compact>
        {!editingDraftId ? (
          <Space align="center" size={8} style={{ flex: '0 0 auto' }}>
            <Switch
              checked={customerAppDraftMode}
              disabled={!canWrite}
              onChange={(checked) => {
                setCustomerAppDraftMode(checked);
                if (checked && !customerId) {
                  message.info('Chọn khách hàng để gửi đơn qua app');
                }
              }}
            />
            <Typography.Text>Gửi đơn qua app khách</Typography.Text>
          </Space>
        ) : null}
        <div style={{ display: 'flex', flex: '1 1 420px', gap: 8, minWidth: 320, alignItems: 'flex-start' }}>
          <Space direction="vertical" size={2} style={{ flex: 1, minWidth: 0 }}>
            <AutoComplete
              style={{ width: '100%' }}
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
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => void addByBarcode()}
            disabled={!canWrite || !warehouseId || cartLocked}
          >
            Thêm
          </Button>
        </div>
      </div>

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
        <Space direction="vertical" align="end">
          {customerAppDraftMode && activeCustomerDraft && customerId && !editingDraftId ? (
            <CustomerDraftOrderStatusBar draft={activeCustomerDraft} />
          ) : null}
          <Space>
            {customerAppDraftMode && !editingDraftId ? (
              <Tooltip title={!customerId ? 'Chọn khách hàng trước khi gửi app' : undefined}>
                <span>
                  <Button
                    type="primary"
                    icon={<SendOutlined />}
                    disabled={!canWrite || !customerId || cart.length === 0 || draftLoading}
                    loading={saving}
                    onClick={() => void sendCustomerDraftToApp()}
                  >
                    Gửi khách hàng
                  </Button>
                </span>
              </Tooltip>
            ) : (
              <Button
                icon={<SaveOutlined />}
                disabled={!canWrite || cart.length === 0 || draftLoading}
                loading={saving}
                onClick={() => void saveDraft()}
              >
                {editingDraftId ? 'Cập nhật nháp' : 'Lưu nháp'}
              </Button>
            )}
            <Button
              type={customerAppDraftMode && !editingDraftId ? 'default' : 'primary'}
              size="large"
              icon={<CreditCardOutlined />}
              disabled={!canWrite || cart.length === 0 || !openShift || cart.some((l) => l.quantity <= 0)}
              loading={checkoutValidating}
              onClick={() => void openCheckout()}
            >
              Thanh toán
            </Button>
          </Space>
        </Space>
      </div>

      <PosCheckoutModal
        open={checkoutOpen}
        loading={saving}
        totalAmount={pricing.totalAmount}
        subtotalGross={pricing.subtotalGross}
        lineDiscountTotal={pricing.lineDiscountTotal}
        orderDiscountAmount={pricing.orderDiscountAmount}
        customerLoyalty={customerLoyalty}
        customerVouchers={customerVouchers}
        onCancel={() => setCheckoutOpen(false)}
        onConfirm={(result) => confirmCheckout(result)}
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
              <Button type="link" icon={<UnorderedListOutlined />} onClick={() => navigate('/sales/orders')}>
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

      <CustomerFormDrawer
        open={quickCustomerOpen}
        editing={null}
        variant="quick"
        onClose={() => setQuickCustomerOpen(false)}
        onSaved={handleQuickCustomerSaved}
      />
    </Card>
  );
}
