import { useEffect, useState } from 'react';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import {
  compileProductionPrompt,
  fetchKitVideoProductionShots,
  fetchProductionPrompt,
  type ProductionPromptCompilerRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'COMPILED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED') return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'MISSING' || s === 'NOT_READY') return 'red';
  return 'orange';
};

export function ContentKitVideoProductionPromptCompilerCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ProductionPromptCompilerRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchProductionPrompt(target)
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Prompt Compiler.')))
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

  const compile = () => {
    if (!shotId) return;
    setBusy(true);
    void compileProductionPrompt(shotId)
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Compile bị BLOCK.')))
      .finally(() => setBusy(false));
  };

  return (
    <Card
      size="small"
      title="PRODUCTION PROMPT COMPILER"
      extra={
        <Button size="small" type="primary" disabled={!shotId} loading={busy} onClick={compile}>
          COMPILE PROMPT
        </Button>
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
        <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
        <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
        <Tag color={gateColor(row?.governanceEngine)}>IDENTITY GOVERNANCE [{row?.governanceEngine || '—'}]</Tag>
        <Tag color={gateColor(row?.shotContract)}>SHOT CONTRACT [{row?.shotContract || '—'}]</Tag>
        <Tag color={gateColor(row?.directorApproval)}>DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
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
        · {row?.promptVersion || 'V1'} · {row?.status || 'NOT_READY'}
      </Typography.Text>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, fontSize: 12 }}>
        <div>
          <Typography.Text strong>SOURCE FACTS / PROVENANCE</Typography.Text>
          <div>Character: {row?.characterId || '—'}</div>
          <div>Contract: {row?.contractVersion || 'V1'} · {(row?.contractSha256 || '').slice(0, 16) || '—'}</div>
          <div>Master SHA: {(row?.masterSha256 || '').slice(0, 16) || '—'}</div>
          <div>DNA SHA: {(row?.dnaSha256 || '').slice(0, 16) || '—'}</div>
          <div>PRP SHA: {(row?.prpSha256 || '').slice(0, 16) || '—'}</div>
          <div>Prompt SHA: {(row?.promptSha256 || '').slice(0, 16) || '—'}</div>
        </div>
        <div>
          <Typography.Text strong>NEGATIVE CONSTRAINTS</Typography.Text>
          <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
            {(row?.negativeConstraints || []).join('\n') || '—'}
          </pre>
        </div>
      </div>

      <div style={{ marginTop: 12 }}>
        <Typography.Text strong>COMPILED PROMPT</Typography.Text>
        <pre style={{ whiteSpace: 'pre-wrap', marginTop: 6, fontSize: 12 }}>
          {row?.prompt || (row?.blocks?.[0]?.message ?? 'NOT_READY — Director must approve the Shot Contract first.')}
        </pre>
      </div>
    </Card>
  );
}
