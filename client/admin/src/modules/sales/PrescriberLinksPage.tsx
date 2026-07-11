import { useCallback, useEffect, useMemo, useState } from 'react';
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
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import {
  approvePrescriberLink,
  fetchPendingPrescriberLinks,
  fetchPrescriberLinks,
  invitePrescriberLink,
  rejectPrescriberLink,
  revokePrescriberLink,
  type RxPrescriberLink,
} from '@/shared/api/rx.api';
import { useHasPermission } from '@/shared/auth/usePermission';
import { apiErrorMessage } from '@/shared/api/api-error';

const linkStatusColor: Record<string, string> = {
  active: 'green',
  pending_nt_invite: 'blue',
  pending_nt_approval: 'gold',
  rejected: 'default',
  revoked: 'red',
};

const LINK_STATUS_KEYS = [
  'active',
  'pending_nt_invite',
  'pending_nt_approval',
  'rejected',
  'revoked',
] as const;

type InviteFormValues = {
  phone: string;
  fullName: string;
  licenseNumber?: string;
  specialty?: string;
  notes?: string;
};

export function PrescriberLinksPage() {
  const { t } = useTranslation('rx');
  const canManage = useHasPermission('rx.prescriber.link.manage') || useHasPermission('rx.prescriber.manage');
  const [items, setItems] = useState<RxPrescriberLink[]>([]);
  const [pending, setPending] = useState<RxPrescriberLink[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [inviteOpen, setInviteOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<InviteFormValues>();

  const linkStatusLabel = useCallback(
    (key: string) => t(`enums.linkStatus.${key}`, { defaultValue: key }),
    [t],
  );
  const initiatedByLabel = useCallback(
    (key: string) => t(`enums.initiatedBy.${key}`, { defaultValue: key }),
    [t],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [links, queue] = await Promise.all([
        fetchPrescriberLinks(statusFilter),
        fetchPendingPrescriberLinks(),
      ]);
      setItems(links);
      setPending(queue);
    } catch (error) {
      message.error(apiErrorMessage(error, t('links.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onInvite = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await invitePrescriberLink(values);
      message.success(t('links.inviteSuccess'));
      setInviteOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('links.inviteFailed')));
    } finally {
      setSaving(false);
    }
  };

  const approve = useCallback(
    async (id: string) => {
      try {
        await approvePrescriberLink(id);
        message.success(t('links.approveSuccess'));
        await load();
      } catch (error) {
        message.error(apiErrorMessage(error, t('links.approveFailed')));
      }
    },
    [load, t],
  );

  const reject = useCallback(
    async (id: string) => {
      try {
        await rejectPrescriberLink(id);
        message.success(t('links.rejectSuccess'));
        await load();
      } catch (error) {
        message.error(apiErrorMessage(error, t('links.rejectFailed')));
      }
    },
    [load, t],
  );

  const revoke = useCallback(
    async (id: string) => {
      try {
        await revokePrescriberLink(id);
        message.success(t('links.revokeSuccess'));
        await load();
      } catch (error) {
        message.error(apiErrorMessage(error, t('links.revokeFailed')));
      }
    },
    [load, t],
  );

  const columns: ColumnsType<RxPrescriberLink> = useMemo(
    () => [
      {
        title: t('links.columns.doctor'),
        render: (_, row) => (
          <div>
            <div>{row.prescriberName ?? '—'}</div>
            <div style={{ color: '#64748b', fontSize: 12 }}>{row.prescriberPhone ?? '—'}</div>
          </div>
        ),
      },
      {
        title: t('links.columns.license'),
        dataIndex: 'prescriberLicenseNumber',
        render: (v) => v || '—',
      },
      {
        title: t('links.columns.status'),
        dataIndex: 'linkStatus',
        render: (value: string) => (
          <Tag color={linkStatusColor[value] ?? 'default'}>{linkStatusLabel(value)}</Tag>
        ),
      },
      {
        title: t('links.columns.initiatedBy'),
        dataIndex: 'initiatedBy',
        render: (value: string) => initiatedByLabel(value),
      },
      {
        title: t('links.columns.actions'),
        render: (_, row) => (
          <Space>
            {canManage && row.linkStatus === 'pending_nt_approval' ? (
              <>
                <Button type="link" onClick={() => void approve(row.id)}>
                  {t('links.actions.approve')}
                </Button>
                <Button type="link" danger onClick={() => void reject(row.id)}>
                  {t('links.actions.reject')}
                </Button>
              </>
            ) : null}
            {canManage && row.linkStatus === 'active' ? (
              <Popconfirm
                title={t('links.revokeConfirm')}
                onConfirm={() => void revoke(row.id)}
              >
                <Button type="link" danger>
                  {t('links.actions.revoke')}
                </Button>
              </Popconfirm>
            ) : null}
          </Space>
        ),
      },
    ],
    [approve, canManage, initiatedByLabel, linkStatusLabel, reject, revoke, t],
  );

  return (
    <div style={{ padding: 16 }}>
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Card
          title={t('links.pendingTitle')}
          extra={
            <Button icon={<ReloadOutlined />} onClick={() => void load()}>
              {t('links.reload')}
            </Button>
          }
        >
          <Table
            rowKey="id"
            loading={loading}
            dataSource={pending}
            pagination={false}
            columns={columns}
            locale={{ emptyText: t('links.emptyPending') }}
          />
        </Card>

        <Card
          title={t('links.networkTitle')}
          extra={
            canManage ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setInviteOpen(true)}>
                {t('links.invite')}
              </Button>
            ) : null
          }
        >
          <Space style={{ marginBottom: 16 }}>
            <Select
              allowClear
              placeholder={t('links.filterStatus')}
              style={{ width: 240 }}
              value={statusFilter}
              onChange={setStatusFilter}
              options={LINK_STATUS_KEYS.map((key) => ({
                value: key,
                label: linkStatusLabel(key),
              }))}
            />
          </Space>
          <Table rowKey="id" loading={loading} dataSource={items} columns={columns} />
        </Card>
      </Space>

      <Modal
        title={t('links.inviteTitle')}
        open={inviteOpen}
        onCancel={() => setInviteOpen(false)}
        onOk={() => void onInvite()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="fullName" label={t('links.form.fullName')} rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label={t('links.form.phone')} rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item
            name="licenseNumber"
            label={t('links.form.license')}
            rules={[{ required: true, message: t('links.form.licenseRequired') }]}
            extra={t('links.form.licenseExtra')}
          >
            <Input placeholder={t('links.form.licensePlaceholder')} />
          </Form.Item>
          <Form.Item name="specialty" label={t('links.form.specialty')}>
            <Input />
          </Form.Item>
          <Form.Item name="notes" label={t('links.form.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
