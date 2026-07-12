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
  IdcardOutlined,
  MedicineBoxOutlined,
  FlagOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  approveClinicMembership,
  confirmClinicMembership,
  fetchClinicMemberships,
  fetchClinicPendingMemberships,
  inviteClinicDoctor,
  rejectClinicMembership,
  revokeClinicMembership,
  type ConnectDoctorMembership,
} from '@/shared/api/connect.api';
import { clinicSpecialtySelectOptions } from '@/modules/clinic/clinic-specialties';

const STATUS_COLOR: Record<string, string> = {
  active: 'green',
  pending_doctor_accept: 'blue',
  pending_clinic_approval: 'gold',
  pending_our_approval: 'gold',
  rejected: 'default',
  revoked: 'red',
};

type InviteForm = {
  fullName: string;
  phone: string;
  licenseNumber?: string;
  specialty?: string;
  membershipRole: string;
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

export function ConnectTeamPage() {
  const { t } = useTranslation('connect');
  const [items, setItems] = useState<ConnectDoctorMembership[]>([]);
  const [pending, setPending] = useState<ConnectDoctorMembership[]>([]);
  const [loading, setLoading] = useState(false);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<InviteForm>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, queue] = await Promise.all([
        fetchClinicMemberships(),
        fetchClinicPendingMemberships(),
      ]);
      setItems(list);
      setPending(queue);
    } catch (error) {
      message.error(apiErrorMessage(error, t('team.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onInvite = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await inviteClinicDoctor(values);
      message.success(t('team.inviteSuccess'));
      setInviteOpen(false);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('team.inviteFailed')));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ConnectDoctorMembership> = [
    {
      title: colTitle(<IdcardOutlined />, t('team.colDoctor')),
      ellipsis: true,
      render: (_, row) => (
        <div style={{ minWidth: 0 }}>
          <Typography.Text ellipsis style={{ display: 'block', maxWidth: '100%' }}>
            {row.doctorFullName}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis>
            {row.doctorPhone}
            {row.doctorLicenseNumber ? ` · ${row.doctorLicenseNumber}` : ''}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: colTitle(<MedicineBoxOutlined />, t('team.colRole')),
      dataIndex: 'membershipRole',
      width: 120,
      render: (role: string) => t(`team.roles.${role}`, { defaultValue: role }),
    },
    {
      title: colTitle(<FlagOutlined />, t('team.colStatus')),
      dataIndex: 'membershipStatus',
      width: 180,
      align: 'center',
      render: (status: string) => (
        <Tag color={STATUS_COLOR[status] ?? 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`team.status.${status}`, { defaultValue: status })}
        </Tag>
      ),
    },
    {
      title: t('team.colActions'),
      width: 240,
      render: (_, row) => (
        <Space wrap>
          {row.membershipStatus === 'pending_doctor_accept' ? (
            <>
              <Button
                type="link"
                size="small"
                onClick={() =>
                  void confirmClinicMembership(row.id)
                    .then(() => {
                      message.success(t('team.confirmSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('team.actionFailed'))))
                }
              >
                {t('team.confirm')}
              </Button>
              <Button
                type="link"
                danger
                size="small"
                onClick={() =>
                  void rejectClinicMembership(row.id)
                    .then(() => {
                      message.success(t('team.rejectSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('team.actionFailed'))))
                }
              >
                {t('team.reject')}
              </Button>
            </>
          ) : null}
          {row.membershipStatus === 'pending_our_approval' ? (
            <>
              <Button
                type="link"
                size="small"
                onClick={() =>
                  void approveClinicMembership(row.id)
                    .then(() => {
                      message.success(t('team.approveSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('team.actionFailed'))))
                }
              >
                {t('team.approve')}
              </Button>
              <Button
                type="link"
                danger
                size="small"
                onClick={() =>
                  void rejectClinicMembership(row.id)
                    .then(() => {
                      message.success(t('team.rejectSuccess'));
                      return load();
                    })
                    .catch((error) => message.error(apiErrorMessage(error, t('team.actionFailed'))))
                }
              >
                {t('team.reject')}
              </Button>
            </>
          ) : null}
          {row.membershipStatus === 'active' ? (
            <Popconfirm
              title={t('team.revokeConfirm')}
              onConfirm={() =>
                void revokeClinicMembership(row.id)
                  .then(() => {
                    message.success(t('team.revokeSuccess'));
                    return load();
                  })
                  .catch((error) => message.error(apiErrorMessage(error, t('team.actionFailed'))))
              }
            >
              <Button type="link" danger size="small">
                {t('team.revoke')}
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
            {t('team.title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('team.subtitle')}</Typography.Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('team.refresh')}
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              form.setFieldsValue({ membershipRole: 'attending' });
              setInviteOpen(true);
            }}
          >
            {t('team.invite')}
          </Button>
        </Space>
      </div>

      {pending.length > 0 ? (
        <Card title={t('team.pendingTitle')} size="small">
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
          locale={{ emptyText: t('team.empty') }}
        />
      </Card>

      <Modal
        title={t('team.inviteTitle')}
        open={inviteOpen}
        onCancel={() => setInviteOpen(false)}
        onOk={() => void onInvite()}
        confirmLoading={saving}
        okText={t('team.invite')}
        destroyOnClose
      >
        <Form form={form} layout="vertical" initialValues={{ membershipRole: 'attending' }}>
          <Form.Item
            name="fullName"
            label={t('team.fullName')}
            rules={[{ required: true, message: t('team.fullNameRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="phone"
            label={t('team.phone')}
            rules={[{ required: true, message: t('team.phoneRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="licenseNumber" label={t('team.license')}>
            <Input />
          </Form.Item>
          <Form.Item name="specialty" label={t('team.specialty')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('team.specialtyPlaceholder')}
              options={clinicSpecialtySelectOptions()}
            />
          </Form.Item>
          <Form.Item name="membershipRole" label={t('team.role')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'attending', label: t('team.roles.attending') },
                { value: 'consultant', label: t('team.roles.consultant') },
                { value: 'owner', label: t('team.roles.owner') },
              ]}
            />
          </Form.Item>
          <Form.Item name="notes" label={t('team.notes')}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
