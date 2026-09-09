import { useEffect, useState } from 'react';
import { Alert, Button, Card, Input, Space, Tag, Typography } from 'antd';
import {
  approveImageGenerationContract,
  fetchImageGenerationContract,
  fetchKitVideoProductionShots,
  rejectImageGenerationContract,
  saveImageGenerationContract,
  validateImageGenerationContract,
  type ImageGenerationContractRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'COMPILED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED') return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'MISSING' || s === 'NOT_READY') return 'red';
  return 'orange';
};

export function ContentKitVideoImageGenerationContractCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ImageGenerationContractRow>();
  const [quality, setQuality] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchImageGenerationContract(target)
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Image Generation Contract.')))
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

  const overlay = () => (quality.trim() ? { qualityPolicy: quality.trim() } : {});
  const run = (fn: () => Promise<ImageGenerationContractRow>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Image Generation Contract bị BLOCK.')))
      .finally(() => setBusy(false));
  };

  return (
    <Card
      size="small"
      title="IMAGE GENERATION CONTRACT"
      extra={
        <Space>
          <Button size="small" disabled={!shotId} loading={busy} onClick={() => run(() => saveImageGenerationContract(shotId!, overlay()))}>
            CREATE CONTRACT
          </Button>
          <Button size="small" disabled={!shotId} loading={busy} onClick={() => run(() => validateImageGenerationContract(shotId!, overlay()))}>
            VALIDATE
          </Button>
          <Button size="small" disabled={!shotId || row?.immutable} loading={busy} onClick={() => run(() => approveImageGenerationContract(shotId!))}>
            DIRECTOR APPROVE
          </Button>
          <Button size="small" danger disabled={!shotId || row?.immutable} loading={busy} onClick={() => run(() => rejectImageGenerationContract(shotId!))}>
            REJECT
          </Button>
        </Space>
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
        <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
        <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
        <Tag color={gateColor(row?.governanceEngine)}>IDENTITY GOVERNANCE [{row?.governanceEngine || '—'}]</Tag>
        <Tag color={gateColor(row?.shotContract)}>SHOT CONTRACT [{row?.shotContract || '—'}]</Tag>
        <Tag color={gateColor(row?.prompt)}>PROMPT [{row?.prompt || '—'}]</Tag>
        <Tag color="orange">DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
        <Tag>GENERATION NOT EXECUTED</Tag>
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
        · {row?.contractVersion || 'V1'} · {row?.status || 'DRAFT'}
      </Typography.Text>
      <label style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
        Quality policy (optional, not identity)
        <Input size="small" value={quality} onChange={(e) => setQuality(e.target.value)} />
      </label>
      <div style={{ fontSize: 12 }}>
        <div>Contract SHA: {row?.contractSha256 || '—'}</div>
        <div>Prompt SHA: {row?.promptSha256 || '—'}</div>
        <div>Master SHA: {row?.masterSha256 || '—'}</div>
        <div>DNA SHA: {row?.dnaSha256 || '—'}</div>
        <div>PRP SHA: {row?.prpSha256 || '—'}</div>
        <div>Shot Contract SHA: {row?.shotContractSha256 || '—'}</div>
      </div>
    </Card>
  );
}
