import { useCallback, useEffect, useState } from 'react';
import {
  App,
  Button,
  Drawer,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Table,
  Typography,
} from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  createContentBrand,
  fetchContentBrands,
  fetchContentChannels,
  fetchContentSettings,
  fetchContentSites,
  updateContentBrand,
  upsertContentChannel,
  upsertContentSite,
  type ContentBrand,
  type ContentChannelTarget,
  type ContentSiteTarget,
} from '@/shared/api/content.api';

export function ContentBrandsPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(true);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [connectorTypes, setConnectorTypes] = useState<string[]>([]);
  const [channelTypes, setChannelTypes] = useState<string[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editing, setEditing] = useState<ContentBrand | null>(null);
  const [sites, setSites] = useState<ContentSiteTarget[]>([]);
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [form] = Form.useForm();
  const [siteForm] = Form.useForm();
  const [channelForm] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [b, settings] = await Promise.all([fetchContentBrands(false), fetchContentSettings()]);
      setBrands(b);
      setConnectorTypes(settings.connectorTypes ?? []);
      setChannelTypes(settings.channelTypes ?? []);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được brand'));
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ pauseWhenExceeded: true, isActive: true, sortOrder: 100 });
    setSites([]);
    setChannels([]);
    setDrawerOpen(true);
  };

  const openEdit = async (row: ContentBrand) => {
    setEditing(row);
    form.setFieldsValue({
      code: row.code,
      name: row.name,
      defaultCtaUrl: row.defaultCtaUrl ?? undefined,
      defaultCtaLabel: row.defaultCtaLabel ?? undefined,
      monthlyCeilingUsd: row.monthlyCeilingUsd ?? undefined,
      imageTier: row.imageTier ?? undefined,
      pauseWhenExceeded: row.pauseWhenExceeded,
      isActive: row.isActive,
      sortOrder: row.sortOrder,
    });
    setDrawerOpen(true);
    try {
      const [s, c] = await Promise.all([fetchContentSites(row.id), fetchContentChannels(row.id)]);
      setSites(s);
      setChannels(c);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải site/channel'));
    }
  };

  const saveBrand = async () => {
    try {
      const v = await form.validateFields();
      if (editing) {
        await updateContentBrand(editing.id, v);
        message.success('Đã cập nhật brand');
      } else {
        await createContentBrand(v);
        message.success('Đã tạo brand');
        setDrawerOpen(false);
      }
      await load();
      if (editing) {
        const refreshed = (await fetchContentBrands(false)).find((b) => b.id === editing.id);
        if (refreshed) setEditing(refreshed);
      }
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được brand'));
    }
  };

  const saveSite = async () => {
    if (!editing) return;
    try {
      const v = await siteForm.validateFields();
      await upsertContentSite(editing.id, { ...v, isActive: true });
      message.success('Đã lưu site');
      siteForm.resetFields();
      setSites(await fetchContentSites(editing.id));
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được site'));
    }
  };

  const saveChannel = async () => {
    if (!editing) return;
    try {
      const v = await channelForm.validateFields();
      await upsertContentChannel(editing.id, { ...v, isActive: true });
      message.success('Đã lưu channel');
      channelForm.resetFields();
      setChannels(await fetchContentChannels(editing.id));
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được channel'));
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Brand / Web / Page
          </Typography.Title>
          <Typography.Text type="secondary">
            Thêm brand + site + fanpage động — không cần nâng cấp schema.
          </Typography.Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            Thêm brand
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        loading={loading}
        dataSource={brands}
        columns={[
          { title: 'Code', dataIndex: 'code', width: 120 },
          { title: 'Tên', dataIndex: 'name' },
          {
            title: 'Trần brand',
            dataIndex: 'monthlyCeilingUsd',
            render: (v: number | null | undefined) => (v == null ? 'Theo global' : `$${v}`),
          },
          { title: 'Tier', dataIndex: 'imageTier', render: (v) => v ?? 'Theo global' },
          {
            title: 'Active',
            dataIndex: 'isActive',
            render: (v: boolean) => (v ? 'Có' : 'Không'),
          },
          {
            title: '',
            key: 'actions',
            render: (_, row) => (
              <Button type="link" onClick={() => void openEdit(row)}>
                Sửa
              </Button>
            ),
          },
        ]}
      />

      <Drawer
        title={editing ? `Brand: ${editing.name}` : 'Thêm brand'}
        width={560}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        extra={
          <Button type="primary" onClick={() => void saveBrand()}>
            Lưu brand
          </Button>
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="code" label="Code" rules={[{ required: true }]}>
            <Input disabled={!!editing} placeholder="novixa" />
          </Form.Item>
          <Form.Item name="name" label="Tên" rules={[{ required: true }]}>
            <Input placeholder="Novixa" />
          </Form.Item>
          <Form.Item name="defaultCtaUrl" label="CTA mặc định">
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="defaultCtaLabel" label="Nhãn CTA">
            <Input />
          </Form.Item>
          <Form.Item name="monthlyCeilingUsd" label="Trần USD/tháng (để trống = global)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="imageTier" label="Tier ảnh override">
            <Select
              allowClear
              options={[
                { value: 'lean', label: 'Lean' },
                { value: 'balanced', label: 'Balanced' },
                { value: 'premium', label: 'Premium' },
              ]}
            />
          </Form.Item>
          <Form.Item name="pauseWhenExceeded" label="Pause khi vượt trần" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="isActive" label="Active" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="sortOrder" label="Sort">
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
        </Form>

        {editing ? (
          <>
            <Typography.Title level={5}>Sites (web)</Typography.Title>
            <Table
              size="small"
              rowKey="id"
              pagination={false}
              dataSource={sites}
              columns={[
                { title: 'Code', dataIndex: 'code' },
                { title: 'Tên', dataIndex: 'name' },
                { title: 'Connector', dataIndex: 'connectorType' },
                { title: 'URL', dataIndex: 'baseUrl' },
              ]}
              style={{ marginBottom: 12 }}
            />
            <Form form={siteForm} layout="vertical">
              <Space align="start" wrap>
                <Form.Item name="code" rules={[{ required: true }]}>
                  <Input placeholder="code" />
                </Form.Item>
                <Form.Item name="name" rules={[{ required: true }]}>
                  <Input placeholder="name" />
                </Form.Item>
                <Form.Item name="connectorType" rules={[{ required: true }]}>
                  <Select
                    style={{ width: 160 }}
                    options={connectorTypes.map((c) => ({ value: c, label: c }))}
                    placeholder="connector"
                  />
                </Form.Item>
                <Form.Item name="baseUrl">
                  <Input placeholder="https://" style={{ width: 180 }} />
                </Form.Item>
                <Form.Item name="secretRef">
                  <Input placeholder="env secret ref" style={{ width: 160 }} />
                </Form.Item>
                <Form.Item name="configJson">
                  <Input placeholder='{"username":"..."}' style={{ width: 200 }} />
                </Form.Item>
                <Button onClick={() => void saveSite()}>Thêm/sửa site</Button>
              </Space>
            </Form>

            <Typography.Title level={5}>Channels (page)</Typography.Title>
            <Table
              size="small"
              rowKey="id"
              pagination={false}
              dataSource={channels}
              columns={[
                { title: 'Code', dataIndex: 'code' },
                { title: 'Tên', dataIndex: 'name' },
                { title: 'Type', dataIndex: 'channelType' },
                { title: 'External id', dataIndex: 'externalId' },
              ]}
              style={{ marginBottom: 12 }}
            />
            <Form form={channelForm} layout="vertical">
              <Space align="start" wrap>
                <Form.Item name="code" rules={[{ required: true }]}>
                  <Input placeholder="code" />
                </Form.Item>
                <Form.Item name="name" rules={[{ required: true }]}>
                  <Input placeholder="name" />
                </Form.Item>
                <Form.Item name="channelType" rules={[{ required: true }]}>
                  <Select
                    style={{ width: 160 }}
                    options={channelTypes.map((c) => ({ value: c, label: c }))}
                    placeholder="type"
                  />
                </Form.Item>
                <Form.Item name="externalId">
                  <Input placeholder="page id" style={{ width: 140 }} />
                </Form.Item>
                <Form.Item name="secretRef">
                  <Input placeholder="env page token" style={{ width: 160 }} />
                </Form.Item>
                <Button onClick={() => void saveChannel()}>Thêm/sửa channel</Button>
              </Space>
            </Form>
          </>
        ) : null}
      </Drawer>
    </div>
  );
}
