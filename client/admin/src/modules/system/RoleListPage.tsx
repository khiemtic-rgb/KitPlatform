import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Checkbox,
  Drawer,
  Form,
  Input,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, PlusOutlined, ReloadOutlined, SaveOutlined, SettingOutlined } from '@ant-design/icons';
import {
  createRole,
  fetchPermissions,
  fetchRole,
  fetchRoles,
  updateRole,
  updateRolePermissions,
} from '@/shared/api/identity-admin.api';
import type { PermissionLookup, RoleListItem } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  type DiscountLevel,
  applyPermissionToggle,
  discountLevelLabel,
  getDiscountLevel,
  groupPermissionsForUi,
  normalizePermissionCodesForSave,
  setDiscountLevel,
} from '@/shared/auth/permission-picker';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSystemEnums } from '@/shared/i18n/use-system-enums';

interface RoleFormValues {
  roleCode: string;
  roleName: string;
  description?: string;
  status: number;
}

const DISCOUNT_LEVELS: DiscountLevel[] = ['none', 'sales.discount', 'sales.discount.unlimited'];

export function RoleListPage() {
  const { t } = useTranslation('system', { keyPrefix: 'roles' });
  const { t: tc, i18n } = useTranslation('common');
  const { userStatusOptions } = useSystemEnums();
  const canWrite = useHasPermission('system.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<RoleListItem[]>([]);
  const [permissions, setPermissions] = useState<PermissionLookup[]>([]);
  const [formDrawerOpen, setFormDrawerOpen] = useState(false);
  const [permDrawerOpen, setPermDrawerOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<RoleListItem | null>(null);
  const [permRole, setPermRole] = useState<RoleListItem | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [savingPerms, setSavingPerms] = useState(false);
  const [form] = Form.useForm<RoleFormValues>();

  const permissionGroups = useMemo(
    () => groupPermissionsForUi(permissions),
    [permissions, i18n.language],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchRoles());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
    void fetchPermissions().then(setPermissions).catch(() => setPermissions([]));
  }, [load]);

  const openCreate = () => {
    setEditingRole(null);
    form.resetFields();
    form.setFieldsValue({ status: 1 });
    setFormDrawerOpen(true);
  };

  const openEdit = (row: RoleListItem) => {
    setEditingRole(row);
    form.setFieldsValue({
      roleCode: row.roleCode,
      roleName: row.roleName,
      description: row.description,
      status: row.status,
    });
    setFormDrawerOpen(true);
  };

  const openPermissions = async (row: RoleListItem) => {
    try {
      const detail = await fetchRole(row.id);
      setPermRole(row);
      setSelectedCodes(detail.permissionCodes);
      setPermDrawerOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.permissionsLoadFailed')));
    }
  };

  const togglePermission = (code: string, checked: boolean) => {
    setSelectedCodes((prev) => applyPermissionToggle(prev, code, checked));
  };

  const handleSaveRole = async () => {
    let values: RoleFormValues;
    try {
      values = await form.validateFields();
    } catch (error) {
      const first = (error as { errorFields?: { errors: string[] }[] })?.errorFields?.[0]?.errors?.[0];
      if (first) message.warning(first);
      return;
    }

    const code = values.roleCode.trim().toUpperCase();
    const payload = {
      roleCode: code,
      roleName: values.roleName.trim(),
      description: values.description?.trim() || undefined,
      status: values.status,
    };

    setSaving(true);
    try {
      if (editingRole) {
        await updateRole(editingRole.id, payload);
        message.success(t('messages.updated'));
      } else {
        await createRole(payload);
        message.success(t('messages.created'));
      }
      setFormDrawerOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const handleSavePermissions = async () => {
    if (!permRole) return;
    setSavingPerms(true);
    try {
      await updateRolePermissions(permRole.id, normalizePermissionCodesForSave(selectedCodes));
      message.success(t('messages.permissionsUpdated'));
      setPermDrawerOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.permissionsSaveFailed')));
    } finally {
      setSavingPerms(false);
    }
  };

  const columns: ColumnsType<RoleListItem> = useMemo(
    () => [
      { title: t('columns.code'), dataIndex: 'roleCode', width: 120 },
      { title: t('columns.name'), dataIndex: 'roleName' },
      { title: t('columns.description'), dataIndex: 'description', render: (v?: string) => v ?? '—' },
      { title: t('columns.userCount'), dataIndex: 'userCount', width: 100 },
      { title: t('columns.permissionCount'), dataIndex: 'permissionCount', width: 80 },
      {
        title: t('columns.actions'),
        width: 120,
        render: (_, row) =>
          canWrite ? (
            <Space size={4}>
              <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
                {tc('actions.edit')}
              </Button>
              <Button
                type="link"
                size="small"
                icon={<SettingOutlined />}
                onClick={() => void openPermissions(row)}
              >
                {t('permissions')}
              </Button>
            </Space>
          ) : null,
      },
    ],
    [canWrite, t, tc],
  );

  return (
    <>
      <Card
        title={t('title')}
        extra={
          <Space>
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
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer
        title={editingRole ? t('edit') : t('create')}
        open={formDrawerOpen}
        onClose={() => setFormDrawerOpen(false)}
        width={420}
        extra={
          canWrite ? (
            <Space>
              <Button onClick={() => setFormDrawerOpen(false)}>{tc('actions.cancel')}</Button>
              <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void handleSaveRole()}>
                {tc('actions.save')}
              </Button>
            </Space>
          ) : null
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="roleCode"
            label={t('form.code')}
            rules={[{ required: true, message: t('form.codeRequired') }]}
            extra={t('form.codeExtra')}
          >
            <Input placeholder="STAFF" autoComplete="off" disabled={Boolean(editingRole)} />
          </Form.Item>
          <Form.Item
            name="roleName"
            label={t('form.name')}
            rules={[{ required: true, message: t('form.nameRequired') }]}
          >
            <Input placeholder={t('form.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('form.description')}>
            <Input.TextArea rows={2} placeholder={t('form.descriptionPlaceholder')} />
          </Form.Item>
          <Form.Item name="status" label={t('form.status')}>
            <Select options={userStatusOptions} />
          </Form.Item>
        </Form>
      </Drawer>

      <Drawer
        title={permRole ? t('permissionsTitle', { name: permRole.roleName }) : t('permissionsTitleDefault')}
        open={permDrawerOpen}
        onClose={() => setPermDrawerOpen(false)}
        width={520}
        extra={
          canWrite ? (
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={savingPerms}
              onClick={() => void handleSavePermissions()}
            >
              {tc('actions.save')}
            </Button>
          ) : null
        }
      >
        {permRole ? (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {t('permissionsHint')}
            </Typography.Paragraph>
            <Tag color="blue">{permRole.roleCode}</Tag>
            {permissionGroups.map((group) => (
              <div key={group.moduleKey}>
                <Typography.Text strong>{group.moduleLabel}</Typography.Text>
                {group.hint ? (
                  <Typography.Paragraph type="secondary" style={{ margin: '4px 0 8px', fontSize: 12 }}>
                    {group.hint}
                  </Typography.Paragraph>
                ) : null}
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  {group.items.map((item) => (
                    <Checkbox
                      key={item.code}
                      disabled={!canWrite}
                      checked={selectedCodes.includes(item.code)}
                      onChange={(e) => togglePermission(item.code, e.target.checked)}
                    >
                      {item.label}
                    </Checkbox>
                  ))}
                </Space>
                {'discountCodes' in group && group.discountCodes?.length ? (
                  <>
                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 12 }}>
                      {t('discountSelectOne')}
                    </Typography.Text>
                    <Radio.Group
                      style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 8 }}
                      disabled={!canWrite}
                      value={getDiscountLevel(selectedCodes)}
                      onChange={(e) =>
                        setSelectedCodes((prev) => setDiscountLevel(prev, e.target.value as DiscountLevel))
                      }
                      options={DISCOUNT_LEVELS.map((level) => ({
                        value: level,
                        label: discountLevelLabel(level),
                      }))}
                    />
                  </>
                ) : null}
              </div>
            ))}
          </Space>
        ) : null}
      </Drawer>
    </>
  );
}
