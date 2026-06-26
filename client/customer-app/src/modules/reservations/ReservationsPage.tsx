import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Empty,
  Form,
  Input,
  InputNumber,
  List,
  Popconfirm,
  Radio,
  Select,
  Space,
  Spin,
  Tag,
  Typography,
  message,
} from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
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
  CUSTOMER_RESERVATION_FULFILLMENT_LABELS,
  CUSTOMER_RESERVATION_STATUS,
  CUSTOMER_RESERVATION_STATUS_LABELS,
} from '@/shared/api/customer-app.types';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';

type DraftLine = {
  key: string;
  productId: string;
  productName: string;
  productCode: string;
  unitName: string;
  quantity: number;
  customerNote?: string;
};


function reservationStatusColor(status: number): string {
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
  return (
    <Card size="small" style={{ borderRadius: 12, marginBottom: 12 }}>
      <Space direction="vertical" size="small" style={{ width: '100%' }}>
        <Space wrap>
          <Typography.Text strong>{detail.reservationNumber}</Typography.Text>
          <Tag color={reservationStatusColor(detail.status)}>
            {CUSTOMER_RESERVATION_STATUS_LABELS[detail.status] ?? detail.status}
          </Tag>
        </Space>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {CUSTOMER_RESERVATION_FULFILLMENT_LABELS[detail.fulfillmentType] ?? detail.fulfillmentType}
          {detail.addressSummary ? ` · ${detail.addressSummary}` : ''}
        </Typography.Text>
        {detail.notes ? (
          <Typography.Text style={{ fontSize: 13 }}>Ghi chú: {detail.notes}</Typography.Text>
        ) : null}
        {detail.staffNotes ? (
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Nhà thuốc: {detail.staffNotes}
          </Typography.Text>
        ) : null}
        {detail.salesOrderNumber ? (
          <Typography.Text type="success" style={{ fontSize: 13, display: 'block' }}>
            Hóa đơn {detail.salesOrderNumber} — xem tab Đơn hàng → Đã mua
          </Typography.Text>
        ) : detail.status === CUSTOMER_RESERVATION_STATUS.Collected ? (
          <Alert
            type="warning"
            showIcon
            message="Chưa có hóa đơn bán"
            description="Nhà thuốc cần bán qua quầy để ghi nhận thanh toán."
          />
        ) : null}
        <List
          size="small"
          dataSource={detail.items}
          renderItem={(line) => (
            <List.Item style={{ paddingInline: 0 }}>
              <Space direction="vertical" size={0}>
                <Typography.Text>
                  {line.productName} × {line.quantity} {line.unitName}
                </Typography.Text>
                {line.customerNote ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {line.customerNote}
                  </Typography.Text>
                ) : null}
              </Space>
            </List.Item>
          )}
        />
        {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
          <Popconfirm title="Hủy yêu cầu đặt trước?" onConfirm={onCancel}>
            <Button danger loading={cancelling} block>
              Hủy yêu cầu
            </Button>
          </Popconfirm>
        ) : null}
      </Space>
    </Card>
  );
}

export function ReservationsPage() {
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

  const [createOpen, setCreateOpen] = useState(searchParams.get('create') === '1');
  const [submitting, setSubmitting] = useState(false);
  const [fulfillmentType, setFulfillmentType] = useState<number>(CUSTOMER_RESERVATION_FULFILLMENT.Pickup);
  const [addressId, setAddressId] = useState<string | undefined>();
  const [notes, setNotes] = useState('');
  const [addresses, setAddresses] = useState<CustomerAddress[]>([]);
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [productSearch, setProductSearch] = useState('');
  const [productOptions, setProductOptions] = useState<CustomerProductSearchItem[]>([]);
  const [productLoading, setProductLoading] = useState(false);
  const [searchDropdownOpen, setSearchDropdownOpen] = useState(false);

  const loadList = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchReservations());
    } catch (error) {
      setItems([]);
      setLoadError(getApiErrorMessage(error, 'Không tải được yêu cầu đặt trước'));
    } finally {
      setLoading(false);
    }
  }, []);

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
        message.error(getApiErrorMessage(error, 'Không tải được chi tiết'));
        setSelectedId(null);
      })
      .finally(() => setDetailLoading(false));
  }, [selectedId]);

  useEffect(() => {
    if (searchParams.get('create') === '1') {
      setCreateOpen(true);
      setSearchParams({}, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  const loadProducts = useCallback(async (search?: string) => {
    setProductLoading(true);
    try {
      const result = await searchProducts(search?.trim() || undefined, 1, 30);
      setProductOptions(result.items);
    } catch (error) {
      setProductOptions([]);
      message.error(getApiErrorMessage(error, 'Không tải được danh sách thuốc'));
    } finally {
      setProductLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!createOpen) return;
    const q = productSearch.trim();
    if (q.length < 2) {
      setProductOptions([]);
      return;
    }
    const timer = window.setTimeout(() => {
      void loadProducts(q);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [createOpen, productSearch, loadProducts]);

  const searchResults = useMemo(
    () => productOptions.filter((p) => !draftLines.some((line) => line.productId === p.id)),
    [productOptions, draftLines],
  );

  const showSearchDropdown = searchDropdownOpen && productSearch.trim().length >= 2;

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
      message.info('Sản phẩm đã có trong danh sách');
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
    setProductOptions([]);
    setSearchDropdownOpen(false);
  };

  const submitCreate = async () => {
    if (draftLines.length === 0) {
      message.warning('Thêm ít nhất một sản phẩm');
      return;
    }
    if (fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery && !addressId) {
      message.warning('Chọn địa chỉ giao hàng');
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
      message.success('Đã gửi yêu cầu đặt trước');
      setCreateOpen(false);
      setDraftLines([]);
      setNotes('');
      setFulfillmentType(CUSTOMER_RESERVATION_FULFILLMENT.Pickup);
      await loadList();
      setSelectedId(created.id);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không gửi được yêu cầu'));
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
      message.success('Đã hủy yêu cầu');
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không hủy được yêu cầu'));
    } finally {
      setCancelling(false);
    }
  };

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <BackToHomeButton />
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          Đặt thuốc trước
        </Typography.Title>
        <Typography.Text type="secondary">
          Gửi danh sách thuốc cần — nhà thuốc xác nhận khi có hàng
        </Typography.Text>
      </div>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <Typography.Text type="danger">{loadError}</Typography.Text>
      ) : null}

      {!createOpen ? (
        <Button
          type="primary"
          icon={<PlusOutlined />}
          block
          size="large"
          onClick={() => {
            setCreateOpen(true);
            setProductSearch('');
            setProductOptions([]);
            setSearchDropdownOpen(false);
          }}
        >
          Tạo yêu cầu mới
        </Button>
      ) : (
        <Card size="small" title="Yêu cầu mới" style={{ borderRadius: 12 }}>
          <Form layout="vertical" requiredMark={false}>
            <Form.Item label="Hình thức nhận">
              <Radio.Group
                value={fulfillmentType}
                onChange={(e) => setFulfillmentType(e.target.value)}
                optionType="button"
                buttonStyle="solid"
              >
                <Radio.Button value={CUSTOMER_RESERVATION_FULFILLMENT.Pickup}>Đến quầy</Radio.Button>
                <Radio.Button value={CUSTOMER_RESERVATION_FULFILLMENT.Delivery}>Giao tận nơi</Radio.Button>
              </Radio.Group>
            </Form.Item>

            {fulfillmentType === CUSTOMER_RESERVATION_FULFILLMENT.Delivery ? (
              <Form.Item label="Địa chỉ giao hàng">
                {addresses.length === 0 ? (
                  <Typography.Text type="secondary">
                    Chưa có địa chỉ.{' '}
                    <Link to="/addresses" onClick={() => navigate('/addresses')}>
                      Thêm địa chỉ
                    </Link>
                  </Typography.Text>
                ) : (
                  <Select
                    value={addressId}
                    onChange={setAddressId}
                    options={addresses.map((a) => ({
                      value: a.id,
                      label: `${a.label} — ${[a.addressLine, a.ward, a.district].filter(Boolean).join(', ')}`,
                    }))}
                  />
                )}
              </Form.Item>
            ) : null}

            <Form.Item label="Tìm sản phẩm" extra="Gõ tên hoặc mã thuốc, chọn + để thêm vào danh sách">
              <div style={{ position: 'relative' }}>
                <Input
                  value={productSearch}
                  onChange={(e) => {
                    setProductSearch(e.target.value);
                    setSearchDropdownOpen(true);
                  }}
                  onFocus={() => setSearchDropdownOpen(true)}
                  onBlur={() => {
                    window.setTimeout(() => setSearchDropdownOpen(false), 180);
                  }}
                  placeholder="Gõ tên hoặc mã thuốc..."
                  allowClear
                  onClear={() => {
                    setProductOptions([]);
                    setSearchDropdownOpen(false);
                  }}
                />
                {showSearchDropdown ? (
                  <div
                    style={{
                      position: 'absolute',
                      top: 'calc(100% + 4px)',
                      left: 0,
                      right: 0,
                      zIndex: 20,
                      background: '#fff',
                      border: '1px solid #d9d9d9',
                      borderRadius: 10,
                      boxShadow: '0 6px 16px rgba(0,0,0,0.12)',
                      maxHeight: 260,
                      overflowY: 'auto',
                    }}
                  >
                    {productLoading ? (
                      <div style={{ textAlign: 'center', padding: 16 }}>
                        <Spin size="small" />
                      </div>
                    ) : searchResults.length === 0 ? (
                      <Typography.Text type="secondary" style={{ display: 'block', padding: '12px 14px' }}>
                        Không tìm thấy thuốc
                      </Typography.Text>
                    ) : (
                      searchResults.map((product) => (
                        <div
                          key={product.id}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 8,
                            padding: '10px 12px',
                            borderBottom: '1px solid #f0f0f0',
                          }}
                        >
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <Typography.Text strong style={{ fontSize: 14, display: 'block' }}>
                              {product.productName}
                            </Typography.Text>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {product.productCode}
                              {product.saleUnitName ? ` · ${product.saleUnitName}` : ''}
                            </Typography.Text>
                          </div>
                          <Button
                            type="primary"
                            shape="circle"
                            size="small"
                            icon={<PlusOutlined />}
                            aria-label={`Thêm ${product.productName}`}
                            title="Thêm vào danh sách"
                            onMouseDown={(event) => event.preventDefault()}
                            onClick={() => addProduct(product)}
                          />
                        </div>
                      ))
                    )}
                  </div>
                ) : null}
              </div>
            </Form.Item>

            {draftLines.length > 0 ? (
              <>
                <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                  Đã chọn ({draftLines.length})
                </Typography.Text>
                {draftLines.map((line) => (
                  <Card
                    key={line.key}
                    size="small"
                    style={{
                      marginBottom: 10,
                      borderRadius: 12,
                      borderColor: '#99f6e4',
                      background: '#f0fdfa',
                    }}
                    styles={{ body: { padding: '12px 14px' } }}
                  >
                    <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <Typography.Text strong style={{ fontSize: 15, display: 'block' }}>
                          {line.productName}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {line.productCode}
                          {line.unitName ? ` · ${line.unitName}` : ''}
                        </Typography.Text>
                      </div>
                      <Button
                        type="text"
                        danger
                        icon={<DeleteOutlined />}
                        aria-label={`Xóa ${line.productName}`}
                        onClick={() => setDraftLines((prev) => prev.filter((x) => x.key !== line.key))}
                      />
                    </div>
                    <InputNumber
                      min={0.01}
                      step={1}
                      value={line.quantity}
                      addonAfter={line.unitName || 'SL'}
                      style={{ width: '100%', marginTop: 10 }}
                      onChange={(value) =>
                        setDraftLines((prev) =>
                          prev.map((x) =>
                            x.key === line.key ? { ...x, quantity: Number(value) || 1 } : x,
                          ),
                        )
                      }
                    />
                    <Input
                      placeholder="Ghi chú (tuỳ chọn)"
                      value={line.customerNote}
                      style={{ marginTop: 8 }}
                      onChange={(e) =>
                        setDraftLines((prev) =>
                          prev.map((x) =>
                            x.key === line.key ? { ...x, customerNote: e.target.value } : x,
                          ),
                        )
                      }
                    />
                  </Card>
                ))}
              </>
            ) : (
              <Empty
                description="Chưa có thuốc — gõ tên ở trên rồi bấm +"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                style={{ marginBottom: 8 }}
              />
            )}

            <Form.Item label="Ghi chú chung">
              <Input.TextArea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Form.Item>

            <Space style={{ width: '100%' }} direction="vertical">
              <Button type="primary" block size="large" loading={submitting} onClick={() => void submitCreate()}>
                Gửi yêu cầu
              </Button>
              <Button block onClick={() => setCreateOpen(false)}>
                Huỷ
              </Button>
            </Space>
          </Form>
        </Card>
      )}

      {detailLoading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : detail ? (
        <ReservationDetailPanel detail={detail} onCancel={() => void handleCancel()} cancelling={cancelling} />
      ) : null}

      {items.length === 0 ? (
        <Empty description="Chưa có yêu cầu đặt trước" image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : (
        <List
          dataSource={items}
          renderItem={(item) => (
            <Card
              size="small"
              style={{
                marginBottom: 8,
                borderRadius: 12,
                borderColor: item.id === selectedId ? '#0f766e' : undefined,
                cursor: 'pointer',
              }}
              onClick={() => setSelectedId(item.id)}
            >
              <Space direction="vertical" size={2}>
                <Space wrap>
                  <Typography.Text strong>{item.reservationNumber}</Typography.Text>
                  <Tag color={reservationStatusColor(item.status)}>
                    {CUSTOMER_RESERVATION_STATUS_LABELS[item.status] ?? item.status}
                  </Tag>
                </Space>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {item.itemCount} sản phẩm · {dayjs(item.submittedAt).format('DD/MM/YYYY HH:mm')}
                </Typography.Text>
              </Space>
            </Card>
          )}
        />
      )}
    </Space>
  );
}
