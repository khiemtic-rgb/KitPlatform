import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Card,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { isAxiosError } from 'axios';
import { PlusOutlined, ReloadOutlined, EyeOutlined, CheckOutlined } from '@ant-design/icons';
import {
  approveAdjustment,
  createAdjustment,
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

interface AdjustmentLineForm {
  batchId: string;
  actualQuantity: number;
  note?: string;
}

export function AdjustmentListPage() {
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<AdjustmentListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [warehouseBatches, setWarehouseBatches] = useState<StockBatch[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<AdjustmentDetail | null>(null);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
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
        <Tag color={v === 3 ? 'green' : 'default'}>{ADJUSTMENT_STATUS_LABELS[v] ?? v}</Tag>
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
      width: 160,
      render: (_, row) => (
        <Space size={4} onClick={(e) => e.stopPropagation()}>
          <Tag
            color="blue"
            icon={<EyeOutlined />}
            style={{ cursor: 'pointer', margin: 0 }}
            onClick={() => openDetail(row.id)}
          >
            Chi tiết
          </Tag>
          {row.status !== 3 && row.status !== 4 && (
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
      <Card
        title="Phiếu kiểm kê"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              Tải lại
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Tạo phiếu
            </Button>
          </Space>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer
        title="Tạo phiếu kiểm kê"
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
        title={detail ? `Phiếu ${detail.adjustmentNumber}` : 'Chi tiết kiểm kê'}
        width={640}
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        extra={
          detail && detail.status !== 3 && detail.status !== 4 ? (
            <Button type="primary" onClick={() => handleApprove(detail.id)}>
              Duyệt
            </Button>
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
          </>
        )}
      </Drawer>
    </>
  );
}
