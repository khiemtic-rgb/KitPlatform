import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Input, Popconfirm, Space, Table, Tag, Tooltip, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { deleteUser, fetchUser, fetchUsers } from '@/shared/api/identity-admin.api';
import type { UserDetail, UserListItem } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useHasPermission } from '@/shared/auth/usePermission';
import { UserFormDrawer } from '@/modules/system/UserFormDrawer';
import { useSystemEnums } from '@/shared/i18n/use-system-enums';

export function UserListPage() {
  const { t } = useTranslation('system', { keyPrefix: 'users' });
  const { t: tc } = useTranslation('common');
  const { userStatusLabel } = useSystemEnums();
  const canWrite = useHasPermission('system.write');
  const currentUserId = useAuthStore((s) => s.user?.id);
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
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, t]);

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
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const handleDelete = async (row: UserListItem) => {
    try {
      await deleteUser(row.id);
      message.success(t('messages.deleted', { username: row.username }));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.deleteFailed')));
    }
  };

  const columns: ColumnsType<UserListItem> = useMemo(
    () => [
      {
        title: t('columns.username'),
        dataIndex: 'username',
        width: 120,
        ellipsis: true,
      },
      {
        title: t('columns.fullName'),
        dataIndex: 'employeeName',
        width: 220,
        ellipsis: true,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.phone'),
        dataIndex: 'employeePhone',
        width: 130,
        render: (v?: string) => v ?? '—',
      },
      {
        title: t('columns.roles'),
        dataIndex: 'roleCodes',
        width: 110,
        render: (codes: string[]) =>
          codes.length ? codes.map((c) => <Tag key={c}>{c}</Tag>) : '—',
      },
      {
        title: t('columns.lastLogin'),
        dataIndex: 'lastLoginAt',
        width: 148,
        render: (v?: string) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '—'),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 108,
        align: 'center',
        render: (v: number) => (
          <Tag color={v === 1 ? 'green' : 'default'}>{userStatusLabel(v)}</Tag>
        ),
      },
      {
        title: t('columns.actions'),
        key: 'actions',
        width: 88,
        fixed: 'right',
        align: 'center',
        render: (_, row) =>
          canWrite ? (
            <Space size={4}>
              <Tooltip title={tc('actions.edit')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={tc('actions.edit')}
                  onClick={() => void openEdit(row)}
                />
              </Tooltip>
              <Popconfirm
                title={t('deleteConfirm', { username: row.username })}
                description={t('deleteDescription')}
                okText={tc('actions.delete')}
                cancelText={tc('actions.cancel')}
                okButtonProps={{ danger: true }}
                disabled={row.id === currentUserId}
                onConfirm={() => void handleDelete(row)}
              >
                <Tooltip
                  title={
                    row.id === currentUserId
                      ? t('cannotDeleteSelf')
                      : tc('actions.delete')
                  }
                >
                  <span>
                    <Button
                      type="text"
                      size="small"
                      danger
                      disabled={row.id === currentUserId}
                      icon={<DeleteOutlined />}
                      aria-label={tc('actions.delete')}
                      style={row.id === currentUserId ? { opacity: 0.35 } : undefined}
                    />
                  </span>
                </Tooltip>
              </Popconfirm>
            </Space>
          ) : null,
      },
    ],
    [canWrite, currentUserId, t, tc, userStatusLabel],
  );

  return (
    <>
      <Card
        title={t('title')}
        extra={
          <Space>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={t('searchPlaceholder')}
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
              {tc('actions.search')}
            </Button>
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              {tc('actions.reload')}
            </Button>
            {canWrite ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('add')}
              </Button>
            ) : null}
          </Space>
        }
      >
        <Table
          rowKey="id"
          size="middle"
          loading={loading}
          columns={columns}
          dataSource={items}
          scroll={{ x: 920 }}
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
        onClose={() => {
          setDrawerOpen(false);
          setEditing(null);
        }}
        onSaved={() => void load()}
      />
    </>
  );
}
