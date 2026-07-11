import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  createKapRule,
  deleteKapRule,
  fetchKapRules,
  fetchKapTemplates,
  updateKapRule,
  type KapRule,
  type KapTemplateListItem,
} from '@/shared/api/kap-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const DEFAULT_PAYLOAD = '{\n  "title": "Tiêu đề",\n  "body": "Nội dung",\n  "severity": "info"\n}';

export function KapRulesPage() {
  const [templates, setTemplates] = useState<KapTemplateListItem[]>([]);
  const [templateId, setTemplateId] = useState<string>();
  const [rules, setRules] = useState<KapRule[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<KapRule | null>(null);
  const [form] = Form.useForm();

  useEffect(() => {
    void fetchKapTemplates().then((items) => {
      setTemplates(items);
      if (items[0]) setTemplateId(items[0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!templateId) return;
    setLoading(true);
    try {
      setRules(await fetchKapRules(templateId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được quy tắc'));
    } finally {
      setLoading(false);
    }
  }, [templateId]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({
      actionType: 'insight',
      actionPayloadJson: DEFAULT_PAYLOAD,
      priority: 50,
      isActive: true,
    });
    setModalOpen(true);
  };

  const openEdit = (rule: KapRule) => {
    setEditing(rule);
    form.setFieldsValue(rule);
    setModalOpen(true);
  };

  const save = async () => {
    if (!templateId) return;
    const values = await form.validateFields();
    try {
      if (editing) {
        await updateKapRule(editing.id, values);
      } else {
        await createKapRule({ templateId, code: values.code, ...values });
      }
      message.success('Đã lưu quy tắc');
      setModalOpen(false);
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được quy tắc'));
    }
  };

  const remove = async (rule: KapRule) => {
    try {
      await deleteKapRule(rule.id);
      message.success('Đã xóa quy tắc');
      void load();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được'));
    }
  };

  const columns: ColumnsType<KapRule> = useMemo(
    () => [
      { title: 'Mã', dataIndex: 'code', width: 120 },
      { title: 'Tên', dataIndex: 'name', ellipsis: true },
      {
        title: 'Loại',
        dataIndex: 'actionType',
        width: 140,
        render: (v: string) =>
          v === 'insight' ? 'Nhận xét' : v === 'recommendation' ? 'Khuyến nghị' : v,
      },
      { title: 'Ưu tiên', dataIndex: 'priority', width: 80 },
      {
        title: 'Bật',
        dataIndex: 'isActive',
        width: 80,
        render: (v: boolean) => (v ? <Tag color="green">Bật</Tag> : <Tag>Tắt</Tag>),
      },
      {
        title: '',
        key: 'actions',
        width: 100,
        render: (_, row) => (
          <Space>
            <Button type="link" icon={<EditOutlined />} onClick={() => openEdit(row)} />
            <Button type="link" danger icon={<DeleteOutlined />} onClick={() => void remove(row)} />
          </Space>
        ),
      },
    ],
    [],
  );

  return (
    <>
      <Card
        title="Quy tắc đánh giá"
        extra={
          <Space>
            <Select
              style={{ width: 260 }}
              value={templateId}
              options={templates.map((t) => ({
                value: t.id,
                label: `${t.code} v${t.version}`,
              }))}
              onChange={setTemplateId}
            />
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading} />
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              Thêm quy tắc
            </Button>
          </Space>
        }
      >
        <Table rowKey="id" loading={loading} columns={columns} dataSource={rules} pagination={false} />
      </Card>

      <Modal
        title={editing ? 'Sửa quy tắc' : 'Thêm quy tắc'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void save()}
        width={720}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          {!editing ? (
            <Form.Item name="code" label="Mã quy tắc" rules={[{ required: true }]}>
              <Input placeholder="RULE_CODE" />
            </Form.Item>
          ) : null}
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="expression" label="Điều kiện" rules={[{ required: true }]}>
            <Input.TextArea rows={2} placeholder="category.CUSTOMER.score < 2.5" />
          </Form.Item>
          <Form.Item name="actionType" label="Loại" rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'insight', label: 'Nhận xét' },
                { value: 'recommendation', label: 'Khuyến nghị' },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="actionPayloadJson"
            label="Nội dung (JSON)"
            rules={[{ required: true }]}
            extra="Định dạng kỹ thuật cho tiêu đề và nội dung hiển thị trên báo cáo."
          >
            <Input.TextArea rows={8} />
          </Form.Item>
          <Space>
            <Form.Item name="priority" label="Ưu tiên">
              <InputNumber min={0} max={999} />
            </Form.Item>
            <Form.Item name="isActive" label="Bật quy tắc" valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
        </Form>
      </Modal>
    </>
  );
}
