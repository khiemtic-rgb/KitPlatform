import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Card, Form, Input, Modal, Popconfirm, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
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
      message.error(apiErrorMessage(error, 'Không tải được danh sách bác sĩ kê đơn'));
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
        message.success('Đã cập nhật bác sĩ kê đơn');
      } else {
        await createPrescriber(values);
        message.success('Đã thêm bác sĩ kê đơn');
      }
      setModalOpen(false);
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, editing ? 'Không cập nhật được bác sĩ' : 'Không tạo được bác sĩ'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deletePrescriber(id);
      message.success('Đã xóa bác sĩ kê đơn');
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được bác sĩ'));
    }
  };

  const columns: ColumnsType<RxPrescriber> = useMemo(
    () => [
      {
        title: 'Họ tên',
        dataIndex: 'fullName',
      },
      {
        title: 'Số giấy phép',
        dataIndex: 'licenseNumber',
        width: 180,
        render: (value?: string) => value || '—',
      },
      {
        title: 'Điện thoại',
        dataIndex: 'phone',
        width: 130,
        render: (value?: string) => value || '—',
      },
      {
        title: 'Chuyên khoa',
        dataIndex: 'specialty',
        width: 180,
        render: (value?: string) => value || '—',
      },
      {
        title: 'Trạng thái',
        dataIndex: 'status',
        width: 110,
        render: (value: number) => (
          <Tag color={value === 1 ? 'green' : 'default'}>{value === 1 ? 'Đang dùng' : 'Ngưng'}</Tag>
        ),
      },
      {
        title: 'Thao tác',
        key: 'actions',
        width: 140,
        render: (_, row) =>
          canWrite ? (
            <Space size={4}>
              <Button size="small" type="link" onClick={() => openEdit(row)}>
                Sửa
              </Button>
              <Popconfirm title="Xóa bác sĩ này?" onConfirm={() => void handleDelete(row.id)}>
                <Button size="small" type="link" danger>
                  Xóa
                </Button>
              </Popconfirm>
            </Space>
          ) : null,
      },
    ],
    [canWrite],
  );

  return (
    <Card
      title="Bác sĩ kê đơn"
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
          {canWrite ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Thêm bác sĩ
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
        title={editing ? 'Cập nhật bác sĩ kê đơn' : 'Thêm bác sĩ kê đơn'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void submit()}
        okText={editing ? 'Lưu' : 'Tạo'}
        cancelText="Hủy"
        confirmLoading={saving}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            label="Họ tên"
            name="fullName"
            rules={[{ required: true, message: 'Nhập họ tên bác sĩ' }]}
          >
            <Input placeholder="Nguyễn Văn A" />
          </Form.Item>
          <Form.Item
            label="Số CCHN"
            name="licenseNumber"
            rules={[{ required: true, message: 'Nhập số chứng chỉ hành nghề (CCHN)' }]}
            extra="Bắt buộc để đối chiếu khi thanh tra / kiểm tra"
          >
            <Input placeholder="Số CCHN" />
          </Form.Item>
          <Form.Item label="Điện thoại" name="phone">
            <Input placeholder="09xxxxxxxx" />
          </Form.Item>
          <Form.Item label="Chuyên khoa" name="specialty">
            <Input placeholder="Nội tổng quát" />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
