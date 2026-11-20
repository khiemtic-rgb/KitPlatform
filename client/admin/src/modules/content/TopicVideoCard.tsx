import { useState } from 'react';
import { App, Button, Card, Space, Typography, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { ContentAsset } from '@/shared/api/content.api';
import { uploadTopicVideo } from '@/shared/api/content-youtube.api';

/**
 * Video đính kèm của một bài (cho YouTube/TikTok). Phim làm trong Video Studio tự đính ở bước 5;
 * thẻ này dùng cho video dựng ở ngoài.
 */
export function TopicVideoCard({ topicId, assets, onUploaded }: { topicId: string; assets: ContentAsset[]; onUploaded: () => void | Promise<void> }) {
  const { message } = App.useApp();
  const [busy, setBusy] = useState(false);
  const videos = assets.filter((a) => a.kind === 'video');
  return (
    <Card size="small" title={`Video (${videos.length})`}>
      <Space direction="vertical" size={8} style={{ width: '100%' }}>
        {videos.length ? (
          videos.map((v) => (
            <Typography.Text key={v.id} ellipsis>
              {v.fileName}
            </Typography.Text>
          ))
        ) : (
          <Typography.Text type="secondary">Chưa có video. Bài có video mới được xếp lịch lên YouTube/TikTok.</Typography.Text>
        )}
        <Upload
          accept=".mp4,.mov,.webm,.m4v"
          showUploadList={false}
          customRequest={async ({ file, onSuccess, onError }) => {
            setBusy(true);
            try {
              const v = await uploadTopicVideo(topicId, file as File);
              message.success(`Đã đính video: ${v.fileName}`);
              onSuccess?.(v);
              await onUploaded();
            } catch (e) {
              message.error(apiErrorMessage(e, 'Không tải được video'));
              onError?.(e as Error);
            } finally {
              setBusy(false);
            }
          }}
        >
          <Button size="small" icon={<UploadOutlined />} loading={busy}>
            {videos.length ? 'Thay video' : 'Tải video lên'}
          </Button>
        </Upload>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>MP4 / MOV / WEBM, tối đa 512 MB.</Typography.Text>
      </Space>
    </Card>
  );
}
