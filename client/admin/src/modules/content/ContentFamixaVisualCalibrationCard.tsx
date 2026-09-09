import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Collapse, Modal, Radio, Space, Tag } from 'antd';
import {
  approveVisualCalibrationPack,
  createVisualCalibrationPack,
  fetchIdentityConditionedCalibrationDirectorReview,
  fetchVisualCalibrationPack,
  fetchVisualUniverseAuthority,
  generateVisualCalibrationPack,
  lockVisualCalibrationPack,
  rejectVisualCalibrationPack,
  type IdentityConditionedCalibrationPackReview,
  type VisualCalibrationPack,
  type VisualUniverseAuthority,
} from '@/shared/api/content.api';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { FAMIXA_VISUAL_MODE, visualModeLabel } from './kit-video-visual-mode';
import { http } from '@/shared/api/http';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  CALIBRATION_DIRECTOR_FAIL_REASONS,
  CALIBRATION_REQUIRED_SLOTS,
  CALIBRATION_VIEWS,
  VISUAL_CALIBRATION_PACK_ID,
  calibrationCoverageValid,
  calibrationMayApprove,
  calibrationMayCreate,
  calibrationMayGenerate,
  calibrationMayLock,
  calibrationSlotImageUrl,
  calibrationSlotStatus,
  calibrationStatusLabel,
  calibrationViewLabel,
  DIRECTOR_REVIEW_ADVANCED,
  DIRECTOR_REVIEW_OPEN_LABEL,
} from './kit-video-visual-calibration';
import { ContentFamixaDirectorReviewHandoff } from './ContentFamixaDirectorReviewHandoff';

async function authImage(url: string) {
  const { data } = await http.get<Blob>(url.replace(/^\/api/, ''), { responseType: 'blob' });
  return URL.createObjectURL(data);
}

function visualApprovalLabel(pack?: VisualCalibrationPack) {
  if (pack?.directorPass === true) return 'PASS';
  if ((pack?.status || '').toUpperCase() === 'REJECTED') return 'FAIL';
  return 'PENDING';
}

export function ContentFamixaVisualCalibrationCard({
  markBusy,
  onMark,
}: {
  markBusy?: boolean;
  onMark?: () => void;
} = {}) {
  const [pack, setPack] = useState<VisualCalibrationPack>();
  const [universe, setUniverse] = useState<VisualUniverseAuthority>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [confirm, setConfirm] = useState<'create' | 'generate' | 'approve' | 'reject' | 'lock' | null>(null);
  const [failReason, setFailReason] = useState<(typeof CALIBRATION_DIRECTOR_FAIL_REASONS)[number]>(
    CALIBRATION_DIRECTOR_FAIL_REASONS[0],
  );
  const [images, setImages] = useState<Record<string, string>>({});
  const [review, setReview] = useState<IdentityConditionedCalibrationPackReview>();

  const load = () => {
    setBusy(true);
    void Promise.all([
      fetchVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID),
      fetchVisualUniverseAuthority('FAMIXA').catch(() => undefined),
      fetchIdentityConditionedCalibrationDirectorReview().catch(() => undefined),
    ])
      .then(([next, vua, workspace]) => {
        setPack(next);
        setUniverse(vua);
        setReview(workspace);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Visual Calibration.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!generating) return;
    const tick = window.setInterval(() => {
      void fetchVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID)
        .then((next) => setPack(next))
        .catch(() => undefined);
    }, 2000);
    return () => window.clearInterval(tick);
  }, [generating]);

  const slots = useMemo(() => {
    const subjects = pack?.subjects?.length
      ? pack.subjects
      : [
          { calibrationSubjectId: 'CAL-001', label: 'Child Boy', chronologicalAge: 11, artifacts: [] },
          { calibrationSubjectId: 'CAL-002', label: 'Child Girl', chronologicalAge: 11, artifacts: [] },
          { calibrationSubjectId: 'CAL-003', label: 'Adult Male', chronologicalAge: 35, artifacts: [] },
          { calibrationSubjectId: 'CAL-004', label: 'Adult Female', chronologicalAge: 35, artifacts: [] },
          { calibrationSubjectId: 'CAL-005', label: 'Older Adult Male', chronologicalAge: 65, artifacts: [] },
          { calibrationSubjectId: 'CAL-006', label: 'Adult Female Full Body', chronologicalAge: 35, artifacts: [] },
        ];
    return subjects.map((subject) => ({
      ...subject,
      views: CALIBRATION_VIEWS.map((view) => {
        const art = subject.artifacts?.find((a) => a.viewType === view);
        return {
          view,
          artifact: art,
          status: calibrationSlotStatus(art, pack?.status),
        };
      }),
    }));
  }, [pack]);

  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    const jobs = slots.flatMap((subject) =>
      subject.views
        .filter((row) => row.artifact?.path || row.artifact?.imageUrl)
        .map((row) => {
          const url =
            row.artifact?.imageUrl
            || calibrationSlotImageUrl(
              pack?.packId || VISUAL_CALIBRATION_PACK_ID,
              subject.calibrationSubjectId,
              row.view,
            );
          return [`${subject.calibrationSubjectId}/${row.view}`, url] as const;
        }),
    );
    void Promise.all(
      jobs.map(([key, url]) =>
        authImage(url)
          .then((blob) => {
            made.push(blob);
            return [key, blob] as const;
          })
          .catch(() => undefined),
      ),
    ).then((pairs) => {
      if (dead) {
        made.forEach((u) => URL.revokeObjectURL(u));
        return;
      }
      const next: Record<string, string> = {};
      for (const pair of pairs) if (pair) next[pair[0]] = pair[1];
      setImages(next);
    });
    return () => {
      dead = true;
      made.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [pack?.packId, pack?.coverage?.valid, pack?.generationExecutionId, pack?.status]);

  const run = (work: () => Promise<VisualCalibrationPack>) => {
    setBusy(true);
    void work()
      .then((next) => {
        if (next.gateCode) throw new Error(next.staffMessage || next.gateCode);
        setPack(next);
        setConfirm(null);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không thực hiện được.')))
      .finally(() => setBusy(false));
  };

  const status = pack?.status || 'DRAFT';
  const valid = pack?.coverage?.valid ?? 0;
  const required = pack?.coverage?.required ?? CALIBRATION_REQUIRED_SLOTS;
  const rows = pack?.subjects?.length
    ? pack.subjects
    : ['Child Boy', 'Child Girl', 'Adult Male', 'Adult Female', 'Older Adult', 'Full Body'].map((label) => ({
        calibrationSubjectId: label,
        label,
        gender: '',
        chronologicalAge: 0,
        artifacts: [],
      }));

  return (
    <Card id="famixa-visual-calibration" className="fx-look__card fx-pvs fx-pvs--director" size="small" title="Visual Calibration">
      <ContentFamixaVisualModeBadge compact />
      <p className="fx-desk__note">
        Calibration inherit Project Visual Mode · {visualModeLabel(FAMIXA_VISUAL_MODE.visualMode)}.
        FRONT / THREE_QUARTER / SIDE / FULL_BODY cùng một Visual Mode. Không chọn style riêng.
      </p>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <div className="fx-pvs__summary">
        <div>
          <p className="fx-pvs__kicker">VISUAL UNIVERSE</p>
          <p>
            Current: <b>{universe?.currentAuthority ? universe.version : 'V1'}</b>
          </p>
          <Tag>{universe?.currentAuthority ? 'LOCKED' : 'PVS V1 ACTIVE'}</Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Candidate</p>
          <p>
            <b>{universe?.version || 'V1'}</b>
          </p>
          <Tag color={status === 'APPROVED' || status === 'LOCKED' ? 'green' : 'default'}>
            {calibrationStatusLabel(status)}
          </Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Calibration Generation</p>
          <p>
            <b>
              {valid} / {required}
            </b>
          </p>
          <Tag>{generating || status === 'GENERATING' ? 'GENERATING' : calibrationStatusLabel(status)}</Tag>
        </div>
      </div>
      <p className="fx-desk__note">
        Đây là bài test phong cách, chưa phải nhân vật thật. 6 nhân vật mẫu · 24 ảnh · cùng một Visual Universe.
      </p>
      <p className="fx-desk__note">
        Coverage {pack?.coverage?.valid ?? 0}/{pack?.coverage?.required ?? CALIBRATION_REQUIRED_SLOTS}
        {pack?.authorityTransitioned ? ' · VUA promoted' : ' · VUA not promoted'}
      </p>
      <div className="fx-cal__review-summary">
        <p>
          Coverage: <b>{valid} / {required}</b>
        </p>
        <p>
          Pixel validity: <b>{valid} / {required}</b>
        </p>
        <p>
          Visual approval: <b>{visualApprovalLabel(pack)}</b>
        </p>
        <p>
          Visual Universe: <b>{universe?.currentAuthority ? 'LOCKED' : 'PENDING DIRECTOR REVIEW'}</b>
        </p>
        <p>
          Authority: <b>UNCHANGED</b>
        </p>
      </div>
      {calibrationCoverageValid(pack?.coverage) ? (
        <ContentFamixaDirectorReviewHandoff review={review} busy={markBusy} onMark={onMark} />
      ) : (
        <p className="fx-desk__note">
          6 archetypes có thực sự trông như cùng một FAMIXA Visual Universe không?
        </p>
      )}
      <p className="fx-desk__note">
        Generation ở đây. Review 24 ảnh ở Director Review workspace — không dùng Master Revision / Tạo lại bộ ảnh cho calibration.
        {' '}
        {DIRECTOR_REVIEW_OPEN_LABEL}
      </p>
      <Collapse
        ghost
        className="fx-director-tech"
        items={[{
          key: 'cal-images',
          label: 'Xem 24 ảnh đã tạo',
          children: (
            <div className="fx-cal__subjects">
              {slots.map((subject) => (
                <section key={subject.calibrationSubjectId} className="fx-cal__subject">
                  <h4>
                    {subject.calibrationSubjectId} {subject.label}
                  </h4>
                  <div className="fx-cstudio__grid fx-cal__grid">
                    {subject.views.map((row) => (
                      <div key={`${subject.calibrationSubjectId}-${row.view}`} className="fx-cstudio__card">
                        {images[`${subject.calibrationSubjectId}/${row.view}`] ? (
                          <img src={images[`${subject.calibrationSubjectId}/${row.view}`]} alt="" />
                        ) : (
                          <div className="fx-clib__ph" />
                        )}
                        <strong>{calibrationViewLabel(row.view)}</strong>
                        <p>{row.status}</p>
                      </div>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          ),
        }]}
      />
      <div className="fx-cstudio__grid" style={{ display: 'none', gridTemplateColumns: 'repeat(4, minmax(88px, 1fr))' }}>
        {rows.map((subject) =>
          CALIBRATION_VIEWS.map((view) => {
            const art = subject.artifacts?.find((a) => a.viewType === view);
            return (
              <div key={`${subject.calibrationSubjectId}-${view}`} className="fx-cstudio__card">
                {art?.path ? <img src={art.path} alt="" /> : <div className="fx-clib__ph" />}
                <strong>{subject.label}</strong>
                <p>{view === 'THREE_QUARTER' ? '3/4' : view === 'FULL_BODY' ? 'Full Body' : view === 'FRONT' ? 'Front' : 'Side'}</p>
              </div>
            );
          }),
        )}
      </div>
      {calibrationCoverageValid(pack?.coverage) ? (
        <Collapse
          ghost
          className="fx-director-tech"
          items={[{
            key: 'cal-adv',
            label: DIRECTOR_REVIEW_ADVANCED,
            children: (
              <Space wrap>
                {calibrationMayGenerate(status, pack?.coverage) ? (
                  <Button loading={busy || generating} title="Generate Calibration Pack" onClick={() => setConfirm('generate')}>
                    Tạo 24 ảnh Calibration
                  </Button>
                ) : (
                  <Button disabled>Tạo 24 ảnh Calibration</Button>
                )}
                {calibrationMayApprove(status, pack?.coverage) ? (
                  <Button loading={busy} onClick={() => setConfirm('approve')}>Duyệt</Button>
                ) : null}
                {status !== 'LOCKED' && status !== 'GENERATING' ? (
                  <Button loading={busy} onClick={() => setConfirm('reject')}>Không đạt</Button>
                ) : null}
                {calibrationMayLock(status) ? (
                  <Button loading={busy} onClick={() => setConfirm('lock')}>Khóa Visual Universe</Button>
                ) : null}
              </Space>
            ),
          }]}
        />
      ) : (
      <Space wrap style={{ marginTop: 8 }}>
        {calibrationMayCreate(status) ? (
          <Button loading={busy} onClick={() => setConfirm('create')}>
            Tạo bộ kiểm tra phong cách
          </Button>
        ) : null}
        {calibrationMayGenerate(status, pack?.coverage) ? (
          <Button
            type="primary"
            loading={busy || generating}
            title="Generate Calibration Pack"
            onClick={() => setConfirm('generate')}
          >
            Tạo 24 ảnh Calibration
          </Button>
        ) : null}
      </Space>
      )}
      <Collapse
        ghost
        className="fx-director-tech"
        items={[
          {
            key: 'cal-tech',
            label: 'Chi tiết kỹ thuật',
            children: (
              <div>
                <p>Pack {pack?.packId || VISUAL_CALIBRATION_PACK_ID}</p>
                <p>VUA {pack?.visualUniverseSha || universe?.sha256 || '—'}</p>
                <p>PVS {pack?.projectVisualStyleSha || '—'}</p>
                <p>CDL {pack?.characterDesignLanguageSha || '—'}</p>
                <p>Impact {pack?.impact?.charactersAffected ?? 0} / locked {pack?.impact?.lockedCharacters ?? 0}</p>
              </div>
            ),
          },
        ]}
      />
      <Modal
        title="Xác nhận"
        open={!!confirm}
        confirmLoading={busy || generating}
        okText={confirm === 'generate' ? 'Xác nhận tạo 24 ảnh' : confirm === 'approve' ? 'PASS' : confirm === 'reject' ? 'FAIL' : 'Xác nhận'}
        cancelText="Hủy"
        onCancel={() => setConfirm(null)}
        onOk={() => {
          if (confirm === 'create')
            run(() => createVisualCalibrationPack({ confirm: true, packId: VISUAL_CALIBRATION_PACK_ID }));
          else if (confirm === 'generate') {
            setGenerating(true);
            setBusy(true);
            void generateVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID, { confirm: true, generate: true })
              .then((next) => {
                if (next.gateCode && next.gateCode !== 'CALIBRATION_INCOMPLETE') {
                  throw new Error(next.staffMessage || next.gateCode);
                }
                setPack(next);
                setConfirm(null);
                setError(undefined);
              })
              .catch((e) => setError(apiErrorMessage(e, 'Không tạo được 24 ảnh Calibration.')))
              .finally(() => {
                setBusy(false);
                setGenerating(false);
              });
          } else if (confirm === 'approve')
            run(() =>
              approveVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID, { confirm: true, directorPass: true }),
            );
          else if (confirm === 'reject')
            run(() =>
              rejectVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID, {
                confirm: true,
                rejectionReason: failReason,
              }),
            );
          else if (confirm === 'lock')
            run(() => lockVisualCalibrationPack(VISUAL_CALIBRATION_PACK_ID, { confirm: true }));
        }}
      >
        {confirm === 'generate' ? (
          <>
            <p>24 ảnh sẽ được tạo.</p>
            <p>Dùng để kiểm tra Visual Universe.</p>
            <p>Không tạo nhân vật.</p>
            <p>Không thay đổi Character Master.</p>
            <p>Không Approve.</p>
            <p>Không Lock.</p>
            <p>Không thay đổi Authority.</p>
            <p className="fx-desk__note">
              Generate Calibration Pack: 24 PIXEL qua provider. Không tự duyệt / khóa. Xác nhận?
            </p>
          </>
        ) : confirm === 'approve' ? (
          <>
            <p>Director đánh giá visual PASS?</p>
            <p className="fx-desk__note">Coverage 24/24 không phải visual PASS. Chỉ Director quyết định.</p>
          </>
        ) : confirm === 'reject' ? (
          <>
            <p>Director đánh giá visual FAIL?</p>
            <Radio.Group value={failReason} onChange={(e) => setFailReason(e.target.value)}>
              {CALIBRATION_DIRECTOR_FAIL_REASONS.map((reason) => (
                <Radio key={reason} value={reason} style={{ display: 'block', marginBottom: 6 }}>
                  {reason}
                </Radio>
              ))}
            </Radio.Group>
          </>
        ) : confirm === 'lock' ? (
          'Khóa pack. Không tự đổi PVS V1. Character LOCKED không bị mutate.'
        ) : (
          'Đây là bài test phong cách. Không tạo Character Master.'
        )}
      </Modal>
    </Card>
  );
}
