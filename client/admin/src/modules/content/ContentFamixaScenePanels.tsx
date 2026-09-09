import { useEffect, useState } from 'react';
import { Button, Collapse, Modal } from 'antd';
import { fetchCharacterStudioList, type CharacterLibraryViewRow, type CharacterStudioRow } from '@/shared/api/content.api';
import { ContentFamixaApprovalPanel } from './ContentFamixaApprovalPanel';
import { ContentFamixaCharacterLibrary, useFrontThumbs } from './ContentFamixaCharacterLibrary';
import type { FamixaCharacter, SeriesShotRun } from './content-famixa-series';
import { shotCharacterIds } from './content-famixa-series';
import { clipOf, stillOf } from './content-famixa-shot-catalog';
import {
  displayPersonName,
  formatClock,
  locationLabel,
  sceneActionsFromMedia,
  sceneCastCopy,
  staffErrorCopy,
  type SceneNextAction,
  type SceneStepMark,
  type SceneView,
  type SceneWorkStep,
  type SceneShotView,
} from './kit-video-scene-workspace';

export function SceneScriptPanel({ scene, onEdit }: { scene: SceneView; onEdit?: () => void }) {
  const beats = scene.goal
    .split(/\n+/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 12);
  return (
    <section className="fx-scw__panel">
      <h3>Kịch bản cảnh</h3>
      <p>
        <strong>Mục tiêu:</strong> {scene.goal || scene.title}
      </p>
      {beats.length ? (
        <ol>
          {beats.map((line, i) => (
            <li key={`${scene.id}-b-${i}`}>{line}</li>
          ))}
        </ol>
      ) : null}
      {scene.emotion ? (
        <p>
          <strong>Cảm xúc:</strong> {scene.emotion}
        </p>
      ) : null}
      <p>
        <strong>Thời lượng dự kiến:</strong> {scene.seconds}s
      </p>
      {onEdit ? <Button onClick={onEdit}>Sửa kịch bản</Button> : <p className="fx-desk__note">Kịch bản đang xem ở chế độ đọc.</p>}
    </section>
  );
}

export function SceneCharactersPanel({
  scene,
  people,
  library,
  onSaveCharacters,
}: {
  scene: SceneView;
  people?: FamixaCharacter[];
  library?: CharacterLibraryViewRow[];
  onSaveCharacters?: (sceneId: string, characterIds: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [picked, setPicked] = useState<string[]>(scene.characterIds);
  const [error, setError] = useState<string>();
  const [studio, setStudio] = useState<CharacterStudioRow[]>([]);
  useEffect(() => {
    setPicked(scene.characterIds);
  }, [scene.id, scene.characterIds.join('|')]);
  useEffect(() => {
    void fetchCharacterStudioList()
      .then((row) => setStudio(row.items ?? []))
      .catch(() => setStudio([]));
  }, []);
  const known = new Set((library ?? []).map((row) => row.characterId.toUpperCase()));
  const thumbs = useFrontThumbs(
    picked.map((id) => {
      const row = (library ?? []).find((item) => item.characterId.toUpperCase() === id.toUpperCase());
      return row ?? ({ characterId: id, frontPackId: null, frontItemId: null, masterLocked: false } as CharacterLibraryViewRow);
    }),
    studio,
  );
  const save = (ids: string[]) => {
    const invalid = ids.find((id) => !known.has(id.toUpperCase()) && !(people ?? []).some((p) => p.id.toUpperCase() === id.toUpperCase()));
    if (invalid) {
      setError('Không thể sử dụng nhân vật này');
      return;
    }
    setPicked(ids);
    setError(undefined);
    onSaveCharacters?.(scene.id, ids);
  };
  return (
    <section className="fx-scw__panel">
      <h3>Nhân vật trong cảnh</h3>
      <p className="fx-desk__note">
        Ai có mặt ở cảnh này. Ảnh đại diện = góc TRƯỚC MẶT từ kho, hoặc Master đã khóa ở Character Studio — không lấy ảnh shot.
      </p>
      {!picked.length ? <p className="fx-desk__note">Cảnh này chưa gắn nhân vật.</p> : null}
      <div className="fx-scw__cast">
        {picked.map((id) => {
          const row = library?.find((item) => item.characterId.toUpperCase() === id.toUpperCase());
          const locked = studio.find((item) => item.characterId.toUpperCase() === id.toUpperCase());
          const copy = sceneCastCopy(row, locked);
          const thumb = thumbs[row?.characterId || ''] || thumbs[id];
          return (
            <article key={id} className="fx-scw__cast-card">
              {thumb ? <img src={thumb} alt="" className="fx-scw__cast-face" /> : <div className="fx-scw__cast-face fx-scw__cast-face--empty" />}
              <strong>{displayPersonName(id, people, library)}</strong>
              <p>{id}</p>
              <p>{copy.mark}</p>
              {copy.reason ? <p>{copy.reason}</p> : null}
              <Button size="small" onClick={() => save(picked.filter((x) => x.toUpperCase() !== id.toUpperCase()))}>
                Bỏ
              </Button>
            </article>
          );
        })}
      </div>
      {error ? <p className="fx-desk__note">{error}</p> : null}
      <div className="fx-desk__btns">
        <Button onClick={() => setOpen(true)}>Chọn nhân vật</Button>
        <Button type="primary" onClick={() => save(picked)}>
          Lưu nhân vật
        </Button>
      </div>
      <Modal
        title="Chọn nhân vật"
        open={open}
        onCancel={() => setOpen(false)}
        footer={<Button onClick={() => setOpen(false)}>Đóng</Button>}
        width={720}
      >
        <ContentFamixaCharacterLibrary
          pickerOnly
          onPick={(id, canUse) => {
            if (!id) return;
            if (!known.has(id.toUpperCase()) && !(people ?? []).some((p) => p.id.toUpperCase() === id.toUpperCase())) {
              setError('Không thể sử dụng nhân vật này');
              return;
            }
            if (!canUse) setError('Không thể sử dụng nhân vật này cho tạo hình. Vẫn có thể gắn vào cảnh.');
            else setError(undefined);
            const next = picked.some((x) => x.toUpperCase() === id.toUpperCase()) ? picked : [...picked, id];
            setPicked(next);
            setOpen(false);
          }}
        />
      </Modal>
    </section>
  );
}

export function SceneImagePanel({
  shots,
  runOf,
  creating,
  onOpen,
  onCreate,
}: {
  shots: SceneShotView[];
  runOf: (id: string) => SeriesShotRun;
  creating?: boolean;
  onOpen: (id: string) => void;
  onCreate?: (id: string) => void;
}) {
  return (
    <section className="fx-scw__panel">
      <h3>Tạo hình</h3>
      <div className="fx-scw__media-list">
        {shots.map((row) => {
          const still = stillOf(runOf(row.shot.id), row.shot.story);
          const actions = sceneActionsFromMedia(row.media);
          return (
            <article key={row.shot.id} className="fx-scw__media-row">
              {still ? <img src={still} alt="" /> : <div className="fx-scw__ph" />}
              <div>
                <strong>Shot {String(row.index + 1).padStart(2, '0')}</strong>
                <p>{row.shot.story || row.statusLabel}</p>
                <p>{row.media.hasStill ? '✓ Đã có hình' : '⚠ Chưa có hình'}</p>
                <Button size="small" onClick={() => onOpen(row.shot.id)}>
                  Xem
                </Button>
                <Button
                  size="small"
                  type={!still ? 'primary' : 'default'}
                  loading={creating}
                  disabled={!onCreate}
                  onClick={() => onCreate?.(row.shot.id)}
                >
                  Tạo hình
                </Button>
                <Button size="small" disabled={!actions.canApproveImage} onClick={() => onOpen(row.shot.id)}>
                  Duyệt
                </Button>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}

export function SceneVideoPanel({
  shots,
  onOpen,
}: {
  shots: SceneShotView[];
  onOpen: (id: string) => void;
}) {
  return (
    <section className="fx-scw__panel">
      <h3>Dựng video</h3>
      <div className="fx-scw__media-list">
        {shots.map((row) => {
          const locked = !row.media.hasStill;
          return (
            <article key={row.shot.id} className="fx-scw__media-row">
              <div>
                <strong>Shot {String(row.index + 1).padStart(2, '0')}</strong>
                <p>
                  Hình {row.media.hasStill ? '✓' : '⚠'} · Video {row.media.hasClip ? '✓' : locked ? '🔒' : '○'}
                </p>
                {locked ? <p>Video bị khóa. Cần duyệt hình trước.</p> : null}
                <Button size="small" onClick={() => onOpen(row.shot.id)}>
                  {locked ? 'Xem lý do' : 'Xem'}
                </Button>
                <Button size="small" disabled>
                  Chuẩn bị video
                </Button>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}

export function SceneReviewPanel({
  scene,
  director,
  onOpen,
}: {
  scene: SceneView;
  director?: boolean;
  onOpen: (id: string) => void;
}) {
  const waiting = scene.shots.filter((s) => s.status === 'review' || s.status === 'fix');
  const done = scene.shots.filter((s) => s.status === 'done').length;
  const canApproveScene = scene.shots.length > 0 && scene.shots.every((s) => s.status === 'done');
  return (
    <section className="fx-scw__panel">
      <h3>{director ? 'Chờ duyệt' : 'Duyệt cảnh'}</h3>
      <p>
        {scene.shots.length} shots · ✓ {done} đã duyệt · ⚠ {waiting.filter((s) => s.status === 'review').length} chờ
        duyệt · ✕ {waiting.filter((s) => s.status === 'fix').length} cần sửa
      </p>
      {waiting.map((row) => (
        <article key={row.shot.id} className="fx-scw__todo">
          <span>
            Shot {String(row.index + 1).padStart(2, '0')} — {row.statusLabel}
          </span>
          <Button size="small" onClick={() => onOpen(row.shot.id)}>
            Xem
          </Button>
        </article>
      ))}
      <Button type="primary" disabled={!canApproveScene}>
        Duyệt cảnh
      </Button>
    </section>
  );
}

export function SceneFinishPanel({
  marks,
  next,
  onOpen,
}: {
  marks: SceneStepMark[];
  next: SceneNextAction;
  onOpen: (step: SceneWorkStep, shotId?: string) => void;
}) {
  const leftover = marks.filter((m) => !m.done && m.id !== 'overview');
  return (
    <section className="fx-scw__panel">
      <h3>Hoàn thiện cảnh</h3>
      <ul className="fx-scw__checks">
        {marks
          .filter((m) => m.id !== 'overview')
          .map((m) => (
            <li key={m.id}>
              {m.done ? '✓' : '○'} {m.label} {m.detail}
            </li>
          ))}
      </ul>
      {leftover.length === 0 ? (
        <p>Cảnh đã hoàn thành.</p>
      ) : (
        <>
          <p>Cảnh chưa thể hoàn thành. Còn {leftover.length} việc cần xử lý.</p>
          {leftover.map((m) => (
            <Button key={m.id} type="link" onClick={() => onOpen(m.id)}>
              {m.label}
            </Button>
          ))}
        </>
      )}
      <Button type="primary" onClick={() => onOpen(next.step, next.shotId)}>
        Tiếp tục
      </Button>
    </section>
  );
}

export function SceneOverviewPanel({
  scene,
  marks,
  next,
  onContinue,
}: {
  scene: SceneView;
  marks: SceneStepMark[];
  next: SceneNextAction;
  onContinue: () => void;
}) {
  const done = marks.filter((m) => m.done).length;
  return (
    <section className="fx-scw__panel">
      <h3>
        Cảnh {String(scene.number).padStart(2, '0')}
      </h3>
      <p>
        {scene.title} · {scene.seconds}s · {scene.shots.length} shots
      </p>
      <ul className="fx-scw__checks">
        {marks
          .filter((m) => m.id !== 'overview')
          .map((m) => (
            <li key={m.id}>
              {m.done ? '✓' : '●'} {m.label} {m.detail}
            </li>
          ))}
      </ul>
      <p>
        Tiến độ {done}/{marks.length}
      </p>
      <p className="fx-scw__next">
        <strong>Việc tiếp theo:</strong> {next.label}
      </p>
      <Button type="primary" onClick={onContinue}>
        Tiếp tục công việc
      </Button>
    </section>
  );
}

export function ShotDetailBody({
  row,
  run,
  people,
  library,
  director,
}: {
  row: SceneShotView;
  run: SeriesShotRun;
  people?: FamixaCharacter[];
  library?: CharacterLibraryViewRow[];
  director?: boolean;
}) {
  const still = stillOf(run, row.shot.story);
  const clip = clipOf(run, row.shot.story);
  const actions = sceneActionsFromMedia(row.media);
  const err = staffErrorCopy(run.turboError);
  const names = shotCharacterIds(row.shot).map((id) => displayPersonName(id, people, library));
  const place = locationLabel(row.shot.location);
  return (
    <div className="fx-scw__drawer">
      <p className="fx-prod__kicker">Shot {String(row.index + 1).padStart(2, '0')}</p>
      <h2>{row.shot.story || row.shot.shot}</h2>
      <p>
        {formatClock(row.shot.editSeconds || row.shot.seconds || 0)} · {row.statusLabel}
      </p>
      <p>
        <strong>Mô tả:</strong> {row.shot.story || row.shot.visual || '—'}
      </p>
      <p>
        <strong>Nhân vật:</strong> {names.join(', ') || '—'}
      </p>
      <p>
        <strong>Bối cảnh:</strong> {place || 'Chưa có bối cảnh'}
      </p>
      <p>
        <strong>Camera:</strong> {row.shot.visual || '—'}
      </p>
      <p>
        <strong>Chuyển động:</strong> {row.shot.motionPromptVi || row.shot.motionPrompt || '—'}
      </p>
      <h4>Tạo hình</h4>
      {still ? <img src={still} alt="" className="fx-approve__media" /> : <p>⚠ Chưa có</p>}
      <h4>Video</h4>
      {clip ? <video src={clip} controls playsInline preload="metadata" className="fx-approve__media" /> : <p>○ Chưa thực hiện</p>}
      {!row.media.hasStill ? (
        <p className="fx-desk__note">
          {err.title}. {err.hint}
        </p>
      ) : null}
      {director && still ? <ContentFamixaApprovalPanel kind="image" onPass={() => undefined} onFail={() => undefined} /> : null}
      {director && clip ? <ContentFamixaApprovalPanel kind="video" onPass={() => undefined} onFail={() => undefined} /> : null}
      <p className="fx-desk__note">
        {actions.canApproveImage ? 'Có thể xem và duyệt hình nếu backend cho phép.' : 'Chưa có hình để duyệt.'}
      </p>
      <Collapse
        items={[
          {
            key: 'tech',
            label: 'Chi tiết sản xuất',
            children: (
              <pre className="fx-scw__tech">
                {JSON.stringify(
                  {
                    shotId: row.shot.id,
                    sceneId: row.shot.sceneId,
                    beatId: row.shot.beatId,
                    status: row.shot.status,
                    run: run.status,
                    generate: false,
                  },
                  null,
                  2,
                )}
              </pre>
            ),
          },
        ]}
      />
    </div>
  );
}

