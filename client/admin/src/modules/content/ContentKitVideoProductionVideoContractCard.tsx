import { useEffect, useState } from 'react';
import { Alert, Button, Card, Input, InputNumber, Modal, Select, Space, Tag, Typography } from 'antd';
import {
  approveProductionVideoContract,
  fetchImageGenerationExecutionArtifactBlob,
  fetchKitVideoProductionShots,
  fetchProductionVideoContract,
  rejectProductionVideoContract,
  saveProductionVideoContract,
  validateProductionVideoContract,
  type ProductionVideoContractRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const gateColor = (v?: string) => {
  const s = (v || '').toUpperCase();
  if (s === 'PASS' || s === 'LOCKED' || s === 'COMPILED' || s === 'APPROVED' || s === 'DIRECTOR_APPROVED' || s === 'VALIDATED' || s === 'IMAGE_APPROVED')
    return 'green';
  if (s === 'BLOCKED' || s === 'FAIL' || s === 'MISSING' || s === 'NOT_READY' || s === 'REJECTED' || s === 'IMAGE_REJECTED')
    return 'red';
  return 'orange';
};

const sha = (v?: string) => (v ? `${v.slice(0, 12)}…${v.slice(-8)}` : '—');

const obj = (v: unknown) => (v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : {});
const text = (v: unknown) => (v == null ? '—' : typeof v === 'string' || typeof v === 'number' ? String(v) : JSON.stringify(v));

const CAMERA_TYPES = ['static', 'slow_push_in', 'slow_pull_out', 'slight_pan', 'hold'];
const HEAD_TYPES = ['none', 'slight_turn', 'eye_glance', 'natural_breathing', 'hold', 'slight_lower'];

export function ContentKitVideoProductionVideoContractCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ProductionVideoContractRow>();
  const [preview, setPreview] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [duration, setDuration] = useState<number | null>(5);
  const [cameraType, setCameraType] = useState('slow_push_in');
  const [cameraDirection, setCameraDirection] = useState('forward');
  const [cameraIntensity, setCameraIntensity] = useState('low');
  const [headType, setHeadType] = useState('slight_turn');
  const [headDirection, setHeadDirection] = useState('right');
  const [headIntensity, setHeadIntensity] = useState('low');
  const [startExpr, setStartExpr] = useState('');
  const [endExpr, setEndExpr] = useState('');
  const [hairMotion, setHairMotion] = useState('moves slightly with air');
  const [clothMotion, setClothMotion] = useState('moves naturally');

  const applyPayload = (data: ProductionVideoContractRow) => {
    const payload = obj(data.payload);
    const timing = obj(payload.timing);
    const camera = obj(payload.camera);
    const camMove = obj(camera.cameraMovement);
    const subject = obj(payload.subjectMotion);
    const head = obj(subject.head);
    const expr = obj(payload.expression);
    const hair = obj(payload.hairClothing);
    if (typeof timing.duration === 'number') setDuration(timing.duration);
    if (typeof camMove.type === 'string') setCameraType(camMove.type);
    if (typeof camMove.direction === 'string') setCameraDirection(camMove.direction);
    if (typeof camMove.intensity === 'string') setCameraIntensity(camMove.intensity);
    if (typeof head.type === 'string') setHeadType(head.type);
    if (typeof head.direction === 'string') setHeadDirection(head.direction);
    if (typeof head.intensity === 'string') setHeadIntensity(head.intensity);
    if (typeof expr.startingExpression === 'string') setStartExpr(expr.startingExpression);
    if (typeof expr.endingExpression === 'string') setEndExpr(expr.endingExpression);
    if (typeof hair.hairMotion === 'string') setHairMotion(hair.hairMotion);
    if (typeof hair.clothMotion === 'string') setClothMotion(hair.clothMotion);
  };

  const overlay = () => ({
    contractId: row?.id || undefined,
    durationSeconds: duration ?? undefined,
    cameraMovementType: cameraType,
    cameraDirection,
    cameraIntensity,
    headMovementType: headType,
    headDirection,
    headIntensity,
    startingExpression: startExpr || undefined,
    endingExpression: endExpr || undefined,
    hairMotion,
    clothMotion,
  });

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void fetchProductionVideoContract(target)
      .then(async (data) => {
        setRow(data);
        applyPayload(data);
        setError(undefined);
        if (data.stillExecutionId && data.artifactReadable) {
          const url = await fetchImageGenerationExecutionArtifactBlob(target, data.stillExecutionId);
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
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Video Contract.')))
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

  const run = (fn: () => Promise<ProductionVideoContractRow>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then((data) => {
        setRow(data);
        applyPayload(data);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Video Contract bị BLOCK.')))
      .finally(() => setBusy(false));
  };

  const payload = obj(row?.payload);
  const authority = obj(payload.authority);
  const character = obj(payload.character);
  const scene = obj(payload.scene);
  const camera = obj(payload.camera);
  const subject = obj(payload.subjectMotion);
  const expr = obj(payload.expression);
  const hair = obj(payload.hairClothing);
  const env = obj(payload.environment);
  const timing = obj(payload.timing);
  const continuity = obj(payload.continuity);
  const composition = obj(payload.composition);
  const constraints = obj(payload.constraints);
  const output = obj(payload.output);
  const blocked = !row?.stillApproved || row?.status === 'NOT_READY' || (row?.blocks || []).some((b) => b.code === 'VIDEO_CONTRACT_NOT_READY');
  const locked = !!row?.immutable;

  return (
    <Card
      id="production-video-contract"
      className="fx-look__card"
      size="small"
      title="PRODUCTION VIDEO CONTRACT"
      extra={
        <Space>
          <Button
            size="small"
            disabled={!shotId || !row?.canCreate || locked}
            loading={busy}
            onClick={() => run(() => saveProductionVideoContract(shotId!, overlay()))}
          >
            CREATE CONTRACT
          </Button>
          <Button
            size="small"
            disabled={!shotId || !row?.canValidate || locked}
            loading={busy}
            onClick={() => run(() => validateProductionVideoContract(shotId!, row?.id || undefined, overlay()))}
          >
            VALIDATE
          </Button>
          <Button
            size="small"
            type="primary"
            disabled={!shotId || !row?.id || !row?.canApprove}
            loading={busy}
            onClick={() => run(() => approveProductionVideoContract(shotId!, row!.id!))}
          >
            DIRECTOR APPROVE
          </Button>
          <Button
            size="small"
            danger
            disabled={!shotId || !row?.id || !row?.canReject}
            loading={busy}
            onClick={() => setRejectOpen(true)}
          >
            REJECT
          </Button>
        </Space>
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {blocked ? (
        <div className="fx-video-contract-blocked">
          VIDEO CONTRACT BLOCKED
          <div>Approved production still required.</div>
        </div>
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
        · {row?.documentId} · {row?.contractVersion || 'V1'} · {row?.status || 'DRAFT'} · generation={String(row?.generation ?? false)}
      </Typography.Text>

      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={gateColor(row?.master)}>MASTER [{row?.master || '—'}]</Tag>
        <Tag color={gateColor(row?.dna)}>DNA [{row?.dna || '—'}]</Tag>
        <Tag color={gateColor(row?.prp)}>PRP [{row?.prp || '—'}]</Tag>
        <Tag color={gateColor(row?.governanceEngine)}>IDENTITY GOVERNANCE [{row?.governanceEngine || '—'}]</Tag>
        <Tag color={gateColor(row?.shotContract)}>SHOT CONTRACT [{row?.shotContract || '—'}]</Tag>
        <Tag color={row?.stillApproved ? 'green' : 'red'}>
          {row?.stillApproved ? 'VISUAL ANCHOR · IMAGE APPROVED' : 'VISUAL ANCHOR BLOCKED'}
        </Tag>
        <Tag color={gateColor(row?.status)}>VIDEO CONTRACT [{row?.status || 'DRAFT'}]</Tag>
        <Tag color={gateColor(row?.directorApproval)}>DIRECTOR APPROVAL [{row?.directorApproval || 'PENDING'}]</Tag>
      </Space>

      <div className="fx-dir-review-layout">
        <div className="fx-dir-review-preview">
          <Typography.Text strong>{row?.stillApproved ? 'APPROVED STILL' : 'PRODUCTION STILL (not approved)'}</Typography.Text>
          {row?.stillApproved ? <Tag color="green" style={{ marginLeft: 8 }}>IMAGE APPROVED</Tag> : <Tag color="orange">NOT IMAGE_APPROVED</Tag>}
          {preview ? (
            <img src={preview} alt={row?.stillApproved ? 'Approved production still visual anchor' : 'Production still awaiting Director approval'} />
          ) : (
            <div className="fx-dir-review-preview__empty">Chưa có artifact thật để xem.</div>
          )}
          <div className="fx-dir-review-sha">
            <div>Execution: {row?.stillExecutionId || '—'}</div>
            <div>Artifact SHA: {sha(row?.stillArtifactSha256)}</div>
            <div>Still status: {row?.stillStatus || '—'} · Review: {row?.directorReviewStatus || '—'}</div>
          </div>
        </div>
        <div className="fx-dir-review-meta">
          <Typography.Text strong>A. Authority · INHERITED</Typography.Text>
          <div className="fx-dir-review-sha">
            <div>Master SHA {sha(row?.masterSha256 || String(authority.masterSha256 || ''))}</div>
            <div>DNA SHA {sha(row?.dnaSha256 || String(authority.dnaSha256 || ''))}</div>
            <div>PRP SHA {sha(row?.prpSha256 || String(authority.prpSha256 || ''))}</div>
            <div>Shot Contract SHA {sha(row?.shotContractSha256 || String(authority.shotContractSha256 || ''))}</div>
            <div>Video Contract SHA {sha(row?.contractSha256)}</div>
          </div>
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
            Video Contract không sửa DNA / Master / PRP. Chỉ mô tả motion từ approved still.
          </Typography.Text>
          {(row?.blocks || []).length ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginTop: 8 }}
              message={row?.blocks[0]?.code || 'VIDEO_CONTRACT_NOT_READY'}
              description={row?.blocks.map((b) => b.message).join(' ')}
            />
          ) : null}
        </div>
      </div>

      <div className="fx-video-contract-grid">
        <section>
          <h4>C. Character State · FACT / INHERITED</h4>
          <div>characterId {text(character.characterId)}</div>
          <div>age {text(character.age)}</div>
          <div>identity {text(character.identityState)}</div>
          <div>wardrobe {text(character.wardrobeState)}</div>
          <div>hair {text(character.hairState)}</div>
          <div>expression {text(character.expressionState)}</div>
        </section>
        <section>
          <h4>D. Scene · FACT</h4>
          <div>location {text(scene.location)}</div>
          <div>environment {text(scene.environmentState)}</div>
          <div>time {text(scene.timeOfDay)}</div>
        </section>
        <section>
          <h4>E. Camera · MOTION</h4>
          <div>framing {text(camera.framing)}</div>
          <label>
            type
            <Select size="small" value={cameraType} options={CAMERA_TYPES.map((v) => ({ value: v, label: v }))} onChange={setCameraType} disabled={locked} />
          </label>
          <label>
            direction
            <Input size="small" value={cameraDirection} onChange={(e) => setCameraDirection(e.target.value)} disabled={locked} />
          </label>
          <label>
            intensity
            <Input size="small" value={cameraIntensity} onChange={(e) => setCameraIntensity(e.target.value)} disabled={locked} />
          </label>
        </section>
        <section>
          <h4>F. Subject Motion · MOTION</h4>
          <div>body {text(obj(subject.body).type)}</div>
          <label>
            head
            <Select size="small" value={headType} options={HEAD_TYPES.map((v) => ({ value: v, label: v }))} onChange={setHeadType} disabled={locked} />
          </label>
          <label>
            direction
            <Input size="small" value={headDirection} onChange={(e) => setHeadDirection(e.target.value)} disabled={locked} />
          </label>
          <label>
            intensity
            <Input size="small" value={headIntensity} onChange={(e) => setHeadIntensity(e.target.value)} disabled={locked} />
          </label>
          <div>eye {text(obj(subject.eye).type)}</div>
        </section>
        <section>
          <h4>G. Expression · MOTION</h4>
          <label>
            start
            <Input size="small" value={startExpr} onChange={(e) => setStartExpr(e.target.value)} disabled={locked} />
          </label>
          <label>
            end
            <Input size="small" value={endExpr} onChange={(e) => setEndExpr(e.target.value)} disabled={locked} />
          </label>
          <div>transition {text(expr.expressionTransition)}</div>
        </section>
        <section>
          <h4>H. Hair / Clothing · MOTION</h4>
          <label>
            hair
            <Input size="small" value={hairMotion} onChange={(e) => setHairMotion(e.target.value)} disabled={locked} />
          </label>
          <label>
            cloth
            <Input size="small" value={clothMotion} onChange={(e) => setClothMotion(e.target.value)} disabled={locked} />
          </label>
          <div>inherited: {text(hair.hairMotion)}</div>
        </section>
        <section>
          <h4>I. Environment · MOTION</h4>
          <div>light {text(env.lightMovement)}</div>
          <div>background {text(env.backgroundMovement)}</div>
        </section>
        <section>
          <h4>K. Timing · FACT</h4>
          <label>
            duration
            <InputNumber size="small" min={0} value={duration} onChange={(v) => setDuration(v)} disabled={locked} />
          </label>
          <div>fps {text(timing.fps)}</div>
          <div>start {text(timing.startState)}</div>
        </section>
        <section>
          <h4>L. Continuity · CONSTRAINT</h4>
          <div>start {text(continuity.startContinuity)}</div>
          <div>end {text(continuity.endContinuity)}</div>
        </section>
        <section>
          <h4>M. Composition · FACT</h4>
          <div>subject {text(composition.subjectPosition)}</div>
          <div>look {text(composition.lookDirection)}</div>
        </section>
        <section>
          <h4>N. Constraints · CONSTRAINT</h4>
          <div>invariants {text(constraints.invariants)}</div>
          <div>forbidden {text(constraints.forbidden)}</div>
        </section>
        <section>
          <h4>O. Output · FACT</h4>
          <div>aspect {text(output.aspectRatio)}</div>
          <div>resolution {text(output.resolution)}</div>
          <div>duration {text(output.duration)}</div>
        </section>
      </div>

      <Modal
        title="REJECT VIDEO CONTRACT"
        open={rejectOpen}
        okText="REJECT"
        okButtonProps={{ danger: true, disabled: reason.trim().length < 3 }}
        onCancel={() => setRejectOpen(false)}
        onOk={() => {
          if (!shotId || !row?.id || reason.trim().length < 3) return;
          setRejectOpen(false);
          run(() => rejectProductionVideoContract(shotId, row.id!, reason.trim()));
        }}
      >
        <Typography.Paragraph>Director phải nhập lý do. Reject không tạo V2, không generate, không gọi provider.</Typography.Paragraph>
        <Input.TextArea rows={4} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Camera quá nhanh. / Motion đụng identity. / Timing không khớp shot." />
      </Modal>
    </Card>
  );
}
