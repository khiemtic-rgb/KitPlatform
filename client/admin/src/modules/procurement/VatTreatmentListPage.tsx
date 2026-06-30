import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  PercentageOutlined,
  PlusOutlined,
  ReloadOutlined,
  StopOutlined,
} from '@ant-design/icons';
import { isAxiosError } from 'axios';
import {
  createVatTreatment,
  deleteVatTreatment,
  fetchVatTreatments,
  updateVatTreatment,
} from '@/shared/api/procurement.api';
import type { ProcurementVatTreatment } from '@/shared/api/procurement.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useProcurementWrite } from '@/shared/auth/usePermission';

interface VatFormValues {
  treatmentCode?: string;
  treatmentName: string;
  ratePercent: number;
  isNotSubject: boolean;
  sortOrder: number;
  isActive: boolean;
}

export function VatTreatmentListPage() {
  const { t } = useTranslation('procurement', { keyPrefix: 'vatTreatments' });
  const { t: tShared } = useTranslation('procurement', { keyPrefix: 'shared' });
  const { t: tCommon } = useTranslation('common', { keyPrefix: 'actions' });
  const canWrite = useProcurementWrite();
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<ProcurementVatTreatment[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ProcurementVatTreatment | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<VatFormValues>();

  const deleteBlockedReason = useCallback(
    (row: ProcurementVatTreatment): string | null => {
      if (row.canDelete) return null;
      if (['kct', 'vat_0', 'vat_5', 'vat_8', 'vat_10'].includes(row.treatmentCode)) {
        return t('deleteDefaultBlocked');
      }
      return t('deleteInUseBlocked');
    },
    [t],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchVatTreatments(false));
    } catch (error) {
      const msg = apiErrorMessage(error, t('messages.loadFailed'));
      setLoadError(msg);
      message.error(msg);
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
    form.setFieldsValue({
      ratePercent: 8,
      isNotSubject: false,
      sortOrder: items.length,
      isActive: true,
    });
    setModalOpen(true);
  };

  const openEdit = (row: ProcurementVatTreatment) => {
    setEditing(row);
    form.setFieldsValue({
      treatmentName: row.treatmentName,
      ratePercent: row.ratePercent,
      isNotSubject: row.isNotSubject,
      sortOrder: row.sortOrder,
      isActive: row.isActive,
    });
    setModalOpen(true);
  };

  const handleDelete = async (row: ProcurementVatTreatment) => {
    try {
      await deleteVatTreatment(row.id);
      message.success(t('messages.deleted', { code: row.treatmentCode }));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.deleteFailed')));
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      const code = values.treatmentCode?.trim().toLowerCase() ?? '';
      if (!editing) {
        if (items.some((row) => row.treatmentCode === code)) {
          message.error(t('validation.duplicateCode', { code }));
          return;
        }
      }
      setSaving(true);
      if (editing) {
        await updateVatTreatment(editing.id, {
          treatmentName: values.treatmentName,
          ratePercent: values.isNotSubject ? 0 : values.ratePercent,
          isNotSubject: values.isNotSubject,
          sortOrder: values.sortOrder,
          isActive: values.isActive,
        });
        message.success(t('messages.updated'));
      } else {
        await createVatTreatment({
          treatmentCode: code,
          treatmentName: values.treatmentName.trim(),
          ratePercent: values.isNotSubject ? 0 : values.ratePercent,
          isNotSubject: values.isNotSubject,
          sortOrder: values.sortOrder,
        });
        message.success(t('messages.created'));
      }
      setModalOpen(false);
      void load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.saveFailed')));
        return;
      }
      if (error && typeof error === 'object' && 'errorFields' in error) {
        message.warning(t('validation.checkRequired'));
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ProcurementVatTreatment> = useMemo(
    () => [
      { title: tShared('columns.code'), dataIndex: 'treatmentCode', width: 100 },
      { title: t('displayName'), dataIndex: 'treatmentName' },
      {
        title: t('ratePercent'),
        dataIndex: 'ratePercent',
        width: 100,
        align: 'right',
        render: (v: number, row) => (row.isNotSubject ? tShared('emDash') : `${v}%`),
      },
      {
        title: t('type'),
        width: 130,
        render: (_, row) =>
          row.isNotSubject ? <Tag>{t('kctTag')}</Tag> : <Tag color="blue">{t('rateTag')}</Tag>,
      },
      { title: t('sortOrder'), dataIndex: 'sortOrder', width: 80, align: 'center' },
      {
        title: tShared('columns.status'),
        dataIndex: 'isActive',
        width: 110,
        render: (v: boolean) =>
          v ? (
            <Tag icon={<CheckCircleOutlined />} color="green">
              {t('inUse')}
            </Tag>
          ) : (
            <Tag icon={<StopOutlined />}>{t('inactive')}</Tag>
          ),
      },
      {
        title: tShared('columns.actions'),
        width: 110,
        render: (_, row) => {
          if (!canWrite) return null;
          const blocked = deleteBlockedReason(row);
          return (
            <Space size={4}>
              <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
                {tCommon('edit')}
              </Button>
              <Popconfirm
                title={t('deleteConfirm', { code: row.treatmentCode })}
                description={t('deleteDescription')}
                okText={tCommon('delete')}
                cancelText={tCommon('cancel')}
                okButtonProps={{ danger: true }}
                disabled={!row.canDelete}
                onConfirm={() => void handleDelete(row)}
              >
                <Tooltip title={blocked ?? t('deleteTooltip')}>
                  <span>
                    <Button
                      type="text"
                      size="small"
                      danger
                      disabled={!row.canDelete}
                      icon={<DeleteOutlined />}
                      aria-label={tCommon('delete')}
                      style={!row.canDelete ? { opacity: 0.35 } : undefined}
                    />
                  </span>
                </Tooltip>
              </Popconfirm>
            </Space>
          );
        },
      },
    ],
    [canWrite, deleteBlockedReason, t, tCommon, tShared],
  );

  return (
    <Card
      title={
        <Space>
          <PercentageOutlined />
          {t('title')}
        </Space>
      }
      bordered={false}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {tCommon('reload')}
          </Button>
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('addRate')}
            </Button>
          ) : null}
        </Space>
      }
    >
      <p style={{ marginTop: 0, color: '#666' }}>{t('intro')}</p>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={false}
        locale={{
          emptyText: loadError ? loadError : t('emptyText'),
        }}
      />

      <Modal
        title={
          editing ? (
            <Space>
              <EditOutlined />
              {t('editRate')}
            </Space>
          ) : (
            <Space>
              <PlusOutlined />
              {t('addRate')}
            </Space>
          )
        }
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSave()}
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          {!editing && (
            <Form.Item
              name="treatmentCode"
              label={t('codeLabel')}
              extra={t('codeExtra')}
              rules={[
                { required: true, message: t('validation.enterCode') },
                {
                  pattern: /^[a-z0-9_]+$/,
                  message: t('validation.codePattern'),
                },
              ]}
            >
              <Input
                placeholder={t('codePlaceholder')}
                onBlur={(e) => form.setFieldValue('treatmentCode', e.target.value.trim().toLowerCase())}
              />
            </Form.Item>
          )}
          <Form.Item name="treatmentName" label={t('displayName')} rules={[{ required: true }]}>
            <Input placeholder={t('namePlaceholder')} />
          </Form.Item>
          <Form.Item name="isNotSubject" label={t('notSubjectSwitch')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item noStyle shouldUpdate={(prev, cur) => prev.isNotSubject !== cur.isNotSubject}>
            {() =>
              form.getFieldValue('isNotSubject') ? null : (
                <Form.Item
                  name="ratePercent"
                  label={t('ratePercentLabel')}
                  rules={[{ required: true, message: t('validation.enterRate') }]}
                >
                  <InputNumber min={0} max={100} style={{ width: '100%' }} />
                </Form.Item>
              )
            }
          </Form.Item>
          <Space style={{ width: '100%' }} size="large">
            <Form.Item name="sortOrder" label={t('sortOrder')}>
              <InputNumber min={0} />
            </Form.Item>
            {editing && (
              <Form.Item name="isActive" label={t('active')} valuePropName="checked">
                <Switch />
              </Form.Item>
            )}
          </Space>
        </Form>
      </Modal>
    </Card>
  );
}
