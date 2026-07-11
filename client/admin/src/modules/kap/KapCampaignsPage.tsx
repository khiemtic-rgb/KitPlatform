import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Form, Input, Modal, Select, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  archiveSurveyCampaign,
  createSurveyCampaign,
  fetchKapTemplates,
  fetchSurveyCampaigns,
  updateSurveyCampaign,
  type KapTemplateListItem,
  type SurveyCampaign,
} from '@/shared/api/kap-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';

export function KapCampaignsPage() {
  const [templates, setTemplates] = useState<KapTemplateListItem[]>([]);
  const [items, setItems] = useState<SurveyCampaign[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<SurveyCampaign | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [campaigns, tpls] = await Promise.all([fetchSurveyCampaigns(), fetchKapTemplates()]);
      setItems(campaigns);
      setTemplates(tpls);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được chiến dịch'));
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
    form.setFieldsValue({ status: 'active', settingsJson: '{}' });
    setModalOpen(true);
  };

  const openEdit = (row: SurveyCampaign) => {
    setEditing(row);
    form.setFieldsValue({
      campaignName: row.campaignName,
      status: row.status,
      settingsJson: row.settingsJson,
    });
    setModalOpen(true);
  };

  const save = async () => {
    const values = await form.validateFields();
    try {
      if (editing) {
        await updateSurveyCampaign(editing.id, values);
      } else {
        await createSurveyCampaign(values);
      }
      message.success('Đã lưu chiến dịch');
      setModalOpen(false);
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được chiến dịch'));
    }
  };

  const archive = async (row: SurveyCampaign) => {
    try {
      await archiveSurveyCampaign(row.id);
      message.success('Đã lưu trữ chiến dịch');
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu trữ được'));
    }
  };

  const statusLabel = (v: string) =>
    v === 'active' ? 'Đang chạy' : v === 'paused' ? 'Tạm dừng' : v === 'archived' ? 'Lưu trữ' : v;

  const columns: ColumnsType<SurveyCampaign> = [
    { title: 'Mã', dataIndex: 'campaignCode', width: 140 },
    { title: 'Tên', dataIndex: 'campaignName', ellipsis: true },
    { title: 'Biểu mẫu', dataIndex: 'templateCode', width: 130 },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 110,
      render: (v: string) => (
        <Tag color={v === 'active' ? 'green' : v === 'paused' ? 'orange' : 'default'}>{statusLabel(v)}</Tag>
      ),
    },
    {
      title: 'Cập nhật',
      dataIndex: 'updatedAt',
      width: 160,
      render: (v: string) => formatDisplayDateTime(v),
    },
    {
      title: '',
      key: 'actions',
      width: 100,
      render: (_, row) => (
        <Space>
          <Button type="link" icon={<EditOutlined />} onClick={() => openEdit(row)} />
          <Button type="link" danger icon={<DeleteOutlined />} onClick={() => void archive(row)} />
        </Space>
      ),
    },
  ];

  return (
    <>
      <Card
        title="Chiến dịch khảo sát"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading} />
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Tạo chiến dịch
            </Button>
          </Space>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Modal
        title={editing ? 'Sửa chiến dịch' : 'Tạo chiến dịch'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void save()}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          {!editing ? (
            <>
              <Form.Item name="templateId" label="Biểu mẫu" rules={[{ required: true }]}>
                <Select
                  options={templates.map((t) => ({
                    value: t.id,
                    label: `${t.code} v${t.version}`,
                  }))}
                />
              </Form.Item>
              <Form.Item name="campaignCode" label="Mã chiến dịch" rules={[{ required: true }]}>
                <Input placeholder="PHARMACY_Q1_2026" />
              </Form.Item>
            </>
          ) : null}
          <Form.Item name="campaignName" label="Tên" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'active', label: 'Đang chạy' },
                { value: 'paused', label: 'Tạm dừng' },
                { value: 'archived', label: 'Lưu trữ' },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="settingsJson"
            label="Cấu hình bổ sung (JSON)"
            extra="Tùy chọn kỹ thuật — để trống {} nếu không dùng."
          >
            <Input.TextArea rows={4} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
