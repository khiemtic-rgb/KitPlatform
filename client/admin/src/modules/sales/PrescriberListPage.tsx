import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Card, Form, Input, Modal, Popconfirm, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import {
  createPrescriber,
  deletePrescriber,
  fetchPrescribers,
  type RxPrescriber,
  updatePrescriber,
} from '@/shared/api/rx.api';
import { useHasPermission } from '@/shared/auth/usePermission';
import { apiErrorMessage } from '@/shared/api/api-error';

type PrescriberFormValues = {
  fullName: string;
  licenseNumber: string;
  phone?: string;
  specialty?: string;
};

export function PrescriberListPage() {
  const { t } = useTranslation('rx');
  const canWrite =
    useHasPermission('rx.prescriber.manage') ||
    useHasPermission('rx.prescription.create') ||
    useHasPermission('sales.write');
  const [items, setItems] = useState<RxPrescriber[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<RxPrescriber | null>(null);
  const [form] = Form.useForm<PrescriberFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchPrescribers(undefined, false));
    } catch (error) {
      message.error(apiErrorMessage(error, t('prescribers.loadFailed')));
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
    setModalOpen(true);
  };

  const openEdit = (row: RxPrescriber) => {
    setEditing(row);
    form.setFieldsValue({
      fullName: row.fullName,
      licenseNumber: row.licenseNumber,
      phone: row.phone,
      specialty: row.specialty,
    });
    setModalOpen(true);
  };

  const submit = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      if (editing) {
        await updatePrescriber(editing.id, {
          ...values,
          status: editing.status ?? 1,
        });
        message.success(t('prescribers.updateSuccess'));
      } else {
        await createPrescriber(values);
        message.success(t('prescribers.createSuccess'));
      }
      setModalOpen(false);
      await load();
    } catch (error) {
      message.error(
        apiErrorMessage(
          error,
          editing ? t('prescribers.saveFailedUpdate') : t('prescribers.saveFailedCreate'),
        ),
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deletePrescriber(id);
      message.success(t('prescribers.deleteSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('prescribers.deleteFailed')));
    }
  };

  const columns: ColumnsType<RxPrescriber> = useMemo(
    () => [
      {
        title: t('prescribers.columns.fullName'),
        dataIndex: 'fullName',
      },
      {
        title: t('prescribers.columns.license'),
        dataIndex: 'licenseNumber',
        width: 180,
        render: (value?: string) => value || '—',
      },
      {
        title: t('prescribers.columns.phone'),
        dataIndex: 'phone',
        width: 130,
        render: (value?: string) => value || '—',
      },
      {
        title: t('prescribers.columns.specialty'),
        dataIndex: 'specialty',
        width: 180,
        render: (value?: string) => value || '—',
      },
      {
        title: t('prescribers.columns.status'),
        dataIndex: 'status',
        width: 110,
        render: (value: number) => (
          <Tag color={value === 1 ? 'green' : 'default'}>
            {t(`enums.prescriberStatus.${value === 1 ? '1' : '0'}`)}
          </Tag>
        ),
      },
      {
        title: t('prescribers.columns.actions'),
        key: 'actions',
        width: 140,
        render: (_, row) =>
          canWrite ? (
            <Space size={4}>
              <Button size="small" type="link" onClick={() => openEdit(row)}>
                {t('prescribers.actions.edit')}
              </Button>
              <Popconfirm
                title={t('prescribers.deleteConfirm')}
                onConfirm={() => void handleDelete(row.id)}
              >
                <Button size="small" type="link" danger>
                  {t('prescribers.actions.delete')}
                </Button>
              </Popconfirm>
            </Space>
          ) : null,
      },
    ],
    [canWrite, t],
  );

  return (
    <Card
      title={t('prescribers.title')}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('prescribers.reload')}
          </Button>
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('prescribers.add')}
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
        pagination={{ pageSize: 20, showSizeChanger: false }}
      />

      <Modal
        title={editing ? t('prescribers.edit') : t('prescribers.create')}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void submit()}
        confirmLoading={saving}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            label={t('prescribers.form.fullName')}
            name="fullName"
            rules={[{ required: true, message: t('prescribers.form.fullNameRequired') }]}
          >
            <Input placeholder={t('prescribers.form.fullNamePlaceholder')} />
          </Form.Item>
          <Form.Item
            label={t('prescribers.form.license')}
            name="licenseNumber"
            rules={[{ required: true, message: t('prescribers.form.licenseRequired') }]}
            extra={t('prescribers.form.licenseExtra')}
          >
            <Input placeholder={t('prescribers.form.licensePlaceholder')} />
          </Form.Item>
          <Form.Item label={t('prescribers.form.phone')} name="phone">
            <Input placeholder={t('prescribers.form.phonePlaceholder')} />
          </Form.Item>
          <Form.Item label={t('prescribers.form.specialty')} name="specialty">
            <Input placeholder={t('prescribers.form.specialtyPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
