import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Collapse, Input, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoCharacterDna,
  approveKitVideoCharacterDna,
  createKitVideoCharacterDna,
  editKitVideoCharacterDna,
  fetchKitVideoCharacterDna,
  fetchKitVideoMasterCandidateObjectUrl,
  rejectKitVideoCharacterDna,
  returnKitVideoCharacterDna,
  type KitVideoCharacterDnaGetRow,
  type KitVideoCharacterDnaRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  CHARACTER_DNA_CODE,
  CHARACTER_DNA_VERSION,
  directorGatePass,
  evaluateDirectorGate,
  type CharacterDnaSpec,
  type DnaCheckItem,
} from './kit-video-character-dna';
import { MASTER_CODE_V1 } from './kit-video-master-lock';

const CORE: { key: string; title: string }[] = [
  { key: 'face', title: 'FACE' },
  { key: 'eyes', title: 'EYES' },
  { key: 'hair', title: 'HAIR' },
  { key: 'age', title: 'AGE' },
  { key: 'expression', title: 'EXPRESSION' },
  { key: 'body', title: 'PROPORTION / SILHOUETTE' },
  { key: 'style', title: 'STYLE' },
];

const EXTRA: { key: string; title: string }[] = [
  { key: 'identityInvariants', title: 'Identity Invariants' },
  { key: 'allowedVariation', title: 'Allowed Variation' },
  { key: 'forbiddenVariation', title: 'Forbidden Variation' },
  { key: 'variationBoundaries', title: 'Variation Boundaries' },
  { key: 'continuityRules', title: 'Continuity Rules' },
  { key: 'stressRules', title: 'Stress Rules' },
  { key: 'regressionRules', title: 'Regression Rules' },
  { key: 'lockMetadata', title: 'Lock Metadata' },
];

function listOf(value: unknown): string[] {
  if (Array.isArray(value)) return value.map((x) => String(x));
  return [];
}

function groupOf(spec: Record<string, unknown>, key: string) {
  const raw = spec[key];
  if (!raw || typeof raw !== 'object') return { invariant: [] as string[], allowed: [] as string[], forbidden: [] as string[] };
  const g = raw as { invariant?: unknown; allowed?: unknown; forbidden?: unknown };
  return { invariant: listOf(g.invariant), allowed: listOf(g.allowed), forbidden: listOf(g.forbidden) };
}

export function ContentKitVideoCharacterDnaCard() {
  const { modal, message } = App.useApp();
  const [bundle, setBundle] = useState<KitVideoCharacterDnaGetRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [note, setNote] = useState('');
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Record<string, unknown>>();
  const [thumb, setThumb] = useState<string>();

  const load = () => {
    setBusy(true);
    void fetchKitVideoCharacterDna('CHAR-001')
      .then((row) => {
        setBundle(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Character DNA.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const dna = bundle?.dna;
  const master = bundle?.master || dna?.master;
  const locked = dna?.status === 'LOCKED';
  const spec = (draft || dna?.spec || {}) as Record<string, unknown>;
  const gateItems = evaluateDirectorGate({
    spec: spec as CharacterDnaSpec,
    masterExists: !!master,
    masterLocked: master?.status === 'MASTER_REFERENCE_LOCKED',
    shaValid: !!master?.sha256 && master.sha256 === (dna?.masterSha256 || master.sha256),
    identityPass: dna?.identityPass ?? bundle?.identityPass,
    stressPass: dna?.stressPass ?? bundle?.stressPass,
    p0: dna?.p0 ?? bundle?.p0 ?? 0,
  });
  const gateOk = directorGatePass(gateItems);

  useEffect(() => {
    const id = master?.sourceCandidateId;
    if (!id) return;
    let dead = false;
    let url: string | undefined;
    void fetchKitVideoMasterCandidateObjectUrl(id)
      .then((next) => {
        if (dead) {
          URL.revokeObjectURL(next);
          return;
        }
        url = next;
        setThumb(next);
      })
      .catch(() => undefined);
    return () => {
      dead = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [master?.sourceCandidateId]);

  const applyDna = (row: KitVideoCharacterDnaRow, msg?: string) => {
    setBundle((cur) => ({
      ...(cur || { canCreate: false }),
      dna: row,
      canCreate: false,
      identityPass: row.identityPass,
      stressPass: row.stressPass,
      p0: row.p0,
    }));
    setDraft(undefined);
    setEditing(false);
    if (msg) setNotice(msg);
    setError(undefined);
  };

  const createEnabled = !!bundle?.canCreate && !dna && !busy;

  return (
    <Card size="small" title="CHARACTER DNA REVIEW — CHAR-001 / MINH / ERA-01" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        DNA derived from {MASTER_CODE_V1}. Không tạo ảnh. Không Gemini/Runway. Không sửa Master. Không đụng Golden SH01-01.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}

      <Typography.Text strong>A. MASTER REFERENCE</Typography.Text>
      <div style={{ display: 'flex', gap: 12, margin: '8px 0 12px', alignItems: 'flex-start' }}>
        {thumb ? (
          <img src={thumb} alt="Master V1" style={{ width: 96, height: 120, objectFit: 'contain', borderRadius: 6, background: '#f8fafc' }} />
        ) : (
          <div style={{ width: 96, height: 120, background: '#f1f5f9', borderRadius: 6 }} />
        )}
        <div style={{ fontSize: 12, color: '#334155' }}>
          <div>Master ID: {master?.masterReferenceId || '—'}</div>
          <div>Character: Minh · CHAR-001</div>
          <div>Era: ERA-01</div>
          <div>Source: {master?.sourceCandidateCode || 'MINH-E01-CANDIDATE-004-D'}</div>
          <div>Master SHA256: {master?.sha256 || '—'}</div>
          <div>Status: {master?.status === 'MASTER_REFERENCE_LOCKED' ? 'LOCKED' : master?.status || '—'}</div>
        </div>
      </div>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={master?.status === 'MASTER_REFERENCE_LOCKED' ? 'green' : 'orange'}>MASTER {master?.status === 'MASTER_REFERENCE_LOCKED' ? 'LOCKED' : '—'}</Tag>
        <Tag color={(dna?.identityPass ?? bundle?.identityPass) ? 'green' : 'red'}>Identity {(dna?.identityHave ?? 7)}/7 {(dna?.identityPass ?? bundle?.identityPass) ? 'PASS' : '—'}</Tag>
        <Tag color={(dna?.stressPass ?? bundle?.stressPass) ? 'green' : 'red'}>Stress {(dna?.stressHave ?? 10)}/10 {(dna?.stressPass ?? bundle?.stressPass) ? 'PASS' : '—'}</Tag>
        <Tag color={(dna?.p0 ?? bundle?.p0 ?? 0) === 0 ? 'green' : 'red'}>P0 {dna?.p0 ?? bundle?.p0 ?? '—'}</Tag>
        <Tag color={locked ? 'green' : dna ? 'blue' : 'default'}>DNA {dna?.status || 'CHƯA TẠO'}</Tag>
        {dna ? <Tag color={gateOk || locked ? 'green' : 'red'}>DNA GATE: {gateOk || locked ? 'PASS' : 'FAIL'}</Tag> : null}
      </Space>

      {bundle?.blocked && !dna ? <Alert type="warning" showIcon message={bundle.blocked} style={{ marginBottom: 8 }} /> : null}
      <Space wrap style={{ marginBottom: 8 }}>
        <Button
          size="small"
          type="primary"
          disabled={!createEnabled}
          onClick={() => {
            setBusy(true);
            void createKitVideoCharacterDna('CHAR-001')
              .then((row) => applyDna(row, 'DNA STATUS: DRAFT · CHAR-001-MINH-ERA01-DNA-V1. Chưa khóa.'))
              .catch((e) => {
                const msg = apiErrorMessage(e, 'Chưa tạo được Character DNA.');
                setError(msg);
                message.error(msg);
              })
              .finally(() => setBusy(false));
          }}
        >
          Tạo Character DNA V1
        </Button>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>

      {dna ? (
        locked ? (
          <Alert
            type="success"
            showIcon
            message="CHARACTER DNA LOCKED"
            description={
              <div style={{ fontSize: 12 }}>
                <div>CHARACTER: Minh</div>
                <div>MASTER: {dna.masterCode}</div>
                <div>DNA: {dna.dnaCode}</div>
                <div>STATUS: LOCKED</div>
                <div>MASTER SHA256: {dna.masterSha256}</div>
                <div>DNA SHA256: {dna.dnaSha256 || '—'}</div>
                <div>Approved by: {dna.approvedBy || 'Director'} · {dna.approvedAt || '—'}</div>
                {CORE.map((g) => {
                  const row = groupOf(dna.spec || {}, g.key);
                  return (
                    <div key={g.key} style={{ marginTop: 6 }}>
                      <div style={{ fontWeight: 600 }}>{g.title}</div>
                      <div>INVARIANT · {row.invariant.join(', ') || '—'}</div>
                      <div>ALLOWED · {row.allowed.join(', ') || '—'}</div>
                      <div>FORBIDDEN · {row.forbidden.join(', ') || '—'}</div>
                    </div>
                  );
                })}
              </div>
            }
          />
        ) : (
          <DnaDraft
            dna={dna}
            spec={spec}
            gateItems={gateItems}
            gateOk={gateOk}
            busy={busy}
            editing={editing}
            note={note}
            onNote={setNote}
            onEdit={() => {
              setDraft({ ...(dna.spec || {}) });
              setEditing(true);
            }}
            onChange={setDraft}
            onAnalyze={() => {
              setBusy(true);
              void analyzeKitVideoCharacterDna('CHAR-001')
                .then((row) => {
                  applyDna(row);
                  setNotice(row.analysis || 'Phân tích xong — chưa khóa.');
                  message.success(row.analysis || 'Phân tích xong');
                })
                .catch((e) => {
                  const msg = apiErrorMessage(e, 'Phân tích không chạy.');
                  setError(msg);
                  message.error(msg);
                })
                .finally(() => setBusy(false));
            }}
            onSave={() => {
              setBusy(true);
              void editKitVideoCharacterDna('CHAR-001', spec, note.trim())
                .then((row) => applyDna(row, 'Đã lưu DNA DRAFT. Chưa khóa.'))
                .catch((e) => {
                  const msg = apiErrorMessage(e, 'Chưa lưu được DNA.');
                  setError(msg);
                  message.error(msg);
                })
                .finally(() => setBusy(false));
            }}
            onApprove={() => {
              modal.confirm({
                title: 'DIRECTOR APPROVE DNA',
                okText: 'DIRECTOR APPROVE DNA',
                cancelText: 'HỦY',
                centered: true,
                zIndex: 3100,
                content: (
                  <div>
                    Khóa {CHARACTER_DNA_CODE} / {CHARACTER_DNA_VERSION} từ {MASTER_CODE_V1}. Không tạo ảnh. Muốn đổi sau phải tạo DNA-V2.
                  </div>
                ),
                onOk: () => {
                  setBusy(true);
                  return approveKitVideoCharacterDna('CHAR-001', note.trim())
                    .then((row) => {
                      applyDna(row, 'CHARACTER DNA LOCKED');
                      message.success('CHARACTER DNA LOCKED');
                    })
                    .catch((e) => {
                      const msg = apiErrorMessage(e, 'Chưa approve được DNA.');
                      setError(msg);
                      message.error(msg);
                      throw e;
                    })
                    .finally(() => setBusy(false));
                },
              });
            }}
            onReject={() => {
              setBusy(true);
              void rejectKitVideoCharacterDna('CHAR-001', note.trim())
                .then((row) => applyDna(row, 'REJECT. DNA chưa khóa.'))
                .catch((e) => {
                  const msg = apiErrorMessage(e, 'Chưa reject được.');
                  setError(msg);
                  message.error(msg);
                })
                .finally(() => setBusy(false));
            }}
            onReturn={() => {
              setBusy(true);
              void returnKitVideoCharacterDna('CHAR-001', note.trim())
                .then((row) => applyDna(row, 'RETURN TO EDIT · DRAFT.'))
                .catch((e) => {
                  const msg = apiErrorMessage(e, 'Chưa return được.');
                  setError(msg);
                  message.error(msg);
                })
                .finally(() => setBusy(false));
            }}
          />
        )
      ) : null}
    </Card>
  );
}

function DnaDraft({
  dna,
  spec,
  gateItems,
  gateOk,
  busy,
  editing,
  note,
  onNote,
  onEdit,
  onChange,
  onAnalyze,
  onSave,
  onApprove,
  onReject,
  onReturn,
}: {
  dna: KitVideoCharacterDnaRow;
  spec: Record<string, unknown>;
  gateItems: DnaCheckItem[];
  gateOk: boolean;
  busy: boolean;
  editing: boolean;
  note: string;
  onNote: (v: string) => void;
  onEdit: () => void;
  onChange: (spec: Record<string, unknown>) => void;
  onAnalyze: () => void;
  onSave: () => void;
  onApprove: () => void;
  onReject: () => void;
  onReturn: () => void;
}) {
  const patchGroup = (key: string, field: 'invariant' | 'allowed' | 'forbidden', text: string) => {
    const cur = (spec[key] && typeof spec[key] === 'object' ? spec[key] : {}) as Record<string, unknown>;
    onChange({
      ...spec,
      [key]: { ...cur, [field]: text.split('\n').map((s) => s.trim()).filter(Boolean) },
    });
  };

  return (
    <>
      <div style={{ fontSize: 12, marginBottom: 8 }}>DNA STATUS: {dna.status} · {dna.dnaCode} · derived_from {dna.masterCode}</div>
      {dna.blocked ? <Alert type="warning" showIcon message={dna.blocked} style={{ marginBottom: 8 }} /> : null}
      {dna.analysis ? <Alert type="info" showIcon message={dna.analysis} style={{ marginBottom: 8 }} /> : null}
      <Typography.Text strong>B. IDENTITY INVARIANTS / ALLOWED / FORBIDDEN</Typography.Text>
      <div style={{ display: 'grid', gap: 8, margin: '8px 0 12px', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' }}>
        <div style={{ border: '1px solid #bbf7d0', background: '#f0fdf4', borderRadius: 8, padding: 8, fontSize: 12 }}>
          <div style={{ fontWeight: 700 }}>IDENTITY INVARIANTS</div>
          <div style={{ color: '#166534', marginBottom: 4 }}>Những đặc điểm tuyệt đối không được thay đổi</div>
          <div>CRITICAL · {listOf((spec.identityInvariants as { CRITICAL?: unknown } | undefined)?.CRITICAL).join(', ') || '—'}</div>
          <div>HIGH · {listOf((spec.identityInvariants as { HIGH?: unknown } | undefined)?.HIGH).join(', ') || '—'}</div>
        </div>
        <div style={{ border: '1px solid #bfdbfe', background: '#eff6ff', borderRadius: 8, padding: 8, fontSize: 12 }}>
          <div style={{ fontWeight: 700 }}>ALLOWED VARIATION</div>
          <div style={{ color: '#1d4ed8', marginBottom: 4 }}>Được phép thay đổi nhưng không được phá identity</div>
          <div>{listOf(spec.allowedVariation).join(', ') || '—'}</div>
        </div>
        <div style={{ border: '1px solid #fecaca', background: '#fef2f2', borderRadius: 8, padding: 8, fontSize: 12 }}>
          <div style={{ fontWeight: 700 }}>FORBIDDEN</div>
          <div style={{ color: '#b91c1c', marginBottom: 4 }}>Thay đổi làm Minh không còn là Minh</div>
          <div>{listOf(spec.forbiddenVariation).join(', ') || '—'}</div>
        </div>
      </div>
      <Typography.Text strong>C. IDENTITY CORE</Typography.Text>
      <div style={{ display: 'grid', gap: 8, margin: '8px 0' }}>
        {CORE.map((g) => {
          const row = groupOf(spec, g.key);
          return (
            <div key={g.key} style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 8, fontSize: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{g.title}</div>
              {editing ? (
                <>
                  <div>INVARIANT</div>
                  <Input.TextArea rows={2} value={row.invariant.join('\n')} onChange={(e) => patchGroup(g.key, 'invariant', e.target.value)} />
                  <div>ALLOWED</div>
                  <Input.TextArea rows={2} value={row.allowed.join('\n')} onChange={(e) => patchGroup(g.key, 'allowed', e.target.value)} />
                  <div>FORBIDDEN</div>
                  <Input.TextArea rows={2} value={row.forbidden.join('\n')} onChange={(e) => patchGroup(g.key, 'forbidden', e.target.value)} />
                </>
              ) : (
                <>
                  <div>INVARIANT · {row.invariant.join(', ') || '—'}</div>
                  <div>ALLOWED · {row.allowed.join(', ') || '—'}</div>
                  <div>FORBIDDEN · {row.forbidden.join(', ') || '—'}</div>
                </>
              )}
            </div>
          );
        })}
      </div>
      <Collapse
        size="small"
        items={EXTRA.map((s) => ({
          key: s.key,
          label: s.title,
          children: <pre style={{ margin: 0, fontSize: 11, whiteSpace: 'pre-wrap' }}>{JSON.stringify(spec[s.key] ?? '—', null, 2)}</pre>,
        }))}
      />
      <Typography.Text strong style={{ display: 'block', marginTop: 12 }}>D. DNA SELF-CHECK / DIRECTOR REVIEW</Typography.Text>
      <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 8, margin: '8px 0', fontSize: 12 }}>
        {gateItems.map((item) => (
          <div key={item.code} style={{ display: 'flex', gap: 8, alignItems: 'flex-start', marginBottom: 4 }}>
            <Tag color={item.pass ? 'green' : 'red'} style={{ minWidth: 56, textAlign: 'center' }}>
              {item.pass ? 'PASS' : 'FAIL'}
            </Tag>
            <div>
              <div>{item.label}</div>
              {!item.pass && item.reason ? <div style={{ color: '#b91c1c' }}>{item.reason}</div> : null}
            </div>
          </div>
        ))}
      </div>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateOk ? 'green' : 'red'}>DNA GATE: {gateOk ? 'PASS' : 'FAIL'}</Tag>
      </Space>
      <Input.TextArea rows={2} value={note} onChange={(e) => onNote(e.target.value)} placeholder="CHARACTER DNA NOTE" style={{ margin: '8px 0' }} />
      <Space wrap>
        <Button size="small" disabled={busy} onClick={onAnalyze}>
          Phân tích
        </Button>
        <Button size="small" disabled={busy || !dna.canEdit} onClick={onEdit}>
          Sửa DNA
        </Button>
        {editing ? (
          <Button size="small" type="primary" disabled={busy} onClick={onSave}>
            Lưu DNA
          </Button>
        ) : null}
        <Button size="small" type="primary" disabled={busy || !dna.canApprove || !gateOk} onClick={onApprove}>
          DIRECTOR APPROVE DNA
        </Button>
        <Button size="small" danger disabled={busy || !dna.canReject} onClick={onReject}>
          REJECT
        </Button>
        <Button size="small" disabled={busy || !dna.canReturn} onClick={onReturn}>
          RETURN TO EDIT
        </Button>
      </Space>
    </>
  );
}
