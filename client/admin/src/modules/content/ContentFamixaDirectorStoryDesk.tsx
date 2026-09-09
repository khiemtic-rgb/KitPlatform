import { Button, Radio } from 'antd';
import type { ReactNode } from 'react';
import type { DirectorStoryAction } from './kit-video-director-nav';

export function ContentFamixaDirectorStoryDesk({
  script,
  shotOrder,
  aspect,
  onAspect,
  storyAction,
  onPrimary,
}: {
  script: ReactNode;
  shotOrder: { id: string; label: string }[];
  aspect?: '16:9' | '9:16';
  onAspect?: (next: '16:9' | '9:16') => void;
  storyAction: DirectorStoryAction;
  onPrimary: () => void;
}) {
  return (
    <section className="fx-desk" data-director-desk="story">
      <h2>Chuyện</h2>
      <p className="fx-desk__lead">Viết chuyện, xem thứ tự shot, rồi khóa một lần. Máy không tự tạo ảnh hay video.</p>
      {script}
      <article className="fx-media-card">
        <h3>Thứ tự shot</h3>
        {shotOrder.length ? (
          <ol className="fx-wizard__sum">
            {shotOrder.map((row) => (
              <li key={row.id}>{row.label}</li>
            ))}
          </ol>
        ) : (
          <p className="fx-desk__note">Chưa có shot. Dán nội dung rồi khóa chuyện — máy sẽ tách cảnh.</p>
        )}
      </article>
      <article className="fx-media-card">
        <h3>Khung xuất</h3>
        <Radio.Group
          value={aspect || '16:9'}
          onChange={(e) => onAspect?.(e.target.value)}
          options={[
            { value: '16:9', label: '16:9' },
            { value: '9:16', label: '9:16' },
          ]}
        />
      </article>
      <article className="fx-media-card" data-director-story-lock="1">
        {storyAction.locked ? (
          <p>Chuyện đã khóa.</p>
        ) : storyAction.canLock ? (
          <p>Khóa chuyện khi nội dung và cách chia shot đã đúng.</p>
        ) : (
          <>
            <p>{storyAction.reason}</p>
            {storyAction.missing ? <p className="fx-desk__note">Còn thiếu: {storyAction.missing}</p> : null}
          </>
        )}
        <div className="fx-desk__btns">
          <Button type="primary" disabled={storyAction.locked} onClick={onPrimary} data-director-story-cta="1">
            {storyAction.cta}
          </Button>
        </div>
      </article>
    </section>
  );
}
