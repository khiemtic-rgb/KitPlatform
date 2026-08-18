import { useEffect, useState } from 'react';
import { Button, Checkbox, Space, Typography } from 'antd';
import { CopyOutlined, TeamOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import type { FbGroupLink } from '@/modules/content/content-channels';

export function pickGroupPostText(
  variants: Array<{ kind: string; title?: string | null; bodyMarkdown?: string | null }>,
): string {
  const v = variants.find((x) => x.kind === 'group_suggested' && (x.bodyMarkdown ?? '').trim());
  if (!v) return '';
  return (v.bodyMarkdown ?? '').trim();
}

type Props = {
  groupLinks: FbGroupLink[];
  text: string;
  onCopied: (ok: boolean, opened?: string) => void;
};

export function ContentGroupPostBar({ groupLinks, text, onCopied }: Props) {
  const [done, setDone] = useState<Record<string, boolean>>({});

  useEffect(() => {
    setDone({});
  }, [text, groupLinks.map((g) => g.url).join('|')]);

  const copy = async (openUrl?: string, openName?: string) => {
    try {
      if (text.trim()) await navigator.clipboard.writeText(text);
      onCopied(true, openName);
      if (openUrl) {
        window.open(openUrl, '_blank', 'noopener,noreferrer');
        setDone((prev) => ({ ...prev, [openUrl]: true }));
      }
    } catch {
      onCopied(false);
      if (openUrl) window.open(openUrl, '_blank', 'noopener,noreferrer');
    }
  };

  const openOne = (g: FbGroupLink) => {
    window.open(g.url, '_blank', 'noopener,noreferrer');
    setDone((prev) => ({ ...prev, [g.url]: true }));
  };

  const remaining = groupLinks.filter((g) => !done[g.url]);
  const next = remaining[0];

  return (
    <div
      style={{
        marginBottom: 12,
        padding: 10,
        borderRadius: 8,
        background: '#f8fafc',
        border: '1px solid #e2e8f0',
      }}
    >
      <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
        Đăng tay nhiều nhóm
      </Typography.Text>
      <Typography.Paragraph type="secondary" style={{ margin: '0 0 8px', fontSize: 12 }}>
        Facebook không cho hệ thống đăng hộ vào nhóm. Chỉ copy chữ <code>group_suggested</code> — đừng kèm banner thương hiệu. Generate lại nếu bản cũ còn nhắc sản phẩm.
      </Typography.Paragraph>
      <Space wrap>
        <Button
          size="small"
          type="primary"
          icon={<CopyOutlined />}
          disabled={!text.trim()}
          onClick={() => void copy()}
        >
          Copy bài nhóm
        </Button>
        {groupLinks.length === 1 ? (
          <Button
            size="small"
            icon={<TeamOutlined />}
            disabled={!text.trim()}
            onClick={() => void copy(groupLinks[0].url, groupLinks[0].name)}
          >
            Copy & mở {groupLinks[0].name}
          </Button>
        ) : null}
        {groupLinks.length > 1 && next ? (
          <Button
            size="small"
            icon={<TeamOutlined />}
            disabled={!text.trim()}
            onClick={() => void copy(next.url, next.name)}
          >
            Copy & mở nhóm tiếp ({remaining.length} còn lại)
          </Button>
        ) : null}
      </Space>
      {groupLinks.length === 0 ? (
        <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
          Chưa có danh sách nhóm.{' '}
          <Link to="/content/brands">Thương hiệu → MXH · Nhóm Facebook</Link> — mỗi dòng một link,
          hoặc <code>Tên nhóm | https://facebook.com/groups/…</code>
        </Typography.Paragraph>
      ) : groupLinks.length > 1 ? (
        <div style={{ marginTop: 10 }}>
          {groupLinks.map((g) => (
            <div
              key={g.url}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '4px 0',
              }}
            >
              <Checkbox
                checked={!!done[g.url]}
                onChange={(e) => setDone((prev) => ({ ...prev, [g.url]: e.target.checked }))}
              />
              <Button size="small" type="link" style={{ padding: 0 }} onClick={() => openOne(g)}>
                {g.name}
              </Button>
            </div>
          ))}
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {remaining.length === 0
              ? 'Đã mở hết nhóm — tick để nhớ đã dán.'
              : `Còn ${remaining.length}/${groupLinks.length} nhóm chưa mở.`}
          </Typography.Text>
        </div>
      ) : null}
    </div>
  );
}
