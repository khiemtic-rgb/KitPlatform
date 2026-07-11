import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Descriptions, Drawer, Form, Input, Select, Space, Table, Tag, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, ReloadOutlined } from '@ant-design/icons';
import { fetchKapTemplate, fetchKapTemplates, updateKapTemplate, type KapTemplateListItem } from '@/shared/api/kap-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayDateTime } from '@/shared/utils/date';

export function KapTemplatesPage() {
  const [items, setItems] = useState<KapTemplateListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [editing, setEditing] = useState<KapTemplateListItem | null>(null);
  const [questionCount, setQuestionCount] = useState(0);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await fetchKapTemplates());
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được biểu mẫu'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openEdit = async (row: KapTemplateListItem) => {
    setEditing(row);
    setDetailOpen(true);
    form.setFieldsValue({ name: row.name, description: row.description ?? '', status: row.status });
    try {
      const detail = await fetchKapTemplate(row.id);
      let count = 0;
      for (const cat of detail.tree.categories as Array<{ dimensions?: Array<{ questions?: unknown[] }> }>) {
        for (const dim of cat.dimensions ?? []) {
          count += dim.questions?.length ?? 0;
        }
      }
      setQuestionCount(count);
    } catch {
      setQuestionCount(0);
    }
  };

  const save = async () => {
    if (!editing) return;
    const values = await form.validateFields();
    try {
      await updateKapTemplate(editing.id, values);
      message.success('Đã cập nhật biểu mẫu');
      setDetailOpen(false);
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được'));
    }
  };

  const columns: ColumnsType<KapTemplateListItem> = [
    { title: 'Mã', dataIndex: 'code', width: 130 },
    { title: 'Tên', dataIndex: 'name', ellipsis: true },
    { title: 'Phiên bản', dataIndex: 'version', width: 90 },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      width: 100,
      render: (v: string) => (
        <Tag color={v === 'active' ? 'green' : v === 'archived' ? 'default' : 'blue'}>
          {v === 'active' ? 'Đang dùng' : v === 'draft' ? 'Nháp' : v === 'archived' ? 'Lưu trữ' : v}
        </Tag>
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
      width: 80,
      render: (_, row) => (
        <Button type="link" icon={<EditOutlined />} onClick={() => void openEdit(row)} />
      ),
    },
  ];

  return (
    <>
      <Card
        title="Biểu mẫu khảo sát"
        extra={
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Làm mới
          </Button>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
      </Card>

      <Drawer title="Chi tiết biểu mẫu" width={520} open={detailOpen} onClose={() => setDetailOpen(false)}>
        {editing ? (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Mã">{editing.code}</Descriptions.Item>
              <Descriptions.Item label="Phiên bản">{editing.version}</Descriptions.Item>
              <Descriptions.Item label="Số câu hỏi">{questionCount}</Descriptions.Item>
            </Descriptions>
            <Form form={form} layout="vertical" onFinish={() => void save()}>
              <Form.Item name="name" label="Tên hiển thị" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
              <Form.Item name="description" label="Mô tả">
                <Input.TextArea rows={3} />
              </Form.Item>
              <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
                <Select
                  options={[
                    { value: 'draft', label: 'Nháp' },
                    { value: 'active', label: 'Đang dùng' },
                    { value: 'archived', label: 'Lưu trữ' },
                  ]}
                />
              </Form.Item>
              <Button type="primary" htmlType="submit">
                Lưu thông tin
              </Button>
            </Form>
          </Space>
        ) : null}
      </Drawer>
    </>
  );
}
