import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { App, Alert, Button, Drawer, Input, InputNumber, Select, Space, Typography } from 'antd';
import type { InputRef } from 'antd';
import {
  HomeOutlined,
  MinusOutlined,
  PlusOutlined,
  SaveOutlined,
  ScanOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import dayjs from 'dayjs';
import {
  createSale,
  fetchBatchModeSettings,
  fetchOpenShift,
  fetchPrescriptionPosLoad,
  fetchRxSettings,
  fetchSalesOrder,
  fetchWarehouses,
  lookupPosProduct,
  reportRxPosBlock,
  searchCustomers,
  searchPosProducts,
  updateDraftSale,
} from '@/shared/api/sales.api';
import type { PosProductSearchItem, SalesShiftDetail, TenantBatchModeValue, TenantRxSettings } from '@/shared/api/sales.types';
import { RX_POS_BLOCK_MESSAGE, shouldBlockRxAtPos } from '@/modules/pos/rx-dispensing';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { formatMoney } from '@/shared/utils/money';
import { priceCart } from '@/modules/sales/pos-pricing';
import { defaultBatchLabel, showsBatchLabelField, showsBatchPicker, validateCartBatchLabels } from '@/modules/sales/pos-batch';
import { buildCreateSalePayload, buildDraftUpdatePayload } from '@/modules/sales/pos-sale-payload';
import {
  clearPosDraftEdit,
  loadDraftCartLines,
  orderDiscountFromDetail,
  persistPosDraftEdit,
  readPosDraftEditId,
} from '@/modules/sales/sales-draft-helpers';
import { loadCustomerDraftOrderForPos } from '@/shared/api/customer-draft-orders.api';
import {
  loadCustomerDraftCartLines,
  orderDiscountFromCustomerDraft,
} from '@/modules/sales/customer-draft-order-helpers';
import { fetchCustomerById } from '@/shared/api/customer.api';
import { loadReservationForPos } from '@/shared/api/reservations.api';
import { buildReservationCartLines } from '@/modules/reservations/reservation-pos-load';
import { applyBatchLabelScan } from '@/modules/sales/pos-batch-scan';
import {
  applyLineAdjustClear,
  applyLineAdjustMode,
  applyLineAdjustValue,
  POS_LINE_ADJUST,
  resolveLineAdjust,
} from '@/modules/sales/pos-line-adjust';
import { useSalesDiscountPolicy } from '@/modules/sales/useSalesDiscountPolicy';
import { usePosHotkeys } from '@/shared/hooks/usePosHotkeys';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { OpenShiftSheet } from '@/modules/pos/OpenShiftSheet';
import { CloseShiftSheet } from '@/modules/today/CloseShiftSheet';
import { PosShiftDrawer } from '@/modules/pos/PosShiftDrawer';
import { BarcodeScanSheet } from '@/modules/pos/BarcodeScanSheet';
import { guessPhoneOrName, QuickCreateCustomerSheet } from '@/modules/pos/QuickCreateCustomerSheet';
import type { CustomerListItem } from '@/shared/api/sales.types';

export function PosPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((s) => s.user);
  const { canDiscount, maxPercent, canPriceOverride } = useSalesDiscountPolicy();

  const {
    warehouseId,
    warehouses,
    cart,
    customer,
    setWarehouses,
    setWarehouseId,
    setCustomer,
    addLine,
    replaceCart,
    updateQuantity,
    updateBatchLabel,
    updateLineDiscount,
    removeLine,
    editingDraftId,
    editingDraftNumber,
    setOrderDiscount,
    loadDraftIntoSession,
    setEditingDraft,
    clearDraftEdit,
    setLoadedCustomerDraft,
    setLoadedPrescription,
    setLoadedReservation,
  } = usePosSession();

  const [query, setQuery] = useState('');
  const searchRef = useRef<InputRef>(null);
  const [barcode, setBarcode] = useState('');
  const [scanOpen, setScanOpen] = useState(false);
  const [hits, setHits] = useState<PosProductSearchItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [savingDraft, setSavingDraft] = useState(false);
  const [shift, setShift] = useState<SalesShiftDetail | null>(null);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('off');
  const [rxSettings, setRxSettings] = useState<TenantRxSettings>({ enforcementMode: 'off', posBlockedAudit: true });
  const [shiftModal, setShiftModal] = useState(false);
  const [closeShiftModal, setCloseShiftModal] = useState(false);
  const [shiftDrawer, setShiftDrawer] = useState(false);
  const [customerOpen, setCustomerOpen] = useState(false);
  const [customerQuery, setCustomerQuery] = useState('');
  const [customerHits, setCustomerHits] = useState<Awaited<ReturnType<typeof searchCustomers>>>([]);
  const [batchLineKey, setBatchLineKey] = useState<string | null>(null);
  const [quickCreateOpen, setQuickCreateOpen] = useState(false);

  const orderDiscount = usePosSession((s) => s.orderDiscount);
  const loadedReservationNumber = usePosSession((s) => s.loadedReservationNumber);
  const loadedCustomerDraftNumber = usePosSession((s) => s.loadedCustomerDraftNumber);
  const loadedPrescriptionCode = usePosSession((s) => s.loadedPrescriptionCode);
  const priced = useMemo(() => priceCart(cart, orderDiscount), [cart, orderDiscount]);

  const selectCustomer = (c: CustomerListItem | null) => {
    setCustomer(c);
    setCustomerOpen(false);
    setCustomerQuery('');
  };

  const handleCustomerCreated = (c: CustomerListItem) => {
    setCustomerHits((prev) => (prev.some((row) => row.id === c.id) ? prev : [c, ...prev]));
    selectCustomer(c);
    setQuickCreateOpen(false);
  };

  const quickCreateDefaults = guessPhoneOrName(customerQuery);

  const loadShift = useCallback(async () => {
    if (!warehouseId) return;
    setShift(await fetchOpenShift(warehouseId));
  }, [warehouseId]);

  const loadDraftFromUrl = useCallback(
    async (draftId: string) => {
      try {
        const order = await fetchSalesOrder(draftId);
        if (order.status !== 1) {
          clearPosDraftEdit();
          clearDraftEdit();
          return;
        }
        const lines = await loadDraftCartLines(order);
        let draftCustomer: CustomerListItem | null = null;
        if (order.customerId) {
          const hits = await searchCustomers(order.customerName ?? '');
          draftCustomer = hits.find((c) => c.id === order.customerId) ?? hits[0] ?? null;
        }
        persistPosDraftEdit(draftId);
        loadDraftIntoSession({
          warehouseId: order.warehouseId ?? '',
          cart: lines,
          customer: draftCustomer,
          orderDiscount: orderDiscountFromDetail(order),
          draftId: order.id,
          draftNumber: order.orderNumber,
        });
        message.success(`Đã mở nháp ${order.orderNumber}`);
      } catch (error) {
        clearPosDraftEdit();
        clearDraftEdit();
        message.error(apiErrorMessage(error, 'Không mở được đơn nháp'));
      }
    },
    [clearDraftEdit, loadDraftIntoSession, message],
  );

  const loadCustomerDraftIntoPos = useCallback(
    async (draftOrderId: string, options?: { autoCheckout?: boolean }) => {
      try {
        const payload = await loadCustomerDraftOrderForPos(draftOrderId);
        const lines = await loadCustomerDraftCartLines(payload);
        setWarehouseId(payload.warehouseId);
        replaceCart(lines);
        setOrderDiscount(orderDiscountFromCustomerDraft(payload));
        const customerRow = await fetchCustomerById(payload.customerId);
        setCustomer({
          id: customerRow.id,
          customerCode: customerRow.customerCode,
          fullName: customerRow.fullName,
          phone: customerRow.phone,
          allowCredit: customerRow.allowCredit,
        });
        setLoadedCustomerDraft(payload.draftOrderId, payload.draftNumber);
        if (options?.autoCheckout) {
          message.success(`Đã nạp ${payload.draftNumber} — chuyển thanh toán`);
          navigate('/checkout');
        } else {
          message.success(`Đã nạp đơn nháp ${payload.draftNumber}`);
        }
      } catch (error) {
        message.error(apiErrorMessage(error, 'Không nạp được đơn nháp app khách'));
      }
    },
    [message, navigate, replaceCart, setCustomer, setLoadedCustomerDraft, setOrderDiscount, setWarehouseId],
  );

  const loadCustomerReservationIntoPos = useCallback(
    async (reservationId: string, options?: { autoCheckout?: boolean }) => {
      try {
        const payload = await loadReservationForPos(reservationId);
        const lines = await buildReservationCartLines(payload);
        setWarehouseId(payload.warehouseId);
        replaceCart(lines);
        setOrderDiscount({});
        const customerRow = await fetchCustomerById(payload.customerId);
        setCustomer({
          id: customerRow.id,
          customerCode: customerRow.customerCode,
          fullName: customerRow.fullName,
          phone: customerRow.phone,
          allowCredit: customerRow.allowCredit,
        });
        setLoadedReservation(payload.reservationId, payload.reservationNumber);
        if (options?.autoCheckout) {
          message.success(`Đã nạp ${payload.reservationNumber} — chuyển thanh toán`);
          navigate('/checkout');
        } else {
          message.success(`Đã nạp giữ hàng ${payload.reservationNumber}`);
        }
      } catch (error) {
        message.error(apiErrorMessage(error, 'Không nạp được giữ hàng'));
      }
    },
    [message, navigate, replaceCart, setCustomer, setLoadedReservation, setOrderDiscount, setWarehouseId],
  );

  useEffect(() => {
    void (async () => {
      let lastError: unknown;
      for (let attempt = 0; attempt < 3; attempt += 1) {
        try {
          if (attempt > 0) await new Promise((r) => window.setTimeout(r, 700));
          const [wh, mode, rx] = await Promise.all([
            fetchWarehouses(),
            fetchBatchModeSettings(),
            fetchRxSettings(),
          ]);
          setWarehouses(wh, wh[0]?.id);
          setBatchMode(mode);
          setRxSettings(rx);
          return;
        } catch (error) {
          lastError = error;
        }
      }
      message.error(apiErrorMessage(lastError, 'Không tải được dữ liệu quầy'));
    })();
  }, [message, setWarehouses]);

  useEffect(() => {
    void loadShift();
  }, [loadShift]);

  useEffect(() => {
    const draftId = searchParams.get('draftId') ?? readPosDraftEditId();
    if (!draftId || editingDraftId === draftId) return;
    if (searchParams.get('draftId')) {
      setSearchParams({}, { replace: true });
    }
    void loadDraftFromUrl(draftId);
  }, [searchParams, editingDraftId, loadDraftFromUrl, setSearchParams]);

  const customerDraftDeepLink = useRef<string | null>(null);
  useEffect(() => {
    const customerDraftId = searchParams.get('customerDraftId');
    if (!customerDraftId) {
      customerDraftDeepLink.current = null;
      return;
    }
    if (customerDraftDeepLink.current === customerDraftId) return;
    customerDraftDeepLink.current = customerDraftId;
    const autoCheckout = searchParams.get('checkout') === '1';
    void loadCustomerDraftIntoPos(customerDraftId, { autoCheckout }).finally(() => {
      customerDraftDeepLink.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [loadCustomerDraftIntoPos, searchParams, setSearchParams]);

  const reservationDeepLink = useRef<string | null>(null);
  useEffect(() => {
    const customerReservationId = searchParams.get('customerReservationId');
    if (!customerReservationId) {
      reservationDeepLink.current = null;
      return;
    }
    if (reservationDeepLink.current === customerReservationId) return;
    reservationDeepLink.current = customerReservationId;
    const autoCheckout = searchParams.get('checkout') === '1';
    void loadCustomerReservationIntoPos(customerReservationId, { autoCheckout }).finally(() => {
      reservationDeepLink.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [loadCustomerReservationIntoPos, searchParams, setSearchParams]);

  const prescriptionDeepLink = useRef<string | null>(null);
  useEffect(() => {
    const prescriptionId = searchParams.get('prescriptionId');
    if (!prescriptionId) {
      prescriptionDeepLink.current = null;
      return;
    }
    if (!warehouseId) return;
    if (prescriptionDeepLink.current === prescriptionId) return;
    prescriptionDeepLink.current = prescriptionId;
    void fetchPrescriptionPosLoad(prescriptionId, warehouseId)
      .then((payload) => {
        replaceCart(
          payload.lines
            .filter((line) => line.productUnitId && line.qtyRemaining > 0)
            .map((line) => ({
              key: `${line.productUnitId}-${line.prescriptionLineId}`,
              productId: line.productId,
              productCode: line.productCode,
              productName: line.productName,
              productUnitId: line.productUnitId as string,
              unitName: line.unitName ?? '',
              quantity: line.qtyRemaining,
              unitPrice: line.unitPrice,
              catalogUnitPrice: line.unitPrice,
              stockAvailable: line.stockAvailable,
              dispensingClass: line.lineDispensingClass,
              prescriptionLineId: line.prescriptionLineId,
            })),
        );
        setLoadedPrescription(payload.id, payload.prescriptionCode);
        message.success(`Đã nạp đơn BS ${payload.prescriptionCode}`);
      })
      .catch((error) => message.error(apiErrorMessage(error, 'Không nạp được đơn BS')))
      .finally(() => {
        prescriptionDeepLink.current = null;
        setSearchParams({}, { replace: true });
      });
  }, [searchParams, setSearchParams, warehouseId, replaceCart, setLoadedPrescription, message]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setHits([]);
      return;
    }
    const timer = window.setTimeout(() => {
      void (async () => {
        if (!warehouseId) return;
        setSearching(true);
        try {
          setHits(await searchPosProducts(query.trim(), warehouseId));
        } catch {
          setHits([]);
        } finally {
          setSearching(false);
        }
      })();
    }, 280);
    return () => window.clearTimeout(timer);
  }, [query, warehouseId]);

  const blockRxProduct = (product: { productId: string; dispensingClass?: string | null }) => {
    if (!shouldBlockRxAtPos(product.dispensingClass, rxSettings.enforcementMode)) return false;
    message.error(RX_POS_BLOCK_MESSAGE);
    if (rxSettings.posBlockedAudit && warehouseId) {
      void reportRxPosBlock(product.productId, warehouseId).catch(() => undefined);
    }
    return true;
  };

  const addFromLookup = async (lookupCode: string) => {
    if (!warehouseId) return;
    try {
      const product = await lookupPosProduct(lookupCode, warehouseId);
      if (blockRxProduct(product)) return;
      const key = `${product.productUnitId}`;
      addLine({
        key,
        productId: product.productId,
        productCode: product.productCode,
        productName: product.productName,
        productUnitId: product.productUnitId,
        unitName: product.unitName,
        quantity: 1,
        unitPrice: product.unitPrice,
        catalogUnitPrice: product.unitPrice,
        dispensingClass: product.dispensingClass,
        stockAvailable: product.stockAvailable,
        batchHints: product.batchHints,
        batchLabel: defaultBatchLabel(product.batchHints),
      });
      setQuery('');
      setBarcode('');
      setHits([]);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thêm được sản phẩm'));
    }
  };

  const addByBarcode = useCallback(
    async (code?: string) => {
      const value = (code ?? barcode).trim();
      if (!warehouseId || !value) return;
      try {
        const item = await lookupPosProduct(value, warehouseId);
        if (blockRxProduct(item)) return;
        if (item.stockAvailable <= 0) {
          message.warning('Hết hàng');
          return;
        }
        const existing = cart.find((l) => l.productUnitId === item.productUnitId);
        if (existing) {
          if (existing.quantity + 1 > item.stockAvailable + 0.0001) {
            message.warning(`Vượt tồn (${item.stockAvailable})`);
            return;
          }
          updateQuantity(existing.key, existing.quantity + 1);
        } else {
          addLine({
            key: item.productUnitId,
            productId: item.productId,
            productCode: item.productCode,
            productName: item.productName,
            productUnitId: item.productUnitId,
            unitName: item.unitName,
            quantity: 1,
            unitPrice: item.unitPrice,
            catalogUnitPrice: item.unitPrice,
            dispensingClass: item.dispensingClass,
            stockAvailable: item.stockAvailable,
            batchHints: item.batchHints,
            batchLabel: defaultBatchLabel(item.batchHints),
          });
        }
        setBarcode('');
        setQuery('');
        setHits([]);
      } catch {
        if (showsBatchLabelField(batchMode)) {
          const batchResult = applyBatchLabelScan(cart, value);
          if (batchResult) {
            replaceCart(batchResult.cart);
            message.success(`Đã gán lô ${batchResult.batchNumber} · ${batchResult.productName}`);
            setBarcode('');
            return;
          }
        }
        message.error('Không tìm thấy sản phẩm / lô');
      }
    },
    [barcode, batchMode, cart, message, warehouseId, addLine, replaceCart, updateQuantity],
  );

  const handleSearchEnter = async () => {
    const q = query.trim();
    if (!q || !warehouseId) return;
    if (hits.length === 1) {
      await addFromLookup(hits[0].lookupCode);
      return;
    }
    const compact = q.replace(/\s/g, '');
    if (/^\d{6,}$/.test(compact)) {
      await addByBarcode(compact);
      setQuery('');
      return;
    }
    try {
      await addFromLookup(q);
    } catch {
      if (hits.length === 0) {
        message.info('Không thấy SP — thử tên khác hoặc quét mã vạch');
      }
    }
  };

  const goCheckout = () => {
    if (!shift) {
      message.warning('Mở ca trước khi thanh toán');
      setShiftDrawer(true);
      return;
    }
    if (cart.length === 0) return;
    navigate('/checkout');
  };

  const saveDraft = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho trước');
      return;
    }
    if (cart.length === 0) {
      message.warning('Thêm sản phẩm vào giỏ');
      return;
    }
    const batchError = validateCartBatchLabels(cart, batchMode);
    if (batchError) {
      message.warning(batchError);
      return;
    }
    setSavingDraft(true);
    try {
      if (editingDraftId) {
        const order = await updateDraftSale(
          editingDraftId,
          buildDraftUpdatePayload(customer?.id, cart, orderDiscount),
        );
        setEditingDraft(order.id, order.orderNumber);
        message.success(`Đã cập nhật nháp ${order.orderNumber}`);
      } else {
        const order = await createSale(
          buildCreateSalePayload(warehouseId, customer?.id, cart, orderDiscount, true),
        );
        persistPosDraftEdit(order.id);
        setEditingDraft(order.id, order.orderNumber);
        message.success(`Đã lưu nháp ${order.orderNumber}`);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được nháp'));
    } finally {
      setSavingDraft(false);
    }
  };

  const posHotkeys = useMemo(
    () => ({
      onFocusSearch: () => searchRef.current?.focus(),
      onCheckout: goCheckout,
      onSaveDraft: () => void saveDraft(),
    }),
    [cart.length, shift, warehouseId],
  );
  usePosHotkeys(posHotkeys);

  const batchLine = cart.find((c) => c.key === batchLineKey);
  const activeWarehouse = warehouses.find((w) => w.id === warehouseId);
  const warehouseLabel = activeWarehouse
    ? activeWarehouse.branchName
      ? `${activeWarehouse.warehouseName} · ${activeWarehouse.branchName}`
      : activeWarehouse.warehouseName
    : '—';

  return (
    <div className="staff-shell">
      <header className="staff-header pos-header">
        <div className="pos-header-row">
          <span className="pos-tenant-label">{user?.tenantCode ?? '—'}</span>
          <Button
            type="text"
            className="pos-home-btn"
            icon={<HomeOutlined />}
            onClick={() => navigate('/')}
            aria-label="Về menu"
          />
        </div>
        <Select
          className="pos-warehouse-select"
          variant="borderless"
          size="small"
          value={warehouseId ?? undefined}
          suffixIcon={<span className="pos-shift-chevron">▾</span>}
          options={warehouses.map((w) => ({
            value: w.id,
            label: w.branchName ? `${w.warehouseName} · ${w.branchName}` : w.warehouseName,
          }))}
          onChange={setWarehouseId}
        />
        <button
          type="button"
          className={`pos-shift-bar${shift ? ' is-open' : ''}`}
          onClick={() => setShiftDrawer(true)}
        >
          <span className={`pos-shift-dot${shift ? ' is-open' : ''}`} />
          <span className="pos-shift-label">
            {shift ? `Ca ${shift.shiftNumber} · đang mở` : 'Chưa mở ca · chạm để mở'}
          </span>
          <span className="pos-shift-chevron">›</span>
        </button>
      </header>

      <main className="staff-body pos-body">
        {loadedReservationNumber ? (
          <Alert
            type="info"
            showIcon
            message={`Giữ hàng ${loadedReservationNumber}`}
            description="Sau khi bán xong, đơn sẽ tự đánh dấu đã lấy."
            style={{ marginBottom: 12 }}
          />
        ) : null}
        {loadedCustomerDraftNumber ? (
          <Alert
            type="info"
            showIcon
            message={`Đơn nháp app ${loadedCustomerDraftNumber}`}
            description="Sau khi bán xong, đơn nháp sẽ được liên kết với hóa đơn."
            style={{ marginBottom: 12 }}
          />
        ) : null}
        {loadedPrescriptionCode ? (
          <Alert
            type="success"
            showIcon
            message={`Đơn BS ${loadedPrescriptionCode}`}
            description="Giỏ hàng đang liên kết đơn thuốc bác sĩ."
            style={{ marginBottom: 12 }}
          />
        ) : null}
        {editingDraftNumber ? (
          <Alert
            type="warning"
            showIcon
            message={`Đang sửa nháp ${editingDraftNumber}`}
            description="Lưu nháp để cập nhật · Thanh toán để chốt đơn."
            style={{ marginBottom: 12 }}
          />
        ) : null}

        <section className="pos-search-block">
          <div className="pos-field-head">
            <span className="pos-field-label">Tìm và thêm sản phẩm</span>
            <span className="pos-field-hint">
              {searching ? 'Đang tìm…' : 'Gõ tên / mã SP · chạm kết quả để thêm'}
            </span>
          </div>
          <Input
            ref={searchRef}
            size="large"
            className="staff-touch-input"
            prefix={<SearchOutlined />}
            suffix={
              <Button
                type="text"
                icon={<ScanOutlined />}
                aria-label="Mở camera quét"
                onClick={() => setScanOpen(true)}
                style={{ marginInlineEnd: -6 }}
              />
            }
            placeholder="Tìm và thêm sản phẩm vào đơn"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onPressEnter={() => void handleSearchEnter()}
            allowClear
          />
          {hits.length > 0 ? (
            <div className="pos-hit-list">
              {hits.map((hit) => (
                <div key={hit.lookupCode} className="search-hit" onClick={() => void addFromLookup(hit.lookupCode)}>
                  <Typography.Text strong>{hit.productName}</Typography.Text>
                  <div className="pos-hit-meta">
                    {hit.productCode} · {formatMoney(hit.unitPrice)} · Tồn {hit.stockAvailable}
                  </div>
                </div>
              ))}
            </div>
          ) : null}
        </section>

        <section className="pos-search-block">
          <div className="pos-field-head">
            <span className="pos-field-label">Quét mã vạch</span>
            <span className="pos-field-hint">Camera hoặc gõ mã rồi Enter</span>
          </div>
          <Space.Compact block className="pos-barcode-row">
            <Input
              size="large"
              prefix={<ScanOutlined />}
              placeholder="Barcode..."
              value={barcode}
              onChange={(e) => setBarcode(e.target.value)}
              onPressEnter={() => void addByBarcode()}
              allowClear
            />
            <Button size="large" icon={<ScanOutlined />} onClick={() => setScanOpen(true)} aria-label="Mở camera quét" />
            <Button type="primary" size="large" onClick={() => void addByBarcode()}>
              +
            </Button>
          </Space.Compact>
        </section>

        <Typography.Title level={5} className="pos-cart-title">
          Giỏ hàng ({cart.length})
        </Typography.Title>

        {cart.length === 0 ? (
          <div className="pos-empty-cart">
            <div className="pos-empty-cart__icon" aria-hidden>
              <ShoppingCartOutlined />
            </div>
            <p className="pos-empty-cart__title">Đơn chưa có sản phẩm</p>
            <p className="pos-empty-cart__hint">Tìm hoặc quét mã để thêm vào giỏ</p>
            <Button
              type="link"
              className="pos-empty-cart__action"
              onClick={() => searchRef.current?.focus()}
            >
              Chọn sản phẩm
            </Button>
          </div>
        ) : (
          cart.map((line) => (
            <div key={line.key} className="cart-line">
              <div className="cart-line-top">
                <div className="cart-line-info">
                  <Typography.Text strong>{line.productName}</Typography.Text>
                  <div className="cart-line-meta">
                    <span>{line.productCode}</span>
                    <span>·</span>
                    {resolveLineAdjust(line) === POS_LINE_ADJUST.UnitPrice ? (
                      <InputNumber
                        min={0}
                        precision={0}
                        value={line.unitPrice}
                        controls={false}
                        style={{ width: 110 }}
                        formatter={(v) =>
                          v == null ? '' : `${v}`.replace(/\./g, '').replace(/\B(?=(\d{3})+(?!\d))/g, '.')
                        }
                        parser={(v) => Number(String(v ?? '').replace(/\./g, '')) as 0}
                        onChange={(v) => {
                          replaceCart(
                            cart.map((l) =>
                              l.key === line.key ? applyLineAdjustValue(l, Number(v ?? 0)) : l,
                            ),
                          );
                        }}
                      />
                    ) : (
                      <span>{formatMoney(line.unitPrice)}</span>
                    )}
                  </div>
                  {showsBatchPicker(batchMode, line.batchHints) ? (
                    <Button
                      type="link"
                      style={{ padding: 0, height: 'auto', fontSize: 14, fontWeight: 600 }}
                      onClick={() => setBatchLineKey(line.key)}
                    >
                      Lô: {line.batchLabel || defaultBatchLabel(line.batchHints) || 'Chọn lô'}
                      {line.batchHints?.some((h) => h.isSuggested) ? ' · FEFO' : ''}
                    </Button>
                  ) : null}
                </div>
                <Button
                  type="text"
                  danger
                  className="cart-line-remove"
                  onClick={() => removeLine(line.key)}
                  aria-label="Xóa dòng"
                >
                  ×
                </Button>
              </div>
              <div className="cart-line-foot">
                <Space className="cart-line-qty" size={4}>
                  <Button icon={<MinusOutlined />} onClick={() => updateQuantity(line.key, line.quantity - 1)} />
                  <InputNumber min={1} value={line.quantity} controls={false} readOnly />
                  <Button icon={<PlusOutlined />} onClick={() => updateQuantity(line.key, line.quantity + 1)} />
                </Space>
                {(canDiscount || canPriceOverride) ? (
                <Space className="cart-line-discount" size={4} align="center">
                  <span className="cart-line-discount-label">CK</span>
                  <Select
                    size="small"
                    allowClear
                    placeholder="%"
                    style={{ width: canPriceOverride ? 92 : 56 }}
                    value={resolveLineAdjust(line)}
                    options={[
                      ...(canDiscount
                        ? [
                            { value: POS_LINE_ADJUST.Percent, label: '%' },
                            { value: POS_LINE_ADJUST.Fixed, label: 'Giá trị' },
                          ]
                        : []),
                      ...(canPriceOverride
                        ? [{ value: POS_LINE_ADJUST.UnitPrice, label: 'Sửa giá' }]
                        : []),
                    ]}
                    onChange={(mode) => {
                      replaceCart(
                        cart.map((l) =>
                          l.key === line.key
                            ? mode
                              ? applyLineAdjustMode(l, mode as typeof POS_LINE_ADJUST.Percent)
                              : applyLineAdjustClear(l)
                            : l,
                        ),
                      );
                    }}
                  />
                  {resolveLineAdjust(line) === POS_LINE_ADJUST.UnitPrice ? null : (
                    <InputNumber
                      size="small"
                      className="cart-line-discount-input"
                      min={0}
                      max={
                        resolveLineAdjust(line) === POS_LINE_ADJUST.Percent ? maxPercent : undefined
                      }
                      precision={0}
                      placeholder="0"
                      disabled={!resolveLineAdjust(line)}
                      value={(line.discountValue ?? 0) > 0 ? line.discountValue : undefined}
                      inputMode="numeric"
                      controls={false}
                      formatter={(v) => {
                        if (v == null) return '';
                        const raw = `${v}`.replace(/\./g, '');
                        if (resolveLineAdjust(line) === POS_LINE_ADJUST.Percent) return raw;
                        return raw.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
                      }}
                      parser={(v) => Number(String(v ?? '').replace(/\./g, '')) as 0}
                      onChange={(v) => {
                        let val = Number(v ?? 0);
                        if (val <= 0) {
                          replaceCart(
                            cart.map((l) => (l.key === line.key ? applyLineAdjustClear(l) : l)),
                          );
                          return;
                        }
                        if (resolveLineAdjust(line) === POS_LINE_ADJUST.Percent) {
                          val = Math.min(val, maxPercent);
                        }
                        replaceCart(
                          cart.map((l) =>
                            l.key === line.key ? applyLineAdjustValue(l, val) : l,
                          ),
                        );
                      }}
                    />
                  )}
                </Space>
                ) : null}
              </div>
            </div>
          ))
        )}

        <Button
          block
          size="large"
          className="pos-customer-btn"
          icon={<UserOutlined />}
          style={{ marginTop: 12 }}
          onClick={() => {
            setCustomerOpen(true);
            void searchCustomers('').then(setCustomerHits);
          }}
        >
          {customer
            ? `${customer.fullName} · ${customer.phone}${customer.allowCredit ? ' · được nợ' : ''}`
            : 'Thêm khách hàng'}
        </Button>
      </main>

      <footer className="staff-footer">
        <div className="pos-footer-total">
          <div>
            <span className="pos-footer-total__label">Tạm tính</span>
            <span className="pos-footer-total__badge">{cart.length} SP</span>
            {priced.totalDiscountAmount > 0 ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 13, marginTop: 2 }}>
                Đã giảm {formatMoney(priced.totalDiscountAmount)}
              </Typography.Text>
            ) : null}
          </div>
          <Typography.Title level={3} className="pos-footer-total__amount">
            {formatMoney(priced.totalAmount)}
          </Typography.Title>
        </div>
        <div className="pos-footer-actions">
          <Button
            className="pos-footer-draft"
            icon={<SaveOutlined />}
            disabled={cart.length === 0}
            loading={savingDraft}
            onClick={() => void saveDraft()}
          >
            {editingDraftId ? 'Cập nhật' : 'Lưu tạm'}
          </Button>
          <Button
            type="primary"
            className="pos-footer-pay"
            disabled={cart.length === 0}
            onClick={goCheckout}
          >
            Thanh toán
          </Button>
        </div>
      </footer>

      <OpenShiftSheet
        open={shiftModal}
        warehouseId={warehouseId ?? ''}
        onClose={() => setShiftModal(false)}
        onOpened={() => void loadShift()}
      />

      <PosShiftDrawer
        open={shiftDrawer}
        shift={shift}
        warehouseLabel={warehouseLabel}
        onClose={() => setShiftDrawer(false)}
        onOpenShift={() => {
          setShiftDrawer(false);
          setShiftModal(true);
        }}
        onCloseShift={() => {
          setShiftDrawer(false);
          setCloseShiftModal(true);
        }}
        onViewToday={() => {
          setShiftDrawer(false);
          navigate('/today');
        }}
      />

      <CloseShiftSheet
        open={closeShiftModal}
        shift={shift}
        onClose={() => setCloseShiftModal(false)}
        onClosed={() => void loadShift()}
      />

      <BarcodeScanSheet
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        onScan={(code) => void addByBarcode(code)}
      />

      <Drawer
        title="Khách hàng"
        className="pos-customer-drawer"
        open={customerOpen}
        onClose={() => setCustomerOpen(false)}
        height="70%"
        placement="bottom"
      >
        <div className="pos-customer-drawer__head">
          <Input
            placeholder="SĐT hoặc tên"
            value={customerQuery}
            onChange={(e) => {
              setCustomerQuery(e.target.value);
              void searchCustomers(e.target.value).then(setCustomerHits);
            }}
            style={{ marginBottom: 12 }}
          />
          <Button
            block
            icon={<UserAddOutlined />}
            style={{ marginBottom: 12 }}
            onClick={() => setQuickCreateOpen(true)}
          >
            Thêm khách mới
          </Button>
          {customerQuery.trim().length >= 2 && customerHits.length === 0 ? (
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
              Không thấy khách — thêm mới với thông tin đã gõ.
            </Typography.Text>
          ) : null}
        </div>
        <div className="pos-customer-drawer__list">
          {customerHits.map((c) => (
            <div key={c.id} className="search-hit" onClick={() => selectCustomer(c)}>
              <Typography.Text strong>{c.fullName}</Typography.Text>
              <div style={{ fontSize: 12, color: '#64748b' }}>{c.phone}</div>
            </div>
          ))}
        </div>
        <div className="pos-customer-drawer__foot">
          <Button block onClick={() => selectCustomer(null)}>
            Bán không chọn khách
          </Button>
        </div>
      </Drawer>

      <QuickCreateCustomerSheet
        open={quickCreateOpen}
        initialPhone={quickCreateDefaults.phone}
        initialName={quickCreateDefaults.name}
        onClose={() => setQuickCreateOpen(false)}
        onCreated={handleCustomerCreated}
      />

      <Drawer
        title={batchLine ? `Chọn lô · ${batchLine.productName}` : 'Chọn lô'}
        open={Boolean(batchLineKey)}
        onClose={() => setBatchLineKey(null)}
        height="50%"
        placement="bottom"
      >
        {batchLine?.batchHints?.map((hint) => (
          <div
            key={hint.batchId}
            className="search-hit"
            onClick={() => {
              if (batchLineKey) updateBatchLabel(batchLineKey, hint.batchNumber);
              setBatchLineKey(null);
            }}
          >
            <Typography.Text strong>
              {hint.batchNumber}
              {hint.isSuggested ? ' · FEFO' : ''}
            </Typography.Text>
            <div style={{ fontSize: 12, color: '#64748b' }}>
              HSD {hint.expiryDate ? dayjs(hint.expiryDate).format('MM/YYYY') : '—'} · Tồn {hint.quantityAvailable}
            </div>
          </div>
        ))}
      </Drawer>
    </div>
  );
}
