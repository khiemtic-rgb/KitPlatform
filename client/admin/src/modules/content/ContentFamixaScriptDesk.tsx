import { type ReactNode } from 'react';
import { Button, Collapse, Input } from 'antd';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';

export function ContentFamixaScriptDesk({
  title,
  goal,
  audience,
  body,
  dialogue,
  moral,
  cta,
  locked,
  onTitle,
  onGoal,
  onBody,
  onSave,
  onConfirm,
  onProposeScenes,
  onReceivePack,
  onGoScenes,
  proposeNote,
  needsStoryReview,
  onReviewStory,
  sceneLabels,
  needsInheritReview,
  inheritFrom,
  inheritTo,
  inheritThreads,
  onReviewInherit,
  hideActions,
}: {
  title: string;
  goal: string;
  audience?: string;
  body: string;
  dialogue: string;
  moral?: string;
  cta?: string;
  locked: boolean;
  onTitle: (v: string) => void;
  onGoal: (v: string) => void;
  onBody: (v: string) => void;
  onSave: () => void;
  onConfirm: () => void;
  onProposeScenes: () => void;
  onReceivePack?: () => void;
  onGoScenes?: () => void;
  proposeNote?: string;
  needsStoryReview?: boolean;
  onReviewStory?: () => void;
  sceneLabels?: string[];
  needsInheritReview?: boolean;
  inheritFrom?: string;
  inheritTo?: string;
  inheritThreads?: string[];
  onReviewInherit?: () => void;
  hideActions?: boolean;
}) {
  return (
    <section className="fx-desk">
      {hideActions ? null : <h2>Kịch bản tập phim</h2>}
      <ContentFamixaVisualModeBadge compact />
      {hideActions ? null : (
      <p className="fx-desk__lead">
        Dán hành động + thoại vào Nội dung, bấm <strong>Nhận pack</strong> để tách cảnh. Duyệt rồi Hoàn tất. Máy không tự tạo
        ảnh hay video.
      </p>
      )}
      <label>
        Tên tập
        <Input
          value={title}
          placeholder="Ví dụ: Về nhà buổi tối"
          onChange={(e) => onTitle(e.target.value)}
        />
      </label>
      <label>
        Mục tiêu video
        <Input value={goal} disabled={locked} onChange={(e) => onGoal(e.target.value)} />
      </label>
      {audience ? (
        <p>
          <strong>Đối tượng:</strong> {audience}
        </p>
      ) : null}
      {moral ? (
        <p>
          <strong>Thông điệp chính:</strong> {moral}
        </p>
      ) : null}
      {cta ? (
        <p>
          <strong>Call to action:</strong> {cta}
        </p>
      ) : null}
      <Collapse
        defaultActiveKey={['body']}
        destroyOnHidden
        items={[
          {
            key: 'body',
            label: 'Nội dung chi tiết',
            children: (
              <label>
                Nội dung
                <Input.TextArea rows={6} value={body} disabled={locked} onChange={(e) => onBody(e.target.value)} />
              </label>
            ),
          },
          dialogue
            ? { key: 'dlg', label: 'Lời thoại', children: <pre className="fx-desk__aside">{dialogue}</pre> }
            : null,
          {
            key: 'tech',
            label: 'Thông tin kỹ thuật',
            children: (
              <>
                <p className="fx-desk__note">
                  Tách cảnh trên máy từ Nội dung. Không gửi nguyên văn lên nhà cung cấp từ màn này.
                </p>
                <Button onClick={onReceivePack || onProposeScenes} disabled={locked || !body.trim()}>
                  Nhận pack — tách cảnh
                </Button>
                {proposeNote ? <p className="fx-desk__note">{proposeNote}</p> : null}
              </>
            ),
          },
        ].filter(Boolean) as { key: string; label: string; children: ReactNode }[]}
      />
      {hideActions ? null : !locked && body.trim() && !sceneLabels?.length && !needsStoryReview ? (
        <p className="fx-desk__note">Chưa tách cảnh. Bấm <strong>Nhận pack</strong> — nút Duyệt hiện sau khi máy đọc nội dung.</p>
      ) : null}
      {hideActions ? null : needsStoryReview && onReviewStory ? (
        <div className="fx-media-card" data-script-review="director" style={{ margin: '12px 0' }}>
          <h3 style={{ margin: '0 0 6px' }}>Duyệt kịch bản đã tách</h3>
          <p className="fx-desk__note">
            Máy đã tách cảnh từ nội dung. Bấm Duyệt nếu đúng — rồi mới Hoàn tất kịch bản. Không tạo ảnh/video.
          </p>
          {sceneLabels?.length ? (
            <ul className="fx-wizard__sum">
              {sceneLabels.map((label) => (
                <li key={label}>{label}</li>
              ))}
            </ul>
          ) : null}
          <Button type="primary" onClick={onReviewStory}>
            Duyệt
          </Button>
        </div>
      ) : null}
      {hideActions ? null : needsInheritReview && onReviewInherit ? (
        <div className="fx-media-card" data-script-review="inherit" style={{ margin: '12px 0' }}>
          <h3 style={{ margin: '0 0 6px' }}>Duyệt kế thừa tập trước</h3>
          <p className="fx-desk__note">
            {inheritTo || 'Tập này'} nhận trạng thái từ {inheritFrom || 'tập trước'}. KIT không đóng thread, không viết xin
            lỗi hay bài học. Bấm Duyệt kế thừa nếu đúng — rồi Hoàn tất. Không tạo ảnh/video.
          </p>
          {inheritThreads?.length ? (
            <ul className="fx-wizard__sum">
              {inheritThreads.map((label) => (
                <li key={label}>{label}</li>
              ))}
            </ul>
          ) : (
            <p className="fx-desk__note">Không có thread OPEN trên sổ tập trước.</p>
          )}
          <Button type="primary" onClick={onReviewInherit}>
            Duyệt kế thừa
          </Button>
        </div>
      ) : null}
      {hideActions ? null : <div className="fx-desk__btns">
        {onReceivePack ? (
          <Button onClick={onReceivePack} disabled={locked || !body.trim()}>
            Nhận pack
          </Button>
        ) : null}
        <Button type="primary" onClick={onConfirm} disabled={locked || needsStoryReview || needsInheritReview}>
          Hoàn tất kịch bản
        </Button>
        <Button onClick={onSave} disabled={locked}>
          Lưu kịch bản
        </Button>
        {locked && onGoScenes ? (
          <Button onClick={onGoScenes}>Chia cảnh →</Button>
        ) : null}
      </div>}
      {hideActions ? null : <p className="fx-desk__note">Nhân viên phải xác nhận. Không tự tạo ảnh hay video.</p>}
    </section>
  );
}
