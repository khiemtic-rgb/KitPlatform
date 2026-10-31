import { useCallback, useEffect, useRef, useState } from 'react';
import { App, Button, Card, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { CheckOutlined, PlayCircleOutlined, ReloadOutlined, UploadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  approveContentVideoJob,
  fetchContentVideoJobs,
  importContentVideo,
  type ContentBrand,
  type ContentVideoJob,
} from '@/shared/api/content.api';
import { factoryVideoTag, playableMarketingVideoUrl } from '@/modules/content/content-video-factory-label';

type ImportedMeta = { imported?: boolean; sourceFileName?: string; durationSeconds?: number; sha256?: string };

/** Chọn template theo khung thật của file: ngang → 16:9 (video dài), vuông → 1:1, còn lại dọc mặc định. */
async function templateCodeForFile(file: File): Promise<string | undefined> {
  const url = URL.createObjectURL(file);
  try {
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.src = url;
    await new Promise<void>((resolve, reject) => {
      video.onloadedmetadata = () => resolve();
      video.onerror = () => reject(new Error('metadata'));
    });
    if (video.videoWidth > video.videoHeight) return 'youtube_long_16x9';
    if (video.videoWidth > 0 && video.videoWidth === video.videoHeight) return 'reel_square_promo';
    return undefined;
  } catch {
    return undefined;
  } finally {
    URL.revokeObjectURL(url);
  }
}

function readMeta(job: ContentVideoJob): ImportedMeta {
  try {
    return JSON.parse(job.configJson || '{}') as ImportedMeta;
  } catch {
    return {};
  }
}

/** Video thành phẩm dựng ngoài hệ thống: nhập file MP4, kiểm giải mã ở máy chủ, rồi xem và duyệt. */
export function ContentImportedVideosCard({ brands, brandId }: { brands?: ContentBrand[]; brandId?: string }) {
  const { message } = App.useApp();
  const fileRef = useRef<HTMLInputElement>(null);
  const [rows, setRows] = useState<ContentVideoJob[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [targetBrand, setTargetBrand] = useState<string | undefined>(brandId);
  const [title, setTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [watch, setWatch] = useState<ContentVideoJob | null>(null);

  useEffect(() => {
    if (brandId) setTargetBrand(brandId);
  }, [brandId]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const all = await fetchContentVideoJobs({ brandId });
      setRows(all.filter((j) => !j.packageId && !j.topicId && readMeta(j).imported === true));
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được video đã nhập'));
    } finally {
      setLoading(false);
    }
  }, [brandId, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const onImport = async () => {
    if (!targetBrand) {
      message.warning('Chọn thương hiệu');
      return;
    }
    if (!file) {
      message.warning('Chọn file MP4');
      return;
    }
    setBusy(true);
    try {
      const templateCode = await templateCodeForFile(file);
      const job = await importContentVideo({
        brandId: targetBrand,
        title: title.trim() || file.name.replace(/\.mp4$/i, ''),
        templateCode,
        file,
      });
      message.success(`Đã nhập: ${job.title}`);
      setFile(null);
      setTitle('');
      if (fileRef.current) fileRef.current.value = '';
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Nhập video thất bại'));
      await load();
    } finally {
      setBusy(false);
    }
  };

  const onApprove = async (job: ContentVideoJob) => {
    try {
      await approveContentVideoJob(job.id);
      message.success('Đã duyệt');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không duyệt được'));
    }
  };

  const watchUrl = watch ? playableMarketingVideoUrl(watch.outputUrl) : '';

  return (
    <Card
      size="small"
      style={{ marginBottom: 12 }}
      title="Video thành phẩm đã nhập"
      extra={
        <Button size="small" icon={<ReloadOutlined />} onClick={() => void load()}>
          Tải lại
        </Button>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        Nhập file MP4 đã dựng ngoài hệ thống (H.264, đúng khung của template, có tiếng). Máy chủ kiểm giải mã trước khi đặt
        «Sẵn sàng». Không gọi nhà cung cấp ngoài, không tạo video mới.
      </Typography.Paragraph>
      <Space wrap style={{ marginBottom: 12 }}>
        {brands ? (
          <Select
            placeholder="Thương hiệu"
            style={{ width: 200 }}
            value={targetBrand}
            onChange={setTargetBrand}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
          />
        ) : null}
        <Input
          placeholder="Tiêu đề (mặc định theo tên file)"
          style={{ width: 280 }}
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
        <input
          ref={fileRef}
          type="file"
          accept="video/mp4,.mp4"
          style={{ display: 'none' }}
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
        />
        <Button icon={<UploadOutlined />} onClick={() => fileRef.current?.click()}>
          {file ? file.name : 'Chọn file MP4'}
        </Button>
        <Button type="primary" loading={busy} disabled={!file || !targetBrand} onClick={() => void onImport()}>
          Nhập video
        </Button>
      </Space>
      <Table
        size="small"
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={{ pageSize: 8, hideOnSinglePage: true }}
        locale={{ emptyText: 'Chưa có video nào được nhập.' }}
        columns={[
          { title: 'Thương hiệu', dataIndex: 'brandName', width: 130 },
          {
            title: 'Tiêu đề',
            dataIndex: 'title',
            render: (t: string, row: ContentVideoJob) => (
              <div>
                <div>{t}</div>
                {readMeta(row).sourceFileName ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {readMeta(row).sourceFileName}
                  </Typography.Text>
                ) : null}
                {row.status === 'Failed' && row.errorMessage ? (
                  <div>
                    <Typography.Text type="danger" style={{ fontSize: 12 }}>
                      {row.errorMessage}
                    </Typography.Text>
                  </div>
                ) : null}
              </div>
            ),
          },
          {
            title: 'Thời lượng',
            width: 100,
            render: (_: unknown, row: ContentVideoJob) =>
              readMeta(row).durationSeconds ? `${Math.round(readMeta(row).durationSeconds as number)} giây` : '—',
          },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            width: 110,
            render: (_: string, row: ContentVideoJob) => {
              const tag = factoryVideoTag(row);
              return <Tag color={tag.color}>{tag.text}</Tag>;
            },
          },
          {
            title: '',
            width: 190,
            render: (_: unknown, row: ContentVideoJob) => (
              <Space size={4}>
                <Button
                  size="small"
                  icon={<PlayCircleOutlined />}
                  disabled={!playableMarketingVideoUrl(row.outputUrl)}
                  onClick={() => setWatch(row)}
                >
                  Xem
                </Button>
                <Button
                  size="small"
                  icon={<CheckOutlined />}
                  disabled={row.status !== 'Ready'}
                  onClick={() => void onApprove(row)}
                >
                  Duyệt
                </Button>
              </Space>
            ),
          },
        ]}
      />
      <Modal open={!!watch} title={watch?.title} footer={null} width={420} onCancel={() => setWatch(null)} destroyOnClose>
        {watchUrl ? <video src={watchUrl} controls autoPlay style={{ width: '100%', maxHeight: '70vh' }} /> : null}
      </Modal>
    </Card>
  );
}
