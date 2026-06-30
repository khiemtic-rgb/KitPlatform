import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import dayjs from 'dayjs';
import {
  Alert,
  App,
  AutoComplete,
  Button,
  InputNumber,
  Select,
  Space,
  Spin,
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
import { useTranslation } from 'react-i18next';

export function PosPage() {
  const { t } = useTranslation('sales');
  const { t: tc } = useTranslation('common');
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
        message.success(t('pos.messages.draftLoadedCheckout', { number: payload.draftNumber }));
      } else {
        message.success(t('pos.messages.draftLoadedCart', { number: payload.draftNumber }));
      }
    } catch (error) {
      pendingAutoCheckoutRef.current = false;
      message.error(apiErrorMessage(error, t('pos.messages.loadDraftFailed')));
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
        message.success(t('pos.messages.reservationLoadedCheckout', { number: payload.reservationNumber }));
      } else {
        message.success(t('pos.messages.reservationLoadedCart', { number: payload.reservationNumber }));
      }
    } catch (error) {
      pendingAutoCheckoutRef.current = false;
      message.error(apiErrorMessage(error, t('pos.messages.loadReservationFailed')));
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
      message.error(apiErrorMessage(error, t('pos.messages.loadShiftFailed')));
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
      allowCredit: customer.allowCredit,
      creditLimit: customer.creditLimit ?? undefined,
    };
    setCustomers((prev) => {
      if (prev.some((row) => row.id === customer.id)) {
        return prev.map((row) => (row.id === customer.id ? listItem : row));
      }
      return [...prev, listItem].sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi'));
    });
    setCustomerId(customer.id);
    message.success(t('pos.messages.customerSelected', { name: customer.fullName, code: customer.customerCode }));
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
              label: t('pos.toolbar.searchOptionLabel', {
                code: p.productCode,
                name: p.productName,
                stock: p.stockAvailable.toLocaleString(),
                unit: p.unitName,
              }),
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
  }, [barcode, warehouseId, t]);

  const loadDraftFromUrl = useCallback(
    async (draftId: string) => {
      setDraftLoading(true);
      try {
        const order = await fetchSalesOrder(draftId);
        if (order.status !== 1) {
          message.warning(t('pos.messages.draftNotDraft'));
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
        message.error(apiErrorMessage(error, t('pos.messages.loadDraftOrderFailed')));
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
        message.error(t('pos.messages.noDiscountPermission'));
        return false;
      }
      return true;
    }

    const policyError = validateCartDiscountPolicy(cart, orderDiscount, maxPercent, unlimited, t);
    if (policyError) {
      message.error(policyError);
      return false;
    }

    return true;
  }, [canDiscount, cart, maxPercent, orderDiscount, unlimited, message, t]);

  const validateBatchLabels = useCallback((): boolean => {
    const error = validateCartBatchLabels(cart, batchMode, t);
    if (error) {
      message.warning(error);
      return false;
    }
    return true;
  }, [batchMode, cart, message, t]);

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
      message.error(apiErrorMessage(error, t('pos.messages.fefoFailed')));
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
          message.warning(t('pos.messages.outOfStock'));
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
            message.success(t('pos.messages.batchAssigned', { batch: batchResult.batchNumber, name: batchResult.productName }));
            setBarcode('');
            return;
          }
        }
        message.error(apiErrorMessage(error, t('pos.messages.productNotFound')));
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
      message.warning(t('pos.messages.selectWarehouseFirst'));
      return;
    }
    if (cart.length === 0) {
      message.warning(t('pos.messages.addProductFirst'));
      return;
    }
    if (!validateDiscounts()) return;
    if (!validateBatchLabels()) return;
    const updatingExisting = Boolean(editingDraftId);
    const useCustomerDraft = Boolean(customerAppDraftMode && customerId && !editingDraftId);
    setSaving(true);
    const hideLoading = message.loading(
      updatingExisting ? t('pos.messages.updatingDraft') : t('pos.messages.savingDraft'),
      0,
    );
    try {
      if (useCustomerDraft) {
        const order = await saveCustomerDraftTemp();
        hideLoading();
        message.success(t('pos.messages.draftSaved', { number: order.draftNumber, amount: formatDisplayMoney(order.totalAmount) }));
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
          t('pos.messages.draftTempUpdated', {
            number: order.orderNumber,
            amount: formatDisplayMoney(order.totalAmount),
          }),
        );
        clearDraftEdit();
        navigate(`/sales/orders?orderId=${order.id}`);
      } else {
        const order = await createSale(
          buildCreateSalePayload(warehouseId, customerId, cart, orderDiscount, true),
        );
        hideLoading();
        clearPosDraftEdit();
        message.success(t('pos.messages.draftSavedOrder', { number: order.orderNumber }));
        resetCart();
        navigate(`/sales/orders?orderId=${order.id}`);
      }
    } catch (error) {
      hideLoading();
      message.error(apiErrorMessage(error, updatingExisting ? t('pos.messages.updateDraftFailed') : t('pos.messages.saveDraftFailed')));
    } finally {
      setSaving(false);
    }
  };

  const sendCustomerDraftToApp = async () => {
    if (!warehouseId) {
      message.warning(t('pos.messages.selectWarehouseFirst'));
      return;
    }
    if (!customerId) {
      message.warning(t('pos.messages.selectCustomerForApp'));
      return;
    }
    if (cart.length === 0) {
      message.warning(t('pos.messages.addProductFirst'));
      return;
    }
    if (!validateDiscounts()) return;
    if (!validateBatchLabels()) return;
    setSaving(true);
    const hideLoading = message.loading(t('pos.messages.sendingDraft'), 0);
    try {
      let order = await saveCustomerDraftTemp();
      if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Draft) {
        order = await sendCustomerDraftOrder(order.id);
        hideLoading();
        message.success(t('pos.messages.draftSent', { number: order.draftNumber }));
      } else if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent) {
        hideLoading();
        message.info(t('pos.messages.draftSentWaiting', { number: order.draftNumber }));
      } else if (order.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed) {
        hideLoading();
        message.success(t('pos.messages.draftConfirmed', { number: order.draftNumber }));
      } else {
        hideLoading();
        message.success(t('pos.messages.draftUpdated', { number: order.draftNumber }));
      }
      setActiveCustomerDraft(order);
      setLoadedCustomerDraftOrderId(order.id);
      refreshCustomerDraftList();
    } catch (error) {
      hideLoading();
      message.error(apiErrorMessage(error, t('pos.messages.sendDraftFailed')));
    } finally {
      setSaving(false);
    }
  };

  const confirmCheckout = async ({ payments, loyaltyDiscountAmount, customerVoucherId }: PosCheckoutConfirm) => {
    if (!warehouseId) {
      message.warning(t('pos.messages.selectWarehouseFirst'));
      throw new Error('missing-warehouse');
    }
    if (cart.length === 0) {
      message.warning(t('pos.messages.cartEmpty'));
      throw new Error('empty-cart');
    }
    if (!validateDiscounts()) {
      throw new Error('invalid-discount');
    }
    if (!validateBatchLabels()) {
      throw new Error('invalid-batch-label');
    }
    setSaving(true);
    const hideLoading = message.loading(t('pos.messages.creatingSale'), 0);
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
          message.warning(t('pos.messages.linkDraftFailed'));
        }
      }
      if (loadedCustomerReservationId) {
        try {
          await linkCustomerReservationSale(loadedCustomerReservationId, order.id);
        } catch {
          message.warning(t('pos.messages.linkReservationFailed'));
        }
      }
      setCheckoutOpen(false);
      clearDraftEdit();
      resetCart();
      void printSalesInvoice(order).then((printed) => {
        if (!printed) {
          setLastCompletedOrder(order);
          message.warning(t('pos.messages.printBlocked'));
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
      message.warning(t('pos.messages.openShiftBeforeCheckout'));
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

  const columns: ColumnsType<CartLine> = useMemo(() => [
    {
      title: t('pos.columns.product'),
      ellipsis: true,
      render: (_, row) => {
        const suggestedBatch = formatSuggestedBatch(row.batchHints);
        const stockWarning =
          row.qtyWarning ??
          (row.stockAvailable != null && row.stockAvailable <= 0
            ? outOfStockWarningText(row.unitName, t)
            : null);
        const body = (
          <div style={{ lineHeight: 1.45, minWidth: 0 }}>
            <Typography.Text type="secondary" style={{ fontSize: 11, lineHeight: 1.35 }}>
              {t('pos.columns.productCode')}: {row.productCode}
            </Typography.Text>
            <div>
              <Typography.Text strong ellipsis={{ tooltip: row.productName }}>
                {row.productName}
              </Typography.Text>
              <Typography.Text
                style={{ marginLeft: 6, fontSize: '0.88em', color: '#0d7377', fontWeight: 500 }}
              >
                {t('pos.columns.stockInline', { count: (row.stockAvailable ?? 0).toLocaleString() })}
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
          <Tooltip title={t('pos.columns.fefoHint', { batch: suggestedBatch })}>{body}</Tooltip>
        ) : (
          body
        );
      },
    },
    {
      title: t('pos.columns.unit'),
      dataIndex: 'unitName',
      width: 52,
      align: 'center',
      className: 'pos-cart-col-uom',
      render: (value: string) => (
        <Typography.Text style={{ fontSize: 12 }}>{value}</Typography.Text>
      ),
    },
    ...(showsBatchLabelField(batchMode)
      ? ([
          {
            title: t('pos.columns.batch'),
            width: 128,
            render: (_, row) => (
              <Select
                showSearch
                allowClear={batchMode !== 'label_required'}
                placeholder={t('pos.columns.batchPlaceholder')}
                style={{ width: 120 }}
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
      title: t('pos.columns.qty'),
      dataIndex: 'quantity',
      width: 80,
      align: 'right',
      className: 'pos-cart-col-qty',
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
      title: t('pos.columns.unitPrice'),
      dataIndex: 'unitPrice',
      width: 92,
      align: 'right',
      className: 'pos-cart-col-money',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    ...(canDiscount
      ? ([
          {
            title: t('pos.columns.discount'),
            width: 112,
            className: 'pos-cart-col-discount',
            render: (_, row) => (
              <Space.Compact>
                <Select
                  allowClear
                  placeholder="%"
                  style={{ width: 48 }}
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
                  options={[
                    { value: SALES_DISCOUNT_TYPES.Percent, label: '%' },
                    { value: SALES_DISCOUNT_TYPES.Fixed, label: t('enums.discountType.fixed') },
                  ]}
                />
                <InputNumber
                  disabled={!canWrite || cartLocked || !row.discountType}
                  value={row.discountValue}
                  {...(row.discountType === SALES_DISCOUNT_TYPES.Fixed
                    ? { ...moneyInputNumberPropsAllowZeroSuffix, style: { ...moneyInputNumberStyle, width: 64 } }
                    : { min: 0, max: 100, style: { ...moneyInputNumberStyle, width: 52 } })}
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
      title: t('pos.columns.lineTotal'),
      width: 100,
      align: 'right',
      className: 'pos-cart-col-money',
      render: (_, row) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(lineNet(row))}</span>
      ),
    },
    {
      title: '',
      width: 36,
      align: 'center',
      render: (_, row) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          disabled={!canWrite || cartLocked}
          aria-label={t('pos.columns.removeLine')}
          onClick={() => setCart((p) => p.filter((l) => l.key !== row.key))}
        />
      ),
    },
  ], [t, canWrite, cartLocked, batchMode, canDiscount]);

  const handleOpenShift = async (openingCash: number) => {
    if (!warehouseId) {
      message.warning(t('pos.messages.selectWarehouseForShift'));
      throw new Error('missing warehouse');
    }
    setShiftSaving(true);
    try {
      const shift = await openSalesShift({ warehouseId, openingCash });
      setOpenShift(shift);
      setOpenShiftModal(false);
      message.success(t('pos.messages.shiftOpened', { number: shift.shiftNumber }));
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
      message.error(apiErrorMessage(error, t('pos.messages.openShiftFailed')));
      throw error;
    } finally {
      setShiftSaving(false);
    }
  };

  const warehouseName = warehouses.find((w) => w.id === warehouseId)?.warehouseName;

  return (
    <Spin spinning={draftLoading} wrapperClassName="pos-page-spin">
      <div className="pos-page">
      <div className="pos-page__alerts">
      {editingDraftId && editingDraftNumber && (
        <Alert
          type="info"
          showIcon
          message={t('pos.alerts.editingDraft', { number: editingDraftNumber })}
          description={t('pos.alerts.editingDraftDesc')}
          action={
            <Button size="small" icon={<RollbackOutlined />} onClick={resetCartAndExitDraft}>
              {t('pos.alerts.discardEdit')}
            </Button>
          }
        />
      )}
      {activeCountSession && (
        <Alert
          type="warning"
          showIcon
          message={t('pos.alerts.countingTitle', {
            warehouse: warehouseName ?? '',
            number: activeCountSession.adjustmentNumber,
          })}
          description={t('pos.alerts.countingDesc')}
          action={
            <Button
              size="small"
              onClick={() => navigate(`/inventory/adjustments/${activeCountSession.id}/count`)}
            >
              {t('pos.alerts.countingAction')}
            </Button>
          }
        />
      )}
      {!openShift && warehouseId && canWrite && (
        <Alert
          type="warning"
          showIcon
          message={t('pos.alerts.shiftNotOpenTitle')}
          description={t('pos.alerts.shiftNotOpenDesc')}
          action={
            <Button size="small" type="primary" icon={<ClockCircleOutlined />} onClick={() => setOpenShiftModal(true)}>
              {t('pos.alerts.openShift')}
            </Button>
          }
        />
      )}
      {openShift && (
        <Alert
          type="info"
          showIcon
          message={t('pos.alerts.shiftOpen', {
            number: openShift.shiftNumber,
            opening: formatDisplayMoney(openShift.openingCash),
          })}
          description={t('pos.alerts.shiftOpenDesc', {
            openedAt: dayjs(openShift.openedAt).format('DD-MM-YYYY HH:mm'),
            net: formatDisplayMoney(openShift.summary.netTotal),
          })}
        />
      )}
      {customerDraftOrders.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          message={t('pos.alerts.pendingDraftsTitle')}
          description={
            <Space direction="vertical" size={4}>
              {customerDraftOrders.map((draft) => (
                <Space key={draft.id} wrap>
                  <span>
                    {draft.draftNumber} · {draft.totalAmount.toLocaleString()}đ
                    {draft.confirmedAt ? t('pos.alerts.pendingDraftConfirmed') : ''}
                  </span>
                  <Button
                    size="small"
                    type="primary"
                    icon={<ShoppingCartOutlined />}
                    onClick={() => void loadCustomerDraftIntoPos(draft.id, { autoCheckout: true })}
                  >
                    {t('pos.alerts.loadIntoPos')}
                  </Button>
                </Space>
              ))}
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('pos.alerts.pendingDraftsHint')}
              </Typography.Text>
            </Space>
          }
        />
      ) : null}
      {customerAppDraftMode && !editingDraftId ? (
        <Alert
          type="info"
          showIcon
          message={t('pos.alerts.appDraftModeTitle')}
          description={t('pos.alerts.appDraftModeDesc')}
        />
      ) : null}
      </div>
      <div className="pos-page__toolbar">
        <div className="pos-page__toolbar-cart">
          <div className="pos-page__toolbar-row">
            <Select
              className="pos-page__warehouse-select"
              placeholder={t('pos.toolbar.warehouse')}
              value={warehouseId}
              disabled={!!editingDraftId}
              onChange={(id) => {
                if (editingDraftId) return;
                setWarehouseId(id);
                resetCart();
              }}
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
            />
            <Space.Compact block className="pos-page__customer-select">
              <Select
                allowClear={!customerAppDraftMode}
                showSearch
                optionFilterProp="label"
                style={{ width: 'calc(100% - 32px)' }}
                placeholder={customerAppDraftMode ? t('pos.toolbar.customerRequired') : t('pos.toolbar.customerOptional')}
                value={customerId}
                onChange={setCustomerId}
                options={customers.map((c) => ({
                  value: c.id,
                  label: `${c.customerCode} — ${c.fullName}`,
                }))}
              />
              {canWrite && !editingDraftId ? (
                <Tooltip title={t('pos.toolbar.quickAddCustomer')}>
                  <Button icon={<UserAddOutlined />} onClick={() => setQuickCustomerOpen(true)} />
                </Tooltip>
              ) : null}
            </Space.Compact>
            {!editingDraftId ? (
              <Space align="center" size={8} className="pos-page__app-draft-toggle">
                <Switch
                  checked={customerAppDraftMode}
                  disabled={!canWrite}
                  onChange={(checked) => {
                    setCustomerAppDraftMode(checked);
                    if (checked && !customerId) {
                      message.info(t('pos.toolbar.selectCustomerForApp'));
                    }
                  }}
                />
                <Typography.Text className="pos-page__app-draft-toggle-label">
                  {t('pos.toolbar.appDraftMode')}
                </Typography.Text>
              </Space>
            ) : null}
          </div>
          <div className="pos-page__toolbar-row pos-page__toolbar-row--scan">
            <AutoComplete
              className="pos-page__barcode-field"
              placeholder={t('pos.toolbar.scanPlaceholder')}
              value={barcode}
              options={productSearchOptions}
              onChange={setBarcode}
              onSelect={(value) => void addByBarcode(String(value))}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void addByBarcode();
              }}
              disabled={!canWrite || !warehouseId || cartLocked}
              notFoundContent={t('pos.toolbar.noProducts')}
            />
            <Button
              className="pos-page__add-btn"
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => void addByBarcode()}
              disabled={!canWrite || !warehouseId || cartLocked}
            >
              {tc('actions.add')}
            </Button>
          </div>
        </div>
        <div className="pos-page__toolbar-aside">
          <div className="pos-page__toolbar-actions">
            <Button
              block
              size="large"
              className="pos-page__pay-btn"
              type={customerAppDraftMode && !editingDraftId ? 'default' : 'primary'}
              icon={<CreditCardOutlined />}
              disabled={!canWrite || cart.length === 0 || !openShift || cart.some((l) => l.quantity <= 0)}
              loading={checkoutValidating}
              onClick={() => void openCheckout()}
            >
              {t('pos.toolbar.checkout')}
            </Button>
            {customerAppDraftMode && !editingDraftId ? (
              <Tooltip title={!customerId ? t('pos.toolbar.selectCustomerBeforeSend') : undefined}>
                <span className="pos-page__toolbar-action-slot">
                  <Button
                    block
                    className="pos-page__draft-btn"
                    type="primary"
                    icon={<SendOutlined />}
                    disabled={!canWrite || !customerId || cart.length === 0 || draftLoading}
                    loading={saving}
                    onClick={() => void sendCustomerDraftToApp()}
                  >
                    {t('pos.toolbar.sendCustomer')}
                  </Button>
                </span>
              </Tooltip>
            ) : (
              <Button
                block
                className="pos-page__draft-btn"
                icon={<SaveOutlined />}
                disabled={!canWrite || cart.length === 0 || draftLoading}
                loading={saving}
                onClick={() => void saveDraft()}
              >
                {editingDraftId ? t('pos.toolbar.updateDraft') : t('pos.toolbar.saveDraft')}
              </Button>
            )}
          </div>
        </div>
      </div>

      <div className="pos-page__body">
        <div className="pos-page__cart">
          <Table
            rowKey="key"
            size="small"
            pagination={false}
            dataSource={cart}
            columns={columns}
            scroll={{ y: 'calc(100vh - 210px)' }}
            tableLayout="fixed"
            className="pos-cart-table"
          />
        </div>

        <aside className="pos-page__aside">
          {customerAppDraftMode && activeCustomerDraft && customerId && !editingDraftId ? (
            <CustomerDraftOrderStatusBar draft={activeCustomerDraft} />
          ) : null}

          <div className="pos-page__aside-panel">
            <div className="pos-page__aside-summary-box">
              <PosSummaryPanel variant="sidebar">
                <PosSummaryRow label={t('pos.summary.subtotal')} value={formatDisplayMoney(pricing.subtotalGross)} />
                {pricing.lineDiscountTotal > 0 && (
                  <PosSummaryRow
                    label={t('pos.summary.lineDiscount')}
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
                    label={t('pos.summary.totalDiscount')}
                    value={`−${formatDisplayMoney(pricing.totalDiscountAmount)}`}
                    danger
                  />
                )}
                <PosSummaryDivider />
                <PosSummaryRow
                  label={t('pos.summary.payable')}
                  value={formatDisplayMoney(pricing.totalAmount)}
                  total
                />
              </PosSummaryPanel>
            </div>
          </div>
        </aside>
      </div>

      <PosCheckoutModal
        open={checkoutOpen}
        loading={saving}
        totalAmount={pricing.totalAmount}
        subtotalGross={pricing.subtotalGross}
        lineDiscountTotal={pricing.lineDiscountTotal}
        orderDiscountAmount={pricing.orderDiscountAmount}
        customerId={customerId}
        customers={customers}
        onCustomerChange={setCustomerId}
        onQuickAddCustomer={canWrite && !editingDraftId ? () => setQuickCustomerOpen(true) : undefined}
        customerAllowCredit={customers.find((c) => c.id === customerId)?.allowCredit}
        customerCreditLimit={customers.find((c) => c.id === customerId)?.creditLimit}
        customerCurrentOutstanding={customers.find((c) => c.id === customerId)?.currentOutstanding}
        customerLoyalty={customerLoyalty}
        customerVouchers={customerVouchers}
        onCancel={() => setCheckoutOpen(false)}
        onConfirm={(result) => confirmCheckout(result)}
      />

      {lastCompletedOrder && (
        <Alert
          style={{ marginTop: 12 }}
          type="success"
          showIcon
          closable
          onClose={() => setLastCompletedOrder(null)}
          message={t('pos.alerts.completedTitle', { orderNumber: lastCompletedOrder.orderNumber })}
          description={
            <Space wrap>
              <Button
                icon={<PrinterOutlined />}
                onClick={() => void printSalesInvoice(lastCompletedOrder)}
              >
                {t('pos.alerts.printInvoice')}
              </Button>
              <Button type="link" icon={<UnorderedListOutlined />} onClick={() => navigate('/sales/orders')}>
                {t('pos.alerts.viewOrders')}
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
      </div>
    </Spin>
  );
}
