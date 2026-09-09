import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Input, InputNumber, Space, Switch, Tag, Typography } from 'antd';
import {
  approveProductionShotContract,
  fetchKitVideoProductionShots,
  fetchProductionShotContract,
  rejectProductionShotContract,
  saveProductionShotContract,
  validateProductionShotContract,
  type ProductionShotContractRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

type Payload = Record<string, any>;

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED') return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'MISSING' || s === 'REJECTED') return 'red';
  return 'orange';
};

const nest = (obj: Payload, path: string[], value: unknown): Payload => {
  const next = structuredClone(obj);
  let cur: any = next;
  for (let i = 0; i < path.length - 1; i += 1) {
    const key = path[i];
    const index = Number(key);
    const nextIsIndex = String(Number(path[i + 1])) === path[i + 1];
    if (String(index) === key) {
      if (!Array.isArray(cur)) return obj;
      cur[index] = cur[index] && typeof cur[index] === 'object' ? cur[index] : {};
      cur = cur[index];
      continue;
    }
    if (nextIsIndex) {
      if (!Array.isArray(cur[key])) cur[key] = [];
    } else {
      cur[key] = { ...(cur[key] || {}) };
    }
    cur = cur[key];
  }
  cur[path[path.length - 1]] = value;
  return next;
};

const read = (obj: Payload, path: string[]) => {
  let cur: any = obj;
  for (const key of path) cur = cur?.[key];
  return cur ?? '';
};

export function ContentKitVideoProductionShotContractCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ProductionShotContractRow>();
  const [payload, setPayload] = useState<Payload>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchProductionShotContract(target)
      .then((data) => {
        setRow(data);
        setPayload(data.payload || {});
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Shot Contract.')))
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
      .catch((e) => setError(apiErrorMessage(e, 'Chưa tải được Production Shots.')));
  }, []);

  const setField = (path: string[], value: unknown) => setPayload((cur) => nest(cur, path, value));

  const run = (fn: () => Promise<ProductionShotContractRow>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then((data) => {
        setRow(data);
        setPayload(data.payload || {});
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Contract gate BLOCKED.')))
      .finally(() => setBusy(false));
  };

  const field = (label: string, path: string[]) => (
    <label style={{ display: 'block', marginBottom: 6, fontSize: 12 }}>
      {label}
      <Input
        size="small"
        value={String(read(payload, path) ?? '')}
        onChange={(e) => setField(path, e.target.value)}
        disabled={row?.immutable}
      />
    </label>
  );

  return (
    <Card
      size="small"
      title="PRODUCTION SHOT CONTRACT V1"
      extra={
        <Space wrap>
          <Button size="small" loading={busy} onClick={() => load()}>
            Tải lại
          </Button>
          <Button size="small" disabled={!shotId || row?.immutable} loading={busy} onClick={() => run(() => saveProductionShotContract(shotId!, payload))}>
            Lưu DRAFT
          </Button>
          <Button size="small" disabled={!shotId} loading={busy} onClick={() => run(() => validateProductionShotContract(shotId!, payload))}>
            Validate
          </Button>
          <Button size="small" disabled={!shotId || row?.immutable} loading={busy} onClick={() => run(() => approveProductionShotContract(shotId!))}>
            Director Approve
          </Button>
          <Button size="small" danger disabled={!shotId || row?.immutable} loading={busy} onClick={() => run(() => rejectProductionShotContract(shotId!))}>
            Reject
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
        <Tag color={gateColor(row?.contractValidation)}>CONTRACT VALIDATION [{row?.contractValidation || '—'}]</Tag>
        <Tag color="orange">DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
        <Tag>GENERATION NOT EXECUTED</Tag>
        <Tag>SHA {(row?.contractSha256 || '').slice(0, 12) || '—'}</Tag>
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
        · {row?.contractVersion || 'V1'} · {row?.status || 'DRAFT'} · {row?.id ? 'persisted' : 'not saved'}
      </Typography.Text>

      <div className="fx-dir-ws__intent">
        <Typography.Text strong>SCENE INTENT</Typography.Text>
        <p>{String(read(payload, ['story', 'action']) || read(payload, ['story', 'objective']) || 'View full shot description below.')}</p>
      </div>

      <Collapse
        size="small"
        items={[
          {
            key: 'story',
            label: 'Story & Narrative',
            children: (
              <>
                {field('Beat', ['story', 'beat'])}
                {field('Action', ['story', 'action'])}
                {field('Objective', ['story', 'objective'])}
              </>
            ),
          },
          {
            key: 'action',
            label: 'Action',
            children: (
              <>
                {field('Expression', ['character', 'expression'])}
                {field('Pose', ['character', 'pose'])}
                {field('Gaze', ['character', 'gaze'])}
                {field('Movement', ['character', 'movement'])}
              </>
            ),
          },
          {
            key: 'visual',
            label: 'Visual',
            children: (
              <>
                {field('Wardrobe', ['wardrobe', 'description'])}
                {field('Wardrobe continuity', ['wardrobe', 'continuity'])}
                {field('Prop id', ['props', '0', 'id'])}
                {field('Name', ['props', '0', 'name'])}
                {field('State', ['props', '0', 'state'])}
              </>
            ),
          },
          {
            key: 'camera',
            label: 'Camera',
            children: (
              <>
                {field('Framing', ['composition', 'framing'])}
                {field('Camera Angle', ['composition', 'cameraAngle'])}
                {field('Camera Distance', ['composition', 'cameraDistance'])}
                {field('Camera Position', ['composition', 'cameraPosition'])}
                {field('Subject Position', ['composition', 'subjectPosition'])}
                {field('Spatial Relationship', ['composition', 'spatialRelationship'])}
                {field('Subject Motion', ['motion', 'subjectMotion'])}
                {field('Camera Motion', ['motion', 'cameraMotion'])}
                {field('Motion Intent', ['motion', 'intent'])}
              </>
            ),
          },
          {
            key: 'location',
            label: 'Location',
            children: (
              <>
                {field('Location', ['scene', 'location'])}
                {field('Time', ['scene', 'time'])}
                {field('Environment', ['scene', 'environment'])}
              </>
            ),
          },
          {
            key: 'lighting',
            label: 'Lighting',
            children: field('Environment light', ['scene', 'environment']),
          },
          {
            key: 'props',
            label: 'Props',
            children: (
              <>
                {field('Prop id', ['props', '0', 'id'])}
                {field('Name', ['props', '0', 'name'])}
                {field('State', ['props', '0', 'state'])}
              </>
            ),
          },
          {
            key: 'character',
            label: 'Character State',
            children: (
              <>
                {field('Character', ['characterId'])}
                {field('Series', ['seriesId'])}
                {field('Era', ['eraId'])}
                {field('Expression', ['character', 'expression'])}
                {field('Pose', ['character', 'pose'])}
              </>
            ),
          },
          {
            key: 'relationship',
            label: 'Relationship State',
            children: field('Spatial Relationship', ['composition', 'spatialRelationship']),
          },
          {
            key: 'continuity',
            label: 'Continuity',
            children: (
              <>
                {field('Previous Shot', ['continuity', 'previousShotId'])}
                {field('Rules (comma)', ['continuity', 'rules'])}
                {field('Wardrobe continuity', ['wardrobe', 'continuity'])}
              </>
            ),
          },
          {
            key: 'meta',
            label: 'Technical Metadata',
            children: (
              <>
                <label style={{ display: 'block', marginBottom: 6 }}>
                  Duration seconds
                  <InputNumber
                    size="small"
                    min={0.1}
                    style={{ width: '100%' }}
                    value={Number(read(payload, ['timing', 'durationSeconds']) || 0)}
                    onChange={(v) => setField(['timing', 'durationSeconds'], v || 0)}
                    disabled={row?.immutable}
                  />
                </label>
                {field('Mandatory', ['constraints', 'mandatory'])}
                {field('Allowed', ['constraints', 'allowed'])}
                {field('Forbidden', ['constraints', 'forbidden'])}
                <label style={{ display: 'block', marginBottom: 6 }}>
                  Dialogue enabled <Switch size="small" checked={!!read(payload, ['dialogue', 'enabled'])} onChange={(v) => setField(['dialogue', 'enabled'], v)} disabled={row?.immutable} />
                </label>
                {field('Text', ['dialogue', 'text'])}
                {field('Aspect Ratio', ['production', 'aspectRatio'])}
                {field('Resolution', ['production', 'resolution'])}
                {field('Frame Rate', ['production', 'frameRate'])}
              </>
            ),
          },
          {
            key: 'authority',
            label: 'Source / SHA / Authority',
            children: (
              <>
                <div>Master SHA: {row?.masterSha256 || '—'}</div>
                <div>DNA SHA: {row?.dnaSha256 || '—'}</div>
                <div>PRP SHA: {row?.prpSha256 || '—'}</div>
                <div>Contract SHA: {row?.contractSha256 || '—'}</div>
              </>
            ),
          },
        ]}
      />
    </Card>
  );
}
