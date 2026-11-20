import { useEffect, useState } from 'react';
import { Alert, Button, Card, Select, Space, Typography } from 'antd';
import {
  fetchPerformanceCatalog,
  renderPerformanceFilm,
  type PerformanceCatalog,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

/** forBrand: show only when that brand has a performance timeline (a character rig). */
export function StudioPerformancePanel({ forBrand }: { forBrand?: string | null } = {}) {
  const [catalog, setCatalog] = useState<PerformanceCatalog | null>(null);
  const [brandCode, setBrandCode] = useState('novixa');
  const [clipId, setClipId] = useState('act-explain');
  const [duration, setDuration] = useState(2);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [videoUrl, setVideoUrl] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void fetchPerformanceCatalog()
      .then((row) => {
        if (cancelled) return;
        setCatalog(row);
        setBrandCode(row.timelines[0]?.brandCode || 'novixa');
        setClipId(row.clips[0]?.id || 'act-explain');
      })
      .catch((reason) => {
        if (!cancelled) setError(apiErrorMessage(reason, 'Không đọc được danh mục diễn hoạt.'));
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => () => {
    if (videoUrl) URL.revokeObjectURL(videoUrl);
  }, [videoUrl]);

  const timeline = catalog?.timelines.find((row) => row.brandCode === brandCode);
  const scoped = (forBrand || '').trim().toLowerCase();
  if (scoped && (!catalog || !catalog.timelines.some((row) => row.brandCode.toLowerCase() === scoped))) return null;

  const renderFilm = async () => {
    setBusy(true);
    setError(null);
    try {
      const blob = await renderPerformanceFilm({ brandCode, clipId, duration });
      const typed = blob.type.startsWith('video/') ? blob : new Blob([await blob.arrayBuffer()], { type: 'video/mp4' });
      const url = URL.createObjectURL(typed);
      setVideoUrl((prev) => {
        if (prev) URL.revokeObjectURL(prev);
        return url;
      });
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không render được phim.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card size="small" title="Diễn hoạt">
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Typography.Text>
          Nhân vật {catalog?.character || 'lan-stand.jpg'} trên nền {timeline?.plate || 'plate-pharmacy.jpg'}. Engine {catalog?.engineId || 'kit-rig-v1'}. Không gọi AI.
        </Typography.Text>
        <Space wrap>
          <Select
            style={{ minWidth: 220 }}
            value={brandCode}
            options={(catalog?.timelines || []).map((row) => ({ value: row.brandCode, label: row.label }))}
            onChange={setBrandCode}
          />
          <Select
            style={{ minWidth: 160 }}
            value={clipId}
            options={(catalog?.clips || []).map((row) => ({ value: row.id, label: row.label }))}
            onChange={(value) => {
              setClipId(value);
              if (value === 'film-novixa-short') setDuration(15);
            }}
          />
          <Select
            style={{ minWidth: 140 }}
            value={duration}
            options={[
              { value: 1, label: '1 giây' },
              { value: 2, label: '2 giây' },
              { value: 15, label: '15 giây' },
            ]}
            onChange={setDuration}
          />
          <Button type="primary" loading={busy} onClick={() => void renderFilm()}>
            Render
          </Button>
        </Space>
        {catalog?.limits ? <Typography.Text type="secondary">{catalog.limits}</Typography.Text> : null}
        {error ? <Alert type="error" showIcon message={error} /> : null}
        {videoUrl ? <video src={videoUrl} controls autoPlay style={{ width: '100%', maxWidth: 720, background: '#111' }} /> : null}
      </Space>
    </Card>
  );
}
