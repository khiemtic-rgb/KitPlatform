import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Space,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CheckOutlined, CloseOutlined, ReloadOutlined, SettingOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  approveCustomerAppLoginRequest,
  fetchCustomerAppAuthSettings,
  fetchCustomerAppLoginRequests,
  rejectCustomerAppLoginRequest,
  updateCustomerAppAuthSettings,
  type CustomerAppLoginRequest,
} from '@/shared/api/customer-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useCanSalesCustomers, useCanSalesSettings } from '@/shared/auth/usePermission';

export function CustomerAppLoginRequestsCard() {
  const { t } = useTranslation('customer', { keyPrefix: 'appLogin' });
  const canWrite = useCanSalesCustomers();
  const canSettings = useCanSalesSettings();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<CustomerAppLoginRequest[]>([]);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsLoading, setSettingsLoading] = useState(false);
  const [hasCounterPin, setHasCounterPin] = useState(false);
  const [hasInviteCode, setHasInviteCode] = useState(false);
  const [inviteHint, setInviteHint] = useState<string | null>(null);
  const [approvedCode, setApprovedCode] = useState<string | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchCustomerAppLoginRequests('pending'));
    } catch (error) {
      setItems([]);
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  const loadSettings = useCallback(async () => {
    if (!canSettings) return;
    setSettingsLoading(true);
    try {
      const s = await fetchCustomerAppAuthSettings();
      setHasCounterPin(s.hasCounterPin);
      setHasInviteCode(s.hasInviteCode);
      setInviteHint(s.inviteCodeHint ?? null);
    } catch (error) {
      message.error(apiErrorMessage(error, t('settingsLoadFailed')));
    } finally {
      setSettingsLoading(false);
    }
  }, [canSettings, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (settingsOpen) void loadSettings();
  }, [settingsOpen, loadSettings]);

  const onApprove = async (row: CustomerAppLoginRequest) => {
    try {
      const res = await approveCustomerAppLoginRequest(row.id);
      if (res.pilotCode) setApprovedCode(res.pilotCode);
      message.success(res.message || t('approveSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('approveFailed')));
    }
  };

  const onReject = async (row: CustomerAppLoginRequest) => {
    try {
      await rejectCustomerAppLoginRequest(row.id);
      message.success(t('rejectSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('rejectFailed')));
    }
  };

  const onSaveSettings = async () => {
    const values = await form.validateFields();
    try {
      const next = await updateCustomerAppAuthSettings({
        counterPin: values.counterPin || undefined,
        inviteCode: values.inviteCode || undefined,
        clearCounterPin: Boolean(values.clearCounterPin),
        clearInviteCode: Boolean(values.clearInviteCode),
      });
      setHasCounterPin(next.hasCounterPin);
      setHasInviteCode(next.hasInviteCode);
      setInviteHint(next.inviteCodeHint ?? null);
      form.resetFields();
      message.success(t('settingsSaved'));
      setSettingsOpen(false);
    } catch (error) {
      message.error(apiErrorMessage(error, t('settingsSaveFailed')));
    }
  };

  const columns: ColumnsType<CustomerAppLoginRequest> = [
    {
      title: t('columns.phone'),
      dataIndex: 'phone',
      width: 130,
    },
    {
      title: t('columns.customer'),
      dataIndex: 'customerName',
      render: (v: string | null | undefined, row) => v || row.phone,
    },
    {
      title: t('columns.invite'),
      dataIndex: 'referralCodeUsed',
      width: 110,
      render: (v: string | null | undefined) => v || '—',
    },
    {
      title: t('columns.requestedAt'),
      dataIndex: 'requestedAt',
      width: 160,
      render: (v: string) => (v ? dayjs(v).format('DD/MM HH:mm') : '—'),
    },
    {
      title: t('columns.actions'),
      key: 'actions',
      width: 200,
      render: (_, row) =>
        canWrite ? (
          <Space>
            <Button type="primary" size="small" icon={<CheckOutlined />} onClick={() => void onApprove(row)}>
              {t('approve')}
            </Button>
            <Button size="small" danger icon={<CloseOutlined />} onClick={() => void onReject(row)}>
              {t('reject')}
            </Button>
          </Space>
        ) : (
          '—'
        ),
    },
  ];

  return (
    <>
      <Card
        size="small"
        title={t('title')}
        extra={
          <Space>
            {canSettings ? (
              <Button size="small" icon={<SettingOutlined />} onClick={() => setSettingsOpen(true)}>
                {t('settings')}
              </Button>
            ) : null}
            <Button size="small" icon={<ReloadOutlined />} onClick={() => void load()}>
              {t('reload')}
            </Button>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t('hint')}
        </Typography.Paragraph>
        {approvedCode ? (
          <Alert
            type="success"
            showIcon
            closable
            onClose={() => setApprovedCode(null)}
            style={{ marginBottom: 12 }}
            message={t('approvedCodeTitle')}
            description={
              <Typography.Title level={2} style={{ margin: 0, letterSpacing: 8, fontFamily: 'monospace' }}>
                {approvedCode}
              </Typography.Title>
            }
          />
        ) : null}
        <Table
          rowKey="id"
          size="small"
          loading={loading}
          pagination={false}
          columns={columns}
          dataSource={items}
          locale={{ emptyText: t('empty') }}
        />
      </Card>

      <Modal
        title={t('settingsTitle')}
        open={settingsOpen}
        onCancel={() => setSettingsOpen(false)}
        onOk={() => void onSaveSettings()}
        confirmLoading={settingsLoading}
        destroyOnClose
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('settingsStatus', {
            pin: hasCounterPin ? t('configured') : t('missing'),
            invite: hasInviteCode ? inviteHint || t('configured') : t('missing'),
          })}
        />
        <Form form={form} layout="vertical">
          <Form.Item name="counterPin" label={t('counterPin')} extra={t('counterPinExtra')}>
            <Input.Password placeholder="1357" maxLength={12} />
          </Form.Item>
          <Form.Item name="inviteCode" label={t('inviteCode')} extra={t('inviteCodeExtra')}>
            <Input placeholder="XUANHOA" maxLength={24} style={{ textTransform: 'uppercase' }} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
