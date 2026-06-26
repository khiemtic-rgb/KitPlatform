import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Input, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { fetchCustomers, fetchCustomer } from '@/shared/api/customer-admin.api';
import type { CustomerAdminListItem, CustomerDetail } from '@/shared/api/customer-admin.types';
import { CUSTOMER_STATUS_LABELS } from '@/shared/api/customer-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { CustomerFormDrawer } from '@/modules/customer/CustomerFormDrawer';
import { formatDisplayDate } from '@/shared/utils/date';

export function CustomerListPage() {
  const navigate = useNavigate();
  const canWrite = useHasPermission('sales.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerAdminListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerDetail | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchCustomers({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      setItems([]);
      setTotal(0);
      message.error(apiErrorMessage(error, 'Không tải được danh sách khách hàng'));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setDrawerOpen(true);
  };

  const openEdit = async (row: CustomerAdminListItem, event: React.MouseEvent) => {
    event.stopPropagation();
    try {
      setEditing(await fetchCustomer(row.id));
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được khách hàng'));
    }
  };

  const columns: ColumnsType<CustomerAdminListItem> = [
    {
      title: 'Mã KH',
      dataIndex: 'customerCode',
      width: 120,
    },
    {
      title: 'Họ tên',
      dataIndex: 'fullName',
    },
    {
      title: 'SĐT',
      dataIndex: 'phone',
      width: 130,
    },
    {
      title: 'Email',
      dataIndex: 'email',
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (status: number) => (
        <Tag color={status === 1 ? 'green' : 'default'}>
          {CUSTOMER_STATUS_LABELS[status] ?? status}
        </Tag>
      ),
    },
    {
      title: 'Ngày tạo',
      dataIndex: 'createdAt',
      width: 120,
      render: (v: string) => formatDisplayDate(v),
    },
    ...(canWrite
      ? [
          {
            title: '',
            width: 72,
            render: (_: unknown, row: CustomerAdminListItem) => (
              <Button
                type="link"
                size="small"
                icon={<EditOutlined />}
                onClick={(event) => void openEdit(row, event)}
              >
                Sửa
              </Button>
            ),
          } as ColumnsType<CustomerAdminListItem>[number],
        ]
      : []),
  ];

  return (
    <>
      <Card
        title="Khách hàng"
        extra={
          canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Thêm KH
            </Button>
          ) : null
        }
      >
        <Space wrap style={{ marginBottom: 16 }}>
          <Input
            allowClear
            style={{ width: 280 }}
            placeholder="Tên, SĐT, mã KH, email"
            prefix={<SearchOutlined />}
            value={searchInput}
            onChange={(e) => {
              setSearchInput(e.target.value);
              if (!e.target.value) setSearch('');
            }}
            onPressEnter={() => {
              setPage(1);
              setSearch(searchInput.trim());
            }}
          />
          <Button
            type="primary"
            icon={<SearchOutlined />}
            onClick={() => {
              setPage(1);
              setSearch(searchInput.trim());
            }}
          >
            Tìm
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        </Space>

        <Table
          rowKey="id"
          size="small"
          loading={loading}
          columns={columns}
          dataSource={items}
          onRow={(row) => ({
            style: { cursor: 'pointer' },
            onClick: () => navigate(`/customer/${row.id}`),
          })}
          pagination={{
            current: page,
            pageSize,
            total,
            showSizeChanger: true,
            onChange: (nextPage, nextSize) => {
              setPage(nextPage);
              setPageSize(nextSize);
            },
          }}
        />
      </Card>

      <CustomerFormDrawer
        open={drawerOpen}
        editing={editing}
        onClose={() => setDrawerOpen(false)}
        onSaved={(customer) => {
          void load();
          if (!editing) navigate(`/customer/${customer.id}`);
        }}
      />
    </>
  );
}
