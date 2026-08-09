import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  App,
  Button,
  Card,
  Checkbox,
  Collapse,
  DatePicker,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import type { Dayjs } from 'dayjs';
import { PlusOutlined, ReloadOutlined, EyeOutlined, CheckOutlined, TeamOutlined, AppstoreOutlined, StopOutlined } from '@ant-design/icons';
import {
  approveAdjustment,
  cancelAdjustment,
  createAdjustment,
  createCountingSession,
  fetchActiveCountingSession,
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
import { formatDisplayQuantity } from '@/shared/utils/money';
import { InventoryCountBatchPickModal } from '@/modules/inventory/InventoryCountBatchPickModal';
import { InventoryCountWorkflowSteps } from '@/modules/inventory/InventoryCountWorkflowSteps';
import {
  expiryToneColor,
  getExpiryTone,
  sortBatchesForCount,
} from '@/modules/inventory/inventory-count-batch-sort';
import {
  buildCountReason,
  getCountReasonPresets,
  getInventoryCountWorkflowSteps,
} from '@/modules/inventory/inventory-count-workflow';
import { useInventoryEnums } from '@/shared/i18n/use-inventory-enums';

interface AdjustmentLineForm {
  batchId?: string;
  actualQuantity: number;
  note?: string;
}

export function AdjustmentListPage() {
  const navigate = useNavigate();
  const { message, modal } = App.useApp();
  const { t, i18n } = useTranslation('inventory', { keyPrefix: 'adjustmentList' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const { adjustmentStatusLabel, adjustmentStatusOptions } = useInventoryEnums();
  const inventoryCountWorkflowSteps = useMemo(
    () => getInventoryCountWorkflowSteps(),
    [i18n.language],
  );
  const countReasonPresets = useMemo(() => getCountReasonPresets(), [i18n.language]);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<AdjustmentListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<number | undefined>();
  const [warehouseFilter, setWarehouseFilter] = useState<string | undefined>();
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
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
  const [prepareAckError, setPrepareAckError] = useState(false);
  const [batchPickOpen, setBatchPickOpen] = useState(false);
  /** null = thêm dòng mới từ modal; số = gắn vào dòng Form.List hiện có */
  const [batchPickFieldIndex, setBatchPickFieldIndex] = useState<number | null>(null);
  const warehouseId = Form.useWatch('warehouseId', form);
  const formItems = Form.useWatch('items', form) as AdjustmentLineForm[] | undefined;

  const warehouseOptions = useMemo(
    () => warehouses.map((w) => ({ value: w.id, label: w.warehouseName })),
    [warehouses],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [paged, wh] = await Promise.all([
        fetchAdjustments({
          search: search.trim() || undefined,
          status: statusFilter,
          warehouseId: warehouseFilter,
          dateFrom: dateRange?.[0] ? dateRange[0].format('YYYY-MM-DD') : undefined,
          dateTo: dateRange?.[1] ? dateRange[1].format('YYYY-MM-DD') : undefined,
          page,
          pageSize,
        }),
        fetchWarehouses(),
      ]);
      setItems(paged.items);
      setTotal(paged.total);
      setWarehouses(wh);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t, message, search, statusFilter, warehouseFilter, dateRange, page, pageSize]);

  useEffect(() => {
    load();
  }, [load]);

  const resetFilters = () => {
    setSearchInput('');
    setSearch('');
    setStatusFilter(undefined);
    setWarehouseFilter(undefined);
    setDateRange(null);
    setPage(1);
  };

  useEffect(() => {
    if (!warehouseId) {
      setWarehouseBatches([]);
      return;
    }
    fetchStockBatches({ warehouseId, page: 1, pageSize: 200 })
      .then((r) => setWarehouseBatches(sortBatchesForCount(r.items)))
      .catch(() => setWarehouseBatches([]));
  }, [warehouseId]);

  const batchById = useMemo(() => {
    const map = new Map<string, StockBatch>();
    for (const b of warehouseBatches) map.set(b.id, b);
    return map;
  }, [warehouseBatches]);

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({ items: [] });
    setDrawerOpen(true);
  };

  const openBatchPick = (fieldIndex: number | null) => {
    if (!warehouseId) {
      message.warning(t('validation.selectWarehouseFirst'));
      return;
    }
    if (warehouseBatches.length === 0) {
      message.warning(t('validation.noBatchesInWarehouse'));
      return;
    }
    setBatchPickFieldIndex(fieldIndex);
    setBatchPickOpen(true);
  };

  const handleBatchPickConfirm = (selected: StockBatch[]) => {
    setBatchPickOpen(false);
    if (selected.length === 0) return;

    const current = [...((form.getFieldValue('items') as AdjustmentLineForm[] | undefined) ?? [])];
    const usedIds = new Set(
      current.map((r) => r.batchId).filter((id): id is string => Boolean(id)),
    );

    const toLine = (b: StockBatch): AdjustmentLineForm => ({
      batchId: b.id,
      actualQuantity: Math.max(0, b.quantityAvailable),
    });

    if (batchPickFieldIndex != null && batchPickFieldIndex >= 0 && batchPickFieldIndex < current.length) {
      const [first, ...rest] = selected;
      current[batchPickFieldIndex] = toLine(first);
      usedIds.add(first.id);
      for (const b of rest) {
        if (usedIds.has(b.id)) continue;
        current.push(toLine(b));
        usedIds.add(b.id);
      }
      form.setFieldsValue({ items: current });
    } else {
      const next = [...current];
      for (const b of selected) {
        if (usedIds.has(b.id)) continue;
        next.push(toLine(b));
        usedIds.add(b.id);
      }
      form.setFieldsValue({ items: next.length > 0 ? next : selected.map(toLine) });
    }

    message.success(
      selected.length === 1
        ? t('messages.batchSelected', { batch: selected[0].batchNumber })
        : t('messages.batchesAdded', { count: selected.length }),
    );
  };

  const openCreateSession = () => {
    sessionForm.resetFields();
    sessionForm.setFieldsValue({
      countType: 'periodic',
      warehouseId: warehouses[0]?.id,
    });
    setPrepareAcknowledged(false);
    setPrepareAckError(false);
    setSessionDrawerOpen(true);
  };

  const handleCreateSession = async () => {
    if (!prepareAcknowledged) {
      setPrepareAckError(true);
      modal.warning({
        title: t('messages.prepareRequiredTitle'),
        content: t('messages.prepareRequired'),
        okText: tc('actions.close'),
        centered: true,
      });
      return;
    }
    setPrepareAckError(false);
    try {
      const values = await sessionForm.validateFields();
      setSaving(true);

      const active = await fetchActiveCountingSession(values.warehouseId);
      if (active) {
        message.info(t('messages.sessionAlreadyOpen', { number: active.adjustmentNumber }));
        setSessionDrawerOpen(false);
        navigate(`/inventory/adjustments/${active.id}/count`);
        return;
      }

      const created = await createCountingSession({
        warehouseId: values.warehouseId,
        reason: buildCountReason(values.countType, values.reasonNote),
      });
      message.success(t('messages.sessionCreateSuccess', { number: created.adjustmentNumber }));
      setSessionDrawerOpen(false);
      navigate(`/inventory/adjustments/${created.id}/count`);
    } catch (error) {
      // Ant Design validateFields rejects with { errorFields } — not Axios.
      if (
        error &&
        typeof error === 'object' &&
        'errorFields' in error &&
        Array.isArray((error as { errorFields?: unknown[] }).errorFields)
      ) {
        message.warning(t('messages.sessionFormIncomplete'));
        return;
      }
      message.error(apiErrorMessage(error, t('messages.sessionCreateFailed')));
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
        items: (values.items as AdjustmentLineForm[])
          .filter((i) => i.batchId)
          .map((i) => ({
            batchId: i.batchId!,
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

  const handleCancel = (row: Pick<AdjustmentListItem, 'id' | 'adjustmentNumber'>) => {
    modal.confirm({
      title: t('messages.cancelConfirmTitle'),
      content: t('messages.cancelConfirmBody', { number: row.adjustmentNumber }),
      okText: t('messages.cancelConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('messages.cancelConfirmKeep'),
      centered: true,
      onOk: async () => {
        try {
          await cancelAdjustment(row.id);
          message.success(t('messages.cancelSuccess', { number: row.adjustmentNumber }));
          if (detail?.id === row.id) {
            setDetail(await fetchAdjustment(row.id));
          }
          await load();
        } catch (error) {
          message.error(apiErrorMessage(error, t('messages.cancelFailed')));
          throw error;
        }
      },
    });
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
      width: 280,
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
          {(row.status === 1 || row.status === 2) && (
            <Tag
              color="red"
              icon={<StopOutlined />}
              style={{ cursor: 'pointer', margin: 0 }}
              onClick={() => handleCancel(row)}
            >
              {ts('cancel')}
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
        <Space wrap style={{ marginBottom: 12, width: '100%' }} size={8}>
          <Input.Search
            allowClear
            placeholder={t('filters.searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onSearch={(v) => {
              setSearch(v.trim());
              setPage(1);
            }}
            style={{ width: 200 }}
          />
          <Select
            allowClear
            placeholder={t('filters.status')}
            options={adjustmentStatusOptions}
            value={statusFilter}
            onChange={(v) => {
              setStatusFilter(v);
              setPage(1);
            }}
            style={{ width: 160 }}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filters.warehouse')}
            options={warehouseOptions}
            value={warehouseFilter}
            onChange={(v) => {
              setWarehouseFilter(v);
              setPage(1);
            }}
            style={{ width: 220 }}
          />
          <DatePicker.RangePicker
            value={dateRange}
            onChange={(v) => {
              setDateRange(v);
              setPage(1);
            }}
            format="DD-MM-YYYY"
            placeholder={[t('filters.dateRange'), '']}
          />
          <Button onClick={resetFilters}>{t('filters.reset')}</Button>
        </Space>
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
          pagination={{
            current: page,
            pageSize,
            total,
            showSizeChanger: true,
            showTotal: (count) => t('paginationTotal', { count: count.toLocaleString('vi-VN') }),
            onChange: (p, ps) => {
              setPage(p);
              setPageSize(ps);
            },
          }}
        />
      </Card>

      <Drawer
        title={t('createBatchTitle')}
        width={880}
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
          <Typography.Paragraph type="secondary" style={{ marginTop: 0, fontSize: 13 }}>
            {t('batchDrawerTip')}
          </Typography.Paragraph>
          <Form.Item name="warehouseId" label={t('countWarehouse')} rules={[{ required: true }]}>
            <Select
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              placeholder={t('selectWarehouse')}
              onChange={() => form.setFieldsValue({ items: [] })}
            />
          </Form.Item>
          <Form.Item name="reason" label={ts('reason')}>
            <Input.TextArea rows={2} />
          </Form.Item>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'minmax(280px, 1fr) 112px 80px 72px 64px',
              gap: 10,
              marginBottom: 8,
              fontSize: 12,
              color: '#64748b',
              fontWeight: 600,
            }}
          >
            <span>{t('columns.batch')}</span>
            <span>{t('columns.actualQty')}</span>
            <span>{t('columns.systemQty')}</span>
            <span>{t('columns.variance')}</span>
            <span />
          </div>

          <Form.List
            name="items"
            rules={[
              {
                validator: async (_, value) => {
                  if (!value || value.length === 0) {
                    return Promise.reject(new Error(t('validation.atLeastOneBatch')));
                  }
                },
              },
            ]}
          >
            {(fields, { remove }, { errors }) => (
              <>
                {fields.map((field) => {
                  const row = formItems?.[field.name];
                  const batch = row?.batchId ? batchById.get(row.batchId) : undefined;
                  const sysQty = batch?.quantityAvailable ?? null;
                  const actual = Number(row?.actualQuantity ?? 0);
                  const variance = sysQty == null ? null : actual - sysQty;
                  const tone = getExpiryTone(batch?.expiryDate);
                  return (
                    <div
                      key={field.key}
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'minmax(280px, 1fr) 112px 80px 72px 64px',
                        gap: 10,
                        marginBottom: 8,
                        alignItems: 'start',
                      }}
                    >
                      <Form.Item
                        {...field}
                        name={[field.name, 'batchId']}
                        rules={[{ required: true, message: t('validation.selectBatch') }]}
                        style={{ marginBottom: 0 }}
                      >
                        <BatchPickTrigger
                          label={
                            batch
                              ? t('batchOptionLabel', {
                                  code: batch.productCode,
                                  batch: batch.batchNumber,
                                  qty: batch.quantityAvailable,
                                })
                              : t('pickBatch')
                          }
                          expiryColor={expiryToneColor(tone)}
                          onClick={() => openBatchPick(field.name)}
                          disabled={!warehouseId}
                        />
                      </Form.Item>
                      <Form.Item
                        {...field}
                        name={[field.name, 'actualQuantity']}
                        rules={[{ required: true, message: t('validation.actualQuantity') }]}
                        style={{ marginBottom: 0 }}
                      >
                        <InputNumber min={0} style={{ width: '100%' }} />
                      </Form.Item>
                      <div style={{ lineHeight: '32px', color: '#64748b' }}>
                        {sysQty == null ? '—' : formatDisplayQuantity(sysQty)}
                      </div>
                      <div
                        style={{
                          lineHeight: '32px',
                          fontWeight: 600,
                          color:
                            variance == null || variance === 0
                              ? '#64748b'
                              : variance > 0
                                ? '#389e0d'
                                : '#cf1322',
                        }}
                      >
                        {variance == null
                          ? '—'
                          : `${variance > 0 ? '+' : ''}${formatDisplayQuantity(variance)}`}
                      </div>
                      <Button type="text" danger onClick={() => remove(field.name)}>
                        {tc('actions.delete')}
                      </Button>
                    </div>
                  );
                })}
                <Button
                  type="primary"
                  ghost
                  icon={<AppstoreOutlined />}
                  onClick={() => openBatchPick(null)}
                  block
                  disabled={!warehouseId}
                  style={{ marginTop: fields.length > 0 ? 4 : 0 }}
                >
                  {t('pickBatches')}
                </Button>
                <Form.ErrorList errors={errors} />
              </>
            )}
          </Form.List>
        </Form>
      </Drawer>

      <InventoryCountBatchPickModal
        open={batchPickOpen}
        productLabel={
          warehouses.find((w) => w.id === warehouseId)?.warehouseName ?? t('countWarehouse')
        }
        batches={warehouseBatches}
        initialSelectedIds={
          batchPickFieldIndex != null && formItems?.[batchPickFieldIndex]?.batchId
            ? [formItems[batchPickFieldIndex].batchId!]
            : []
        }
        onCancel={() => setBatchPickOpen(false)}
        onConfirm={handleBatchPickConfirm}
      />

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
          {warehouses.length === 0 ? (
            <Typography.Paragraph type="danger">
              {t('messages.noWarehouse')}
            </Typography.Paragraph>
          ) : null}
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
          <div
            style={{
              padding: 12,
              borderRadius: 8,
              border: prepareAckError ? '1px solid #ff4d4f' : '1px solid transparent',
              background: prepareAckError ? '#fff2f0' : undefined,
            }}
          >
            <Checkbox
              checked={prepareAcknowledged}
              onChange={(e) => {
                setPrepareAcknowledged(e.target.checked);
                if (e.target.checked) setPrepareAckError(false);
              }}
            >
              {t('prepareAcknowledge')}
            </Checkbox>
            {prepareAckError ? (
              <Typography.Paragraph type="danger" style={{ margin: '8px 0 0', fontSize: 13 }}>
                {t('messages.prepareRequired')}
              </Typography.Paragraph>
            ) : null}
          </div>
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
              <Button danger icon={<StopOutlined />} onClick={() => handleCancel(detail)}>
                {ts('cancel')}
              </Button>
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

/** Trigger Form.Item (value/onChange) — mở modal chọn lô thay vì Select. */
function BatchPickTrigger({
  label,
  expiryColor,
  onClick,
  disabled,
  value,
}: {
  label: string;
  expiryColor?: string;
  onClick: () => void;
  disabled?: boolean;
  value?: string;
  onChange?: (v?: string) => void;
}) {
  const hasValue = Boolean(value);
  return (
    <Button
      block
      disabled={disabled}
      onClick={onClick}
      style={{
        textAlign: 'left',
        height: 'auto',
        minHeight: 32,
        whiteSpace: 'normal',
        borderColor: hasValue && expiryColor ? expiryColor : undefined,
        color: hasValue && expiryColor && expiryColor !== '#64748b' ? expiryColor : undefined,
      }}
    >
      {label}
    </Button>
  );
}
