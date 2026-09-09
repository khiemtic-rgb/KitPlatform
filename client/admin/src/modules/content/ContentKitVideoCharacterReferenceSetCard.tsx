import { useEffect, useState } from 'react';
import { Alert, Button, Collapse, Input, Modal, Radio, Space, Typography } from 'antd';
import {
  executeCharacterReferenceRegeneration,
  executeCharacterReferenceSet,
  fetchCharacterReferenceRegeneration,
  fetchCharacterReferenceSet,
  prepareCharacterReferenceRegeneration,
  prepareCharacterReferenceSet,
  rejectCharacterReferenceSetReview,
  type CharacterReferenceRegenerationRow,
  type CharacterReferenceSetRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  SET_PROVIDERS,
  STAFF_APPROVE,
  STAFF_BLOCKED,
  STAFF_CANCEL,
  STAFF_CREATE_SET,
  STAFF_DETAILS,
  STAFF_PENDING,
  STAFF_START,
  canOfferSet,
  pendingSetLabel,
  providerLabel,
  setConfirmCopy,
} from './kit-video-character-reference-generation-v2';
import {
  REGEN_PROVIDERS,
  REJECT_REASON_CODES,
  STAFF_HISTORY,
  STAFF_REGENERATE,
  STAFF_REJECT,
  STAFF_REJECT_CONFIRM,
  STAFF_REJECT_REASON,
  STAFF_REJECT_TITLE,
  STAFF_REJECTED,
  STAFF_START as STAFF_REGEN_START,
  isPendingReviewStatus,
  isRejectedStatus,
  regenProviderLabel,
  regenerateConfirmCopy,
  rejectReasonValid,
  rejectedSetLabel,
} from './kit-video-character-reference-regeneration';

export function ContentKitVideoCharacterReferenceSetCard({
  characterId,
  characterName,
  eraId = 'ERA-01',
  locked,
  coverageReady: _coverageReady,
  onGenerated,
  onApprove,
}: {
  characterId: string;
  characterName?: string;
  eraId?: string;
  locked?: boolean;
  coverageReady?: boolean;
  onGenerated?: () => void;
  onApprove?: () => void;
  onReject?: () => void;
}) {
  const [row, setRow] = useState<CharacterReferenceSetRow>();
  const [regen, setRegen] = useState<CharacterReferenceRegenerationRow>();
  const [provider, setProvider] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [regenOpen, setRegenOpen] = useState(false);
  const [reasonCode, setReasonCode] = useState('INCONSISTENT');
  const [reasonText, setReasonText] = useState('');

  const load = (picked?: string) => {
    setBusy(true);
    const opts = { eraId, provider: picked || provider || undefined };
    void Promise.all([
      fetchCharacterReferenceSet(characterId, opts),
      fetchCharacterReferenceRegeneration(characterId, opts),
    ])
      .then(([set, next]) => {
        setRow(set);
        setRegen(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được bộ ảnh tham chiếu.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, [characterId]);

  if (locked || regen?.locked) return null;

  const authorityReady = row?.authorityValid === true || regen?.mayReject || regen?.mayRegenerate;
  const rejected = isRejectedStatus(regen?.status);
  const pending = !rejected && isPendingReviewStatus(regen?.status);
  const offer = canOfferSet({
    authorityReady: !!authorityReady,
    provider,
    locked: !!locked || !!row?.locked || !!regen?.locked,
    alreadyComplete: (!!row?.locked && !!row?.canUse) || !!regen?.locked,
  });
  const name = regen?.characterName || row?.characterName || characterName || 'Nhân vật';
  const copy = setConfirmCopy({
    name,
    characterId,
    eraId: row?.eraId || eraId,
    provider: providerLabel(provider),
  });
  const regenCopy = regenerateConfirmCopy(regenProviderLabel(provider));

  const startCreate = () => {
    if (!offer || rejected || pending) return;
    setConfirmOpen(false);
    setBusy(true);
    void prepareCharacterReferenceSet(characterId, { eraId, provider })
      .then(() => executeCharacterReferenceSet(characterId, { confirm: true, provider, eraId }))
      .then((data) => {
        setRow(data);
        setError(undefined);
        onGenerated?.();
        load();
      })
      .catch((e) => setError(apiErrorMessage(e, row?.staffMessage || STAFF_BLOCKED)))
      .finally(() => setBusy(false));
  };

  const confirmReject = () => {
    if (!rejectReasonValid(reasonText)) {
      setError('Cần nhập lý do không đạt.');
      return;
    }
    setRejectOpen(false);
    setBusy(true);
    void rejectCharacterReferenceSetReview(characterId, {
      rejectReasonCode: reasonCode,
      rejectReasonText: reasonText.trim(),
      eraId,
    })
      .then((data) => {
        setRegen(data);
        setError(undefined);
        onGenerated?.();
        load();
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đánh giá được bộ ảnh.')))
      .finally(() => setBusy(false));
  };

  const startRegen = () => {
    if (provider !== 'GEMINI') return;
    setRegenOpen(false);
    setBusy(true);
    void prepareCharacterReferenceRegeneration(characterId, { eraId, provider })
      .then(() => executeCharacterReferenceRegeneration(characterId, { confirm: true, provider, eraId }))
      .then((data) => {
        setRegen(data);
        setError(undefined);
        onGenerated?.();
        load();
      })
      .catch((e) => setError(apiErrorMessage(e, regen?.staffMessage || STAFF_BLOCKED)))
      .finally(() => setBusy(false));
  };

  return (
    <section className="fx-crp__sec">
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {!authorityReady && !rejected && !pending ? (
        <Alert type="warning" showIcon message="Chưa thể tạo bộ ảnh chuẩn" description={STAFF_BLOCKED} />
      ) : rejected ? (
        <>
          <Alert
            type="warning"
            showIcon
            message={`${name} · ${rejectedSetLabel(regen?.coverage ?? row?.coverage ?? 4)}`}
            description={STAFF_REJECT_REASON}
            style={{ marginBottom: 8 }}
          />
          {regen?.rejectReasonText ? <p className="fx-desk__note">{regen.rejectReasonText}</p> : null}
          <div style={{ marginBottom: 8 }}>Nhà cung cấp</div>
          <Radio.Group
            value={provider}
            onChange={(e) => {
              const next = e.target.value as string;
              setProvider(next);
              load(next);
            }}
          >
            {REGEN_PROVIDERS.filter((x) => x.id).map((x) => (
              <Radio key={x.id} value={x.id} disabled={!x.available}>
                {x.label} {x.available ? '' : '— chưa hỗ trợ'}
              </Radio>
            ))}
          </Radio.Group>
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={provider !== 'GEMINI'} loading={busy} onClick={() => setRegenOpen(true)}>
              {STAFF_REGENERATE}
            </Button>
          </div>
        </>
      ) : pending ? (
        <>
          <Alert
            type="info"
            showIcon
            message={`${name} · ${pendingSetLabel(regen?.coverage ?? row?.coverage ?? 4)}`}
            description={STAFF_PENDING}
            style={{ marginBottom: 8 }}
          />
          <Space>
            <Button type="primary" onClick={onApprove}>
              {STAFF_APPROVE}
            </Button>
            <Button danger onClick={() => setRejectOpen(true)}>
              {STAFF_REJECT}
            </Button>
          </Space>
        </>
      ) : (
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
            {SET_PROVIDERS.filter((x) => x.id).map((x) => (
              <Radio key={x.id} value={x.id} disabled={!x.available}>
                {x.label} {x.available ? '' : '— chưa hỗ trợ'}
              </Radio>
            ))}
          </Radio.Group>
          <div style={{ marginTop: 12 }}>
            <Button type="primary" disabled={!offer} loading={busy} onClick={() => setConfirmOpen(true)}>
              {STAFF_CREATE_SET}
            </Button>
          </div>
        </>
      )}
      {(regen?.history?.length ?? 0) > 0 ? (
        <Collapse
          ghost
          style={{ marginTop: 8 }}
          items={[
            {
              key: 'history',
              label: STAFF_HISTORY,
              children: (
                <ul className="fx-crp__checks">
                  {regen?.history.map((item) => (
                    <li key={`${item.version}-${item.setId || item.sha256}`}>
                      {item.version} · {item.statusLabel}
                      {item.rejectReasonText ? ` — ${item.rejectReasonText}` : ''}
                    </li>
                  ))}
                </ul>
              ),
            },
          ]}
        />
      ) : null}
      <Collapse
        ghost
        style={{ marginTop: 8 }}
        items={[
          {
            key: 'tech',
            label: STAFF_DETAILS,
            children: (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {regen?.gateCode || row?.gateCode || regen?.status || row?.gateStatus || 'Chưa có'}
              </Typography.Paragraph>
            ),
          },
        ]}
      />
      <Modal
        title={STAFF_REJECT_TITLE}
        open={rejectOpen}
        onCancel={() => setRejectOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setRejectOpen(false)}>{STAFF_CANCEL}</Button>
            <Button type="primary" danger disabled={!rejectReasonValid(reasonText)} loading={busy} onClick={confirmReject}>
              {STAFF_REJECT_CONFIRM}
            </Button>
          </Space>
        }
      >
        <p>{STAFF_REJECT_REASON}</p>
        <Radio.Group
          value={reasonCode}
          onChange={(e) => setReasonCode(e.target.value as string)}
          style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 12 }}
        >
          {REJECT_REASON_CODES.map((item) => (
            <Radio key={item.id} value={item.id}>
              {item.label}
            </Radio>
          ))}
        </Radio.Group>
        <Input.TextArea
          rows={3}
          value={reasonText}
          onChange={(e) => setReasonText(e.target.value)}
          placeholder="Toàn thân không đồng nhất với góc trước mặt."
        />
      </Modal>
      <Modal
        title={regenCopy.title}
        open={regenOpen}
        onCancel={() => setRegenOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setRegenOpen(false)}>{STAFF_CANCEL}</Button>
            <Button type="primary" disabled={provider !== 'GEMINI'} loading={busy} onClick={startRegen}>
              {STAFF_REGEN_START}
            </Button>
          </Space>
        }
      >
        <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>{regenCopy.body}</pre>
      </Modal>
      <Modal
        title={copy.title}
        open={confirmOpen}
        onCancel={() => setConfirmOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setConfirmOpen(false)}>{STAFF_CANCEL}</Button>
            <Button type="primary" loading={busy} onClick={startCreate}>
              {STAFF_START}
            </Button>
          </Space>
        }
      >
        <p>{copy.character}</p>
        <p>{copy.id}</p>
        <p>{copy.era}</p>
        <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>{copy.body}</pre>
      </Modal>
    </section>
  );
}

export { coverageLine } from './kit-video-character-reference-generation-v2';
