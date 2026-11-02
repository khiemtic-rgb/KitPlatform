import { useState } from 'react';
import { Button, InputNumber, Space, Typography } from 'antd';
import {
  DIRECTOR_PRODUCTION_TIMING_OPTIONS,
  DIRECTOR_PRODUCTION_TIMING_UNDECIDED,
  explicitDirectorSpeechInterval,
  formatDirectorProductionDuration,
  isAuthoritativeDirectorTiming,
  type DirectorProductionDurationSec,
  type DirectorProductionTiming,
  type DirectorSpeechInterval,
} from './famixa-director-timing';

export function FamixaDirectorTimingPicker({
  timing,
  busy,
  onConfirm,
}: {
  timing?: DirectorProductionTiming | null;
  busy?: boolean;
  onConfirm: (durationSec: DirectorProductionDurationSec, speech?: DirectorSpeechInterval | null) => void;
}) {
  const decided = isAuthoritativeDirectorTiming(timing);
  const currentSec = decided ? Number(timing?.durationSec) : undefined;
  const status = formatDirectorProductionDuration(timing);
  const saved = explicitDirectorSpeechInterval(timing);
  const stamp = `${timing?.performanceSnapshotId ?? ''}:${timing?.durationSec ?? ''}:${saved?.speechStartSec ?? ''}:${saved?.speechEndSec ?? ''}`;
  const [seen, setSeen] = useState(stamp);
  const [start, setStart] = useState<number | null>(saved?.speechStartSec ?? null);
  const [end, setEnd] = useState<number | null>(saved?.speechEndSec ?? null);
  if (seen !== stamp) {
    setSeen(stamp);
    setStart(saved?.speechStartSec ?? null);
    setEnd(saved?.speechEndSec ?? null);
  }
  const typed = explicitDirectorSpeechInterval({
    timingSource: 'DIRECTOR',
    speechStartSec: start,
    speechEndSec: end,
  });
  const partial = (start != null || end != null) && !typed;
  const picture = currentSec === 5 || currentSec === 10 ? currentSec : null;

  return (
    <div data-director-production-duration="1">
      <Space style={{ marginBottom: 4 }} wrap>
        <Typography.Text type="secondary">Production duration</Typography.Text>
        <Typography.Text data-director-timing-status="1">{status}</Typography.Text>
      </Space>
      <Space wrap>
        {DIRECTOR_PRODUCTION_TIMING_OPTIONS.map((sec) => (
          <Button
            key={sec}
            size="small"
            type={currentSec === sec ? 'primary' : 'default'}
            loading={busy}
            disabled={busy}
            data-director-timing-option={sec}
            onClick={() => {
              if (partial) return;
              onConfirm(sec, typed);
            }}
          >
            {sec === 5 ? '5 giây' : '10 giây'}
          </Button>
        ))}
      </Space>
      <Space style={{ marginTop: 8 }} wrap>
        <Typography.Text type="secondary">Khoảng thoại</Typography.Text>
        <InputNumber
          size="small"
          min={0}
          step={0.1}
          value={start}
          placeholder="Bắt đầu"
          disabled={busy}
          data-director-speech-start="1"
          onChange={(value) => setStart(typeof value === 'number' ? value : null)}
        />
        <InputNumber
          size="small"
          min={0}
          step={0.1}
          value={end}
          placeholder="Kết thúc"
          disabled={busy}
          data-director-speech-end="1"
          onChange={(value) => setEnd(typeof value === 'number' ? value : null)}
        />
        <Button
          size="small"
          loading={busy}
          disabled={busy || picture == null || partial}
          data-director-speech-save="1"
          onClick={() => {
            if (picture == null || partial) return;
            onConfirm(picture, typed);
          }}
        >
          Lưu khoảng thoại
        </Button>
      </Space>
      {!decided ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
          {DIRECTOR_PRODUCTION_TIMING_UNDECIDED}. Chọn để xác nhận trên Production OS.
        </Typography.Text>
      ) : null}
      {partial ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4 }} data-director-speech-partial="1">
          Khoảng thoại cần cả thời điểm bắt đầu và kết thúc. 5 giây và 10 giây không tạo khoảng thoại.
        </Typography.Text>
      ) : (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
          Nhập cả hai mốc. 5 giây và 10 giây chỉ là độ dài hình.
        </Typography.Text>
      )}
    </div>
  );
}
