import { useEffect, useState } from 'react';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import {
  executeVideoGenerationExecution,
  fetchKitVideoProductionShots,
  fetchVideoGenerationExecution,
  preflightVideoGenerationExecution,
  videoGenerationExecutionArtifactUrl,
  type VideoGenerationExecutionRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'COMPILED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED' || s === 'IMAGE_APPROVED' || s === 'READY_FOR_DIRECTOR' || s === 'SUCCEEDED')
    return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'FAILED' || s === 'QA_FAILED' || s === 'MISSING' || s === 'NOT_READY' || s === 'REJECTED')
    return 'red';
  return 'orange';
};

const sha = (v?: string) => (v ? `${v.slice(0, 12)}…${v.slice(-8)}` : '—');

export function ContentKitVideoVideoGenerationExecutionCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<VideoGenerationExecutionRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchVideoGenerationExecution(target)
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Video Generation Execution.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    void fetchKitVideoProductionShots()
      .then((bundle) => {
        const list = (bundle.shots || []).map((s) => ({ id: s.id, shotCode: s.shotCode }));
        setShots(list);
        const first = list[0]?.id;
        if (first) {
          setShotId(first);
          load(first);
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Shot.')));
  }, []);

  const run = (fn: () => Promise<VideoGenerationExecutionRow>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Video Generation Execution bị BLOCK.')))
      .finally(() => setBusy(false));
  };

  const canExecute = !!shotId && !!row?.canExecute && !!row?.preflightPass && !row?.id;
  const video = shotId && row?.id && row.artifactSha256 ? videoGenerationExecutionArtifactUrl(shotId, row.id) : '';
  const ready = (row?.status || '').toUpperCase() === 'READY_FOR_DIRECTOR';

  return (
    <Card
      id="production-video-generation-execution"
      className="fx-look__card"
      size="small"
      title="VIDEO GENERATION EXECUTION"
      extra={
        <Space>
          <Button size="small" disabled={!shotId} loading={busy} onClick={() => run(() => preflightVideoGenerationExecution(shotId!))}>
            PRE-FLIGHT
          </Button>
          <Button
            size="small"
            type="primary"
            disabled={!canExecute}
            loading={busy}
            onClick={() => run(() => executeVideoGenerationExecution(shotId!))}
          >
            EXECUTE VIDEO
          </Button>
        </Space>
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {ready ? (
        <div className="fx-dir-review-badge" style={{ cursor: 'default' }}>
          READY FOR DIRECTOR
        </div>
      ) : null}
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
        <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
        <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
        <Tag color={gateColor(row?.governanceEngine)}>IDENTITY GOVERNANCE [{row?.governanceEngine || '—'}]</Tag>
        <Tag color={gateColor(row?.shotContract)}>SHOT CONTRACT [{row?.shotContract || '—'}]</Tag>
        <Tag color={gateColor(row?.prompt)}>PROMPT [{row?.prompt || '—'}]</Tag>
        <Tag color={gateColor(row?.imageGenerationContract)}>IMAGE CONTRACT [{row?.imageGenerationContract || '—'}]</Tag>
        <Tag color={gateColor(row?.imageDirectorApproval)}>IMAGE DIRECTOR [{row?.imageDirectorApproval || '—'}]</Tag>
        <Tag color={gateColor(row?.videoContract)}>VIDEO CONTRACT [{row?.videoContract || '—'}]</Tag>
        <Tag color={gateColor(row?.directorApproval)}>DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
      </Space>
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
        Shot:{' '}
        <select
          value={shotId || ''}
          onChange={(e) => {
            setShotId(e.target.value);
            load(e.target.value);
          }}
        >
          {shots.map((s) => (
            <option key={s.id} value={s.id}>
              {s.shotCode}
            </option>
          ))}
        </select>{' '}
        · {row?.status || 'PREFLIGHT'} · generation={String(row?.generation ?? false)}
      </Typography.Text>
      <div style={{ fontSize: 12 }}>
        <div>Preflight: {row?.preflightPass ? 'PASS' : 'BLOCKED'}</div>
        <div>Execution: {row?.status || '—'}</div>
        <div>Fingerprint: {row?.executionFingerprint || '—'}</div>
        <div>Provider: {row?.provider || '—'} · request {row?.providerRequestId || '—'}</div>
        <div>Still SHA {sha(row?.stillArtifactSha256)}</div>
        <div>Video Contract SHA {sha(row?.videoContractSha256)}</div>
        <div>Artifact SHA {sha(row?.artifactSha256)}</div>
        <div>
          Technical QA [{row?.qa?.technical || '—'}] Identity [{row?.qa?.identity || '—'}] Continuity [{row?.qa?.continuity || '—'}]
        </div>
      </div>
      {(row?.blocks || []).length ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 8 }}
          message={row?.blocks[0]?.code || 'VIDEO_GENERATION_NOT_READY'}
          description={row?.blocks.map((b) => b.message).join(' ')}
        />
      ) : null}
      {video ? (
        <video src={video} controls style={{ maxWidth: '100%', marginTop: 8, border: '1px solid #d9d9d9' }}>
          Video artifact
        </video>
      ) : null}
    </Card>
  );
}
