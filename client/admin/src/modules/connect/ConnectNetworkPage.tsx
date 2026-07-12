import { useCallback, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import {
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  ReloadOutlined,
  ShopOutlined,
  BankOutlined,
  FlagOutlined,
  TeamOutlined,
  FilterOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  acceptConnectOrgLink,
  fetchConnectOrgLinks,
  fetchConnectOrgProfile,
  fetchConnectPendingOrgLinks,
  inviteConnectOrgLink,
  rejectConnectOrgLink,
  revokeConnectOrgLink,
  searchConnectDirectory,
  type ConnectDirectoryEntry,
  type ConnectOrgLink,
  type ConnectOrgProfile,
} from '@/shared/api/connect.api';

const STATUS_COLOR: Record<string, string> = {
  active: 'green',
  pending_partner_accept: 'blue',
  pending_our_approval: 'gold',
  rejected: 'default',
  revoked: 'red',
};

type InviteForm = {
  partnerTenantCode: string;
  ourOrgRole: string;
  partnerOrgRole: string;
  notes?: string;
};

function colTitle(icon: ReactNode, label: string) {
  return (
    <Space size={6}>
      <span style={{ color: 'rgba(0,0,0,0.45)', display: 'inline-flex' }}>{icon}</span>
      <span>{label}</span>
    </Space>
  );
}

export function ConnectNetworkPage() {
  const { t } = useTranslation('connect');
  const [items, setItems] = useState<ConnectOrgLink[]>([]);
  const [pending, setPending] = useState<ConnectOrgLink[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [inviteOpen, setInviteOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [directory, setDirectory] = useState<ConnectDirectoryEntry[]>([]);
  const [profile, setProfile] = useState<ConnectOrgProfile | null>(null);
  const [form] = Form.useForm<InviteForm>();
  const partnerCode = Form.useWatch('partnerTenantCode', form);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [links, queue, me] = await Promise.all([
        fetchConnectOrgLinks(statusFilter),
        fetchConnectPendingOrgLinks(),
        fetchConnectOrgProfile(),
      ]);
      setItems(links);
      setPending(queue);
      setProfile(me);
    } catch (error) {
      message.error(apiErrorMessage(error, t('network.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!partnerCode) return;
    const partner = directory.find((d) => d.tenantCode === partnerCode);
    if (partner?.orgKind) {
      form.setFieldValue('partnerOrgRole', partner.orgKind);
    }
  }, [partnerCode, directory, form]);

  const openInvite = async () => {
    const ourKind = profile?.orgKind ?? 'pharmacy';
    form.setFieldsValue({
      ourOrgRole: ourKind,
      partnerOrgRole: ourKind === 'pharmacy' ? 'clinic' : 'pharmacy',
      partnerTenantCode: undefined,
      notes: undefined,
    });
    setInviteOpen(true);
    try {
      setDirectory(await searchConnectDirectory());
    } catch {
      setDirectory([]);
    }
  };

  const onInvite = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await inviteConnectOrgLink(values);
      message.success(t('network.inviteSuccess'));
      setInviteOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('network.inviteFailed')));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ConnectOrgLink> = [
    {
      title: colTitle(
        profile?.orgKind === 'pharmacy' ? <BankOutlined /> : <ShopOutlined />,
        t('network.colPartner'),
      ),
      ellipsis: true,
      render: (_, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.partnerTenantName}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
            {row.partnerTenantCode}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<TeamOutlined />, t('network.colRoles')),
      width: 160,
      render: (_, row) => (
        <span>
          {t(`network.roles.${row.ourOrgRole}`, { defaultValue: row.ourOrgRole })} →{' '}
          {t(`network.roles.${row.partnerOrgRole}`, { defaultValue: row.partnerOrgRole })}
        </span>
      ),
    },
    {
      title: colTitle(<FlagOutlined />, t('network.colStatus')),
      dataIndex: 'linkStatus',
      width: 160,
      align: 'center',
      render: (status: string) => (
        <Tag color={STATUS_COLOR[status] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`network.status.${status}`, { defaultValue: status })}
        </Tag>
      ),
    },
    {
      title: t('network.colActions'),
      width: 220,
      render: (_, row) => (
        <Space wrap>
          {row.linkStatus === 'pending_our_approval' ? (
            <>
              <Button
                type="link"
                size="small"
                onClick={() =>
                  void acceptConnectOrgLink(row.id)
                    .then(() => {
                      message.success(t('network.acceptSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('network.actionFailed'))))
                }
              >
                {t('network.accept')}
              </Button>
              <Button
                type="link"
                danger
                size="small"
                onClick={() =>
                  void rejectConnectOrgLink(row.id)
                    .then(() => {
                      message.success(t('network.rejectSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('network.actionFailed'))))
                }
              >
                {t('network.reject')}
              </Button>
            </>
          ) : null}
          {row.linkStatus === 'active' ? (
            <Popconfirm
              title={t('network.revokeConfirm')}
              onConfirm={() =>
                void revokeConnectOrgLink(row.id)
                  .then(() => {
                    message.success(t('network.revokeSuccess'));
                    return load();
                  })
                  .catch((error) => message.error(apiErrorMessage(error, t('network.actionFailed'))))
              }
            >
              <Button type="link" danger size="small">
                {t('network.revoke')}
              </Button>
            </Popconfirm>
          ) : null}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('network.title')}
          </Typography.Title>
          <Typography.Text type="secondary">
            {t('network.subtitle')}
            {profile?.orgKind
              ? ` · ${t('network.ourProfile')}: ${t(`network.roles.${profile.orgKind}`, { defaultValue: profile.orgKind })}`
              : ''}
          </Typography.Text>
        </div>
        <Space>
          <Select
            allowClear
            placeholder={t('network.filterStatus')}
            suffixIcon={<FilterOutlined />}
            style={{ minWidth: 180 }}
            value={statusFilter}
            onChange={(value) => setStatusFilter(value)}
            options={[
              'active',
              'pending_partner_accept',
              'pending_our_approval',
              'rejected',
              'revoked',
            ].map((key) => ({
              value: key,
              label: t(`network.status.${key}`),
            }))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('network.refresh')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => void openInvite()}>
            {t('network.invite')}
          </Button>
        </Space>
      </div>

      {pending.length > 0 ? (
        <Card title={t('network.pendingTitle')} size="small">
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            dataSource={pending}
            columns={columns}
            tableLayout="fixed"
          />
        </Card>
      ) : null}

      <Card>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={items}
          columns={columns}
          tableLayout="fixed"
          pagination={{ pageSize: 20 }}
          locale={{ emptyText: t('network.empty') }}
        />
      </Card>

      <Modal
        title={t('network.inviteTitle')}
        open={inviteOpen}
        onCancel={() => setInviteOpen(false)}
        onOk={() => void onInvite()}
        confirmLoading={saving}
        okText={t('network.invite')}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="partnerTenantCode"
            label={t('network.partnerCode')}
            rules={[{ required: true, message: t('network.partnerCodeRequired') }]}
          >
            <Select
              showSearch
              placeholder={t('network.partnerCodePlaceholder')}
              options={directory.map((item) => ({
                value: item.tenantCode,
                label: `${item.tenantName} (${item.tenantCode})${
                  item.orgKind
                    ? ` · ${t(`network.roles.${item.orgKind}`, { defaultValue: item.orgKind })}`
                    : ''
                }`,
              }))}
              filterOption={(input, option) =>
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
            />
          </Form.Item>
          <Form.Item name="ourOrgRole" label={t('network.ourRole')} rules={[{ required: true }]}>
            <Select
              disabled
              options={[
                { value: 'pharmacy', label: t('network.roles.pharmacy') },
                { value: 'clinic', label: t('network.roles.clinic') },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="partnerOrgRole"
            label={t('network.partnerRole')}
            rules={[{ required: true }]}
          >
            <Select
              disabled
              options={[
                { value: 'pharmacy', label: t('network.roles.pharmacy') },
                { value: 'clinic', label: t('network.roles.clinic') },
              ]}
            />
          </Form.Item>
          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {t('network.rolesLockedHint')}
          </Typography.Paragraph>
          <Form.Item name="notes" label={t('network.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
