import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Modal, Radio, Space, Typography } from 'antd';
import {
  approveImageDirectorReview,
  executeImageGenerationExecution,
  fetchFirstRealProduction,
  fetchImageDirectorReview,
  fetchImageGenerationExecution,
  fetchImageGenerationExecutionArtifactBlob,
  fetchKitVideoProductionShots,
  openImageDirectorReview,
  rejectImageDirectorReview,
  type FirstRealProductionRow,
  type ImageGenerationExecutionRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  FIRST_REAL_PROVIDERS,
  STAFF_CRP_BLOCK,
  canOfferGenerate,
  confirmCopy,
  pickShot001,
  staffShotLabel,
} from './kit-video-first-real-production';
import {
  DIRECTOR_QA_CHECKS,
  HISTORICAL_STILL_ID,
  approvedReviewLabel,
  crpLockedLabel,
  isImageDirectorApproved,
  isImageDirectorRejected,
  pendingReviewLabel,
} from './kit-video-first-real-production-v2';

export function ContentKitVideoImageGenerationExecutionCard() {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<{ id: string; shotCode: string }[]>([]);
  const [row, setRow] = useState<ImageGenerationExecutionRow>();
  const [preview, setPreview] = useState<FirstRealProductionRow>();
  const [provider, setProvider] = useState('');
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [stillUrl, setStillUrl] = useState<string>();

  const load = (id?: string, picked?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void Promise.all([
      fetchImageGenerationExecution(target),
      fetchFirstRealProduction(target, picked || provider || undefined).catch(() => undefined),
    ])
      .then(async ([data, first]) => {
        setRow(data);
        if (first) setPreview(first);
        setError(undefined);
        if (data.id && data.artifactSha256 && data.id !== HISTORICAL_STILL_ID) {
          const url = await fetchImageGenerationExecutionArtifactBlob(target, data.id);
          setStillUrl((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return url;
          });
        } else {
          setStillUrl((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return undefined;
          });
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được thông tin tạo hình.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    void fetchKitVideoProductionShots()
      .then((bundle) => {
        const list = (bundle.shots || []).map((s) => ({ id: s.id, shotCode: s.shotCode }));
        setShots(list);
        const first = pickShot001(list);
        if (first) {
          setShotId(first.id);
          load(first.id);
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được Production Shot.')));
    return () => {
      setStillUrl((prev) => {
        if (prev) URL.revokeObjectURL(prev);
        return undefined;
      });
    };
  }, []);

  const crpUsable = preview?.crpUsable === true && row?.crpUsable !== false;
  const generated =
    crpUsable &&
    !!row?.id &&
    row.id !== HISTORICAL_STILL_ID &&
    (!!row.artifactSha256 ||
      ['READY_FOR_DIRECTOR', 'SUCCEEDED', 'APPROVED', 'IMAGE_APPROVED', 'REJECTED', 'IMAGE_REJECTED'].includes(
        (row.status || '').toUpperCase(),
      ));
  const shotCode = preview?.shotCode || shots.find((s) => s.id === shotId)?.shotCode;
  const shotLabel = staffShotLabel(shotCode);
  const character = preview?.characterName || 'Minh';
  const location = preview?.location || 'Phòng khách';
  const action = preview?.action || 'Đọc tờ giấy';
  const duration = preview?.durationLabel || '5 giây';
  const providerLabel = FIRST_REAL_PROVIDERS.find((x) => x.id === provider)?.label || 'Chưa chọn';
  const copy = confirmCopy({ shotLabel, character, location, action, provider: providerLabel });
  const offer = canOfferGenerate({ crpUsable, provider, alreadyGenerated: generated, shotCode });
  const img = stillUrl || '';
  const alreadyApproved = isImageDirectorApproved(row?.status, row?.directorApproval);
  const alreadyRejected = isImageDirectorRejected(row?.status, row?.directorApproval);
  const pendingReview = generated && !alreadyApproved && !alreadyRejected;

  const startGenerate = () => {
    if (!shotId || !offer) return;
    setConfirmOpen(false);
    setCreating(true);
    setBusy(true);
    void executeImageGenerationExecution(shotId, { confirm: true, provider })
      .then((data) => {
        setRow(data);
        setError(undefined);
        load(shotId, provider);
      })
      .catch((e) => setError(apiErrorMessage(e, preview?.staffMessage || STAFF_CRP_BLOCK)))
      .finally(() => {
        setBusy(false);
        setCreating(false);
      });
  };

  const approveImage = () => {
    if (!shotId || alreadyApproved) return;
    setBusy(true);
    void openImageDirectorReview(shotId)
      .then(() => approveImageDirectorReview(shotId))
      .then(() => fetchImageDirectorReview(shotId))
      .then(() => load(shotId, provider))
      .catch((e) => setError(apiErrorMessage(e, 'Chưa duyệt được ảnh.')))
      .finally(() => setBusy(false));
  };

  const rejectImage = () => {
    if (!shotId || !rejectReason.trim()) return;
    setBusy(true);
    void openImageDirectorReview(shotId)
      .then(() => rejectImageDirectorReview(shotId, rejectReason.trim()))
      .then(() => {
        setRejectOpen(false);
        setRejectReason('');
        load(shotId, provider);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa từ chối được ảnh.')))
      .finally(() => setBusy(false));
  };

  return (
    <Card
      className="fx-look__card"
      size="small"
      title={`Tạo hình — ${shotLabel}`}
      extra={
        generated ? null : (
          <Button type="primary" disabled={!offer} loading={busy} onClick={() => setConfirmOpen(true)}>
            Tạo hình
          </Button>
        )
      }
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {!crpUsable ? (
        <Alert type="warning" showIcon message={STAFF_CRP_BLOCK} style={{ marginBottom: 8 }} />
      ) : null}
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
        Shot:{' '}
        <select
          value={shotId || ''}
          onChange={(e) => {
            setShotId(e.target.value);
            load(e.target.value, provider);
          }}
        >
          {shots.map((s) => (
            <option key={s.id} value={s.id}>
              {staffShotLabel(s.shotCode)}
            </option>
          ))}
        </select>
      </Typography.Text>
      <div style={{ fontSize: 14, lineHeight: 1.7, marginBottom: 12 }}>
        <div>
          {shotLabel} · {character} · {location} · Tối
        </div>
        <div>Bối cảnh: {location}</div>
        <div>Hành động: {action}</div>
        <div>Thời lượng: {duration}</div>
        <div>Bộ ảnh chuẩn: {crpLockedLabel(crpUsable)}</div>
        <div>Nhà cung cấp: {providerLabel}</div>
      </div>
      {creating ? <Alert type="info" showIcon message="Đang tạo hình..." style={{ marginBottom: 8 }} /> : null}
      {generated ? (
        <>
          <Alert
            type={alreadyApproved ? 'success' : alreadyRejected ? 'error' : 'info'}
            showIcon
            message={`${shotLabel} · ${character} · Hình ảnh mới · ${alreadyApproved ? 'Đã duyệt' : alreadyRejected ? 'Không đạt' : 'Chờ duyệt'}`}
            description={alreadyApproved ? approvedReviewLabel() : alreadyRejected ? 'Hình ảnh 1/1 · Không đạt' : pendingReviewLabel()}
            style={{ marginBottom: 8 }}
          />
          {img ? (
            <img src={img} alt="Hình production Shot 01" style={{ maxWidth: '100%', marginBottom: 8, border: '1px solid #d9d9d9' }} />
          ) : (
            <p style={{ marginBottom: 8 }}>Đang tải ảnh production...</p>
          )}
          <div style={{ fontSize: 13, marginBottom: 8, color: '#595959' }}>
            {DIRECTOR_QA_CHECKS.map((item) => (
              <div key={item}>{alreadyApproved ? '☑' : '☐'} {item}</div>
            ))}
          </div>
          {pendingReview ? (
            <Space>
              <Button type="primary" loading={busy} onClick={approveImage}>
                Duyệt hình
              </Button>
              <Button danger loading={busy} onClick={() => setRejectOpen(true)}>
                Không đạt
              </Button>
            </Space>
          ) : (
            <Typography.Text>{alreadyApproved ? 'Đã duyệt hình.' : 'Đã từ chối hình.'}</Typography.Text>
          )}
        </>
      ) : (
        <>
          <div style={{ marginBottom: 8 }}>Nhà cung cấp AI:</div>
          <Radio.Group
            value={provider}
            onChange={(e) => {
              const next = e.target.value as string;
              setProvider(next);
              if (shotId) load(shotId, next);
            }}
          >
            {FIRST_REAL_PROVIDERS.map((item) => (
              <Radio key={item.id || 'none'} value={item.id}>
                {item.label}
              </Radio>
            ))}
          </Radio.Group>
        </>
      )}
      <Collapse
        style={{ marginTop: 12 }}
        items={[
          {
            key: 'tech',
            label: 'Chi tiết sản xuất',
            children: (
              <div style={{ fontSize: 12 }}>
                <div>IntentSha: {preview?.intentSha256 || '—'}</div>
                <div>MasterSha: {preview?.masterSha256 || row?.masterSha256 || '—'}</div>
                <div>DNASha: {preview?.dnaSha256 || row?.dnaSha256 || '—'}</div>
                <div>ReferenceSha: {preview?.referenceSha256 || row?.prpSha256 || '—'}</div>
                <div>ShotContractSha: {preview?.shotContractSha256 || row?.shotContractSha256 || '—'}</div>
                <div>Fingerprint: {preview?.executionFingerprint || row?.executionFingerprint || '—'}</div>
                <div>Capability: {preview?.capabilityStatus || '—'}</div>
                <div>Generation allowed: {String(preview?.generationAllowed ?? false)}</div>
              </div>
            ),
          },
        ]}
      />
      <Modal
        title={copy.title}
        open={confirmOpen}
        onCancel={() => setConfirmOpen(false)}
        footer={[
          <Button key="cancel" onClick={() => setConfirmOpen(false)}>
            Hủy
          </Button>,
          <Button key="ok" type="primary" loading={busy} onClick={startGenerate}>
            Bắt đầu tạo
          </Button>,
        ]}
      >
        <div>Nhân vật: {copy.character}</div>
        <div>Cảnh: {copy.location}</div>
        <div>Hành động: {copy.action}</div>
        <div>Nhà cung cấp: {copy.provider}</div>
        <p style={{ marginTop: 8 }}>{copy.note}</p>
      </Modal>
      <Modal
        title="Từ chối hình này?"
        open={rejectOpen}
        onCancel={() => setRejectOpen(false)}
        onOk={rejectImage}
        okText="Từ chối"
        okButtonProps={{ danger: true, disabled: !rejectReason.trim() }}
      >
        <textarea
          value={rejectReason}
          onChange={(e) => setRejectReason(e.target.value)}
          rows={3}
          style={{ width: '100%' }}
          placeholder="Lý do từ chối"
        />
      </Modal>
    </Card>
  );
}
