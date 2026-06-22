import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AutoComplete,
  Button,
  Card,
  Input,
  Space,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined, PrinterOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchSalesReturn, fetchSalesReturns } from '@/shared/api/sales.api';
import type { SalesReturnListItem } from '@/shared/api/sales.types';
import { SALES_RETURN_STATUS_LABELS } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { SalesReturnDetailDrawer } from '@/modules/sales/SalesReturnDetailDrawer';
import { filterBarStyle, TabularMoney } from '@/modules/sales/sales-ui-styles';
import { printSalesReturn } from '@/modules/sales/sales-return-print';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';

export function SalesReturnListPage() {
  const canRead = useHasPermission('sales.read');
  const navigate = useNavigate();
  const [items, setItems] = useState<SalesReturnListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailReturnId, setDetailReturnId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchSalesReturns(search || undefined));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được phiếu trả'));
    } finally {
      setLoading(false);
    }
  }, [search]);

  useEffect(() => {
    void load();
  }, [load]);

  const searchSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    const seen = new Set<string>();
    return items
      .filter((row) => {
        if (!q) return false;
        return (
          row.returnNumber.toLowerCase().includes(q) ||
          row.orderNumber.toLowerCase().includes(q)
        );
      })
      .slice(0, 15)
      .map((row) => {
        const value = row.returnNumber;
        if (seen.has(value)) return null;
        seen.add(value);
        return {
          value,
          label: `${row.returnNumber} — ${row.orderNumber}`,
        };
      })
      .filter((opt): opt is { value: string; label: string } => opt !== null);
  }, [items, searchInput]);

  const applySearch = (value?: string) => {
    setSearchInput(value ?? searchInput);
    setSearch((value ?? searchInput).trim());
  };

  const openDetail = (id: string) => {
    setDetailReturnId(id);
    setDetailOpen(true);
  };

  const printReturnById = async (id: string) => {
    try {
      if (!printSalesReturn(await fetchSalesReturn(id))) {
        message.warning('Trình duyệt chặn cửa sổ in — cho phép popup và thử lại.');
      }
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không in được phiếu trả'));
    }
  };

  const columns: ColumnsType<SalesReturnListItem> = [
    {
      title: 'Số phiếu',
      dataIndex: 'returnNumber',
      width: 130,
      render: (value: string, row) => (
        <Button type="link" size="small" onClick={() => openDetail(row.id)}>
          {value}
        </Button>
      ),
    },
    {
      title: 'Đơn bán',
      dataIndex: 'orderNumber',
      width: 130,
      render: (value: string, row) => (
        <Button
          type="link"
          size="small"
          onClick={(e) => {
            e.stopPropagation();
            navigate(`/sales/orders?orderId=${row.salesOrderId}`);
          }}
        >
          {value}
        </Button>
      ),
    },
    {
      title: 'Ngày trả',
      dataIndex: 'returnDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 100,
      render: (status: number) => (
        <Tag>{SALES_RETURN_STATUS_LABELS[status] ?? status}</Tag>
      ),
    },
    {
      title: 'Ca',
      dataIndex: 'shiftNumber',
      width: 100,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Tổng hoàn tiền',
      dataIndex: 'totalRefund',
      width: 120,
      align: 'right',
      render: (v: number) => <TabularMoney>{formatDisplayMoney(v)}</TabularMoney>,
    },
    {
      title: 'Thao tác',
      width: 130,
      render: (_, row) =>
        canRead ? (
          <Space size="small" onClick={(e) => e.stopPropagation()}>
            <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openDetail(row.id)}>
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

  return (
    <Card title="Phiếu trả hàng">
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
            placeholder="Số phiếu, số đơn"
            prefix={<SearchOutlined />}
            onPressEnter={() => applySearch()}
          />
        </AutoComplete>
        <Button type="primary" icon={<SearchOutlined />} onClick={() => applySearch()}>
          Lọc
        </Button>
        <Button
          onClick={() => {
            setSearch('');
            setSearchInput('');
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
        dataSource={items}
        columns={columns}
        pagination={{ pageSize: 20, showTotal: (total) => `${total} phiếu` }}
        onRow={(record) => ({
          onClick: () => openDetail(record.id),
          style: { cursor: 'pointer' },
        })}
      />

      <SalesReturnDetailDrawer
        open={detailOpen}
        returnId={detailReturnId}
        onClose={() => {
          setDetailOpen(false);
          setDetailReturnId(null);
        }}
        onOpenOrder={(orderId) => {
          setDetailOpen(false);
          setDetailReturnId(null);
          navigate(`/sales/orders?orderId=${orderId}`);
        }}
      />
    </Card>
  );
}
