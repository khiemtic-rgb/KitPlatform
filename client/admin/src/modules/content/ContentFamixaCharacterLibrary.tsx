import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Collapse, Input, Modal, Select, Skeleton } from 'antd';
import {
  characterStudioMasterImageUrl,
  characterStudioReferenceImageUrl,
  fetchCharacterLibrary,
  fetchCharacterLibraryDetail,
  fetchCharacterReferenceItemObjectUrl,
  fetchCharacterStudioImageObjectUrl,
  type CharacterLibraryDetailRow,
  type CharacterLibraryViewRow,
  type CharacterStudioRow,
} from '@/shared/api/content.api';
import { sceneCastFaceSource } from './kit-video-scene-workspace';
import { apiErrorMessage } from '@/shared/api/api-error';
import { ContentFamixaCharacterAuthorityCard } from './ContentFamixaCharacterAuthorityCard';
import { ContentKitVideoCharacterReferencePackCard } from './ContentKitVideoCharacterReferencePackCard';
import {
  LIBRARY_FILTERS,
  LIBRARY_SORTS,
  coverageLine,
  pageSlice,
  viewSlotLabel,
} from './kit-video-character-library';
import { staffReadinessCopy } from './kit-video-production-workflow';

const PAGE = 20;

export function useFrontThumbs(rows: CharacterLibraryViewRow[], studio: CharacterStudioRow[] = []) {
  const [urls, setUrls] = useState<Record<string, string>>({});
  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    const studioById = new Map((studio ?? []).map((row) => [row.characterId.toUpperCase(), row]));
    void Promise.all(
      rows.map(async (row) => {
        const st = studioById.get(row.characterId.toUpperCase());
        const source = sceneCastFaceSource(row, st);
        if (!source) return undefined;
        try {
          const url =
            source === 'crp'
              ? await fetchCharacterReferenceItemObjectUrl(row.characterId, row.frontPackId!, row.frontItemId!)
              : await fetchCharacterStudioImageObjectUrl(
                  source === 'studio-front'
                    ? characterStudioReferenceImageUrl(
                        row.characterId,
                        'FRONT',
                        st?.slots?.find((s) => s.type.toUpperCase() === 'FRONT')?.sha256,
                      )
                    : characterStudioMasterImageUrl(row.characterId, st?.masterSha256),
                );
          made.push(url);
          return [row.characterId, url] as const;
        } catch {
          return undefined;
        }
      }),
    ).then((pairs) => {
      if (dead) {
        made.forEach((url) => URL.revokeObjectURL(url));
        return;
      }
      const next: Record<string, string> = {};
      for (const pair of pairs) {
        if (pair) next[pair[0]] = pair[1];
      }
      setUrls(next);
    });
    return () => {
      dead = true;
      made.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [
    rows.map((r) => `${r.characterId}:${r.frontItemId ?? ''}:${r.masterLocked ? '1' : '0'}`).join('|'),
    studio
      .map((s) =>
        `${s.characterId}:${s.officialLocked ? '1' : '0'}:${s.masterSha256 ?? ''}:${s.slots?.find((slot) => slot.type.toUpperCase() === 'FRONT')?.sha256 ?? ''}`,
      )
      .join('|'),
  ]);
  return urls;
}

export function ContentFamixaCharacterLibrary({
  shots,
  pickerOnly,
  onPick,
}: {
  shots?: { characterIds?: string[]; characters?: string[] }[];
  pickerOnly?: boolean;
  onPick?: (characterId: string, canUse: boolean) => void;
}) {
  const [items, setItems] = useState<CharacterLibraryViewRow[]>([]);
  const [total, setTotal] = useState(0);
  const [q, setQ] = useState('');
  const [filter, setFilter] = useState('all');
  const [sort, setSort] = useState('series');
  const [page, setPage] = useState(0);
  const [openId, setOpenId] = useState<string>();
  const [detail, setDetail] = useState<CharacterLibraryDetailRow>();
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string>();
  const [pickerOpen, setPickerOpen] = useState(false);

  const load = (nextQ = q, nextFilter = filter, nextSort = sort) => {
    setBusy(true);
    void fetchCharacterLibrary({ q: nextQ || undefined, filter: nextFilter, sort: nextSort })
      .then((row) => {
        setItems(row.items ?? []);
        setTotal(row.total ?? 0);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không thể tải danh sách nhân vật.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!openId) {
      setDetail(undefined);
      return;
    }
    void fetchCharacterLibraryDetail(openId)
      .then(setDetail)
      .catch((e) => setError(apiErrorMessage(e, 'Không thể tải nhân vật.')));
  }, [openId]);

  const ordered = useMemo(() => {
    if (!pickerOnly) return items;
    return [...items].sort((a, b) => Number(b.canUse) - Number(a.canUse));
  }, [items, pickerOnly]);
  const pageRows = useMemo(() => pageSlice(ordered, page, PAGE), [ordered, page]);
  const thumbs = useFrontThumbs(pageRows);
  const pages = Math.max(1, Math.ceil(ordered.length / PAGE));

  const buildCount = (code: string) => {
    if (!shots?.length) return undefined;
    return shots.filter((shot) => {
      const ids = shot.characterIds?.length ? shot.characterIds : shot.characters ?? [];
      return ids.some((id) => id.toUpperCase() === code.toUpperCase());
    }).length;
  };

  const open = (row: CharacterLibraryViewRow) => {
    if (pickerOnly) {
      onPick?.(row.characterId, row.canUse);
      return;
    }
    setOpenId(row.characterId);
  };

  return (
    <section className="fx-clib">
      {pickerOnly ? null : (
        <header className="fx-clib__head">
          <h2>Nhân vật</h2>
          <p className="fx-desk__lead">Quản lý nhân vật và bộ ảnh chuẩn dùng trong các cảnh.</p>
        </header>
      )}
      <div className="fx-clib__tools">
        <Input
          allowClear
          placeholder="Tìm nhân vật..."
          value={q}
          onChange={(e) => {
            setQ(e.target.value);
            setPage(0);
            load(e.target.value, filter, sort);
          }}
        />
        <div className="fx-clib__filters">
          {LIBRARY_FILTERS.map((item) => (
            <Button
              key={item.id}
              size="small"
              type={filter === item.id ? 'primary' : 'default'}
              onClick={() => {
                setFilter(item.id);
                setPage(0);
                load(q, item.id, sort);
              }}
            >
              {item.label}
            </Button>
          ))}
        </div>
        <Select
          size="small"
          value={sort}
          style={{ width: 160 }}
          options={LIBRARY_SORTS.map((item) => ({ value: item.id, label: item.label }))}
          onChange={(value) => {
            setSort(value);
            load(q, filter, value);
          }}
        />
        {pickerOnly ? null : (
          <Button onClick={() => setPickerOpen(true)}>Chọn nhân vật</Button>
        )}
      </div>
      {error ? (
        <Alert
          type="error"
          showIcon
          message={error}
          action={
            <Button size="small" onClick={() => load()}>
              Thử lại
            </Button>
          }
        />
      ) : null}
      {busy && !items.length ? (
        <div className="fx-clib__grid">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} active avatar paragraph={{ rows: 2 }} />
          ))}
        </div>
      ) : null}
      {!busy && !items.length && !error ? (
        <p className="fx-desk__note">Chưa có nhân vật được thiết lập.</p>
      ) : null}
      <div className="fx-clib__grid">
        {pageRows.map((row) => {
          const build = buildCount(row.characterId);
          const staff = staffReadinessCopy(row);
          return (
            <article key={row.characterId} className={`fx-clib__card${openId === row.characterId ? ' is-on' : ''}`}>
              {thumbs[row.characterId] ? (
                <img src={thumbs[row.characterId]} alt="" />
              ) : (
                <div className="fx-clib__ph" />
              )}
              <div>
                <strong>{row.displayName || row.name}</strong>
                <p>{row.characterId}</p>
                <p>Bộ ảnh chuẩn {coverageLine(row.requiredReady, row.requiredTotal)}</p>
                <p>{staff.badge}</p>
                <p>{staff.detail}</p>
                <p>
                  Đang xuất hiện: {row.sceneCount} cảnh
                  {typeof build === 'number' ? ` · bản dựng ${build}` : ''}
                </p>
                {pickerOnly && !row.canUse ? <p>Không thể sử dụng nhân vật này. {row.readinessReason}</p> : null}
                <Button size="small" type="primary" onClick={() => open(row)}>
                  {pickerOnly ? 'Chọn nhân vật' : row.canUse ? 'XEM BỘ ẢNH' : 'HOÀN THIỆN BỘ ẢNH'}
                </Button>
              </div>
            </article>
          );
        })}
      </div>
      {items.length > PAGE ? (
        <div className="fx-clib__pager">
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

      {openId && detail && !pickerOnly ? (
        <article className="fx-clib__detail">
          <h3>{detail.character.displayName || detail.character.name}</h3>
          <p>{detail.character.role || 'Nhân vật'}</p>
          <ul className="fx-crp__checks">
            <li>{detail.character.masterLocked ? '✓' : '○'} Hồ sơ nhân vật</li>
            <li>{detail.character.dnaLocked ? '✓' : '○'} Quy tắc nhận diện</li>
            <li>
              {detail.character.requiredReady === detail.character.requiredTotal && detail.character.requiredTotal > 0
                ? '✓'
                : '○'}{' '}
              Bộ ảnh chuẩn
              {detail.character.referencePackVersion ? ` ${detail.character.referencePackVersion}` : ''}
            </li>
            <li>{detail.character.canUse ? '✓' : '○'} Đã khóa</li>
          </ul>
          <h4>Ảnh chuẩn</h4>
          <div className="fx-crp__grid">
            {detail.views.map((view) => (
              <article key={view.type} className="fx-crp__tile">
                <strong>{viewSlotLabel(view.type)}</strong>
                <p>{view.present ? '✓ Có ảnh' : '⚠ Chưa có'}</p>
              </article>
            ))}
          </div>
          <h4 id="fx-clib-scenes">Được sử dụng trong {detail.character.sceneCount} cảnh</h4>
          {detail.scenes.length ? (
            <>
              <Button size="small" href="#fx-clib-scenes">
                Xem cảnh
              </Button>
              <ul className="fx-crp__checks">
                {detail.scenes.map((scene) => (
                  <li key={scene.shotId}>
                    {scene.shotCode} — {scene.title}
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="fx-desk__note">Chưa có cảnh sản xuất gắn nhân vật này.</p>
          )}
          <p className="fx-clib__next">
            <strong>Việc tiếp theo:</strong> {detail.character.nextAction}
          </p>
          {!detail.character.masterLocked || !detail.character.dnaLocked || !detail.character.productionReferenceLocked ? (
            <ContentFamixaCharacterAuthorityCard
              characterId={openId}
              characterName={detail.character.name}
              onChanged={() => openId && void fetchCharacterLibraryDetail(openId).then(setDetail)}
            />
          ) : null}
          <ContentKitVideoCharacterReferencePackCard characterId={openId} characterName={detail.character.name} />
          <Collapse
            className="fx-crp__tech"
            items={[
              {
                key: 'tech',
                label: 'Chi tiết kỹ thuật',
                children: <pre>{JSON.stringify(detail.technical ?? {}, null, 2)}</pre>,
              },
            ]}
          />
        </article>
      ) : null}

      <Modal
        title="Chọn nhân vật"
        open={pickerOpen}
        onCancel={() => setPickerOpen(false)}
        footer={<Button onClick={() => setPickerOpen(false)}>Hủy</Button>}
        width={720}
      >
        <ContentFamixaCharacterLibrary
          pickerOnly
          shots={shots}
          onPick={(id, canUse) => {
            setOpenId(id);
            setPickerOpen(false);
            onPick?.(id, canUse);
          }}
        />
      </Modal>
    </section>
  );
}
