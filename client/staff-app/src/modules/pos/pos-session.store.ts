import { create } from 'zustand';
import type { CartLine, CustomerListItem, Warehouse } from '@/shared/api/sales.types';
import type { OrderDiscountState } from '@/modules/sales/pos-pricing';

interface PosSessionState {
  warehouseId: string | null;
  warehouses: Warehouse[];
  cart: CartLine[];
  customer: CustomerListItem | null;
  orderDiscount: OrderDiscountState;
  loadedReservationId: string | null;
  loadedReservationNumber: string | null;
  loadedCustomerDraftOrderId: string | null;
  loadedCustomerDraftNumber: string | null;
  loadedPrescriptionId: string | null;
  loadedPrescriptionCode: string | null;
  editingDraftId: string | null;
  editingDraftNumber: string | null;
  setWarehouses: (items: Warehouse[], defaultId?: string) => void;
  setWarehouseId: (id: string) => void;
  setCustomer: (customer: CustomerListItem | null) => void;
  setOrderDiscount: (discount: OrderDiscountState) => void;
  setLoadedReservation: (id: string | null, number?: string | null) => void;
  setLoadedCustomerDraft: (id: string | null, number?: string | null) => void;
  setLoadedPrescription: (id: string | null, code?: string | null) => void;
  setEditingDraft: (id: string | null, number?: string | null) => void;
  loadDraftIntoSession: (payload: {
    warehouseId: string;
    cart: CartLine[];
    customer: CustomerListItem | null;
    orderDiscount: OrderDiscountState;
    draftId: string;
    draftNumber: string;
  }) => void;
  clearDraftEdit: () => void;
  updateLineDiscount: (key: string, discountType?: number, discountValue?: number) => void;
  updateUnitPrice: (key: string, unitPrice: number) => void;
  replaceCart: (lines: CartLine[]) => void;
  addLine: (line: CartLine) => void;
  updateQuantity: (key: string, quantity: number) => void;
  updateBatchLabel: (key: string, batchLabel: string) => void;
  removeLine: (key: string) => void;
  clearCart: () => void;
}

export const usePosSession = create<PosSessionState>((set, get) => ({
  warehouseId: null,
  warehouses: [],
  cart: [],
  customer: null,
  orderDiscount: {},
  loadedReservationId: null,
  loadedReservationNumber: null,
  loadedCustomerDraftOrderId: null,
  loadedCustomerDraftNumber: null,
  loadedPrescriptionId: null,
  loadedPrescriptionCode: null,
  editingDraftId: null,
  editingDraftNumber: null,
  setWarehouses: (items, defaultId) =>
    set({
      warehouses: items,
      warehouseId: defaultId ?? items[0]?.id ?? null,
    }),
  setWarehouseId: (id) => {
    const current = get().warehouseId;
    if (current === id) return;
    set({
      warehouseId: id,
      cart: [],
      customer: null,
      loadedReservationId: null,
      loadedReservationNumber: null,
      loadedCustomerDraftOrderId: null,
      loadedCustomerDraftNumber: null,
      loadedPrescriptionId: null,
      loadedPrescriptionCode: null,
    });
  },
  setCustomer: (customer) => set({ customer }),
  setOrderDiscount: (orderDiscount) => set({ orderDiscount }),
  setLoadedReservation: (id, number = null) =>
    set({
      loadedReservationId: id,
      loadedReservationNumber: number ?? null,
      loadedCustomerDraftOrderId: null,
      loadedCustomerDraftNumber: null,
      loadedPrescriptionId: null,
      loadedPrescriptionCode: null,
    }),
  setLoadedCustomerDraft: (id, number = null) =>
    set({
      loadedCustomerDraftOrderId: id,
      loadedCustomerDraftNumber: number ?? null,
      loadedReservationId: null,
      loadedReservationNumber: null,
      loadedPrescriptionId: null,
      loadedPrescriptionCode: null,
      editingDraftId: null,
      editingDraftNumber: null,
    }),
  setLoadedPrescription: (id, code = null) =>
    set({
      loadedPrescriptionId: id,
      loadedPrescriptionCode: code ?? null,
      loadedReservationId: null,
      loadedReservationNumber: null,
      loadedCustomerDraftOrderId: null,
      loadedCustomerDraftNumber: null,
      editingDraftId: null,
      editingDraftNumber: null,
    }),
  setEditingDraft: (id, number = null) =>
    set({ editingDraftId: id, editingDraftNumber: number ?? null }),
  loadDraftIntoSession: ({ warehouseId, cart, customer, orderDiscount, draftId, draftNumber }) =>
    set({
      warehouseId,
      cart,
      customer,
      orderDiscount,
      editingDraftId: draftId,
      editingDraftNumber: draftNumber,
      loadedReservationId: null,
      loadedReservationNumber: null,
      loadedCustomerDraftOrderId: null,
      loadedCustomerDraftNumber: null,
      loadedPrescriptionId: null,
      loadedPrescriptionCode: null,
    }),
  clearDraftEdit: () => set({ editingDraftId: null, editingDraftNumber: null }),
  updateLineDiscount: (key, discountType, discountValue) =>
    set({
      cart: get().cart.map((c) =>
        c.key === key
          ? {
              ...c,
              discountType: discountType as CartLine['discountType'],
              discountValue: discountType ? discountValue ?? 0 : undefined,
            }
          : c,
      ),
    }),
  updateUnitPrice: (key, unitPrice) =>
    set({
      cart: get().cart.map((c) => (c.key === key ? { ...c, unitPrice } : c)),
    }),
  replaceCart: (lines) => set({ cart: lines }),
  addLine: (line) => {
    const cart = get().cart;
    const existing = cart.find((c) => c.key === line.key);
    if (existing) {
      set({
        cart: cart.map((c) =>
          c.key === line.key ? { ...c, quantity: c.quantity + line.quantity } : c,
        ),
      });
      return;
    }
    set({ cart: [...cart, line] });
  },
  updateQuantity: (key, quantity) => {
    if (quantity <= 0) {
      set({ cart: get().cart.filter((c) => c.key !== key) });
      return;
    }
    set({
      cart: get().cart.map((c) => (c.key === key ? { ...c, quantity } : c)),
    });
  },
  updateBatchLabel: (key, batchLabel) =>
    set({
      cart: get().cart.map((c) => (c.key === key ? { ...c, batchLabel } : c)),
    }),
  removeLine: (key) => set({ cart: get().cart.filter((c) => c.key !== key) }),
  clearCart: () =>
    set({
      cart: [],
      customer: null,
      orderDiscount: {},
      loadedReservationId: null,
      loadedReservationNumber: null,
      loadedCustomerDraftOrderId: null,
      loadedCustomerDraftNumber: null,
      loadedPrescriptionId: null,
      loadedPrescriptionCode: null,
      editingDraftId: null,
      editingDraftNumber: null,
    }),
}));
