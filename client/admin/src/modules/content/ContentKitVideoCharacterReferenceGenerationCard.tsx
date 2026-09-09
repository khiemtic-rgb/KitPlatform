import { useEffect, useState } from 'react';
import { Alert, Button, Collapse, Input, Modal, Radio, Space } from 'antd';
import {
  acceptCharacterReferenceGeneration,
  executeCharacterReferenceGeneration,
  fetchCharacterReferenceGeneration,
  fetchCharacterReferenceGenerationImageUrl,
  prepareCharacterReferenceGeneration,
  registerCharacterReferenceGeneration,
  rejectCharacterReferenceGeneration,
  validateCharacterReferenceGenerationPack,
  type CharacterReferenceGenerationRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  REFERENCE_GENERATION_PROVIDERS,
  STAFF_ACCEPT,
  STAFF_CANCEL,
  STAFF_CREATED,
  STAFF_DETAILS,
  STAFF_REJECT,
  STAFF_START,
  STAFF_WAITING,
  angleLabel,
  confirmCopy,
  generateCta,
  providerLabel,
  reviewCopy,
} from './kit-video-character-reference-generation';

type Props = {
  characterId: string;
  characterName: string;
  referenceType: string;
  packId?: string;
  replaceExisting?: boolean;
  open: boolean;
  onClose: () => void;
  onChanged: () => void;
};

export function ContentKitVideoCharacterReferenceGenerationCard({
  characterId,
  characterName,
  referenceType,
  packId,
  replaceExisting = false,
  open,
  onClose,
  onChanged,
}: Props) {
  const [row, setRow] = useState<CharacterReferenceGenerationRow>();
  const [provider, setProvider] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [rejectWhy, setRejectWhy] = useState('');
  const [preview, setPreview] = useState<string>();

  const load = (nextProvider = provider) => {
    if (!characterId || !open) return;
    setBusy(true);
    void fetchCharacterReferenceGeneration(characterId, { referenceType, provider: nextProvider || undefined })
      .then(setRow)
      .catch((err: unknown) => setError(apiErrorMessage(err)))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    if (!open) return;
    setError(undefined);
    setConfirmOpen(false);
    setRejectWhy('');
    void prepareCharacterReferenceGeneration(characterId, { referenceType })
      .then(setRow)
      .catch(() => load());
  }, [open, characterId, referenceType]);

  useEffect(() => {
    let dead = false;
    let url = '';
    if (!row?.executionId || (
      row.slotState !== 'READY_FOR_DIRECTOR'
      && row.reviewStatus !== 'PENDING'
      && row.candidateStatus !== 'READY_FOR_DIRECTOR'
      && row.candidateStatus !== 'ACCEPTED'
      && row.candidateStatus !== 'REGISTERED'
    )) {
      setPreview(undefined);
      return;
    }
    void fetchCharacterReferenceGenerationImageUrl(characterId, row.executionId)
      .then((next) => {
        if (dead) {
          URL.revokeObjectURL(next);
          return;
        }
        url = next;
        setPreview(next);
      })
      .catch(() => setPreview(undefined));
    return () => {
      dead = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [characterId, row?.executionId, row?.candidateStatus, row?.slotState, row?.reviewStatus]);

  const apply = (next: CharacterReferenceGenerationRow) => {
    setRow(next);
    onChanged();
  };

  const copy = confirmCopy({
    character: row?.characterName || characterName,
    angle: angleLabel(referenceType),
    provider: providerLabel(provider),
  });

  const waiting = row?.slotState === 'READY_FOR_DIRECTOR' || row?.candidateStatus === 'READY_FOR_DIRECTOR';
  const accepted = row?.candidateStatus === 'ACCEPTED';
  const registered = row?.slotState === 'REGISTERED' || row?.candidateStatus === 'REGISTERED';

  return (
    <Modal
      open={open}
      onCancel={onClose}
      footer={null}
      width={720}
      title="Tạo ảnh tham chiếu nhân vật"
      destroyOnClose
    >
      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 12 }} /> : null}
      <p>
        <strong>{row?.characterName || characterName}</strong> · {row?.eraId || 'ERA-01'}
      </p>

      <ol className="fx-crp__checks">
        <li>✓ Bước 1 — Nhân vật: {row?.characterName || characterName}</li>
        <li>✓ Bước 2 — Góc ảnh: {angleLabel(referenceType)}</li>
        <li>✓ Bước 3 — Nguồn nhân vật: Master · DNA · Production Reference</li>
        <li>
          Bước 4 — Nhà cung cấp:{' '}
          {waiting || accepted || registered ? providerLabel(row?.provider) : providerLabel(provider) || 'Chưa chọn'}
        </li>
        <li>
          Bước 5 — Kiểm tra:{' '}
          {row?.authorityValid ? '✓ Nhân vật hợp lệ · ✓ Canon hợp lệ' : row?.staffMessage || 'Đang kiểm tra'}
          {row?.gateStatus === 'READY' || row?.mayCallProvider || waiting ? ' · ✓ Có thể tạo ảnh' : ''}
        </li>
      </ol>

      {!waiting && !accepted && !registered ? (
        <section className="fx-crp__sec">
          <h3>Nhà cung cấp hình ảnh</h3>
          <Radio.Group
            value={provider}
            onChange={(e) => {
              const next = String(e.target.value || '');
              setProvider(next);
              load(next);
            }}
          >
            {REFERENCE_GENERATION_PROVIDERS.filter((p) => p.id).map((p) => (
              <Radio key={p.id} value={p.id}>
                {p.label}
              </Radio>
            ))}
          </Radio.Group>
          <p className="fx-desk__note">{row?.staffMessage}</p>
          <Space>
            <Button onClick={onClose}>{STAFF_CANCEL}</Button>
            <Button
              type="primary"
              loading={busy}
              disabled={!provider || !!row?.gateCode && row.gateCode !== 'CONFIRM_REQUIRED' && row.gateStatus !== 'READY' && !row.confirmRequired}
              onClick={() => setConfirmOpen(true)}
            >
              {generateCta(referenceType, replaceExisting)}
            </Button>
          </Space>
        </section>
      ) : null}

      {waiting ? (
        <section className="fx-crp__sec">
          <h3>Ứng viên bộ ảnh chuẩn</h3>
          <p>
            {row?.characterName || characterName} · {angleLabel(referenceType)}
          </p>
          {preview ? <img src={preview} alt={angleLabel(referenceType)} style={{ maxWidth: '100%' }} /> : <div className="fx-crp__ph" />}
          <p>
            Trạng thái: <strong>{reviewCopy(row?.reviewStatus)}</strong>
          </p>
          <Alert type="info" showIcon message={STAFF_CREATED} description={STAFF_WAITING} />
          <Space style={{ marginTop: 12 }}>
            <Button
              type="primary"
              loading={busy}
              onClick={() => {
                if (!row?.executionId) return;
                setBusy(true);
                void acceptCharacterReferenceGeneration(characterId, row.executionId)
                  .then(apply)
                  .catch((err: unknown) => setError(apiErrorMessage(err)))
                  .finally(() => setBusy(false));
              }}
            >
              {STAFF_ACCEPT}
            </Button>
            <Button
              onClick={() => {
                if (row?.provider) setProvider(row.provider);
                setConfirmOpen(true);
              }}
            >
              {generateCta(referenceType, true)}
            </Button>
            <Button
              danger
              onClick={() => {
                if (!row?.executionId) return;
                if (rejectWhy.trim().length < 3) {
                  setError('Cần ghi lý do khi từ chối.');
                  return;
                }
                setBusy(true);
                void rejectCharacterReferenceGeneration(characterId, row.executionId, rejectWhy.trim())
                  .then(apply)
                  .catch((err: unknown) => setError(apiErrorMessage(err)))
                  .finally(() => setBusy(false));
              }}
            >
              {STAFF_REJECT}
            </Button>
          </Space>
          <Input.TextArea
            style={{ marginTop: 8 }}
            placeholder="Lý do từ chối"
            value={rejectWhy}
            onChange={(e) => setRejectWhy(e.target.value)}
          />
        </section>
      ) : null}

      {accepted && !registered ? (
        <section className="fx-crp__sec">
          {preview ? <img src={preview} alt={angleLabel(referenceType)} style={{ maxWidth: '100%' }} /> : null}
          <Button
            type="primary"
            loading={busy}
            onClick={() => {
              if (!row?.executionId) return;
              setBusy(true);
              void registerCharacterReferenceGeneration(characterId, row.executionId)
                .then(apply)
                .catch((err: unknown) => setError(apiErrorMessage(err)))
                .finally(() => setBusy(false));
            }}
          >
            Đưa vào bộ ảnh chuẩn
          </Button>
        </section>
      ) : null}

      {registered ? (
        <section className="fx-crp__sec">
          <Alert type="success" showIcon message="Đã đưa vào bộ ảnh chuẩn." description="Bộ ảnh vẫn chờ Director kiểm tra và duyệt." />
          {preview ? <img src={preview} alt={angleLabel(referenceType)} style={{ maxWidth: '100%', marginTop: 12 }} /> : null}
          <Space wrap style={{ marginTop: 12 }}>
            {row?.executionId ? (
              <Button
                loading={busy}
                onClick={() => {
                  setBusy(true);
                  setError(undefined);
                  void registerCharacterReferenceGeneration(characterId, row.executionId)
                    .then(apply)
                    .catch((err: unknown) => setError(apiErrorMessage(err)))
                    .finally(() => setBusy(false));
                }}
              >
                Đưa ảnh này vào khung
              </Button>
            ) : null}
            {packId ? (
              <Button
                type="primary"
                loading={busy}
                onClick={() => {
                  setBusy(true);
                  setError(undefined);
                  void validateCharacterReferenceGenerationPack(characterId, packId, row?.executionId)
                    .then((next) => {
                      apply(next);
                      onClose();
                    })
                    .catch((err: unknown) => {
                      setError(apiErrorMessage(err));
                      onChanged();
                    })
                    .finally(() => setBusy(false));
                }}
              >
                Kiểm tra bộ ảnh
              </Button>
            ) : (
              <Button onClick={onClose}>Đóng và xem 4 góc</Button>
            )}
          </Space>
        </section>
      ) : null}

      <Collapse
        className="fx-crp__tech"
        items={[
          {
            key: 'tech',
            label: STAFF_DETAILS,
            children: (
              <pre>
                {JSON.stringify(row?.technical ?? {
                  characterId: row?.characterId,
                  eraId: row?.eraId,
                  referenceType: row?.referenceType,
                  intentSha256: row?.intentSha256,
                  masterSha256: row?.masterSha256,
                  dnaSha256: row?.dnaSha256,
                  prpSha256: row?.prpSha256,
                  provider: row?.provider,
                  providerRequestId: row?.providerRequestId,
                  executionId: row?.executionId,
                  artifactId: row?.artifactId,
                  fingerprint: row?.fingerprint,
                }, null, 2)}
              </pre>
            ),
          },
        ]}
      />

      <Modal
        open={confirmOpen}
        title={copy.title}
        onCancel={() => setConfirmOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setConfirmOpen(false)}>{copy.cancel}</Button>
            <Button
              type="primary"
              loading={busy}
              onClick={() => {
                setBusy(true);
                setError(undefined);
                void executeCharacterReferenceGeneration(characterId, {
                  confirm: true,
                  provider: provider || row?.provider || undefined,
                  referenceType,
                  regenerate: replaceExisting || waiting,
                })
                  .then((next) => {
                    setConfirmOpen(false);
                    apply(next);
                  })
                  .catch((err: unknown) => setError(apiErrorMessage(err)))
                  .finally(() => setBusy(false));
              }}
            >
              {STAFF_START}
            </Button>
          </Space>
        }
      >
        <p>Nhân vật: {copy.character}</p>
        <p>Góc: {copy.angle}</p>
        <p>Nguồn nhân vật: {copy.source} — {copy.character} / {row?.eraId || 'ERA-01'}</p>
        <p>Master: Đã khóa</p>
        <p>DNA: Đã khóa</p>
        <p>Provider: {copy.provider}</p>
        <p>Ảnh này sẽ trở thành: {copy.willBecome}</p>
      </Modal>
    </Modal>
  );
}
