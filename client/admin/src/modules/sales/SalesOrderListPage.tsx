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
  EditOutlined,
  PrinterOutlined,
  ReloadOutlined,
  RollbackOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import {
  cancelDraftSale,
  completeDraftSale,
  createSaleReturn,
  fetchOrderReturns,
  fetchSalesOrder,
  fetchSalesOrders,
  fetchSalesReturn,
} from '@/shared/api/sales.api';
import type { SalesOrderDetail, SalesOrderListItem, SalesReturnListItem } from '@/shared/api/sales.types';
import { SALES_RETURN_STATUS_LABELS } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import {
  matchesSaleStatusFilter,
  orderDisplayStatus,
  PARTIAL_RETURN_STATUS,
  SALE_STATUS_FILTER_OPTIONS,
} from '@/modules/sales/sales-order-status';
import { OrderDetailFinancials } from '@/modules/sales/OrderDetailFinancials';
import { PosCheckoutModal } from '@/modules/sales/PosCheckoutModal';
import { SalesReturnDetailDrawer } from '@/modules/sales/SalesReturnDetailDrawer';
import { SalesReturnModal } from '@/modules/sales/SalesReturnModal';
import { printSalesInvoice } from '@/modules/sales/sales-invoice-print';
import { printSalesReturn } from '@/modules/sales/sales-return-print';
import { filterBarStyle, sectionGapStyle, sectionGapTopStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

function isCompletedSaleStatus(status: number): boolean {
  return status === 2 || status === 4;
}

export function SalesOrderListPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canRead = useHasPermission('sales.read');
  const canWrite = useHasPermission('sales.write');
  const [items, setItems] = useState<SalesOrderListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
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
  const [draftCompleting, setDraftCompleting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchSalesOrders());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn bán'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const onFocus = () => void load();
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, [load]);

  const searchSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const seen = new Set<string>();
    return items
      .filter((row) => {
        if (!q) return false;
        return (
          row.orderNumber.toLowerCase().includes(q) ||
          row.warehouseName.toLowerCase().includes(q) ||
          (row.customerName?.toLowerCase().includes(q) ?? false)
        );
      })
      .slice(0, 15)
      .map((row) => {
        const value = row.orderNumber;
        if (seen.has(value)) return null;
        seen.add(value);
        return {
          value,
          label: `${row.orderNumber} — ${row.warehouseName}${row.customerName ? ` · ${row.customerName}` : ''}`,
        };
      })
      .filter((opt): opt is { value: string; label: string } => opt !== null);
  }, [items, searchInput]);

  const filteredItems = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((row) => {
      if (!matchesSaleStatusFilter(row, statusFilter)) return false;
      if (!q) return true;
      return (
        row.orderNumber.toLowerCase().includes(q) ||
        row.warehouseName.toLowerCase().includes(q) ||
        (row.customerName?.toLowerCase().includes(q) ?? false)
      );
    });
  }, [items, search, statusFilter]);

  const applySearch = (value?: string) => {
    setSearchInput(value ?? searchInput);
    setSearch((value ?? searchInput).trim());
  };

  const loadOrderReturns = useCallback(async (orderId: string) => {
    setOrderReturnsLoading(true);
    try {
      setOrderReturns(await fetchOrderReturns(orderId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiếu trả của đơn'));
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
        message.error(apiErrorMessage(error, 'Không tải được chi tiết đơn'));
        setDetailOpen(false);
      } finally {
        setDetailLoading(false);
      }
    },
    [loadOrderReturns],
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
    void load();
  };

  const handleCompleteDraft = () => {
    if (!detail) return;
    setDraftCheckoutOpen(true);
  };

  const confirmCompleteDraft = async (payments: { paymentMethod: number; amount: number }[]) => {
    if (!detail) {
      message.error('Không tìm thấy đơn nháp');
      throw new Error('missing-draft');
    }
    setDraftCompleting(true);
    try {
      const updated = await completeDraftSale(detail.id, payments);
      setDetail(updated);
      setDraftCheckoutOpen(false);
      message.success('Đã hoàn tất đơn nháp');
      if (!(await printSalesInvoice(updated))) {
        message.warning('Trình duyệt chặn cửa sổ in — bấm In hóa đơn trong chi tiết đơn.');
      }
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hoàn tất được đơn'));
      throw error;
    } finally {
      setDraftCompleting(false);
    }
  };

  const handleCancelDraft = async () => {
    if (!detail) return;
    try {
      await cancelDraftSale(detail.id);
      message.success('Đã hủy đơn nháp');
      setDetailOpen(false);
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được đơn'));
    }
  };

  const printOrderById = async (id: string) => {
    try {
      if (!(await printSalesInvoice(await fetchSalesOrder(id)))) {
        message.warning('Trình duyệt chặn cửa sổ in — cho phép popup và thử lại.');
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không in được hóa đơn'));
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
        message.warning('Trình duyệt chặn cửa sổ in — cho phép popup và thử lại.');
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không in được phiếu trả'));
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
      message.success(`Đã ghi nhận trả hàng ${ret.returnNumber}`);
      setReturnOpen(false);
      if (!(await printSalesReturn(ret))) {
        message.warning('Trình duyệt chặn cửa sổ in — mở lại từ chi tiết đơn.');
      }
      await refreshDetail(detail.id);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không trả hàng được'));
    } finally {
      setReturnSaving(false);
    }
  };

  const renderOrderStatus = (row: SalesOrderListItem & { items?: SalesOrderDetail['items'] }) => {
    const { label, color } = orderDisplayStatus(row);
    return <Tag color={color}>{label}</Tag>;
  };

  const columns: ColumnsType<SalesOrderListItem> = [
    { title: 'Số đơn', dataIndex: 'orderNumber', width: 130 },
    { title: 'Kho', dataIndex: 'warehouseName', width: 140 },
    { title: 'Khách', dataIndex: 'customerName', render: (v) => v ?? '—' },
    {
      title: 'Ngày',
      dataIndex: 'orderDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 160,
      render: (_: number, row) => renderOrderStatus(row),
    },
    {
      title: 'Tổng tiền',
      dataIndex: 'totalAmount',
      width: 120,
      align: 'right',
      render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
    },
    {
      title: 'Thao tác',
      width: 130,
      render: (_, row) => (
        <Space size="small" onClick={(e) => e.stopPropagation()}>
          <Button
            type="link"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => void openDetail(row)}
          >
            Xem
          </Button>
          {isCompletedSaleStatus(row.status) && canRead && (
            <Button
              type="link"
              size="small"
              icon={<PrinterOutlined />}
              title="In hóa đơn"
              onClick={() => void printOrderById(row.id)}
            />
          )}
        </Space>
      ),
    },
  ];

  const lineColumns: ColumnsType<SalesOrderDetail['items'][number]> = [
    { title: 'Mã SP', dataIndex: 'productCode', width: 100 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'Lô', dataIndex: 'batchNumber', width: 100, render: (v?: string) => v ?? '—' },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 100,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    { title: 'ĐVT', dataIndex: 'unitName', width: 70 },
    {
      title: 'SL',
      dataIndex: 'quantity',
      width: 70,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: 'Đã trả',
      dataIndex: 'returnedQuantity',
      width: 70,
      align: 'right',
      render: (v?: number) => (v ? v.toLocaleString('vi-VN') : '—'),
    },
    {
      title: 'Đơn giá',
      dataIndex: 'unitPrice',
      width: 100,
      align: 'right',
      render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
    },
    {
      title: 'Thành tiền',
      dataIndex: 'lineTotal',
      width: 110,
      align: 'right',
      render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
    },
  ];

  const returnColumns: ColumnsType<SalesReturnListItem> = [
    {
      title: 'Số phiếu',
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
      title: 'Ngày',
      dataIndex: 'returnDate',
      width: 100,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 90,
      render: (status: number) => (
        <Tag>{SALES_RETURN_STATUS_LABELS[status] ?? status}</Tag>
      ),
    },
    {
      title: 'Tổng hoàn tiền',
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
            <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openReturnDetail(row.id)}>
              Xem
            </Button>
            <Button
              type="link"
              size="small"
              icon={<PrinterOutlined />}
              onClick={() => void printReturnById(row.id)}
            >
              In
            </Button>
          </Space>
        ) : null,
    },
  ];

  const returnableLines =
    detail?.items.filter(
      (line) => line.batchId && line.quantity - (line.returnedQuantity ?? 0) > 0.0001,
    ) ?? [];

  return (
    <Card title="Đơn bán hàng">
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
            placeholder="Số đơn, kho, khách hàng"
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
          style={{ width: 140 }}
          value={statusFilter}
          onChange={setStatusFilter}
          options={SALE_STATUS_FILTER_OPTIONS.map(({ value, label }) => ({ value, label }))}
        />
        <Button
          onClick={() => {
            setSearch('');
            setSearchInput('');
            setStatusFilter(undefined);
          }}
        >
          Xóa lọc
        </Button>
        <Button type="primary" ghost icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Tải lại
        </Button>
      </Space>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={filteredItems}
        columns={columns}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} đơn` }}
        onRow={(record) => ({
          onClick: () => void openDetail(record),
          style: { cursor: 'pointer' },
        })}
      />

      <Drawer
        title={detail ? `Xem ${detail.orderNumber}` : 'Xem đơn bán'}
        width={720}
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
                message="Đơn nháp chưa trừ tồn kho"
                description="Bấm Hoàn tất đơn để xuất hóa đơn, trừ tồn theo FEFO và cho phép trả hàng sau này."
              />
            )}

            {(detail.status === 1 && canWrite) ||
            (isCompletedSaleStatus(detail.status) && (canRead || canWrite)) ? (
              <Card size="small" title="Thao tác" style={sectionGapStyle}>
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
                        Tiếp tục sửa
                      </Button>
                      <Button type="primary" onClick={handleCompleteDraft}>
                        Hoàn tất đơn
                      </Button>
                      <Popconfirm title="Hủy đơn nháp?" onConfirm={() => void handleCancelDraft()}>
                        <Button danger>Hủy đơn</Button>
                      </Popconfirm>
                    </>
                  )}
                  {isCompletedSaleStatus(detail.status) && canRead && (
                    <Button icon={<PrinterOutlined />} onClick={() => void printSalesInvoice(detail)}>
                      In hóa đơn
                    </Button>
                  )}
                  {isCompletedSaleStatus(detail.status) &&
                    canWrite &&
                    returnableLines.length > 0 && (
                      <Button icon={<RollbackOutlined />} onClick={openReturnModal}>
                        Trả hàng
                      </Button>
                    )}
                </Space>
              </Card>
            ) : null}

            <Descriptions column={2} size="small" bordered style={sectionGapStyle}>
              <Descriptions.Item label="Kho">{detail.warehouseName}</Descriptions.Item>
              <Descriptions.Item label="Khách">{detail.customerName ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="Trạng thái">
                {renderOrderStatus({
                  ...detail,
                  itemCount: detail.items.length,
                  items: detail.items,
                })}
              </Descriptions.Item>
              <Descriptions.Item label="Ngày">{formatDisplayDate(detail.orderDate)}</Descriptions.Item>
              <Descriptions.Item label="Ca">{detail.shiftNumber ?? '—'}</Descriptions.Item>
            </Descriptions>

            <OrderDetailFinancials order={detail} />

            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={detail.items}
              columns={lineColumns}
            />

            {orderReturns.length > 0 || orderReturnsLoading ? (
              <Card
                size="small"
                title="Phiếu trả hàng"
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
          onCancel={() => setDraftCheckoutOpen(false)}
          onConfirm={(payments) => confirmCompleteDraft(payments)}
        />
      )}
    </Card>
  );
}
