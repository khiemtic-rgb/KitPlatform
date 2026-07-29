'use client';

import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { STATS_VIEW_PASSWORD } from '@/lib/stats-config';
import { filterTopPages } from '@/lib/stats-top-pages';
import type { StatsDaily, StatsHourly, StatsPayload, StatsTopPage } from '@/lib/stats-types';

const STORAGE_KEY = 'famixa-stats-key';

const fmt = new Intl.NumberFormat('vi-VN');
const fmtDate = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  dateStyle: 'medium',
  timeStyle: 'short',
});
const fmtHour = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  hour: '2-digit',
  minute: '2-digit',
});
const fmtDay = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  weekday: 'short',
  day: 'numeric',
  month: 'short',
});

function StatsTable({
  headers,
  rows,
}: {
  headers: string[];
  rows: ReactNode[][];
}) {
  if (!rows.length) {
    return <p className="stats-empty">Chưa có dữ liệu.</p>;
  }
  return (
    <table className="stats-table">
      <thead>
        <tr>
          {headers.map((h) => (
            <th key={h}>{h}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((cells, i) => (
          <tr key={i}>
            {cells.map((c, j) => (
              <td key={j}>{c}</td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function HourlyBars({ hourly }: { hourly: StatsHourly[] }) {
  if (!hourly.length) {
    return <p className="stats-empty">Chưa có dữ liệu.</p>;
  }
  const max = Math.max(...hourly.map((h) => h.visitors), 1);
  return (
    <div className="stats-bars" aria-label="Biểu đồ visitor theo giờ">
      {hourly.map((row) => {
        const height = Math.max(4, Math.round((row.visitors / max) * 100));
        const label = row.time ? fmtHour.format(new Date(row.time)) : '';
        return (
          <div
            key={row.time || label}
            className="stats-bar"
            title={`${label}: ${fmt.format(row.visitors)} visitor`}
          >
            <div className="stats-bar-fill" style={{ height: `${height}%` }} />
            <span className="stats-bar-value">{row.visitors || ''}</span>
            <span className="stats-bar-label">{label}</span>
          </div>
        );
      })}
    </div>
  );
}

async function fetchLiveStats(key: string): Promise<StatsPayload> {
  const response = await fetch('/api/stats', {
    headers: { Authorization: `Bearer ${key}` },
    cache: 'no-store',
  });
  const data = (await response.json().catch(() => ({}))) as StatsPayload & { error?: string };
  if (response.status === 401) {
    throw new Error('Mật khẩu không đúng trên server — kiểm tra STATS_VIEW_KEY trên Cloudflare.');
  }
  if (!response.ok) {
    throw new Error(data?.error ?? `API thống kê lỗi (${response.status})`);
  }
  return data;
}

async function fetchSnapshotStats(): Promise<StatsPayload> {
  const response = await fetch('/stats-snapshot.json', { cache: 'no-store' });
  const raw = await response.text();
  let data: StatsPayload;
  try {
    data = JSON.parse(raw) as StatsPayload;
  } catch {
    throw new Error('File thống kê chưa sẵn sàng — chạy workflow Famixa update stats trên GitHub.');
  }
  if (!response.ok) {
    throw new Error('Không tải được file thống kê.');
  }
  if (data.ok === false) {
    throw new Error(
      data.error ??
        'Chưa có dữ liệu thống kê. Chạy workflow "Famixa update stats" trên GitHub Actions.',
    );
  }
  return data;
}

export function StatsDashboard() {
  const [password, setPassword] = useState('');
  const [unlocked, setUnlocked] = useState(false);
  const [loginError, setLoginError] = useState('');
  const [sourceNotice, setSourceNotice] = useState<{ message: string; level: 'ok' | 'warn' } | null>(
    null,
  );
  const [refreshing, setRefreshing] = useState(false);
  const [data, setData] = useState<StatsPayload | null>(null);

  async function loadStats(key: string, { preferLive = false } = {}) {
    setLoginError('');
    if (key !== STATS_VIEW_PASSWORD) {
      throw new Error('Mật khẩu không đúng.');
    }

    if (preferLive) {
      try {
        const live = await fetchLiveStats(key);
        setData(live);
        setSourceNotice({ message: 'Dữ liệu live từ Cloudflare API.', level: 'ok' });
        setUnlocked(true);
        return;
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Không tải được API.';
        const apiUnavailable =
          message.includes('STATS_VIEW_KEY') ||
          message.includes('503') ||
          message.includes('502') ||
          message.includes('CF_ZONE_ID') ||
          message.includes('Failed to fetch') ||
          message.includes('fetch');
        if (!apiUnavailable) {
          throw err;
        }
        setSourceNotice({
          message:
            'API live chưa sẵn sàng trên Cloudflare Pages — đang dùng bản snapshot. Nếu vừa chạy workflow GitHub mà giờ cập nhật vẫn cũ: đợi Cloudflare deploy xong (vài phút) hoặc Deployments → Retry deployment.',
          level: 'warn',
        });
      }
    } else {
      setSourceNotice(null);
    }

    const snapshot = await fetchSnapshotStats();
    setData(snapshot);
    setUnlocked(true);
  }

  async function handleLogin(e?: FormEvent) {
    e?.preventDefault();
    const key = password.trim();
    if (!key) {
      setLoginError('Vui lòng nhập mật khẩu.');
      return;
    }
    try {
      await loadStats(key, { preferLive: true });
      sessionStorage.setItem(STORAGE_KEY, key);
    } catch (err) {
      setLoginError(err instanceof Error ? err.message : 'Lỗi không xác định.');
    }
  }

  async function handleRefresh() {
    const key = sessionStorage.getItem(STORAGE_KEY) ?? password.trim();
    if (!key) return;
    setRefreshing(true);
    try {
      await loadStats(key, { preferLive: true });
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Không tải lại được.');
    } finally {
      setRefreshing(false);
    }
  }

  useEffect(() => {
    const saved = sessionStorage.getItem(STORAGE_KEY);
    if (saved) {
      setPassword(saved);
      loadStats(saved, { preferLive: true }).catch(() => {
        sessionStorage.removeItem(STORAGE_KEY);
      });
      return;
    }
    if (location.hostname === 'localhost' || location.hostname === '127.0.0.1') {
      setLoginError('Local: dùng mật khẩu trang; live API chỉ có trên Cloudflare Pages.');
    }
  }, []);

  const summary = data?.summary;
  const total7Visitors = (data?.daily ?? []).reduce((sum, row) => sum + (row.visitors ?? 0), 0);
  const total7Pages = (data?.daily ?? []).reduce((sum, row) => sum + (row.pageViews ?? 0), 0);
  const topPages: StatsTopPage[] = filterTopPages(data?.topPages ?? []);
  const daily: StatsDaily[] = data?.daily ?? [];

  return (
    <div className="stats-container">
      <h1 className="stats-title">Thống kê truy cập website</h1>
      <p className="stats-intro">
        Xem nhanh lượt ghé thăm famixa.vn — dữ liệu Cloudflare. Bấm <strong>Tải lại</strong> để lấy
        số liệu mới nhất (API trực tiếp); nếu API chưa cấu hình thì dùng bản snapshot lúc deploy.
      </p>

      {!unlocked && (
        <form className="stats-login" onSubmit={handleLogin}>
          <label htmlFor="stats-key">Mật khẩu xem thống kê</label>
          <div className="stats-login-row">
            <input
              id="stats-key"
              type="password"
              autoComplete="current-password"
              placeholder="Nhập mật khẩu"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <button type="submit" className="stats-btn stats-btn-primary">
              Xem thống kê
            </button>
          </div>
          {loginError ? <p className="stats-error">{loginError}</p> : null}
        </form>
      )}

      {unlocked && data && summary ? (
        <div className="stats-panel">
          <div className="stats-toolbar">
            <p className="stats-updated">Cập nhật: {fmtDate.format(new Date(data.generatedAt))}</p>
            <button
              type="button"
              className="stats-btn stats-btn-secondary"
              onClick={handleRefresh}
              disabled={refreshing}
            >
              {refreshing ? 'Đang tải…' : 'Tải lại'}
            </button>
          </div>

          {sourceNotice ? (
            <p className="stats-source-notice" data-level={sourceNotice.level}>
              {sourceNotice.message}
            </p>
          ) : null}

          <div className="stats-cards">
            <article className="stats-card">
              <h2>Hôm nay</h2>
              <p className="stats-number">{fmt.format(summary.todayVisitors)}</p>
              <p className="stats-label">Visitor duy nhất</p>
              <p className="stats-sub">
                {fmt.format(summary.todayPageViews)} lượt xem trang ·{' '}
                {fmt.format(summary.todayRequests)} request
              </p>
            </article>
            <article className="stats-card">
              <h2>24 giờ qua</h2>
              <p className="stats-number">{fmt.format(summary.last24hVisitors)}</p>
              <p className="stats-label">Visitor duy nhất</p>
              <p className="stats-sub">
                {fmt.format(summary.last24hPageViews)} lượt xem trang ·{' '}
                {fmt.format(summary.last24hRequests)} request
              </p>
            </article>
            <article className="stats-card">
              <h2>7 ngày qua</h2>
              <p className="stats-number">{fmt.format(total7Visitors)}</p>
              <p className="stats-label">Tổng visitor (theo ngày)</p>
              <p className="stats-sub">{fmt.format(total7Pages)} lượt xem trang (7 ngày)</p>
            </article>
          </div>

          <article className="stats-block">
            <h2>Biểu đồ 24 giờ qua</h2>
            <p className="stats-hint">Mỗi cột = số visitor duy nhất trong 1 giờ (giờ Việt Nam).</p>
            <HourlyBars hourly={data.hourly ?? []} />
          </article>

          <article className="stats-block">
            <h2>Trang được xem nhiều (24h)</h2>
            <div className="stats-table-wrap">
              <StatsTable
                headers={['Trang', 'Lượt truy cập']}
                rows={topPages.map((row) => [row.path, fmt.format(row.views)])}
              />
            </div>
          </article>

          <article className="stats-block">
            <h2>Theo ngày (7 ngày)</h2>
            <div className="stats-table-wrap">
              <StatsTable
                headers={['Ngày', 'Visitor', 'Lượt xem', 'Request']}
                rows={daily.map((row) => [
                  row.date ? fmtDay.format(new Date(`${row.date}T12:00:00+07:00`)) : '—',
                  fmt.format(row.visitors),
                  fmt.format(row.pageViews),
                  fmt.format(row.requests),
                ])}
              />
            </div>
          </article>

          <p className="stats-footnote">
            Trang này không hiện trên menu công khai. Visitor = trình duyệt/IP khác nhau
            (Cloudflare). Đợt tăng đột biến có thể do bot — xem cột Trang để biết khách xem nội dung
            nào.
          </p>
        </div>
      ) : null}
    </div>
  );
}
