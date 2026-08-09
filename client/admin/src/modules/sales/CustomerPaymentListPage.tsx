import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  DatePicker,
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
  PlusOutlined,
  EyeOutlined,
  EditOutlined,
  CheckOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  parseCustomerPaymentPrefill,
  type CustomerPaymentPrefill,
} from '@/modules/sales/customer-payment-nav';
import { CustomerPaymentFormDrawer } from '@/modules/sales/CustomerPaymentFormDrawer';
import {
  buildCustomerSearchSuggestions,
  buildDocumentSearchSuggestions,
} from '@/modules/sales/sales-list-customer-search';
import { SalesListDualSearchBar, SalesListDualSearchWrap } from '@/modules/sales/SalesListDualSearchBar';
import {
  cancelCustomerPayment,
  fetchCustomerPayments,
  postCustomerPayment,
  searchCustomers,
} from '@/shared/api/sales.api';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  CustomerListItem,
  CustomerPaymentListFilters,
  CustomerPaymentListItem,
} from '@/shared/api/sales.types';
import { CUSTOMER_PAYMENT_STATUS_TAG } from '@/shared/api/sales.types';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

const emptyFilters: CustomerPaymentListFilters = {};
const tableScroll = { x: 1000 };
const alertStyle = { marginBottom: 16 };
const statusSelectStyle = { width: 140 };
const methodSelectStyle = { width: 160 };
const warehouseSelectStyle = { width: 200 };
const tableWrapClassName = 'sales-list-table-wrap';

function CustomerPaymentListPageInner() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerPayments' });
  const canWrite = useHasPermission('sales.write');
  const {
    paymentMethodLabel,
    customerPaymentStatusLabel,
    customerPaymentStatusOptions,
    collectionPaymentMethodOptions,
  } = useSalesEnums();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillHandled = useRef(false);
  const loadSeqRef = useRef(0);
  const [items, setItems] = useState<CustomerPaymentListItem[]>([]);
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<CustomerPaymentListFilters>(emptyFilters);
  const [customerInput, setCustomerInput] = useState('');
  const [documentInput, setDocumentInput] = useState('');
  const [appliedCustomer, setAppliedCustomer] = useState('');
  const [appliedDocument, setAppliedDocument] = useState('');
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [formPrefill, setFormPrefill] = useState<CustomerPaymentPrefill | undefined>();
  const [detailOpen, setDetailOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingRow, setEditingRow] = useState<CustomerPaymentListItem | null>(null);
  const [detail, setDetail] = useState<CustomerPaymentListItem | null>(null);
  const [saving, setSaving] = useState(false);
  const [referenceReady, setReferenceReady] = useState(false);

  const tablePagination = useMemo(
    () => ({
      pageSize: 20,
      showTotal: (total: number) => t('paginationTotal', { count: total }),
    }),
    [t],
  );

  const loadPayments = useCallback(
    async (
      nextFilters: CustomerPaymentListFilters,
      customerSearch: string,
      documentSearch: string,
      nextDateRange: [Dayjs, Dayjs] | null,
    ) => {
      const seq = ++loadSeqRef.current;
      try {
        const hasLookup = Boolean(customerSearch.trim() || documentSearch.trim());
        const useDate = !hasLookup && nextDateRange != null;
        const rows = await fetchCustomerPayments({
          ...nextFilters,
          customerSearch: customerSearch.trim() || undefined,
          documentSearch: documentSearch.trim() || undefined,
          dateFrom: useDate ? nextDateRange![0].format('YYYY-MM-DD') : undefined,
          dateTo: useDate ? nextDateRange![1].format('YYYY-MM-DD') : undefined,
        });
        if (seq !== loadSeqRef.current) return;
        setFilters(nextFilters);
        setAppliedCustomer(customerSearch);
        setAppliedDocument(documentSearch);
        setItems(rows);
      } catch (error) {
        if (seq !== loadSeqRef.current) return;
        message.error(apiErrorMessage(error, t('messages.loadFailed')));
      }
    },
    [t],
  );

  useEffect(() => {
    let cancelled = false;
    void Promise.all([searchCustomers(), fetchWarehouses()])
      .then(([customerRows, warehouseRows]) => {
        if (!cancelled) {
          setCustomers(customerRows);
          setWarehouses(warehouseRows);
          setReferenceReady(true);
        }
      })
      .catch(() => {
        if (!cancelled) setReferenceReady(true);
      });
    void loadPayments(emptyFilters, '', '', null);
    return () => {
      cancelled = true;
      loadSeqRef.current += 1;
    };
  }, [loadPayments]);

  useEffect(() => {
    if (prefillHandled.current || !referenceReady) return;
    const prefill = parseCustomerPaymentPrefill(searchParams);
    if (!prefill) return;

    prefillHandled.current = true;
    if (!canWrite) {
      message.warning(t('messages.noPermission'));
      navigate('/receivables/customer-payments', { replace: true });
      return;
    }

    setFormPrefill(prefill);
    setEditingId(null);
    setEditingRow(null);
    setFormOpen(true);
    navigate('/receivables/customer-payments', { replace: true });
  }, [searchParams, referenceReady, canWrite, navigate, t]);

  const customerSuggestions = useMemo(() => {
    const rows = [
      ...customers.map((customer) => ({
        customerName: customer.fullName,
        customerPhone: customer.phone,
      })),
      ...items.map((row) => ({
        customerName: row.customerName,
        customerPhone: null as string | null,
      })),
    ];
    return buildCustomerSearchSuggestions(rows, customerInput);
  }, [customers, items, customerInput]);

  const documentSuggestions = useMemo(() => {
    const numbers = items.flatMap((row) =>
      [row.paymentNumber, row.orderNumber].filter((value): value is string => Boolean(value)),
    );
    return buildDocumentSearchSuggestions(numbers, documentInput);
  }, [items, documentInput]);

  const applySearch = useCallback(
    (values: { customer: string; document: string }) => {
      const customer = values.customer.trim();
      const document = values.document.trim();
      setCustomerInput(customer);
      setDocumentInput(document);
      void loadPayments(filters, customer, document, dateRange);
    },
    [filters, loadPayments, dateRange],
  );

  const resetFilters = useCallback(() => {
    setCustomerInput('');
    setDocumentInput('');
    setDateRange(null);
    void loadPayments(emptyFilters, '', '', null);
  }, [loadPayments]);

  const reloadList = useCallback(() => {
    void loadPayments(filters, appliedCustomer, appliedDocument, dateRange);
  }, [loadPayments, filters, appliedCustomer, appliedDocument, dateRange]);

  const openCreate = useCallback(() => {
    setFormPrefill(undefined);
    setEditingId(null);
    setEditingRow(null);
    setFormOpen(true);
  }, []);

  const closeFormDrawer = useCallback(() => {
    setFormOpen(false);
    setFormPrefill(undefined);
    setEditingId(null);
    setEditingRow(null);
  }, []);

  const openEdit = useCallback((row: CustomerPaymentListItem) => {
    setFormPrefill(undefined);
    setEditingId(row.id);
    setEditingRow(row);
    setDetailOpen(false);
    setFormOpen(true);
  }, []);

  const openDetail = useCallback((row: CustomerPaymentListItem) => {
    setDetail(row);
    setDetailOpen(true);
  }, []);

  const handleFormSaved = useCallback(
    (saved: CustomerPaymentListItem) => {
      closeFormDrawer();
      setDetail(saved);
      setDetailOpen(true);
      void loadPayments(filters, appliedCustomer, appliedDocument, dateRange);
    },
    [closeFormDrawer, loadPayments, filters, appliedCustomer, appliedDocument, dateRange],
  );

  const handlePost = async (id: string) => {
    try {
      setSaving(true);
      const updated = await postCustomerPayment(id);
      message.success(t('messages.postSuccess'));
      setDetail(updated);
      void loadPayments(filters, appliedCustomer, appliedDocument, dateRange);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.postFailed')));
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async (id: string) => {
    try {
      setSaving(true);
      await cancelCustomerPayment(id);
      message.success(t('messages.cancelSuccess'));
      setDetailOpen(false);
      void loadPayments(filters, appliedCustomer, appliedDocument, dateRange);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    } finally {
      setSaving(false);
    }
  };

  const columns = useMemo<ColumnsType<CustomerPaymentListItem>>(
    () => [
      { title: t('columns.paymentNumber'), dataIndex: 'paymentNumber', width: 130 },
      { title: t('columns.customer'), dataIndex: 'customerName' },
      {
        title: t('columns.amount'),
        dataIndex: 'amount',
        width: 120,
        align: 'right',
        render: (v: number) => (
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
        ),
      },
      {
        title: t('columns.paymentMethod'),
        dataIndex: 'paymentMethod',
        width: 120,
        render: (m: number) => paymentMethodLabel(m),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (s: number) => (
          <Tag color={CUSTOMER_PAYMENT_STATUS_TAG[s] ?? 'default'}>
            {customerPaymentStatusLabel(s)}
          </Tag>
        ),
      },
      {
        title: t('columns.paymentDate'),
        dataIndex: 'paymentDate',
        width: 110,
        render: (v: string) => formatDisplayDate(v),
      },
      {
        title: t('columns.orderNumber'),
        dataIndex: 'orderNumber',
        width: 120,
        render: (v) => v ?? '—',
      },
      {
        title: '',
        width: 90,
        render: (_, row) => (
          <Button
            type="link"
            size="small"
            icon={<EyeOutlined />}
            onClick={(e) => {
              e.stopPropagation();
              openDetail(row);
            }}
          >
            {t('columns.view')}
          </Button>
        ),
      },
    ],
    [customerPaymentStatusLabel, openDetail, paymentMethodLabel, t],
  );

  const handleRowClick = useCallback(
    (record: CustomerPaymentListItem) => ({
      onClick: () => openDetail(record),
      style: { cursor: 'pointer' } as const,
    }),
    [openDetail],
  );

  const handleStatusChange = useCallback(
    (status?: number) => {
      void loadPayments({ ...filters, status }, appliedCustomer, appliedDocument, dateRange);
    },
    [loadPayments, filters, appliedCustomer, appliedDocument, dateRange],
  );

  const handlePaymentMethodChange = useCallback(
    (paymentMethod?: number) => {
      void loadPayments({ ...filters, paymentMethod }, appliedCustomer, appliedDocument, dateRange);
    },
    [loadPayments, filters, appliedCustomer, appliedDocument, dateRange],
  );

  const handleWarehouseChange = useCallback(
    (warehouseId?: string) => {
      void loadPayments({ ...filters, warehouseId }, appliedCustomer, appliedDocument, dateRange);
    },
    [loadPayments, filters, appliedCustomer, appliedDocument, dateRange],
  );

  const handleDateRangeChange = useCallback(
    (value: [Dayjs | null, Dayjs | null] | null) => {
      const next =
        value?.[0] && value[1]
          ? ([value[0].startOf('day'), value[1].endOf('day')] as [Dayjs, Dayjs])
          : null;
      setDateRange(next);
      void loadPayments(filters, appliedCustomer, appliedDocument, next);
    },
    [loadPayments, filters, appliedCustomer, appliedDocument],
  );

  return (
    <Card
      title={t('title')}
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!canWrite}>
          {t('create')}
        </Button>
      }
    >
      <Alert type="info" showIcon style={alertStyle} message={t('alert.message')} />
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
        <DatePicker.RangePicker
          allowClear
          value={dateRange}
          onChange={handleDateRangeChange}
          style={{ width: 260 }}
          format="DD/MM/YYYY"
          placeholder={[t('filters.dateFrom'), t('filters.dateTo')]}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={t('filters.warehouse')}
          style={warehouseSelectStyle}
          value={filters.warehouseId}
          onChange={handleWarehouseChange}
          options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
        />
        <Select
          allowClear
          placeholder={t('filters.status')}
          style={statusSelectStyle}
          value={filters.status}
          onChange={handleStatusChange}
          options={customerPaymentStatusOptions}
        />
        <Select
          allowClear
          placeholder={t('filters.paymentMethod')}
          style={methodSelectStyle}
          value={filters.paymentMethod}
          onChange={handlePaymentMethodChange}
          options={collectionPaymentMethodOptions}
        />
        <Button onClick={resetFilters}>{t('filters.clear')}</Button>
        <Button type="primary" ghost icon={<ReloadOutlined />} onClick={reloadList}>
          {t('filters.reload')}
        </Button>
      </SalesListDualSearchWrap>

      <div className={tableWrapClassName}>
        <Table
          rowKey="id"
          columns={columns}
          dataSource={items}
          pagination={tablePagination}
          scroll={tableScroll}
          onRow={handleRowClick}
        />
      </div>

      {formOpen ? (
        <CustomerPaymentFormDrawer
          open
          editingId={editingId}
          editingRow={editingRow}
          customers={customers}
          prefill={formPrefill}
          onClose={closeFormDrawer}
          onSaved={handleFormSaved}
        />
      ) : null}

      <Drawer
        title={detail ? detail.paymentNumber : t('detail.drawerTitleDefault')}
        width={480}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && canWrite ? (
            <Space>
              {detail.status === 1 ? (
                <>
                  <Button icon={<EditOutlined />} onClick={() => openEdit(detail)}>
                    {t('detail.edit')}
                  </Button>
                  <Popconfirm
                    title={t('detail.postConfirm')}
                    onConfirm={() => void handlePost(detail.id)}
                  >
                    <Button type="primary" icon={<CheckOutlined />} loading={saving}>
                      {t('detail.post')}
                    </Button>
                  </Popconfirm>
                  <Popconfirm
                    title={t('detail.cancelConfirm')}
                    onConfirm={() => void handleCancel(detail.id)}
                  >
                    <Button danger icon={<CloseCircleOutlined />} loading={saving}>
                      {t('detail.cancel')}
                    </Button>
                  </Popconfirm>
                </>
              ) : null}
            </Space>
          ) : undefined
        }
      >
        {detail ? (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('detail.customer')}>{detail.customerName}</Descriptions.Item>
            <Descriptions.Item label={t('detail.amount')}>{formatDisplayMoney(detail.amount)}</Descriptions.Item>
            <Descriptions.Item label={t('detail.paymentMethod')}>
              {paymentMethodLabel(detail.paymentMethod)}
            </Descriptions.Item>
            <Descriptions.Item label={t('detail.status')}>
              <Tag color={CUSTOMER_PAYMENT_STATUS_TAG[detail.status] ?? 'default'}>
                {customerPaymentStatusLabel(detail.status)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('detail.paymentDate')}>
              {formatDisplayDate(detail.paymentDate)}
            </Descriptions.Item>
            <Descriptions.Item label={t('detail.orderNumber')}>{detail.orderNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={t('detail.notes')}>{detail.notes ?? '—'}</Descriptions.Item>
          </Descriptions>
        ) : null}
      </Drawer>
    </Card>
  );
}

export const CustomerPaymentListPage = memo(CustomerPaymentListPageInner);
