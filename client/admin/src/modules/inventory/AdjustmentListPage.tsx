import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Checkbox,
  Collapse,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { PlusOutlined, ReloadOutlined, EyeOutlined, CheckOutlined, TeamOutlined } from '@ant-design/icons';
import {
  approveAdjustment,
  createAdjustment,
  createCountingSession,
  fetchAdjustment,
  fetchAdjustments,
  fetchStockBatches,
  fetchWarehouses,
} from '@/shared/api/inventory.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type {
  AdjustmentDetail,
  AdjustmentListItem,
  StockBatch,
  Warehouse,
} from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { InventoryCountWorkflowSteps } from '@/modules/inventory/InventoryCountWorkflowSteps';
import {
  buildCountReason,
  getCountReasonPresets,
  getInventoryCountWorkflowSteps,
} from '@/modules/inventory/inventory-count-workflow';
import { useInventoryEnums } from '@/shared/i18n/use-inventory-enums';

interface AdjustmentLineForm {
  batchId: string;
  actualQuantity: number;
  note?: string;
}

export function AdjustmentListPage() {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation('inventory', { keyPrefix: 'adjustmentList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { adjustmentStatusLabel } = useInventoryEnums();
  const inventoryCountWorkflowSteps = useMemo(
    () => getInventoryCountWorkflowSteps(),
    [i18n.language],
  );
  const countReasonPresets = useMemo(() => getCountReasonPresets(), [i18n.language]);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<AdjustmentListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseBatches, setWarehouseBatches] = useState<StockBatch[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<AdjustmentDetail | null>(null);
  const [form] = Form.useForm();
  const [sessionForm] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [prepareAcknowledged, setPrepareAcknowledged] = useState(false);
  const warehouseId = Form.useWatch('warehouseId', form);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [adjustments, wh] = await Promise.all([fetchAdjustments(), fetchWarehouses()]);
      setItems(adjustments);
      setWarehouses(wh);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (!warehouseId) {
      setWarehouseBatches([]);
      return;
    }
    fetchStockBatches({ warehouseId, page: 1, pageSize: 200 })
      .then((r) => setWarehouseBatches(r.items))
      .catch(() => setWarehouseBatches([]));
  }, [warehouseId]);

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({ items: [{ actualQuantity: 0 }] });
    setDrawerOpen(true);
  };

  const openCreateSession = () => {
    sessionForm.resetFields();
    sessionForm.setFieldsValue({ countType: 'periodic' });
    setPrepareAcknowledged(false);
    setSessionDrawerOpen(true);
  };

  const handleCreateSession = async () => {
    if (!prepareAcknowledged) {
      message.warning(t('messages.prepareRequired'));
      return;
    }
    try {
      const values = await sessionForm.validateFields();
      setSaving(true);
      const created = await createCountingSession({
        warehouseId: values.warehouseId,
        reason: buildCountReason(values.countType, values.reasonNote),
      });
      message.success(t('messages.sessionCreateSuccess', { number: created.adjustmentNumber }));
      setSessionDrawerOpen(false);
      navigate(`/inventory/adjustments/${created.id}/count`);
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.sessionCreateFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const openDetail = async (id: string) => {
    try {
      setDetail(await fetchAdjustment(id));
      setDetailOpen(true);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.detailLoadFailed')));
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);
      const created = await createAdjustment({
        warehouseId: values.warehouseId,
        reason: values.reason,
        items: (values.items as AdjustmentLineForm[]).map((i) => ({
          batchId: i.batchId,
          actualQuantity: i.actualQuantity,
          note: i.note,
        })),
      });
      message.success(t('messages.createSuccess', { number: created.adjustmentNumber }));
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, t('messages.createFailed')));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id: string) => {
    try {
      await approveAdjustment(id);
      message.success(t('messages.approveSuccess'));
      if (detail?.id === id) {
        setDetail(await fetchAdjustment(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.approveFailed')));
    }
  };

  const columns: ColumnsType<AdjustmentListItem> = [
    { title: ts('documentNumber'), dataIndex: 'adjustmentNumber', width: 130 },
    { title: ts('warehouse'), dataIndex: 'warehouseName' },
    {
      title: tc('fields.status'),
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 3 ? 'green' : v === 2 ? 'processing' : v === 1 ? 'gold' : 'default'}>
          {adjustmentStatusLabel(v)}
        </Tag>
      ),
    },
    {
      title: ts('date'),
      dataIndex: 'adjustmentDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    { title: ts('lineCount'), dataIndex: 'itemCount', width: 70, align: 'right' },
    {
      title: tc('fields.actions'),
      key: 'actions',
      width: 220,
      render: (_, row) => (
        <Space size={4} onClick={(e) => e.stopPropagation()}>
          {row.status === 2 && (
            <Tag
              color="processing"
              icon={<TeamOutlined />}
              style={{ cursor: 'pointer', margin: 0 }}
              onClick={() => navigate(`/inventory/adjustments/${row.id}/count`)}
            >
              {ts('count')}
            </Tag>
          )}
          <Tag
            color="blue"
            icon={<EyeOutlined />}
            style={{ cursor: 'pointer', margin: 0 }}
            onClick={() => openDetail(row.id)}
          >
            {ts('detail')}
          </Tag>
          {row.status !== 3 && row.status !== 4 && row.status !== 2 && (
            <Tag
              color="green"
              icon={<CheckOutlined />}
              style={{ cursor: 'pointer', margin: 0 }}
              onClick={() => handleApprove(row.id)}
            >
              {tc('actions.approve')}
            </Tag>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Collapse
        size="small"
        style={{ marginBottom: 16 }}
        items={[
          {
            key: 'workflow',
            label: t('workflowTitle'),
            children: (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <InventoryCountWorkflowSteps status={2} entryCount={0} canApprove={false} />
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                  <Trans i18nKey="workflowIntro" ns="inventory" t={t} />
                </Typography.Paragraph>
                <ul style={{ margin: 0, paddingLeft: 18, fontSize: 13, color: '#555' }}>
                  {inventoryCountWorkflowSteps.map((step) => (
                    <li key={step.title}>
                      <strong>{step.title}:</strong> {step.description}
                    </li>
                  ))}
                </ul>
              </Space>
            ),
          },
        ]}
      />

      <Card
        title={t('title')}
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              {tc('actions.reload')}
            </Button>
            <Button icon={<TeamOutlined />} onClick={openCreateSession}>
              {t('countSession')}
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('batchDocument')}
            </Button>
          </Space>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer
        title={t('createBatchTitle')}
        width={600}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" loading={saving} onClick={handleCreate}>
              {tc('actions.save')}
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="warehouseId" label={t('countWarehouse')} rules={[{ required: true }]}>
            <Select
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              placeholder={t('selectWarehouse')}
            />
          </Form.Item>
          <Form.Item name="reason" label={ts('reason')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <Space key={field.key} align="start" style={{ display: 'flex', marginBottom: 8 }}>
                    <Form.Item
                      {...field}
                      name={[field.name, 'batchId']}
                      rules={[{ required: true, message: t('validation.selectBatch') }]}
                      style={{ width: 300, marginBottom: 0 }}
                    >
                      <Select
                        placeholder={ts('batchAbbr')}
                        options={warehouseBatches.map((b) => ({
                          value: b.id,
                          label: t('batchOptionLabel', {
                            code: b.productCode,
                            batch: b.batchNumber,
                            qty: b.quantityAvailable,
                          }),
                        }))}
                      />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'actualQuantity']}
                      rules={[{ required: true, message: t('validation.actualQuantity') }]}
                      style={{ width: 110, marginBottom: 0 }}
                    >
                      <InputNumber min={0} style={{ width: '100%' }} />
                    </Form.Item>
                    <Button type="text" danger onClick={() => remove(field.name)}>
                      {tc('actions.delete')}
                    </Button>
                  </Space>
                ))}
                <Button type="dashed" onClick={() => add({ actualQuantity: 0 })} block>
                  {ts('addLine')}
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Drawer>

      <Drawer
        title={t('sessionStep1Title')}
        width={520}
        open={sessionDrawerOpen}
        onClose={() => setSessionDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setSessionDrawerOpen(false)}>{tc('actions.cancel')}</Button>
            <Button type="primary" loading={saving} onClick={handleCreateSession}>
              {t('startCounting')}
            </Button>
          </Space>
        }
      >
        <Form form={sessionForm} layout="vertical">
          <Form.Item name="warehouseId" label={t('countWarehouse')} rules={[{ required: true }]}>
            <Select
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              placeholder={t('selectWarehouse')}
            />
          </Form.Item>
          <Form.Item name="countType" label={t('countType')} rules={[{ required: true }]}>
            <Select options={countReasonPresets.map((p) => ({ value: p.value, label: p.label }))} />
          </Form.Item>
          <Form.Item name="reasonNote" label={t('reasonNote')}>
            <Input.TextArea rows={2} placeholder={t('reasonNotePlaceholder')} />
          </Form.Item>
          <Checkbox checked={prepareAcknowledged} onChange={(e) => setPrepareAcknowledged(e.target.checked)}>
            {t('prepareAcknowledge')}
          </Checkbox>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? t('detailTitleWithNumber', { number: detail.adjustmentNumber }) : t('detailTitle')}
        width={640}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && detail.status !== 3 && detail.status !== 4 ? (
            <Space>
              {detail.status === 2 && (
                <Button type="primary" onClick={() => navigate(`/inventory/adjustments/${detail.id}/count`)}>
                  {t('countScreen')}
                </Button>
              )}
              {detail.status !== 2 && (
                <Button type="primary" onClick={() => handleApprove(detail.id)}>
                  {tc('actions.approve')}
                </Button>
              )}
            </Space>
          ) : null
        }
      >
        {detail && (
          <>
            <p>
              <strong>{ts('warehouse')}:</strong> {detail.warehouseName}
            </p>
            <p>
              <strong>{tc('fields.status')}:</strong> {adjustmentStatusLabel(detail.status)}
            </p>
            {detail.reason && (
              <p>
                <strong>{ts('reason')}:</strong> {detail.reason}
              </p>
            )}
            {detail.status === 2 ? (
              <p style={{ color: '#1677ff' }}>
                <Trans i18nKey="sessionInProgressHint" ns="inventory" t={t} />
              </p>
            ) : (
              <Table
                rowKey="id"
                size="small"
                pagination={false}
                dataSource={detail.items}
                columns={[
                  { title: ts('productAbbr'), dataIndex: 'productName' },
                  { title: ts('batchAbbr'), dataIndex: 'batchNumber', width: 90 },
                  { title: ts('systemQtyAbbr'), dataIndex: 'systemQuantity', width: 70, align: 'right' },
                  { title: ts('actualQtyAbbr'), dataIndex: 'actualQuantity', width: 70, align: 'right' },
                  { title: ts('varianceAbbr'), dataIndex: 'differenceQuantity', width: 70, align: 'right' },
                ]}
              />
            )}
          </>
        )}
      </Drawer>
    </>
  );
}
