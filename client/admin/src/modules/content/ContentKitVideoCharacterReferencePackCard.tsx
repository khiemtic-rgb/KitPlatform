import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Collapse, Modal, Space, Table, Tabs, Typography, Upload } from 'antd';
import {
  approveCharacterReferencePack,
  attachCharacterReferenceItem,
  createCharacterReferencePack,
  fetchCharacterReferenceItemObjectUrl,
  fetchCharacterReferencePack,
  fetchFamixaCharacters,
  fetchKitVideoMasterCandidateObjectUrl,
  lockCharacterReferencePack,
  rejectCharacterReferencePack,
  validateCharacterReferencePack,
  type CharacterReferencePackGetRow,
  type CharacterReferencePackRow,
  type FamixaCharacterRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  IDENTITY_LABELS,
  OPTIONAL_REFERENCE_TYPES,
  REQUIRED_REFERENCE_TYPES,
  identityLine,
  nextWork,
  referenceStatusLabel,
  viewLabel,
} from './kit-video-character-reference-pack';
import { ContentFamixaCharacterAuthorityCard } from './ContentFamixaCharacterAuthorityCard';
import { ContentKitVideoCharacterReferenceGenerationCard } from './ContentKitVideoCharacterReferenceGenerationCard';
import { ContentKitVideoCharacterReferenceSetCard } from './ContentKitVideoCharacterReferenceSetCard';
import { STAFF_UPLOAD_EXISTING, generateCta } from './kit-video-character-reference-generation';
import {
  STAFF_APPROVE,
  STAFF_AUTHORITY,
  STAFF_COMPLETE,
  STAFF_LOCK,
  STAFF_LOCKED,
  STAFF_NOT_READY,
  STAFF_PRODUCTION_READY,
  approveConfirm,
  lockConfirm,
  mayApprove,
  mayLock,
  notReadyReasons,
} from './kit-video-character-reference-approval-lock';

function useThumbs(characterId: string, pack?: CharacterReferencePackRow | null) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    if (!characterId || !pack) {
      setUrls({});
      return;
    }
    void Promise.all(
      pack.items
        .filter((item) => item.artifactPath)
        .map((item) =>
          fetchCharacterReferenceItemObjectUrl(characterId, pack.id, item.id, item.artifactSha256)
            .then((url) => {
              made.push(url);
              return [item.type, url] as const;
            })
            .catch(() => undefined),
        ),
    ).then((rows) => {
      if (dead) {
        made.forEach((url) => URL.revokeObjectURL(url));
        return;
      }
      const next: Record<string, string> = {};
      for (const row of rows) {
        if (row) next[row[0]] = row[1];
      }
      setUrls(next);
    });
    return () => {
      dead = true;
      made.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [characterId, pack?.id, pack?.items.map((x) => `${x.id}:${x.artifactSha256}`).join(',')]);
  return urls;
}

export function ContentKitVideoCharacterReferenceList({ appearances }: { appearances?: number }) {
  const [rows, setRows] = useState<FamixaCharacterRow[]>([]);
  const [packs, setPacks] = useState<Record<string, CharacterReferencePackGetRow>>({});
  const [openId, setOpenId] = useState<string>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const load = () => {
    setBusy(true);
    void fetchFamixaCharacters()
      .then(async (list) => {
        setRows(list);
        const next: Record<string, CharacterReferencePackGetRow> = {};
        for (const row of list) {
          try {
            next[row.characterCode] = await fetchCharacterReferencePack(row.characterCode);
          } catch {
            next[row.characterCode] = {
              canCreate: false,
              masterLocked: false,
              dnaLocked: false,
              characterName: row.name,
              authority: [],
            };
          }
        }
        setPacks(next);
        setError(undefined);
        if (!openId && list[0]) setOpenId(list[0].characterCode);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa tải được danh sách nhân vật.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  return (
    <section className="fx-crp">
      <header className="fx-crp__head">
        <h2>Nhân vật</h2>
        <p className="fx-desk__lead">Chọn nhân vật để xem bộ ảnh chuẩn. Không cần đọc mã kỹ thuật.</p>
        {typeof appearances === 'number' ? <p className="fx-desk__note">Xuất hiện trong bản dựng: {appearances} cảnh</p> : null}
      </header>
      {error ? <Alert type="error" showIcon message={error} /> : null}
      <div className="fx-crp__cast">
        {rows.map((row) => {
          const pack = packs[row.characterCode]?.pack;
          const status =
            pack?.status === 'LOCKED'
              ? '✓ Đã khóa'
              : pack?.coverageReady
                ? '4/4 · Chờ duyệt'
                : pack
                  ? '⚠ Thiếu ảnh chuẩn'
                  : '○ Chưa có ảnh chuẩn';
          return (
            <button
              key={row.characterCode}
              type="button"
              className={`fx-crp__cast-card${row.characterCode === openId ? ' is-on' : ''}`}
              onClick={() => setOpenId(row.characterCode)}
            >
              <strong>{row.name}</strong>
              <span>{status}</span>
            </button>
          );
        })}
      </div>
      {rows.length > 20 ? (
        <div className="fx-crp__table">
          <Table
            rowKey="characterCode"
            size="small"
            loading={busy}
            pagination={{ pageSize: 20 }}
            dataSource={rows}
            onRow={(row) => ({ onClick: () => setOpenId(row.characterCode) })}
            rowClassName={(row) => (row.characterCode === openId ? 'fx-crp__row--on' : 'fx-crp__row')}
            columns={[
              { title: 'Nhân vật', dataIndex: 'name' },
              { title: 'Trạng thái', render: (_, row) => referenceStatusLabel(packs[row.characterCode]) },
            ]}
          />
        </div>
      ) : null}
      {openId ? (
        <ContentKitVideoCharacterReferencePackCard
          characterId={openId}
          characterName={rows.find((r) => r.characterCode === openId)?.name}
          onChanged={(row) => setPacks((cur) => ({ ...cur, [openId]: { ...cur[openId], pack: row, characterName: row.characterName } }))}
        />
      ) : null}
    </section>
  );
}

export function ContentKitVideoCharacterReferencePackCard({
  characterId,
  characterName,
  onChanged,
}: {
  characterId: string;
  characterName?: string;
  onChanged?: (row: CharacterReferencePackRow) => void;
}) {
  const [bundle, setBundle] = useState<CharacterReferencePackGetRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [reviewOpen, setReviewOpen] = useState(false);
  const [lockOpen, setLockOpen] = useState(false);
  const [generateType, setGenerateType] = useState<string>();
  const [masterUrl, setMasterUrl] = useState<string>();
  const pack = bundle?.pack;
  const thumbs = useThumbs(characterId, pack);
  const authorityReady = !!(bundle?.masterLocked && bundle?.dnaLocked && bundle?.prpLocked);
  const reload = () => {
    setBusy(true);
    void fetchCharacterReferencePack(characterId)
      .then((next) => {
        setBundle(next);
        if (next.pack) onChanged?.(next.pack);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa tải được bộ ảnh chuẩn.')))
      .finally(() => setBusy(false));
  };
  const work = nextWork(bundle);
  const ident = identityLine(pack);

  const load = () => {
    if (!characterId.trim()) {
      setError('Thiếu mã nhân vật.');
      return;
    }
    setBusy(true);
    void fetchCharacterReferencePack(characterId)
      .then((row) => {
        setBundle(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được bộ tham chiếu.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, [characterId]);

  useEffect(() => {
    const id = bundle?.masterCandidateId;
    if (!id) {
      setMasterUrl(undefined);
      return;
    }
    let dead = false;
    let url: string | undefined;
    void fetchKitVideoMasterCandidateObjectUrl(id)
      .then((next) => {
        if (dead) {
          URL.revokeObjectURL(next);
          return;
        }
        url = next;
        setMasterUrl(next);
      })
      .catch(() => undefined);
    return () => {
      dead = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [bundle?.masterCandidateId]);

  const apply = (row: CharacterReferencePackRow) => {
    setBundle((cur) =>
      cur
        ? { ...cur, pack: row, blocked: row.blocked, characterName: row.characterName }
        : cur,
    );
    onChanged?.(row);
  };

  const run = (task: () => Promise<CharacterReferencePackRow>) => {
    setBusy(true);
    void task()
      .then((row) => {
        apply(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không thực hiện được.')))
      .finally(() => setBusy(false));
  };

  const name = bundle?.characterName || characterName || 'Nhân vật';
  const dnaSummary = useMemo(() => {
    const seen = new Map<string, { locked: string; ref: string; verdict: string }>();
    for (const item of pack?.identity ?? []) {
      if (!seen.has(item.attribute)) {
        seen.set(item.attribute, { locked: item.lockedValue, ref: item.referenceValue, verdict: item.verdict });
      }
    }
    return [...seen.entries()];
  }, [pack?.identity]);

  const renderView = (type: string) => {
    const item = pack?.items.find((x) => x.type === type);
    const ready = !!item?.artifactPath;
    const missingWarn = type === 'FULL_BODY' && !ready ? '⚠ Toàn thân — Chưa có' : ready ? '✓' : 'Chưa có';
    return (
      <article key={type} className="fx-crp__tile">
        <strong>{viewLabel(type)}</strong>
        {thumbs[type] ? <img src={thumbs[type]} alt={viewLabel(type)} /> : <div className="fx-crp__ph" />}
        <p>{ready ? `✓ ${viewLabel(type)}` : missingWarn}</p>
        {pack && !pack.immutable && !authorityReady ? (
          <Space direction="vertical" size={4}>
            <Button type={ready ? 'default' : 'primary'} size="small" onClick={() => setGenerateType(type)}>
              {generateCta(type, ready)}
            </Button>
            <Upload accept="image/*" showUploadList={false} beforeUpload={(file) => { run(() => attachCharacterReferenceItem(characterId, pack.id, type, file)); return false; }}>
              <Button size="small">{ready ? 'Đổi ảnh' : STAFF_UPLOAD_EXISTING}</Button>
            </Upload>
          </Space>
        ) : null}
      </article>
    );
  };

  return (
    <article className="fx-crp__pack">
      <header className="fx-crp__head">
        <h2>Nhân vật: {name}</h2>
        <p className="fx-desk__lead">Bộ ảnh chuẩn — dùng để giữ nhân vật nhất quán qua các cảnh.</p>
        <p>
          Nhân vật: {name} · Character ID: {characterId} · Era: {pack?.eraId || bundle?.pack?.eraId || 'ERA-01'}
        </p>
        {pack?.status === 'LOCKED' ? (
          <Alert
            type="success"
            showIcon
            message={`BỘ ẢNH CHUẨN · ${STAFF_LOCKED}`}
            description={
              <ul className="fx-crp__checks">
                <li>✓ {pack.requiredReady}/{pack.requiredTotal} góc</li>
                <li>✓ Character hợp lệ</li>
                <li>✓ DNA hợp lệ</li>
                <li>✓ Production Reference hợp lệ</li>
                <li>✓ Đã duyệt</li>
                <li>✓ Đã khóa</li>
                <li>{STAFF_AUTHORITY}</li>
                <li>{STAFF_COMPLETE}</li>
                <li>{STAFF_PRODUCTION_READY}</li>
              </ul>
            }
          />
        ) : (
          <p>
            {pack?.canUse
              ? STAFF_COMPLETE
              : pack?.missingTypes?.length
                ? `Thiếu: ${pack.missingTypes.map(viewLabel).join(', ')}`
                : pack?.coverageReady
                  ? 'Bộ ảnh chuẩn đã đủ. Kiểm tra trước khi gửi duyệt.'
                  : pack
                    ? 'Cần bổ sung'
                    : 'Chưa tạo bộ ảnh'}
          </p>
        )}
        <p className="fx-crp__progress">
          {pack
            ? `${pack.requiredReady}/${pack.requiredTotal}${
                pack.productionReady || pack.status === 'LOCKED'
                  ? ' · Sẵn sàng sản xuất'
                  : pack.canUse
                    ? ` · ${STAFF_COMPLETE}`
                    : pack.missingTypes?.length
                      ? ` · Thiếu: ${pack.missingTypes.map(viewLabel).join(', ')}`
                      : ''
              }`
            : 'Chưa có bộ ảnh chuẩn'}
        </p>
        <p className="fx-crp__next">
          <strong>Việc tiếp theo: {work.headline}</strong>
          <span>{work.hint}</span>
        </p>
      </header>
      {error ? <Alert type="error" showIcon message={error} /> : null}

      <section className="fx-crp__sec">
        <h3>Thông tin nhân vật</h3>
        <div className="fx-crp__who">
          {masterUrl ? <img src={masterUrl} alt={name} /> : <div className="fx-crp__ph" />}
          <div>
            <p>
              <strong>{name}</strong>
              {bundle?.characterAge ? <span> · {bundle.characterAge}</span> : null}
            </p>
            <p className="fx-desk__note">{bundle?.characterSummary || 'Đặc điểm lấy từ bộ nhận diện đã khóa.'}</p>
            <ul className="fx-crp__checks">
              <li>{bundle?.masterLocked ? '✓' : '○'} Nhân vật hợp lệ</li>
              <li>{bundle?.dnaLocked ? '✓' : '○'} DNA hợp lệ</li>
              <li>{pack ? '✓' : '○'} Đã có bộ ảnh</li>
              <li>{pack?.coverageReady ? '✓' : '○'} Đủ 4 góc</li>
            </ul>
          </div>
        </div>
        {!pack && bundle?.canCreate && !authorityReady ? (
          <Button type="primary" loading={busy} onClick={() => run(() => createCharacterReferencePack(characterId))}>
            {work.action || 'Tạo bộ tham chiếu'}
          </Button>
        ) : null}
        {!pack && !bundle?.canCreate && bundle?.blocked && !authorityReady ? (
          <ContentFamixaCharacterAuthorityCard
            characterId={characterId}
            characterName={name}
            eraId={pack?.eraId || 'ERA-01'}
            onChanged={reload}
          />
        ) : !pack && !bundle?.canCreate && bundle?.blocked ? (
          <Alert type="warning" showIcon message="Chưa thể tạo bộ ảnh chuẩn" description="Cần nhân vật và nhận diện đã khóa. Hệ thống không tự sửa." />
        ) : null}
        {authorityReady && !pack?.immutable ? (
          <ContentKitVideoCharacterReferenceSetCard
            characterId={characterId}
            characterName={name}
            eraId={pack?.eraId || 'ERA-01'}
            locked={pack?.status === 'LOCKED'}
            coverageReady={!!pack?.coverageReady}
            onGenerated={reload}
            onApprove={() => setReviewOpen(true)}
            onReject={() => setReviewOpen(true)}
          />
        ) : null}
      </section>

      {pack ? (
        <>
          <Tabs
            className="fx-crp__tabs"
            items={[
              {
                key: 'overview',
                label: 'Tổng quan',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Ảnh chuẩn của nhân vật</h3>
                    <div className="fx-crp__grid">{REQUIRED_REFERENCE_TYPES.map(renderView)}</div>
                    <ul className="fx-crp__checks">
                      <li>{bundle?.masterLocked ? '✓' : '○'} Nhân vật hợp lệ</li>
                      <li>{bundle?.dnaLocked ? '✓' : '○'} DNA hợp lệ</li>
                      <li>{pack.coverageReady ? '✓' : '○'} Đủ 4 góc</li>
                      <li>{pack.identityPass ? '✓' : '○'} Kiểm tra tính nhất quán</li>
                    </ul>
                    {mayApprove(pack) ? (
                      <Button type="primary" loading={busy} onClick={() => setReviewOpen(true)}>
                        {STAFF_APPROVE}
                      </Button>
                    ) : mayLock(pack) ? (
                      <Button type="primary" loading={busy} onClick={() => setLockOpen(true)}>
                        {STAFF_LOCK}
                      </Button>
                    ) : work.action && !pack.immutable ? (
                      <Button
                        type="primary"
                        loading={busy}
                        onClick={() => {
                          if (pack.coverageReady) run(() => validateCharacterReferencePack(characterId, pack.id));
                          else if (pack.missingTypes?.includes('FULL_BODY')) setGenerateType('FULL_BODY');
                          else if (pack.missingTypes?.[0]) setGenerateType(pack.missingTypes[0]);
                        }}
                      >
                        {work.action}
                      </Button>
                    ) : !pack.immutable && !mayApprove(pack) && !mayLock(pack) && pack.coverageReady ? (
                      <Alert type="info" showIcon message={STAFF_NOT_READY} description={notReadyReasons(pack).join(' ')} />
                    ) : null}
                  </section>
                ),
              },
              {
                key: 'face',
                label: 'Gương mặt',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Góc nhìn nhân vật</h3>
                    <div className="fx-crp__grid">{REQUIRED_REFERENCE_TYPES.filter((t) => t !== 'FULL_BODY').map(renderView)}</div>
                  </section>
                ),
              },
              {
                key: 'body',
                label: 'Hình dáng',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Dáng người</h3>
                    {renderView('FULL_BODY')}
                  </section>
                ),
              },
              {
                key: 'wardrobe',
                label: 'Trang phục',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Trang phục</h3>
                    <p className="fx-desk__note">Chỉ gắn khi DNA cho phép. Không tự thêm kính, mũ, trang sức.</p>
                  </section>
                ),
              },
              {
                key: 'expression',
                label: 'Biểu cảm',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Cảm xúc</h3>
                    <div className="fx-crp__emo">
                      {OPTIONAL_REFERENCE_TYPES.map((type) => {
                        const item = pack.items.find((x) => x.type === type);
                        return (
                          <article key={type} className="fx-crp__tile fx-crp__tile--sm">
                            <strong>{viewLabel(type)}</strong>
                            {thumbs[type] ? <img src={thumbs[type]} alt={viewLabel(type)} /> : <div className="fx-crp__ph fx-crp__ph--sm" />}
                            <p>{item?.artifactPath ? '✓ Có' : 'Không bắt buộc'}</p>
                            {!pack.immutable ? (
                              <Upload accept="image/*" showUploadList={false} beforeUpload={(file) => { run(() => attachCharacterReferenceItem(characterId, pack.id, type, file)); return false; }}>
                                <Button size="small">Gắn</Button>
                              </Upload>
                            ) : null}
                          </article>
                        );
                      })}
                    </div>
                  </section>
                ),
              },
              {
                key: 'continuity',
                label: 'Liên tục',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Điểm nhận diện cần giữ</h3>
                    <ul className="fx-crp__checks">
                      <li>Gương mặt</li>
                      <li>Mái tóc</li>
                      <li>Tỷ lệ cơ thể</li>
                    </ul>
                  </section>
                ),
              },
              {
                key: 'rules',
                label: 'Được phép',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Những điều không được thay đổi</h3>
                    <ul className="fx-crp__checks">
                      {['face', 'eyes', 'hair', 'age', 'proportion', 'style'].map((key) => {
                        const rows = pack.identity.filter((x) => x.attribute === key);
                        const hit = rows.find((x) => x.verdict === 'FAIL');
                        const passed = rows.some((x) => x.verdict === 'PASS') && !hit;
                        return (
                          <li key={key}>
                            {hit ? '✕' : passed ? '✓' : '○'} {IDENTITY_LABELS[key]}{' '}
                            {hit ? 'không khớp' : passed ? 'nhất quán' : 'chưa đối chiếu'}
                          </li>
                        );
                      })}
                    </ul>
                    {!ident.ok && ident.detail ? (
                      <Alert type="warning" showIcon message="Ảnh tham chiếu đang khác với đặc điểm nhân vật đã khóa." />
                    ) : null}
                  </section>
                ),
              },
              {
                key: 'review',
                label: 'Kiểm tra',
                children: (
                  <section className="fx-crp__sec">
                    <h3>Kiểm tra tính nhất quán</h3>
                    <ul className="fx-crp__checks">
                      <li>Ảnh tham chiếu: {pack.requiredReady} / {pack.requiredTotal}</li>
                      <li>Đặc điểm: {pack.identityPass ? 'khớp' : 'cần sửa'}</li>
                    </ul>
                    {!pack.immutable ? (
                      <Button loading={busy} onClick={() => run(() => validateCharacterReferencePack(characterId, pack.id))}>
                        Kiểm tra bộ ảnh
                      </Button>
                    ) : null}
                    {mayApprove(pack) || mayLock(pack) || pack.status === 'LOCKED' ? (
                      <>
                        <h3>Director Review</h3>
                        {pack.status === 'LOCKED' ? (
                          <p>{STAFF_AUTHORITY} · {pack.packVersion}</p>
                        ) : mayLock(pack) ? (
                          <Button type="primary" loading={busy} onClick={() => setLockOpen(true)}>
                            {STAFF_LOCK}
                          </Button>
                        ) : mayApprove(pack) ? (
                          <Space wrap>
                            <Button type="primary" onClick={() => setReviewOpen(true)}>{STAFF_APPROVE}</Button>
                            <Button danger onClick={() => {
                              const why = window.prompt('Lý do từ chối?') || '';
                              if (why.trim().length < 3) return;
                              run(() => rejectCharacterReferencePack(characterId, pack.id, why.trim()));
                            }}>Từ chối</Button>
                          </Space>
                        ) : (
                          <Alert type="info" showIcon message={STAFF_NOT_READY} description={notReadyReasons(pack).join(' ')} />
                        )}
                        {pack.productionReady ? <p className="fx-crp__ready">{STAFF_PRODUCTION_READY}</p> : null}
                      </>
                    ) : !pack.immutable ? (
                      <Alert type="info" showIcon message={STAFF_NOT_READY} description={notReadyReasons(pack).join(' ')} />
                    ) : null}
                  </section>
                ),
              },
            ]}
          />
          <Collapse
            className="fx-crp__tech"
            items={[
              {
                key: 'tech',
                label: 'Xem thông tin hệ thống',
                children: (
                  <pre>
                    {JSON.stringify(
                      {
                        packCode: pack.packCode,
                        status: pack.status,
                        version: pack.packVersion,
                        masterSha256: pack.masterSha256,
                        dnaSha256: pack.dnaSha256,
                        packSha256: pack.packSha256,
                        blocked: pack.blocked,
                      },
                      null,
                      2,
                    )}
                  </pre>
                ),
              },
            ]}
          />
        </>
      ) : null}

      <Modal
        title={approveConfirm(name).title}
        open={reviewOpen}
        onCancel={() => setReviewOpen(false)}
        width={880}
        footer={
          <Space>
            <Button onClick={() => setReviewOpen(false)}>{approveConfirm(name).cancel}</Button>
            <Button
              type="primary"
              loading={busy}
              disabled={!mayApprove(pack)}
              onClick={() => {
                if (!pack || !mayApprove(pack)) return;
                run(() => approveCharacterReferencePack(characterId, pack.id));
                setReviewOpen(false);
              }}
            >
              {approveConfirm(name).confirm}
            </Button>
          </Space>
        }
      >
        <p>{approveConfirm(name).body}</p>
        <p>{approveConfirm(name).after}</p>
        <p>{approveConfirm(name).check}</p>
        <div className="fx-crp__compare">
          <article>
            <strong>MASTER</strong>
            {masterUrl ? <img src={masterUrl} alt="Master" /> : <p>Chưa có ảnh Master</p>}
          </article>
          <article>
            <strong>REFERENCE</strong>
            <div className="fx-crp__grid fx-crp__grid--sm">
              {REQUIRED_REFERENCE_TYPES.map((type) => (
                <div key={type}>
                  <span>{viewLabel(type)}</span>
                  {thumbs[type] ? <img src={thumbs[type]} alt={viewLabel(type)} /> : <div className="fx-crp__ph fx-crp__ph--sm" />}
                </div>
              ))}
            </div>
          </article>
        </div>
        <Typography.Paragraph>
          DNA SUMMARY
          {dnaSummary.map(([key, val]) => (
            <span key={key}>
              <br />
              {IDENTITY_LABELS[key] || key}: {val.locked || 'UNKNOWN'}
            </span>
          ))}
        </Typography.Paragraph>
      </Modal>
      <Modal
        title={lockConfirm().title}
        open={lockOpen}
        onCancel={() => setLockOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setLockOpen(false)}>{lockConfirm().cancel}</Button>
            <Button
              type="primary"
              loading={busy}
              disabled={!mayLock(pack)}
              onClick={() => {
                if (!pack || !mayLock(pack)) return;
                run(() => lockCharacterReferencePack(characterId, pack.id));
                setLockOpen(false);
              }}
            >
              {lockConfirm().confirm}
            </Button>
          </Space>
        }
      >
        <p>{lockConfirm().body}</p>
        <p>{lockConfirm().after}</p>
        <p>{lockConfirm().check}</p>
      </Modal>
      <ContentKitVideoCharacterReferenceGenerationCard
        characterId={characterId}
        characterName={name}
        referenceType={generateType || 'FULL_BODY'}
        packId={pack?.id}
        replaceExisting={!!pack?.items.find((x) => x.type === (generateType || 'FULL_BODY') && x.artifactPath)}
        open={!!generateType && pack?.status !== 'LOCKED'}
        onClose={() => setGenerateType(undefined)}
        onChanged={load}
      />
    </article>
  );
}
