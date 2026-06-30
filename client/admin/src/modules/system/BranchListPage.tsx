import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, Drawer, Form, Input, Popconfirm, Select, Space, Switch, Table, Tag, Tooltip, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  createBranch,
  deleteBranch,
  fetchBranches,
  updateBranch,
} from '@/shared/api/identity-admin.api';
import type { BranchListItem } from '@/shared/api/identity-admin.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useSystemEnums } from '@/shared/i18n/use-system-enums';

interface BranchFormValues {
  branchCode: string;
  branchName: string;
  address?: string;
  phone?: string;
  isHeadOffice?: boolean;
  status?: number;
}

export function BranchListPage() {
  const { t } = useTranslation('system', { keyPrefix: 'branches' });
  const { t: tc } = useTranslation('common');
  const { branchStatusLabel, branchStatusOptions } = useSystemEnums();
  const canWrite = useHasPermission('system.write');
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<BranchListItem[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<BranchListItem | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<BranchFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchBranches());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ status: 1, isHeadOffice: false });
    setDrawerOpen(true);
  };

  const openEdit = (row: BranchListItem) => {
    setEditing(row);
    form.setFieldsValue({
      branchCode: row.branchCode,
      branchName: row.branchName,
      address: row.address,
      phone: row.phone,
      isHeadOffice: row.isHeadOffice,
      status: row.status,
    });
    setDrawerOpen(true);
  };

  const handleSave = async () => {
    let values: BranchFormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }
    setSaving(true);
    try {
      const code = values.branchCode.trim().toUpperCase();
      if (editing) {
        await updateBranch(editing.id, {
          branchCode: code,
          branchName: values.branchName.trim(),
          address: values.address?.trim(),
          phone: values.phone?.trim(),
          isHeadOffice: values.isHeadOffice ?? false,
          status: values.status ?? 1,
        });
        message.success(t('messages.updated'));
      } else {
        await createBranch({
          branchCode: code,
          branchName: values.branchName.trim(),
          address: values.address?.trim(),
          phone: values.phone?.trim(),
          isHeadOffice: values.isHeadOffice ?? false,
          status: values.status ?? 1,
        });
        message.success(t('messages.created'));
      }
      setDrawerOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (row: BranchListItem) => {
    try {
      await deleteBranch(row.id);
      message.success(t('messages.deleted', { code: row.branchCode }));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.deleteFailed')));
    }
  };

  const columns: ColumnsType<BranchListItem> = useMemo(
    () => [
      { title: t('columns.code'), dataIndex: 'branchCode', width: 100 },
      { title: t('columns.name'), dataIndex: 'branchName' },
      { title: t('columns.phone'), dataIndex: 'phone', width: 130, render: (v?: string) => v ?? '—' },
      {
        title: t('columns.headOffice'),
        dataIndex: 'isHeadOffice',
        width: 90,
        render: (v: boolean) => (v ? <Tag color="blue">{t('headOfficeTag')}</Tag> : '—'),
      },
      {
        title: t('columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (v: number) => (
          <Tag color={v === 1 ? 'green' : 'default'}>{branchStatusLabel(v)}</Tag>
        ),
      },
      {
        title: t('columns.actions'),
        width: 100,
        render: (_, row) =>
          canWrite ? (
            <Space size={4}>
              <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
                {tc('actions.edit')}
              </Button>
              <Popconfirm
                title={t('deleteConfirm', { code: row.branchCode })}
                description={t('deleteDescription')}
                okText={tc('actions.delete')}
                cancelText={tc('actions.cancel')}
                okButtonProps={{ danger: true }}
                disabled={row.isHeadOffice}
                onConfirm={() => void handleDelete(row)}
              >
                <Tooltip
                  title={row.isHeadOffice ? t('cannotDeleteHeadOffice') : tc('actions.delete')}
                >
                  <span>
                    <Button
                      type="text"
                      size="small"
                      danger
                      disabled={row.isHeadOffice}
                      icon={<DeleteOutlined />}
                      aria-label={tc('actions.delete')}
                      style={row.isHeadOffice ? { opacity: 0.35 } : undefined}
                    />
                  </span>
                </Tooltip>
              </Popconfirm>
            </Space>
          ) : null,
      },
    ],
    [branchStatusLabel, canWrite, t, tc],
  );

  return (
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

      <Drawer
        title={editing ? t('edit') : t('create')}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        width={420}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              {tc('actions.save')}
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="branchCode"
            label={t('form.code')}
            rules={[{ required: true, message: t('form.codeRequired') }]}
          >
            <Input placeholder="HN01" style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Form.Item
            name="branchName"
            label={t('form.name')}
            rules={[{ required: true, message: t('form.nameRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="phone" label={t('form.phone')}>
            <Input />
          </Form.Item>
          <Form.Item name="address" label={t('form.address')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="isHeadOffice" label={t('form.isHeadOffice')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="status" label={t('form.status')}>
            <Select options={branchStatusOptions} />
          </Form.Item>
        </Form>
      </Drawer>
    </Card>
  );
}
