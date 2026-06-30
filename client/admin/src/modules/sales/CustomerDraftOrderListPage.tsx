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
import { EyeOutlined, ReloadOutlined, ShoppingCartOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  cancelCustomerDraftOrder,
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_DRAFT_ORDER_STATUS_COLORS,
  fetchCustomerDraftOrder,
  fetchCustomerDraftOrders,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
} from '@/shared/api/customer-draft-orders.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { CustomerDraftOrderStatusBar } from '@/modules/sales/CustomerDraftOrderStatusBar';
import { sectionGapStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
import {
  buildCustomerSearchSuggestions,
  buildDocumentSearchSuggestions,
  matchesSalesListDualSearch,
} from '@/modules/sales/sales-list-customer-search';
import { SalesListDualSearchBar, SalesListDualSearchWrap } from '@/modules/sales/SalesListDualSearchBar';
import { formatDisplayMoney } from '@/shared/utils/money';

const ACTIVE_STATUSES: number[] = [
  CUSTOMER_DRAFT_ORDER_STATUS.Draft,
  CUSTOMER_DRAFT_ORDER_STATUS.Sent,
  CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
];

function isActiveDraftStatus(status: number) {
  return ACTIVE_STATUSES.includes(status);
}

function isActionableStatus(status: number): boolean {
  return (
    status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed
  );
}

export function CustomerDraftOrderListPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerDrafts' });
  const { customerDraftStatusLabel, customerDraftStatusOptions } = useSalesEnums();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<CustomerDraftOrderListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [customerQuery, setCustomerQuery] = useState('');
  const [documentQuery, setDocumentQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [activeOnly, setActiveOnly] = useState(false);
  const [actionableOnly, setActionableOnly] = useState(
    () =>
      searchParams.get('actionable') === '1' || searchParams.get('actionable') === 'true',
  );
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<CustomerDraftOrder | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchCustomerDraftOrders());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setActionableOnly(
      searchParams.get('actionable') === '1' || searchParams.get('actionable') === 'true',
    );
  }, [searchParams]);

  useEffect(() => {
    const onFocus = () => void load();
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, [load]);

  useEffect(() => {
    const hasPending = items.some((row) => row.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent);
    if (!hasPending) return;
    const timer = window.setInterval(() => void load(), 15_000);
    return () => window.clearInterval(timer);
  }, [items, load]);

  const customerSuggestions = useMemo(
    () => buildCustomerSearchSuggestions(items, customerQuery),
    [items, customerQuery],
  );

  const documentSuggestions = useMemo(
    () => buildDocumentSearchSuggestions(items.map((row) => row.draftNumber), documentQuery),
    [items, documentQuery],
  );

  const filteredItems = useMemo(() => {
    return items.filter((row) => {
      if (actionableOnly && !isActionableStatus(row.status)) return false;
      if (activeOnly && !isActiveDraftStatus(row.status)) return false;
      if (statusFilter != null && row.status !== statusFilter) return false;
      return matchesSalesListDualSearch(
        { customerQuery, documentQuery },
        {
          customerName: row.customerName,
          customerPhone: row.customerPhone,
          documentNumbers: [row.draftNumber],
        },
      );
    });
  }, [items, customerQuery, documentQuery, statusFilter, activeOnly, actionableOnly]);

  const clearSearch = () => {
    setCustomerQuery('');
    setDocumentQuery('');
  };

  const openDetailById = useCallback(
    async (draftOrderId: string) => {
      setDetailOpen(true);
      setDetail(null);
      setDetailLoading(true);
      try {
        setDetail(await fetchCustomerDraftOrder(draftOrderId));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
        setDetailOpen(false);
      } finally {
        setDetailLoading(false);
      }
    },
    [t],
  );

  const deepLinkHandled = useRef<string | null>(null);

  useEffect(() => {
    const draftOrderId = searchParams.get('draftOrderId');
    if (!draftOrderId) {
      deepLinkHandled.current = null;
      return;
    }
    if (deepLinkHandled.current === draftOrderId) return;
    deepLinkHandled.current = draftOrderId;
    void openDetailById(draftOrderId).finally(() => {
      deepLinkHandled.current = null;
      setSearchParams({}, { replace: true });
    });
  }, [searchParams, openDetailById, setSearchParams]);

  const openDetail = async (row: CustomerDraftOrderListItem) => {
    await openDetailById(row.id);
  };

  const loadIntoPos = (draftOrderId: string) => {
    setDetailOpen(false);
    navigate(`/sales/pos?customerDraftId=${draftOrderId}&checkout=1`);
  };

  const handleCancel = async (draftOrderId: string) => {
    try {
      await cancelCustomerDraftOrder(draftOrderId);
      message.success(t('messages.cancelSuccess'));
      setDetailOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.cancelFailed')));
    }
  };

  const lineColumns = useMemo(
    () => [
      { title: t('detail.lines.productCode'), dataIndex: 'productCode', width: 100 },
      { title: t('detail.lines.productName'), dataIndex: 'productName', ellipsis: true },
      { title: t('detail.lines.unit'), dataIndex: 'unitName', width: 72 },
      { title: t('detail.lines.qty'), dataIndex: 'quantity', width: 56, align: 'right' as const },
      {
        title: t('detail.lines.unitPrice'),
        dataIndex: 'unitPrice',
        width: 100,
        align: 'right' as const,
        render: (v: number) => formatDisplayMoney(v),
      },
      {
        title: t('detail.lines.lineTotal'),
        dataIndex: 'lineAmount',
        width: 110,
        align: 'right' as const,
        render: (v: number) => formatDisplayMoney(v),
      },
    ],
    [t],
  );

  const columns: ColumnsType<CustomerDraftOrderListItem> = useMemo(
    () => [
      {
        title: t('columns.draftNumber'),
        dataIndex: 'draftNumber',
        width: 130,
      },
      {
        title: t('columns.customer'),
        dataIndex: 'customerName',
        ellipsis: true,
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 170,
        render: (status: number, row) => (
          <Space size={4} wrap>
            <Tag color={CUSTOMER_DRAFT_ORDER_STATUS_COLORS[status] ?? 'default'}>
              {customerDraftStatusLabel(status)}
            </Tag>
            {row.hiddenByCustomerAt ? (
              <Tag color="default">{t('columns.hiddenOnApp')}</Tag>
            ) : null}
          </Space>
        ),
      },
      {
        title: t('columns.itemCount'),
        dataIndex: 'itemCount',
        width: 56,
        align: 'center',
      },
      {
        title: t('columns.total'),
        dataIndex: 'totalAmount',
        width: 120,
        align: 'right',
        render: (value: number) => <TabularMoney>{formatDisplayMoney(value)}</TabularMoney>,
      },
      {
        title: t('columns.timeline'),
        key: 'timeline',
        width: 150,
        render: (_, row) => {
          if (row.confirmedAt) {
            return (
              <span style={{ fontSize: 12, color: '#059669' }}>
                {t('columns.confirmedAt', {
                  time: dayjs(row.confirmedAt).format('DD/MM HH:mm'),
                })}
              </span>
            );
          }
          if (row.sentAt) {
            return (
              <span style={{ fontSize: 12, color: '#64748b' }}>
                {t('columns.sentAt', { time: dayjs(row.sentAt).format('DD/MM HH:mm') })}
              </span>
            );
          }
          return '—';
        },
      },
      {
        title: t('columns.actions'),
        key: 'actions',
        width: 160,
        render: (_, row) => (
          <Space size={4} onClick={(e) => e.stopPropagation()}>
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => void openDetail(row)}
            >
              {t('columns.view')}
            </Button>
            {isActionableStatus(row.status) && canWrite ? (
              <Button
                type="link"
                size="small"
                icon={<ShoppingCartOutlined />}
                onClick={() => loadIntoPos(row.id)}
              >
                {t('columns.loadPos')}
              </Button>
            ) : null}
          </Space>
        ),
      },
    ],
    [canWrite, customerDraftStatusLabel, t],
  );

  const pendingCount = items.filter((row) => isActionableStatus(row.status)).length;
  const activeCount = items.filter((row) => isActiveDraftStatus(row.status)).length;

  return (
    <Card title={t('title')}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('alert.message')}
        description={t('alert.description')}
      />

      <SalesListDualSearchWrap>
        <SalesListDualSearchBar
          customerValue={customerQuery}
          documentValue={documentQuery}
          onCustomerChange={setCustomerQuery}
          onDocumentChange={setDocumentQuery}
          onApply={(values) => {
            setCustomerQuery(values.customer);
            setDocumentQuery(values.document);
          }}
          onClear={clearSearch}
          customerSuggestions={customerSuggestions}
          documentSuggestions={documentSuggestions}
          documentPlaceholder={t('filters.documentPlaceholder')}
          liveFilter
          showApplyButton={false}
        />
        <Select
          allowClear
          placeholder={t('filters.status')}
          style={{ width: 180 }}
          value={statusFilter}
          onChange={(value) => {
            setStatusFilter(value);
            setActiveOnly(false);
          }}
          options={customerDraftStatusOptions}
        />
        <Button
          onClick={() => {
            clearSearch();
            setStatusFilter(undefined);
            setActiveOnly(false);
            setActionableOnly(false);
          }}
        >
          {t('filters.clear')}
        </Button>
        <Button
          onClick={() => {
            setActionableOnly((prev) => !prev);
            setStatusFilter(undefined);
            setActiveOnly(false);
          }}
          type={actionableOnly ? 'primary' : 'default'}
          ghost={actionableOnly}
        >
          {t('filters.actionable', { count: pendingCount })}
        </Button>
        <Button
          onClick={() => {
            setActiveOnly((prev) => !prev);
            setStatusFilter(undefined);
          }}
          type={activeOnly ? 'primary' : 'default'}
          ghost={activeOnly}
        >
          {t('filters.active', { count: activeCount })}
        </Button>
        <Button type="primary" ghost icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('filters.reload')}
        </Button>
      </SalesListDualSearchWrap>

      {pendingCount > 0 ? (
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('pendingAlert.message', { count: pendingCount })}
          description={t('pendingAlert.description')}
        />
      ) : null}

      <Table
        rowKey="id"
        loading={loading}
        dataSource={filteredItems}
        columns={columns}
        pagination={{ pageSize: 20, showTotal: (total) => t('paginationTotal', { count: total }) }}
        onRow={(record) => ({
          onClick: () => void openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={
          detail ? t('detail.drawerTitle', { draftNumber: detail.draftNumber }) : t('detail.drawerTitleDefault')
        }
        width={720}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        loading={detailLoading}
      >
        {detail ? (
          <>
            <CustomerDraftOrderStatusBar draft={detail} />

            {isActionableStatus(detail.status) ? (
              <Alert
                type="success"
                showIcon
                style={{ marginBottom: 16 }}
                message={
                  detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed
                    ? t('detail.confirmedAlert.message')
                    : t('detail.sentAlert.message')
                }
                description={
                  detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed
                    ? t('detail.confirmedAlert.description')
                    : t('detail.sentAlert.description')
                }
              />
            ) : null}

            {(isActionableStatus(detail.status) && canWrite) || detail.salesOrderNumber ? (
              <Card size="small" title={t('detail.actions.title')} style={sectionGapStyle}>
                <Space wrap>
                  {isActionableStatus(detail.status) && canWrite ? (
                    <>
                      <Button
                        type="primary"
                        icon={<ShoppingCartOutlined />}
                        onClick={() => loadIntoPos(detail.id)}
                      >
                        {t('detail.actions.loadPos')}
                      </Button>
                      <Popconfirm
                        title={t('detail.actions.cancelConfirm')}
                        onConfirm={() => void handleCancel(detail.id)}
                      >
                        <Button danger>{t('detail.actions.cancel')}</Button>
                      </Popconfirm>
                    </>
                  ) : null}
                  {detail.salesOrderId && detail.salesOrderNumber ? (
                    <Button
                      type="link"
                      onClick={() => navigate(`/sales/orders?orderId=${detail.salesOrderId}`)}
                    >
                      {t('detail.actions.viewOrder', { orderNumber: detail.salesOrderNumber })}
                    </Button>
                  ) : null}
                </Space>
              </Card>
            ) : null}

            <Descriptions bordered size="small" column={2} style={sectionGapStyle}>
              <Descriptions.Item label={t('detail.descriptions.customer')}>
                {detail.customerName}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.phone')}>
                {detail.customerPhone ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.status')}>
                <Tag color={CUSTOMER_DRAFT_ORDER_STATUS_COLORS[detail.status] ?? 'default'}>
                  {customerDraftStatusLabel(detail.status)}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.descriptions.total')}>
                {formatDisplayMoney(detail.totalAmount)}
              </Descriptions.Item>
              {detail.sentAt ? (
                <Descriptions.Item label={t('detail.descriptions.sentAt')}>
                  {dayjs(detail.sentAt).format('DD-MM-YYYY HH:mm')}
                </Descriptions.Item>
              ) : null}
              {detail.confirmedAt ? (
                <Descriptions.Item label={t('detail.descriptions.confirmedAt')}>
                  {dayjs(detail.confirmedAt).format('DD-MM-YYYY HH:mm')}
                </Descriptions.Item>
              ) : null}
              {detail.salesOrderNumber ? (
                <Descriptions.Item label={t('detail.descriptions.salesOrder')}>
                  {detail.salesOrderNumber}
                </Descriptions.Item>
              ) : null}
            </Descriptions>

            <Table
              style={{ marginTop: 16 }}
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={lineColumns}
            />
          </>
        ) : null}
      </Drawer>
    </Card>
  );
}
