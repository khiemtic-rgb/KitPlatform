import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Collapse, Input, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoProductionReferencePack,
  approveKitVideoProductionReferencePack,
  createKitVideoProductionReferencePack,
  editKitVideoProductionReferencePack,
  fetchKitVideoCharacterDna,
  fetchKitVideoProductionReferencePack,
  rejectKitVideoProductionReferencePack,
  returnKitVideoProductionReferencePack,
  type KitVideoProductionReferencePackGetRow,
  type KitVideoProductionReferencePackRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  PRODUCTION_PACK_CODE,
  PRODUCTION_PACK_VERSION,
  attachMissingProductionRules,
  directorReviewPass,
  evaluateDirectorReview,
  evaluateProductionPackGate,
  productionPackGatePass,
  type ProductionPackSpec,
} from './kit-video-production-reference-pack';
import { MASTER_CODE_V1 } from './kit-video-master-lock';
import { CHARACTER_DNA_CODE } from './kit-video-character-dna';

const SECTIONS: { key: string; title: string }[] = [
  { key: 'masterReference', title: 'A. MASTER SOURCE' },
  { key: 'characterDna', title: 'B. CHARACTER DNA SOURCE' },
  { key: 'identityConstraints', title: 'C. IDENTITY CONSTRAINTS' },
  { key: 'productionRules', title: 'D. PRODUCTION RULES' },
  { key: 'shotRules', title: 'E. SHOT RULES' },
  { key: 'cameraRules', title: 'F. CAMERA RULES' },
  { key: 'expressionRules', title: 'G. EXPRESSION RULES' },
  { key: 'wardrobeRules', title: 'H. WARDROBE RULES' },
  { key: 'environmentRules', title: 'I. ENVIRONMENT RULES' },
  { key: 'productionContinuity', title: 'J. CONTINUITY RULES' },
  { key: 'forbiddenProduction', title: 'K. FORBIDDEN PRODUCTION CONDITIONS' },
  { key: 'lockMetadata', title: 'L. REGRESSION / LOCK METADATA' },
];

export function ContentKitVideoProductionReferencePackCard() {
  const { modal, message } = App.useApp();
  const [bundle, setBundle] = useState<KitVideoProductionReferencePackGetRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [note, setNote] = useState('');

  const load = () => {
    setBusy(true);
    void Promise.all([
      fetchKitVideoCharacterDna('CHAR-001').catch(() => undefined),
      fetchKitVideoProductionReferencePack('CHAR-001'),
    ])
      .then(([dnaRow, row]) => {
        setBundle({
          ...row,
          master: row.master || dnaRow?.master || row.dna?.master || undefined,
          dna: row.dna || dnaRow?.dna || undefined,
          identityPass: row.identityPass ?? dnaRow?.identityPass,
          stressPass: row.stressPass ?? dnaRow?.stressPass,
          p0: row.p0 ?? dnaRow?.p0,
        });
        setError(undefined);
      })
      .catch((e) => {
        setError(apiErrorMessage(e, 'Chưa đọc được Production Reference Pack. Restart API :5290 rồi Tải lại.'));
        void fetchKitVideoCharacterDna('CHAR-001')
          .then((dnaRow) => {
            setBundle({
              canCreate: false,
              master: dnaRow.master,
              dna: dnaRow.dna,
              identityPass: dnaRow.identityPass,
              stressPass: dnaRow.stressPass,
              p0: dnaRow.p0,
              gatePass: false,
            });
          })
          .catch(() => undefined);
      })
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const pack = bundle?.pack;
  const master = bundle?.master;
  const dna = bundle?.dna;
  const locked = pack?.status === 'LOCKED';
  const createChecks = evaluateProductionPackGate({
    masterLocked: master?.status === 'MASTER_REFERENCE_LOCKED',
    dnaLocked: dna?.status === 'LOCKED',
    masterShaValid: !!master?.sha256 && master.sha256 === (pack?.masterSha256 || master.sha256),
    dnaShaValid: !!dna?.dnaSha256,
    identityPass: bundle?.identityPass,
    stressPass: bundle?.stressPass,
    p0: bundle?.p0 ?? 0,
    directorApproval: master?.status === 'MASTER_REFERENCE_LOCKED' && dna?.status === 'LOCKED',
  });
  const reviewSpec = pack
    ? attachMissingProductionRules(
        pack.spec as ProductionPackSpec,
        pack.masterSha256 || master?.sha256 || '',
        pack.dnaSha256 || dna?.dnaSha256 || '',
      )
    : undefined;
  const reviewInput = {
    spec: reviewSpec,
    dna: dna?.spec,
    masterLocked: master?.status === 'MASTER_REFERENCE_LOCKED',
    dnaLocked: dna?.status === 'LOCKED',
    masterShaValid: !!master?.sha256 && master.sha256 === (pack?.masterSha256 || master.sha256),
    dnaShaValid: !!dna?.dnaSha256,
    identityPass: bundle?.identityPass,
    stressPass: bundle?.stressPass,
    p0: bundle?.p0 ?? 0,
    directorApproval: master?.status === 'MASTER_REFERENCE_LOCKED' && dna?.status === 'LOCKED',
  };
  const checks = bundle?.selfCheck?.length
    ? bundle.selfCheck.map((x) => ({
        code: x.code,
        label: x.label,
        pass: x.pass,
        reason: x.reason || undefined,
      }))
    : pack
      ? evaluateDirectorReview(reviewInput)
      : createChecks;
  const gateOk = productionPackGatePass(createChecks);
  const directorOk = pack?.directorGatePass ?? (bundle?.gatePass && pack ? directorReviewPass(checks) : false);
  const spec = (reviewSpec || pack?.spec || {}) as Record<string, unknown>;

  const applyPack = (row: KitVideoProductionReferencePackRow, msg?: string) => {
    setBundle((cur) => ({
      ...(cur || { canCreate: false }),
      pack: row,
      canCreate: false,
      gatePass: row.gatePass,
      selfCheck: row.selfCheck,
      identityPass: row.identityPass,
      stressPass: row.stressPass,
      p0: row.p0,
    }));
    if (msg) setNotice(msg);
    setError(undefined);
  };

  return (
    <Card size="small" title="PRODUCTION REFERENCE PACK REVIEW — CHAR-001 / MINH / ERA-01" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        PRP derived from {MASTER_CODE_V1} + {CHARACTER_DNA_CODE}. Không định nghĩa lại Identity. Không sửa Master/DNA.
        Không tạo ảnh/video. Không Gemini/Runway. Không Production Shot. DNA thắng khi conflict.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={master?.status === 'MASTER_REFERENCE_LOCKED' ? 'green' : 'orange'}>
          MASTER {master?.status === 'MASTER_REFERENCE_LOCKED' ? 'LOCKED' : master?.status || '—'}
        </Tag>
        <Tag color={dna?.status === 'LOCKED' ? 'green' : 'orange'}>DNA {dna?.status || '—'}</Tag>
        <Tag color={bundle?.identityPass ? 'green' : 'red'}>Identity {bundle?.identityPass ? 'PASS' : '—'}</Tag>
        <Tag color={bundle?.stressPass ? 'green' : 'red'}>Stress {bundle?.stressPass ? 'PASS' : '—'}</Tag>
        <Tag color={(bundle?.p0 ?? 1) === 0 ? 'green' : 'red'}>P0 {bundle?.p0 ?? '—'}</Tag>
        <Tag color={locked ? 'green' : pack ? 'blue' : 'default'}>PRP {pack?.status || 'CHƯA TẠO'}</Tag>
        <Tag color={gateOk ? 'green' : 'red'}>PRP GATE: {gateOk ? 'PASS' : 'FAIL'}</Tag>
        <Tag color={directorOk ? 'green' : pack ? 'red' : 'default'}>
          DIRECTOR GATE: {pack ? (directorOk ? 'PASS' : 'FAIL') : '—'}
        </Tag>
        <Tag color={pack?.productionReady || directorOk ? 'green' : 'default'}>
          PRODUCTION {pack?.productionReady || directorOk ? 'READY' : 'NOT READY'}
        </Tag>
      </Space>

      <div style={{ fontSize: 12, color: '#334155', marginBottom: 8 }}>
        <div>Master: {master?.masterCode || MASTER_CODE_V1}</div>
        <div>Master SHA256: {master?.sha256 || pack?.masterSha256 || '—'}</div>
        <div>DNA: {dna?.dnaCode || CHARACTER_DNA_CODE} · {dna?.status || '—'}</div>
        <div>DNA SHA256: {dna?.dnaSha256 || pack?.dnaSha256 || '—'}</div>
      </div>

      <Typography.Text strong>PRP SELF-CHECK / DIRECTOR PRP REVIEW</Typography.Text>
      <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 8, margin: '8px 0', fontSize: 12 }}>
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
      {bundle?.blocked && !pack ? <Alert type="warning" showIcon message={bundle.blocked} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        <Button
          size="small"
          type="primary"
          disabled={!bundle?.canCreate || !!pack || busy || !gateOk}
          onClick={() => {
            setBusy(true);
            void createKitVideoProductionReferencePack('CHAR-001')
              .then((row) => applyPack(row, `PACK STATUS: DRAFT · ${PRODUCTION_PACK_CODE}. Chưa khóa.`))
              .catch((e) => {
                const msg = apiErrorMessage(e, 'Chưa tạo được Production Reference Pack.');
                setError(msg);
                message.error(msg);
              })
              .finally(() => setBusy(false));
          }}
        >
          CREATE PRODUCTION REFERENCE PACK
        </Button>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>

      {pack ? (
        <>
          {locked ? (
            <Alert
              type="success"
              showIcon
              message="PRODUCTION REFERENCE PACK LOCKED"
              description={
                <div style={{ fontSize: 12 }}>
                  <div>PACK: {pack.packCode}</div>
                  <div>MASTER: {pack.masterCode}</div>
                  <div>DNA: {pack.dnaCode}</div>
                  <div>MASTER SHA256: {pack.masterSha256}</div>
                  <div>DNA SHA256: {pack.dnaSha256}</div>
                  <div>PRP SHA256: {pack.prpSha256 || '—'}</div>
                  <div>Approved by: {pack.approvedBy || 'Director'} · {pack.approvedAt || '—'}</div>
                  <div>PRODUCTION REFERENCE PACK READY · chưa Production Shot.</div>
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
          <div style={{ fontSize: 12, margin: '8px 0' }}>
            MASTER SHA256 · {pack.masterSha256 || '—'}
            <br />
            DNA SHA256 · {pack.dnaSha256 || '—'}
            <br />
            Continuity: character_id / master_id / master_sha256 / dna_id / dna_sha256 / prp_id / prp_version
          </div>
          {pack.blocked ? <Alert type="warning" showIcon message={pack.blocked} style={{ marginBottom: 8 }} /> : null}
          {pack.analysis ? <Alert type="info" showIcon message={pack.analysis} style={{ marginBottom: 8 }} /> : null}
          {!locked ? (
            <>
              <Input.TextArea
                rows={2}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="PRODUCTION REFERENCE PACK NOTE"
                style={{ margin: '8px 0' }}
              />
              <Space wrap>
                <Button
                  size="small"
                  disabled={busy}
                  onClick={() => {
                    setBusy(true);
                    void analyzeKitVideoProductionReferencePack('CHAR-001')
                      .then((row) => {
                        applyPack(row);
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
                >
                  ANALYZE
                </Button>
                <Button
                  size="small"
                  disabled={busy || !pack.canEdit}
                  onClick={() => {
                    setBusy(true);
                    void editKitVideoProductionReferencePack('CHAR-001', spec, note.trim())
                      .then((row) => applyPack(row, 'Đã lưu note DRAFT. Identity không đổi. Chưa khóa.'))
                      .catch((e) => {
                        const msg = apiErrorMessage(e, 'Chưa sửa được Pack.');
                        setError(msg);
                        message.error(msg);
                      })
                      .finally(() => setBusy(false));
                  }}
                >
                  EDIT
                </Button>
                <Button
                  size="small"
                  type="primary"
                  disabled={busy || !pack.canApprove || !directorOk}
                  onClick={() => {
                    modal.confirm({
                      title: 'DIRECTOR APPROVE PRP',
                      okText: 'APPROVE PRP',
                      cancelText: 'HỦY',
                      centered: true,
                      zIndex: 3100,
                      content: (
                        <div>
                          Khóa {PRODUCTION_PACK_CODE} / {PRODUCTION_PACK_VERSION}. Ghi approved_by / prp_sha256. Không tạo ảnh. Không đổi Master/DNA.
                        </div>
                      ),
                      onOk: () => {
                        setBusy(true);
                        return approveKitVideoProductionReferencePack('CHAR-001', note.trim())
                          .then((row) => {
                            applyPack(row, 'PRODUCTION REFERENCE PACK LOCKED');
                            message.success('PRODUCTION REFERENCE PACK LOCKED');
                          })
                          .catch((e) => {
                            const msg = apiErrorMessage(e, 'Chưa approve được Pack.');
                            setError(msg);
                            message.error(msg);
                            throw e;
                          })
                          .finally(() => setBusy(false));
                      },
                    });
                  }}
                >
                  DIRECTOR APPROVE PRP
                </Button>
                <Button
                  size="small"
                  danger
                  disabled={busy || !pack.canReject}
                  onClick={() => {
                    setBusy(true);
                    void rejectKitVideoProductionReferencePack('CHAR-001', note.trim())
                      .then((row) => applyPack(row, 'REJECT. Pack chưa khóa.'))
                      .catch((e) => {
                        const msg = apiErrorMessage(e, 'Chưa reject được.');
                        setError(msg);
                        message.error(msg);
                      })
                      .finally(() => setBusy(false));
                  }}
                >
                  REJECT
                </Button>
                <Button
                  size="small"
                  disabled={busy || !pack.canReturn}
                  onClick={() => {
                    setBusy(true);
                    void returnKitVideoProductionReferencePack('CHAR-001', note.trim())
                      .then((row) => applyPack(row, 'RETURN TO EDIT · DRAFT.'))
                      .catch((e) => {
                        const msg = apiErrorMessage(e, 'Chưa return được.');
                        setError(msg);
                        message.error(msg);
                      })
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
