import { useState } from 'react';
import { Alert, App, Button, Checkbox, Modal, Space, Table, Tag, Typography } from 'antd';
import {
  cleanNovixaStorage,
  fetchContentBrands,
  fetchContentSeriesBuild,
  fetchContentTopicDetail,
  fetchNovixaStorage,
  fetchSeriesBuildsForBrand,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

type Row = {
  key: string;
  brand: string;
  title: string;
  total: number;
  cleanable: number;
  state: 'ready' | 'waiting' | 'unscheduled' | 'failed' | 'nothing';
  note: string;
};

function size(bytes: number) {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
  if (bytes >= 1024 ** 2) return `${Math.round(bytes / 1024 ** 2)} MB`;
  return `${Math.max(0, Math.round(bytes / 1024))} KB`;
}

/**
 * Dọn video đã dựng của mọi thương hiệu. Chỉ dọn phim mà mọi bài xuất ở bước 5 đều đã đăng xong
 * (mọi lượt đăng "Succeeded"). Giữ giọng, mốc thời gian và ảnh để dựng lại không tốn tiền giọng.
 */
export function ContentStorageCleanup() {
  const { message } = App.useApp();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [picked, setPicked] = useState<Record<string, boolean>>({});
  const [scanned, setScanned] = useState(false);

  const scan = async () => {
    setBusy('scan');
    setError('');
    setScanned(false);
    try {
      const brands = await fetchContentBrands();
      const out: Row[] = [];
      const topicDone = new Map<string, 'done' | 'waiting' | 'failed'>();
      for (const brand of brands) {
        const builds = await fetchSeriesBuildsForBrand(brand.code, brand.id);
        for (const summary of builds) {
          const storage = await fetchNovixaStorage(summary.id).catch(() => null);
          if (!storage || storage.cleanableBytes <= 0) continue;
          const full = await fetchContentSeriesBuild(summary.id);
          const film = (full.graph as { filmOrchestration?: { exports?: Record<string, { topicId?: string }> } }).filmOrchestration;
          const topicIds = [...new Set(Object.values(film?.exports ?? {}).map((item) => item.topicId).filter((id): id is string => Boolean(id)))];
          let state: Row['state'] = 'ready';
          let note = `${topicIds.length} bài đã đăng xong`;
          if (!topicIds.length) {
            state = 'unscheduled';
            note = 'Chưa đưa vào lịch đăng';
          } else {
            let waiting = 0;
            let failed = 0;
            for (const id of topicIds) {
              let status = topicDone.get(id);
              if (!status) {
                const detail = await fetchContentTopicDetail(id).catch(() => null);
                const jobs = detail?.jobs ?? [];
                status = !jobs.length ? 'waiting' : jobs.some((job) => job.status === 'Failed') ? 'failed' : jobs.every((job) => job.status === 'Succeeded') ? 'done' : 'waiting';
                topicDone.set(id, status);
              }
              if (status === 'waiting') waiting += 1;
              if (status === 'failed') failed += 1;
            }
            if (failed) {
              state = 'failed';
              note = `${failed}/${topicIds.length} bài đăng lỗi`;
            } else if (waiting) {
              state = 'waiting';
              note = `Còn ${waiting}/${topicIds.length} bài chưa đăng xong`;
            }
          }
          out.push({ key: summary.id, brand: brand.name, title: `${summary.episodeCode} · ${summary.title}`, total: storage.totalBytes, cleanable: storage.cleanableBytes, state, note });
        }
      }
      setRows(out);
      setPicked(Object.fromEntries(out.filter((row) => row.state === 'ready').map((row) => [row.key, true])));
      setScanned(true);
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không đọc được dung lượng. Kiểm tra máy chủ rồi thử lại.'));
    } finally {
      setBusy('');
    }
  };

  const chosen = rows.filter((row) => row.state === 'ready' && picked[row.key]);
  const freeable = chosen.reduce((sum, row) => sum + row.cleanable, 0);

  const clean = async () => {
    setBusy('clean');
    setError('');
    let freed = 0;
    try {
      for (const row of chosen) {
        const after = await cleanNovixaStorage(row.key);
        freed += row.total - after.totalBytes;
      }
      message.success(`Đã giải phóng khoảng ${size(freed)}.`);
      await scan();
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không dọn hết được. Quét lại để xem phần còn lại.'));
    } finally {
      setBusy('');
    }
  };

  return (
    <>
      <Button
        onClick={() => {
          setOpen(true);
          void scan();
        }}
      >
        Dọn dung lượng
      </Button>
      <Modal
        title="Dọn dung lượng video"
        open={open}
        width={900}
        onCancel={() => !busy && setOpen(false)}
        footer={[
          <Button key="close" disabled={Boolean(busy)} onClick={() => setOpen(false)}>Đóng</Button>,
          <Button key="rescan" disabled={Boolean(busy)} loading={busy === 'scan'} onClick={() => void scan()}>Quét lại</Button>,
          <Button key="clean" type="primary" danger disabled={!chosen.length || Boolean(busy)} loading={busy === 'clean'} onClick={() => void clean()}>
            Dọn {chosen.length} phim{freeable ? ` (${size(freeable)})` : ''}
          </Button>,
        ]}
      >
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          <Typography.Text type="secondary">
            Quét mọi thương hiệu. Chỉ phim mà mọi bài đã xuất ở bước 5 đều đăng xong mới được chọn sẵn. Chỉ xóa video đã dựng (bản dài, bản ngang, bản dọc, video ngắn) và file voice.wav.
            Giọng, mốc thời gian, ảnh và kịch bản được giữ, nên dựng lại không tốn thêm tiền giọng.
          </Typography.Text>
          <Alert
            type="warning"
            showIcon
            message="TikTok vào hộp nháp được tính là đã đăng. Chỉ dọn sau khi bạn đã đăng các video đó từ hộp nháp."
          />
          {error ? <Alert type="error" showIcon message={error} /> : null}
          <Table<Row>
            size="small"
            loading={busy === 'scan'}
            pagination={false}
            rowKey="key"
            dataSource={rows}
            locale={{ emptyText: scanned ? 'Không có phim nào có video để dọn.' : 'Đang quét…' }}
            columns={[
              {
                title: '',
                width: 40,
                render: (_, row) => (
                  <Checkbox
                    disabled={row.state !== 'ready' || Boolean(busy)}
                    checked={Boolean(picked[row.key]) && row.state === 'ready'}
                    onChange={(event) => setPicked((cur) => ({ ...cur, [row.key]: event.target.checked }))}
                  />
                ),
              },
              { title: 'Thương hiệu', dataIndex: 'brand', width: 150 },
              { title: 'Phim', dataIndex: 'title' },
              { title: 'Tổng', width: 90, render: (_, row) => size(row.total) },
              { title: 'Dọn được', width: 100, render: (_, row) => size(row.cleanable) },
              {
                title: 'Trạng thái',
                width: 240,
                render: (_, row) => <Tag color={row.state === 'ready' ? 'green' : row.state === 'failed' ? 'red' : 'default'}>{row.note}</Tag>,
              },
            ]}
          />
        </Space>
      </Modal>
    </>
  );
}
