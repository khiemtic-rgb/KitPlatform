import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Input, Space, Tag, Typography } from 'antd';
import {
  fetchKitVideoIdentityArtifactObjectUrl,
  fetchKitVideoIdentityStressArtifactObjectUrl,
  fetchKitVideoMasterCandidateObjectUrl,
  fetchKitVideoMasterReference,
  listKitVideoMasterReview,
  openKitVideoMasterReview,
  passKitVideoMasterReview,
  rejectKitVideoMasterReview,
  type KitVideoMasterCandidateRow,
  type KitVideoMasterReviewRow,
  type KitVideoMasterReviewShotRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { REVIEW_ALLOWED_CANDIDATE, REVIEW_PARENT, REVIEW_VARIATION, reviewRegressionStatus } from './kit-video-master-review';

export function ContentKitVideoMasterReviewCard() {
  const { modal, message } = App.useApp();
  const [candidates, setCandidates] = useState<KitVideoMasterCandidateRow[]>([]);
  const [reviews, setReviews] = useState<KitVideoMasterReviewRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [note, setNote] = useState('');
  const [previews, setPreviews] = useState<Record<string, string>>({});

  const load = () => {
    setBusy(true);
    void Promise.all([fetchKitVideoMasterReference('FAMIXA', 'CHAR-001'), listKitVideoMasterReview()])
      .then(([master, rows]) => {
        setCandidates(master.candidates || []);
        setReviews(rows);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Master Review.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    const eligible = candidates.find((c) => c.candidateCode === REVIEW_ALLOWED_CANDIDATE);
    const shots = reviews.flatMap((r) => [...r.identityShots, ...r.stressShots]).filter((s) => s.id && s.sha256);
    let gone = false;
    const urls: Record<string, string> = {};
    void Promise.all([
      eligible
        ? fetchKitVideoMasterCandidateObjectUrl(eligible.id)
            .then((url) => {
              urls[`cand:${eligible.id}`] = url;
            })
            .catch(() => undefined)
        : Promise.resolve(),
      ...shots.map((s) =>
        (s.kind === 'IDENTITY_STRESS' ? fetchKitVideoIdentityStressArtifactObjectUrl : fetchKitVideoIdentityArtifactObjectUrl)(s.id)
          .then((url) => {
            urls[s.id] = url;
          })
          .catch(() => undefined),
      ),
    ]).then(() => {
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
  }, [reviews, candidates]);

  const target = candidates.find((c) => c.candidateCode === REVIEW_ALLOWED_CANDIDATE);
  const apply = (row: KitVideoMasterReviewRow, msg?: string) => {
    setReviews((cur) => [row, ...cur.filter((r) => r.id !== row.id)]);
    setNotice(msg);
    setError(undefined);
  };

  return (
    <Card size="small" title="Master Review — CHAR-001 / MINH / ERA-01" style={{ marginBottom: 12 }}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        Chỉ {REVIEW_ALLOWED_CANDIDATE} sau Identity 7/7 + Stress 10/10. Director PASS = MASTER_REFERENCE_V1 LOCKED. Không tạo ảnh. Không tự chọn. Không đụng Golden SH01-01.
      </Typography.Paragraph>
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {notice ? <Alert type="info" showIcon message={notice} style={{ marginBottom: 8 }} /> : null}
      <Space wrap style={{ marginBottom: 8 }}>
        <Button
          size="small"
          type="primary"
          disabled={busy || !target}
          onClick={() => {
            setBusy(true);
            void openKitVideoMasterReview(target?.id)
              .then((row) => apply(row, 'Đã mở Master Review — chưa chọn hộ chiếu.'))
              .catch((e) => setError(apiErrorMessage(e, 'Chưa mở được Master Review.')))
              .finally(() => setBusy(false));
          }}
        >
          Mở Master Review · {REVIEW_ALLOWED_CANDIDATE}
        </Button>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
      </Space>
      {reviews.map((row) => (
        <ReviewBlock
          key={row.id}
          row={row}
          candidate={target}
          previews={previews}
          busy={busy}
          note={note}
          onNote={setNote}
          onPass={() => {
            modal.confirm({
              title: 'DIRECTOR PASS',
              okText: 'DIRECTOR PASS',
              cancelText: 'HỦY',
              centered: true,
              zIndex: 3100,
              content: (
                <div>
                  Khóa {REVIEW_ALLOWED_CANDIDATE} thành CHAR-001_MASTER_REFERENCE_V1 (LOCKED).
                  <br />
                  Không tạo ảnh mới. Không đụng Golden SH01-01. Muốn đổi sau phải tạo V2.
                </div>
              ),
              onOk: () => {
                setBusy(true);
                return passKitVideoMasterReview(row.id, note.trim())
                  .then((r) => {
                    apply(r, 'DIRECTOR APPROVED · MASTER REFERENCE LOCKED.');
                    message.success('DIRECTOR APPROVED · MASTER REFERENCE LOCKED');
                  })
                  .catch((e) => {
                    const msg = apiErrorMessage(e, 'Chưa ghi được Director PASS.');
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
            void rejectKitVideoMasterReview(row.id, note.trim())
              .then((r) => {
                apply(r, 'REJECT / RETURN. Không tự Canon.');
                message.success('REJECT / RETURN');
              })
              .catch((e) => {
                const msg = apiErrorMessage(e, 'Chưa reject được.');
                setError(msg);
                message.error(msg);
              })
              .finally(() => setBusy(false));
          }}
        />
      ))}
    </Card>
  );
}

function ReviewBlock({
  row,
  candidate,
  previews,
  busy,
  note,
  onNote,
  onPass,
  onReject,
}: {
  row: KitVideoMasterReviewRow;
  candidate?: KitVideoMasterCandidateRow;
  previews: Record<string, string>;
  busy: boolean;
  note: string;
  onNote: (v: string) => void;
  onPass: () => void;
  onReject: () => void;
}) {
  const hero = candidate ? previews[`cand:${candidate.id}`] : undefined;
  const locked = row.status === 'LOCKED' || row.canon || !!row.lock;
  const directorApproved = locked || ['PASS', 'SELECTED', 'APPROVED'].includes(row.status);
  const regression = reviewRegressionStatus({
    candidateCode: row.candidateCode,
    identityPass: row.identityPass,
    identityHave: row.identityHave,
    identityP0: row.identityP0,
    stressPass: row.stressPass,
    stressHave: row.stressHave,
    stressP0: row.stressP0,
    st10Pass: row.st10Pass,
    dnaApproved: row.dnaApproved,
  });
  return (
    <div style={{ border: '1px solid #e2e8f0', borderRadius: 8, padding: 10, marginBottom: 8 }}>
      <Space wrap size={4}>
        <Tag>Candidate: {row.candidateCode}</Tag>
        <Tag color={locked ? 'green' : row.status === 'REJECTED' ? 'red' : 'blue'}>{locked ? 'MASTER REFERENCE LOCKED' : row.status}</Tag>
        <Tag>{row.lock?.masterCode || row.masterRefCode}</Tag>
        {directorApproved ? <Tag color="green">DIRECTOR APPROVED</Tag> : row.directorDecision ? <Tag>Director {row.directorDecision}</Tag> : <Tag>DIRECTOR PENDING</Tag>}
      </Space>

      <div style={{ display: 'grid', gridTemplateColumns: '160px 1fr', gap: 12, margin: '10px 0' }}>
        {hero ? (
          <img src={hero} alt={row.candidateCode} style={{ width: 160, height: 200, objectFit: 'contain', borderRadius: 8, background: '#f8fafc' }} />
        ) : (
          <div style={{ width: 160, height: 200, background: '#f1f5f9', borderRadius: 8 }} />
        )}
        <div>
          <Typography.Text strong>A. Candidate Identity</Typography.Text>
          <div style={{ fontSize: 12, color: '#475569', marginTop: 4 }}>
            {row.candidateCode}
            <br />
            Variation: {row.variation || REVIEW_VARIATION}
            <br />
            Parent: {row.parentCode || REVIEW_PARENT}
            <br />
            SHA {row.sourceSha256?.slice(0, 16) || '—'}
          </div>
        </div>
      </div>

      <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 6 }}>IDENTITY REVIEW</div>
      <Space wrap size={4} style={{ marginBottom: 8 }}>
        <Tag color={row.identityPass ? 'green' : 'red'}>Identity Test {row.identityHave}/{row.identityRequired} {row.identityPass ? 'PASS' : 'FAIL'}</Tag>
        <Tag color={row.stressPass ? 'green' : 'red'}>Stress Test {row.stressHave}/{row.stressRequired} {row.stressPass ? 'PASS' : 'FAIL'}</Tag>
        <Tag color={row.identityP0 + row.stressP0 === 0 ? 'green' : 'red'}>P0 {row.identityP0 + row.stressP0}</Tag>
        <Tag color={row.dnaApproved ? 'green' : 'orange'}>DNA {row.dnaApproved ? 'APPROVED' : '—'}</Tag>
        <Tag color={regression.ok ? 'green' : 'red'}>REGRESSION {regression.label}</Tag>
      </Space>
      {row.blocked && !locked ? <Alert type="warning" showIcon message={row.blocked} style={{ marginBottom: 8 }} /> : null}

      <Typography.Text strong>B. Identity Test</Typography.Text>
      <ShotStrip shots={row.identityShots} previews={previews} />
      <Typography.Text strong>C. Identity Stress Test</Typography.Text>
      <ShotStrip shots={row.stressShots} previews={previews} />

      <Typography.Text strong>D. DIRECTOR DECISION</Typography.Text>
      <div style={{ fontSize: 12, color: '#334155', margin: '8px 0' }}>
        <div>Identity Test {row.identityHave}/{row.identityRequired} {row.identityPass ? 'PASS' : 'FAIL'}</div>
        <div>Stress Test {row.stressHave}/{row.stressRequired} {row.stressPass ? 'PASS' : 'FAIL'}</div>
        <div>P0 {row.identityP0 + row.stressP0}</div>
        <div>DNA {row.dnaApproved ? 'APPROVED' : '—'}</div>
        <div>REGRESSION {regression.label} · Golden SH01-01 untouched · no generate</div>
      </div>
      {locked ? (
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 8 }}
          message="DIRECTOR APPROVED · MASTER REFERENCE LOCKED"
          description={
            <div style={{ fontSize: 12 }}>
              <div>MASTER REFERENCE · {row.lock?.masterCode || row.masterRefCode}</div>
              <div>Character: Minh · Source: {row.lock?.sourceCandidateCode || row.candidateCode}</div>
              <div>Identity Test: {row.lock?.identityTestResult || `${row.identityHave}/7 PASS`}</div>
              <div>Stress Test: {row.lock?.stressTestResult || `${row.stressHave}/10 PASS`}</div>
              <div>P0: 0 · DNA: APPROVED · STATUS: LOCKED</div>
              <div>SHA256 {row.lock?.sha256 || row.sourceSha256}</div>
              <div>Approved by {row.lock?.lockedBy || '—'} · {row.lock?.lockedAt || '—'}</div>
            </div>
          }
        />
      ) : (
        <>
          <Input.TextArea
            rows={2}
            value={note}
            onChange={(e) => onNote(e.target.value)}
            placeholder="MASTER REVIEW NOTE"
            style={{ margin: '8px 0' }}
          />
          <Space wrap>
            <Button size="small" type="primary" disabled={busy || !row.canDirectorPass} onClick={onPass}>
              DIRECTOR PASS
            </Button>
            <Button size="small" danger disabled={busy || row.status === 'REJECTED'} onClick={onReject}>
              REJECT / RETURN
            </Button>
          </Space>
        </>
      )}
    </div>
  );
}

function ShotStrip({ shots, previews }: { shots: KitVideoMasterReviewShotRow[]; previews: Record<string, string> }) {
  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, margin: '8px 0 12px' }}>
      {shots.map((s) => (
        <div key={s.code} style={{ width: 88, textAlign: 'center' }}>
          {s.id && previews[s.id] ? (
            <img src={previews[s.id]} alt={s.code} style={{ width: 88, height: 110, objectFit: 'contain', borderRadius: 6, background: '#f8fafc' }} />
          ) : (
            <div style={{ width: 88, height: 110, background: '#f1f5f9', borderRadius: 6, fontSize: 10, color: '#94a3b8', display: 'grid', placeItems: 'center' }}>
              {s.code}
            </div>
          )}
          <div style={{ fontSize: 10, color: '#334155' }}>{s.label}</div>
          <div style={{ fontSize: 10, color: '#64748b' }}>{s.qaStatus} · P0 {s.p0}</div>
        </div>
      ))}
    </div>
  );
}
