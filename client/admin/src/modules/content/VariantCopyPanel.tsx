import { App, Alert, Button, Card, Input, Space, Tag, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import type { ContentVariant } from '@/shared/api/content.api';
import { checkFacebookPost, splitFacebookPost } from './facebook-post';

/**
 * Tab "Bản viết": gom theo nơi đăng. Mỗi phần nằm trong một ô riêng, có số ký tự và nút Copy,
 * để dán thẳng vào Fanpage / website / mạng xã hội mà không phải bôi đen từng đoạn.
 */

type Box = {
  key: string;
  label: string;
  hint?: string;
  text: string;
  rows?: number;
};

type Group = {
  key: string;
  title: string;
  color: string;
  note?: string;
  issues?: string[];
  boxes: Box[];
};

const HASHTAG = /#[\p{L}\p{N}_]+/gu;

function hashtagsOf(text: string): string[] {
  return Array.from(new Set(text.match(HASHTAG) ?? []));
}

function pick(variants: ContentVariant[], kind: string) {
  return variants.find((v) => v.kind === kind && v.bodyMarkdown.trim());
}

export function buildVariantGroups(variants: ContentVariant[]): Group[] {
  const groups: Group[] = [];
  const used = new Set<string>();

  const fb = pick(variants, 'fb_page');
  const fbShort = pick(variants, 'fb_short');
  if (fb || fbShort) {
    const boxes: Box[] = [];
    let issues: string[] | undefined;
    if (fb) {
      used.add('fb_page');
      const { post, comment } = splitFacebookPost(fb.bodyMarkdown);
      issues = checkFacebookPost(post);
      const tags = hashtagsOf(post);
      if (tags.length === 0) issues.push('chưa có hashtag (nên 3–5)');
      boxes.push({ key: 'fb-post', label: 'Nội dung bài', hint: 'Đã gồm hashtag. Dán vào ô viết bài trên Fanpage', text: post, rows: 12 });
      if (tags.length > 0) {
        boxes.push({ key: 'fb-tags', label: `Hashtag (${tags.length})`, hint: 'Copy riêng nếu cần dán lại', text: tags.join(' '), rows: 1 });
      }
      if (comment) {
        boxes.push({ key: 'fb-comment', label: 'Bình luận đầu', hint: 'Đăng xong, dán làm bình luận và ghim', text: comment, rows: 4 });
      }
    }
    if (fbShort) {
      used.add('fb_short');
      boxes.push({ key: 'fb-short', label: 'Bản ngắn (dùng thay khi cần bài gọn)', text: fbShort.bodyMarkdown.trim(), rows: 5 });
    }
    groups.push({
      key: 'facebook',
      title: 'Facebook Fanpage',
      color: 'blue',
      note: 'Fanpage đăng tay. Bài lên lịch hiện ở tab Đăng tay.',
      issues,
      boxes,
    });
  }

  const web = pick(variants, 'web_long');
  const seo = pick(variants, 'seo_meta');
  if (web || seo) {
    const boxes: Box[] = [];
    if (web) {
      used.add('web_long');
      if (web.title?.trim()) boxes.push({ key: 'web-title', label: 'Tiêu đề', text: web.title.trim(), rows: 1 });
    }
    if (seo) {
      used.add('seo_meta');
      boxes.push({ key: 'web-seo', label: 'Mô tả SEO', text: seo.bodyMarkdown.trim(), rows: 2 });
    }
    if (web) boxes.push({ key: 'web-body', label: 'Nội dung bài (Markdown)', text: web.bodyMarkdown.trim(), rows: 16 });
    groups.push({
      key: 'website',
      title: 'Website',
      color: 'green',
      note: 'Website Thái Nguyên Life tự đăng khi đến giờ. Các ô này chỉ để xem hoặc dán tay.',
      boxes,
    });
  }

  const social = pick(variants, 'social_caption');
  if (social) {
    used.add('social_caption');
    groups.push({
      key: 'social',
      title: 'Mạng xã hội khác',
      color: 'purple',
      note: 'Caption cho Instagram, nhóm Facebook, LinkedIn và các nơi đăng tay khác.',
      boxes: [
        { key: 'social-caption', label: 'Caption', hint: 'Đã gồm hashtag', text: social.bodyMarkdown.trim(), rows: 7 },
        ...(hashtagsOf(social.bodyMarkdown).length > 0
          ? [{ key: 'social-tags', label: 'Hashtag', hint: 'Copy riêng nếu cần', text: hashtagsOf(social.bodyMarkdown).join(' '), rows: 1 }]
          : []),
      ],
    });
  }

  const rest = variants.filter((v) => !used.has(v.kind) && v.bodyMarkdown.trim());
  if (rest.length > 0) {
    groups.push({
      key: 'other',
      title: 'Bản khác',
      color: 'default',
      boxes: rest.map((v) => ({
        key: `other-${v.id}`,
        label: v.title?.trim() ? `${v.kind} · ${v.title.trim()}` : v.kind,
        text: v.bodyMarkdown.trim(),
        rows: 8,
      })),
    });
  }
  return groups;
}

async function writeClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    try {
      const el = document.createElement('textarea');
      el.value = text;
      el.style.position = 'fixed';
      el.style.opacity = '0';
      document.body.appendChild(el);
      el.select();
      const ok = document.execCommand('copy');
      document.body.removeChild(el);
      return ok;
    } catch {
      return false;
    }
  }
}

function CopyBox({ box }: { box: Box }) {
  const { message } = App.useApp();
  const copy = async () => {
    if (await writeClipboard(box.text)) message.success(`Đã copy «${box.label}»`);
    else message.error('Trình duyệt không cho copy. Bôi đen trong ô rồi bấm Ctrl+C.');
  };
  return (
    <div style={{ marginBottom: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 4 }}>
        <Space size={8} wrap>
          <Typography.Text strong>{box.label}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {box.text.length} ký tự
          </Typography.Text>
          {box.hint ? (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              · {box.hint}
            </Typography.Text>
          ) : null}
        </Space>
        <Button size="small" type="primary" icon={<CopyOutlined />} onClick={() => void copy()}>
          Copy
        </Button>
      </div>
      <Input.TextArea
        value={box.text}
        readOnly
        autoSize={{ minRows: Math.min(box.rows ?? 4, 3), maxRows: box.rows ?? 12 }}
        onFocus={(e) => e.target.select()}
        style={{ lineHeight: 1.6 }}
      />
    </div>
  );
}

export function VariantCopyPanel({ variants }: { variants: ContentVariant[] }) {
  const groups = buildVariantGroups(variants);
  if (groups.length === 0) {
    return <Typography.Text type="secondary">Chưa có bản viết</Typography.Text>;
  }
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {groups.map((g) => (
        <Card
          key={g.key}
          size="small"
          title={<Tag color={g.color}>{g.title}</Tag>}
          extra={
            g.issues && g.issues.length === 0 ? (
              <Tag color="success">Đạt chuẩn Fanpage</Tag>
            ) : null
          }
        >
          {g.note ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 0, fontSize: 12 }}>
              {g.note}
            </Typography.Paragraph>
          ) : null}
          {g.issues && g.issues.length > 0 ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message="Cần sửa trước khi đăng"
              description={
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {g.issues.map((i) => (
                    <li key={i}>{i}</li>
                  ))}
                </ul>
              }
            />
          ) : null}
          {g.boxes.map((b) => (
            <CopyBox key={b.key} box={b} />
          ))}
        </Card>
      ))}
    </Space>
  );
}
