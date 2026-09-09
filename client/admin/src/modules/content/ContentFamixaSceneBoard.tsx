import { useEffect, useState } from 'react';
import { Button } from 'antd';
import { fetchKitVideoProductionShots } from '@/shared/api/content.api';
import { directorShotTitle, shortShotLabel, shotSceneLabel } from './kit-video-director-workspace';

export type SceneBoardRow = {
  id: string;
  shotCode?: string;
  title: string;
  seconds?: number;
  characters?: string;
  setting?: string;
  action?: string;
  image: string;
  video: string;
  productionId?: string;
};

export function ContentFamixaSceneBoard({
  rows,
  onOpen,
  onPropose,
  proposeNote,
}: {
  rows: SceneBoardRow[];
  onOpen: (id: string) => void;
  onPropose: () => void;
  proposeNote?: string;
}) {
  const [prod, setProd] = useState<SceneBoardRow[]>(rows);
  useEffect(() => {
    void fetchKitVideoProductionShots()
      .then((bundle) => {
        const live = (bundle.shots || []).map((shot) => ({
          id: shot.id,
          shotCode: shot.shotCode,
          title: directorShotTitle(shot.spec, shot.note),
          characters: shot.characterName,
          action: directorShotTitle(shot.spec, shot.note),
          image: 'Đang chờ duyệt hoặc đã có',
          video: 'Chưa tạo',
          productionId: shot.id,
        }));
        const extras = rows.filter((r) => !live.some((p) => p.id === r.id));
        setProd(live.length ? [...live, ...extras] : rows);
      })
      .catch(() => setProd(rows));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows.length]);
  const view = prod;
  return (
    <section className="fx-desk">
      <h2>Cảnh</h2>
      <p className="fx-desk__lead">
        Video này hiện có {view.length} cảnh. Mã SHOT là mã hệ thống, không phải tên việc.
      </p>
      <div className="fx-desk__btns">
        <Button onClick={onPropose}>AI đề xuất chia cảnh</Button>
      </div>
      {proposeNote ? <p className="fx-desk__note">{proposeNote}</p> : null}
      {!view.length ? <p className="fx-desk__note">Chưa có cảnh. Xác nhận kịch bản trước.</p> : null}
      {view.map((row, i) => (
        <article key={row.id} className="fx-scene-card">
          <h3>
            {shotSceneLabel(row.shotCode || `SHOT-${String(i + 1).padStart(3, '0')}`).toUpperCase()}
            {row.title ? ` — ${row.title}` : ''}
          </h3>
          {row.shotCode ? <p className="fx-scene-card__code">{shortShotLabel(row.shotCode)}</p> : null}
          {row.seconds ? <p>Thời lượng: {row.seconds} giây</p> : null}
          {row.characters ? <p>Nhân vật: {row.characters}</p> : null}
          {row.setting ? <p>Bối cảnh: {row.setting}</p> : null}
          {row.action ? <p>Hành động: {row.action}</p> : null}
          <p>Hình ảnh: {row.image}</p>
          <p>Video: {row.video}</p>
          {row.productionId ? (
            <Button type="primary" onClick={() => onOpen(row.productionId!)}>
              Mở cảnh
            </Button>
          ) : (
            <p className="fx-desk__note">Cảnh này chưa vào sản xuất.</p>
          )}
        </article>
      ))}
    </section>
  );
}
