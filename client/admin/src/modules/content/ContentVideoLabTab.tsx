import { useMemo, useState } from 'react';
import { Alert, Button, Card, Select, Space, Table, Tag, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { brandColor } from './content-brand';
import {
  VIDEO_LAB_PHASE1,
  VIDEO_SHOT_SOURCE_LABEL,
  countLabSources,
  flattenLabShots,
  labShotsToCsv,
  type VideoLabItem,
  type VideoShotSource,
} from './content-video-lab';

const SOURCE_COLOR: Record<VideoShotSource, string> = {
  live: 'red',
  ai: 'purple',
  screen: 'blue',
  asset: 'default',
};

const CREW_LABEL: Record<VideoLabItem['crew'], { text: string; color: string }> = {
  screen: { text: 'Không đoàn — screen', color: 'blue' },
  'half-day': { text: 'Nửa ngày quay', color: 'gold' },
  'full-day': { text: 'Cả ngày + đoàn', color: 'orange' },
};

function downloadCsv(videos: VideoLabItem[]) {
  const blob = new Blob(['\uFEFF' + labShotsToCsv(videos)], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = videos.length === 1 ? `video-lab-${videos[0]!.code}.csv` : 'video-lab-phase1.csv';
  a.click();
  URL.revokeObjectURL(url);
}

export function ContentVideoLabTab() {
  const [videoCode, setVideoCode] = useState<string | undefined>();
  const [sourceFilter, setSourceFilter] = useState<VideoShotSource | undefined>();

  const videos = useMemo(
    () => (videoCode ? VIDEO_LAB_PHASE1.filter((v) => v.code === videoCode) : VIDEO_LAB_PHASE1),
    [videoCode],
  );

  const rows = useMemo(() => {
    const all = flattenLabShots(videos);
    if (!sourceFilter) return all;
    return all.filter((r) => r.sources.includes(sourceFilter));
  }, [videos, sourceFilter]);

  const totals = useMemo(() => {
    const shots = VIDEO_LAB_PHASE1.flatMap((v) => v.shots);
    return { videos: VIDEO_LAB_PHASE1.length, shots: shots.length, ...countLabSources(shots) };
  }, []);

  return (
    <div>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Phase 1 = phòng thí nghiệm 6 công thức (+ vision factory). Không phải 18 quảng cáo."
        description="Làm trước P1-03 / P1-02 (screen). Quay P1-04 + P1-05. Thuê đoàn P1-10 + P1-11 khi đã gửi CSV. Không auto-post TikTok. Xuân Hòa / NT_XUANHOA không nằm bảng này."
      />

      <div className="content-video-lab-stats">
        <span>
          <strong>{totals.videos}</strong> video
        </span>
        <span>
          <strong>{totals.shots}</strong> shot
        </span>
        <span>
          <Tag color="red">QUAY {totals.live}</Tag>
          <Tag color="blue">SCREEN {totals.screen}</Tag>
          <Tag color="purple">AI {totals.ai}</Tag>
          <Tag>ASSET {totals.asset}</Tag>
        </span>
      </div>

      <div className="content-video-lab-cards">
        {VIDEO_LAB_PHASE1.map((v) => {
          const n = countLabSources(v.shots);
          const active = !videoCode || videoCode === v.code;
          const crew = CREW_LABEL[v.crew];
          return (
            <button
              key={v.code}
              type="button"
              className={`content-video-lab-card${videoCode === v.code ? ' content-video-lab-card--on' : ''}${active ? '' : ' content-video-lab-card--dim'}`}
              onClick={() => setVideoCode((cur) => (cur === v.code ? undefined : v.code))}
            >
              <div className="content-video-lab-card__head">
                <span className="content-brand-dot" style={{ background: brandColor(v.brandCode) }} />
                <strong>{v.code}</strong>
                <Tag color={crew.color}>{crew.text}</Tag>
              </div>
              <div className="content-video-lab-card__title">{v.title}</div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {v.brandName} · {v.formula} · {v.aspect} · {v.durationSec}s · {v.shots.length} shot
              </Typography.Text>
              <div style={{ marginTop: 8 }}>
                {n.live > 0 ? <Tag color="red">QUAY {n.live}</Tag> : null}
                {n.screen > 0 ? <Tag color="blue">SCREEN {n.screen}</Tag> : null}
                {n.ai > 0 ? <Tag color="purple">AI {n.ai}</Tag> : null}
                {n.asset > 0 ? <Tag>ASSET {n.asset}</Tag> : null}
              </div>
              <Typography.Paragraph type="secondary" ellipsis={{ rows: 2 }} style={{ margin: '8px 0 0', fontSize: 12 }}>
                {v.purpose}
              </Typography.Paragraph>
            </button>
          );
        })}
      </div>

      <Card
        size="small"
        title={videoCode ? `Bảng shot · ${videoCode}` : 'Bảng shot Phase 1'}
        extra={
          <Space wrap>
            <Select
              allowClear
              placeholder="Lọc nguồn"
              style={{ width: 140 }}
              value={sourceFilter}
              onChange={setSourceFilter}
              options={(Object.keys(VIDEO_SHOT_SOURCE_LABEL) as VideoShotSource[]).map((k) => ({
                value: k,
                label: VIDEO_SHOT_SOURCE_LABEL[k],
              }))}
            />
            <Button icon={<DownloadOutlined />} onClick={() => downloadCsv(videos)}>
              CSV {videoCode ?? 'cả Phase 1'}
            </Button>
          </Space>
        }
      >
        <Table
          size="small"
          rowKey="id"
          pagination={false}
          scroll={{ x: 1280 }}
          dataSource={rows}
          columns={[
            { title: 'Video', dataIndex: 'videoCode', width: 80, fixed: 'left' },
            { title: 'Scene', dataIndex: 'scene', width: 110 },
            { title: 'Shot', dataIndex: 'shot', width: 130 },
            { title: 'Clock', dataIndex: 'clock', width: 80 },
            { title: 'Nội dung', dataIndex: 'content', width: 320 },
            {
              title: 'Nguồn',
              dataIndex: 'sources',
              width: 160,
              render: (srcs: VideoShotSource[]) =>
                srcs.map((src) => (
                  <Tag key={src} color={SOURCE_COLOR[src]}>
                    {VIDEO_SHOT_SOURCE_LABEL[src]}
                  </Tag>
                )),
            },
            { title: 'Địa điểm', dataIndex: 'location', width: 140 },
            { title: 'Diễn viên', dataIndex: 'talent', width: 140 },
            { title: 'Thiết bị', dataIndex: 'gear', width: 160 },
            { title: 'Reuse', dataIndex: 'reuse', width: 140 },
          ]}
        />
      </Card>
    </div>
  );
}
