import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Input, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { fetchUser, fetchUsers } from '@/shared/api/identity-admin.api';
import type { UserDetail, UserListItem } from '@/shared/api/identity-admin.types';
import { USER_STATUS_LABELS } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { UserFormDrawer } from '@/modules/system/UserFormDrawer';

export function UserListPage() {
  const canWrite = useHasPermission('system.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<UserListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<UserDetail | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await fetchUsers({ search: search || undefined, page, pageSize });
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách tài khoản'));
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

  const openEdit = async (row: UserListItem) => {
    try {
      setEditing(await fetchUser(row.id));
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tài khoản'));
    }
  };

  const columns: ColumnsType<UserListItem> = [
    { title: 'Tên đăng nhập', dataIndex: 'username', width: 140 },
    { title: 'Email', dataIndex: 'email' },
    { title: 'Nhân viên', dataIndex: 'employeeName', render: (v?: string) => v ?? '—' },
    {
      title: 'Vai trò',
      dataIndex: 'roleCodes',
      render: (codes: string[]) =>
        codes.length ? codes.map((c) => <Tag key={c}>{c}</Tag>) : '—',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 1 ? 'green' : 'default'}>{USER_STATUS_LABELS[v] ?? v}</Tag>
      ),
    },
    {
      title: 'Đăng nhập cuối',
      dataIndex: 'lastLoginAt',
      width: 150,
      render: (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: '',
      width: 80,
      render: (_, row) =>
        canWrite ? (
          <Button type="link" icon={<EditOutlined />} onClick={() => void openEdit(row)}>
            Sửa
          </Button>
        ) : null,
    },
  ];

  return (
    <>
      <Card
        title="Tài khoản nhân viên"
        extra={
          <Space>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder="Tìm username, email, tên..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onPressEnter={() => {
                setPage(1);
                setSearch(searchInput.trim());
              }}
              style={{ width: 260 }}
            />
            <Button
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
            {canWrite ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                Thêm tài khoản
              </Button>
            ) : null}
          </Space>
        }
      >
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
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

      <UserFormDrawer
        open={drawerOpen}
        editing={editing}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => void load()}
      />
    </>
  );
}
