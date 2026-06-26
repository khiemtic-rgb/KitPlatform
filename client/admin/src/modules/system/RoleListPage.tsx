import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Card, Checkbox, Drawer, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import {
  fetchPermissions,
  fetchRole,
  fetchRoles,
  updateRolePermissions,
} from '@/shared/api/identity-admin.api';
import type { PermissionLookup, RoleListItem } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';

export function RoleListPage() {
  const canWrite = useHasPermission('system.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<RoleListItem[]>([]);
  const [permissions, setPermissions] = useState<PermissionLookup[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<RoleListItem | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchRoles());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được vai trò'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    void fetchPermissions().then(setPermissions).catch(() => setPermissions([]));
  }, [load]);

  const openEdit = async (row: RoleListItem) => {
    try {
      const detail = await fetchRole(row.id);
      setEditingRole(row);
      setSelectedCodes(detail.permissionCodes);
      setDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được vai trò'));
    }
  };

  const handleSave = async () => {
    if (!editingRole) return;
    setSaving(true);
    try {
      await updateRolePermissions(editingRole.id, selectedCodes);
      message.success('Đã cập nhật quyền vai trò');
      setDrawerOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được quyền'));
    } finally {
      setSaving(false);
    }
  };

  const groupedPermissions = useMemo(() => {
    const groups = new Map<string, PermissionLookup[]>();
    for (const p of permissions) {
      const list = groups.get(p.moduleName) ?? [];
      list.push(p);
      groups.set(p.moduleName, list);
    }
    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [permissions]);

  const columns: ColumnsType<RoleListItem> = [
    { title: 'Mã vai trò', dataIndex: 'roleCode', width: 120 },
    { title: 'Tên vai trò', dataIndex: 'roleName' },
    { title: 'Mô tả', dataIndex: 'description', render: (v?: string) => v ?? '—' },
    { title: 'Người dùng', dataIndex: 'userCount', width: 100 },
    { title: 'Quyền', dataIndex: 'permissionCount', width: 80 },
    {
      title: '',
      width: 80,
      render: (_, row) =>
        canWrite ? (
          <Button type="link" icon={<EditOutlined />} onClick={() => void openEdit(row)}>
            Quyền
          </Button>
        ) : null,
    },
  ];

  return (
    <>
      <Card
        title="Vai trò & phân quyền"
        extra={
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer
        title={editingRole ? `Quyền: ${editingRole.roleName}` : 'Quyền vai trò'}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={520}
        extra={
          canWrite ? (
            <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void handleSave()}>
              Lưu
            </Button>
          ) : null
        }
      >
        {editingRole ? (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Tag color="blue">{editingRole.roleCode}</Tag>
            {groupedPermissions.map(([moduleName, modulePermissions]) => (
              <div key={moduleName}>
                <Typography.Text strong>{moduleName}</Typography.Text>
                <Checkbox.Group
                  style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 8 }}
                  value={selectedCodes}
                  disabled={!canWrite}
                  onChange={(values) => setSelectedCodes(values as string[])}
                  options={modulePermissions.map((p) => ({
                    value: p.permissionCode,
                    label: `${p.permissionName} (${p.permissionCode})`,
                  }))}
                />
              </div>
            ))}
          </Space>
        ) : null}
      </Drawer>
    </>
  );
}
