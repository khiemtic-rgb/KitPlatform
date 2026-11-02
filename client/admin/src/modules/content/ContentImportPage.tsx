import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Card, Input, Select, Space, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchContentTopics, type ContentTopic } from '@/shared/api/content.api';
import { useContentBrand } from './content-brand-context';
import { importTopicVariants } from '@/shared/api/content-youtube.api';

const KINDS = [
  { value: 'web_long', label: 'Bài web (web_long)' },
  { value: 'seo_meta', label: 'Mô tả SEO (seo_meta)' },
  { value: 'social_caption', label: 'Caption mạng xã hội' },
  { value: 'fb_page', label: 'Bài Fanpage' },
  { value: 'youtube', label: 'Mô tả YouTube' },
];

/** Nhập bản viết có sẵn (nguyên văn, không qua AI) vào một bài. Bài chuyển sang Review; duyệt/đăng vẫn do người duyệt. */
export function ContentImportPage() {
  const { message } = App.useApp();
  const workspace = useContentBrand();
  const brandId = workspace.brandId;
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [topicId, setTopicId] = useState<string | undefined>();
  const [kind, setKind] = useState('web_long');
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setTopics([]);
    if (!brandId) return;
    setTopicId(undefined);
    void fetchContentTopics({ brandId }).then(setTopics);
  }, [brandId]);

  const submit = async () => {
    if (!topicId) return message.warning('Chọn bài.');
    setBusy(true);
    try {
      const r = await importTopicVariants(topicId, [{ kind, title: title || undefined, bodyMarkdown: body }]);
      message.success(`Đã nhập ${r.imported} bản viết, bài chuyển sang ${r.status}. Chưa đăng gì.`);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không nhập được'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <Typography.Title level={4} style={{ marginTop: 0 }}>Nhập bài có sẵn</Typography.Title>
      <Typography.Paragraph type="secondary">
        <Link to={`/content/topics${window.location.search}`}>← Bài viết</Link>
      </Typography.Paragraph>
      {!brandId ? <Alert type="warning" showIcon style={{ marginBottom: 16 }} message="Chọn một thương hiệu ở đầu trang để chọn bài." /> : null}
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Văn bản được lưu nguyên văn, không qua AI. Bài chuyển sang chờ duyệt; anh duyệt và đăng ở tab Bài viết."
      />
      <Card>
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <Select
              style={{ minWidth: 360 }}
              placeholder="Chọn bài"
              showSearch
              optionFilterProp="label"
              value={topicId}
              onChange={setTopicId}
              options={topics.map((t) => ({ value: t.id, label: t.title }))}
            />
            <Select style={{ minWidth: 220 }} value={kind} onChange={setKind} options={KINDS} />
          </Space>
          <Input placeholder="Tiêu đề (để trống = tiêu đề bài)" value={title} onChange={(e) => setTitle(e.target.value)} />
          <Input.TextArea
            rows={18}
            placeholder="Dán nội dung Markdown nguyên văn"
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />
          <Button type="primary" loading={busy} disabled={!body.trim()} onClick={() => void submit()}>
            Nhập nguyên văn
          </Button>
        </Space>
      </Card>
    </div>
  );
}
