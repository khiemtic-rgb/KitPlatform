import { useEffect, useState } from 'react';
import { Alert, Button, Card, Input, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoIdentityStress,
  createKitVideoIdentityStress,
  fetchFamixaCharacter,
  fetchKitVideoIdentityStressArtifactObjectUrl,
  fetchKitVideoMasterReference,
  listKitVideoIdentityStress,
  listKitVideoIdentityTests,
  passKitVideoIdentityStress,
  promoteKitVideoIdentityStress,
  rejectKitVideoIdentityStress,
  repairKitVideoIdentityStress,
  runKitVideoIdentityStress,
  type KitVideoIdentityStressRow,
  type KitVideoMasterCandidateRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { dnaApproved } from './content-famixa-minh-visual-dna';
import { parseFamixaCanon } from './content-famixa-character-memory';
import { diagnoseCollapsedStressQa, STRESS_ALLOWED_CANDIDATE, STRESS_CASES } from './kit-video-identity-stress';

export function ContentKitVideoIdentityStressCard() {
  const [candidates, setCandidates] = useState<KitVideoMasterCandidateRow[]>([]);
  const [tests, setTests] = useState<KitVideoIdentityStressRow[]>([]);
  const [completeIds, setCompleteIds] = useState<Set<string>>(new Set());
  const [dnaOk, setDnaOk] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [previews, setPreviews] = useState<Record<string, string>>({});
  const [repairCase, setRepairCase] = useState('ST-08');
  const [repairText, setRepairText] = useState('');

  const load = () => {
    setBusy(true);
    void Promise.all([
      fetchKitVideoMasterReference('FAMIXA', 'CHAR-001'),
      fetchFamixaCharacter('CHAR-001'),
      listKitVideoIdentityTests(),
      listKitVideoIdentityStress(),
    ])
      .then(([master, character, identity, rows]) => {
        const canon = parseFamixaCanon(character.canon);
        setDnaOk(dnaApproved(canon.visualDna));
        setCandidates(master.candidates || []);
        setCompleteIds(new Set(identity.filter((t) => t.complete || t.status === 'PASS' || t.status === 'CONDITIONAL').map((t) => t.candidateId)));
        setTests(rows);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Identity Stress Test.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    const ids = tests.flatMap((t) => t.artifacts.filter((a) => a.sha256).map((a) => a.id));
    let gone = false;
    const urls: Record<string, string> = {};
    void Promise.all(
      ids.map((id) =>
        fetchKitVideoIdentityStressArtifactObjectUrl(id)
          .then((url) => {
            urls[id] = url;
          })
          .catch(() => undefined),
      ),
    ).then(() => {
      if (gone) {
        Object.values(urls).forEach((u) => URL.revokeObjectURL(u));
        return;
      }
      setPreviews((prev) => {
        Object.values(prev).forEach((u) => URL.revokeObjectURL(u));
        return urls;
      });
    });
    return () => {
      gone = true;
    };
  }, [tests]);

  const eligible = candidates.filter((c) => {
    const blocked = c.lifecycle === 'INELIGIBLE' || c.lifecycle === 'NOT_ELIGIBLE' || c.lifecycle === 'REJECTED';
    return c.candidateCode === STRESS_ALLOWED_CANDIDATE && !blocked && completeIds.has(c.id);
  });

  const apply = (row: KitVideoIdentityStressRow, note?: string) => {
    setTests((cur) => [row, ...cur.filter((t) => t.id !== row.id)]);
    setNotice(note);
    setError(undefined);
  };

  return (
    <Card size="small" title="Identity Stress Test — CHAR-001 / MINH / ERA-01" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        QA layer trên {STRESS_ALLOWED_CANDIDATE}. 10 case. Không tự SELECT / APPROVE / LOCK. Ảnh Stress không phải Production Still.
      </Typography.Paragraph>
      {!dnaOk ? <Alert type="warning" showIcon message="DNA chưa APPROVED — không chạy Stress Test." style={{ marginBottom: 8 }} /> : null}
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}

      <Space wrap style={{ marginBottom: 8 }}>
        {eligible.map((c) => (
          <Button
            key={c.id}
            size="small"
            type="primary"
            disabled={busy || !dnaOk}
            onClick={() => {
              setBusy(true);
              void createKitVideoIdentityStress(c.id)
                .then(apply)
                .catch((e) => setError(apiErrorMessage(e, 'Chưa mở được Stress Test.')))
                .finally(() => setBusy(false));
            }}
          >
            RUN STRESS TEST · {c.candidateCode}
          </Button>
        ))}
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>
      {eligible.length === 0 ? (
        <Typography.Paragraph type="secondary">Cần {STRESS_ALLOWED_CANDIDATE} + Identity Test 7/7 COMPLETE + Vision PASS.</Typography.Paragraph>
      ) : null}

      {tests.map((t) => (
        <StressBlock
          key={t.id}
          row={t}
          previews={previews}
          busy={busy}
          dnaOk={dnaOk}
          repairCase={repairCase}
          repairText={repairText}
          onRepairCase={setRepairCase}
          onRepairText={setRepairText}
          onRun={(code) => {
            setBusy(true);
            void runKitVideoIdentityStress(t.id, code)
              .then(apply)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa chạy được Stress Test.')))
              .finally(() => setBusy(false));
          }}
          onAnalyze={() => {
            const code = (repairCase || 'ST-10').trim().toUpperCase();
            setBusy(true);
            setError(undefined);
            setNotice(`Đang chấm Vision ${code} trên artifact hiện có — không gen ảnh mới.`);
            void analyzeKitVideoIdentityStress(t.id, code)
              .then((row) => {
                const art = row.artifacts.find((a) => a.testCase === code);
                const qa = (art?.qa || {}) as { identityScore?: number; p0?: string[] };
                apply(
                  row,
                  `${code} · ${art?.qaStatus || row.status} · ID ${qa.identityScore ?? '—'} · P0 ${Array.isArray(qa.p0) ? qa.p0.length : 0}. Không tạo ảnh mới.`,
                );
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa phân tích được.')))
              .finally(() => setBusy(false));
          }}
          onPass={() => {
            setBusy(true);
            void passKitVideoIdentityStress(t.id)
              .then((row) => {
                apply(row);
                setNotice('Director PASS. Chưa phải Master, chưa khóa.');
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa ghi được Director PASS.')))
              .finally(() => setBusy(false));
          }}
          onReject={() => {
            setBusy(true);
            void rejectKitVideoIdentityStress(t.id)
              .then((row) => {
                apply(row);
                setNotice('Director Reject. Không tự Canon.');
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa reject được.')))
              .finally(() => setBusy(false));
          }}
          onRepair={() => {
            if (!repairText.trim()) {
              setError('Repair cần diagnosis — không blind retry.');
              return;
            }
            setBusy(true);
            void repairKitVideoIdentityStress(t.id, repairCase, repairText.trim())
              .then(apply)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa repair được.')))
              .finally(() => setBusy(false));
          }}
          onPromote={() => {
            setBusy(true);
            void promoteKitVideoIdentityStress(t.id)
              .then((row) => {
                apply(row);
                setNotice('Đưa sang Master Review — chưa chọn hộ chiếu, chưa khóa.');
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa đưa sang Master Review được.')))
              .finally(() => setBusy(false));
          }}
        />
      ))}
    </Card>
  );
}

function StressBlock({
  row,
  previews,
  busy,
  dnaOk,
  repairCase,
  repairText,
  onRepairCase,
  onRepairText,
  onRun,
  onAnalyze,
  onPass,
  onReject,
  onRepair,
  onPromote,
}: {
  row: KitVideoIdentityStressRow;
  previews: Record<string, string>;
  busy: boolean;
  dnaOk: boolean;
  repairCase: string;
  repairText: string;
  onRepairCase: (v: string) => void;
  onRepairText: (v: string) => void;
  onRun: (code?: string) => void;
  onAnalyze: () => void;
  onPass: () => void;
  onReject: () => void;
  onRepair: () => void;
  onPromote: () => void;
}) {
  const latest = new Map(row.artifacts.map((a) => [a.testCase, a]));
  const passed = row.artifacts.filter((a) => a.qaStatus === 'PASS').length;
  const canPromote = row.status === 'PASS' && row.directorDecision === 'PASS';
  const qaOf = (art?: { qa?: Record<string, unknown> }) => art?.qa || {};
  return (
    <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 10, marginBottom: 8 }}>
      <Space wrap size={4}>
        <Tag>Candidate: {row.candidateCode}</Tag>
        <Tag color={row.status === 'PASS' ? 'green' : row.status === 'FAIL' ? 'red' : 'default'}>{row.status || 'NOT RUN'}</Tag>
        <Tag>
          {passed} / 10 PASS
        </Tag>
        <Tag>P0 {row.p0}</Tag>
        {row.directorDecision ? <Tag>Director {row.directorDecision}</Tag> : <Tag>DIRECTOR PENDING</Tag>}
      </Space>
      <div style={{ fontSize: 12, color: '#475569', margin: '8px 0' }}>
        Identity {row.identityScore ?? 0} · Structure {row.facialStructureScore ?? 0} · Hair {row.hairScore ?? 0} · Age {row.ageConsistencyScore ?? 0} · Style {row.styleConsistencyScore ?? 0}
      </div>
      {(() => {
        const st10 = latest.get('ST-10');
        const diag = diagnoseCollapsedStressQa(qaOf(st10));
        if (!diag) return null;
        const why = diag.p0Provenance && typeof diag.p0Provenance === 'object'
          ? Object.entries(diag.p0Provenance as Record<string, string>).map(([k, v]) => `${k}: ${v}`).join(' · ')
          : '';
        const passed = st10?.qaStatus === 'PASS';
        return (
          <Alert
            type={passed ? 'success' : diag.collapsedCascade ? 'warning' : 'info'}
            showIcon
            style={{ marginBottom: 8 }}
            message={passed ? 'ST-10 đã chấm lại — PASS' : 'ST-10 provenance'}
            description={
              <div style={{ fontSize: 12 }}>
                <div>Source: {row.candidateCode} · SHA {row.sourceSha256?.slice(0, 16) || '—'} · ref {diag.referenceAttached === false ? 'NOT attached' : '004-D'} · target {String(diag.visionTarget || 'FULL_FRAME')} · crop Minh: {diag.minhCrop === false ? 'no' : 'yes'}</div>
                <div>Contract: {String(diag.contractPurpose || 'MASTER_REFERENCE')} · expected {String(diag.expectedCharacters ?? (diag.imageContract ? '1' : '?'))} · detected {String(diag.detectedCharacters ?? '—')} · scoreMode {String(diag.scoreMode || '')}</div>
                {diag.collapsedCascade ? <div>P0=6 + ID 40 = cascade từ 1 Vision FAIL — không phải 6 feature độc lập. Không regenerate. Bấm Phân tích để chấm lại artifact hiện có.</div> : null}
                {passed ? <div>Vision PASS · ID {String((qaOf(st10) as { identityScore?: number }).identityScore ?? '—')} · expected 2 / detected {String(diag.detectedCharacters ?? '—')} · 004-D attached. Không phải Master.</div> : null}
                {why ? <div>{why}</div> : null}
              </div>
            }
          />
        );
      })()}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginBottom: 10 }}>
        {STRESS_CASES.map((c) => {
          const art = latest.get(c.code);
          const qa = qaOf(art);
          const score = typeof qa.identityScore === 'number' ? qa.identityScore : '—';
          const diag = diagnoseCollapsedStressQa(qa);
          return (
            <div key={c.code} style={{ width: 160, flex: '0 0 160px', textAlign: 'center' }}>
              {art && previews[art.id] ? (
                <img
                  src={previews[art.id]}
                  alt={c.code}
                  style={{ width: 160, height: 200, objectFit: 'contain', borderRadius: 8, background: '#f8fafc' }}
                />
              ) : (
                <div style={{ width: 160, height: 200, background: '#f1f5f9', borderRadius: 8, fontSize: 12, color: '#94a3b8', display: 'grid', placeItems: 'center' }}>
                  {c.code}
                </div>
              )}
              <div style={{ fontSize: 12, color: '#334155', marginTop: 4 }}>
                {c.code} {c.label}
              </div>
              <div style={{ fontSize: 11, color: '#64748b' }}>
                {art?.qaStatus || 'NOT RUN'} · ID {score} · P0 {Array.isArray(qa.p0) ? (qa.p0 as string[]).length : 0}
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8', wordBreak: 'break-all' }}>{art?.sha256 ? art.sha256.slice(0, 12) : ''}</div>
              {diag ? (
                <div style={{ fontSize: 10, color: '#b45309', textAlign: 'left', marginTop: 4 }}>
                  {String(diag.root || 'DIAG')} · ref {diag.referenceAttached === false ? 'NONE' : '004-D'} · target {String(diag.visionTarget || 'FULL_FRAME')}
                  {diag.collapsedCascade ? ' · P0 cascade từ 1 Vision FAIL' : ''}
                </div>
              ) : null}
              <Button size="small" type="link" disabled={busy || !dnaOk} onClick={() => onRun(c.code)} style={{ padding: 0, height: 'auto' }}>
                {art && art.qaStatus !== 'PASS' ? 'Regenerate case' : 'Chạy'}
              </Button>
            </div>
          );
        })}
      </div>
      <Input.TextArea
        rows={2}
        value={repairText}
        onChange={(e) => onRepairText(e.target.value)}
        placeholder="STRESS_DIAGNOSIS — ví dụ: ST-08 giữ face 004-D, occlusion nhẹ, age 11."
        style={{ marginBottom: 8 }}
      />
      <Space wrap>
        <Button size="small" type="primary" disabled={busy || !dnaOk} onClick={() => onRun()}>
          Chạy Stress Test
        </Button>
        <Button size="small" loading={busy} disabled={busy} onClick={onAnalyze}>
          Phân tích lại {repairCase || 'ST-10'}
        </Button>
        <Button size="small" disabled={busy} onClick={onPass}>
          Director PASS
        </Button>
        <Button size="small" danger disabled={busy} onClick={onReject}>
          Reject
        </Button>
        <Input
          size="small"
          value={repairCase}
          onChange={(e) => onRepairCase(e.target.value.toUpperCase())}
          style={{ width: 80 }}
        />
        <Button size="small" disabled={busy || !dnaOk} onClick={onRepair}>
          Đưa về Repair
        </Button>
        <Button size="small" disabled={busy || !canPromote} onClick={onPromote}>
          Đưa sang Master Review
        </Button>
      </Space>
    </div>
  );
}
