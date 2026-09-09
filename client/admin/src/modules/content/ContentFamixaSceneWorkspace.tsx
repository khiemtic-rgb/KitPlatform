import { useEffect, useMemo, useState } from 'react';
import { Button, Drawer, Input, Select } from 'antd';
import { fetchCharacterLibrary, type CharacterLibraryViewRow } from '@/shared/api/content.api';
import type { FamixaCharacter, FamixaSceneNode, FamixaSeriesShot, SeriesShotRun } from './content-famixa-series';
import {
  SceneCharactersPanel,
  SceneFinishPanel,
  SceneImagePanel,
  SceneOverviewPanel,
  SceneReviewPanel,
  SceneScriptPanel,
  SceneVideoPanel,
  ShotDetailBody,
} from './ContentFamixaScenePanels';
import {
  SCENE_SHOT_FILTERS,
  SCENE_WORK_STEPS,
  buildSceneViews,
  displayPersonName,
  filterSceneShots,
  formatClock,
  locationLabel,
  nextSceneAction,
  pageSlice,
  sceneStepMarks,
  type ProductionUserModeLike,
  type SceneShotFilter,
  type SceneWorkStep,
} from './kit-video-scene-workspace';

const PAGE = 20;

export function ContentFamixaSceneWorkspace({
  shots,
  runOf,
  scenes,
  characters,
  scriptLocked,
  mode,
  onEditScript,
  onSaveCharacters,
  onCreateImage,
  creating,
}: {
  shots: FamixaSeriesShot[];
  runOf: (id: string) => SeriesShotRun;
  scenes?: FamixaSceneNode[];
  characters?: FamixaCharacter[];
  scriptLocked?: boolean;
  mode?: ProductionUserModeLike;
  onEditScript?: () => void;
  onSaveCharacters?: (sceneId: string, characterIds: string[]) => void;
  onCreateImage?: (shotId: string) => void;
  creating?: boolean;
}) {
  const [library, setLibrary] = useState<CharacterLibraryViewRow[]>([]);
  const [openSceneId, setOpenSceneId] = useState<string>();
  const [step, setStep] = useState<SceneWorkStep>('overview');
  const [filter, setFilter] = useState<SceneShotFilter>('all');
  const [query, setQuery] = useState('');
  const [castFilter, setCastFilter] = useState<string>();
  const [page, setPage] = useState(0);
  const [openShotId, setOpenShotId] = useState<string>();

  useEffect(() => {
    void fetchCharacterLibrary().then((row) => setLibrary(row.items ?? [])).catch(() => setLibrary([]));
  }, []);

  const views = useMemo(() => buildSceneViews(shots, runOf, scenes), [shots, runOf, scenes]);
  const current = views.find((s) => s.id === openSceneId);
  const marks = current ? sceneStepMarks(current, library, scriptLocked) : [];
  const next = current ? nextSceneAction(current, marks, library) : undefined;
  const filtered = current ? filterSceneShots(current.shots, filter, query, castFilter) : [];
  const visible = pageSlice(filtered, page, PAGE);
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE));
  const openShot = current?.shots.find((row) => row.shot.id === openShotId);
  const director = mode === 'director';

  const goNext = (nextStep?: SceneWorkStep, shotId?: string) => {
    if (nextStep) setStep(nextStep);
    if (shotId) setOpenShotId(shotId);
  };

  if (!views.length) {
    return (
      <section className="fx-scw">
        <h2>Cảnh</h2>
        <article className="fx-media-card">
          <h3>Cảnh chưa được chia shot</h3>
          <p>Hãy chia kịch bản thành các shot để bắt đầu sản xuất.</p>
          {onEditScript ? <Button onClick={onEditScript}>Chia shot</Button> : null}
        </article>
      </section>
    );
  }

  if (!current) {
    return (
      <section className="fx-scw">
        <div className="fx-scw__layout">
          <aside className="fx-scw__list">
            <h2>Cảnh</h2>
            <p className="fx-desk__lead">{views.length} cảnh</p>
            {views.map((scene) => (
              <button key={scene.id} type="button" className="fx-scw__scene" onClick={() => setOpenSceneId(scene.id)}>
                <strong>
                  {String(scene.number).padStart(2, '0')} {scene.title}
                </strong>
                <span>
                  {formatClock(scene.seconds)} · {scene.shots.length} shots · {scene.characterIds.length} nhân vật
                </span>
                <span>
                  {scene.status === 'done' ? '✓' : scene.status === 'not_started' ? '○' : '●'} {scene.statusLabel}
                </span>
              </button>
            ))}
          </aside>
          <div className="fx-scw__empty">
            <p>Chọn một cảnh để xem kịch bản, shot, nhân vật và việc tiếp theo.</p>
          </div>
        </div>
      </section>
    );
  }

  const beatsOnPage = current.beats
    .map((beat) => ({ ...beat, shots: beat.shots.filter((row) => visible.some((v) => v.shot.id === row.shot.id)) }))
    .filter((beat) => beat.shots.length);

  return (
    <section className="fx-scw">
      <div className="fx-scw__layout">
        <aside className="fx-scw__list">
          <h2>Cảnh</h2>
          {views.map((scene) => (
            <button
              key={scene.id}
              type="button"
              className={`fx-scw__scene${scene.id === current.id ? ' is-on' : ''}`}
              onClick={() => {
                setOpenSceneId(scene.id);
                setStep('overview');
                setPage(0);
              }}
            >
              <strong>
                {String(scene.number).padStart(2, '0')} {scene.title}
              </strong>
              <span>
                {formatClock(scene.seconds)} · {scene.shots.length} shots
              </span>
              <span>{scene.statusLabel}</span>
            </button>
          ))}
        </aside>

        <div className="fx-scw__main">
          <Button type="link" onClick={() => setOpenSceneId(undefined)}>
            ← Quay lại danh sách cảnh
          </Button>
          <header className="fx-scw__head">
            <p className="fx-prod__kicker">Cảnh {String(current.number).padStart(2, '0')}</p>
            <h2>{current.title}</h2>
            <p>
              {current.seconds} giây · {current.shots.length} shots · {current.characterIds.length} nhân vật · {current.statusLabel}
            </p>
            <p>{current.goal}</p>
            <p>
              Nhân vật:{' '}
              {current.characterIds.map((id) => displayPersonName(id, characters, library)).join(', ') || '—'}
            </p>
            <p>Bối cảnh: {locationLabel(current.location) || 'Chưa có bối cảnh'}</p>
          </header>

          <div className="fx-scw__progress">
            {marks.map((m) => (
              <span key={m.id} className={m.done ? 'is-on' : ''}>
                {m.done ? '●' : '○'} {m.label}
              </span>
            ))}
            <strong>
              {marks.filter((m) => m.done).length}/{marks.length}
            </strong>
          </div>

          {next ? (
            <p className="fx-scw__next">
              <strong>Việc tiếp theo:</strong> {next.label}{' '}
              <Button size="small" type="primary" onClick={() => goNext(next.step, next.shotId)}>
                Tiếp tục
              </Button>
            </p>
          ) : null}

          <div className="fx-scw__steps">
            {SCENE_WORK_STEPS.map((item) => (
              <button key={item.id} type="button" className={step === item.id ? 'is-on' : ''} onClick={() => setStep(item.id)}>
                {item.label}
              </button>
            ))}
          </div>

          {step === 'overview' && next ? (
            <SceneOverviewPanel scene={current} marks={marks} next={next} onContinue={() => goNext(next.step, next.shotId)} />
          ) : null}
          {step === 'script' ? <SceneScriptPanel scene={current} onEdit={onEditScript} /> : null}
          {step === 'cast' ? (
            <SceneCharactersPanel
              scene={current}
              people={characters}
              library={library}
              onSaveCharacters={onSaveCharacters}
            />
          ) : null}
          {step === 'image' ? (
            <SceneImagePanel
              shots={current.shots}
              runOf={runOf}
              creating={creating}
              onOpen={setOpenShotId}
              onCreate={onCreateImage}
            />
          ) : null}
          {step === 'video' ? <SceneVideoPanel shots={current.shots} onOpen={setOpenShotId} /> : null}
          {step === 'review' ? <SceneReviewPanel scene={current} director={director} onOpen={setOpenShotId} /> : null}
          {step === 'finish' && next ? (
            <SceneFinishPanel marks={marks} next={next} onOpen={goNext} />
          ) : null}

          {step === 'shots' || step === 'overview' ? (
            <section className="fx-scw__board">
              <h3>
                {current.title} — {current.shots.length} shots
              </h3>
              {!current.shots.length ? (
                <article className="fx-media-card">
                  <h3>Cảnh chưa được chia shot</h3>
                  <p>Hãy chia kịch bản thành các shot để bắt đầu sản xuất.</p>
                </article>
              ) : null}
              <div className="fx-scw__tools">
                {SCENE_SHOT_FILTERS.map((item) => (
                  <Button
                    key={item.id}
                    size="small"
                    type={filter === item.id ? 'primary' : 'default'}
                    onClick={() => {
                      setFilter(item.id);
                      setPage(0);
                    }}
                  >
                    {item.label}
                  </Button>
                ))}
                <Input
                  allowClear
                  placeholder="Tìm shot..."
                  value={query}
                  onChange={(e) => {
                    setQuery(e.target.value);
                    setPage(0);
                  }}
                />
                <Select
                  allowClear
                  placeholder="Nhân vật"
                  style={{ width: 160 }}
                  value={castFilter}
                  onChange={(v) => {
                    setCastFilter(v);
                    setPage(0);
                  }}
                  options={current.characterIds.map((id) => ({
                    value: id,
                    label: displayPersonName(id, characters, library),
                  }))}
                />
              </div>
              {beatsOnPage.map((beat) => (
                <div key={beat.id} className="fx-scw__beat">
                  <h4>{beat.label}</h4>
                  <div className="fx-scw__shots">
                    {beat.shots.map((row) => (
                      <button key={row.shot.id} type="button" className="fx-scw__shot" onClick={() => setOpenShotId(row.shot.id)}>
                        <strong>Shot {String(row.index + 1).padStart(2, '0')}</strong>
                        <span>{row.shot.story || '—'}</span>
                        <span>{row.shot.seconds || 5}s</span>
                        <span>
                          {row.media.hasStill ? '● Đã có hình' : '⚠ Chờ tạo hình'}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>
              ))}
              {filtered.length > PAGE ? (
                <div className="fx-scw__pager">
                  <Button size="small" disabled={page <= 0} onClick={() => setPage((p) => p - 1)}>
                    Trước
                  </Button>
                  <span>
                    {page + 1} / {pages}
                  </span>
                  <Button size="small" disabled={page + 1 >= pages} onClick={() => setPage((p) => p + 1)}>
                    Sau
                  </Button>
                </div>
              ) : null}
            </section>
          ) : null}
        </div>
      </div>

      <Drawer title="Shot" open={Boolean(openShot)} onClose={() => setOpenShotId(undefined)} width={440}>
        {openShot ? (
          <ShotDetailBody
            row={openShot}
            run={runOf(openShot.shot.id)}
            people={characters}
            library={library}
            director={director}
          />
        ) : null}
      </Drawer>
    </section>
  );
}
