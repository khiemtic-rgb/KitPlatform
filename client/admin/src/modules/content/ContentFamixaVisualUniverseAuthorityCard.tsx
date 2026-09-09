import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Modal, Space, Tag } from 'antd';
import {
  approveVisualUniverse,
  fetchVisualUniverseAuthority,
  fetchVisualUniverseCalibration,
  lockVisualUniverse,
  rejectVisualUniverse,
  requestVisualUniverseCalibration,
  requestVisualUniverseRevision,
  type VisualUniverseAuthority,
  type VisualUniverseCalibration,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { directorPvsStatusLabel } from './kit-video-character-studio';

export function ContentFamixaVisualUniverseAuthorityCard() {
  const [row, setRow] = useState<VisualUniverseAuthority>();
  const [cal, setCal] = useState<VisualUniverseCalibration>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<'revision' | 'calibrate' | 'approve' | 'reject' | 'lock' | null>(null);

  const load = () => {
    setBusy(true);
    void Promise.all([
      fetchVisualUniverseAuthority('FAMIXA'),
      fetchVisualUniverseCalibration('FAMIXA').catch(() => undefined),
    ])
      .then(([next, nextCal]) => {
        setRow(next);
        setCal(nextCal);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Visual Universe Authority.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const run = (work: () => Promise<unknown>) => {
    setBusy(true);
    void work()
      .then(() => {
        setConfirm(null);
        load();
      })
      .catch((e) => {
        setError(apiErrorMessage(e, 'Không thực hiện được.'));
        setBusy(false);
      });
  };

  const slots = cal?.slots?.length ? cal.slots : row?.calibration ?? [];
  const status = row?.status || 'DRAFT';

  return (
    <Card className="fx-look__card fx-pvs fx-pvs--director" size="small" title="FAMIXA Visual Universe">
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <div className="fx-pvs__summary">
        <div>
          <p className="fx-pvs__kicker">Current Authority</p>
          <p>
            <b>V1</b>
          </p>
          <Tag color={row?.currentAuthority ? 'green' : 'default'}>
            {row?.currentAuthority ? 'LOCKED' : 'PVS V1 ACTIVE'}
          </Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Candidate</p>
          <p>
            <b>V1</b>
          </p>
          <Tag color={status === 'LOCKED' ? 'green' : status === 'PENDING_REVIEW' ? 'gold' : 'default'}>
            {directorPvsStatusLabel(status)}
          </Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Stylization</p>
          <p>
            <b>{row?.stylizationLevel || 'STRONG'}</b>
          </p>
          <p className="fx-desk__note">Photorealism {row?.photorealismCeiling || 'LOW'}</p>
        </div>
      </div>
      <p className="fx-desk__note">
        Visual Universe is project-level. Characters inherit it. PVS V1 stays production authority until Director locks.
      </p>
      <h4>STYLE CALIBRATION</h4>
      <div className="fx-cstudio__grid">
        {['Adult Male', 'Adult Female', 'Child Boy', 'Child Girl'].map((label) => {
          const slot = slots.find((s) => s.label === label);
          return (
            <div key={label} className="fx-cstudio__card">
              {slot?.path ? <img src={slot.path} alt="" /> : <div className="fx-clib__ph" />}
              <strong>{label}</strong>
            </div>
          );
        })}
      </div>
      <Space wrap style={{ marginTop: 8 }}>
        {row?.mayRequest ? (
          <Button loading={busy} onClick={() => setConfirm('revision')}>
            Tạo Style Revision
          </Button>
        ) : null}
        {row?.mayCalibrate ? (
          <Button loading={busy} onClick={() => setConfirm('calibrate')}>
            Xem Calibration
          </Button>
        ) : null}
        {row?.mayApprove ? (
          <Button loading={busy} onClick={() => setConfirm('approve')}>
            Duyệt
          </Button>
        ) : null}
        {row?.mayReject ? (
          <Button loading={busy} onClick={() => setConfirm('reject')}>
            Không đạt
          </Button>
        ) : null}
        {row?.mayLock ? (
          <Button type="primary" loading={busy} onClick={() => setConfirm('lock')}>
            Lock Authority
          </Button>
        ) : null}
      </Space>
      <Collapse
        ghost
        className="fx-director-tech"
        items={[
          {
            key: 'vua-tech',
            label: 'Chi tiết kỹ thuật',
            children: (
              <div>
                <p>SHA {row?.sha256 || '—'}</p>
                <p>Design Language SHA {row?.characterDesignLanguageSha || '—'}</p>
                <p>Style Reference SHA {row?.styleReferencePackSha || '—'}</p>
                <p>Calibration SHA {row?.styleCalibrationPackSha || '—'}</p>
                <p>Wrapped PVS V1 {row?.projectVisualStyleSha || '—'}</p>
                <p>Realism {row?.realismCeiling}</p>
                <p>Face</p>
                <p>{row?.faceLanguage}</p>
                <p>Eyes</p>
                <p>{row?.eyeLanguage}</p>
              </div>
            ),
          },
        ]}
      />
      <Modal
        title="Xác nhận"
        open={!!confirm}
        confirmLoading={busy}
        onCancel={() => setConfirm(null)}
        onOk={() => {
          if (confirm === 'revision')
            run(() => requestVisualUniverseRevision({ projectId: 'FAMIXA', confirm: true }));
          else if (confirm === 'calibrate')
            run(() => requestVisualUniverseCalibration({ projectId: 'FAMIXA', confirm: true, generate: false }));
          else if (confirm === 'approve') run(() => approveVisualUniverse({ projectId: 'FAMIXA' }));
          else if (confirm === 'reject') run(() => rejectVisualUniverse({ projectId: 'FAMIXA' }));
          else if (confirm === 'lock') run(() => lockVisualUniverse({ projectId: 'FAMIXA' }));
        }}
      >
        {confirm === 'calibrate'
          ? 'Compile Calibration Pack. Không gọi Gemini trừ khi Director bật generate.'
          : confirm === 'lock'
            ? 'Khóa Visual Universe. PVS V1 không bị xóa. Character LOCKED không bị mutate.'
            : 'Thao tác này không tự duyệt, không tự khóa, không generate character.'}
      </Modal>
    </Card>
  );
}
