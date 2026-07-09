import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Drawer,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  EyeOutlined,
  EditOutlined,
  DollarOutlined,
  PrinterOutlined,
  ReloadOutlined,
  RollbackOutlined,
} from '@ant-design/icons';
import {
  cancelDraftSale,
  completeDraftSale,
  createSaleReturn,
  fetchOrderReturns,
  fetchPosCustomerLoyalty,
  fetchSalesOrder,
  fetchSalesOrders,
  fetchSalesReturn,
  searchCustomers,
} from '@/shared/api/sales.api';
import type {
  CustomerListItem,
  PosCheckoutConfirm,
  PosCustomerLoyalty,
  SalesOrderDetail,
  SalesOrderListItem,
  SalesReturnListItem,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSaleStatusLabels } from '@/shared/i18n/use-sale-status-labels';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import {
  matchesSaleStatusFilter,
  PARTIAL_RETURN_STATUS,
} from '@/modules/sales/sales-order-status';
import { OrderDetailFinancials } from '@/modules/sales/OrderDetailFinancials';
import { DispensingNotesPanel } from '@/modules/sales/DispensingNotesPanel';
import { buildCustomerPaymentCreateUrl } from '@/modules/sales/customer-payment-nav';
import { resolveOrderPaymentSummary } from '@/modules/sales/sales-order-payment-summary';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import type { CustomerDetail } from '@/shared/api/customer-admin.types';
import { PosCheckoutModal } from '@/modules/sales/PosCheckoutModal';
import { SalesReturnDetailDrawer } from '@/modules/sales/SalesReturnDetailDrawer';
import { SalesReturnModal } from '@/modules/sales/SalesReturnModal';
import { printSalesInvoice } from '@/modules/sales/sales-invoice-print';
import { formatPosCheckoutSuccessMessage } from '@/modules/sales/pos-checkout-message';
import { printSalesReturn } from '@/modules/sales/sales-return-print';
import { sectionGapStyle, sectionGapTopStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
import {
  buildCustomerSearchSuggestions,
  buildDocumentSearchSuggestions,
} from '@/modules/sales/sales-list-customer-search';
import { SalesListDualSearchBar, SalesListDualSearchWrap } from '@/modules/sales/SalesListDualSearchBar';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

function isCompletedSaleStatus(status: number): boolean {
  return status === 2 || status === 4;
}

export function SalesOrderListPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'orders' });
  const { orderDisplayStatus, saleStatusFilterOptions } = useSaleStatusLabels();
  const { returnStatusLabel } = useSalesEnums();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canRead = useHasPermission('sales.read');
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<SalesOrderListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [customerInput, setCustomerInput] = useState('');
  const [documentInput, setDocumentInput] = useState('');
  const [appliedCustomer, setAppliedCustomer] = useState('');
  const [appliedDocument, setAppliedDocument] = useState('');
  const [statusFilter, setStatusFilter] = useState<number | typeof PARTIAL_RETURN_STATUS | undefined>();
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<SalesOrderDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);
  const [returnSaving, setReturnSaving] = useState(false);
  const [orderReturns, setOrderReturns] = useState<SalesReturnListItem[]>([]);
  const [orderReturnsLoading, setOrderReturnsLoading] = useState(false);
  const [returnDetailOpen, setReturnDetailOpen] = useState(false);
  const [returnDetailId, setReturnDetailId] = useState<string | null>(null);
  const [draftCheckoutOpen, setDraftCheckoutOpen] = useState(false);
  const [draftCustomerId, setDraftCustomerId] = useState<string>();
  const [draftCustomerLoyalty, setDraftCustomerLoyalty] = useState<PosCustomerLoyalty | null>(null);
  const [draftCompleting, setDraftCompleting] = useState(false);
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [quickCustomerOpen, setQuickCustomerOpen] = useState(false);

  const load = useCallback(async (
    nextPage: number,
    nextPageSize: number,
    nextStatus: number | typeof PARTIAL_RETURN_STATUS | undefined = statusFilter,
    customerSearch: string = appliedCustomer,
    documentSearch: string = appliedDocument,
  ) => {
    setLoading(true);
    try {
      const apiStatus = typeof nextStatus === 'number' ? nextStatus : undefined;
      const result = await fetchSalesOrders({
        customerSearch: customerSearch.trim() || undefined,
        documentSearch: documentSearch.trim() || undefined,
        status: apiStatus,
        page: nextPage,
        pageSize: nextPageSize,
      });
      const rows =
        nextStatus === PARTIAL_RETURN_STATUS
          ? result.items.filter((row) => matchesSaleStatusFilter(row, nextStatus))
          : result.items;
      setItems(rows);
      setTotal(nextStatus === PARTIAL_RETURN_STATUS ? rows.length : result.total);
      setPage(nextPage);
      setPageSize(nextPageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, appliedCustomer, appliedDocument, t]);

  const applySearch = (values: { customer: string; document: string }) => {
    const customer = values.customer.trim();
    const document = values.document.trim();
    setCustomerInput(customer);
    setDocumentInput(document);
    setAppliedCustomer(customer);
    setAppliedDocument(document);
  };

  const reloadList = useCallback(() => {
    void load(page, pageSize, statusFilter, appliedCustomer, appliedDocument);
  }, [load, page, pageSize, statusFilter, appliedCustomer, appliedDocument]);

  useEffect(() => {
    void searchCustomers()
      .then(setCustomers)
      .catch(() => {
        /* POS/search KH tùy chọn */
      });
  }, []);

  useEffect(() => {
    const onFocus = () => reloadList();
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, [reloadList]);

  useEffect(() => {
    void load(1, pageSize, statusFilter, appliedCustomer, appliedDocument);
  }, [statusFilter, pageSize, appliedCustomer, appliedDocument, load]);

  const customerSuggestions = useMemo(() => {
    const rows = [
      ...customers.map((customer) => ({
        customerName: customer.fullName,
        customerPhone: customer.phone,
      })),
      ...items.map((row) => ({
        customerName: row.customerName ?? '',
        customerPhone: null as string | null,
      })),
    ];
    return buildCustomerSearchSuggestions(rows, customerInput);
  }, [customers, items, customerInput]);

  const documentSuggestions = useMemo(
    () => buildDocumentSearchSuggestions(items.map((row) => row.orderNumber), documentInput),
    [items, documentInput],
  );

  const filteredItems = items;

  const resetFilters = () => {
    setCustomerInput('');
    setDocumentInput('');
    setAppliedCustomer('');
    setAppliedDocument('');
    setStatusFilter(undefined);
  };

  const loadOrderReturns = useCallback(async (orderId: string) => {
    setOrderReturnsLoading(true);
    try {
      setOrderReturns(await fetchOrderReturns(orderId));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.returnsLoadFailed')));
      setOrderReturns([]);
    } finally {
      setOrderReturnsLoading(false);
    }
  }, []);

  const openDetailById = useCallback(
    async (orderId: string) => {
      setDetailOpen(true);
      setDetail(null);
      setOrderReturns([]);
      setDetailLoading(true);
      try {
        const order = await fetchSalesOrder(orderId);
        setDetail(order);
        if (isCompletedSaleStatus(order.status) && (order.totalRefunded ?? 0) > 0) {
          await loadOrderReturns(orderId);
        }
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
        setDetailOpen(false);
      } finally {
        setDetailLoading(false);
      }
    },
    [loadOrderReturns, t],
  );

  const deepLinkHandled = useRef<string | null>(null);

  useEffect(() => {
    const orderId = searchParams.get('orderId');
    if (!orderId) {
      deepLinkHandled.current = null;
      return;
    }
    if (deepLinkHandled.current === orderId) return;
    deepLinkHandled.current = orderId;
    void openDetailById(orderId).finally(() => {
      deepLinkHandled.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [searchParams, openDetailById, setSearchParams]);

  const openDetail = async (row: SalesOrderListItem) => {
    await openDetailById(row.id);
  };

  const refreshDetail = async (id: string) => {
    const next = await fetchSalesOrder(id);
    setDetail(next);
    if (isCompletedSaleStatus(next.status) && (next.totalRefunded ?? 0) > 0) {
      await loadOrderReturns(id);
    } else {
      setOrderReturns([]);
    }
    void load(page, pageSize);
  };

  const handleCompleteDraft = () => {
    if (!detail) return;
    setDraftCustomerId(detail.customerId);
    setDraftCheckoutOpen(true);
  };

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
    setDraftCustomerId(customer.id);
    message.success(t('messages.customerSelected', { name: customer.fullName, code: customer.customerCode }));
  }, [t]);

  useEffect(() => {
    if (!draftCheckoutOpen || !draftCustomerId || !detail || detail.totalAmount <= 0) {
      setDraftCustomerLoyalty(null);
      return;
    }
    let cancelled = false;
    void (async () => {
      const loyalty = await fetchPosCustomerLoyalty(draftCustomerId, detail.totalAmount);
      if (!cancelled) setDraftCustomerLoyalty(loyalty);
    })();
    return () => {
      cancelled = true;
    };
  }, [draftCheckoutOpen, draftCustomerId, detail?.totalAmount]);

  const draftCustomer = useMemo(
    () => customers.find((c) => c.id === draftCustomerId),
    [customers, draftCustomerId],
  );

  const confirmCompleteDraft = async ({
    payments,
    loyaltyDiscountAmount,
    customerVoucherId,
    orderReminderLabel,
    orderReminderDaysSupply,
  }: PosCheckoutConfirm) => {
    if (!detail) {
      message.error(t('messages.draftNotFound'));
      throw new Error('missing-draft');
    }
    setDraftCompleting(true);
    try {
      const updated = await completeDraftSale(detail.id, {
        payments,
        customerId: draftCustomerId ?? detail.customerId ?? null,
        ...(loyaltyDiscountAmount != null && loyaltyDiscountAmount > 0
          ? { loyaltyDiscountAmount }
          : {}),
        ...(customerVoucherId ? { customerVoucherId } : {}),
        ...(orderReminderDaysSupply != null && orderReminderDaysSupply >= 1
          ? {
              orderReminderLabel: orderReminderLabel ?? null,
              orderReminderDaysSupply,
            }
          : {}),
      });
      setDetail(updated);
      setDraftCheckoutOpen(false);
      message.success(formatPosCheckoutSuccessMessage(updated));
      if (!(await printSalesInvoice(updated))) {
        message.warning(t('messages.printBlocked'));
      }
      void load(page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.completeFailed')));
      throw error;
    } finally {
      setDraftCompleting(false);
    }
  };

  const handleCancelDraft = async () => {
    if (!detail) return;
    try {
      await cancelDraftSale(detail.id);
      message.success(t('messages.cancelSuccess'));
      setDetailOpen(false);
      void load(page, pageSize);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    }
  };

  const printOrderById = async (id: string) => {
    try {
      if (!(await printSalesInvoice(await fetchSalesOrder(id)))) {
        message.warning(t('messages.printBlockedRetry'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.invoicePrintFailed')));
    }
  };

  const openReturnModal = () => {
    if (!detail) return;
    setReturnOpen(true);
  };

  const openReturnDetail = (returnId: string) => {
    setReturnDetailId(returnId);
    setReturnDetailOpen(true);
  };

  const printReturnById = async (returnId: string) => {
    try {
      if (!(await printSalesReturn(await fetchSalesReturn(returnId)))) {
        message.warning(t('messages.printBlockedRetry'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.returnPrintFailed')));
    }
  };

  const submitReturn = async (payload: {
    reason?: string;
    items: { salesOrderItemId: string; quantity: number }[];
    payments: { paymentMethod: number; amount: number }[];
  }) => {
    if (!detail) return;
    try {
      setReturnSaving(true);
      const ret = await createSaleReturn(detail.id, payload);
      message.success(t('messages.returnSuccess', { returnNumber: ret.returnNumber }));
      setReturnOpen(false);
      if (!(await printSalesReturn(ret))) {
        message.warning(t('messages.returnPrintBlocked'));
      }
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.returnFailed')));
    } finally {
      setReturnSaving(false);
    }
  };

  const renderOrderStatus = useCallback(
    (row: SalesOrderListItem & { items?: SalesOrderDetail['items'] }) => {
      const { label, color } = orderDisplayStatus(row);
      return <Tag color={color}>{label}</Tag>;
    },
    [orderDisplayStatus],
  );

  const columns: ColumnsType<SalesOrderListItem> = useMemo(
    () => [
      { title: t('columns.orderNumber'), dataIndex: 'orderNumber', width: 130 },
      { title: t('columns.warehouse'), dataIndex: 'warehouseName', width: 140 },
      { title: t('columns.customer'), dataIndex: 'customerName', render: (v) => v ?? '—' },
      {
        title: t('columns.date'),
        dataIndex: 'orderDate',
        width: 110,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 160,
        render: (_: number, row) => renderOrderStatus(row),
      },
      {
        title: t('columns.total'),
        dataIndex: 'totalAmount',
        width: 130,
        align: 'right',
        render: (v: number, row) => {
          const outstanding = row.outstanding ?? 0;
          return (
            <Space direction="vertical" size={0} style={{ alignItems: 'flex-end' }}>
              <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>
              {outstanding > 0.009 ? (
                <Tag color="orange" style={{ margin: 0, fontSize: 11 }}>
                  {t('columns.outstanding', { amount: formatDisplayMoney(outstanding) })}
                </Tag>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: t('columns.actions'),
        width: 130,
        render: (_, row) => (
          <Space size="small" onClick={(e) => e.stopPropagation()}>
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => void openDetail(row)}
            >
              {t('columns.view')}
            </Button>
            {isCompletedSaleStatus(row.status) && canRead && (
              <Button
                type="link"
                size="small"
                icon={<PrinterOutlined />}
                title={t('columns.printInvoice')}
                onClick={() => void printOrderById(row.id)}
              />
            )}
          </Space>
        ),
      },
    ],
    [canRead, openDetail, printOrderById, renderOrderStatus, t],
  );

  const lineColumns: ColumnsType<SalesOrderDetail['items'][number]> = useMemo(
    () => [
      { title: t('lines.productCode'), dataIndex: 'productCode', width: 72, ellipsis: true },
      { title: t('lines.productName'), dataIndex: 'productName', ellipsis: true },
      {
        title: t('lines.batch'),
        dataIndex: 'batchNumber',
        width: 76,
        ellipsis: true,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('lines.expiry'),
        dataIndex: 'expiryDate',
        width: 84,
        render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
      },
      { title: t('lines.unit'), dataIndex: 'unitName', width: 48, ellipsis: true },
      {
        title: t('lines.qty'),
        dataIndex: 'quantity',
        width: 56,
        align: 'right',
        render: (v: number) => v.toLocaleString(),
      },
      {
        title: t('lines.returnedQty'),
        dataIndex: 'returnedQuantity',
        width: 56,
        align: 'right',
        render: (v?: number) => (v ? v.toLocaleString() : '—'),
      },
      {
        title: t('lines.unitPrice'),
        dataIndex: 'unitPrice',
        width: 84,
        align: 'right',
        render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
      },
      {
        title: t('lines.lineTotal'),
        dataIndex: 'lineTotal',
        width: 92,
        align: 'right',
        render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
      },
    ],
    [t],
  );

  const returnColumns: ColumnsType<SalesReturnListItem> = useMemo(
    () => [
      {
        title: t('returns.returnNumber'),
        dataIndex: 'returnNumber',
        width: 120,
        render: (value: string, row) => (
          <Button
            type="link"
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              openReturnDetail(row.id);
            }}
          >
            {value}
          </Button>
        ),
      },
      {
        title: t('returns.date'),
        dataIndex: 'returnDate',
        width: 100,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('returns.status'),
        dataIndex: 'status',
        width: 90,
        render: (status: number) => <Tag>{returnStatusLabel(status)}</Tag>,
      },
      {
        title: t('returns.refundTotal'),
        dataIndex: 'totalRefund',
        width: 110,
        align: 'right',
        render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
      },
      {
        title: '',
        width: 120,
        render: (_, row) =>
          canRead ? (
            <Space size="small" onClick={(e) => e.stopPropagation()}>
              <Button
                type="link"
                size="small"
                icon={<EyeOutlined />}
                onClick={() => openReturnDetail(row.id)}
              >
                {t('returns.view')}
              </Button>
              <Button
                type="link"
                size="small"
                icon={<PrinterOutlined />}
                onClick={() => void printReturnById(row.id)}
              >
                {t('returns.print')}
              </Button>
            </Space>
          ) : null,
      },
    ],
    [canRead, openReturnDetail, printReturnById, returnStatusLabel, t],
  );

  const returnableLines =
    detail?.items.filter(
      (line) => line.batchId && line.quantity - (line.returnedQuantity ?? 0) > 0.0001,
    ) ?? [];

  const canCollectOrderDebt =
    Boolean(
      detail &&
        canWrite &&
        detail.customerId &&
        isCompletedSaleStatus(detail.status) &&
        resolveOrderPaymentSummary(detail).hasOutstanding,
    );

  const openCollectOrderDebt = () => {
    if (!detail?.customerId) {
      message.warning(t('messages.noCustomerForDebt'));
      return;
    }
    const { outstanding } = resolveOrderPaymentSummary(detail);
    setDetailOpen(false);
    navigate(
      buildCustomerPaymentCreateUrl({
        customerId: detail.customerId,
        salesOrderId: detail.id,
        amount: outstanding,
      }),
    );
  };

  return (
    <Card title={t('title')}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('appDraftAlert.message')}
        description={t('appDraftAlert.description')}
        action={
          <Button size="small" onClick={() => navigate('/sales/customer-drafts')}>
            {t('appDraftAlert.action')}
          </Button>
        }
      />
      <SalesListDualSearchWrap>
        <SalesListDualSearchBar
          customerValue={customerInput}
          documentValue={documentInput}
          onCustomerChange={setCustomerInput}
          onDocumentChange={setDocumentInput}
          onApply={applySearch}
          customerSuggestions={customerSuggestions}
          documentSuggestions={documentSuggestions}
          documentPlaceholder={t('filters.documentPlaceholder')}
        />
        <Select
          allowClear
          placeholder={t('filters.status')}
          style={{ width: 140 }}
          value={statusFilter}
          onChange={(value) => setStatusFilter(value)}
          options={saleStatusFilterOptions.map(({ value, label }) => ({ value, label }))}
        />
        <Button onClick={resetFilters}>{t('filters.clear')}</Button>
        <Button
          type="primary"
          ghost
          icon={<ReloadOutlined />}
          onClick={reloadList}
          loading={loading}
        >
          {t('filters.reload')}
        </Button>
      </SalesListDualSearchWrap>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={filteredItems}
        columns={columns}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          showTotal: (count) => t('paginationTotal', { count }),
          onChange: (nextPage, nextPageSize) => {
            void load(
              nextPage,
              nextPageSize ?? pageSize,
              statusFilter,
              appliedCustomer,
              appliedDocument,
            );
          },
        }}
        onRow={(record) => ({
          onClick: () => void openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={detail ? t('detail.drawerTitle', { orderNumber: detail.orderNumber }) : t('detail.drawerTitleDefault')}
        width={880}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        loading={detailLoading}
      >
        {detail && (
          <>
            {detail.status === 1 && (
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message={t('detail.draftAlert.message')}
                description={t('detail.draftAlert.description')}
              />
            )}

            {(detail.status === 1 && canWrite) ||
            (isCompletedSaleStatus(detail.status) && (canRead || canWrite)) ? (
              <Card size="small" title={t('detail.actions.title')} style={sectionGapStyle}>
                <Space wrap>
                  {detail.status === 1 && canWrite && (
                    <>
                      <Button
                        icon={<EditOutlined />}
                        onClick={() => {
                          setDetailOpen(false);
                          navigate(`/sales/pos?draftId=${detail.id}`);
                        }}
                      >
                        {t('detail.actions.continueEdit')}
                      </Button>
                      <Button type="primary" onClick={handleCompleteDraft}>
                        {t('detail.actions.complete')}
                      </Button>
                      <Popconfirm
                        title={t('detail.actions.cancelConfirm')}
                        onConfirm={() => void handleCancelDraft()}
                      >
                        <Button danger>{t('detail.actions.cancel')}</Button>
                      </Popconfirm>
                    </>
                  )}
                  {isCompletedSaleStatus(detail.status) && canRead && (
                    <Button icon={<PrinterOutlined />} onClick={() => void printSalesInvoice(detail)}>
                      {t('detail.actions.printInvoice')}
                    </Button>
                  )}
                  {isCompletedSaleStatus(detail.status) &&
                    canWrite &&
                    returnableLines.length > 0 && (
                      <Button icon={<RollbackOutlined />} onClick={openReturnModal}>
                        {t('detail.actions.return')}
                      </Button>
                    )}
                  {canCollectOrderDebt && (
                    <Button type="primary" icon={<DollarOutlined />} onClick={openCollectOrderDebt}>
                      {t('detail.actions.collectDebt')}
                    </Button>
                  )}
                </Space>
              </Card>
            ) : null}

            <Descriptions column={2} size="small" bordered style={sectionGapStyle}>
              <Descriptions.Item label={t('detail.descriptions.warehouse')}>
                {detail.warehouseName}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.customer')}>
                {detail.customerName ?? '—'}
              </Descriptions.Item>
              {(detail.voucherDiscountAmount ?? 0) > 0 ? (
                <Descriptions.Item label={t('detail.descriptions.voucher')}>
                  {detail.voucherCode ? `${detail.voucherCode} · ` : ''}
                  −{formatDisplayMoney(detail.voucherDiscountAmount ?? 0)}
                </Descriptions.Item>
              ) : null}
              {(detail.loyaltyPointsRedeemed ?? 0) > 0 ? (
                <Descriptions.Item label={t('detail.descriptions.pointsRedeem')}>
                  −{detail.loyaltyPointsRedeemed!.toLocaleString()}{' '}
                  {t('detail.descriptions.pointsUnit')} (−
                  {formatDisplayMoney(detail.loyaltyDiscountAmount ?? 0)})
                </Descriptions.Item>
              ) : null}
              {(detail.loyaltyPointsEarned ?? 0) > 0 ? (
                <Descriptions.Item label={t('detail.descriptions.pointsEarned')}>
                  +{detail.loyaltyPointsEarned!.toLocaleString()}{' '}
                  {t('detail.descriptions.pointsUnit')}
                </Descriptions.Item>
              ) : null}
              <Descriptions.Item label={t('detail.descriptions.status')}>
                {renderOrderStatus({
                  ...detail,
                  itemCount: detail.items.length,
                  items: detail.items,
                })}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.date')}>
                {formatDisplayDate(detail.orderDate)}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.shift')}>
                {detail.shiftNumber ?? '—'}
              </Descriptions.Item>
            </Descriptions>

            <OrderDetailFinancials
              order={detail}
              onCollectDebt={canCollectOrderDebt ? openCollectOrderDebt : undefined}
            />

            {isCompletedSaleStatus(detail.status) ? (
              <DispensingNotesPanel
                salesOrderId={detail.id}
                customerId={detail.customerId}
                readOnly={!canWrite}
              />
            ) : null}

            <Table
              rowKey="id"
              size="small"
              pagination={false}
              tableLayout="fixed"
              style={{ width: '100%' }}
              dataSource={detail.items}
              columns={lineColumns}
            />

            {orderReturns.length > 0 || orderReturnsLoading ? (
              <Card
                size="small"
                title={t('returns.title')}
                style={sectionGapTopStyle}
                loading={orderReturnsLoading}
              >
                <Table
                  rowKey="id"
                  size="small"
                  pagination={false}
                  dataSource={orderReturns}
                  columns={returnColumns}
                  onRow={(record) => ({
                    onClick: () => openReturnDetail(record.id),
                    style: { cursor: 'pointer' },
                  })}
                />
              </Card>
            ) : null}
          </>
        )}
      </Drawer>

      <SalesReturnModal
        open={returnOpen}
        loading={returnSaving}
        order={detail}
        onCancel={() => setReturnOpen(false)}
        onConfirm={(payload) => void submitReturn(payload)}
      />

      <SalesReturnDetailDrawer
        open={returnDetailOpen}
        returnId={returnDetailId}
        onClose={() => {
          setReturnDetailOpen(false);
          setReturnDetailId(null);
        }}
        onOpenOrder={(orderId) => {
          setReturnDetailOpen(false);
          setReturnDetailId(null);
          navigate(`/sales/orders?orderId=${orderId}`);
        }}
      />

      {detail && (
        <PosCheckoutModal
          open={draftCheckoutOpen}
          loading={draftCompleting}
          totalAmount={detail.totalAmount}
          subtotalGross={detail.subtotal}
          lineDiscountTotal={
            detail.lineDiscountTotal ??
            detail.items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0)
          }
          orderDiscountAmount={detail.discountAmount}
          customerId={draftCustomerId}
          customers={customers}
          onCustomerChange={setDraftCustomerId}
          onQuickAddCustomer={canWrite ? () => setQuickCustomerOpen(true) : undefined}
          customerAllowCredit={draftCustomer?.allowCredit}
          customerCreditLimit={draftCustomer?.creditLimit}
          customerCurrentOutstanding={draftCustomer?.currentOutstanding}
          customerLoyalty={draftCustomerLoyalty}
          onCancel={() => setDraftCheckoutOpen(false)}
          onConfirm={(result) => confirmCompleteDraft(result)}
        />
      )}

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
