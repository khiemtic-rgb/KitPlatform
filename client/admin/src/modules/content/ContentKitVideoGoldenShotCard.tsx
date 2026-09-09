import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import {
  decideKitVideoMotion,
  fetchKitVideoI2vReady,
  fetchKitVideoMotion,
  fetchKitVideoMotionVideoBlob,
  fetchLatestKitVideoMotion,
  kitVideoMotionVideoUrl,
  pollKitVideoMotion,
  preflightKitVideoMotion,
  submitKitVideoMotion,
  type KitVideoMotionTakeRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  canPollMotionTake,
  canSubmitMotionTake,
  famixaGoldenMotion,
  formatMotionBoard,
  isLiveMotionTake,
} from './kit-video-motion';

const ATTEMPT_01 = '6036d11b-35de-43f1-88e4-f0138365016d';
const TAKE_01 = '7505cd20-94dd-4e29-8bd5-aab2fb24fea8';
const GOLDEN_KEY = 'golden-sh01-01-attempt-01';
const TAKE_STORE = 'kit-video-golden-take-id';

function rememberTake(row?: KitVideoMotionTakeRow) {
  if (isLiveMotionTake(row) && row?.takeId) sessionStorage.setItem(TAKE_STORE, row.takeId);
}

export function ContentKitVideoGoldenShotCard() {
  const motion = useMemo(() => famixaGoldenMotion(), []);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [i2v, setI2v] = useState<{ ready: boolean; imageType?: string; sourceArtifactHash?: string; blocked: string[] }>();
  const [take, setTake] = useState<KitVideoMotionTakeRow>();
  const [approved, setApproved] = useState<KitVideoMotionTakeRow>();
  const [videoUrl, setVideoUrl] = useState<string>();
  const [videoError, setVideoError] = useState<string>();

  const keepTake = (row: KitVideoMotionTakeRow) => {
    setTake(row);
    rememberTake(row);
  };

  useEffect(() => {
    setBusy(true);
    void fetchKitVideoI2vReady(ATTEMPT_01)
      .then((row) => {
        setI2v({ ready: row.ready, imageType: row.imageType, sourceArtifactHash: row.sourceArtifactHash, blocked: row.blocked });
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được I2V package.')));
    void fetchLatestKitVideoMotion(ATTEMPT_01)
      .then((row) => {
        setTake(row);
        rememberTake(row);
        setError(undefined);
      })
      .catch(() => {
        const saved = sessionStorage.getItem(TAKE_STORE);
        if (!saved) return;
        return fetchKitVideoMotion(saved).then((row) => {
          setTake(row);
          rememberTake(row);
        });
      })
      .finally(() => setBusy(false));
  }, []);

  useEffect(() => {
    let dead = false;
    let objectUrl: string | undefined;
    void fetchKitVideoMotion(TAKE_01)
      .then((row) => {
        if (!dead) setApproved(row);
      })
      .catch(() => undefined);
    void fetchKitVideoMotionVideoBlob(TAKE_01)
      .then((blob) => {
        const next = URL.createObjectURL(blob);
        if (dead) {
          URL.revokeObjectURL(next);
          return;
        }
        objectUrl = next;
        setVideoUrl(next);
        setVideoError(undefined);
      })
      .catch((e) => {
        if (!dead) setVideoError(apiErrorMessage(e, 'Không tải được take-01.mp4.'));
      });
    return () => {
      dead = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, []);

  const refreshI2v = () => {
    setBusy(true);
    void fetchKitVideoI2vReady(ATTEMPT_01)
      .then((row) => {
        setI2v({ ready: row.ready, imageType: row.imageType, sourceArtifactHash: row.sourceArtifactHash, blocked: row.blocked });
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được I2V package.')))
      .finally(() => setBusy(false));
  };

  const preflight = () => {
    setBusy(true);
    void preflightKitVideoMotion({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: motion,
      confirmed: false,
      idempotencyKey: GOLDEN_KEY,
    })
      .then((row) => {
        setTake((cur) => {
          if (isLiveMotionTake(cur) && cur) {
            return { ...cur, preflight: row.preflight };
          }
          rememberTake(row);
          return row;
        });
        setError(row.status === 'BLOCKED' ? row.diagnose : undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Preflight chặn — 0 credit.')))
      .finally(() => setBusy(false));
  };

  const run = () => {
    if (!canSubmitMotionTake(take)) return;
    setBusy(true);
    void submitKitVideoMotion({
      keyframeAttemptId: ATTEMPT_01,
      motionContract: motion,
      confirmed: true,
      idempotencyKey: GOLDEN_KEY,
    })
      .then((row) => {
        keepTake(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không gọi Runway.')))
      .finally(() => setBusy(false));
  };

  const poll = () => {
    if (!canPollMotionTake(take) || !take?.takeId) return;
    setBusy(true);
    void pollKitVideoMotion(take.takeId)
      .then((row) => {
        keepTake(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Poll thất bại — take vẫn giữ trên card.')))
      .finally(() => setBusy(false));
  };

  const decide = (decision: 'APPROVE' | 'REJECT') => {
    if (!take?.takeId) return;
    setBusy(true);
    void decideKitVideoMotion(take.takeId, decision)
      .then((row) => {
        keepTake(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Director decision thất bại.')))
      .finally(() => setBusy(false));
  };

  const live = isLiveMotionTake(take);

  return (
    <Card className="fx-look__card" size="small" title="Bản thử máy quay — không phải mặt Minh chính thức">
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 8 }}
        message="Đây là bản thử kỹ thuật SH01-01 / take-01."
        description="Dùng để kiểm tra máy quay. Không lấy khuôn mặt trong clip này để khóa Minh. Không vẽ lại ảnh gốc."
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        Chỉ Famixa · ảnh Attempt 01 giữ nguyên · máy nhận việc chưa chắc đã có video
      </Typography.Paragraph>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <Space wrap>
        <Tag color={i2v?.ready ? 'green' : undefined}>Keyframe {i2v?.ready ? '✓ APPROVED' : '· check I2V'}</Tag>
        <Tag color={i2v?.sourceArtifactHash ? 'green' : undefined}>SHA256 {i2v?.sourceArtifactHash ? '✓ MATCH' : '·'}</Tag>
        <Tag color={take?.prompt && live ? 'green' : undefined}>Motion {take?.prompt && live ? '✓ READY' : '·'}</Tag>
        <Tag color={take?.preflight?.ok ? 'green' : take?.status === 'BLOCKED' ? 'red' : undefined}>
          Preflight {take?.preflight?.ok ? '✓ PASS' : take?.status === 'BLOCKED' ? 'BLOCKED' : '·'}
        </Tag>
        <Tag>{live ? take?.creditState || 'NONE' : 'NONE'} credit</Tag>
      </Space>
      <Space wrap style={{ marginTop: 8 }}>
        <Button size="small" loading={busy} onClick={refreshI2v}>
          Check I2V
        </Button>
        <Button size="small" loading={busy} onClick={preflight}>
          Preflight (0 cr)
        </Button>
        <Button size="small" type="primary" loading={busy} disabled={!canSubmitMotionTake(take)} onClick={run}>
          RUNWAY TEST
        </Button>
        <Button size="small" type={canPollMotionTake(take) ? 'primary' : 'default'} loading={busy} disabled={!canPollMotionTake(take)} onClick={poll}>
          Poll
        </Button>
        <Button size="small" disabled={take?.status !== 'READY_FOR_DIRECTOR'} onClick={() => decide('APPROVE')}>
          Approve
        </Button>
        <Button size="small" disabled={take?.status !== 'READY_FOR_DIRECTOR'} onClick={() => decide('REJECT')}>
          Reject
        </Button>
      </Space>
      <pre style={{ marginTop: 8, marginBottom: 0, fontSize: 12, lineHeight: 1.45, whiteSpace: 'pre-wrap' }}>
        {formatMotionBoard({
          shotCode: 'SH01-01',
          status: take?.status || 'READY',
          prompt: take?.prompt || motion.motion,
          credit: take?.actualCredit && take.actualCredit !== 'UNKNOWN' ? take.actualCredit : take?.creditState || 'NONE',
          videoReady: take?.videoReady === true,
          qa: take?.qa,
          diagnose: take?.diagnose,
          takeId: live ? take?.takeId : undefined,
          idle: !live,
        })}
      </pre>
      {videoError ? <Alert type="warning" showIcon message={videoError} style={{ marginTop: 8 }} /> : null}
      {videoUrl ? (
        <div style={{ marginTop: 8 }}>
          <Space wrap>
            <Typography.Link href={videoUrl} target="_blank" rel="noreferrer">
              Xem take-01.mp4
            </Typography.Link>
            <Typography.Link href={videoUrl} download="take-01.mp4">
              Tải take-01.mp4
            </Typography.Link>
            <Typography.Text type="secondary">
              {approved?.status || 'APPROVED_TAKE'} · {kitVideoMotionVideoUrl(TAKE_01)}
            </Typography.Text>
          </Space>
          <video
            src={videoUrl}
            controls
            playsInline
            style={{ display: 'block', width: '100%', maxWidth: 720, marginTop: 8, background: '#111' }}
          />
        </div>
      ) : null}
    </Card>
  );
}
