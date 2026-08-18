import { useState } from 'react';
import { App, Button, Form, Input, Select, Space, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  updateContentPackageBrief,
  type ContentCreativeBrief,
  type ContentPackage,
} from '@/shared/api/content.api';
import {
  CONTENT_BRIEF_DURATIONS,
  CONTENT_BRIEF_EMOTIONS,
  CONTENT_BRIEF_FORMATS,
  CONTENT_BRIEF_OBJECTIVES,
  briefLabel,
} from '@/modules/content/content-brief';

type Props = {
  pkg: ContentPackage;
  busy?: boolean;
  onSaved: (next: ContentPackage) => void;
};

export function ContentPackageBriefCard({ pkg, busy, onSaved }: Props) {
  const { message } = App.useApp();
  const [form] = Form.useForm<ContentCreativeBrief>();
  const [saving, setSaving] = useState(false);
  const brief = pkg.creativeBrief;

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const next = await updateContentPackageBrief(pkg.id, {
        objective: values.objective?.trim() || undefined,
        emotion: values.emotion?.trim() || undefined,
        format: values.format?.trim() || undefined,
        visualDirection: values.visualDirection?.trim() || undefined,
        durationSec: values.durationSec || undefined,
      });
      message.success('Đã lưu Brief');
      onSaved(next);
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu Brief'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Cầu nối góc brand → sản xuất. Duyệt cần mục tiêu + format.
      </Typography.Paragraph>
      {brief?.objective || brief?.format ? (
        <Typography.Paragraph style={{ marginBottom: 12 }}>
          {briefLabel(CONTENT_BRIEF_OBJECTIVES, brief.objective)}
          {brief.format ? ` · ${briefLabel(CONTENT_BRIEF_FORMATS, brief.format)}` : ''}
          {brief.emotion ? ` · ${briefLabel(CONTENT_BRIEF_EMOTIONS, brief.emotion)}` : ''}
          {brief.durationSec ? ` · ${brief.durationSec}s` : ''}
        </Typography.Paragraph>
      ) : (
        <Typography.Paragraph type="secondary">Chưa điền Brief.</Typography.Paragraph>
      )}
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          objective: brief?.objective ?? undefined,
          emotion: brief?.emotion ?? undefined,
          format: brief?.format ?? undefined,
          visualDirection: brief?.visualDirection ?? undefined,
          durationSec: brief?.durationSec ?? undefined,
        }}
      >
        <Space wrap style={{ width: '100%' }} size={12}>
          <Form.Item name="objective" label="Mục tiêu" rules={[{ required: true, message: 'Chọn mục tiêu' }]} style={{ marginBottom: 8 }}>
            <Select allowClear style={{ width: 160 }} options={[...CONTENT_BRIEF_OBJECTIVES]} />
          </Form.Item>
          <Form.Item name="format" label="Format" rules={[{ required: true, message: 'Chọn format' }]} style={{ marginBottom: 8 }}>
            <Select allowClear style={{ width: 180 }} options={[...CONTENT_BRIEF_FORMATS]} />
          </Form.Item>
          <Form.Item name="emotion" label="Cảm xúc" style={{ marginBottom: 8 }}>
            <Select allowClear style={{ width: 140 }} options={[...CONTENT_BRIEF_EMOTIONS]} />
          </Form.Item>
          <Form.Item name="durationSec" label="Thời lượng (s)" style={{ marginBottom: 8 }}>
            <Select
              allowClear
              style={{ width: 110 }}
              options={CONTENT_BRIEF_DURATIONS.map((s) => ({ value: s, label: `${s}s` }))}
            />
          </Form.Item>
        </Space>
        <Form.Item name="visualDirection" label="Hướng hình" style={{ marginBottom: 8 }}>
          <Input.TextArea rows={2} placeholder="Screen-first, đồi chè, dược sĩ thật…" />
        </Form.Item>
        <Button type="primary" loading={saving || busy} onClick={() => void save()}>
          Lưu Brief
        </Button>
      </Form>
    </div>
  );
}
