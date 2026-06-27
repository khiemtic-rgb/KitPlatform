import { useCallback, useEffect, useState } from 'react';
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
import { ADJUSTMENT_STATUS_LABELS } from '@/shared/api/inventory.types';
import { formatDisplayDate } from '@/shared/utils/date';
import { InventoryCountWorkflowSteps } from '@/modules/inventory/InventoryCountWorkflowSteps';
import {
  COUNT_REASON_PRESETS,
  INVENTORY_COUNT_WORKFLOW_STEPS,
  buildCountReason,
} from '@/modules/inventory/inventory-count-workflow';

interface AdjustmentLineForm {
  batchId: string;
  actualQuantity: number;
  note?: string;
}

export function AdjustmentListPage() {
  const navigate = useNavigate();
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
      message.error(apiErrorMessage(error, 'Không tải được phiếu kiểm kê'));
    } finally {
      setLoading(false);
    }
  }, []);

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
      message.warning('Xác nhận đã chuẩn bị kiểm kê (bước 1) trước khi mở phiên');
      return;
    }
    try {
      const values = await sessionForm.validateFields();
      setSaving(true);
      const created = await createCountingSession({
        warehouseId: values.warehouseId,
        reason: buildCountReason(values.countType, values.reasonNote),
      });
      message.success(`Đã mở phiên ${created.adjustmentNumber}`);
      setSessionDrawerOpen(false);
      navigate(`/inventory/adjustments/${created.id}/count`);
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không mở được phiên kiểm kê'));
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
      message.error(apiErrorMessage(error, 'Không tải được chi tiết phiếu'));
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
      message.success(`Đã tạo phiếu ${created.adjustmentNumber}`);
      setDrawerOpen(false);
      load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không tạo được phiếu'));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id: string) => {
    try {
      await approveAdjustment(id);
      message.success('Đã duyệt kiểm kê');
      if (detail?.id === id) {
        setDetail(await fetchAdjustment(id));
      }
      load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không duyệt được phiếu'));
    }
  };

  const columns: ColumnsType<AdjustmentListItem> = [
    { title: 'Số phiếu', dataIndex: 'adjustmentNumber', width: 130 },
    { title: 'Kho', dataIndex: 'warehouseName' },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (v: number) => (
        <Tag color={v === 3 ? 'green' : v === 2 ? 'processing' : v === 1 ? 'gold' : 'default'}>
          {ADJUSTMENT_STATUS_LABELS[v] ?? v}
        </Tag>
      ),
    },
    {
      title: 'Ngày',
      dataIndex: 'adjustmentDate',
      width: 110,
      render: (v: string) => formatDisplayDate(v),
    },
    { title: 'Dòng', dataIndex: 'itemCount', width: 70, align: 'right' },
    {
      title: 'Thao tác',
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
              Đếm
            </Tag>
          )}
          <Tag
            color="blue"
            icon={<EyeOutlined />}
            style={{ cursor: 'pointer', margin: 0 }}
            onClick={() => openDetail(row.id)}
          >
            Chi tiết
          </Tag>
          {row.status !== 3 && row.status !== 4 && row.status !== 2 && (
            <Tag
              color="green"
              icon={<CheckOutlined />}
              style={{ cursor: 'pointer', margin: 0 }}
              onClick={() => handleApprove(row.id)}
            >
              Duyệt
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
            label: 'Quy trình kiểm kê chuẩn (4 bước)',
            children: (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <InventoryCountWorkflowSteps status={2} entryCount={0} canApprove={false} />
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                  <strong>Phiên kiểm kê</strong> (nhiều SP, nhiều người): mở phiên → đếm → đối chiếu → duyệt trên màn
                  Đếm. <strong>Phiếu theo lô</strong>: lệch vài lô đã biết — tạo phiếu nhập SL thực tế → duyệt tại
                  danh sách.
                </Typography.Paragraph>
                <ul style={{ margin: 0, paddingLeft: 18, fontSize: 13, color: '#555' }}>
                  {INVENTORY_COUNT_WORKFLOW_STEPS.map((step) => (
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
        title="Phiếu kiểm kê"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              Tải lại
            </Button>
            <Button icon={<TeamOutlined />} onClick={openCreateSession}>
              Phiên kiểm kê
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Phiếu theo lô
            </Button>
          </Space>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer
        title="Tạo phiếu kiểm kê theo lô"
        width={600}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setDrawerOpen(false)}>Hủy</Button>
            <Button type="primary" loading={saving} onClick={handleCreate}>
              Lưu
            </Button>
          </Space>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="warehouseId" label="Kho kiểm kê" rules={[{ required: true }]}>
            <Select
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              placeholder="Chọn kho"
            />
          </Form.Item>
          <Form.Item name="reason" label="Lý do">
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
                      rules={[{ required: true, message: 'Chọn lô' }]}
                      style={{ width: 300, marginBottom: 0 }}
                    >
                      <Select
                        placeholder="Lô hàng"
                        options={warehouseBatches.map((b) => ({
                          value: b.id,
                          label: `${b.productCode} / ${b.batchNumber} (HT ${b.quantityAvailable})`,
                        }))}
                      />
                    </Form.Item>
                    <Form.Item
                      {...field}
                      name={[field.name, 'actualQuantity']}
                      rules={[{ required: true, message: 'SL thực' }]}
                      style={{ width: 110, marginBottom: 0 }}
                    >
                      <InputNumber min={0} style={{ width: '100%' }} />
                    </Form.Item>
                    <Button type="text" danger onClick={() => remove(field.name)}>
                      Xóa
                    </Button>
                  </Space>
                ))}
                <Button type="dashed" onClick={() => add({ actualQuantity: 0 })} block>
                  Thêm dòng
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Drawer>

      <Drawer
        title="Bước 1 — Mở phiên kiểm kê"
        width={520}
        open={sessionDrawerOpen}
        onClose={() => setSessionDrawerOpen(false)}
        extra={
          <Space>
            <Button onClick={() => setSessionDrawerOpen(false)}>Hủy</Button>
            <Button type="primary" loading={saving} onClick={handleCreateSession}>
              Bắt đầu đếm
            </Button>
          </Space>
        }
      >
        <Form form={sessionForm} layout="vertical">
          <Form.Item name="warehouseId" label="Kho kiểm kê" rules={[{ required: true }]}>
            <Select
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              placeholder="Chọn kho"
            />
          </Form.Item>
          <Form.Item name="countType" label="Loại kiểm kê" rules={[{ required: true }]}>
            <Select options={COUNT_REASON_PRESETS.map((p) => ({ value: p.value, label: p.label }))} />
          </Form.Item>
          <Form.Item name="reasonNote" label="Ghi chú thêm">
            <Input.TextArea rows={2} placeholder="VD: Cuối tháng 6, khu A+B…" />
          </Form.Item>
          <Checkbox checked={prepareAcknowledged} onChange={(e) => setPrepareAcknowledged(e.target.checked)}>
            Đã thông báo hạn chế bán hàng / nhập xuất tại kho trong lúc kiểm kê
          </Checkbox>
        </Form>
      </Drawer>

      <Drawer
        title={detail ? `Phiếu ${detail.adjustmentNumber}` : 'Chi tiết kiểm kê'}
        width={640}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && detail.status !== 3 && detail.status !== 4 ? (
            <Space>
              {detail.status === 2 && (
                <Button type="primary" onClick={() => navigate(`/inventory/adjustments/${detail.id}/count`)}>
                  Màn đếm
                </Button>
              )}
              {detail.status !== 2 && (
                <Button type="primary" onClick={() => handleApprove(detail.id)}>
                  Duyệt
                </Button>
              )}
            </Space>
          ) : null
        }
      >
        {detail && (
          <>
            <p>
              <strong>Kho:</strong> {detail.warehouseName}
            </p>
            <p>
              <strong>Trạng thái:</strong> {ADJUSTMENT_STATUS_LABELS[detail.status] ?? detail.status}
            </p>
            {detail.reason && (
              <p>
                <strong>Lý do:</strong> {detail.reason}
              </p>
            )}
            {detail.status === 2 ? (
              <p style={{ color: '#1677ff' }}>
                Phiên đang kiểm — vào màn <strong>Đếm</strong>, hoàn tất bước 2–3 rồi <strong>Duyệt</strong> (bước 4)
                kèm checklist.
              </p>
            ) : (
              <Table
                rowKey="id"
                size="small"
                pagination={false}
                dataSource={detail.items}
                columns={[
                  { title: 'SP', dataIndex: 'productName' },
                  { title: 'Lô', dataIndex: 'batchNumber', width: 90 },
                  { title: 'HT', dataIndex: 'systemQuantity', width: 70, align: 'right' },
                  { title: 'Thực', dataIndex: 'actualQuantity', width: 70, align: 'right' },
                  { title: 'Lệch', dataIndex: 'differenceQuantity', width: 70, align: 'right' },
                ]}
              />
            )}
          </>
        )}
      </Drawer>
    </>
  );
}
