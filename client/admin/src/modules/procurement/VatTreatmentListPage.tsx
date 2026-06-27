import { useCallback, useEffect, useState } from 'react';
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

function deleteBlockedReason(row: ProcurementVatTreatment): string | null {
  if (row.canDelete) return null;
  if (['kct', 'vat_0', 'vat_5', 'vat_8', 'vat_10'].includes(row.treatmentCode)) {
    return 'Mức thuế mặc định — không thể xóa';
  }
  return 'Đang dùng trên đơn đặt hàng — bấm Sửa và chọn «Ngừng dùng»';
}

export function VatTreatmentListPage() {
  const canWrite = useProcurementWrite();
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<ProcurementVatTreatment[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ProcurementVatTreatment | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<VatFormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setItems(await fetchVatTreatments(false));
    } catch (error) {
      const msg = apiErrorMessage(error, 'Không tải được bảng thuế');
      setLoadError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, []);

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
      message.success(`Đã xóa mức thuế «${row.treatmentCode}»`);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được mức thuế'));
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      const code = values.treatmentCode?.trim().toLowerCase() ?? '';
      if (!editing) {
        if (items.some((row) => row.treatmentCode === code)) {
          message.error(`Mã «${code}» đã có trong bảng. Bấm Sửa trên dòng đó thay vì thêm mới.`);
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
        message.success('Đã cập nhật loại thuế');
      } else {
        await createVatTreatment({
          treatmentCode: code,
          treatmentName: values.treatmentName.trim(),
          ratePercent: values.isNotSubject ? 0 : values.ratePercent,
          isNotSubject: values.isNotSubject,
          sortOrder: values.sortOrder,
        });
        message.success('Đã thêm loại thuế');
      }
      setModalOpen(false);
      void load();
    } catch (error) {
      if (isAxiosError(error)) {
        message.error(apiErrorMessage(error, 'Không lưu được loại thuế'));
        return;
      }
      if (error && typeof error === 'object' && 'errorFields' in error) {
        message.warning('Kiểm tra lại các trường bắt buộc.');
      }
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<ProcurementVatTreatment> = [
    { title: 'Mã', dataIndex: 'treatmentCode', width: 100 },
    { title: 'Tên hiển thị', dataIndex: 'treatmentName' },
    {
      title: 'Thuế suất',
      dataIndex: 'ratePercent',
      width: 100,
      align: 'right',
      render: (v: number, row) => (row.isNotSubject ? '—' : `${v}%`),
    },
    {
      title: 'Loại',
      width: 130,
      render: (_, row) =>
        row.isNotSubject ? <Tag>KCT</Tag> : <Tag color="blue">Thuế suất</Tag>,
    },
    { title: 'Thứ tự', dataIndex: 'sortOrder', width: 80, align: 'center' },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      width: 110,
      render: (v: boolean) =>
        v ? (
          <Tag icon={<CheckCircleOutlined />} color="green">
            Dùng
          </Tag>
        ) : (
          <Tag icon={<StopOutlined />}>Ngừng</Tag>
        ),
    },
    {
      title: 'Tác vụ',
      width: 110,
      render: (_, row) => {
        if (!canWrite) return null;
        const blocked = deleteBlockedReason(row);
        return (
          <Space size={4}>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
              Sửa
            </Button>
            <Popconfirm
              title={`Xóa «${row.treatmentCode}»?`}
              description="Chỉ xóa được mức thuế tự thêm, chưa dùng trên đơn đặt hàng."
              okText="Xóa"
              cancelText="Hủy"
              okButtonProps={{ danger: true }}
              disabled={!row.canDelete}
              onConfirm={() => void handleDelete(row)}
            >
              <Tooltip title={blocked ?? 'Xóa mức thuế tự thêm'}>
                <span>
                  <Button
                    type="text"
                    size="small"
                    danger
                    disabled={!row.canDelete}
                    icon={<DeleteOutlined />}
                    aria-label="Xóa"
                    style={!row.canDelete ? { opacity: 0.35 } : undefined}
                  />
                </span>
              </Tooltip>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  return (
    <Card
      title={
        <Space>
          <PercentageOutlined />
          Cài đặt thuế GTGT (mua hàng)
        </Space>
      }
      bordered={false}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Thêm mức thuế
            </Button>
          ) : null}
        </Space>
      }
    >
      <p style={{ marginTop: 0, color: '#666' }}>
        Cấu hình theo tenant — khi luật đổi chỉ cần thêm/sửa tại đây. <strong>KCT</strong> và{' '}
        <strong>thuế suất 0%</strong> là hai loại khác nhau; bảng mặc định đã có sẵn 5 dòng (kct, 0%, 5%, 8%, 10%).
        Mức thuế tự thêm có thể xóa nếu chưa dùng trên đơn đặt hàng.
      </p>
      <Table
        rowKey="id"
        loading={loading}
        columns={columns}
        dataSource={items}
        pagination={false}
        locale={{
          emptyText: loadError
            ? loadError
            : 'Chưa có dòng thuế. Bấm «Tải lại» hoặc chạy migration 039 rồi restart API.',
        }}
      />

      <Modal
        title={
          editing ? (
            <Space>
              <EditOutlined />
              Sửa loại thuế
            </Space>
          ) : (
            <Space>
              <PlusOutlined />
              Thêm loại thuế
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
              label="Mã (vd: vat_12)"
              extra="Chữ thường, không trùng mã đã có (vd: kct đã có sẵn)."
              rules={[
                { required: true, message: 'Nhập mã' },
                {
                  pattern: /^[a-z0-9_]+$/,
                  message: 'Chỉ dùng chữ thường, số và _',
                },
              ]}
            >
              <Input placeholder="vat_12" onBlur={(e) => form.setFieldValue('treatmentCode', e.target.value.trim().toLowerCase())} />
            </Form.Item>
          )}
          <Form.Item name="treatmentName" label="Tên hiển thị" rules={[{ required: true }]}>
            <Input placeholder="Thuế suất 12%" />
          </Form.Item>
          <Form.Item name="isNotSubject" label="Không chịu thuế GTGT (KCT)" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item noStyle shouldUpdate={(prev, cur) => prev.isNotSubject !== cur.isNotSubject}>
            {() =>
              form.getFieldValue('isNotSubject') ? null : (
                <Form.Item
                  name="ratePercent"
                  label="Thuế suất (%)"
                  rules={[{ required: true, message: 'Nhập thuế suất' }]}
                >
                  <InputNumber min={0} max={100} style={{ width: '100%' }} />
                </Form.Item>
              )
            }
          </Form.Item>
          <Space style={{ width: '100%' }} size="large">
            <Form.Item name="sortOrder" label="Thứ tự">
              <InputNumber min={0} />
            </Form.Item>
            {editing && (
              <Form.Item name="isActive" label="Đang dùng" valuePropName="checked">
                <Switch />
              </Form.Item>
            )}
          </Space>
        </Form>
      </Modal>
    </Card>
  );
}
