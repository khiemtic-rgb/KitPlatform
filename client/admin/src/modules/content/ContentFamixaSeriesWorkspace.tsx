import { type ReactNode } from 'react';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { Button, Collapse } from 'antd';
import {
  SERIES_STAFF_TABS,
  focusSeriesTaskPanel,
  type ProductionUserMode,
  type SeriesStaffTab,
} from './kit-video-production-ux';
import { episodeNavTabs } from './kit-video-director-nav';

const TAB_TO_TRACK: Record<string, string> = {
  script: 'script',
  scenes: 'scenes',
  characters: 'cast',
  voice: 'voice',
  images: 'image',
  video: 'video',
  finish: 'finish',
  publish: 'publish',
};

const TRACK_ORDER = ['script', 'scenes', 'cast', 'voice', 'image', 'video', 'finish', 'publish'] as const;

function tabIsReady(tabId: string, track: { id: string; done: boolean }[]) {
  if (tabId === 'overview' || tabId === 'script') return true;
  const mapped = TAB_TO_TRACK[tabId];
  const idx = TRACK_ORDER.indexOf(mapped as (typeof TRACK_ORDER)[number]);
  if (idx < 0) return true;
  if (tabId === 'voice') {
    return ['script', 'scenes'].every((id) => track.find((t) => t.id === id)?.done);
  }
  if (tabId === 'images') {
    return ['script', 'scenes'].every((id) => track.find((t) => t.id === id)?.done);
  }
  if (tabId === 'video') {
    return ['script', 'scenes', 'image'].every((id) => track.find((t) => t.id === id)?.done);
  }
  if (tabId === 'finish') {
    return Boolean(track.find((t) => t.id === 'video')?.done);
  }
  if (tabId === 'publish') {
    return Boolean(track.find((t) => t.id === 'publish')?.done);
  }
  return TRACK_ORDER.slice(0, idx).every((id) => track.find((t) => t.id === id)?.done);
}

function tabMark(tabId: string, track: { id: string; done: boolean }[], currentTrack?: string, selected?: boolean) {
  if (tabId === 'overview') return selected ? '●' : '✓';
  const mapped = TAB_TO_TRACK[tabId];
  const node = track.find((t) => t.id === mapped);
  if (node?.done) return '✓';
  if (currentTrack === mapped || selected) return '●';
  return '○';
}

export function ContentFamixaSeriesWorkspace({
  episodeCode,
  episodeTitle,
  seriesTitle,
  episodeDesc,
  mode,
  tab,
  onBack,
  onMode,
  onTab,
  track,
  currentTrack,
  sceneOpen,
  overview,
  script,
  scenes,
  characters,
  voice,
  images,
  video,
  finish,
  publish,
  sceneWorkspace,
  lookBuild,
  productionSystem,
  currentTask,
  returnBanner,
}: {
  episodeCode: string;
  episodeTitle?: string;
  seriesTitle?: string;
  episodeDesc?: string;
  onBack?: () => void;
  mode: ProductionUserMode;
  tab: SeriesStaffTab;
  onMode: (mode: ProductionUserMode) => void;
  onTab: (tab: SeriesStaffTab) => void;
  track: { id: string; done: boolean; label: string }[];
  currentTrack?: string;
  sceneOpen: boolean;
  overview: ReactNode;
  script: ReactNode;
  scenes: ReactNode;
  characters: ReactNode;
  voice: ReactNode;
  images: ReactNode;
  video: ReactNode;
  finish: ReactNode;
  publish: ReactNode;
  sceneWorkspace: ReactNode;
  lookBuild: ReactNode;
  productionSystem: ReactNode;
  currentTask?: { headline: string; hint: string; action: string; tab: SeriesStaffTab };
  returnBanner?: { label: string; onBack: () => void };
}) {
  const director = mode === 'director';
  const nav = episodeNavTabs(mode);
  const crumb = nav.find((t) => t.id === tab)?.label || SERIES_STAFF_TABS.find((t) => t.id === tab)?.label;
  const body =
    tab === 'overview'
      ? overview
      : tab === 'script'
        ? script
        : tab === 'scenes'
          ? scenes
          : tab === 'characters'
            ? characters
            : tab === 'voice'
              ? voice
            : tab === 'images'
              ? images
              : tab === 'video'
                ? video
                : tab === 'publish'
                  ? publish
                  : finish;

  return (
    <section className="fx-sw">
      <header className="fx-sw__head">
        <div>
          {onBack ? (
            <Button type="link" className="fx-dir-ws__back" onClick={onBack}>
              ← Video Studio
            </Button>
          ) : null}
          <p className="fx-sw__crumb">
            Video Studio / {episodeCode} – {episodeTitle || 'Tập'} / {crumb}
          </p>
          <p className="fx-sw__brand">VIDEO STUDIO</p>
          <h1 className="fx-sw__title" data-episode-heading="1">
            {episodeCode} · {episodeTitle?.trim() || 'Chưa đặt tên tập'}
          </h1>
          <p className="fx-sw__series">{seriesTitle?.trim() || 'Famixa'} · Video giáo dục</p>
          {episodeDesc ? <p className="fx-sw__desc">{episodeDesc}</p> : null}
          <p className="fx-sw__ep">
            Trạng thái: {track.every((t) => t.done) ? 'Hoàn thành' : 'Đang làm'}
          </p>
          <p className="fx-sw__ep">
            Tiến độ: {Math.round((track.filter((t) => t.done).length / Math.max(track.length, 1)) * 100)}%
          </p>
        </div>
        <div className="fx-sw__mode">
          <ContentFamixaVisualModeBadge compact />
          <Button type={mode === 'director' ? 'primary' : 'default'} onClick={() => onMode('director')}>
            Đạo diễn
          </Button>
          <Button type={mode === 'staff' ? 'primary' : 'default'} onClick={() => onMode('staff')}>
            Nhân viên
          </Button>
        </div>
      </header>

      <p className="fx-sw__now" data-episode-title="1">
        Đang mở tập: {episodeCode} · {episodeTitle?.trim() || 'Chưa đặt tên tập'}
      </p>
      <nav className="fx-sw__nav" aria-label={director ? 'Chuyện Người Short' : 'Quy trình tập'} data-director-nav={director ? '1' : '0'}>
        {nav.map((item) => {
          const ready = director || tabIsReady(item.id, track);
          const mark = tabMark(item.id, track, currentTrack, tab === item.id && !sceneOpen);
          return (
            <button
              key={item.id}
              type="button"
              className={`${tab === item.id && !sceneOpen ? 'is-on' : ''}${ready ? '' : ' is-wait'}`}
              disabled={!ready}
              title={ready ? item.label : 'Chưa sẵn sàng'}
              onClick={() => ready && onTab(item.id)}
            >
              {mark} {item.label}
            </button>
          );
        })}
      </nav>

      {returnBanner && !director ? (
        <aside className="fx-prod__now fx-sw__return">
          <p className="fx-prod__kicker">ĐANG XỬ LÝ ĐIỀU KIỆN TẠO ẢNH</p>
          <h3>{returnBanner.label}</h3>
          <p>Xong việc trên tab này rồi quay lại bàn Hình ảnh. Không mở tab thứ ba.</p>
          <Button type="primary" onClick={returnBanner.onBack}>
            Quay lại tạo ảnh
          </Button>
        </aside>
      ) : null}

      {currentTask && !director && tab !== 'overview' && !returnBanner ? (
        <aside className="fx-prod__now">
          <p className="fx-prod__kicker">VIỆC CẦN LÀM</p>
          <h3>{currentTask.headline}</h3>
          <p>{currentTask.hint}</p>
          <Button
            type="primary"
            data-series-task="open"
            onClick={() => {
              onTab(currentTask.tab);
              window.setTimeout(() => focusSeriesTaskPanel(currentTask.tab), 50);
            }}
          >
            {currentTask.action} →
          </Button>
        </aside>
      ) : null}

      {sceneOpen && !director ? sceneWorkspace : (
        <div className="fx-sw__body" id={`fx-tab-${tab}`}>
          {body}
        </div>
      )}

      {mode === 'staff' ? (
        <Collapse
          className="fx-sw__tech"
          destroyOnHidden
          items={[
            {
              key: 'look',
              label: 'Tạo hình (đã xong) — không dùng để làm tập',
              children: lookBuild,
            },
            {
              key: 'sys',
              label: '⚙ Chi tiết hệ thống',
              children: productionSystem,
            },
          ]}
        />
      ) : track.find((t) => t.id === 'cast')?.done ? (
        <p className="fx-sw__lock">Nhân vật đã khóa · Minh</p>
      ) : null}
    </section>
  );
}
