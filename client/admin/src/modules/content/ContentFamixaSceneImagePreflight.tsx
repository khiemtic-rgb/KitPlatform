import { Button } from 'antd';
import type { SceneImageBlocker, SceneImagePreflight } from './kit-video-scene-image-preflight';
import { sceneImageActionStays } from './kit-video-scene-image-preflight';

export function ContentFamixaSceneImagePreflight({
  sceneLabel,
  preflight,
  onAction,
}: {
  sceneLabel: string;
  preflight: SceneImagePreflight;
  onAction: (action: SceneImageBlocker['action']) => void;
}) {
  if (preflight.allowed) {
    return (
      <div className="fx-preflight fx-preflight--ready">
        <p className="fx-preflight__status">✓ Đủ điều kiện tạo ảnh</p>
        <p>{sceneLabel} đã đủ điều kiện tạo ảnh.</p>
        <ul className="fx-preflight__checks">
          {preflight.checks.map((c) => (
            <li key={c.id} className="is-ok">
              ✓ {c.label}
            </li>
          ))}
        </ul>
      </div>
    );
  }

  const failed = preflight.checks.filter((c) => !c.ok);
  const passed = preflight.checks.filter((c) => c.ok);
  return (
    <div className="fx-preflight fx-preflight--blocked">
      <p className="fx-preflight__status">⚠ Chưa đủ điều kiện tạo ảnh</p>
      <p className="fx-preflight__count">
        {preflight.blockers.length === 1
          ? 'Còn 1 việc trên bàn này'
          : `Còn ${preflight.blockers.length} việc trên bàn này`}
      </p>
      {failed.length ? (
        <ul className="fx-preflight__checks">
          {failed.map((c) => (
            <li key={c.id} className="is-bad">
              ✕ {c.label}
            </li>
          ))}
        </ul>
      ) : null}
      {passed.length ? (
        <p className="fx-preflight__done">Đã xong: {passed.map((c) => c.label).join(' · ')}</p>
      ) : null}
      <p className="fx-preflight__title">Làm lần lượt — không nhảy lung tung</p>
      {preflight.blockers.map((b, index) => (
        <article key={`${b.code}-${b.entityId || b.title}`} className="fx-preflight__blocker">
          <p className="fx-preflight__kicker">Việc {index + 1}/{preflight.blockers.length}</p>
          <h4>⚠ {b.title}</h4>
          <p>{b.message}</p>
          <div className="fx-preflight__actions">
            {b.action.id === 'set_aspect' ? (
              <>
                <Button type="primary" onClick={() => onAction({ ...b.action, aspect: '16:9', label: '16:9' })}>
                  16:9 ngang
                </Button>
                <Button type="primary" onClick={() => onAction({ ...b.action, aspect: '9:16', label: '9:16' })}>
                  9:16 dọc
                </Button>
              </>
            ) : (
              <Button type="primary" onClick={() => onAction(b.action)}>
                {b.action.label}
              </Button>
            )}
            {b.action.characterId && b.action.id === 'open_character_studio' ? (
              <Button onClick={() => onAction({ ...b.action, id: 'open_character_view', label: 'Xem nhân vật' })}>
                Xem nhân vật
              </Button>
            ) : null}
          </div>
          <p className="fx-desk__note">
            {b.action.id === 'set_aspect' || b.action.stay || sceneImageActionStays(b.action.id)
              ? 'Ở lại màn Hình ảnh.'
              : `Sang tab ${b.action.destLabel || 'đúng việc'}, xong bấm Quay lại tạo ảnh.`}
          </p>
        </article>
      ))}
    </div>
  );
}
