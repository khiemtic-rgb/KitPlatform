import { useEffect, useState } from 'react';
import { App, Button, Form, Input, InputNumber, Select, Space, Table, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentPackagePerformance,
  ingestContentPackagePerformance,
  type ContentPerformance,
} from '@/shared/api/content.api';

const CHANNELS = [
  { value: 'facebook_page', label: 'Fanpage' },
  { value: 'website', label: 'Website' },
  { value: 'other', label: 'Khác' },
];

type Props = {
  packageId: string;
};

export function ContentPackagePerformanceCard({ packageId }: Props) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [rows, setRows] = useState<ContentPerformance[]>([]);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      setRows(await fetchContentPackagePerformance(packageId));
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải số liệu'));
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [packageId]);

  const onSave = async () => {
    const values = await form.validateFields();
    setBusy(true);
    try {
      await ingestContentPackagePerformance(packageId, {
        channel: values.channel,
        metricDate: values.metricDate,
        impressions: values.impressions ?? null,
        views: values.views ?? null,
        clicks: values.clicks ?? null,
        engagements: values.engagements ?? null,
        comments: values.comments ?? null,
        shares: values.shares ?? null,
        utmCampaign: values.utmCampaign?.trim() || undefined,
        utmSource: values.utmSource?.trim() || undefined,
        utmMedium: values.utmMedium?.trim() || undefined,
        notes: values.notes?.trim() || undefined,
      });
      message.success('Đã ghi số liệu');
      form.resetFields(['impressions', 'views', 'clicks', 'engagements', 'comments', 'shares', 'notes']);
      await load();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu số liệu'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Nhập tay từ Ads Manager / analytics. Chưa kéo Pixel hay API MXH.
      </Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          channel: 'facebook_page',
          metricDate: new Date().toISOString().slice(0, 10),
        }}
      >
        <Space wrap size={12}>
          <Form.Item name="channel" label="Kênh" rules={[{ required: true }]} style={{ marginBottom: 8 }}>
            <Select style={{ width: 140 }} options={CHANNELS} />
          </Form.Item>
          <Form.Item name="metricDate" label="Ngày" rules={[{ required: true }]} style={{ marginBottom: 8 }}>
            <Input type="date" style={{ width: 150 }} />
          </Form.Item>
          <Form.Item name="impressions" label="Hiển thị" style={{ marginBottom: 8 }}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="views" label="Xem" style={{ marginBottom: 8 }}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="clicks" label="Click" style={{ marginBottom: 8 }}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="engagements" label="Tương tác" style={{ marginBottom: 8 }}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
        </Space>
        <Space wrap size={12}>
          <Form.Item name="utmCampaign" label="utm_campaign" style={{ marginBottom: 8 }}>
            <Input style={{ width: 180 }} placeholder="kit-mkt-aug" />
          </Form.Item>
          <Form.Item name="utmSource" label="utm_source" style={{ marginBottom: 8 }}>
            <Input style={{ width: 140 }} placeholder="facebook" />
          </Form.Item>
          <Form.Item name="utmMedium" label="utm_medium" style={{ marginBottom: 8 }}>
            <Input style={{ width: 140 }} placeholder="cpc" />
          </Form.Item>
        </Space>
        <Form.Item name="notes" label="Ghi chú" style={{ marginBottom: 8 }}>
          <Input placeholder="Nguồn số: Ads Manager / GA…" />
        </Form.Item>
        <Button type="primary" loading={busy} onClick={() => void onSave()}>
          Ghi số liệu
        </Button>
      </Form>
      <Table<ContentPerformance>
        size="small"
        rowKey="id"
        pagination={false}
        dataSource={rows}
        locale={{ emptyText: 'Chưa có số' }}
        columns={[
          {
            title: 'Ngày',
            dataIndex: 'metricDate',
            width: 110,
            render: (v: string) => String(v).slice(0, 10),
          },
          {
            title: 'Kênh',
            dataIndex: 'channel',
            width: 110,
            render: (v: string) => CHANNELS.find((c) => c.value === v)?.label ?? v,
          },
          { title: 'Hiển thị', dataIndex: 'impressions', width: 90 },
          { title: 'Xem', dataIndex: 'views', width: 80 },
          { title: 'Click', dataIndex: 'clicks', width: 80 },
          { title: 'UTM', dataIndex: 'utmCampaign', ellipsis: true },
        ]}
      />
    </Space>
  );
}
