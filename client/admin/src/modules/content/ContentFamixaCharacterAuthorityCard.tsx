import { useEffect, useState } from 'react';
import { Alert, Button, Collapse, Modal, Radio, Space, Typography } from 'antd';
import {
  approveCharacterAuthorityMaster,
  executeCharacterAuthorityMaster,
  fetchCharacterAuthorityInitialization,
  fetchCharacterAuthorityMasterObjectUrl,
  fetchCharacterAuthorityPipeline,
  lockCharacterAuthorityMaster,
  prepareCharacterAuthorityMaster,
  rejectCharacterAuthorityMaster,
  type CharacterAuthorityInitializationRow,
  type CharacterAuthorityPipelineRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  AUTHORITY_PROVIDERS,
  STAFF_CREATE_MASTER,
  STAFF_DETAILS,
  STAFF_MISSING,
  STAFF_NEED_DNA,
  STAFF_NEED_MASTER,
  STAFF_START,
  STAFF_WAIT_AUTHORITY,
  STAFF_WAIT_DNA,
  STAFF_WAIT_MASTER,
  canOfferMaster,
  masterConfirmCopy,
  providerLabel,
  stageDot,
} from './kit-video-character-authority-initialization';
import {
  STAFF_APPROVE_MASTER,
  STAFF_BUILDING,
  STAFF_CANCEL,
  STAFF_CREATE_AUTHORITY,
  STAFF_CRP_CREATE,
  STAFF_DNA_AUTO,
  STAFF_LOCK_MASTER,
  STAFF_PRP_AUTO,
  STAFF_REJECT,
  STAFF_SYSTEM,
  authorityConfirmCopy,
  hideDnaPrpCtas,
  mayShowMasterGenerate,
  pipelineStaff,
} from './kit-video-character-authority-pipeline';

export function ContentFamixaCharacterAuthorityCard({
  characterId,
  characterName,
  eraId = 'ERA-01',
  onChanged,
}: {
  characterId: string;
  characterName?: string;
  eraId?: string;
  onChanged?: () => void;
}) {
  const [row, setRow] = useState<CharacterAuthorityInitializationRow>();
  const [pipeline, setPipeline] = useState<CharacterAuthorityPipelineRow>();
  const [provider, setProvider] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirm, setConfirm] = useState(false);
  const [imageUrl, setImageUrl] = useState<string>();

  const load = (picked?: string) => {
    setBusy(true);
    const opts = { eraId, provider: picked || provider || undefined };
    void Promise.all([
      fetchCharacterAuthorityInitialization(characterId, opts),
      fetchCharacterAuthorityPipeline(characterId, opts),
    ])
      .then(([init, next]) => {
        setRow(init);
        setPipeline(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được bộ nhận diện chuẩn.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, [characterId]);

  useEffect(() => {
    if (!row?.master.artifactId && row?.master.status === 'MISSING') {
      setImageUrl(undefined);
      return;
    }
    if (row?.master.status === 'MISSING' || row?.authorityLocked) {
      setImageUrl(undefined);
      return;
    }
    if (row?.master.status === 'MISSING') return;
    let dead = false;
    let url: string | undefined;
    void fetchCharacterAuthorityMasterObjectUrl(characterId, eraId)
      .then((next) => {
        if (dead) {
          URL.revokeObjectURL(next);
          return;
        }
        url = next;
        setImageUrl(next);
      })
      .catch(() => undefined);
    return () => {
      dead = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [characterId, row?.master.status, row?.master.artifactId]);

  if (row?.authorityLocked) return null;

  const name = pipeline?.characterName || row?.characterName || characterName || 'Nhân vật';
  const masterOffer = mayShowMasterGenerate({
    authorityLocked: !!row?.authorityLocked,
    masterStatus: row?.master.status || 'MISSING',
    provider,
  }) && canOfferMaster({
    authorityLocked: !!row?.authorityLocked,
    masterStatus: row?.master.status || 'MISSING',
    provider,
  });
  const confirmCopy = authorityConfirmCopy({ name, eraId: row?.eraId || eraId, provider: providerLabel(provider) });
  const legacyCopy = masterConfirmCopy({ name, eraId: row?.eraId || eraId, provider: providerLabel(provider) });

  const run = (task: () => Promise<CharacterAuthorityInitializationRow>) => {
    setBusy(true);
    void task()
      .then((data) => {
        setRow(data);
        setError(undefined);
        onChanged?.();
        load();
      })
      .catch((e) => setError(apiErrorMessage(e, pipeline?.staffMessage || row?.staffMessage || 'Không thực hiện được.')))
      .finally(() => setBusy(false));
  };

  const startMaster = () => {
    setConfirm(false);
    run(() =>
      prepareCharacterAuthorityMaster(characterId, { eraId, provider }).then(() =>
        executeCharacterAuthorityMaster(characterId, { confirm: true, provider, eraId }),
      ),
    );
  };

  const masterStatus = row?.master.status || 'MISSING';
  const dnaStatus = row?.dna.status || pipeline?.dna.status || 'MISSING';
  const prpStatus = row?.prp.status || pipeline?.prp.status || 'MISSING';
  const banner = pipeline?.staffMessage || pipelineStaff(pipeline?.pipelineState) || STAFF_BUILDING;
  const hideDerivedCtas = hideDnaPrpCtas();

  return (
    <section className="fx-crp__sec">
      <h3>Character Authority</h3>
      <p className="fx-desk__lead">NHÂN VẬT: {name.toUpperCase()}</p>
      <Alert type="info" showIcon message={banner} style={{ marginBottom: 8 }} />
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}

      <article className="fx-crp__tile" style={{ marginBottom: 12 }}>
        <strong>① Master Reference</strong>
        <p>
          {stageDot(masterStatus)} {row?.master.staffLabel || STAFF_NEED_MASTER || STAFF_MISSING}
        </p>
        {imageUrl ? <img src={imageUrl} alt={name} /> : null}
        {masterStatus === 'MISSING' || masterStatus === 'REJECTED' || masterStatus === 'FAILED' ? (
          <>
            <div style={{ marginBottom: 8 }}>Nhà cung cấp</div>
            <Radio.Group
              value={provider}
              onChange={(e) => {
                const next = e.target.value as string;
                setProvider(next);
                load(next);
              }}
            >
              {AUTHORITY_PROVIDERS.filter((x) => x.id).map((x) => (
                <Radio key={x.id} value={x.id} disabled={!x.available}>
                  {x.label} {x.available ? '' : '— chưa hỗ trợ'}
                </Radio>
              ))}
            </Radio.Group>
            <div style={{ marginTop: 12 }}>
              <Button type="primary" disabled={!masterOffer} loading={busy} onClick={() => setConfirm(true)}>
                {STAFF_CREATE_AUTHORITY}
              </Button>
            </div>
          </>
        ) : null}
        {masterStatus === 'READY_FOR_DIRECTOR' ? (
          <Space>
            <Button type="primary" loading={busy} onClick={() => run(() => approveCharacterAuthorityMaster(characterId, eraId))}>
              {STAFF_APPROVE_MASTER}
            </Button>
            <Button danger loading={busy} onClick={() => run(() => rejectCharacterAuthorityMaster(characterId, eraId))}>
              {STAFF_REJECT}
            </Button>
          </Space>
        ) : null}
        {masterStatus === 'APPROVED' ? (
          <Button type="primary" loading={busy} onClick={() => run(() => lockCharacterAuthorityMaster(characterId, eraId))}>
            {STAFF_LOCK_MASTER}
          </Button>
        ) : null}
      </article>

      <article className="fx-crp__tile" style={{ marginBottom: 12 }}>
        <strong>② Character DNA</strong>
        <p>
          {stageDot(dnaStatus)}{' '}
          {masterStatus === 'LOCKED' ? (dnaStatus === 'LOCKED' || dnaStatus === 'VALID' || dnaStatus === 'READY' ? STAFF_DNA_AUTO : STAFF_NEED_DNA) : STAFF_WAIT_MASTER}
        </p>
        {hideDerivedCtas ? <p className="fx-desk__note">{STAFF_DNA_AUTO}</p> : null}
      </article>

      <article className="fx-crp__tile" style={{ marginBottom: 12 }}>
        <strong>③ Production Reference Profile</strong>
        <p>
          {stageDot(prpStatus)} {dnaStatus === 'LOCKED' || dnaStatus === 'VALID' || dnaStatus === 'READY' ? STAFF_PRP_AUTO : STAFF_WAIT_DNA}
        </p>
        {hideDerivedCtas ? <p className="fx-desk__note">{STAFF_PRP_AUTO}</p> : null}
      </article>

      <article className="fx-crp__tile">
        <strong>④ Bộ ảnh tham chiếu</strong>
        <p>{row?.authorityReady || pipeline?.authorityReady ? STAFF_CRP_CREATE : STAFF_WAIT_AUTHORITY}</p>
      </article>

      <Collapse
        ghost
        style={{ marginTop: 8 }}
        items={[
          {
            key: 'tech',
            label: STAFF_SYSTEM,
            children: (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {pipeline?.pipelineState || row?.gateCode || row?.gateStatus || STAFF_DETAILS}
              </Typography.Paragraph>
            ),
          },
        ]}
      />

      <Modal
        title={confirmCopy.title || STAFF_CREATE_MASTER}
        open={confirm}
        onCancel={() => setConfirm(false)}
        footer={
          <Space>
            <Button onClick={() => setConfirm(false)}>{STAFF_CANCEL}</Button>
            <Button type="primary" loading={busy} onClick={startMaster}>
              {STAFF_START}
            </Button>
          </Space>
        }
      >
        <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>
          {confirmCopy.body || legacyCopy.body}
        </pre>
      </Modal>
    </section>
  );
}
