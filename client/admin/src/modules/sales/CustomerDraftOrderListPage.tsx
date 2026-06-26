import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  AutoComplete,
  Button,
  Card,
  Descriptions,
  Drawer,
  Input,
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
  ReloadOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  cancelCustomerDraftOrder,
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_DRAFT_ORDER_STATUS_COLORS,
  CUSTOMER_DRAFT_ORDER_STATUS_FILTER_OPTIONS,
  CUSTOMER_DRAFT_ORDER_STATUS_LABELS,
  fetchCustomerDraftOrder,
  fetchCustomerDraftOrders,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
} from '@/shared/api/customer-draft-orders.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { CustomerDraftOrderStatusBar } from '@/modules/sales/CustomerDraftOrderStatusBar';
import { filterBarStyle, sectionGapStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
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
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<CustomerDraftOrderListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
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
      message.error(apiErrorMessage(error, 'Không tải được đơn tạm app'));
    } finally {
      setLoading(false);
    }
  }, []);

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

  const searchSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const seen = new Set<string>();
    return items
      .filter((row) => {
        if (!q) return false;
        return (
          row.draftNumber.toLowerCase().includes(q) ||
          row.customerName.toLowerCase().includes(q)
        );
      })
      .slice(0, 15)
      .map((row) => {
        const value = row.draftNumber;
        if (seen.has(value)) return null;
        seen.add(value);
        return {
          value,
          label: `${row.draftNumber} — ${row.customerName}`,
        };
      })
      .filter((opt): opt is { value: string; label: string } => opt !== null);
  }, [items, searchInput]);

  const filteredItems = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((row) => {
      if (actionableOnly && !isActionableStatus(row.status)) return false;
      if (activeOnly && !isActiveDraftStatus(row.status)) return false;
      if (statusFilter != null && row.status !== statusFilter) return false;
      if (!q) return true;
      return (
        row.draftNumber.toLowerCase().includes(q) ||
        row.customerName.toLowerCase().includes(q)
      );
    });
  }, [items, search, statusFilter, activeOnly, actionableOnly]);

  const applySearch = (value?: string) => {
    setSearchInput(value ?? searchInput);
    setSearch((value ?? searchInput).trim());
  };

  const openDetailById = useCallback(async (draftOrderId: string) => {
    setDetailOpen(true);
    setDetail(null);
    setDetailLoading(true);
    try {
      setDetail(await fetchCustomerDraftOrder(draftOrderId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chi tiết đơn tạm'));
      setDetailOpen(false);
    } finally {
      setDetailLoading(false);
    }
  }, []);

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
      message.success('Đã hủy đơn tạm');
      setDetailOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được đơn tạm'));
    }
  };

  const columns: ColumnsType<CustomerDraftOrderListItem> = [
    {
      title: 'Số đơn',
      dataIndex: 'draftNumber',
      width: 130,
    },
    {
      title: 'Khách',
      dataIndex: 'customerName',
      ellipsis: true,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 170,
      render: (status: number, row) => (
        <Space size={4} wrap>
          <Tag color={CUSTOMER_DRAFT_ORDER_STATUS_COLORS[status] ?? 'default'}>
            {CUSTOMER_DRAFT_ORDER_STATUS_LABELS[status] ?? status}
          </Tag>
          {row.hiddenByCustomerAt ? <Tag color="default">Ẩn trên app</Tag> : null}
        </Space>
      ),
    },
    {
      title: 'SP',
      dataIndex: 'itemCount',
      width: 56,
      align: 'center',
    },
    {
      title: 'Tổng tiền',
      dataIndex: 'totalAmount',
      width: 120,
      align: 'right',
      render: (value: number) => <TabularMoney>{formatDisplayMoney(value)}</TabularMoney>,
    },
    {
      title: 'Gửi / Xác nhận',
      key: 'timeline',
      width: 150,
      render: (_, row) => {
        if (row.confirmedAt) {
          return (
            <span style={{ fontSize: 12, color: '#059669' }}>
              XN {dayjs(row.confirmedAt).format('DD/MM HH:mm')}
            </span>
          );
        }
        if (row.sentAt) {
          return (
            <span style={{ fontSize: 12, color: '#64748b' }}>
              Gửi {dayjs(row.sentAt).format('DD/MM HH:mm')}
            </span>
          );
        }
        return '—';
      },
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 160,
      render: (_, row) => (
        <Space size={4} onClick={(e) => e.stopPropagation()}>
          <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => void openDetail(row)}>
            Xem
          </Button>
          {isActionableStatus(row.status) && canWrite ? (
            <Button
              type="link"
              size="small"
              icon={<ShoppingCartOutlined />}
              onClick={() => loadIntoPos(row.id)}
            >
              Nạp POS
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  const pendingCount = items.filter((row) => isActionableStatus(row.status)).length;

  return (
    <Card title="Đơn tạm app khách">
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Đơn gửi từ POS (Gửi khách hàng) hiển thị tại đây — không nằm trong tab Đơn bán"
        description="Quy trình: Tạm lưu → Gửi khách → Khách xác nhận tạm (tuỳ chọn) → Nạp POS (thanh toán + in hóa đơn)."
      />

      <Space wrap style={filterBarStyle}>
        <AutoComplete
          style={{ width: 280 }}
          options={searchSuggestions}
          value={searchInput}
          onSelect={(value) => applySearch(String(value))}
          onChange={(value) => {
            setSearchInput(value);
            if (!value) setSearch('');
          }}
        >
          <Input
            allowClear
            placeholder="Số đơn tạm, tên khách"
            prefix={<SearchOutlined />}
            onPressEnter={() => applySearch()}
          />
        </AutoComplete>
        <Button type="primary" icon={<SearchOutlined />} onClick={() => applySearch()}>
          Lọc
        </Button>
        <Select
          allowClear
          placeholder="Trạng thái"
          style={{ width: 180 }}
          value={statusFilter}
          onChange={(value) => {
            setStatusFilter(value);
            setActiveOnly(false);
          }}
          options={CUSTOMER_DRAFT_ORDER_STATUS_FILTER_OPTIONS.map(({ value, label }) => ({
            value,
            label,
          }))}
        />
        <Button
          onClick={() => {
            setSearch('');
            setSearchInput('');
            setStatusFilter(undefined);
            setActiveOnly(false);
            setActionableOnly(false);
          }}
        >
          Xóa lọc
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
          Chờ xử lý ({pendingCount})
        </Button>
        <Button
          onClick={() => {
            setActiveOnly((prev) => !prev);
            setStatusFilter(undefined);
          }}
          type={activeOnly ? 'primary' : 'default'}
          ghost={activeOnly}
        >
          Đang xử lý ({items.filter((row) => isActiveDraftStatus(row.status)).length})
        </Button>
        <Button type="primary" ghost icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Tải lại
        </Button>
      </Space>

      {pendingCount > 0 ? (
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${pendingCount} đơn chờ xử lý tại quầy`}
          description="Đơn 「Khách xác nhận tạm」 hoặc 「Đã gửi khách」 — bấm Nạp POS để thanh toán và in hóa đơn."
        />
      ) : null}

      <Table
        rowKey="id"
        loading={loading}
        dataSource={filteredItems}
        columns={columns}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} đơn tạm` }}
        onRow={(record) => ({
          onClick: () => void openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={detail ? `Xem ${detail.draftNumber}` : 'Xem đơn tạm'}
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
                    ? 'Khách đã xác nhận tạm trên app'
                    : 'Đã gửi app — chờ khách xác nhận (không bắt buộc)'
                }
                description="Hỏi khách tại quầy. Bấm Nạp POS để thanh toán, ghi đơn bán và in hóa đơn."
              />
            ) : null}

            {(isActionableStatus(detail.status) && canWrite) ||
            detail.salesOrderNumber ? (
              <Card size="small" title="Thao tác" style={sectionGapStyle}>
                <Space wrap>
                  {isActionableStatus(detail.status) && canWrite ? (
                    <>
                      <Button
                        type="primary"
                        icon={<ShoppingCartOutlined />}
                        onClick={() => loadIntoPos(detail.id)}
                      >
                        Nạp POS
                      </Button>
                      <Popconfirm title="Hủy đơn tạm?" onConfirm={() => void handleCancel(detail.id)}>
                        <Button danger>Hủy đơn</Button>
                      </Popconfirm>
                    </>
                  ) : null}
                  {detail.salesOrderId && detail.salesOrderNumber ? (
                    <Button
                      type="link"
                      onClick={() => navigate(`/sales/orders?orderId=${detail.salesOrderId}`)}
                    >
                      Xem đơn bán {detail.salesOrderNumber}
                    </Button>
                  ) : null}
                </Space>
              </Card>
            ) : null}

            <Descriptions bordered size="small" column={2} style={sectionGapStyle}>
              <Descriptions.Item label="Khách">{detail.customerName}</Descriptions.Item>
              <Descriptions.Item label="Điện thoại">{detail.customerPhone ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái">
                <Tag color={CUSTOMER_DRAFT_ORDER_STATUS_COLORS[detail.status] ?? 'default'}>
                  {CUSTOMER_DRAFT_ORDER_STATUS_LABELS[detail.status] ?? detail.status}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Tổng tiền">
                {formatDisplayMoney(detail.totalAmount)}
              </Descriptions.Item>
              {detail.sentAt ? (
                <Descriptions.Item label="Gửi app">
                  {dayjs(detail.sentAt).format('DD-MM-YYYY HH:mm')}
                </Descriptions.Item>
              ) : null}
              {detail.confirmedAt ? (
                <Descriptions.Item label="Khách xác nhận">
                  {dayjs(detail.confirmedAt).format('DD-MM-YYYY HH:mm')}
                </Descriptions.Item>
              ) : null}
              {detail.salesOrderNumber ? (
                <Descriptions.Item label="Đơn bán">{detail.salesOrderNumber}</Descriptions.Item>
              ) : null}
            </Descriptions>

            <Table
              style={{ marginTop: 16 }}
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={[
                { title: 'Mã SP', dataIndex: 'productCode', width: 100 },
                { title: 'Tên SP', dataIndex: 'productName', ellipsis: true },
                { title: 'ĐVT', dataIndex: 'unitName', width: 72 },
                { title: 'SL', dataIndex: 'quantity', width: 56, align: 'right' },
                {
                  title: 'Đơn giá',
                  dataIndex: 'unitPrice',
                  width: 100,
                  align: 'right',
                  render: (v: number) => formatDisplayMoney(v),
                },
                {
                  title: 'Thành tiền',
                  dataIndex: 'lineAmount',
                  width: 110,
                  align: 'right',
                  render: (v: number) => formatDisplayMoney(v),
                },
              ]}
            />
          </>
        ) : null}
      </Drawer>
    </Card>
  );
}
