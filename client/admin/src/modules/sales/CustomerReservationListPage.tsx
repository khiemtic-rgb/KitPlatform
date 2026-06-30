import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Descriptions,
  Drawer,
  Input,
  List,
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
  confirmCustomerReservation,
  CUSTOMER_RESERVATION_STATUS,
  CUSTOMER_RESERVATION_STATUS_COLORS,
  fetchCustomerReservation,
  fetchCustomerReservations,
  markCustomerReservationReady,
  rejectCustomerReservation,
  updateCustomerReservationStaffNotes,
  type CustomerReservation,
  type CustomerReservationStaffListItem,
} from '@/shared/api/customer-reservations.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { sectionGapStyle } from '@/modules/sales/sales-ui-styles';
import {
  buildCustomerSearchSuggestions,
  buildDocumentSearchSuggestions,
  matchesSalesListDualSearch,
} from '@/modules/sales/sales-list-customer-search';
import { SalesListDualSearchBar, SalesListDualSearchWrap } from '@/modules/sales/SalesListDualSearchBar';

const AWAITING_STATUSES: number[] = [
  CUSTOMER_RESERVATION_STATUS.Pending,
  CUSTOMER_RESERVATION_STATUS.Confirmed,
  CUSTOMER_RESERVATION_STATUS.Ready,
];

export function CustomerReservationListPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'customerReservations' });
  const { reservationStatusLabel, reservationStatusOptions, reservationFulfillmentLabel } = useSalesEnums();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<CustomerReservationStaffListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [awaitingOnly, setAwaitingOnly] = useState(
    () => searchParams.get('awaiting') === '1' || searchParams.get('awaiting') === 'true',
  );
  const [customerQuery, setCustomerQuery] = useState('');
  const [documentQuery, setDocumentQuery] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<CustomerReservation | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [staffNotes, setStaffNotes] = useState('');
  const [savingNotes, setSavingNotes] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchCustomerReservations());
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
    setAwaitingOnly(
      searchParams.get('awaiting') === '1' || searchParams.get('awaiting') === 'true',
    );
  }, [searchParams]);

  useEffect(() => {
    const hasPending = items.some((row) => row.status === CUSTOMER_RESERVATION_STATUS.Pending);
    if (!hasPending) return;
    const timer = window.setInterval(() => void load(), 30_000);
    return () => window.clearInterval(timer);
  }, [items, load]);

  const customerSuggestions = useMemo(
    () => buildCustomerSearchSuggestions(items, customerQuery),
    [items, customerQuery],
  );

  const documentSuggestions = useMemo(
    () => buildDocumentSearchSuggestions(items.map((row) => row.reservationNumber), documentQuery),
    [items, documentQuery],
  );

  const filteredItems = useMemo(() => {
    return items.filter((row) => {
      if (awaitingOnly && !AWAITING_STATUSES.includes(row.status)) return false;
      if (statusFilter != null && row.status !== statusFilter) return false;
      return matchesSalesListDualSearch(
        { customerQuery, documentQuery },
        {
          customerName: row.customerName,
          customerPhone: row.customerPhone,
          documentNumbers: [row.reservationNumber],
        },
      );
    });
  }, [items, customerQuery, documentQuery, statusFilter, awaitingOnly]);

  const clearSearch = () => {
    setCustomerQuery('');
    setDocumentQuery('');
  };

  const awaitingCount = items.filter((row) => AWAITING_STATUSES.includes(row.status)).length;

  const openDetail = async (row: CustomerReservationStaffListItem) => {
    setDetailOpen(true);
    setDetail(null);
    setDetailLoading(true);
    try {
      const data = await fetchCustomerReservation(row.id);
      setDetail(data);
      setStaffNotes(data.staffNotes ?? '');
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const refreshDetail = async (id: string) => {
    const data = await fetchCustomerReservation(id);
    setDetail(data);
    setStaffNotes(data.staffNotes ?? '');
    await load();
  };

  const runAction = async (successKey: 'confirmed' | 'rejected' | 'ready', action: () => Promise<CustomerReservation>) => {
    if (!detail) return;
    try {
      await action();
      message.success(t(`messages.${successKey}`));
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.actionFailed')));
    }
  };

  const saveStaffNotes = async () => {
    if (!detail) return;
    setSavingNotes(true);
    try {
      await updateCustomerReservationStaffNotes(detail.id, staffNotes.trim() || undefined);
      message.success(t('messages.notesSaved'));
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.notesSaveFailed')));
    } finally {
      setSavingNotes(false);
    }
  };

  const canLoadPos = (item: CustomerReservation) =>
    !item.salesOrderId
    && (item.status === CUSTOMER_RESERVATION_STATUS.Confirmed
      || item.status === CUSTOMER_RESERVATION_STATUS.Ready
      || item.status === CUSTOMER_RESERVATION_STATUS.Collected);

  const loadIntoPos = (reservationId: string) => {
    setDetailOpen(false);
    navigate(`/sales/pos?customerReservationId=${reservationId}&checkout=1`);
  };

  const columns: ColumnsType<CustomerReservationStaffListItem> = [
    { title: t('columns.reservationNumber'), dataIndex: 'reservationNumber', width: 130 },
    { title: t('columns.customerName'), dataIndex: 'customerName' },
    { title: t('columns.phone'), dataIndex: 'customerPhone', width: 120 },
    {
      title: t('columns.status'),
      dataIndex: 'status',
      width: 150,
      render: (status: number) => (
        <Tag color={CUSTOMER_RESERVATION_STATUS_COLORS[status] ?? 'default'}>
          {reservationStatusLabel(status)}
        </Tag>
      ),
    },
    {
      title: t('columns.fulfillmentType'),
      dataIndex: 'fulfillmentType',
      width: 120,
      render: (value: number) => reservationFulfillmentLabel(value),
    },
    { title: t('columns.itemCount'), dataIndex: 'itemCount', width: 60 },
    {
      title: t('columns.submittedAt'),
      dataIndex: 'submittedAt',
      width: 150,
      render: (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: '',
      width: 80,
      render: (_, row) => (
        <Button type="link" icon={<EyeOutlined />} onClick={() => void openDetail(row)}>
          {t('columns.view')}
        </Button>
      ),
    },
  ];

  return (
    <div style={sectionGapStyle}>
      <Card size="small">
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
            options={reservationStatusOptions}
            value={statusFilter}
            onChange={(value) => {
              setStatusFilter(value);
              setAwaitingOnly(false);
            }}
          />
          <Button
            type={awaitingOnly ? 'primary' : 'default'}
            ghost={awaitingOnly}
            onClick={() => {
              setAwaitingOnly((prev) => !prev);
              setStatusFilter(undefined);
            }}
          >
            {t('filters.awaiting', { count: awaitingCount })}
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('filters.reload')}
          </Button>
        </SalesListDualSearchWrap>
      </Card>

      <Table
        rowKey="id"
        size="small"
        loading={loading}
        columns={columns}
        dataSource={filteredItems}
        pagination={{ pageSize: 20, showSizeChanger: false }}
      />

      <Drawer
        title={detail?.reservationNumber ?? t('detail.drawerTitleDefault')}
        width={520}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        loading={detailLoading}
      >
        {detail ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Descriptions size="small" column={1} bordered>
              <Descriptions.Item label={t('detail.status')}>
                <Tag color={CUSTOMER_RESERVATION_STATUS_COLORS[detail.status] ?? 'default'}>
                  {reservationStatusLabel(detail.status)}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('detail.fulfillmentType')}>
                {reservationFulfillmentLabel(detail.fulfillmentType)}
              </Descriptions.Item>
              {detail.addressSummary ? (
                <Descriptions.Item label={t('detail.address')}>{detail.addressSummary}</Descriptions.Item>
              ) : null}
              {detail.notes ? <Descriptions.Item label={t('detail.customerNotes')}>{detail.notes}</Descriptions.Item> : null}
              {detail.salesOrderNumber ? (
                <Descriptions.Item label={t('detail.salesOrder')}>{detail.salesOrderNumber}</Descriptions.Item>
              ) : null}
              <Descriptions.Item label={t('detail.submittedAt')}>
                {dayjs(detail.submittedAt).format('DD/MM/YYYY HH:mm')}
              </Descriptions.Item>
            </Descriptions>

            <List
              size="small"
              header={t('detail.productsHeader')}
              dataSource={detail.items}
              renderItem={(line) => (
                <List.Item>
                  <Space direction="vertical" size={0}>
                    <span>
                      {line.productCode} — {line.productName}
                    </span>
                    <span>
                      × {line.quantity} {line.unitName}
                      {line.customerNote ? ` · ${line.customerNote}` : ''}
                    </span>
                  </Space>
                </List.Item>
              )}
            />

            {canWrite ? (
              <>
                <Input.TextArea
                  rows={3}
                  value={staffNotes}
                  onChange={(e) => setStaffNotes(e.target.value)}
                  placeholder={t('detail.staffNotesPlaceholder')}
                />
                <Button loading={savingNotes} onClick={() => void saveStaffNotes()}>
                  {t('detail.saveNotes')}
                </Button>

                <Space wrap>
                  {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
                    <>
                      <Popconfirm
                        title={t('detail.confirmPopconfirm')}
                        onConfirm={() =>
                          void runAction('confirmed', () => confirmCustomerReservation(detail.id))
                        }
                      >
                        <Button type="primary">{t('detail.confirm')}</Button>
                      </Popconfirm>
                      <Popconfirm
                        title={t('detail.rejectPopconfirm')}
                        onConfirm={() =>
                          void runAction('rejected', () => rejectCustomerReservation(detail.id))
                        }
                      >
                        <Button danger>{t('detail.reject')}</Button>
                      </Popconfirm>
                    </>
                  ) : null}
                  {detail.status === CUSTOMER_RESERVATION_STATUS.Confirmed ? (
                    <Popconfirm
                      title={t('detail.readyPopconfirm')}
                      onConfirm={() =>
                        void runAction('ready', () => markCustomerReservationReady(detail.id))
                      }
                    >
                      <Button>{t('detail.markReady')}</Button>
                    </Popconfirm>
                  ) : null}
                  {canLoadPos(detail) ? (
                    <Button
                      type="primary"
                      icon={<ShoppingCartOutlined />}
                      onClick={() => loadIntoPos(detail.id)}
                    >
                      {t('detail.loadPos')}
                    </Button>
                  ) : null}
                </Space>
              </>
            ) : null}
          </Space>
        ) : null}
      </Drawer>
    </div>
  );
}
