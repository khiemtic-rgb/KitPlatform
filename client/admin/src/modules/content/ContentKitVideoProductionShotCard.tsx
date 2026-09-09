import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Collapse, Input, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoProductionShot,
  approveKitVideoProductionShot,
  createKitVideoProductionShot,
  editKitVideoProductionShot,
  fetchKitVideoProductionReferencePack,
  fetchKitVideoProductionShots,
  identityCheckKitVideoProductionShot,
  rejectKitVideoProductionShot,
  returnKitVideoProductionShot,
  type KitVideoProductionShotGetRow,
  type KitVideoProductionShotRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  PRODUCTION_SHOT_PREFIX,
  evaluateDirectorShotReview,
  evaluateShotSourceGate,
  shotGatePass,
  type ProductionShotSpec,
} from './kit-video-production-shot';
import { MASTER_CODE_V1 } from './kit-video-master-lock';
import { CHARACTER_DNA_CODE } from './kit-video-character-dna';
import { PRODUCTION_PACK_CODE } from './kit-video-production-reference-pack';

const SECTIONS: { key: string; title: string }[] = [
  { key: 'source', title: 'A. SOURCE' },
  { key: 'scene', title: 'B. SCENE' },
  { key: 'camera', title: 'C. CAMERA' },
  { key: 'framing', title: 'D. FRAMING' },
  { key: 'pose', title: 'E. POSE' },
  { key: 'expression', title: 'F. EXPRESSION' },
  { key: 'lighting', title: 'G. LIGHTING' },
  { key: 'wardrobe', title: 'H. WARDROBE' },
  { key: 'environment', title: 'I. ENVIRONMENT' },
  { key: 'interaction', title: 'J. INTERACTION' },
  { key: 'continuity', title: 'K. CONTINUITY' },
  { key: 'identityRisk', title: 'L. IDENTITY RISK' },
  { key: 'forbiddenConditions', title: 'M. FORBIDDEN CONDITIONS' },
  { key: 'identityCheck', title: 'N. IDENTITY CHECK' },
  { key: 'directorDecision', title: 'O. DIRECTOR DECISION' },
];

export function ContentKitVideoProductionShotCard() {
  const { modal, message } = App.useApp();
  const [bundle, setBundle] = useState<KitVideoProductionShotGetRow>();
  const [selectedId, setSelectedId] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [note, setNote] = useState('');

  const load = () => {
    setBusy(true);
    void Promise.all([fetchKitVideoProductionReferencePack('CHAR-001').catch(() => undefined), fetchKitVideoProductionShots('CHAR-001')])
      .then(([prp, row]) => {
        setBundle({
          ...row,
          master: row.master || prp?.master || undefined,
          dna: row.dna || prp?.dna || undefined,
          pack: row.pack || prp?.pack || undefined,
          identityPass: row.identityPass ?? prp?.identityPass,
          stressPass: row.stressPass ?? prp?.stressPass,
        });
        setSelectedId((cur) => cur || row.shots?.[0]?.id);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Shots. Restart API :5290 rồi Tải lại.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const shot = bundle?.shots?.find((s) => s.id === selectedId) || bundle?.shots?.[0];
  const master = bundle?.master;
  const dna = bundle?.dna;
  const pack = bundle?.pack;
  const locked = shot?.shotStatus === 'LOCKED';
  const sourceChecks = evaluateShotSourceGate({
    masterLocked: master?.status === 'MASTER_REFERENCE_LOCKED',
    dnaLocked: dna?.status === 'LOCKED',
    prpLocked: pack?.status === 'LOCKED',
    masterShaValid: !!master?.sha256 && master.sha256 === (shot?.masterSha256 || pack?.masterSha256 || master.sha256),
    dnaShaValid: !!dna?.dnaSha256,
    prpShaValid: !!pack?.prpSha256 || pack?.status === 'LOCKED',
    identityPass: bundle?.identityPass,
    stressPass: bundle?.stressPass,
  });
  const checks = shot?.selfCheck?.length
    ? shot.selfCheck.map((x) => ({ code: x.code, label: x.label, pass: x.pass, reason: x.reason || undefined }))
    : shot
      ? evaluateDirectorShotReview({
          spec: shot.spec as ProductionShotSpec,
          dna: dna?.spec,
          masterLocked: master?.status === 'MASTER_REFERENCE_LOCKED',
          dnaLocked: dna?.status === 'LOCKED',
          prpLocked: pack?.status === 'LOCKED',
          masterShaValid: !!master?.sha256,
          dnaShaValid: !!dna?.dnaSha256,
          prpShaValid: pack?.status === 'LOCKED',
          identityPass: bundle?.identityPass,
          stressPass: bundle?.stressPass,
        })
      : sourceChecks;
  const gateOk = bundle?.gatePass ?? shotGatePass(sourceChecks);
  const directorOk = shot?.directorGatePass ?? false;
  const spec = (shot?.spec || {}) as Record<string, unknown>;

  const applyShot = (row: KitVideoProductionShotRow, msg?: string) => {
    setBundle((cur) => ({
      ...(cur || { shots: [], canCreate: false }),
      shots: [row, ...(cur?.shots || []).filter((s) => s.id !== row.id)],
      canCreate: cur?.canCreate ?? false,
      gatePass: row.gatePass,
    }));
    setSelectedId(row.id);
    if (msg) setNotice(msg);
    setError(undefined);
  };

  return (
    <Card size="small" title="PRODUCTION SHOT REVIEW — CHAR-001 / MINH / ERA-01" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Shot specification derived from {MASTER_CODE_V1} + {CHARACTER_DNA_CODE} + {PRODUCTION_PACK_CODE}. Không tạo ảnh/video.
        Không Gemini/Runway. Không sửa Master/DNA/PRP. DNA thắng khi conflict.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={master?.status === 'MASTER_REFERENCE_LOCKED' ? 'green' : 'orange'}>
          MASTER {master?.status === 'MASTER_REFERENCE_LOCKED' ? 'LOCKED' : master?.status || '—'}
        </Tag>
        <Tag color={dna?.status === 'LOCKED' ? 'green' : 'orange'}>DNA {dna?.status || '—'}</Tag>
        <Tag color={pack?.status === 'LOCKED' ? 'green' : 'orange'}>PRP {pack?.status || '—'}</Tag>
        <Tag color={bundle?.identityPass ? 'green' : 'red'}>Identity {bundle?.identityPass ? 'PASS' : '—'}</Tag>
        <Tag color={bundle?.stressPass ? 'green' : 'red'}>Stress {bundle?.stressPass ? 'PASS' : '—'}</Tag>
        <Tag color={gateOk ? 'green' : 'red'}>PRODUCTION {gateOk ? 'READY' : 'NOT READY'}</Tag>
        <Tag color={locked ? 'green' : shot ? 'blue' : 'default'}>SHOT {shot?.shotStatus || 'CHƯA TẠO'}</Tag>
        <Tag color={directorOk ? 'green' : shot ? 'red' : 'default'}>DIRECTOR GATE: {shot ? (directorOk ? 'PASS' : 'FAIL') : '—'}</Tag>
      </Space>

      <p style={{ fontSize: 13, marginBottom: 8 }}>MASTER ✓ · DNA ✓ · PRP ✓</p>
      <Collapse
        size="small"
        style={{ marginBottom: 8 }}
        items={[
          {
            key: 'sha',
            label: 'Source / SHA / Authority',
            children: (
              <div style={{ fontSize: 12, color: '#334155' }}>
                <div>Master SHA256: {master?.sha256 || shot?.masterSha256 || '—'}</div>
                <div>DNA SHA256: {dna?.dnaSha256 || shot?.dnaSha256 || '—'}</div>
                <div>PRP SHA256: {pack?.prpSha256 || shot?.prpSha256 || '—'}</div>
              </div>
            ),
          },
          {
            key: 'self',
            label: 'SHOT SELF-CHECK / DIRECTOR REVIEW',
            children: (
              <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 8, fontSize: 12 }}>
                {checks.map((item) => (
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
            ),
          },
        ]}
      />

      <Space wrap style={{ marginBottom: 8 }}>
        {(bundle?.shots || []).map((s) => (
          <Button key={s.id} size="small" type={s.id === shot?.id ? 'primary' : 'default'} onClick={() => setSelectedId(s.id)}>
            {s.shotCode} · {s.shotStatus}
          </Button>
        ))}
      </Space>

      {bundle?.blocked && !shot ? <Alert type="warning" showIcon message={bundle.blocked} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <Button
          size="small"
          type="primary"
          disabled={!bundle?.canCreate || busy || !gateOk}
          onClick={() => {
            setBusy(true);
            void createKitVideoProductionShot('CHAR-001')
              .then((row) => applyShot(row, `SHOT STATUS: DRAFT · ${row.shotCode}. Chưa tạo ảnh.`))
              .catch((e) => {
                const msg = apiErrorMessage(e, 'Chưa tạo được Production Shot.');
                setError(msg);
                message.error(msg);
              })
              .finally(() => setBusy(false));
          }}
        >
          CREATE PRODUCTION SHOT
        </Button>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>

      {shot ? (
        <>
          {locked ? (
            <Alert
              type="success"
              showIcon
              message="PRODUCTION SHOT LOCKED"
              description={
                <div style={{ fontSize: 12 }}>
                  <div>SHOT: {shot.shotCode} / {PRODUCTION_SHOT_PREFIX}</div>
                  <div>SHOT SHA256: {shot.shotSha256 || '—'}</div>
                  <div>Approved by: {shot.approvedBy || 'Director'} · {shot.approvedAt || '—'}</div>
                  <div>PRODUCTION SHOT READY · chưa ảnh / video / Gemini / Runway.</div>
                </div>
              }
              style={{ marginBottom: 8 }}
            />
          ) : null}
          <Collapse
            size="small"
            items={SECTIONS.map((s) => ({
              key: s.key,
              label: s.title,
              children: (
                <pre style={{ margin: 0, fontSize: 11, whiteSpace: 'pre-wrap' }}>
                  {JSON.stringify(spec[s.key] ?? '—', null, 2)}
                </pre>
              ),
            }))}
          />
          {shot.blocked ? <Alert type="warning" showIcon message={shot.blocked} style={{ marginBottom: 8 }} /> : null}
          {shot.analysis ? <Alert type="info" showIcon message={shot.analysis} style={{ marginBottom: 8 }} /> : null}
          {!locked ? (
            <>
              <Input.TextArea
                rows={2}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="PRODUCTION SHOT NOTE"
                style={{ margin: '8px 0' }}
              />
              <Space wrap>
                <Button
                  size="small"
                  disabled={busy}
                  onClick={() => {
                    setBusy(true);
                    void analyzeKitVideoProductionShot(shot.id)
                      .then((row) => {
                        applyShot(row);
                        setNotice(row.analysis || 'Phân tích xong — chưa khóa.');
                      })
                      .catch((e) => setError(apiErrorMessage(e, 'Phân tích không chạy.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  ANALYZE
                </Button>
                <Button
                  size="small"
                  disabled={busy || !shot.canEdit}
                  onClick={() => {
                    setBusy(true);
                    void editKitVideoProductionShot(shot.id, spec, note.trim())
                      .then((row) => applyShot(row, 'Đã lưu DRAFT. Identity không đổi.'))
                      .catch((e) => setError(apiErrorMessage(e, 'Chưa sửa được Shot.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  EDIT
                </Button>
                <Button
                  size="small"
                  disabled={busy || locked}
                  onClick={() => {
                    setBusy(true);
                    void identityCheckKitVideoProductionShot(shot.id)
                      .then((row) => applyShot(row, row.identityCheckPass ? 'IDENTITY CHECK PASS' : 'IDENTITY CHECK FAIL'))
                      .catch((e) => setError(apiErrorMessage(e, 'Identity Check không chạy.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  IDENTITY CHECK
                </Button>
                <Button
                  size="small"
                  type="primary"
                  disabled={busy || !shot.canApprove || !directorOk}
                  onClick={() => {
                    modal.confirm({
                      title: 'DIRECTOR APPROVE SHOT',
                      okText: 'APPROVE SHOT',
                      cancelText: 'HỦY',
                      centered: true,
                      zIndex: 3100,
                      content: <div>Khóa {shot.shotCode}. Ghi approved_by / shot_sha256. Không tạo ảnh.</div>,
                      onOk: () => {
                        setBusy(true);
                        return approveKitVideoProductionShot(shot.id, note.trim())
                          .then((row) => {
                            applyShot(row, 'PRODUCTION SHOT LOCKED');
                            message.success('PRODUCTION SHOT LOCKED');
                          })
                          .catch((e) => {
                            const msg = apiErrorMessage(e, 'Chưa approve được Shot.');
                            setError(msg);
                            throw e;
                          })
                          .finally(() => setBusy(false));
                      },
                    });
                  }}
                >
                  DIRECTOR APPROVE
                </Button>
                <Button
                  size="small"
                  danger
                  disabled={busy || !shot.canReject}
                  onClick={() => {
                    setBusy(true);
                    void rejectKitVideoProductionShot(shot.id, note.trim())
                      .then((row) => applyShot(row, 'REJECT. Shot chưa khóa.'))
                      .catch((e) => setError(apiErrorMessage(e, 'Chưa reject được.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  REJECT
                </Button>
                <Button
                  size="small"
                  disabled={busy || !shot.canReturn}
                  onClick={() => {
                    setBusy(true);
                    void returnKitVideoProductionShot(shot.id, note.trim())
                      .then((row) => applyShot(row, 'RETURN TO EDIT · DRAFT.'))
                      .catch((e) => setError(apiErrorMessage(e, 'Chưa return được.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  RETURN TO EDIT
                </Button>
              </Space>
            </>
          ) : null}
        </>
      ) : null}
    </Card>
  );
}
