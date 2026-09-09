import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Modal, Radio, Space, Tag, Typography } from 'antd';
import {
  approveProjectVisualStyleRevision,
  fetchProjectVisualStyle,
  fetchProjectVisualStyleRevisionImpact,
  initializeProjectVisualStyle,
  lockProjectVisualStyleRevision,
  rejectProjectVisualStyleRevision,
  requestProjectVisualStyleRevision,
  type ProjectVisualStyleImpact,
  type ProjectVisualStyleRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { directorPvsStatusLabel } from './kit-video-character-studio';
import {
  projectStyleReady,
  visualStyleRevisionMayApprove,
  visualStyleRevisionMayLock,
  visualStyleRevisionMayReject,
  visualStyleRevisionMayRequest,
} from './kit-video-project-visual-style';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { VISUAL_MODES, pvsKeyOfVisualMode, visualModeLabel } from './kit-video-visual-mode';

export function ContentFamixaProjectVisualStyleCard() {
  const [row, setRow] = useState<ProjectVisualStyleRow>();
  const [impact, setImpact] = useState<ProjectVisualStyleImpact>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [preset, setPreset] = useState('3D_STYLIZED_REALISM');
  const [candidateOpen, setCandidateOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const load = () => {
    setBusy(true);
    void Promise.all([
      fetchProjectVisualStyle('FAMIXA'),
      fetchProjectVisualStyleRevisionImpact('FAMIXA').catch(() => undefined),
    ])
      .then(([next, nextImpact]) => {
        setRow(next);
        if (next.styleKey) setPreset(next.styleKey);
        setImpact(nextImpact);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được phong cách hình ảnh của Project.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const setup = () => {
    setBusy(true);
    if (!preset) return;
    void initializeProjectVisualStyle({
      projectId: 'FAMIXA',
      presetKey: pvsKeyOfVisualMode(preset),
      confirm: true,
      activate: true,
    })
      .then((next) => {
        setRow(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không thiết lập được phong cách.')))
      .finally(() => setBusy(false));
  };

  const applyResult = (result: { style?: ProjectVisualStyleRow | null; staffMessage?: string; gateCode?: string | null }) => {
    if (result.style) setRow(result.style);
    if (result.gateCode) throw new Error(result.staffMessage || result.gateCode);
    void fetchProjectVisualStyleRevisionImpact('FAMIXA').then(setImpact).catch(() => undefined);
  };

  const ready = projectStyleReady(row);
  const chosen = row?.presets?.find((p) => p.styleKey === preset);
  const rev = row?.revision;
  const revStatus = rev?.status || '';
  const mayRequest = visualStyleRevisionMayRequest(revStatus);
  const mayApprove = visualStyleRevisionMayApprove(revStatus) || !!rev?.mayApprove;
  const mayReject = visualStyleRevisionMayReject(revStatus) || !!rev?.mayReject;
  const mayLock = visualStyleRevisionMayLock(revStatus) || !!rev?.mayLock;

  return (
    <Card className="fx-look__card fx-pvs fx-pvs--director" size="small" title="PROJECT VISUAL STYLE">
      <ContentFamixaVisualModeBadge compact />
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {ready ? (
        <>
          <div className="fx-pvs__summary">
            <div>
              <p className="fx-pvs__kicker">Current Authority</p>
              <p>
                <b>{row?.styleName || 'Famixa Stylized 3D V1'}</b>
              </p>
              <Tag color="green">{directorPvsStatusLabel(row?.status)}</Tag>
            </div>
            <div>
              <p className="fx-pvs__kicker">Candidate</p>
              <p>
                <b>Famixa Stylized 3D V2</b>
              </p>
              <Tag color={revStatus === 'PENDING_REVIEW' ? 'gold' : revStatus === 'APPROVED' ? 'blue' : revStatus === 'LOCKED' ? 'green' : 'default'}>
                {directorPvsStatusLabel(revStatus || 'DRAFT')}
              </Tag>
            </div>
            <div>
              <p className="fx-pvs__kicker">Stylization</p>
              <p>
                <b>{rev?.stylizationLevel || 'STRONG'}</b>
              </p>
              <p className="fx-desk__note">Photorealism {rev?.photorealismLevel || 'LOW'}</p>
            </div>
          </div>
          <p className="fx-desk__note">Authority cấp Project — không phải setting của từng nhân vật.</p>
          <p className="fx-desk__note">
            Đổi Visual Mode không mutate authority hiện tại. Phải CREATE VISUAL REVISION.
            Thay đổi Visual Mode sẽ tạo một visual universe revision mới và có thể yêu cầu xây dựng lại Character / Calibration.
          </p>

          {candidateOpen ? (
            <div className="fx-pvs__preview">
              <p>
                <b>Overall direction</b> · Famixa Stylized 3D
              </p>
              <p>Nhân vật 3D designed, ấm, đương đại — không phải chân dung người thật.</p>
            </div>
          ) : null}

          {impact ? (
            <p className="fx-desk__note">
              PVS V2 Impact · {impact.wouldStaleCount} characters affected · {impact.lockedCount} locked character
              protected
            </p>
          ) : null}

          <Space wrap style={{ marginTop: 8 }}>
            <Button onClick={() => setCandidateOpen((v) => !v)}>
              {candidateOpen ? 'Ẩn Candidate' : 'Xem Candidate'}
            </Button>
            {mayRequest ? (
              <Button type="primary" loading={busy} onClick={() => setConfirmOpen(true)}>
                Tạo Visual Style Revision
              </Button>
            ) : null}
            {mayApprove ? (
              <Button
                type="primary"
                loading={busy}
                onClick={() => {
                  setBusy(true);
                  void approveProjectVisualStyleRevision('FAMIXA')
                    .then(applyResult)
                    .catch((e) => setError(apiErrorMessage(e, 'Không duyệt được Visual Style.')))
                    .finally(() => setBusy(false));
                }}
              >
                Duyệt
              </Button>
            ) : null}
            {mayReject ? (
              <Button
                loading={busy}
                onClick={() => {
                  setBusy(true);
                  void rejectProjectVisualStyleRevision('FAMIXA', 'STYLE')
                    .then(applyResult)
                    .catch((e) => setError(apiErrorMessage(e, 'Không từ chối được Visual Style.')))
                    .finally(() => setBusy(false));
                }}
              >
                Không đạt
              </Button>
            ) : null}
            {mayLock ? (
              <Button
                type="primary"
                loading={busy}
                onClick={() => {
                  setBusy(true);
                  void lockProjectVisualStyleRevision('FAMIXA')
                    .then(applyResult)
                    .catch((e) => setError(apiErrorMessage(e, 'Không khóa được Visual Style.')))
                    .finally(() => setBusy(false));
                }}
              >
                Khóa Visual Style
              </Button>
            ) : null}
          </Space>

          <Collapse
            ghost
            className="fx-director-tech"
            items={[
              {
                key: 'pvs-tech',
                label: 'Chi tiết Visual Style',
                children: (
                  <div>
                    <p>CURRENT AUTHORITY</p>
                    <p>
                      {row?.styleName} · {row?.version} · {row?.status} · Authority {row?.authority}
                    </p>
                    <p>PVS SHA {row?.sha || '—'}</p>
                    <p>currentIsAuthority {String(rev?.currentIsAuthority !== false)}</p>
                    <p>candidateIsAuthority {String(!!rev?.candidateIsAuthority)}</p>
                    <p>Candidate SHA {rev?.candidateSha || '—'}</p>
                    <p>{rev?.styleIntent}</p>
                    <p>{rev?.canonicalStyleDescription}</p>
                    <p>Realism boundary: {rev?.humanRealismBoundary}</p>
                    <p>Design: {rev?.characterDesignLanguage}</p>
                    <p>Face: {rev?.faceStyle}</p>
                    <p>Eyes: {rev?.eyeStyle}</p>
                    <p>Skin: {rev?.skinStyle}</p>
                    <p>Hair: {rev?.hairStyle}</p>
                    <p>Body: {rev?.bodyStyle}</p>
                    <p>Clothing: {rev?.clothingStyle}</p>
                    <p>Lighting: {rev?.lightingStyle}</p>
                    <p>Materials: {rev?.materialStyle}</p>
                    <p>Environment: {rev?.environmentStyle}</p>
                    <p>Negative: {rev?.negativeStyleBlock}</p>
                    <p>Prompt / Generation Brief</p>
                    <p>{rev?.stylePromptBlock || row?.prompt}</p>
                    <p>Regression Details</p>
                    <p>
                      Studio V1 · Unified Generation · Age Consistency · Age Gate · Appearance Profile · Master
                      Revision · PVS V2 · TS Smoke · C# Build
                    </p>
                    <ul className="fx-crp__checks">
                      {(row?.preview ?? []).map((line) => (
                        <li key={line}>{line}</li>
                      ))}
                      <li>Rendering {row?.renderingStyle}</li>
                    </ul>
                  </div>
                ),
              },
              ...(impact
                ? [
                    {
                      key: 'impact',
                      label: 'Impact Details',
                      children: (
                        <div>
                          <p>
                            {impact.wouldStaleCount} artifact sẽ STALE nếu khóa V2 · {impact.lockedCount} nhân vật khóa
                            không bị ghi
                          </p>
                          <ul className="fx-crp__checks">
                            {(impact.characters ?? []).map((c) => (
                              <li key={c.characterId}>
                                {c.characterName || c.characterId}
                                {c.officialLocked ? ' · LOCKED · mutationForbidden' : ''}
                                {' · '}
                                Master {(c.masterSha || '—').slice(0, 12)}
                                {' · '}
                                CRP {(c.crpSha || '—').slice(0, 12)}
                                {c.masterStale || c.crpStale ? ' · would STALE' : ''}
                              </li>
                            ))}
                          </ul>
                        </div>
                      ),
                    },
                  ]
                : []),
            ]}
          />
        </>
      ) : (
        <>
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 8 }}
            message="Chọn phong cách hình ảnh cho toàn bộ dự án. Không tạo Project nếu chưa chọn Visual Mode."
          />
          <p className="fx-pvs__kicker">PROJECT VISUAL STYLE</p>
          <p className="fx-desk__note">Chọn phong cách hình ảnh cho toàn bộ dự án.</p>
          <Radio.Group
            value={preset}
            onChange={(e) => setPreset(e.target.value)}
            style={{ marginBottom: 8, display: 'grid', gap: 8 }}
          >
            {VISUAL_MODES.map((m) => (
              <Radio key={m.id} value={m.id}>
                <b>{m.label}</b>
                <span className="fx-desk__note"> — {m.hint}</span>
              </Radio>
            ))}
          </Radio.Group>
          {chosen ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {chosen.description}
            </Typography.Paragraph>
          ) : (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {visualModeLabel(preset)} · inherit xuống Character / Calibration / Scene / Video.
            </Typography.Paragraph>
          )}
          <Button type="primary" loading={busy} disabled={!preset} onClick={setup}>
            Thiết lập phong cách
          </Button>
        </>
      )}

      <Modal
        title="Tạo Visual Style Revision"
        open={confirmOpen}
        okText="Xác nhận"
        cancelText="Hủy"
        okButtonProps={{ loading: busy }}
        onCancel={() => setConfirmOpen(false)}
        onOk={() => {
          setConfirmOpen(false);
          setBusy(true);
          void requestProjectVisualStyleRevision('FAMIXA', { confirm: true })
            .then(applyResult)
            .catch((e) => setError(apiErrorMessage(e, 'Không tạo được Visual Style Revision.')))
            .finally(() => setBusy(false));
        }}
      >
        <p>Bạn đang tạo PVS V2 candidate.</p>
        <p>Thay đổi Visual Mode sẽ tạo một visual universe revision mới và có thể yêu cầu xây dựng lại Character / Calibration.</p>
        <p>PVS V1 vẫn là authority cho đến khi Duyệt rồi Khóa.</p>
        <p>Không gọi Gemini. Không tạo nhân vật. Không duyệt và không khóa tự động.</p>
      </Modal>
    </Card>
  );
}
