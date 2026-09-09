import { useEffect, useState } from 'react';
import { Alert, Button, Card, Input, Modal, Space, Tag, Typography } from 'antd';
import {
  approveImageDirectorReview,
  fetchImageDirectorReview,
  fetchImageGenerationExecutionArtifactBlob,
  fetchKitVideoProductionShots,
  openImageDirectorReview,
  rejectImageDirectorReview,
  type ImageGenerationDirectorReviewRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'COMPILED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED' || s === 'SUCCEEDED' || s === 'READY_FOR_DIRECTOR' || s === 'IMAGE_APPROVED')
    return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'FAILED' || s === 'QA_FAILED' || s === 'MISSING' || s === 'REJECTED' || s === 'IMAGE_REJECTED')
    return 'red';
  return 'orange';
};

const sha = (v?: string) => (v ? `${v.slice(0, 12)}…${v.slice(-8)}` : '—');

export function ContentKitVideoImageDirectorReviewCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ImageGenerationDirectorReviewRow>();
  const [preview, setPreview] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [reason, setReason] = useState('');

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchImageDirectorReview(target)
      .then(async (data) => {
        setRow(data);
        setError(undefined);
        if (data.executionId && data.artifactReadable) {
          const url = await fetchImageGenerationExecutionArtifactBlob(target, data.executionId);
          setPreview((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return url;
          });
        } else {
          setPreview((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return undefined;
          });
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Image Director Review.')))
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
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const run = (fn: () => Promise<ImageGenerationDirectorReviewRow>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then((data) => {
        setRow(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Director Review bị BLOCK.')))
      .finally(() => setBusy(false));
  };

  const pending = (row?.directorApproval || 'PENDING').toUpperCase() === 'PENDING';
  const ready = row?.executionStatus === 'READY_FOR_DIRECTOR';
  const qa = row?.qa;
  const qaPass =
    qa?.technical === 'PASS' &&
    qa?.character === 'PASS' &&
    qa?.identity === 'PASS' &&
    qa?.continuity === 'PASS' &&
    qa?.composition === 'PASS';

  return (
    <Card
      id="production-image-director-review"
      className="fx-look__card"
      size="small"
      title="PRODUCTION IMAGE — DIRECTOR REVIEW"
      extra={
        <Space>
          <Button size="small" disabled={!shotId} loading={busy} onClick={() => run(() => openImageDirectorReview(shotId!))}>
            OPEN REVIEW
          </Button>
          <Button
            size="small"
            type="primary"
            disabled={!shotId || !row?.canApprove || !pending}
            loading={busy}
            onClick={() => run(() => approveImageDirectorReview(shotId!))}
          >
            APPROVE IMAGE
          </Button>
          <Button
            size="small"
            danger
            disabled={!shotId || !row?.canReject || !pending}
            loading={busy}
            onClick={() => setRejectOpen(true)}
          >
            REJECT IMAGE
          </Button>
        </Space>
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {ready && pending ? (
        <button type="button" className="fx-dir-review-badge" onClick={() => document.getElementById('production-image-director-review')?.scrollIntoView({ behavior: 'smooth', block: 'start' })}>
          ● IMAGE READY — DIRECTOR REVIEW REQUIRED
        </button>
      ) : null}
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
        · {row?.documentId} · generation={String(row?.generation ?? false)}
      </Typography.Text>

      <div className="fx-dir-review-layout">
        <div className="fx-dir-review-preview">
          <Typography.Text strong>IMAGE PREVIEW</Typography.Text>
          {preview ? (
            <img src={preview} alt="Production still for Director review" />
          ) : (
            <div className="fx-dir-review-preview__empty">Chưa có artifact thật để xem.</div>
          )}
        </div>
        <div className="fx-dir-review-meta">
          <Typography.Text strong>A. Production Identity</Typography.Text>
          <div>Character: {row?.characterName || 'Minh'}</div>
          <div>Character ID: {row?.characterId || '—'}</div>
          <div>Era: {row?.eraId || '—'}</div>
          <div>Shot: {row?.shotCode || '—'}</div>
          <div>Execution: {row?.executionId || '—'}</div>

          <Typography.Text strong style={{ display: 'block', marginTop: 10 }}>
            B. Authority
          </Typography.Text>
          <Space wrap>
            <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
            <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
            <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
            <Tag color={gateColor(row?.governanceEngine)}>IDENTITY GOVERNANCE [{row?.governanceEngine || '—'}]</Tag>
            <Tag color={gateColor(row?.shotContract)}>SHOT CONTRACT [{row?.shotContract || '—'}]</Tag>
            <Tag color={gateColor(row?.prompt)}>PROMPT [{row?.prompt || '—'}]</Tag>
            <Tag color={gateColor(row?.imageGenerationContract)}>IMAGE CONTRACT [{row?.imageGenerationContract || '—'}]</Tag>
            <Tag color={gateColor(row?.executionStatus)}>EXECUTION [{row?.executionStatus || '—'}]</Tag>
          </Space>
          <div className="fx-dir-review-sha">
            <div>Master SHA {sha(row?.masterSha256)}</div>
            <div>DNA SHA {sha(row?.dnaSha256)}</div>
            <div>PRP SHA {sha(row?.prpSha256)}</div>
            <div>Shot Contract SHA {sha(row?.shotContractSha256)}</div>
            <div>Prompt SHA {sha(row?.promptSha256)}</div>
            <div>Image Generation Contract SHA {sha(row?.igcSha256)}</div>
            <div>Artifact SHA {sha(row?.artifactSha256)}</div>
          </div>

          <Typography.Text strong style={{ display: 'block', marginTop: 10 }}>
            C. Automated QA
          </Typography.Text>
          <div>Technical QA [{qa?.technical || '—'}]</div>
          <div>Character QA [{qa?.character || '—'}]</div>
          <div>Identity QA [{qa?.identity || '—'}]</div>
          <div>Continuity QA [{qa?.continuity || '—'}]</div>
          <div>Composition QA [{qa?.composition || '—'}]</div>
          {qaPass && pending ? (
            <Typography.Text type="secondary">Automated QA đã PASS. Director Approval vẫn PENDING — engine không tự duyệt.</Typography.Text>
          ) : null}

          <Typography.Text strong style={{ display: 'block', marginTop: 10 }}>
            D. Director Decision
          </Typography.Text>
          <Tag color={gateColor(row?.directorApproval)}>DIRECTOR APPROVAL: {row?.directorApproval || 'PENDING'}</Tag>
          {row?.directorApproval === 'APPROVED' ? <Tag color="green">IMAGE_APPROVED</Tag> : null}
          {row?.directorApproval === 'REJECTED' ? <Tag color="red">IMAGE_REJECTED</Tag> : null}
          {row?.rejectionReason ? <div>Reason: {row.rejectionReason}</div> : null}
          {(row?.blocks || []).length ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginTop: 8 }}
              message={row?.blocks[0]?.code || 'IMAGE_DIRECTOR_REVIEW_NOT_READY'}
              description={row?.blocks.map((b) => b.message).join(' ')}
            />
          ) : null}
        </div>
      </div>

      <Modal
        title="REJECT IMAGE"
        open={rejectOpen}
        okText="REJECT IMAGE"
        okButtonProps={{ danger: true, disabled: reason.trim().length < 3 }}
        onCancel={() => setRejectOpen(false)}
        onOk={() => {
          if (!shotId || reason.trim().length < 3) return;
          setRejectOpen(false);
          run(() => rejectImageDirectorReview(shotId, reason.trim()));
        }}
      >
        <Typography.Paragraph>Director phải nhập lý do. Reject không generate lại, không gọi Gemini.</Typography.Paragraph>
        <Input.TextArea
          rows={4}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Face identity chưa đạt. / Ánh sáng không phù hợp với scene. / Biểu cảm không đúng Shot Contract."
        />
      </Modal>
    </Card>
  );
}
