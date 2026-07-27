import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Spin,
  message,
} from 'antd';
import { ArrowLeftOutlined, DeleteOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  cancelReservation,
  createReservation,
  fetchAddresses,
  fetchReservation,
  fetchReservations,
  getApiErrorMessage,
  searchProducts,
} from '@/shared/api/customer-app.api';
import type {
  CustomerAddress,
  CustomerProductSearchItem,
  CustomerReservationDetail,
  CustomerReservationListItem,
} from '@/shared/api/customer-app.types';
import {
  CUSTOMER_RESERVATION_FULFILLMENT,
  CUSTOMER_RESERVATION_STATUS,
} from '@/shared/api/customer-app.types';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import '@/shared/components/EntryPage.css';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';

type DraftLine = {
  key: string;
  productId: string;
  productName: string;
  productCode: string;
  unitName: string;
  quantity: number;
  customerNote?: string;
};

function reservationStatusTone(status: number): string {
  if (status === CUSTOMER_RESERVATION_STATUS.Ready) return 'green';
  if (status === CUSTOMER_RESERVATION_STATUS.Confirmed) return 'blue';
  if (status === CUSTOMER_RESERVATION_STATUS.Collected) return 'success';
  if (status === CUSTOMER_RESERVATION_STATUS.Cancelled || status === CUSTOMER_RESERVATION_STATUS.Rejected)
    return 'error';
  return 'gold';
}

function ReservationDetailPanel({
  detail,
  onCancel,
  cancelling,
}: {
  detail: CustomerReservationDetail;
  onCancel: () => void;
  cancelling: boolean;
}) {
  const { t } = useTranslation();
  const { reservationStatus, reservationFulfillment } = useCustomerLabels();
  const tone = reservationStatusTone(detail.status);

  return (
    <div className="entry-card entry-detail">
      <div className="entry-list-card-top">
        <span className="entry-list-card-title">{detail.reservationNumber}</span>
        <span className={`entry-status entry-status--${tone}`}>{reservationStatus(detail.status)}</span>
      </div>
      <div className="entry-list-card-sub">
        {reservationFulfillment(detail.fulfillmentType)}
        {detail.addressSummary ? ` · ${detail.addressSummary}` : ''}
      </div>
      {detail.notes ? (
        <div className="entry-detail-notes">
          {t('reservations.notes')}: {detail.notes}
        </div>
      ) : null}
      {detail.staffNotes ? (
        <div className="entry-detail-muted">
          {t('reservations.pharmacy')}: {detail.staffNotes}
        </div>
      ) : null}
      {detail.salesOrderNumber ? (
        <div className="entry-detail-notes" style={{ color: '#0f766e' }}>
          {t('reservations.invoiceLink', { number: detail.salesOrderNumber })}
        </div>
      ) : detail.status === CUSTOMER_RESERVATION_STATUS.Collected ? (
        <Alert
          style={{ marginTop: 10 }}
          type="warning"
          showIcon
          message={t('reservations.noInvoiceTitle')}
          description={t('reservations.noInvoiceDesc')}
        />
      ) : null}
                  <ul className="entry-detail-items">
        {detail.items.map((line) => (
          <li key={line.id}>
            <div>
              {line.productName} × {line.quantity} {line.unitName}
            </div>
            {line.customerNote ? <div className="entry-detail-muted">{line.customerNote}</div> : null}
          </li>
        ))}
      </ul>
      {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
        <div className="entry-actions" style={{ marginTop: 12 }}>
          <Popconfirm title={t('reservations.cancelConfirm')} onConfirm={onCancel}>
            <button type="button" className="entry-btn entry-btn--danger" disabled={cancelling}>
              {t('reservations.cancelRequest')}
            </button>
          </Popconfirm>
        </div>
      ) : null}
    </div>
  );
}

export function ReservationsPage() {
  const { t } = useTranslation();
  const { reservationStatus } = useCustomerLabels();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { online } = useApiHealth();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<CustomerReservationListItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<CustomerReservationDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const [createOpen, setCreateOpen] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [fulfillmentType, setFulfillmentType] = useState<number>(CUSTOMER_RESERVATION_FULFILLMENT.Pickup);
  const [addressId, setAddressId] = useState<string | undefined>();
  const [notes, setNotes] = useState('');
  const [addresses, setAddresses] = useState<CustomerAddress[]>([]);
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [productSearch, setProductSearch] = useState('');
  const [productOptions, setProductOptions] = useState<CustomerProductSearchItem[]>([]);
  const [productLoading, setProductLoading] = useState(false);
  const [selectOpen, setSelectOpen] = useState(false);

  const loadList = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchReservations());
    } catch (error) {
      setItems([]);
      setLoadError(getApiErrorMessage(error, t('reservations.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadList();
  }, [loadList]);

  useRetryWhenApiOnline(() => loadList());

  useEffect(() => {
    if (!createOpen) return;
    void fetchAddresses()
      .then(setAddresses)
      .catch(() => setAddresses([]));
  }, [createOpen]);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }
    setDetailLoading(true);
    void fetchReservation(selectedId)
      .then(setDetail)
      .catch((error) => {
        message.error(getApiErrorMessage(error, t('reservations.detailLoadFailed')));
        setSelectedId(null);
      })
      .finally(() => setDetailLoading(false));
  }, [selectedId, t]);

  useEffect(() => {
    if (searchParams.get('create') === '1') {
      setCreateOpen(true);
      setSearchParams({}, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  const loadProducts = useCallback(
    async (search?: string) => {
      setProductLoading(true);
      try {
        const result = await searchProducts(search?.trim() || undefined, 1, 30);
        setProductOptions(result.items);
      } catch (error) {
        setProductOptions([]);
        message.error(getApiErrorMessage(error, t('reservations.productLoadFailed')));
      } finally {
        setProductLoading(false);
      }
    },
    [t],
  );

  useEffect(() => {
    if (!createOpen) return;
    const q = productSearch.trim();
    const timer = window.setTimeout(
      () => {
        void loadProducts(q || undefined);
      },
      q.length === 0 ? 0 : 250,
    );
    return () => window.clearTimeout(timer);
  }, [createOpen, productSearch, loadProducts]);

  useEffect(() => {
    if (createOpen) {
      void loadProducts();
    }
  }, [createOpen, loadProducts]);

  const searchResults = useMemo(
    () => productOptions.filter((p) => !draftLines.some((line) => line.productId === p.id)),
    [productOptions, draftLines],
  );

  const defaultAddressId = useMemo(
    () => addresses.find((a) => a.isDefault)?.id ?? addresses[0]?.id,
    [addresses],
  );

  useEffect(() => {
    if (fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery && !addressId && defaultAddressId) {
      setAddressId(defaultAddressId);
    }
  }, [fulfillmentType, addressId, defaultAddressId]);

  const addProduct = (product: CustomerProductSearchItem) => {
    if (draftLines.some((line) => line.productId === product.id)) {
      message.info(t('reservations.productInList'));
      return;
    }
    setDraftLines((prev) => [
      ...prev,
      {
        key: product.id,
        productId: product.id,
        productName: product.productName,
        productCode: product.productCode,
        unitName: product.saleUnitName ?? '',
        quantity: 1,
      },
    ]);
    setProductSearch('');
    setSelectOpen(false);
  };

  const resetCreateForm = () => {
    setDraftLines([]);
    setNotes('');
    setFulfillmentType(CUSTOMER_RESERVATION_FULFILLMENT.Pickup);
    setProductSearch('');
    setSelectOpen(false);
  };

  const submitCreate = async () => {
    if (draftLines.length === 0) {
      message.warning(t('reservations.addAtLeastOne'));
      return;
    }
    if (fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery && !addressId) {
      message.warning(t('reservations.selectAddress'));
      return;
    }
    setSubmitting(true);
    try {
      const created = await createReservation({
        fulfillmentType,
        addressId: fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery ? addressId : undefined,
        notes: notes.trim() || undefined,
        items: draftLines.map((line) => ({
          productId: line.productId,
          quantity: line.quantity,
          customerNote: line.customerNote?.trim() || undefined,
        })),
      });
      message.success(t('reservations.submitted'));
      resetCreateForm();
      await loadList();
      setSelectedId(created.id);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('reservations.submitFailed')));
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = async () => {
    if (!selectedId) return;
    setCancelling(true);
    try {
      const updated = await cancelReservation(selectedId);
      setDetail(updated);
      await loadList();
      message.success(t('reservations.cancelled'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('reservations.cancelFailed')));
    } finally {
      setCancelling(false);
    }
  };

  if (loading) {
    return (
      <div className="entry-page">
        <div className="entry-page-loading">
          <Spin />
        </div>
      </div>
    );
  }

  return (
    <div className="entry-page">
      <button type="button" className="entry-page-home" onClick={() => navigate('/')}>
        <ArrowLeftOutlined />
        {t('common.backHome')}
      </button>

      <h1 className="entry-page-title">{t('reservations.title')}</h1>
      <p className="entry-page-intro">{t('reservations.intro')}</p>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <div className="entry-page-error">{loadError}</div>
      ) : null}

      {!createOpen ? (
        <button
          type="button"
          className="entry-btn entry-btn--primary entry-open-create"
          onClick={() => {
            setCreateOpen(true);
            setProductSearch('');
            setSelectOpen(false);
          }}
        >
          <PlusOutlined />
          {t('reservations.createNew')}
        </button>
      ) : (
        <section className="entry-card">
          <h2 className="entry-card-title">{t('reservations.newRequest')}</h2>

          <div className="entry-field">
            <span className="entry-label">{t('reservations.fulfillmentType')}</span>
            <div className="entry-seg" role="group" aria-label={t('reservations.fulfillmentType')}>
              <button
                type="button"
                className={`entry-seg-btn${
                  fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Pickup
                    ? ' entry-seg-btn--active'
                    : ''
                }`}
                onClick={() => setFulfillmentType(CUSTOMER_RESERVATION_FULFILLMENT.Pickup)}
              >
                {t('reservations.pickup')}
              </button>
              <button
                type="button"
                className={`entry-seg-btn${
                  fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery
                    ? ' entry-seg-btn--active'
                    : ''
                }`}
                onClick={() => setFulfillmentType(CUSTOMER_RESERVATION_FULFILLMENT.Delivery)}
              >
                {t('reservations.delivery')}
              </button>
            </div>
          </div>

          {fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery ? (
            <div className="entry-field">
              <span className="entry-label">{t('reservations.deliveryAddress')}</span>
              {addresses.length === 0 ? (
                <div className="entry-hint">
                  {t('reservations.noAddress')}{' '}
                  <Link className="entry-page-link" to="/addresses">
                    {t('reservations.addAddress')}
                  </Link>
                </div>
              ) : (
                <Select
                  size="large"
                  value={addressId}
                  onChange={setAddressId}
                  style={{ width: '100%' }}
                  options={addresses.map((a) => ({
                    value: a.id,
                    label: `${a.label} — ${[a.addressLine, a.ward, a.district].filter(Boolean).join(', ')}`,
                  }))}
                />
              )}
            </div>
          ) : null}

          <div className="entry-field">
            <span className="entry-label">{t('reservations.searchProduct')}</span>
            <div className="entry-search-row">
              <Select
                size="large"
                showSearch
                allowClear
                value={null}
                open={selectOpen}
                placeholder={t('reservations.searchPlaceholder')}
                filterOption={false}
                loading={productLoading}
                searchValue={productSearch}
                style={{ width: '100%' }}
                listHeight={280}
                getPopupContainer={(node) => node.parentElement ?? document.body}
                notFoundContent={
                  productLoading ? (
                    <div style={{ textAlign: 'center', padding: 12 }}>
                      <Spin size="small" />
                    </div>
                  ) : (
                    t('reservations.noProducts')
                  )
                }
                onSearch={(value) => setProductSearch(value)}
                onOpenChange={(open) => {
                  setSelectOpen(open);
                  if (open && productOptions.length === 0) {
                    void loadProducts(productSearch.trim() || undefined);
                  }
                }}
                onSelect={(productId) => {
                  const product = productOptions.find((p) => p.id === productId);
                  if (product) addProduct(product);
                  setProductSearch('');
                  setSelectOpen(false);
                }}
                onClear={() => {
                  setProductSearch('');
                  void loadProducts();
                }}
                options={searchResults.map((product) => ({
                  value: product.id,
                  label: `${product.productName} (${product.productCode})${
                    product.saleUnitName ? ` · ${product.saleUnitName}` : ''
                  }`,
                }))}
              />
              <button
                type="button"
                className="entry-search-add"
                aria-label={t('reservations.addProductAria')}
                onClick={() => {
                  if (searchResults[0]) {
                    addProduct(searchResults[0]);
                    return;
                  }
                  setSelectOpen(true);
                  if (productOptions.length === 0) {
                    void loadProducts(productSearch.trim() || undefined);
                  }
                }}
              >
                <PlusOutlined />
              </button>
            </div>
            <span className="entry-hint">{t('reservations.searchHint')}</span>
          </div>

          {draftLines.length > 0 ? (
            <div className="entry-field">
              <span className="entry-label">{t('reservations.selected', { count: draftLines.length })}</span>
              {draftLines.map((line) => (
                <div key={line.key} className="entry-line">
                  <div className="entry-line-head">
                    <div className="entry-line-copy">
                      <div className="entry-line-name">{line.productName}</div>
                      <div className="entry-line-meta">
                        {line.productCode}
                        {line.unitName ? ` · ${line.unitName}` : ''}
                      </div>
                    </div>
                    <button
                      type="button"
                      className="entry-line-remove"
                      aria-label={t('reservations.removeAria', { name: line.productName })}
                      onClick={() => setDraftLines((prev) => prev.filter((x) => x.key !== line.key))}
                    >
                      <DeleteOutlined />
                    </button>
                  </div>
                  <InputNumber
                    className="entry-line-qty"
                    min={0.01}
                    step={1}
                    size="large"
                    value={line.quantity}
                    addonAfter={line.unitName || t('reservations.qtyUnit')}
                    onChange={(value) =>
                      setDraftLines((prev) =>
                        prev.map((x) =>
                          x.key === line.key ? { ...x, quantity: Number(value) || 1 } : x,
                        ),
                      )
                    }
                  />
                  <Input
                    className="entry-line-note"
                    size="large"
                    placeholder={t('reservations.noteOptional')}
                    value={line.customerNote}
                    onChange={(e) =>
                      setDraftLines((prev) =>
                        prev.map((x) =>
                          x.key === line.key ? { ...x, customerNote: e.target.value } : x,
                        ),
                      )
                    }
                  />
                </div>
              ))}
            </div>
          ) : (
            <div className="entry-empty">
              <InboxOutlined className="entry-empty-icon" />
              <span>{t('reservations.emptyDraft')}</span>
            </div>
          )}

          <div className="entry-field">
            <span className="entry-label">{t('reservations.generalNotes')}</span>
            <Input.TextArea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <div className="entry-actions">
            <button
              type="button"
              className="entry-btn entry-btn--primary"
              disabled={submitting}
              onClick={() => void submitCreate()}
            >
              {t('reservations.submit')}
            </button>
            <button
              type="button"
              className="entry-btn entry-btn--ghost"
              disabled={submitting}
              onClick={() => resetCreateForm()}
            >
              {t('common.cancel')}
            </button>
          </div>
        </section>
      )}

      {detailLoading ? (
        <div className="entry-page-loading">
          <Spin />
        </div>
      ) : detail ? (
        <ReservationDetailPanel detail={detail} onCancel={() => void handleCancel()} cancelling={cancelling} />
      ) : null}

      {items.length === 0 ? (
        <div className="entry-empty" style={{ marginTop: 14 }}>
          <InboxOutlined className="entry-empty-icon" />
          <span>{t('reservations.empty')}</span>
        </div>
      ) : (
        <div className="entry-list">
          {items.map((item) => {
            const tone = reservationStatusTone(item.status);
            const active = item.id === selectedId;
            return (
              <button
                key={item.id}
                type="button"
                className={`entry-list-card${active ? ' entry-list-card--active' : ''}`}
                onClick={() => setSelectedId(item.id)}
              >
                <div className="entry-list-card-top">
                  <span className="entry-list-card-title">{item.reservationNumber}</span>
                  <span className={`entry-status entry-status--${tone}`}>
                    {reservationStatus(item.status)}
                  </span>
                </div>
                <div className="entry-list-card-sub">
                  {t('reservations.productCount', { count: item.itemCount })} ·{' '}
                  {dayjs(item.submittedAt).format('DD/MM/YYYY HH:mm')}
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
