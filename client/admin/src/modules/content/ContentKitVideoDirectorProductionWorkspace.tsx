import { useEffect, useState, type ReactNode } from 'react';
import { Alert, Button, Collapse, Input, Modal, Radio, Space, Typography } from 'antd';
import {
  approveImageDirectorReview,
  approveProductionVideoContract,
  approveVideoGenerationExecution,
  executeVideoGenerationExecution,
  fetchImageDirectorReview,
  fetchImageGenerationExecutionArtifactBlob,
  fetchKitVideoProductionShots,
  fetchProductionVideoContract,
  fetchVideoGenerationExecution,
  fetchVideoGenerationExecutionArtifactBlob,
  preflightVideoGenerationExecution,
  rejectImageDirectorReview,
  rejectVideoGenerationExecution,
  saveProductionVideoContract,
  validateProductionVideoContract,
  type ImageGenerationDirectorReviewRow,
  type KitVideoProductionShotRow,
  type ProductionVideoContractRow,
  type VideoGenerationExecutionRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  deriveDirectorWorkspace,
  displayShotTitle,
  directorStatusLabel,
  qaPassedCount,
  shotDisplayName,
  shotSceneLabel,
  shortShotLabel,
  truncateSha,
} from './kit-video-director-workspace';
import { SCENE_STEPS, sceneBrief, sceneChecklist, sceneStepFromAction, type SceneStepId } from './kit-video-production-ux';

/** Director surface = shot + pipeline + media + decide. Engine/studio/SHA stay in Chi tiết kỹ thuật (closed). */
const videoContractWrite = (durationSeconds: 5 | 10) => ({
  durationSeconds,
  cameraMovementType: 'slow_push_in',
  cameraDirection: 'forward',
  cameraIntensity: 'low',
  headMovementType: 'slight_turn',
  headDirection: 'right',
  headIntensity: 'low',
  hairMotion: 'moves slightly with air',
  clothMotion: 'moves naturally',
});

const QA_LABELS = {
  technical: 'Ảnh rõ',
  character: 'Nhân vật đúng',
  identity: 'Góc mặt đúng',
  continuity: 'Bối cảnh đúng',
  composition: 'Bố cục đúng',
} as const;

export function ContentKitVideoDirectorProductionWorkspace({
  children,
  shotId: forcedShotId,
  onBack,
  onContinue,
  showTechnical = true,
}: {
  children: ReactNode;
  shotId?: string;
  onBack?: () => void;
  onContinue?: () => void;
  showTechnical?: boolean;
}) {
  const [shotId, setShotId] = useState<string>();
  const [shots, setShots] = useState<KitVideoProductionShotRow[]>([]);
  const [review, setReview] = useState<ImageGenerationDirectorReviewRow>();
  const [video, setVideo] = useState<ProductionVideoContractRow>();
  const [generation, setGeneration] = useState<VideoGenerationExecutionRow>();
  const [preview, setPreview] = useState<string>();
  const [clip, setClip] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [approveOpen, setApproveOpen] = useState(false);
  const [executeOpen, setExecuteOpen] = useState(false);
  const [rejectVideoOpen, setRejectVideoOpen] = useState(false);
  const [lightbox, setLightbox] = useState(false);
  const [reason, setReason] = useState('');
  const [redoTags, setRedoTags] = useState<string[]>([]);
  const [duration, setDuration] = useState<5 | 10>(5);
  const [copied, setCopied] = useState<string>();
  const [goVideoContract, setGoVideoContract] = useState(false);
  const [waitingVideo, setWaitingVideo] = useState(false);
  const [scenePane, setScenePane] = useState<SceneStepId>('image');

  const load = (id?: string) => {
    const target = id || shotId;
    if (!target) return;
    setBusy(true);
    void Promise.all([
      fetchImageDirectorReview(target),
      fetchProductionVideoContract(target),
      fetchVideoGenerationExecution(target),
    ])
      .then(async ([nextReview, nextVideo, nextGen]) => {
        setReview(nextReview);
        setVideo(nextVideo);
        setGeneration(nextGen);
        setError(undefined);
        const execId = nextReview.executionId || nextVideo.stillExecutionId;
        if (execId && (nextReview.artifactReadable || nextVideo.artifactReadable)) {
          const url = await fetchImageGenerationExecutionArtifactBlob(target, execId);
          setPreview((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return url;
          });
        }
        if (nextGen.id && nextGen.artifactReadable) {
          const url = await fetchVideoGenerationExecutionArtifactBlob(target, nextGen.id);
          setClip((prev) => {
            if (prev) URL.revokeObjectURL(prev);
            return url;
          });
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không tải được màn duyệt Director.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    void fetchKitVideoProductionShots()
      .then((bundle) => {
        const list = bundle.shots || [];
        setShots(list);
        const target = forcedShotId || list[0]?.id;
        if (target) {
          setShotId(target);
          load(target);
        }
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không tải được cảnh production.')));
    return () => {
      if (preview) URL.revokeObjectURL(preview);
      if (clip) URL.revokeObjectURL(clip);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forcedShotId]);

  const derived = deriveDirectorWorkspace({
    master: review?.master,
    dna: review?.dna,
    prp: review?.prp,
    shotContract: review?.shotContract,
    executionStatus: review?.executionStatus,
    directorApproval: review?.directorApproval,
    stillApproved: video?.stillApproved,
    videoContractStatus: video?.status,
    videoContractId: video?.id,
    videoGenerationStatus: generation?.status,
    videoPreflightPass: generation?.preflightPass,
    videoProviderRequestId: generation?.providerRequestId,
    videoRequestedAt: generation?.requestedAt,
  });
  const suggestedPane = sceneStepFromAction(derived.action);
  useEffect(() => {
    setScenePane(suggestedPane);
  }, [suggestedPane]);
  const videoBusy =
    waitingVideo ||
    derived.action === 'VIDEO_PROCESSING' ||
    ((generation?.providerRequestId || '') !== '' && ['REQUESTED', 'ACCEPTED', 'PROCESSING'].includes((generation?.status || '').toUpperCase()));
  useEffect(() => {
    if (!videoBusy || !shotId) return;
    const timer = window.setInterval(() => load(shotId), 8000);
    return () => window.clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [videoBusy, shotId]);
  useEffect(() => {
    if (derived.action === 'VIDEO_REVIEW' || derived.action === 'DONE' || derived.next.includes('Tạo lại')) {
      setWaitingVideo(false);
    }
  }, [derived.action]);

  const run = (fn: () => Promise<unknown>) => {
    if (!shotId) return;
    setBusy(true);
    void fn()
      .then(() => {
        window.dispatchEvent(new Event('famixa-look-changed'));
        load(shotId);
      })
      .catch((e) => {
        setError(apiErrorMessage(e, 'Thao tác Director bị chặn.'));
        setBusy(false);
      });
  };

  const selected = shots.find((s) => s.id === shotId);
  const shotCode = review?.shotCode || selected?.shotCode || 'CHAR-001-MINH-ERA01-SHOT-001';
  const characterName = review?.characterName || selected?.characterName || 'Minh';
  const title = displayShotTitle(selected?.spec, selected?.note);
  const brief = sceneBrief(selected?.spec, selected?.note, characterName);
  const qa = qaPassedCount(review?.qa);
  const copySha = async (label: string, value?: string) => {
    if (!value) return;
    await navigator.clipboard.writeText(value);
    setCopied(label);
  };

  return (
    <section id="director-production-workspace" className="fx-dir-ws fx-dir-ws--lite">
      <span id="production-image-director-review" />
      <span id="production-video-contract" />
      <span id="production-video-generation-execution" />

      <header className="fx-dir-ws__head">
        <div>
          {onBack ? (
            <Button type="link" className="fx-dir-ws__back" onClick={onBack}>
              ← Quay lại danh sách cảnh
            </Button>
          ) : null}
          <p className="fx-dir-ws__brand">
            EP01 / {shotSceneLabel(shotCode).toUpperCase()}
          </p>
          <h2 className="fx-dir-ws__title">
            {title || `${characterName} · ${shotSceneLabel(shotCode)}`}
          </h2>
          {showTechnical ? <p className="fx-dir-ws__code">{shortShotLabel(shotCode)}</p> : null}
        </div>
      </header>

      <ol className="fx-dir-ws__pipe" aria-label="Tiến trình cảnh">
        {SCENE_STEPS.map((step) => {
          const locked = step.id === 'video' && !derived.imageOk && derived.action !== 'DONE' && derived.action !== 'VIDEO_REVIEW';
          const current = scenePane === step.id;
          return (
            <li key={step.id} className={`fx-dir-ws__step${current ? ' fx-dir-ws__step--now' : ''}${locked ? ' fx-dir-ws__step--wait' : ' fx-dir-ws__step--ok'}`}>
              <button
                type="button"
                className="fx-dir-ws__step-btn"
                disabled={locked && step.id !== 'content' && step.id !== 'image'}
                onClick={() => setScenePane(step.id)}
              >
                <span className="fx-dir-ws__n">{step.label}</span>
                <span className="fx-dir-ws__st">
                  {current ? '● Bạn đang ở đây' : locked ? '○ Chưa mở' : '✓'}
                </span>
              </button>
            </li>
          );
        })}
      </ol>

      {error ? (
        <Alert
          type="error"
          showIcon
          message={error}
          style={{ marginBottom: 12 }}
          action={
            <Button size="small" onClick={() => shotId && load(shotId)}>
              Kiểm tra lại
            </Button>
          }
        />
      ) : null}

      <div className="fx-dir-ws__action">
        {scenePane === 'content' ? (
          <div className="fx-dir-ws__brief">
            <p className="fx-dir-ws__action-kicker">NỘI DUNG CẢNH</p>
            {brief.scene ? <p>Cảnh: {brief.scene}</p> : null}
            <p>Nhân vật: {brief.characters || characterName}</p>
            {brief.setting ? <p>Bối cảnh: {brief.setting}</p> : null}
            {brief.action ? <p>Hành động: {brief.action}</p> : null}
            {brief.emotion ? <p>Cảm xúc: {brief.emotion}</p> : null}
            <ol className="fx-dir-ws__check">
              {sceneChecklist({
                master: review?.master,
                dna: review?.dna,
                prp: review?.prp,
                shotContract: review?.shotContract,
                executionStatus: review?.executionStatus,
                directorApproval: review?.directorApproval,
                stillApproved: video?.stillApproved,
                videoContractStatus: video?.status,
                videoContractId: video?.id,
                videoGenerationStatus: generation?.status,
                videoPreflightPass: generation?.preflightPass,
              }).map((item) => (
                <li key={item.id}>
                  {item.done ? '✓' : '○'} {item.label}
                </li>
              ))}
            </ol>
            <p className="fx-dir-ws__ok">
              {derived.steps.SHOT === 'APPROVED' ? '✓ Nội dung cảnh đã xác nhận' : 'Cảnh chưa được xác nhận.'}
            </p>
            <Button type="primary" onClick={() => setScenePane(derived.imageOk ? 'video' : 'image')}>
              {derived.action === 'VIDEO_REVIEW' ? 'Sang duyệt video →' : derived.imageOk ? 'Sang bước video →' : 'Sang bước hình ảnh →'}
            </Button>
          </div>
        ) : null}
        {scenePane === 'image' && derived.action === 'IMAGE_REVIEW' ? (
          <>
            <div className="fx-dir-ws__hero">
              {preview ? (
                <button type="button" className="fx-dir-ws__hero-btn" onClick={() => setLightbox(true)}>
                  <img src={preview} alt="Approved production still" />
                </button>
              ) : (
                <div className="fx-dir-ws__hero-empty">Chưa có ảnh để xem.</div>
              )}
            </div>
            <div className="fx-dir-ws__qa">
              {(['character', 'identity', 'continuity', 'composition'] as const).map((key) => {
                const pass = (review?.qa?.[key] || '').toUpperCase() === 'PASS';
                const label = key === 'character' ? `Đúng ${characterName}` : QA_LABELS[key];
                return (
                  <div key={key} className={pass ? 'is-pass' : ''}>
                    {pass ? '✓' : '○'} {label}
                  </div>
                );
              })}
            </div>
            <p className="fx-dir-ws__stage">🟡 Chờ duyệt</p>
            <p className="fx-dir-ws__ask">Bạn có đồng ý sử dụng hình ảnh này để tạo video?</p>
            <Space size="middle" className="fx-dir-ws__btns">
              <Button
                type="primary"
                size="large"
                aria-label="APPROVE IMAGE"
                disabled={!shotId || !review?.canApprove || busy}
                onClick={() => setApproveOpen(true)}
              >
                ✓ DUYỆT ẢNH
              </Button>
              <Button
                size="large"
                aria-label="REJECT IMAGE"
                disabled={!shotId || !review?.canReject || busy}
                onClick={() => setRejectOpen(true)}
              >
                Yêu cầu làm lại
              </Button>
            </Space>
            <aside className="fx-dir-ws__follow">
              <p className="fx-dir-ws__action-kicker">BƯỚC TIẾP THEO</p>
              <p>Sau khi duyệt ảnh → chuẩn bị video</p>
            </aside>
          </>
        ) : null}

        {scenePane === 'image' && derived.imageOk && derived.action !== 'IMAGE_REVIEW' ? (
          <>
            {preview ? (
              <div className="fx-dir-ws__hero">
                <img src={preview} alt="" />
              </div>
            ) : null}
            <p className="fx-dir-ws__ok">✓ HÌNH ẢNH ĐÃ ĐƯỢC DUYỆT</p>
            <p className="fx-dir-ws__ask">Cảnh này đã sẵn sàng để tạo video.</p>
            <Button type="primary" size="large" onClick={() => setScenePane('video')}>
              {derived.action === 'DONE' || derived.action === 'VIDEO_REVIEW' ? 'Xem video' : 'Tạo video'}
            </Button>
          </>
        ) : null}
        {scenePane === 'image' && !derived.imageOk && derived.action !== 'IMAGE_REVIEW' && derived.action !== 'IMAGE_REJECTED' ? (
          <p className="fx-dir-ws__ask">Ảnh của cảnh chưa sẵn sàng để duyệt.</p>
        ) : null}
        {scenePane === 'image' && derived.action === 'IMAGE_REJECTED' ? (
          <>
            <h3>{derived.currentActionTitle}</h3>
            <p className="fx-dir-ws__ask">{derived.currentActionBody}</p>
          </>
        ) : null}

        {scenePane === 'video' && !derived.imageOk ? (
          <p className="fx-dir-ws__hold">Bạn cần duyệt ảnh trước.</p>
        ) : null}
        {scenePane === 'video' && derived.action === 'CREATE_VIDEO_CONTRACT' ? (
          goVideoContract ? (
            <>
              <p className="fx-dir-ws__ok">ẢNH ĐÃ ĐƯỢC DUYỆT ✓</p>
              {preview ? (
                <div className="fx-dir-ws__hero">
                  <img src={preview} alt="Approved production still" />
                </div>
              ) : null}
              <div className="fx-dir-ws__fields">
                <p>Thời lượng</p>
                <Radio.Group value={duration} onChange={(e) => setDuration(e.target.value)}>
                  <Radio value={5}>5 giây</Radio>
                  <Radio value={10}>10 giây</Radio>
                </Radio.Group>
              </div>
              <Button
                type="primary"
                size="large"
                aria-label="CREATE VIDEO CONTRACT"
                disabled={!shotId || !video?.canCreate || busy}
                loading={busy}
                onClick={() => run(() => saveProductionVideoContract(shotId!, videoContractWrite(duration)))}
              >
                Chuẩn bị video
              </Button>
            </>
          ) : (
            <>
              <p className="fx-dir-ws__ok">ẢNH ĐÃ ĐƯỢC DUYỆT ✓</p>
              <p className="fx-dir-ws__ask">Bước tiếp theo: chuẩn bị video cho cảnh này.</p>
              {preview ? (
                <div className="fx-dir-ws__hero">
                  <img src={preview} alt="Approved production still" />
                </div>
              ) : null}
              <aside className="fx-dir-ws__follow">
                <p className="fx-dir-ws__action-kicker">BƯỚC TIẾP THEO</p>
                <p>Chuẩn bị video</p>
              </aside>
              <Button type="primary" size="large" onClick={() => setGoVideoContract(true)}>
                ĐI ĐẾN BƯỚC VIDEO →
              </Button>
            </>
          )
        ) : null}

        {scenePane === 'video' && derived.action === 'APPROVE_VIDEO_CONTRACT' ? (
          <>
            <h3>{derived.currentActionTitle}</h3>
            <p className="fx-dir-ws__ask">{derived.currentActionBody}</p>
            <Space>
              <Button
                aria-label="VALIDATE CONTRACT"
                disabled={!shotId || !video?.canValidate || busy}
                loading={busy}
                onClick={() => run(() => validateProductionVideoContract(shotId!, video?.id || undefined, videoContractWrite(duration)))}
              >
                Kiểm tra điều kiện
              </Button>
              <Button
                type="primary"
                aria-label="DIRECTOR APPROVE"
                disabled={!shotId || !video?.id || !video?.canApprove || busy}
                loading={busy}
                onClick={() => run(() => approveProductionVideoContract(shotId!, video!.id!))}
              >
                Duyệt kế hoạch video
              </Button>
            </Space>
          </>
        ) : null}

        {scenePane === 'video' && derived.action === 'PREFLIGHT' ? (
          <>
            <h3>{derived.currentActionTitle}</h3>
            <p className="fx-dir-ws__ask">{derived.currentActionBody}</p>
            <Button
              size="large"
              aria-label="PRE-FLIGHT"
              disabled={!shotId || busy}
              loading={busy}
              onClick={() => run(() => preflightVideoGenerationExecution(shotId!))}
            >
              Kiểm tra điều kiện
            </Button>
          </>
        ) : null}

        {scenePane === 'video' && derived.action === 'EXECUTE' && !videoBusy ? (
          <>
            <h3>{derived.currentActionTitle}</h3>
            <p className="fx-dir-ws__ask">{derived.currentActionBody}</p>
            <Button
              type="primary"
              danger
              size="large"
              aria-label="EXECUTE VIDEO"
              disabled={!shotId || !generation?.canExecute || busy}
              onClick={() => setExecuteOpen(true)}
            >
              {derived.next.includes('Tạo lại') ? 'Tạo lại một lần' : 'Tạo video'}
            </Button>
          </>
        ) : null}

        {scenePane === 'video' && videoBusy && derived.action !== 'VIDEO_REVIEW' && derived.action !== 'DONE' ? (
          <>
            <h3>Đang tạo video</h3>
            <p className="fx-dir-ws__ask">Nhà cung cấp đang làm video. Không bấm tạo lại.</p>
            <Button size="large" disabled={!shotId || busy} onClick={() => shotId && load(shotId)}>
              Kiểm tra lại
            </Button>
          </>
        ) : null}

        {scenePane === 'video' && derived.action === 'VIDEO_REVIEW' ? (
          <>
            <p className="fx-dir-ws__stage">🟡 Chờ duyệt</p>
            {clip ? (
              <>
                <video src={clip} controls playsInline preload="metadata" className="fx-dir-ws__player" />
                <p className="fx-dir-ws__dl">
                  <a href={clip} download={`${shortShotLabel(shotCode)}.mp4`}>
                    Tải video · {shortShotLabel(shotCode)}.mp4
                  </a>
                </p>
              </>
            ) : (
              <p>Đang tải video để xem…</p>
            )}
            <p className="fx-dir-ws__ask">Bạn có hài lòng với video này?</p>
            <Space>
              <Button
                type="primary"
                aria-label="APPROVE VIDEO"
                disabled={!shotId || !generation?.canApprove || busy}
                loading={busy}
                onClick={() => run(() => approveVideoGenerationExecution(shotId!))}
              >
                ✓ DUYỆT VIDEO
              </Button>
              <Button
                aria-label="REJECT VIDEO"
                disabled={!shotId || !generation?.canReject || busy}
                onClick={() => setRejectVideoOpen(true)}
              >
                Yêu cầu làm lại
              </Button>
            </Space>
          </>
        ) : null}

        {scenePane === 'video' && derived.action !== 'VIDEO_REVIEW' && derived.action !== 'DONE' && derived.action !== 'CREATE_VIDEO_CONTRACT' && derived.action !== 'APPROVE_VIDEO_CONTRACT' && derived.action !== 'PREFLIGHT' && derived.action !== 'EXECUTE' && !videoBusy ? (
          <p className="fx-dir-ws__hold">🔒 Chưa thể tạo video. Bạn cần duyệt hình ảnh trước.</p>
        ) : null}
        {scenePane === 'video' && derived.action === 'DONE' ? (
          <>
            {clip ? (
              <video src={clip} controls playsInline preload="metadata" className="fx-dir-ws__player" />
            ) : null}
            <p className="fx-dir-ws__ok">✓ VIDEO ĐÃ ĐƯỢC DUYỆT</p>
            <p className="fx-dir-ws__ask">Cảnh này đã hoàn thành.</p>
            {clip ? (
              <p className="fx-dir-ws__dl">
                <a href={clip} download={`${shortShotLabel(shotCode)}.mp4`}>
                  Tải video · {shortShotLabel(shotCode)}.mp4
                </a>
              </p>
            ) : null}
            <Space>
              {onBack ? (
                <Button size="large" onClick={onBack}>
                  ← Về danh sách cảnh
                </Button>
              ) : null}
              {onContinue ? (
                <Button type="primary" size="large" onClick={onContinue}>
                  Sang hoàn thiện tập →
                </Button>
              ) : null}
            </Space>
          </>
        ) : null}

        {derived.action === 'WAIT' ? (
          <>
            <h3>{derived.currentActionTitle}</h3>
            <p className="fx-dir-ws__ask">{derived.currentActionBody}</p>
          </>
        ) : null}
      </div>

      {showTechnical ? (
      <Collapse
        className="fx-dir-ws__tech"
        destroyOnHidden
        items={[
          {
            key: 'tech',
            label: 'Chi tiết kỹ thuật',
            children: (
              <>
                <div className="fx-dir-ws__sha">
                  <div>Shot {shotCode}</div>
                  <div>Shot ID {shotId || '—'}</div>
                  <div>Execution ID {review?.executionId || '—'}</div>
                  <div>Status {directorStatusLabel(review?.executionStatus)}</div>
                  {[
                    ['Master SHA', review?.masterSha256],
                    ['DNA SHA', review?.dnaSha256],
                    ['PRP SHA', review?.prpSha256],
                    ['Shot Contract SHA', review?.shotContractSha256],
                    ['Prompt SHA', review?.promptSha256],
                    ['IGC SHA', review?.igcSha256],
                    ['Artifact SHA', review?.artifactSha256],
                    ['Fingerprint', generation?.executionFingerprint],
                  ].map(([label, sha]) => (
                    <div key={label}>
                      {label} {truncateSha(sha)}{' '}
                      <Button size="small" type="link" onClick={() => void copySha(String(label), sha)}>
                        {copied === label ? 'Đã chép' : 'COPY'}
                      </Button>
                    </div>
                  ))}
                  <div>Provider status {generation?.providerStatus || '—'}</div>
                  <div>generation {String(generation?.generation ?? false)}</div>
                  <div>runProvider {String(generation?.runProvider ?? false)}</div>
                  <div>preflightPass {String(generation?.preflightPass ?? false)}</div>
                  {qa.total ? (
                    <div>
                      QA {qa.passed}/{qa.total}
                      {review?.qa?.technical ? ` · Ảnh rõ ${review.qa.technical}` : ''}
                    </div>
                  ) : null}
                </div>
                {children}
              </>
            ),
          },
          {
            key: 'log',
            label: 'Nhật ký hệ thống',
            children: (
              <div className="fx-dir-ws__sha">
                <div>lifecycle {directorStatusLabel(review?.executionStatus)}</div>
                <div>director {directorStatusLabel(review?.directorApproval)}</div>
                <div>video {directorStatusLabel(video?.status)}</div>
                <div>generation {directorStatusLabel(generation?.status)}</div>
                <div>provider {generation?.providerStatus || '—'}</div>
                <div>generationFlag {String(generation?.generation ?? false)}</div>
                <div>runProvider {String(generation?.runProvider ?? false)}</div>
                <div>preflightPass {String(generation?.preflightPass ?? false)}</div>
              </div>
            ),
          },
        ]}
      />
      ) : null}

      <Modal
        title="Yêu cầu làm lại ảnh?"
        open={rejectOpen}
        okText="Gửi yêu cầu làm lại"
        cancelText="Hủy"
        okButtonProps={{ danger: true, disabled: [...redoTags, reason.trim()].join(' ').length < 3, 'aria-label': 'REJECT IMAGE' }}
        onCancel={() => setRejectOpen(false)}
        onOk={() => {
          const text = [redoTags.join(', '), reason.trim()].filter(Boolean).join('. ');
          if (!shotId || text.length < 3) return;
          setRejectOpen(false);
          run(() => rejectImageDirectorReview(shotId, text));
        }}
      >
        <Typography.Paragraph>Bạn muốn thay đổi điều gì?</Typography.Paragraph>
        <div className="fx-dir-ws__chips">
          {['Nhân vật', 'Bối cảnh', 'Góc máy', 'Ánh sáng', 'Hành động', 'Khác'].map((tag) => (
            <Button
              key={tag}
              size="small"
              type={redoTags.includes(tag) ? 'primary' : 'default'}
              onClick={() => setRedoTags((cur) => (cur.includes(tag) ? cur.filter((t) => t !== tag) : [...cur, tag]))}
            >
              {tag}
            </Button>
          ))}
        </div>
        <Input.TextArea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ghi chú" />
      </Modal>

      <Modal
        title="Duyệt ảnh này?"
        open={approveOpen}
        okText="✓ DUYỆT ẢNH"
        cancelText="Hủy"
        okButtonProps={{ 'aria-label': 'APPROVE IMAGE' }}
        onCancel={() => setApproveOpen(false)}
        onOk={() => {
          if (!shotId) return;
          setApproveOpen(false);
          setGoVideoContract(false);
          run(() => approveImageDirectorReview(shotId));
        }}
      >
        <Typography.Paragraph>
          {shotDisplayName(shotCode)}
          {title ? ` — ${title}` : ''}
        </Typography.Paragraph>
        <Typography.Paragraph>Ảnh đã duyệt sẽ là neo hình. Video Contract không tự tạo.</Typography.Paragraph>
      </Modal>

      <Modal
        title="Tạo video production?"
        open={executeOpen}
        okText={derived.next.includes('Tạo lại') ? 'Tạo lại một lần' : 'Tạo video'}
        cancelText="Hủy"
        okButtonProps={{ danger: true, 'aria-label': 'EXECUTE VIDEO' }}
        onCancel={() => setExecuteOpen(false)}
        onOk={() => {
          if (!shotId) return;
          setExecuteOpen(false);
          setWaitingVideo(true);
          run(() => executeVideoGenerationExecution(shotId));
        }}
      >
        <Typography.Paragraph>
          Bước này sẽ gọi nhà cung cấp và tạo đúng một video cho {shortShotLabel(shotCode)}.
        </Typography.Paragraph>
        <Typography.Paragraph>Thời lượng: {generation?.durationSeconds || duration} giây · Số lần chạy: 1</Typography.Paragraph>
      </Modal>

      <Modal
        title="Yêu cầu làm lại video?"
        open={rejectVideoOpen}
        okText="Gửi yêu cầu làm lại"
        cancelText="Hủy"
        okButtonProps={{ danger: true, disabled: [...redoTags, reason.trim()].join(' ').length < 3, 'aria-label': 'REJECT VIDEO' }}
        onCancel={() => setRejectVideoOpen(false)}
        onOk={() => {
          const text = [redoTags.join(', '), reason.trim()].filter(Boolean).join('. ');
          if (!shotId || text.length < 3) return;
          setRejectVideoOpen(false);
          run(() => rejectVideoGenerationExecution(shotId, text));
        }}
      >
        <Typography.Paragraph>Bạn muốn thay đổi điều gì?</Typography.Paragraph>
        <div className="fx-dir-ws__chips">
          {['Chuyển động', 'Biểu cảm', 'Camera', 'Hành động', 'Bối cảnh', 'Khác'].map((tag) => (
            <Button
              key={tag}
              size="small"
              type={redoTags.includes(tag) ? 'primary' : 'default'}
              onClick={() => setRedoTags((cur) => (cur.includes(tag) ? cur.filter((t) => t !== tag) : [...cur, tag]))}
            >
              {tag}
            </Button>
          ))}
        </div>
        <Input.TextArea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ghi chú" />
      </Modal>

      <Modal open={lightbox} footer={null} onCancel={() => setLightbox(false)} width="90vw">
        {preview ? <img src={preview} alt="Approved production still" style={{ width: '100%' }} /> : null}
      </Modal>
    </section>
  );
}
