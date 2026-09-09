import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Collapse, Input, InputNumber, Modal, Radio, Select, Space, Tag, message } from 'antd';
import {
  approveCharacterStudio,
  approveCharacterStudioMasterRevision,
  characterStudioMasterImageUrl,
  characterStudioMasterRevisionCandidateImageUrl,
  characterStudioMasterRevisionPreviousImageUrl,
  characterStudioReferenceImageUrl,
  createCharacterStudio,
  fetchCharacterStudio,
  fetchCharacterStudioList,
  fetchContentSeriesVoicePreview,
  fetchContentSeriesVoices,
  fetchProjectVisualStyle,
  generateCharacterStudioReferenceSet,
  lockCharacterStudio,
  lockCharacterStudioMasterRevision,
  rejectCharacterStudio,
  rejectCharacterStudioMasterRevision,
  requestCharacterStudioMasterRevision,
  reviewCharacterAgeConsistency,
  reviewCharacterAppearanceConsistency,
  scoreCharacterStudioIdentity,
  type CharacterMasterRevisionResult,
  type CharacterStudioRow,
  type ProjectVisualStyleRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { http } from '@/shared/api/http';
import {
  MASTER_REVISION_REJECT_REASONS,
  STUDIO_GENDERS,
  STUDIO_REJECT_REASONS,
  ageConsistencyLabel,
  ageConsistencyShowsMismatch,
  appearanceConsistencyLabel,
  ageExpressionRange,
  directorFaceGate,
  directorGateFromStatus,
  directorGateMark,
  directorGateText,
  directorIntegrityVerified,
  directorStatusLabel,
  directorStyleConformanceGate,
  directorStyleGate,
  directorUniverseGate,
  masterRevisionMayRequestNew,
  profileValid,
  rejectReasonValid,
  studioGenderLabel,
  studioMayRegenerate,
  studioPublicHeadline,
  studioSlotTone,
  studioSlotVerdictLabel,
  studioMayScoreIdentity,
  studioMasterComplete,
  studioNeedsLock,
  studioAgeNeedsDirectorPass,
  studioAppearanceNeedsDirectorPass,
  studioDirectorNext,
  studioBibleReady,
  studioLockedCount,
  studioSlotsFailed,
  studioSlotsNeedVision,
  STUDIO_BIBLE_EXPECTED,
  STUDIO_BIBLE_READY,
  STUDIO_BUILD_EPISODE,
  STUDIO_COMPLETE_TITLE,
  STUDIO_CONTINUE_EPISODE,
  STUDIO_CONTINUE_EPISODE_MOBILE,
  STUDIO_DIRECTOR_APPROVED_VI,
  STUDIO_MASTER_LOCKED_VI,
  STUDIO_NEXT_STEP,
  STUDIO_SCORE_IDENTITY_LABEL,
  STUDIO_START_EPISODE,
} from './kit-video-character-studio';
import { ContentFamixaCharacterCalibrationShortcut } from './ContentFamixaCharacterCalibrationShortcut';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { FAMIXA_VISUAL_MODE, visualModeLabel } from './kit-video-visual-mode';
import {
  mergeVoiceSamples,
  playVoiceBlob,
  voiceLibraryStamp,
  voicesNeedSampleRefresh,
  type VoiceSampleOption,
} from './content-famixa-voice-sample';

async function authImage(url: string) {
  const { data } = await http.get<Blob>(url.replace(/^\/api/, ''), { responseType: 'blob' });
  return URL.createObjectURL(data);
}

function useStudioImages(row?: CharacterStudioRow) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    if (!row) {
      setUrls({});
      return;
    }
    const jobs: [string, string][] = [
      ['MASTER', characterStudioMasterImageUrl(row.characterId, row.masterSha256)],
      ...row.slots
        .filter((s) => s.present)
        .map((s) => [s.type, characterStudioReferenceImageUrl(row.characterId, s.type, s.sha256)] as [string, string]),
    ];
    if (row.masterRevision?.candidateMasterSha || row.masterRevision?.status) {
      jobs.push(
        ['MASTER_PREVIOUS', characterStudioMasterRevisionPreviousImageUrl(
          row.characterId,
          row.masterRevision.historicalMasterSha,
        )],
        ['MASTER_CANDIDATE', characterStudioMasterRevisionCandidateImageUrl(
          row.characterId,
          row.masterRevision.candidateMasterSha,
        )],
      );
    }
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
      setUrls(next);
    });
    return () => {
      dead = true;
      made.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [
    row?.characterId,
    row?.coverage,
    row?.status,
    row?.crpSha256,
    row?.masterSha256,
    row?.slots?.map((s) => s.sha256).join('|'),
    row?.masterRevision?.candidateMasterSha,
    row?.masterRevision?.historicalMasterSha,
    row?.masterRevision?.status,
  ]);
  return urls;
}

function useStudioCardThumbs(items: CharacterStudioRow[]) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    const jobs = items.map((row) => [row.characterId, characterStudioMasterImageUrl(row.characterId, row.masterSha256)] as const);
    void Promise.all(
      jobs.map(([id, url]) =>
        authImage(url)
          .then((blob) => {
            made.push(blob);
            return [id, blob] as const;
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
      setUrls(next);
    });
    return () => {
      dead = true;
      made.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [items.map((row) => `${row.characterId}:${row.masterSha256 || ''}`).join('|')]);
  return urls;
}

export function ContentFamixaCharacterStudio({
  onBuildEpisode,
  onBibleReady,
  focusCharacterId,
  returnToScene,
  onReturnToScene,
  voices,
  voiceById,
  onAssignVoice,
}: {
  onBuildEpisode?: () => void;
  onBibleReady?: (ready: boolean) => void;
  focusCharacterId?: string;
  returnToScene?: { shotId: string; sceneLabel: string };
  onReturnToScene?: () => void;
  voices?: { value: string; label: string; previewUrl?: string | null }[];
  voiceById?: Record<string, string>;
  onAssignVoice?: (characterId: string, voiceId: string, voiceName?: string) => void;
} = {}) {
  const [items, setItems] = useState<CharacterStudioRow[]>([]);
  const [projectStyle, setProjectStyle] = useState<ProjectVisualStyleRow>();
  const [openId, setOpenId] = useState<string>();
  const [detail, setDetail] = useState<CharacterStudioRow>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [createOpen, setCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [age, setAge] = useState<number>(11);
  const [gender, setGender] = useState('male');
  const [role, setRole] = useState('');
  const [personality, setPersonality] = useState('');
  const [description, setDescription] = useState('');
  const [extraDescription, setExtraDescription] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [confirmError, setConfirmError] = useState<string>();
  const [confirmMode, setConfirmMode] = useState<'generate' | 'regenerate' | 'master-revision' | 'score-identity'>('generate');
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectThenRegen, setRejectThenRegen] = useState(false);
  const [rejectCode, setRejectCode] = useState('FACE_MISMATCH');
  const [rejectText, setRejectText] = useState('');
  const [reviewApproveOpen, setReviewApproveOpen] = useState(false);
  const [reviewRejectOpen, setReviewRejectOpen] = useState(false);
  const [reviewLockOpen, setReviewLockOpen] = useState(false);
  const [justLocked, setJustLocked] = useState<{
    characterId: string;
    name: string;
    version?: string | null;
    masterSha256?: string | null;
  }>();
  const [voiceOpts, setVoiceOpts] = useState<VoiceSampleOption[]>(voices ?? []);
  const [revisionRejectCode, setRevisionRejectCode] = useState<(typeof MASTER_REVISION_REJECT_REASONS)[number]['id']>(
    'TOO_OLD',
  );
  const [revisionRejectNotes, setRevisionRejectNotes] = useState('');
  const images = useStudioImages(detail);
  const thumbs = useStudioCardThumbs(items);
  useEffect(() => {
    if (voices?.length && !voicesNeedSampleRefresh(voices)) {
      setVoiceOpts((cur) => mergeVoiceSamples(cur, voices));
      return;
    }
    if (voices?.length) setVoiceOpts((cur) => mergeVoiceSamples(cur, voices));
    void fetchContentSeriesVoices()
      .then((rows) => setVoiceOpts((cur) => mergeVoiceSamples(cur, (rows ?? []).map((v) => ({
        value: v.voiceId,
        label: v.name || v.voiceId,
        previewUrl: v.previewUrl,
      })))))
      .catch(() => undefined);
  }, [voiceLibraryStamp(voices)]);

  const unwrapRevision = (result: CharacterMasterRevisionResult) => {
    if (result.gateCode && !result.character) {
      throw new Error(result.staffMessage || result.gateCode);
    }
    if (!result.character) throw new Error(result.staffMessage || 'Không cập nhật được Master Revision.');
    return result.character;
  };

  const loadList = () => {
    void Promise.all([fetchCharacterStudioList(), fetchProjectVisualStyle('FAMIXA')])
      .then(([list, style]) => {
        setItems(list.items ?? []);
        setProjectStyle(style);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không tải được Character Studio.')));
  };

  const loadDetail = (id: string) => {
    void fetchCharacterStudio(id)
      .then((row) => {
        setDetail(row);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không tải được nhân vật.')));
  };

  useEffect(() => {
    loadList();
  }, []);

  useEffect(() => {
    if (!openId) {
      setDetail(undefined);
      return;
    }
    loadDetail(openId);
  }, [openId]);

  useEffect(() => {
    if (!error) return;
    const network = /offline|mất kết nối|HTTP 50|server/i.test(error);
    if (!network) return;
    const t = window.setTimeout(() => {
      loadList();
      if (openId) loadDetail(openId);
    }, 3000);
    return () => window.clearTimeout(t);
  }, [error, openId]);

  useEffect(() => {
    onBibleReady?.(studioBibleReady(items));
  }, [items, onBibleReady]);

  useEffect(() => {
    if (focusCharacterId) setOpenId(focusCharacterId);
  }, [focusCharacterId]);

  const selected = detail ?? items.find((r) => r.characterId === openId);
  const crpStale = !!selected?.masterRevision?.crpStale;
  const slotsNeedVision = studioSlotsNeedVision(selected?.slots, selected?.officialLocked);
  const slotsFailed = studioSlotsFailed(selected?.slots, selected?.officialLocked);
  const canRedo = !!selected
    && (selected.mayRegenerate
      || studioMayRegenerate(
        selected.status,
        selected.officialLocked,
        selected.ageConsistencyStatus,
        selected.masterRevision?.crpStale,
        slotsNeedVision || slotsFailed,
      ));
  const canStartRedo = canRedo || crpStale || slotsNeedVision || slotsFailed;
  const canCreate = useMemo(
    () => profileValid({ name, age, gender, description }),
    [name, age, gender, description],
  );

  const run = (job: Promise<CharacterStudioRow>, ok: string, after?: (row: CharacterStudioRow) => void) => {
    setBusy(true);
    void job
      .then((row) => {
        const blocked = (row.gateCode || '').toUpperCase().includes('NOT_READY')
          || (row.status || '').toUpperCase() === 'BLOCKED';
        setDetail(row);
        setOpenId(row.characterId);
        setItems((cur) => {
          const next = cur.some((x) => x.characterId === row.characterId)
            ? cur.map((x) => (x.characterId === row.characterId ? row : x))
            : [row, ...cur];
          return next;
        });
        if (blocked) {
          setError(row.staffMessage || ok);
          return;
        }
        setError(undefined);
        after?.(row);
      })
      .catch((e) => setError(apiErrorMessage(e, ok)))
      .finally(() => setBusy(false));
  };

  const markStudioLocked = (row: CharacterStudioRow) => {
    if (!row.officialLocked) return;
    setJustLocked({
      characterId: row.characterId,
      name: row.characterName,
      version: row.version,
      masterSha256: row.masterSha256,
    });
  };

  const projectStyleReady = !!projectStyle?.ready;
  const ageGate = directorGateFromStatus(selected?.ageConsistencyStatus, selected?.officialLocked);
  const appearanceGate = directorGateFromStatus(selected?.appearanceConsistencyStatus, selected?.officialLocked);
  const faceGate = directorFaceGate(selected?.slots, selected?.officialLocked);
  const styleGate = directorStyleGate(selected?.visualStyleGate, projectStyleReady);
  const styleConformanceGate = directorStyleConformanceGate(
    selected?.styleConformanceStatus,
    selected?.officialLocked,
  );
  const universeGate = directorUniverseGate(selected?.visualUniverseGate, selected?.officialLocked);
  const masterComplete = studioMasterComplete(selected);
  const needsLock = studioNeedsLock(selected);
  const directorNext = studioDirectorNext(selected, { projectStyleReady });
  const bibleReady = studioBibleReady(items);
  const lockedCount = studioLockedCount(items);
  const goEpisode = () => onBuildEpisode?.();

  const runDirectorNext = () => {
    if (!selected || !directorNext?.enabled) return;
    switch (directorNext.kind) {
      case 'score_identity':
        setConfirmMode('score-identity');
        setConfirmOpen(true);
        break;
      case 'age_pass':
        run(
          reviewCharacterAgeConsistency(selected.characterId, { result: 'PASS' }),
          'Không chấm được tuổi. Chấm 4 góc với Master trước nếu chưa làm.',
        );
        break;
      case 'appearance_pass':
        run(
          reviewCharacterAppearanceConsistency(selected.characterId, { result: 'PASS' }),
          'Không chấm được ngoại hình.',
        );
        break;
      case 'approve':
        run(approveCharacterStudio(selected.characterId), 'Không duyệt được.');
        break;
      case 'lock':
        run(lockCharacterStudio(selected.characterId), 'Không khóa được.', markStudioLocked);
        break;
      case 'generate':
        setConfirmMode('generate');
        setConfirmOpen(true);
        break;
      case 'regenerate':
        setConfirmMode('regenerate');
        setConfirmOpen(true);
        break;
      default:
        break;
    }
  };

  return (
    <section className="fx-cstudio">
      <header className="fx-cstudio__head">
        <div>
          <h2>Character Studio</h2>
          <p className="fx-desk__lead">Director Review Workspace</p>
        </div>
        <div className="fx-cstudio__head-actions">
          <ContentFamixaVisualModeBadge compact />
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            + Tạo nhân vật
          </Button>
        </div>
      </header>
      <ContentFamixaCharacterCalibrationShortcut />
      {error ? (
        <Alert
          type="error"
          showIcon
          closable
          onClose={() => setError(undefined)}
          message={error}
          action={
            <Button
              size="small"
              loading={busy}
              onClick={() => {
                loadList();
                if (openId) loadDetail(openId);
              }}
            >
              Thử lại
            </Button>
          }
        />
      ) : null}

      {justLocked || (returnToScene && selected?.officialLocked) ? (
        <div className="fx-cstudio__lock-ok">
          <p className="fx-cstudio__complete-title">
            ✓ Nhân vật {justLocked?.name || selected?.characterName} đã được LOCK
          </p>
          <p>
            Master Reference: {justLocked?.version || selected?.version || 'V1'}
            {(justLocked?.masterSha256 || selected?.masterSha256)
              ? ` · ${(justLocked?.masterSha256 || selected?.masterSha256 || '').slice(0, 8)}`
              : ''}
          </p>
          <p>Character status: LOCKED</p>
          <p>Master đã khóa. Không cần khóa lại. Quay lại cảnh để tạo ảnh.</p>
          {returnToScene && onReturnToScene ? (
            <Button type="primary" size="large" className="fx-cstudio__next-cta" onClick={onReturnToScene}>
              Quay lại {returnToScene.sceneLabel}
            </Button>
          ) : null}
        </div>
      ) : null}

      {bibleReady ? (
        <div className="fx-cstudio__bible">
          <p className="fx-cstudio__bible-kicker">CHARACTER MASTER</p>
          <h3>
            {lockedCount} / {STUDIO_BIBLE_EXPECTED} COMPLETE
          </h3>
          <ul className="fx-cstudio__bible-list">
            {items.map((row) => (
              <li key={row.characterId}>✓ {row.characterName}</li>
            ))}
          </ul>
          <p className="fx-cstudio__bible-status">{STUDIO_BIBLE_READY}</p>
          <Button type="primary" size="large" className="fx-cstudio__next-cta" onClick={goEpisode}>
            {STUDIO_START_EPISODE}
          </Button>
        </div>
      ) : null}

      <div className="fx-cstudio__grid">
        {items.map((row) => {
          const cardAge = directorGateFromStatus(row.ageConsistencyStatus, row.officialLocked);
          const cardAppearance = directorGateFromStatus(row.appearanceConsistencyStatus, row.officialLocked);
          const cardFace = directorFaceGate(row.slots, row.officialLocked);
          const cardStyle = directorStyleGate(row.visualStyleGate, projectStyleReady);
          const cardStyleConformance = directorStyleConformanceGate(row.styleConformanceStatus, row.officialLocked);
          const cardUniverse = directorUniverseGate(row.visualUniverseGate, row.officialLocked);
          return (
            <button
              key={row.characterId}
              type="button"
              className={`fx-cstudio__card${openId === row.characterId ? ' is-on' : ''}${row.officialLocked ? ' is-locked' : ''}`}
              onClick={() => setOpenId(row.characterId)}
            >
              {thumbs[row.characterId] ? (
                <img src={thumbs[row.characterId]} alt="" />
              ) : (
                <div className="fx-clib__ph" />
              )}
              <strong>{row.characterName}</strong>
              <p>
                {row.age ? `${row.age} tuổi` : ''}
                {row.role ? ` · ${row.role}` : ''}
              </p>
              {row.officialLocked ? (
                <p className="fx-cstudio__card-status is-locked">{STUDIO_MASTER_LOCKED_VI}</p>
              ) : (
                <>
                  <p className="fx-cstudio__gates">
                    {directorGateMark(cardAge)} Age · {directorGateMark(cardAppearance)} Appearance · {directorGateMark(cardFace)} Face · {directorGateMark(cardStyle)} Visual Style
                    <br />
                    {directorGateMark(cardStyleConformance)} Style · {directorGateMark(cardUniverse)} Visual Universe
                  </p>
                  <p>{directorStatusLabel({ status: row.status, officialLocked: row.officialLocked, crpStale: row.masterRevision?.crpStale })}</p>
                </>
              )}
              <span className="fx-cstudio__xem">Xem</span>
            </button>
          );
        })}
        {!items.length && !busy ? <p className="fx-desk__note">Chưa có nhân vật.</p> : null}
      </div>

      {selected ? (
        <article className="fx-cstudio__preview">
          <div className="fx-cstudio__hero">
            {images.MASTER || images.FRONT ? (
              <img src={images.MASTER || images.FRONT} alt="" />
            ) : (
              <div className="fx-clib__ph" />
            )}
            <div>
              <h3>{selected.characterName}</h3>
              <p>
                {selected.characterId}
                {selected.age ? ` · ${selected.age} tuổi` : ''}
                {selected.role ? ` · ${selected.role}` : selected.gender ? ` · ${studioGenderLabel(selected.gender)}` : ''}
              </p>
              <p className="fx-pvs__inherit">
                Visual Style · Inherited from Project · {visualModeLabel(FAMIXA_VISUAL_MODE.visualMode)}
                {' · '}
                {selected.projectVisualStyleName || projectStyle?.styleName || FAMIXA_VISUAL_MODE.visualStyle}
              </p>
              <div className="fx-cstudio__refmeta">
                <p className="fx-pvs__kicker">REFERENCE</p>
                <p>
                  {selected.characterName} — Character Studio
                </p>
                <p>Visual: {visualModeLabel(FAMIXA_VISUAL_MODE.visualMode)}</p>
                <p>Status: {selected.officialLocked ? 'LOCKED' : directorStatusLabel({
                  status: selected.status,
                  officialLocked: selected.officialLocked,
                  crpStale,
                })}</p>
                <p>Role: IDENTITY ANCHOR</p>
              </div>
              <p>
                <Tag color={selected.officialLocked ? 'green' : slotsFailed ? 'red' : 'blue'}>
                  {selected.officialLocked
                    ? STUDIO_MASTER_LOCKED_VI
                    : directorStatusLabel({
                        status: selected.status,
                        officialLocked: selected.officialLocked,
                        crpStale,
                      })}
                </Tag>
                {masterComplete || needsLock ? (
                  <Tag color="green">{STUDIO_DIRECTOR_APPROVED_VI}</Tag>
                ) : null}
                {selected.version ? <Tag>Revision {selected.version}</Tag> : null}
              </p>
              <div className="fx-cstudio__voice">
                <p className="fx-pvs__kicker">GIỌNG NÓI</p>
                <p className="fx-desk__note">
                  Gán một lần trên nhân vật. Nghe mẫu thư viện — không tính phí. Dán câu để TTS (tab Thoại) mới tính phí.
                </p>
                <Space wrap>
                  <Select
                    showSearch
                    optionFilterProp="label"
                    style={{ width: 'min(420px, 100%)' }}
                    placeholder={`Chọn giọng cho ${selected.characterName}`}
                    value={voiceById?.[selected.characterId] || undefined}
                    options={voiceOpts}
                    onChange={(id, opt) => {
                      const label = !Array.isArray(opt) && opt && typeof opt === 'object' && 'label' in opt
                        ? String(opt.label)
                        : undefined;
                      onAssignVoice?.(selected.characterId, id, label);
                    }}
                  />
                  <Button
                    disabled={!voiceById?.[selected.characterId]}
                    onClick={() => {
                      const voiceId = voiceById?.[selected.characterId];
                      if (!voiceId) return;
                      const hide = message.loading({ content: 'Đang tải mẫu giọng…', key: 'voice-sample', duration: 0 });
                      void fetchContentSeriesVoicePreview(voiceId)
                        .then((blob) => playVoiceBlob(blob))
                        .then((ok) => {
                          if (!ok) message.warning('Không phát được mẫu. Thử lại — không gọi TTS.');
                        })
                        .catch(() => {
                          message.warning('Giọng này chưa có file mẫu trong thư viện. Không tạo TTS từ đây.');
                        })
                        .finally(() => {
                          hide();
                          message.destroy('voice-sample');
                        });
                    }}
                  >
                    Nghe mẫu
                  </Button>
                </Space>
              </div>
              {!masterComplete ? (
                <>
              <p>
                Appearance target{' '}
                <b>
                  {selected.appearanceProfile?.targetAppearanceAgeMin ??
                    selected.ageExpressionMinYears ??
                    (selected.age ? ageExpressionRange(selected.age).min : '—')}
                  –
                  {selected.appearanceProfile?.targetAppearanceAgeMax ??
                    selected.ageExpressionMaxYears ??
                    (selected.age ? ageExpressionRange(selected.age).max : '—')}
                </b>
              </p>
              <div className="fx-cstudio__review-gates">
                <span className={`is-${ageGate}`}>
                  {directorGateMark(ageGate)} Age {directorGateText(ageGate)}
                </span>
                <span className={`is-${appearanceGate}`}>
                  {directorGateMark(appearanceGate)} Appearance {directorGateText(appearanceGate)}
                </span>
                <span className={`is-${faceGate}`}>
                  {directorGateMark(faceGate)} Face {directorGateText(faceGate)}
                </span>
                <span className={`is-${styleGate}`}>
                  {directorGateMark(styleGate)} Visual Style {directorGateText(styleGate)}
                </span>
                <span className={`is-${styleConformanceGate}`}>
                  {directorGateMark(styleConformanceGate)} Style {directorGateText(styleConformanceGate)}
                </span>
                <span className={`is-${universeGate}`}>
                  {directorGateMark(universeGate)} Visual Universe {directorGateText(universeGate)}
                </span>
              </div>
              <p className="fx-desk__note">
                Authority {selected.visualUniverseVersion || 'V1'} / {(selected.visualUniverseAuthoritySha || '').slice(0, 8) || '—'}
                {' · '}
                Compiler {selected.visualCompiler || 'UnifiedVisualCompilerV1'}
                {' · '}
                {selected.visualCompilerStatus || '—'}
              </p>
                </>
              ) : null}
              {selected.staffMessage && !selected.officialLocked ? (
                <Alert
                  type={slotsFailed ? 'error' : slotsNeedVision || crpStale ? 'warning' : 'info'}
                  showIcon
                  message={selected.staffMessage}
                />
              ) : null}
              {slotsNeedVision ? (
                <Alert
                  type="warning"
                  showIcon
                  message="Bộ 4 ảnh chưa được chấm với Master"
                  description="Bấm Chấm với Master. Máy chấm 4 ảnh hiện tại — không vẽ ảnh mới, không duyệt tự động."
                />
              ) : null}
              {slotsFailed ? (
                <Alert
                  type="error"
                  showIcon
                  message="Có góc không khớp Master"
                  description="Tạo lại bộ ảnh hoặc đánh Không đạt. Không duyệt khi còn góc FAIL."
                />
              ) : null}
              {ageConsistencyShowsMismatch(selected.ageConsistencyStatus, selected.officialLocked) ? (
                <Alert
                  type="error"
                  showIcon
                  message="AGE MISMATCH"
                  description={selected.ageConsistencyReason || 'Tuổi biểu hiện không phù hợp với tuổi nhân vật.'}
                />
              ) : null}
              {selected.rejectReasonText ? <p>Lý do: {selected.rejectReasonText}</p> : null}
            </div>
          </div>

          {!masterComplete && (selected.masterRevision?.candidateMasterSha || selected.masterRevision?.status) ? (
            <div className="fx-cstudio__compare">
              {(() => {
                const rev = selected.masterRevision!;
                const locked =
                  rev.status === 'MASTER_REVISION_LOCKED' || !!rev.candidateIsAuthority;
                const leftSrc = locked ? images.MASTER_PREVIOUS || images.MASTER : images.MASTER;
                const rightSrc = images.MASTER_CANDIDATE || (locked ? images.MASTER : undefined);
                const leftSha = locked
                  ? rev.historicalMasterSha || rev.currentMasterSha
                  : rev.currentMasterSha || selected.masterSha256;
                const rightSha = rev.candidateMasterSha;
                const same =
                  !!leftSha
                  && !!rightSha
                  && leftSha.toLowerCase() === rightSha.toLowerCase();
                const showRight = !!rightSha && !same;
                return (
                  <>
                    <div className="fx-cstudio__compare-grid">
                      <figure>
                        {leftSrc ? <img src={leftSrc} alt="" /> : <div className="fx-clib__ph" />}
                        <figcaption>
                          {locked ? 'Master cũ' : 'Master hiện tại'}
                          {' · '}
                          {locked ? rev.historicalMasterVersion || 'V1' : rev.currentVersion || 'V1'}
                        </figcaption>
                        <p>
                          <Tag color="green">LOCKED</Tag>
                          {locked ? (
                            <Tag>historical authority</Tag>
                          ) : (
                            <Tag color="blue">CURRENT AUTHORITY</Tag>
                          )}
                        </p>
                      </figure>
                      {showRight ? (
                        <figure>
                          {rightSrc ? <img src={rightSrc} alt="" /> : <div className="fx-clib__ph" />}
                          <figcaption>
                            {locked ? 'Master hiện tại' : 'Master Revision candidate'}
                            {' · '}
                            {rev.candidateVersion || 'V2'}
                          </figcaption>
                          <p>
                            {locked ? (
                              <>
                                <Tag color="green">LOCKED</Tag>
                                <Tag color="blue">CURRENT AUTHORITY</Tag>
                              </>
                            ) : (
                              <Tag>{rev.status || 'PENDING_REVIEW'}</Tag>
                            )}
                          </p>
                        </figure>
                      ) : (
                        <div className="fx-cstudio__compare-note">
                          {locked ? (
                            <p>
                              Master Revision đã khóa. Ảnh bên trái là Master cũ. Ảnh authority hiện tại
                              trùng SHA candidate — đang dùng mặt này.
                            </p>
                          ) : (
                            <p>Chưa có Master Revision candidate.</p>
                          )}
                        </div>
                      )}
                    </div>
                    <p>
                      Reason: <b>{rev.revisionReason || '—'}</b>
                      {' · '}
                      Age target: <b>{rev.ageTarget || '—'}</b>
                      {' · '}
                      Status: <Tag>{rev.status || 'NONE'}</Tag>
                    </p>
                    <Collapse
                      ghost
                      className="fx-director-tech"
                      items={[{
                        key: 'rev-adv',
                        label: 'Revision / Advanced Actions',
                        children: (
                    <Space wrap className="fx-cstudio__compare-actions">
                      {rev.mayApprove ? (
                        <Button size="large" loading={busy} onClick={() => setReviewApproveOpen(true)}>
                          Duyệt Master
                        </Button>
                      ) : null}
                      {rev.mayReject ? (
                        <Button size="large" loading={busy} onClick={() => setReviewRejectOpen(true)}>
                          Không đạt
                        </Button>
                      ) : null}
                      {rev.mayLock ? (
                        <Button size="large" loading={busy} onClick={() => setReviewLockOpen(true)}>
                          Khóa Master
                        </Button>
                      ) : null}
                      {rev.status === 'MASTER_REVISION_LOCKED' ? (
                        <Tag color="green">Đã khóa làm Authority mới</Tag>
                      ) : null}
                      {rev.status === 'MASTER_REVISION_REJECTED' ? <Tag color="red">Không đạt</Tag> : null}
                      {rev.crpStale && !selected.officialLocked ? (
                        <Button
                          size="large"
                          loading={busy}
                          onClick={() => {
                            setConfirmMode('regenerate');
                            setConfirmOpen(true);
                          }}
                        >
                          Tạo lại bộ 4 ảnh từ Master hiện tại
                        </Button>
                      ) : null}
                      {!selected.officialLocked && (rev.mayRequest || masterRevisionMayRequestNew(rev.status, selected.officialLocked)
                      || rev.status === 'MASTER_REVISION_REJECTED') ? (
                        <Button
                          size="large"
                          loading={busy}
                          disabled={busy}
                          onClick={() => {
                            setConfirmError(undefined);
                            setConfirmMode('master-revision');
                            setConfirmOpen(true);
                          }}
                        >
                          Tạo Revision mới
                        </Button>
                      ) : null}
                    </Space>
                        ),
                      }]}
                    />
                    {rev.crpStale ? (
                      <Alert
                        type="warning"
                        showIcon
                        message="Bộ 4 ảnh đang theo Master cũ"
                        description="Master hiện tại đã khóa. Bấm Tạo lại bộ 4 ảnh từ Master hiện tại — không cần Master Revision mới."
                      />
                    ) : null}
                    {rev.dnaStale || rev.prpStale ? (
                      <Alert
                        type="info"
                        showIcon
                        message="DNA / PRP đang theo Master cũ"
                        description="Chấm CRP với Master hiện tại trước. DNA và PRP chỉ rebuild khi Director yêu cầu."
                      />
                    ) : null}
                    {rev.mayApprove ? (
                      <p className="fx-desk__note">Muốn lấy mặt candidate: bấm Duyệt Master, rồi Khóa Master.</p>
                    ) : null}
                    {rev.mayLock ? (
                      <p className="fx-desk__note">Đã duyệt. Bấm Khóa Master để mặt này thành Authority.</p>
                    ) : null}
                  </>
                );
              })()}
            </div>
          ) : null}

          {!masterComplete && selected.progress?.length ? (
            <ol className="fx-cstudio__progress">
              {selected.progress.map((step) => (
                <li key={step.step} className={step.failed ? 'is-bad' : step.done ? 'is-done' : step.active ? 'is-on' : ''}>
                  {step.failed ? '✕' : step.done ? '✓' : '○'} {step.label}
                </li>
              ))}
            </ol>
          ) : null}

          {masterComplete ? (
            <div className="fx-cstudio__complete">
              <p className="fx-cstudio__complete-title">✓ {STUDIO_COMPLETE_TITLE}</p>
              <p>
                Nhân vật đã được Director duyệt và Master Reference đã được khóa.
                Không cần tạo Revision mới trừ khi Director chủ động yêu cầu thay đổi.
              </p>
              <div className="fx-cstudio__decision">
                <p>Director Decision</p>
                <p>
                  <Tag color="green">{STUDIO_DIRECTOR_APPROVED_VI}</Tag>
                  {selected.history?.find((h) => (h.status || '').toUpperCase().includes('APPROV'))?.statusLabel
                    ? ` · ${selected.history.find((h) => (h.status || '').toUpperCase().includes('APPROV'))?.statusLabel}`
                    : ''}
                </p>
              </div>
              <h4>MASTER REFERENCE</h4>
              <ul className="fx-cstudio__lock-list">
                <li>✓ Identity locked</li>
                <li>✓ Face consistency locked</li>
                <li>✓ Age consistency locked</li>
                <li>✓ Appearance consistency locked</li>
                <li>✓ Visual style locked</li>
                <li>✓ Required views complete</li>
              </ul>
              {returnToScene && onReturnToScene ? (
                <div className="fx-cstudio__next">
                  <p className="fx-cstudio__next-label">{STUDIO_NEXT_STEP}</p>
                  <p className="fx-cstudio__next-hint">Quay lại cảnh đang làm</p>
                  <Button type="primary" size="large" className="fx-cstudio__next-cta" onClick={onReturnToScene}>
                    Quay lại {returnToScene.sceneLabel}
                  </Button>
                </div>
              ) : !bibleReady ? (
                <div className="fx-cstudio__next">
                  <p className="fx-cstudio__next-label">{STUDIO_NEXT_STEP}</p>
                  <p className="fx-cstudio__next-hint">{STUDIO_BUILD_EPISODE}</p>
                  <Button type="primary" size="large" className="fx-cstudio__next-cta" onClick={goEpisode}>
                    {STUDIO_CONTINUE_EPISODE}
                  </Button>
                </div>
              ) : (
                <p className="fx-desk__note">
                  {STUDIO_BIBLE_READY} · {STUDIO_NEXT_STEP}: {STUDIO_BUILD_EPISODE}
                </p>
              )}
            </div>
          ) : (
            <div className="fx-cstudio__primary">
              <p className="fx-cstudio__next-label">{STUDIO_NEXT_STEP}</p>
              {directorNext ? (
                <>
                  <p className="fx-cstudio__next-hint">{directorNext.hint}</p>
                  {error ? (
                    <Alert type="error" showIcon message={error} className="fx-cstudio__next-error" />
                  ) : null}
                  <Button
                    type="primary"
                    size="large"
                    loading={busy}
                    disabled={!directorNext.enabled}
                    className="fx-cstudio__next-cta"
                    onClick={runDirectorNext}
                  >
                    {directorNext.label}
                  </Button>
                </>
              ) : (
                <p className="fx-desk__note">{selected.staffMessage || 'Hoàn tất bước hiện tại trên nhân vật này.'}</p>
              )}
            </div>
          )}

          <div className="fx-cstudio__refs">
            {selected.slots.map((slot) => (
              <figure key={slot.type}>
                {images[slot.type] ? <img src={images[slot.type]} alt="" /> : <div className="fx-clib__ph" />}
                <figcaption className={`is-${studioSlotTone(slot.verdict)}`}>
                  {slot.label}
                  <br />
                  {studioSlotVerdictLabel(slot.verdict)}
                </figcaption>
              </figure>
            ))}
          </div>

          {!masterComplete ? (
          <p className="fx-desk__note">
            Phong cách dự án: <b>{selected.projectVisualStyleName || projectStyle?.styleName || '—'}</b>
            {' · '}Inherited from Project
          </p>
          ) : null}

          <Collapse
            ghost
            className="fx-director-tech"
            items={[{
              key: 'studio-adv',
              label: 'Revision / Advanced Actions',
              children: (
          <Space wrap>
            {selected.mayRequestMasterRevision && !selected.officialLocked
            && selected.masterRevision?.status !== 'MASTER_REVISION_REJECTED' ? (
              <Button
                loading={busy}
                onClick={() => {
                  if (!projectStyleReady && !selected.projectVisualStyleSha) {
                    setError('Cần Project Visual Style Authority trước khi tạo Master Revision.');
                    return;
                  }
                  setConfirmError(undefined);
                  setConfirmMode('master-revision');
                  setConfirmOpen(true);
                }}
              >
                Bắt đầu tạo Master Revision
              </Button>
            ) : null}
            {selected.mayGenerate && !selected.officialLocked && !canStartRedo ? (
              <Button
                loading={busy}
                disabled={selected.visualStyleGate === 'PROJECT_VISUAL_STYLE_NOT_READY' && !projectStyle?.ready}
                onClick={() => {
                  setConfirmMode('generate');
                  setConfirmOpen(true);
                }}
              >
                Tạo bộ ảnh chuẩn
              </Button>
            ) : null}
            {studioAgeNeedsDirectorPass(selected) ? (
              <Button
                loading={busy}
                onClick={() =>
                  run(
                    reviewCharacterAgeConsistency(selected.characterId, { result: 'PASS' }),
                    'Không chấm được tuổi.',
                  )
                }
              >
                Chấm tuổi · PASS
              </Button>
            ) : null}
            {studioAppearanceNeedsDirectorPass(selected) ? (
              <Button
                loading={busy}
                onClick={() =>
                  run(
                    reviewCharacterAppearanceConsistency(selected.characterId, { result: 'PASS' }),
                    'Không chấm được ngoại hình.',
                  )
                }
              >
                Chấm ngoại hình · PASS
              </Button>
            ) : null}
            {selected.mayApprove
            || (selected.coverage >= 4
            && !selected.officialLocked
            && !studioAgeNeedsDirectorPass(selected)
            && !studioAppearanceNeedsDirectorPass(selected)
            && !selected.mayLock
            && (selected.ageConsistencyStatus || '').toUpperCase() === 'PASS'
            && (selected.appearanceConsistencyStatus || '').toUpperCase() === 'PASS') ? (
              <Button
                loading={busy}
                onClick={() => run(approveCharacterStudio(selected.characterId), 'Không duyệt được.')}
              >
                Duyệt bộ ảnh
              </Button>
            ) : null}
            {selected.mayReject ? (
              <Button
                onClick={() => {
                  setRejectThenRegen(false);
                  setRejectOpen(true);
                }}
              >
                Không đạt
              </Button>
            ) : null}
            {studioMayScoreIdentity(selected) ? (
              <Button
                type="primary"
                loading={busy}
                onClick={() => {
                  setConfirmMode('score-identity');
                  setConfirmOpen(true);
                }}
              >
                {STUDIO_SCORE_IDENTITY_LABEL}
              </Button>
            ) : null}
            {canStartRedo ? (
              <Button
                loading={busy}
                onClick={() => {
                  setConfirmMode('regenerate');
                  setConfirmOpen(true);
                }}
              >
                Tạo lại bộ ảnh
              </Button>
            ) : null}
            {selected.mayLock ? (
              <Button
                loading={busy}
                onClick={() => run(lockCharacterStudio(selected.characterId), 'Không khóa được.', markStudioLocked)}
              >
                Khóa nhân vật
              </Button>
            ) : null}
          </Space>
              ),
            }]}
          />

          <p className="fx-desk__note">
            Artifact Integrity {directorIntegrityVerified(selected) ? '✓ Verified' : '○ Incomplete'}
          </p>
          <Collapse
            ghost
            className="fx-director-tech"
            items={[
              {
                key: 'tech',
                label: 'Chi tiết kỹ thuật',
                children: (
                  <div>
                    <p>Authority · {studioPublicHeadline(selected)}</p>
                    <p>currentIsAuthority / candidateIsAuthority · Master Revision {selected.masterRevision?.status || '—'}</p>
                    <p>Project Visual Style {selected.projectVisualStyleId || projectStyle?.id || '—'}</p>
                    <p>visualStyleGate {selected.visualStyleGate || (projectStyle?.ready ? 'VALID' : 'PROJECT_VISUAL_STYLE_NOT_READY')}</p>
                    <p>SHA / Integrity Details</p>
                    <ul className="fx-crp__checks">
                      {(
                        [
                          ['Master SHA', selected.masterSha256],
                          ['DNA SHA', selected.dnaSha256],
                          ['PRP SHA', selected.prpSha256],
                          ['CRP SHA', selected.crpSha256],
                          ['PVS SHA', selected.projectVisualStyleSha || projectStyle?.sha],
                          ['Identity SHA', selected.identitySha256],
                          ['Appearance SHA', selected.appearanceProfile?.profileSha],
                        ] as [string, string | null | undefined][]
                      ).map(([label, sha]) => (
                        <li key={label}>
                          {label} {sha || '—'}{' '}
                          {sha ? (
                            <Button
                              size="small"
                              type="link"
                              onClick={() => void navigator.clipboard.writeText(sha)}
                            >
                              Copy
                            </Button>
                          ) : null}
                        </li>
                      ))}
                    </ul>
                    <p>Generation</p>
                    <ul className="fx-crp__checks">
                      <li>Provider {selected.provider || '—'}</li>
                      <li>providerCalled {String(!!selected.providerCalled)}</li>
                      <li>generate {String(!!selected.generate)}</li>
                      <li>autoApproved {String(!!selected.autoApproved)}</li>
                      <li>autoLocked {String(!!selected.autoLocked)}</li>
                      <li>Version {selected.version || 'V1'}</li>
                      <li>Age Consistency {ageConsistencyLabel(selected.ageConsistencyStatus, selected.ageConsistencyScore)}</li>
                      <li>Appearance Consistency {appearanceConsistencyLabel(selected.appearanceConsistencyStatus)}</li>
                    </ul>
                    <p>Compilation</p>
                    <p>Tuổi nhân vật: {selected.age ?? '—'} tuổi</p>
                    <p>
                      Tuổi biểu hiện mục tiêu:{' '}
                      {selected.ageExpressionMinYears ?? (selected.age ? ageExpressionRange(selected.age).min : '—')}–
                      {selected.ageExpressionMaxYears ?? (selected.age ? ageExpressionRange(selected.age).max : '—')}
                    </p>
                    <p>Character Appearance</p>
                    <p>
                      Target Appearance:{' '}
                      {selected.appearanceProfile?.targetAppearanceAgeMin ??
                        selected.ageExpressionMinYears ??
                        (selected.age ? ageExpressionRange(selected.age).min : '—')}
                      –
                      {selected.appearanceProfile?.targetAppearanceAgeMax ??
                        selected.ageExpressionMaxYears ??
                        (selected.age ? ageExpressionRange(selected.age).max : '—')}
                    </p>
                    <p>Facial Maturity: {selected.appearanceProfile?.facialMaturityLabel || '—'}</p>
                    <p>Lifestyle: {selected.appearanceProfile?.lifestyleProfile || '—'}</p>
                    <p>Energy: {selected.appearanceProfile?.energyProfile || '—'}</p>
                    <p>Grooming: {selected.appearanceProfile?.groomingProfile || '—'}</p>
                    <p>Prompt / Generation Brief</p>
                    <p>{selected.description}</p>
                    <p>{selected.appearanceProfile?.lifestyleProfile}</p>
                    <p>{selected.appearanceProfile?.facialMaturityLabel}</p>
                    <p>{selected.ageProfile?.appearanceProfile}</p>
                    <p>Regression Details</p>
                    <p>
                      Studio V1 · Unified Generation · Age Consistency · Age Gate · Appearance Profile · Master
                      Revision · PVS V2 · TS Smoke · C# Build
                    </p>
                    <p className="fx-desk__note">
                      Live suites remain on existing GET regression endpoints. Opening this panel does not run them.
                    </p>
                    {selected.history?.length ? (
                      <ul className="fx-crp__checks">
                        {selected.history.map((h) => (
                          <li key={`${h.version}-${h.status}`}>
                            Generation {h.version} · {h.statusLabel || h.status}
                            {h.provider ? ` · ${h.provider}` : ''}
                          </li>
                        ))}
                      </ul>
                    ) : null}
                  </div>
                ),
              },
            ]}
          />
        </article>
      ) : null}

      {(masterComplete || bibleReady) && onBuildEpisode ? (
        <div className="fx-cstudio__sticky-cta">
          <Button type="primary" size="large" block onClick={goEpisode}>
            {bibleReady ? STUDIO_START_EPISODE : STUDIO_CONTINUE_EPISODE_MOBILE}
          </Button>
        </div>
      ) : null}

      <Modal
        title="Tạo nhân vật"
        open={createOpen}
        okText="Lưu hồ sơ"
        cancelText="Hủy"
        okButtonProps={{ disabled: !canCreate, loading: busy }}
        onCancel={() => setCreateOpen(false)}
        onOk={() => {
          run(
            createCharacterStudio({
              name,
              age,
              gender,
              role,
              personality,
              description,
              extraDescription,
              confirm: false,
              generate: false,
            }),
            'Không tạo được nhân vật.',
          );
          setCreateOpen(false);
        }}
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input placeholder="Tên nhân vật" value={name} onChange={(e) => setName(e.target.value)} />
          <InputNumber min={1} max={120} value={age} onChange={(v) => setAge(v ?? 11)} addonBefore="Tuổi" />
          <Select
            value={gender}
            options={STUDIO_GENDERS.map((g) => ({ value: g.id, label: g.label }))}
            onChange={setGender}
          />
          <Input placeholder="Vai trò" value={role} onChange={(e) => setRole(e.target.value)} />
          <Input placeholder="Tính cách" value={personality} onChange={(e) => setPersonality(e.target.value)} />
          <div className="fx-pvs__inherit">
            <p>PHONG CÁCH DỰ ÁN</p>
            <p>
              <b>{projectStyle?.styleName || '3D Stylized Realism'}</b>
            </p>
            <p>Nguồn: Project Visual Style Authority</p>
            <p>Status: INHERITED</p>
            <p className="fx-desk__note">
              Nhân vật sẽ được tạo theo phong cách hình ảnh đã thiết lập cho Project.
            </p>
          </div>
          <Input.TextArea
            rows={3}
            placeholder="Mô tả ngoại hình"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <Input.TextArea
            rows={2}
            placeholder="Mô tả bổ sung"
            value={extraDescription}
            onChange={(e) => setExtraDescription(e.target.value)}
          />
          <p className="fx-desk__note">Ảnh tham chiếu (tuỳ chọn)</p>
          <input type="file" accept="image/*" />
        </Space>
      </Modal>

      <Modal
        title={
          confirmMode === 'master-revision'
            ? 'Master Revision'
            : confirmMode === 'score-identity'
              ? STUDIO_SCORE_IDENTITY_LABEL
              : confirmMode === 'regenerate'
                ? 'Tạo lại bộ ảnh'
                : 'Tạo bộ ảnh chuẩn'
        }
        open={confirmOpen}
        okText={confirmMode === 'master-revision' ? 'Bắt đầu tạo Master Revision' : 'Xác nhận'}
        cancelText="Hủy"
        confirmLoading={busy}
        okButtonProps={{
          loading: busy,
          disabled: confirmMode !== 'master-revision' && confirmMode !== 'score-identity' && !projectStyleReady,
        }}
        onCancel={() => {
          setConfirmOpen(false);
          setConfirmError(undefined);
        }}
        onOk={() => {
          if (!selected) {
            setConfirmError('Chưa chọn nhân vật.');
            return Promise.reject(new Error('Chưa chọn nhân vật.'));
          }
          if (confirmMode === 'score-identity') {
            setConfirmError(undefined);
            setBusy(true);
            return scoreCharacterStudioIdentity(selected.characterId, { confirm: true })
              .then((row) => {
                if (row.gateCode === 'IDENTITY_SCORE_FAILED' || row.gateCode === 'IDENTITY_SCORE_NOT_READY') {
                  throw new Error(row.staffMessage || row.gateCode);
                }
                if (row.gateCode && studioSlotsNeedVision(row.slots, row.officialLocked)) {
                  throw new Error(row.staffMessage || 'Chấm chưa xong. Thử lại Chấm với Master — không tạo ảnh mới.');
                }
                setDetail(row);
                setOpenId(row.characterId);
                setItems((cur) => cur.map((x) => (x.characterId === row.characterId ? row : x)));
                setError(undefined);
                setConfirmError(undefined);
                setConfirmOpen(false);
              })
              .catch((e) => {
                const text = apiErrorMessage(e, 'Không chấm được với Master.');
                setConfirmError(text);
                setError(text);
                return Promise.reject(e);
              })
              .finally(() => setBusy(false));
          }
          if (confirmMode === 'master-revision') {
            setConfirmError(undefined);
            setBusy(true);
            return requestCharacterStudioMasterRevision(selected.characterId, {
              revisionReason: selected.masterRevision?.revisionReason || 'AGE_MISMATCH',
              revisionNotes:
                selected.masterRevision?.reviewNotes
                || 'Create a new Master Revision candidate. Keep current Master unchanged.',
              provider: 'GEMINI',
              confirm: true,
            })
              .then((result) => {
                if (result.gateCode && !result.generationExecuted) {
                  throw new Error(result.staffMessage || result.gateCode);
                }
                if (!result.character) throw new Error(result.staffMessage || 'Không tạo được Master Revision.');
                return result.character;
              })
              .then((row) => {
                setDetail(row);
                setOpenId(row.characterId);
                setItems((cur) => cur.map((x) => (x.characterId === row.characterId ? row : x)));
                setError(undefined);
                setConfirmError(undefined);
                setConfirmOpen(false);
              })
              .catch((e) => {
                const text = apiErrorMessage(e, 'Không tạo được Master Revision.');
                setConfirmError(text);
                setError(text);
                return Promise.reject(e);
              })
              .finally(() => setBusy(false));
          }
          setConfirmOpen(false);
          run(
            generateCharacterStudioReferenceSet(selected.characterId, {
              confirm: true,
              provider: 'GEMINI',
            }).then((result) => {
              if (result.gateCode && !result.providerCalled) {
                throw new Error(result.staffMessage || result.gateCode);
              }
              if (result.gateCode && (result.character?.coverage ?? 0) < 4) {
                throw new Error(result.staffMessage || result.gateCode);
              }
              if (result.character?.masterRevision?.crpStale) {
                throw new Error(
                  result.staffMessage
                  || 'Bộ 4 ảnh vẫn theo Master cũ. Tạo lại chưa gắn Master hiện tại.',
                );
              }
              return result.character;
            }),
            confirmMode === 'regenerate' ? 'Không tạo lại được.' : 'Không tạo được bộ ảnh chuẩn.',
          );
        }}
      >
        {confirmMode === 'score-identity' ? (
          <>
            {confirmError ? <Alert type="error" showIcon message={confirmError} style={{ marginBottom: 12 }} /> : null}
            <p>Chấm 4 ảnh hiện tại với Master.</p>
            <p>Không vẽ ảnh mới. Không tạo lại bộ ảnh.</p>
            <p className="fx-desk__note">
              Có thể mất khoảng 1 phút (Vision). Không duyệt và không khóa tự động.
            </p>
          </>
        ) : confirmMode === 'master-revision' ? (
          <>
            {confirmError ? <Alert type="error" showIcon message={confirmError} style={{ marginBottom: 12 }} /> : null}
            <p>Bạn đang tạo Master Revision mới.</p>
            <p>Master hiện tại sẽ được giữ nguyên.</p>
            <p>Một Master candidate mới sẽ được tạo để Director xem xét.</p>
            <p>Nhà cung cấp: Gemini</p>
            <p className="fx-desk__note">
              Bấm Bắt đầu tạo Master Revision sẽ gọi Gemini và tạo ảnh candidate.
              Không duyệt, không khóa, không tạo CRP tự động.
            </p>
          </>
        ) : (
          <>
            <p>Nhà cung cấp: Gemini</p>
            <p>Phong cách dự án: {selected?.projectVisualStyleName || projectStyle?.styleName || '—'}</p>
            <p className="fx-desk__note">
              {selected?.masterRevision?.crpStale
                ? 'Famixa sẽ bỏ 4 ảnh theo Master cũ và tạo lại turntable từ Master hiện tại: FRONT trước, rồi 3/4 · Nghiêng · Toàn thân khóa theo Master+FRONT. Mỗi góc được chấm với Master. Việc này có thể mất 2–4 phút. Không duyệt và không khóa tự động.'
                : 'Famixa sẽ tạo turntable studio: FRONT trước, rồi 3/4 · Nghiêng · Toàn thân khóa theo Master+FRONT. Mỗi góc được chấm với Master. Việc này có thể mất 2–4 phút. Không duyệt và không khóa tự động.'}
            </p>
          </>
        )}
      </Modal>

      <Modal
        title="Đánh giá bộ ảnh"
        open={rejectOpen}
        okText={rejectThenRegen ? 'Không đạt và tạo lại' : 'Xác nhận không đạt'}
        cancelText="Hủy"
        okButtonProps={{ disabled: !rejectReasonValid(rejectCode, rejectText) }}
        onCancel={() => {
          setRejectOpen(false);
          setRejectThenRegen(false);
        }}
        onOk={() => {
          if (!selected) return;
          const redo = rejectThenRegen;
          setRejectOpen(false);
          setRejectThenRegen(false);
          setBusy(true);
          void rejectCharacterStudio(selected.characterId, {
            rejectReasonCode: rejectCode,
            rejectReasonText: rejectText || STUDIO_REJECT_REASONS.find((r) => r.id === rejectCode)?.label || '',
          })
            .then((row) => {
              setDetail(row);
              setOpenId(row.characterId);
              setItems((cur) => {
                const next = cur.some((x) => x.characterId === row.characterId)
                  ? cur.map((x) => (x.characterId === row.characterId ? row : x))
                  : [row, ...cur];
                return next;
              });
              setError(undefined);
              if (redo) {
                setConfirmMode('regenerate');
                setConfirmOpen(true);
              }
            })
            .catch((e) => setError(apiErrorMessage(e, 'Không đánh giá được.')))
            .finally(() => setBusy(false));
        }}
      >
        <Radio.Group value={rejectCode} onChange={(e) => setRejectCode(e.target.value)}>
          {STUDIO_REJECT_REASONS.map((r) => (
            <Radio key={r.id} value={r.id} style={{ display: 'block', marginBottom: 6 }}>
              {r.label}
            </Radio>
          ))}
        </Radio.Group>
        <Input.TextArea
          rows={3}
          placeholder="Ghi chú"
          value={rejectText}
          onChange={(e) => setRejectText(e.target.value)}
          style={{ marginTop: 12 }}
        />
      </Modal>

      <Modal
        title="Duyệt Master Revision"
        open={reviewApproveOpen}
        okText="Duyệt Master"
        cancelText="Hủy"
        okButtonProps={{ loading: busy }}
        onCancel={() => setReviewApproveOpen(false)}
        onOk={() => {
          if (!selected) return;
          setReviewApproveOpen(false);
          run(
            approveCharacterStudioMasterRevision(selected.characterId).then(unwrapRevision),
            'Không duyệt được Master Revision.',
          );
        }}
      >
        <p>Bạn xác nhận Master Revision này đạt yêu cầu?</p>
        <p>
          Character: <b>{selected?.characterName || selected?.characterId}</b>
        </p>
        <p>
          Age: <b>{selected?.age ?? '—'}</b>
        </p>
        <p>
          Target appearance age: <b>{selected?.masterRevision?.ageTarget || '—'}</b>
        </p>
        <p>
          Visual style: <b>{selected?.projectVisualStyleName || projectStyle?.styleName || '—'}</b>
        </p>
        <p>
          Revision reason: <b>{selected?.masterRevision?.revisionReason || '—'}</b>
        </p>
        <p>
          Candidate SHA: <b>{selected?.masterRevision?.candidateMasterSha || '—'}</b>
        </p>
      </Modal>

      <Modal
        title="Master Revision không đạt"
        open={reviewRejectOpen}
        okText="Không đạt"
        cancelText="Hủy"
        okButtonProps={{ loading: busy, disabled: !revisionRejectCode }}
        onCancel={() => setReviewRejectOpen(false)}
        onOk={() => {
          if (!selected) return;
          const reason =
            MASTER_REVISION_REJECT_REASONS.find((r) => r.id === revisionRejectCode)?.label || revisionRejectCode;
          setReviewRejectOpen(false);
          run(
            rejectCharacterStudioMasterRevision(selected.characterId, {
              rejectionReason: reason,
              notes: revisionRejectNotes || undefined,
            }).then(unwrapRevision),
            'Không đánh giá được Master Revision.',
          );
        }}
      >
        <Radio.Group
          value={revisionRejectCode}
          onChange={(e) => setRevisionRejectCode(e.target.value)}
        >
          {MASTER_REVISION_REJECT_REASONS.map((r) => (
            <Radio key={r.id} value={r.id} style={{ display: 'block', marginBottom: 6 }}>
              {r.label}
            </Radio>
          ))}
        </Radio.Group>
        <Input.TextArea
          rows={3}
          placeholder="Ghi chú"
          value={revisionRejectNotes}
          onChange={(e) => setRevisionRejectNotes(e.target.value)}
          style={{ marginTop: 12 }}
        />
      </Modal>

      <Modal
        title="Khóa Master Revision"
        open={reviewLockOpen}
        okText="Khóa Master"
        cancelText="Hủy"
        okButtonProps={{ loading: busy }}
        onCancel={() => setReviewLockOpen(false)}
        onOk={() => {
          if (!selected) return;
          setReviewLockOpen(false);
          run(
            lockCharacterStudioMasterRevision(selected.characterId).then(unwrapRevision),
            'Không khóa được Master Revision.',
          );
        }}
      >
        <p>Khóa Master Revision này làm Authority mới?</p>
        <p>
          {selected?.masterRevision?.currentMasterSha || selected?.masterSha256 || '—'}
          {' → '}
          {selected?.masterRevision?.candidateMasterSha || '—'}
        </p>
        <p className="fx-desk__note">
          DNA / PRP / CRP hiện tại phụ thuộc Master cũ sẽ trở thành STALE.
        </p>
      </Modal>
    </section>
  );
}
