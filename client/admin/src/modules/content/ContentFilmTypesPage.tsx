import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Checkbox, Form, Input, Select, Space, Switch, Table, Typography } from 'antd';
import {
  bindProjectFilmType,
  createFilmType,
  fetchFilmTypes,
  fetchKitVideoProjects,
  updateFilmType,
  type FilmTypeRow,
  type KitVideoProjectRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useContentBrand } from './content-brand-context';
import {
  VIDEO_PIPELINE_ASPECTS,
  VIDEO_PIPELINE_STEPS,
  VIDEO_PIPELINE_WORKFLOWS,
  videoWorkflowDescription,
} from './video-pipeline';

const STATUS = [
  { value: 'draft', label: 'Nháp' },
  { value: 'active', label: 'Đang bật' },
  { value: 'archived', label: 'Đã tắt' },
];

type PipelineForm = {
  code?: string;
  name: string;
  workflowKey: string;
  status?: string;
  purpose?: string;
  seriesLabel?: string;
  aspect?: string;
  isDefault?: boolean;
  steps?: string[];
};

function workflowLabel(key: string) {
  return VIDEO_PIPELINE_WORKFLOWS.find((row) => row.value === key)?.label || key;
}

const WORKFLOW_OPTIONS = VIDEO_PIPELINE_WORKFLOWS.map((row) => ({ value: row.value, label: row.label }));

function WorkflowSelect({
  form,
  disabled,
}: {
  form: ReturnType<typeof Form.useForm<PipelineForm>>[0];
  disabled?: boolean;
}) {
  const workflowKey = Form.useWatch('workflowKey', form);
  return (
    <Form.Item
      name="workflowKey"
      label="Dạng sản xuất"
      extra={videoWorkflowDescription(workflowKey)}
      rules={[{ required: true, message: 'Chọn dạng sản xuất' }]}
    >
      <Select
        placeholder="Dạng sản xuất"
        style={{ width: 320 }}
        options={WORKFLOW_OPTIONS}
        disabled={disabled}
        optionRender={(option) => (
          <div style={{ whiteSpace: 'normal', padding: '4px 0' }}>
            <div>{option.label}</div>
            <div style={{ color: '#888', fontSize: 12 }}>{videoWorkflowDescription(String(option.value))}</div>
          </div>
        )}
      />
    </Form.Item>
  );
}

function statusLabel(status: string) {
  return STATUS.find((row) => row.value === status)?.label || status;
}

export function ContentFilmTypesPage() {
  const { message } = App.useApp();
  const workspace = useContentBrand();
  const [rows, setRows] = useState<FilmTypeRow[]>([]);
  const [projects, setProjects] = useState<KitVideoProjectRow[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [projectId, setProjectId] = useState<string>();
  const [filmTypeId, setFilmTypeId] = useState<string>();
  const [editing, setEditing] = useState<FilmTypeRow | null>(null);
  const [form] = Form.useForm<PipelineForm>();
  const [editForm] = Form.useForm<PipelineForm>();

  const load = async (brandId: string) => {
    try {
      const [films, allProjects] = await Promise.all([fetchFilmTypes(brandId), fetchKitVideoProjects()]);
      setRows(films.filter((row) => row.brandId === brandId));
      setProjects(allProjects.filter((row) => row.brandId === brandId));
      setNotice(null);
    } catch (e) {
      setRows([]);
      setProjects([]);
      setNotice(apiErrorMessage(e, 'Video Pipeline chưa sẵn. Migration 395 chưa được áp dụng.'));
    }
  };

  useEffect(() => {
    if (!workspace.brandId) return;
    setEditing(null);
    void load(workspace.brandId);
  }, [workspace.brandId]);

  useEffect(() => {
    if (!editing) return;
    editForm.setFieldsValue({
      name: editing.name,
      workflowKey: editing.workflowKey,
      status: editing.status,
      purpose: editing.purpose || '',
      seriesLabel: editing.seriesLabel || '',
      aspect: editing.aspect || '',
      isDefault: editing.isDefault === true,
      steps: editing.steps ?? [],
    });
  }, [editForm, editing]);

  if (workspace.scope.mode !== 'one' || !workspace.brandId) {
    return <Alert type="info" showIcon message="Chọn một thương hiệu. Video Pipeline thuộc brand đang chọn." />;
  }

  const brandId = workspace.brandId;
  const editLocked = (editing?.filmCount ?? 0) > 0;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Text type="secondary">
        Các loại phim (quy trình dựng) của thương hiệu đang chọn. Phim đang dựng ở <Link to={`/content/studio?brand=${brandId}`}>Video Studio</Link>.
        Tỷ lệ khung là định dạng xuất, không phải công cụ tạo video.
      </Typography.Text>
      {notice ? <Alert type="warning" showIcon message={notice} /> : null}
      <Table
        rowKey="id"
        pagination={false}
        dataSource={rows}
        columns={[
          { title: 'Mã', dataIndex: 'code' },
          { title: 'Tên', dataIndex: 'name' },
          { title: 'Mục đích', dataIndex: 'purpose', render: (value: string) => value || '—' },
          { title: 'Dạng sản xuất', dataIndex: 'workflowKey', render: workflowLabel },
          { title: 'Trạng thái', dataIndex: 'status', render: statusLabel },
          {
            title: 'Mặc định',
            dataIndex: 'isDefault',
            render: (value: boolean) => (value ? 'Có' : ''),
          },
          {
            title: 'Phim đang dùng',
            dataIndex: 'filmCount',
            render: (value: number) => (value > 0 ? `${value} phim` : 'Chưa có phim'),
          },
          {
            title: '',
            render: (_value, row) => (
              <Button
                size="small"
                onClick={() => setEditing(row)}
              >
                Sửa
              </Button>
            ),
          },
        ]}
      />
      <Typography.Title level={5} style={{ marginBottom: 0 }}>
        Tạo pipeline
      </Typography.Title>
      <Form
        form={form}
        layout="vertical"
        style={{ maxWidth: 720 }}
        onFinish={async (values) => {
          try {
            await createFilmType({
              brandId,
              code: values.code || '',
              name: values.name,
              workflowKey: values.workflowKey,
              purpose: values.purpose,
              seriesLabel: values.seriesLabel,
              aspect: values.aspect,
              isDefault: values.isDefault === true,
              steps: values.steps,
            });
            form.resetFields();
            await load(brandId);
          } catch (e) {
            message.error(apiErrorMessage(e, 'Không tạo được pipeline'));
          }
        }}
      >
        <Space wrap align="start">
          <Form.Item name="code" label="Mã" rules={[{ required: true, message: 'Cần mã' }]}>
            <Input placeholder="novixa-ops" />
          </Form.Item>
          <Form.Item name="name" label="Tên" rules={[{ required: true, message: 'Cần tên' }]}>
            <Input placeholder="Tên pipeline" />
          </Form.Item>
          <WorkflowSelect form={form} />
        </Space>
        <Form.Item name="purpose" label="Mục đích">
          <Input.TextArea rows={2} placeholder="Pipeline này dùng để làm loại phim nào" />
        </Form.Item>
        <Form.Item name="seriesLabel" label="Nhãn hiển thị">
          <Input placeholder="Để trống nếu không cần nhãn riêng" />
        </Form.Item>
        <Space wrap align="start">
          <Form.Item name="aspect" label="Tỷ lệ khung mặc định">
            <Select
              style={{ width: 160 }}
              options={VIDEO_PIPELINE_ASPECTS.map((value) => ({ value, label: value || 'Chưa chọn' }))}
            />
          </Form.Item>
          <Form.Item name="isDefault" label="Mặc định khi brand có nhiều pipeline" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Space>
        <Form.Item name="steps" label="Bước sản xuất">
          <Checkbox.Group options={VIDEO_PIPELINE_STEPS.map((step) => ({ value: step.value, label: step.label }))} />
        </Form.Item>
        <Button type="primary" htmlType="submit">
          Tạo
        </Button>
      </Form>
      {editing ? (
        <>
          <Typography.Title level={5} style={{ marginBottom: 0 }}>
            Sửa {editing.name}
          </Typography.Title>
          {editLocked ? (
            <Alert type="info" showIcon message="Pipeline đang có phim nên không đổi workflow." />
          ) : null}
          <Form
            form={editForm}
            layout="vertical"
            style={{ maxWidth: 720 }}
            onFinish={async (values) => {
              try {
                await updateFilmType(editing.id, {
                  name: values.name,
                  workflowKey: values.workflowKey,
                  status: values.status || editing.status,
                  purpose: values.purpose,
                  seriesLabel: values.seriesLabel,
                  aspect: values.aspect,
                  isDefault: values.isDefault === true,
                  steps: values.steps,
                });
                setEditing(null);
                await load(brandId);
              } catch (e) {
                message.error(apiErrorMessage(e, 'Không sửa được pipeline'));
              }
            }}
          >
            <Space wrap align="start">
              <Form.Item name="name" label="Tên" rules={[{ required: true, message: 'Cần tên' }]}>
                <Input />
              </Form.Item>
              <WorkflowSelect form={editForm} disabled={editLocked} />
              <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
                <Select style={{ width: 160 }} options={STATUS} />
              </Form.Item>
            </Space>
            <Form.Item name="purpose" label="Mục đích">
              <Input.TextArea rows={2} />
            </Form.Item>
            <Form.Item name="seriesLabel" label="Nhãn hiển thị">
              <Input />
            </Form.Item>
            <Space wrap align="start">
              <Form.Item name="aspect" label="Tỷ lệ khung mặc định">
                <Select
                  style={{ width: 160 }}
                  options={VIDEO_PIPELINE_ASPECTS.map((value) => ({ value, label: value || 'Chưa chọn' }))}
                />
              </Form.Item>
              <Form.Item name="isDefault" label="Pipeline mặc định" valuePropName="checked">
                <Switch />
              </Form.Item>
            </Space>
            <Form.Item name="steps" label="Bước sản xuất">
              <Checkbox.Group options={VIDEO_PIPELINE_STEPS.map((step) => ({ value: step.value, label: step.label }))} />
            </Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">
                Lưu
              </Button>
              <Button onClick={() => setEditing(null)}>Đóng</Button>
            </Space>
          </Form>
        </>
      ) : null}
      {rows.length > 0 ? (
        <Space>
          <Select
            style={{ minWidth: 220 }}
            placeholder="Project cùng brand"
            value={projectId}
            onChange={setProjectId}
            options={projects.map((row) => ({ value: row.id, label: row.projectCode }))}
          />
          <Select
            style={{ minWidth: 220 }}
            placeholder="Video Pipeline"
            value={filmTypeId}
            onChange={setFilmTypeId}
            options={rows.filter((row) => row.status === 'active').map((row) => ({ value: row.id, label: row.name }))}
          />
          <Button
            disabled={!projectId || !filmTypeId}
            onClick={() => {
              if (!projectId || !filmTypeId) return;
              void bindProjectFilmType(projectId, filmTypeId)
                .then(() => load(brandId))
                .catch((e) => message.error(apiErrorMessage(e, 'Không gắn được project')));
            }}
          >
            Gắn project
          </Button>
        </Space>
      ) : null}
    </Space>
  );
}
