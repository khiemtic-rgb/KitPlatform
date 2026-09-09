import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Input, Space, Tag, Typography } from 'antd';
import {
  analyzeKitVideoMasterCandidate,
  approveKitVideoMasterReference,
  compareKitVideoMasterCandidates,
  compileKitVideoMasterReference,
  fetchFamixaCharacter,
  fetchKitVideoMasterCandidateObjectUrl,
  fetchKitVideoMasterReference,
  generateKitVideoMasterCandidate,
  lockKitVideoMasterReference,
  markKitVideoMasterFrontRunner,
  selectKitVideoMasterCandidate,
  type KitVideoMasterCandidateRow,
  type KitVideoMasterReferenceRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { dnaApproved } from './content-famixa-minh-visual-dna';
import { styleReadyForMaster } from './content-famixa-visual-style';
import { parseFamixaCanon } from './content-famixa-character-memory';
import { canLockMaster, CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1 } from './kit-video-master-reference';
import { lookStatusVi } from './content-famixa-look-status';
import { canDirectorSelect, ensureCanVary, evaluateSelection, markFrontRunner } from './kit-video-master-selection';

function candidateRepairNote(c: KitVideoMasterCandidateRow) {
  if ((c.diagnosis || '').trim()) return c.diagnosis!.trim();
  const qa = c.qa || {};
  for (const key of ['notes', 'summary', 'reason', 'diagnosis']) {
    const v = qa[key];
    if (typeof v === 'string' && v.trim()) return v.trim();
  }
  return '';
}

function humanGenerateBlock(raw?: string) {
  const s = raw || '';
  if (/DO_NOT_BLIND_RETRY/i.test(s)) {
    return 'Máy từ chối vẽ lại cùng một lệnh. Ghi chỗ sai vào ô bên dưới, rồi bấm «Vẽ lại (đã ghi chỗ sai)».';
  }
  if (/CREDIT_GATE/i.test(s)) return 'Chưa xác nhận — máy chưa gọi vẽ.';
  if (/BATCH_FULL/i.test(s)) return 'Đợt này đủ 4 ảnh. So sánh rồi mới vẽ đợt mới.';
  if (/DIRECTOR_REVIEW_REQUIRED/i.test(s)) return 'Đã thử 3 lần cùng lệnh. Director phải xem trước khi vẽ tiếp.';
  if (/MASTER_LOCKED/i.test(s)) return 'Ảnh hộ chiếu đã khóa. Muốn đổi thì làm bản V2.';
  if (/SELECTION_GATE|MASTER_NOT_ELIGIBLE/i.test(s)) {
    return 'Chưa chọn được hộ chiếu. Ảnh phải ELIGIBLE, PRODUCTION_STILL, Vision PASS, không P0 — DRAFT/REVIEW không chọn được.';
  }
  if (/FRONT_RUNNER_FULL/i.test(s)) return 'Chung kết đủ 3 ứng viên dẫn đầu.';
  if (/FRONT_RUNNER/i.test(s)) return 'Ảnh này không vào chung kết (loại / ảnh thử kiểm chứng).';
  if (/VARIATION_FULL/i.test(s)) return 'Đã đủ 5 variation (A–E) từ ảnh gốc này.';
  if (/VARIATION/i.test(s)) return 'Không vẽ variation từ ảnh đã loại.';
  return s;
}

function selectGateOf(c: KitVideoMasterCandidateRow, dnaOk: boolean) {
  const qaSha = typeof c.qa?.sha256 === 'string' ? c.qa.sha256 : undefined;
  const result = evaluateSelection({
    projectCode: 'FAMIXA',
    characterId: c.characterId || 'CHAR-001',
    eraId: c.eraId || 'ERA-01',
    candidateId: c.id,
    artifactPath: c.artifactPath,
    sha256: c.sha256,
    liveSha256: c.sha256,
    qaSha256: qaSha,
    artifactExists: Boolean(c.artifactPath && c.sha256),
    readable: Boolean(c.artifactPath && c.sha256),
    imageType: c.imageType,
    visionPass: c.qaStatus === 'PASS',
    qaStatus: c.qaStatus,
    controlledTest: c.canonEligible === false,
    dnaApproved: dnaOk,
    lifecycle: c.lifecycle,
  });
  return canDirectorSelect({
    result,
    dnaApproved: dnaOk,
    lifecycle: c.lifecycle,
    imageType: c.imageType,
  });
}

const PHASES = [
  { id: 'identity', title: '1. Ảnh mặt thẳng', hint: 'Tìm khuôn mặt Minh. Nền đơn, không cảnh phim.' },
  { id: 'angles', title: '2. Nhiều góc', hint: 'Thẳng · nghiêng trái · nghiêng phải · nghiêng hẳn — cùng một người.' },
  { id: 'expr', title: '3. Cảm xúc', hint: 'Bình thường / vui / buồn / tổn thương / giận — mặt không đổi người.' },
  { id: 'body', title: '4. Cả người', hint: 'Đúng trẻ 11 tuổi. Không cao như thiếu niên.' },
  { id: 'wardrobe', title: '5. Nhà / trường', hint: 'Đổi áo vẫn nhận ra Minh. Áo không phải giấy tờ tùy thân.' },
  { id: 'review', title: '6. Director duyệt', hint: 'Máy không tự chọn mặt Canon.' },
];

export function ContentKitVideoMasterReferenceCard() {
  const [row, setRow] = useState<KitVideoMasterReferenceRow>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [compile, setCompile] = useState<string>();
  const [dnaOk, setDnaOk] = useState(false);
  const [styleOk, setStyleOk] = useState(false);
  const [diagnosis, setDiagnosis] = useState('');
  const [previews, setPreviews] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState<string>();
  const [compareNote, setCompareNote] = useState<string>();

  const applyRow = (next: KitVideoMasterReferenceRow) => {
    setRow(next);
    setError(undefined);
  };

  const load = () => {
    setBusy(true);
    void Promise.all([fetchKitVideoMasterReference('FAMIXA', 'CHAR-001'), fetchFamixaCharacter('CHAR-001')])
      .then(([master, character]) => {
        const canon = parseFamixaCanon(character.canon);
        applyRow(master);
        setDnaOk(dnaApproved(canon.visualDna));
        setStyleOk(styleReadyForMaster(canon.famixaVisualStyle));
        const lastNote = [...(master.candidates || [])]
          .reverse()
          .map((c) => candidateRepairNote(c))
          .find((note) => note);
        if (lastNote) setDiagnosis((cur) => cur.trim() || lastNote);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được gói ảnh hộ chiếu — đang hiện bản mô tả local.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
    const refresh = () => load();
    window.addEventListener('famixa-look-changed', refresh);
    return () => window.removeEventListener('famixa-look-changed', refresh);
  }, []);

  useEffect(() => {
    const ids = (row?.candidates || []).filter((c) => c.artifactPath && c.sha256).map((c) => c.id);
    let gone = false;
    const urls: Record<string, string> = {};
    void Promise.all(
      ids.map((id) =>
        fetchKitVideoMasterCandidateObjectUrl(id)
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
  }, [row?.candidates]);

  const spec = CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1;
  const status = (row?.status || spec.status).toUpperCase();
  const chip = lookStatusVi(status);
  const candidates = row?.candidates ?? [];
  const pick = candidates.find((c) => c.status === 'APPROVED') || candidates[candidates.length - 1];
  const lock = canLockMaster({
    status,
    directorApproved: status === 'APPROVED',
    hashes: pick?.sha256 ? { artifact: pick.sha256, reference: pick.sha256, approval: pick.sha256 } : {},
    artifactExists: Boolean(pick?.artifactPath && pick.sha256),
    visionPass: pick?.qaStatus === 'PASS',
    identityPass: pick?.qaStatus === 'PASS',
  });
  const waitingDna = !dnaOk;
  const locked = status === 'LOCKED';
  const canApprove = Boolean(pick && pick.qaStatus === 'PASS' && pick.canonEligible && !locked && status !== 'APPROVED');

  const runGenerate = (view: string, expression: string, withDiagnosis: boolean) => {
    if (withDiagnosis && !diagnosis.trim()) {
      setNotice('Ghi rõ chỗ sai trước khi vẽ lại. Không bấm lại mù.');
      return;
    }
    setBusy(true);
    setNotice(undefined);
    void generateKitVideoMasterCandidate({
      confirmed: true,
      controlledTest: waitingDna || !styleOk,
      view,
      expression,
      diagnosis: withDiagnosis ? diagnosis.trim() : undefined,
    })
      .then((next) => {
        applyRow(next.package);
        setNotice(
          next.blocked
            ? humanGenerateBlock(next.blocked)
            : `Đã tạo ${next.candidateCode}. Ảnh thử — chưa phải Canon. Director: PENDING. Khóa: không.`,
        );
      })
      .catch((e) => setError(humanGenerateBlock(apiErrorMessage(e, 'Chưa tạo được ảnh thử.'))))
      .finally(() => setBusy(false));
  };

  return (
    <Card className="fx-look__card" size="small" title="Bước 3 · Ảnh hộ chiếu của Minh">
      <p className="fx-look__help">
        Ảnh hộ chiếu giúp máy <b>nhớ đúng một Minh</b> qua nhiều tập. Không phải ảnh đẹp một cảnh. Không lấy shot thử
        máy hay ảnh cũ làm mặt chính thức.
      </p>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={chip.color}>{chip.label}</Tag>
        <Tag>11 tuổi</Tag>
        <Tag>Ảnh thử: {candidates.length}</Tag>
        {waitingDna ? <Tag color="gold">Ảnh thử kiểm chứng — không khóa Canon</Tag> : null}
      </Space>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}
      <Alert
        type={waitingDna ? 'warning' : 'info'}
        showIcon
        style={{ marginBottom: 8 }}
        message={waitingDna ? 'DNA Minh chưa duyệt — chỉ được tạo ảnh thử để kiểm chứng máy.' : chip.hint}
        description={
          waitingDna
            ? 'Máy không tự chọn mặt, không duyệt, không khóa. Production không dùng ảnh này.'
            : 'Cần nhiều góc và vài cảm xúc. Một ảnh đẹp mà sai mặt = loại.'
        }
      />

      <div style={{ display: 'grid', gap: 8, marginBottom: 8 }}>
        {PHASES.map((phase) => (
          <div key={phase.id} style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: '8px 10px' }}>
            <b>{phase.title}</b>
            <div style={{ color: '#64748b', fontSize: 12 }}>{phase.hint}</div>
          </div>
        ))}
      </div>

      <Input.TextArea
        size="small"
        rows={3}
        value={diagnosis}
        onChange={(e) => setDiagnosis(e.target.value)}
        placeholder="Ghi chỗ sai rồi bấm Vẽ lại. Ví dụ: mắt hơi anime, mặt generic, hơi non hơn 11 tuổi…"
        style={{ marginBottom: 8 }}
      />
      <Space wrap style={{ marginBottom: 8 }}>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
        <Button size="small" loading={busy} disabled={locked} onClick={() => runGenerate('FRONT', 'NEUTRAL', false)}>
          Tạo ảnh thử
        </Button>
        <Button size="small" type="primary" loading={busy} disabled={locked} onClick={() => runGenerate('FRONT', 'NEUTRAL', true)}>
          Vẽ lại (đã ghi chỗ sai)
        </Button>
        <Button
          size="small"
          disabled={!canApprove || busy}
          onClick={() => {
            if (!pick) return;
            setBusy(true);
            void approveKitVideoMasterReference({ candidateId: pick.id, decision: 'APPROVE' })
              .then(applyRow)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa duyệt được.')))
              .finally(() => setBusy(false));
          }}
        >
          Director duyệt
        </Button>
        <Button
          size="small"
          disabled={!lock.ok || busy}
          onClick={() => {
            setBusy(true);
            void lockKitVideoMasterReference()
              .then(applyRow)
              .catch((e) => setError(apiErrorMessage(e, 'Chưa khóa được — hash / duyệt chưa đủ.')))
              .finally(() => setBusy(false));
          }}
        >
          Khóa ảnh hộ chiếu
        </Button>
      </Space>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 8 }}
        message="Máy chỉ đề xuất. Director mới được chọn mặt Canon."
        description="Điểm cao không thắng lỗi P0. DRAFT/REVIEW không chọn hộ chiếu. FRONT_RUNNER chỉ là vòng chung kết — chưa phải Canon."
      />
      <Space wrap style={{ marginBottom: 8 }}>
        <Button
          size="small"
          loading={busy}
          disabled={!candidates.length}
          onClick={() => {
            setBusy(true);
            void compareKitVideoMasterCandidates()
              .then((next) => {
                applyRow(next.package);
                const rec = next.ranks.find((r) => r.id === next.recommendedCandidateId);
                setCompareNote(
                  next.autoSelected
                    ? 'Lỗi: máy không được tự chọn.'
                    : rec
                      ? `Đề xuất (không tự khóa): ${rec.candidateCode} · ${rec.score} · ${rec.recommendation}`
                      : 'Chưa có ảnh đủ điều kiện. So sánh xong — không tự chọn.',
                );
              })
              .catch((e) => setError(apiErrorMessage(e, 'Chưa so sánh được.')))
              .finally(() => setBusy(false));
          }}
        >
          So sánh ảnh thử
        </Button>
      </Space>
      {compareNote ? <Alert type="success" showIcon message={compareNote} style={{ marginBottom: 8 }} /> : null}

      <CandidateList
        candidates={candidates}
        previews={previews}
        busy={busy}
        locked={locked}
        onAnalyze={(id) => {
          setBusy(true);
          void analyzeKitVideoMasterCandidate(id)
            .then(applyRow)
            .catch((e) => setError(apiErrorMessage(e, 'Chưa phân tích được.')))
            .finally(() => setBusy(false));
        }}
        onDecide={(id, decision) => {
          const note = candidates.find((c) => c.id === id);
          if (decision === 'REQUEST_REVISION' && note) {
            const text = candidateRepairNote(note);
            if (text) setDiagnosis((cur) => cur.trim() || text);
          }
          setBusy(true);
          void approveKitVideoMasterReference({ candidateId: id, decision })
            .then(applyRow)
            .catch((e) => setError(apiErrorMessage(e, 'Chưa ghi được quyết định.')))
            .finally(() => setBusy(false));
        }}
        onSelect={(id) => {
          setBusy(true);
          void selectKitVideoMasterCandidate(id)
            .then(applyRow)
            .catch((e) => setError(apiErrorMessage(e, 'Chưa chọn được Master — DNA / P0 / ảnh thử Canon chưa đủ.')))
            .finally(() => setBusy(false));
        }}
        onFrontRunner={(id) => {
          setBusy(true);
          void markKitVideoMasterFrontRunner(id)
            .then(applyRow)
            .catch((e) => setError(apiErrorMessage(e, 'Chưa đánh dấu ứng viên dẫn đầu.')))
            .finally(() => setBusy(false));
        }}
        onVary={(id) => {
          const parent = candidates.find((c) => c.id === id);
          setBusy(true);
          void generateKitVideoMasterCandidate({
            confirmed: true,
            controlledTest: waitingDna || !styleOk,
            parentCandidateId: id,
            diagnosis: diagnosis.trim() || `Variation of ${parent?.candidateCode || 'parent'}. Same boy. Small look change only.`,
          })
            .then((next) => {
              applyRow(next.package);
              setNotice(
                next.blocked
                  ? humanGenerateBlock(next.blocked)
                  : `Đã tạo ${next.candidateCode} — variation của ${parent?.candidateCode}. Chưa phải Canon.`,
              );
            })
            .catch((e) => setError(apiErrorMessage(e, 'Chưa tạo được variation.')))
            .finally(() => setBusy(false));
        }}
        canSelect={dnaOk && !locked}
        dnaOk={dnaOk}
      />

      <Collapse
        size="small"
        style={{ marginTop: 8 }}
        items={[
          {
            key: 'tech',
            label: 'Dành cho kỹ thuật (không cần để hiểu Minh)',
            children: (
              <Space direction="vertical" style={{ width: '100%' }}>
                <Button
                  size="small"
                  loading={busy}
                  onClick={() => {
                    setBusy(true);
                    void compileKitVideoMasterReference('FRONT', dnaOk, styleOk)
                      .then((next) => setCompile(next.ok ? next.prompt : next.blocked || 'Chưa được soạn lệnh.'))
                      .catch((e) => setCompile(apiErrorMessage(e, 'Chưa soạn được lệnh.')))
                      .finally(() => setBusy(false));
                  }}
                >
                  Soạn lệnh vẽ (không gọi máy vẽ)
                </Button>
                {compile ? (
                  <Typography.Paragraph type="secondary" style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>
                    {compile}
                  </Typography.Paragraph>
                ) : null}
                <Typography.Text type="secondary">
                  Compiler soạn lệnh. UI không nối chuỗi. Ảnh đẹp không thắng sai mặt. Khóa rồi thì chỉ làm bản V2.
                </Typography.Text>
              </Space>
            ),
          },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
        Máy không tự chọn mặt Minh. Chưa có Director duyệt thì Master Reference = chưa khóa — đúng.
      </Typography.Paragraph>
    </Card>
  );
}

function CandidateList({
  candidates,
  previews,
  busy,
  locked,
  onAnalyze,
  onDecide,
  onSelect,
  onFrontRunner,
  onVary,
  canSelect,
  dnaOk,
}: {
  candidates: KitVideoMasterCandidateRow[];
  previews: Record<string, string>;
  busy: boolean;
  locked: boolean;
  onAnalyze: (id: string) => void;
  onDecide: (id: string, decision: 'REJECT' | 'REQUEST_REVISION') => void;
  onSelect: (id: string) => void;
  onFrontRunner: (id: string) => void;
  onVary: (id: string) => void;
  canSelect: boolean;
  dnaOk: boolean;
}) {
  if (!candidates.length) {
    return (
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        Chưa có ảnh thử. Bấm «Tạo ảnh thử» để kiểm chứng Gemini + Vision. Không tự khóa.
      </Typography.Paragraph>
    );
  }
  return (
    <div style={{ display: 'grid', gap: 8, marginBottom: 8 }}>
      {candidates.map((c) => {
        const p0 = Array.isArray(c.qa?.p0) ? (c.qa.p0 as string[]).join(', ') : '';
        const parent = candidates.find((x) => x.id === c.parentCandidateId || x.id === c.sourceCandidateId);
        const kids = candidates.filter((x) => x.parentCandidateId === c.id || x.sourceCandidateId === c.id).length;
        const runnerCount = candidates.filter((x) => x.frontRunner || x.lifecycle === 'FRONT_RUNNER').length;
        const runner = markFrontRunner({
          locked,
          lifecycle: c.lifecycle,
          recommendation: c.recommendation,
          controlledTest: c.canonEligible === false,
          canonEligible: c.canonEligible,
          frontRunnerCount: runnerCount,
          alreadyFrontRunner: c.frontRunner || c.lifecycle === 'FRONT_RUNNER',
        });
        const vary = ensureCanVary({
          locked,
          parentLifecycle: c.lifecycle,
          parentRecommendation: c.recommendation,
          existingVariations: kids,
        });
        const pick = selectGateOf(c, dnaOk);
        return (
          <div
            key={c.id}
            style={{
              display: 'grid',
              gridTemplateColumns: '200px 1fr',
              gap: 12,
              border: `1px solid ${c.frontRunner || c.lifecycle === 'FRONT_RUNNER' ? '#93c5fd' : '#e2e8f0'}`,
              borderRadius: 8,
              padding: 8,
              marginLeft: parent ? 16 : 0,
            }}
          >
            {previews[c.id] ? (
              <img src={previews[c.id]} alt={c.candidateCode} style={{ width: 200, height: 280, objectFit: 'contain', borderRadius: 8, background: '#f8fafc' }} />
            ) : (
              <div style={{ width: 200, height: 280, borderRadius: 8, background: '#f1f5f9', color: '#94a3b8', fontSize: 12, display: 'grid', placeItems: 'center' }}>
                Chưa có ảnh
              </div>
            )}
            <div>
              <Space wrap size={4}>
                <Tag>{c.candidateCode}</Tag>
                <Tag>Lần {c.generationAttempt}</Tag>
                <Tag color={c.qaStatus === 'PASS' ? 'green' : c.qaStatus === 'FAIL' ? 'red' : 'default'}>{c.qaStatus}</Tag>
                <Tag>{c.status}</Tag>
                {!c.canonEligible ? <Tag color="gold">Không phải Canon</Tag> : null}
                {c.recommendation ? <Tag>{c.recommendation}</Tag> : null}
                {c.lifecycle ? <Tag>{c.lifecycle}</Tag> : null}
                {c.frontRunner || c.lifecycle === 'FRONT_RUNNER' ? <Tag color="blue">FRONT_RUNNER</Tag> : null}
                {parent ? <Tag>Con của {parent.candidateCode}</Tag> : null}
                {typeof c.score === 'number' && c.score > 0 ? <Tag>Điểm {c.score}</Tag> : null}
              </Space>
              <div style={{ fontSize: 12, color: '#475569', marginTop: 4 }}>
                Hash: {c.sha256 ? `${c.sha256.slice(0, 12)}…` : '—'}
                {c.fingerprint ? ` · fp ${c.fingerprint.slice(0, 8)}` : ''}
              </div>
              <div style={{ fontSize: 12, color: '#64748b' }}>
                QA: {p0 || c.qaStatus} {c.diagnosis ? `· ${c.diagnosis}` : ''}
              </div>
              <Space wrap size={4} style={{ marginTop: 6 }}>
                <Button size="small" loading={busy} onClick={() => onAnalyze(c.id)}>
                  Phân tích
                </Button>
                <Button size="small" disabled={locked || busy} onClick={() => onDecide(c.id, 'REQUEST_REVISION')}>
                  Yêu cầu sửa
                </Button>
                <Button size="small" danger disabled={locked || busy} onClick={() => onDecide(c.id, 'REJECT')}>
                  Loại
                </Button>
                <Button size="small" disabled={locked || busy || !runner.ok} onClick={() => onFrontRunner(c.id)}>
                  Đưa vào chung kết
                </Button>
                <Button size="small" disabled={locked || busy || !vary.ok} onClick={() => onVary(c.id)}>
                  Tạo variation
                </Button>
                <Button
                  size="small"
                  type="primary"
                  disabled={locked || busy || !canSelect || !pick.ok}
                  onClick={() => onSelect(c.id)}
                >
                  Chọn làm hộ chiếu
                </Button>
              </Space>
            </div>
          </div>
        );
      })}
    </div>
  );
}
