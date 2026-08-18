import { useEffect, useMemo, useState } from 'react';
import { Button, Checkbox, Empty, Space, Typography } from 'antd';
import { CopyOutlined, LinkOutlined, PictureOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import type { ContentChannelTarget, ContentSiteTarget, ContentVariant } from '@/shared/api/content.api';
import {
  destsForLane,
  lanesFromTargets,
  pickManualPostText,
  type ManualDest,
} from '@/modules/content/content-manual-dest';

type Props = {
  channels: ContentChannelTarget[];
  sites: ContentSiteTarget[];
  variants: ContentVariant[];
  hasImage: boolean;
  onCopyImage: () => Promise<void>;
  onCopied: (ok: boolean, detail: string) => void;
};

export function ContentManualPostTab({
  channels,
  sites,
  variants,
  hasImage,
  onCopyImage,
  onCopied,
}: Props) {
  const lanes = useMemo(() => lanesFromTargets(channels, sites), [channels, sites]);
  const [lane, setLane] = useState<string>('');
  const [done, setDone] = useState<Record<string, boolean>>({});
  const [copyingImage, setCopyingImage] = useState(false);

  useEffect(() => {
    if (!lanes.length) {
      setLane('');
      return;
    }
    setLane((cur) => (lanes.some((l) => l.key === cur) ? cur : lanes[0]!.key));
  }, [lanes]);

  useEffect(() => {
    setDone({});
  }, [lane]);

  const active = lanes.find((l) => l.key === lane) ?? lanes[0];
  const dests = active ? destsForLane(active.key, channels, sites) : [];
  const picked = active ? pickManualPostText(variants, active.variantKinds) : { text: '', kind: null };
  const remaining = dests.filter((d) => !done[d.key]);

  const copyText = async (open?: ManualDest) => {
    if (!picked.text.trim()) {
      onCopied(false, 'Chưa có bản viết cho kênh này — Generate đúng kênh trước.');
      return;
    }
    try {
      await navigator.clipboard.writeText(picked.text);
      if (open?.url) {
        window.open(open.url, '_blank', 'noopener,noreferrer');
        setDone((prev) => ({ ...prev, [open.key]: true }));
        onCopied(true, `Đã copy — mở ${open.name}`);
      } else {
        onCopied(true, 'Đã copy bài');
      }
    } catch {
      onCopied(false, 'Không copy được chữ');
    }
  };

  const copyImage = async () => {
    setCopyingImage(true);
    try {
      await onCopyImage();
      onCopied(true, 'Đã copy ảnh — dán riêng sau khi dán chữ');
    } catch (e) {
      const raw = e instanceof Error ? e.message : '';
      onCopied(
        false,
        /clipboard|image\/jpeg|not supported/i.test(raw)
          ? 'Trình duyệt không nhận JPEG — đang cần PNG. Thử Copy ảnh lại, hoặc tải ảnh rồi kéo vào Facebook.'
          : raw || 'Không copy được ảnh',
      );
    } finally {
      setCopyingImage(false);
    }
  };

  if (lanes.length === 0) {
    return (
      <Empty
        description={
          <span>
            Brand chưa có Fanpage / nhóm / LinkedIn.{' '}
            <Link to="/content/brands">Thêm nơi đăng trên Thương hiệu</Link>
          </span>
        }
      />
    );
  }

  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'stretch', minHeight: 280 }}>
      <div style={{ width: 200, flexShrink: 0, borderRight: '1px solid #e2e8f0', paddingRight: 8 }}>
        {lanes.map((l) => {
          const n = destsForLane(l.key, channels, sites).length;
          const on = l.key === active?.key;
          return (
            <button
              key={l.key}
              type="button"
              onClick={() => setLane(l.key)}
              style={{
                display: 'block',
                width: '100%',
                textAlign: 'left',
                padding: '8px 10px',
                marginBottom: 4,
                border: 'none',
                borderRadius: 8,
                cursor: 'pointer',
                background: on ? '#1677ff' : 'transparent',
                color: on ? '#fff' : 'inherit',
              }}
            >
              <div style={{ fontWeight: 600 }}>{l.label}</div>
              <div style={{ fontSize: 12, opacity: 0.8 }}>
                {n} đích{l.alwaysManual ? ' · luôn tay' : ''}
              </div>
            </button>
          );
        })}
      </div>

      <div style={{ flex: 1, minWidth: 0 }}>
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {active?.key === 'facebook_group'
            ? 'Bài nhóm: chỉ chữ giọng thành viên. Đừng dán banner/logo Famixa–Novixa — group đọc thành quảng cáo. Ảnh đời (gia đình, màn hình) thì tự chọn trên Facebook, hoặc đăng không ảnh.'
            : active?.alwaysManual
              ? 'Kênh này không đăng API — copy chữ, copy ảnh (nếu có), mở từng đích rồi dán.'
              : 'Có thể Đẩy lịch đăng nếu còn token. Tab này dùng khi hết hạn hoặc muốn đăng tay.'}{' '}
          {picked.kind ? (
            <>
              Bản đang copy: <Typography.Text code>{picked.kind}</Typography.Text>
            </>
          ) : (
            <Typography.Text type="warning">
              {active?.key === 'facebook_group'
                ? 'Chưa có bài nhóm — Generate (có kênh Nhóm Facebook), không dùng bài fanpage.'
                : 'Chưa có bản viết khớp kênh này.'}
            </Typography.Text>
          )}
        </Typography.Paragraph>

        <Space wrap style={{ marginBottom: 12 }}>
          <Button
            type="primary"
            icon={<CopyOutlined />}
            disabled={!picked.text}
            onClick={() => void copyText()}
          >
            Copy bài {active?.label}
          </Button>
          {active?.key === 'facebook_group' ? null : (
            <Button
              icon={<PictureOutlined />}
              loading={copyingImage}
              disabled={!hasImage}
              onClick={() => void copyImage()}
            >
              Copy ảnh
            </Button>
          )}
          {dests.length > 1 && remaining[0] ? (
            <Button
              icon={<LinkOutlined />}
              disabled={!picked.text || !remaining[0].url}
              onClick={() => void copyText(remaining[0])}
            >
              Copy & mở tiếp ({remaining.length} còn)
            </Button>
          ) : dests.length === 1 && dests[0]?.url ? (
            <Button icon={<LinkOutlined />} disabled={!picked.text} onClick={() => void copyText(dests[0])}>
              Copy & mở {dests[0].name}
            </Button>
          ) : null}
        </Space>

        {dests.length === 0 ? (
          <Typography.Text type="secondary">Chưa có đích trong loại này.</Typography.Text>
        ) : (
          dests.map((d) => (
            <div
              key={d.key}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '6px 0',
                borderBottom: '1px solid #f1f5f9',
              }}
            >
              <Checkbox
                checked={!!done[d.key]}
                onChange={(e) => setDone((prev) => ({ ...prev, [d.key]: e.target.checked }))}
              />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Typography.Text strong>{d.name}</Typography.Text>
                {d.hint ? (
                  <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                    {d.hint}
                  </Typography.Text>
                ) : null}
              </div>
              {d.url ? (
                <Button size="small" type="link" onClick={() => window.open(d.url!, '_blank', 'noopener,noreferrer')}>
                  Mở
                </Button>
              ) : (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Thiếu link
                </Typography.Text>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
