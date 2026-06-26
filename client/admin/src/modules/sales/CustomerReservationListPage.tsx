import { useCallback, useEffect, useMemo, useState } from 'react';
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
  CUSTOMER_RESERVATION_FULFILLMENT_LABELS,
  CUSTOMER_RESERVATION_STATUS,
  CUSTOMER_RESERVATION_STATUS_COLORS,
  CUSTOMER_RESERVATION_STATUS_LABELS,
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
import { filterBarStyle, sectionGapStyle } from '@/modules/sales/sales-ui-styles';

const STATUS_FILTER_OPTIONS = Object.entries(CUSTOMER_RESERVATION_STATUS_LABELS).map(([value, label]) => ({
  value: Number(value),
  label,
}));

const AWAITING_STATUSES: number[] = [
  CUSTOMER_RESERVATION_STATUS.Pending,
  CUSTOMER_RESERVATION_STATUS.Confirmed,
  CUSTOMER_RESERVATION_STATUS.Ready,
];

export function CustomerReservationListPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<CustomerReservationStaffListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [awaitingOnly, setAwaitingOnly] = useState(
    () => searchParams.get('awaiting') === '1' || searchParams.get('awaiting') === 'true',
  );
  const [search, setSearch] = useState('');
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
      message.error(apiErrorMessage(error, 'Không tải được yêu cầu đặt trước'));
    } finally {
      setLoading(false);
    }
  }, []);

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

  const filteredItems = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((row) => {
      if (awaitingOnly && !AWAITING_STATUSES.includes(row.status)) return false;
      if (statusFilter != null && row.status !== statusFilter) return false;
      if (!q) return true;
      return (
        row.reservationNumber.toLowerCase().includes(q) ||
        row.customerName.toLowerCase().includes(q) ||
        (row.customerPhone ?? '').includes(q)
      );
    });
  }, [items, search, statusFilter, awaitingOnly]);

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
      message.error(apiErrorMessage(error, 'Không tải được chi tiết'));
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

  const runAction = async (label: string, action: () => Promise<CustomerReservation>) => {
    if (!detail) return;
    try {
      await action();
      message.success(label);
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Thao tác thất bại'));
    }
  };

  const saveStaffNotes = async () => {
    if (!detail) return;
    setSavingNotes(true);
    try {
      await updateCustomerReservationStaffNotes(detail.id, staffNotes.trim() || undefined);
      message.success('Đã lưu ghi chú');
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được ghi chú'));
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
    { title: 'Số yêu cầu', dataIndex: 'reservationNumber', width: 130 },
    { title: 'Khách hàng', dataIndex: 'customerName' },
    { title: 'SĐT', dataIndex: 'customerPhone', width: 120 },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 150,
      render: (status: number) => (
        <Tag color={CUSTOMER_RESERVATION_STATUS_COLORS[status] ?? 'default'}>
          {CUSTOMER_RESERVATION_STATUS_LABELS[status] ?? status}
        </Tag>
      ),
    },
    {
      title: 'Hình thức',
      dataIndex: 'fulfillmentType',
      width: 120,
      render: (value: number) => CUSTOMER_RESERVATION_FULFILLMENT_LABELS[value] ?? value,
    },
    { title: 'SP', dataIndex: 'itemCount', width: 60 },
    {
      title: 'Gửi lúc',
      dataIndex: 'submittedAt',
      width: 150,
      render: (value: string) => dayjs(value).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: '',
      width: 80,
      render: (_, row) => (
        <Button type="link" icon={<EyeOutlined />} onClick={() => void openDetail(row)}>
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div style={sectionGapStyle}>
      <Card size="small">
        <Space wrap style={filterBarStyle}>
          <Input.Search
            allowClear
            placeholder="Tìm số yêu cầu, tên, SĐT..."
            style={{ width: 280 }}
            onSearch={setSearch}
          />
          <Select
            allowClear
            placeholder="Trạng thái"
            style={{ width: 180 }}
            options={STATUS_FILTER_OPTIONS}
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
            Chờ xử lý ({awaitingCount})
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </Space>
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
        title={detail?.reservationNumber ?? 'Chi tiết đặt trước'}
        width={520}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        loading={detailLoading}
      >
        {detail ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Descriptions size="small" column={1} bordered>
              <Descriptions.Item label="Trạng thái">
                <Tag color={CUSTOMER_RESERVATION_STATUS_COLORS[detail.status] ?? 'default'}>
                  {CUSTOMER_RESERVATION_STATUS_LABELS[detail.status]}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Hình thức">
                {CUSTOMER_RESERVATION_FULFILLMENT_LABELS[detail.fulfillmentType]}
              </Descriptions.Item>
              {detail.addressSummary ? (
                <Descriptions.Item label="Địa chỉ">{detail.addressSummary}</Descriptions.Item>
              ) : null}
              {detail.notes ? <Descriptions.Item label="Ghi chú KH">{detail.notes}</Descriptions.Item> : null}
              {detail.salesOrderNumber ? (
                <Descriptions.Item label="Hóa đơn bán">{detail.salesOrderNumber}</Descriptions.Item>
              ) : null}
              <Descriptions.Item label="Gửi lúc">
                {dayjs(detail.submittedAt).format('DD/MM/YYYY HH:mm')}
              </Descriptions.Item>
            </Descriptions>

            <List
              size="small"
              header="Sản phẩm"
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
                  placeholder="Ghi chú nội bộ (vd: dự kiến có hàng ngày mai)"
                />
                <Button loading={savingNotes} onClick={() => void saveStaffNotes()}>
                  Lưu ghi chú
                </Button>

                <Space wrap>
                  {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
                    <>
                      <Popconfirm
                        title="Xác nhận yêu cầu?"
                        onConfirm={() =>
                          void runAction('Đã xác nhận', () => confirmCustomerReservation(detail.id))
                        }
                      >
                        <Button type="primary">Xác nhận</Button>
                      </Popconfirm>
                      <Popconfirm
                        title="Từ chối yêu cầu?"
                        onConfirm={() =>
                          void runAction('Đã từ chối', () => rejectCustomerReservation(detail.id))
                        }
                      >
                        <Button danger>Từ chối</Button>
                      </Popconfirm>
                    </>
                  ) : null}
                  {detail.status === CUSTOMER_RESERVATION_STATUS.Confirmed ? (
                    <Popconfirm
                      title="Đánh dấu sẵn sàng?"
                      onConfirm={() =>
                        void runAction('Đã sẵn sàng', () => markCustomerReservationReady(detail.id))
                      }
                    >
                      <Button>Sẵn sàng lấy thuốc</Button>
                    </Popconfirm>
                  ) : null}
                  {canLoadPos(detail) ? (
                    <Button
                      type="primary"
                      icon={<ShoppingCartOutlined />}
                      onClick={() => loadIntoPos(detail.id)}
                    >
                      Bán POS — tạo hóa đơn
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
