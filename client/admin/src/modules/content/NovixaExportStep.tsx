import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, App, Button, Card, Checkbox, Input, InputNumber, Popconfirm, Space, Tag, Typography, Upload } from 'antd';
import {
  createContentTopic,
  fetchContentChannels,
  fetchNovixaShort,
  fetchNovixaShorts,
  fetchNovixaVideo,
  fetchScriptLibraryItem,
  renderNovixaStockMotion,
  type ScriptPublishContent,
} from '@/shared/api/content.api';
import { importTopicVariants, savePublishExtras, uploadTopicVideo, type ImportVariantItem } from '@/shared/api/content-youtube.api';
import { fitImage, makeCover, type CoverShape } from './novixa-cover';
import { scheduleTopicsWithAgent, type AgentSkipped, type TopicPublishPlanInput } from '@/shared/api/content-agent.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { SeriesPilotState } from './content-famixa-series';
import type { FilmExportRecord, NovixaMotionRun } from './film-production-orchestrator';
import { checkFacebookPost, joinFacebookPost } from './facebook-post';
import { draftShortPublish } from './novixa-publish-draft';
import { filmScriptHash, isShortOnlyLine, spokenLine } from './film-production-orchestrator';
import { shortOnlyLines } from './novixa-shorts';
import { lintProfileOf } from './novixa-script-lint';
import {
  fetchRemoteStatus,
  remoteBrands,
  remoteChannels,
  remoteCreateTopic,
  remoteImportVariants,
  remotePublishExtras,
  remoteScheduleTopics,
  remoteUploadVideo,
  useRemoteSession,
  type RemoteStatus,
} from '@/shared/api/content-remote.api';
import { RemotePublishServerCard } from './RemotePublishServerCard';

type Channel = 'youtube_channel' | 'tiktok_channel' | 'facebook_page';

type Row = {
  key: string;
  label: string;
  channels: Channel[];
  kind: 'long' | 'short';
  seq: number;
  defaults: { title: string; youtube: string; tiktok: string; facebook: string; fbComment: string };
  ready: boolean;
  /** Việc người duyệt cần xem, do bộ tự soạn nội dung đăng nêu ra. */
  warnings?: string[];
  load: () => Promise<Blob>;
};

type Draft = { title?: string; youtube?: string; tiktok?: string; facebook?: string; fbComment?: string };

/** Ảnh bìa của một video: ảnh đã tạo (hoặc tự chọn), giây lấy khung hình (cũng là khung bìa TikTok). */
type Cover = { blob: Blob; url: string; atSeconds: number; custom?: boolean };
type CoverOpt = { atSeconds?: number | null; withTitle?: boolean };

const PLAYLIST_KEY = (brand?: string) => `kitplatform-yt-playlist:${brand || 'default'}`;

function readPlaylist(brand?: string) {
  try {
    return window.localStorage.getItem(PLAYLIST_KEY(brand)) ?? '';
  } catch {
    return '';
  }
}

function writePlaylist(brand: string | undefined, value: string) {
  try {
    window.localStorage.setItem(PLAYLIST_KEY(brand), value);
  } catch {
    /* trình duyệt chặn lưu: chỉ mất phần nhớ giá trị lần sau */
  }
}

const DISCLAIMER = 'Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.';
const CHANNEL_TEXT: Record<Channel, string> = { youtube_channel: 'YouTube', tiktok_channel: 'TikTok', facebook_page: 'Fanpage' };

function folderOf(path?: string | null) {
  const parts = (path || '').split(/[/\\]/).filter(Boolean);
  return parts.length > 1 ? parts[parts.length - 2] : '';
}

function ready(run?: NovixaMotionRun | null) {
  return run?.status === 'ready' && Boolean(run.videoPath);
}

function vnTime(iso: string) {
  return new Date(iso).toLocaleString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Bước 5: duyệt thành phẩm. Mỗi file thành một bài có video đính kèm; Agent xếp các bài này
 * vào khung trống tiếp theo theo cài đặt lịch của thương hiệu (sau các bài đã có lịch) và duyệt luôn.
 * YouTube tự đăng đúng giờ; TikTok vào hộp nháp (TikTok không nhận caption qua API).
 */
export function NovixaExportStep({
  buildId,
  brandId,
  brandCode,
  scriptId,
  filmTitle,
  scriptLines = [],
  state,
  needsWide,
  onCommit,
}: {
  buildId: string;
  brandId: string;
  brandCode?: string;
  scriptId?: string;
  filmTitle: string;
  /** Các câu đọc của kịch bản, để tự soạn nội dung đăng cho video ngắn. */
  scriptLines?: string[];
  state: SeriesPilotState;
  needsWide: boolean;
  onCommit: (next: SeriesPilotState) => void;
}) {
  const { message } = App.useApp();
  const film = state.filmOrchestration;
  const exports = film?.exports ?? {};
  const [shorts, setShorts] = useState<{ folder: string; items: { index: number; title?: string | null; seconds: number; from?: number; to?: number; parts?: { from: number; to: number }[] | null }[] } | null>(null);
  const [publish, setPublish] = useState<ScriptPublishContent | null>(null);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [unchecked, setUnchecked] = useState<Record<string, boolean>>({});
  const [busy, setBusy] = useState('');
  const [progress, setProgress] = useState('');
  const [error, setError] = useState('');
  const [skipped, setSkipped] = useState<AgentSkipped[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);

  // Ảnh bìa + danh sách phát: đăng kèm video cho chuyên nghiệp (YouTube, Shorts, Reels, video Fanpage, khung bìa TikTok).
  const [covers, setCovers] = useState<Record<string, Cover>>({});
  const [coverOpts, setCoverOpts] = useState<Record<string, CoverOpt>>({});
  const [coverBusy, setCoverBusy] = useState('');
  const [playlist, setPlaylist] = useState(() => readPlaylist(brandCode));
  const videoCache = useRef(new Map<string, Blob>());
  const coverUrls = useRef(new Set<string>());
  useEffect(() => setPlaylist(readPlaylist(brandCode)), [brandCode]);
  useEffect(() => {
    const urls = coverUrls.current;
    const cache = videoCache.current;
    return () => {
      urls.forEach((u) => URL.revokeObjectURL(u));
      urls.clear();
      cache.clear();
    };
  }, []);

  // Đồng bộ lên máy chủ đăng bài: máy này cấu hình Content:RemotePublishBaseUrl thì duyệt = gửi thành phẩm lên máy chủ.
  const [remote, setRemote] = useState<RemoteStatus | null>(null);
  const remoteToken = useRemoteSession((s) => s.accessToken);
  const remoteBase = useRemoteSession((s) => s.baseUrl);
  const [remoteBrandId, setRemoteBrandId] = useState('');
  const [remoteBrandError, setRemoteBrandError] = useState('');
  const remoteOn = Boolean(remote?.enabled);
  const remoteLogged = remoteOn && Boolean(remoteToken) && remoteBase === remote?.baseUrl;
  const remoteReady = remoteLogged && Boolean(remoteBrandId);
  const recOf = (key: string) => (remoteOn ? exports[key]?.remote : exports[key]);
  // Câu miễn trừ đầu tư chỉ áp cho kênh tài chính (Người giữ vốn), không áp cho Novixa.
  const finance = lintProfileOf(brandCode) === 'trading';

  useEffect(() => {
    let cancelled = false;
    void fetchRemoteStatus()
      .then((s) => !cancelled && setRemote(s))
      .catch(() => !cancelled && setRemote({ enabled: false, baseUrl: null }));
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setRemoteBrandId('');
    setRemoteBrandError('');
    if (!remoteLogged) return;
    void remoteBrands()
      .then((list) => {
        if (cancelled) return;
        const found = list.find((b) => brandCode && b.code === brandCode) ?? list.find((b) => b.id === brandId);
        if (found) setRemoteBrandId(found.id);
        else setRemoteBrandError(`Máy chủ đăng bài chưa có thương hiệu «${brandCode || brandId}».`);
      })
      .catch((reason) => !cancelled && setRemoteBrandError(apiErrorMessage(reason, 'Không đọc được thương hiệu trên máy chủ đăng bài.')));
    return () => {
      cancelled = true;
    };
  }, [remoteLogged, brandCode, brandId]);

  useEffect(() => {
    let cancelled = false;
    void fetchNovixaShorts(buildId).then((found) => {
      if (cancelled || !found?.shorts?.length || !found.folder) return;
      setShorts({ folder: found.folder.split(/[\\/]/).pop() || '', items: found.shorts });
    });
    return () => {
      cancelled = true;
    };
  }, [buildId]);

  // Có Fanpage thì video dài (một bản) và các video ngắn cũng lên lịch Fanpage.
  const [hasFanpage, setHasFanpage] = useState(false);
  useEffect(() => {
    let cancelled = false;
    if (remoteOn && !remoteBrandId) return;
    void (remoteOn ? remoteChannels(remoteBrandId) : fetchContentChannels(brandId))
      .then((ch) => !cancelled && setHasFanpage(ch.some((c) => c.isActive && c.channelType === 'facebook_page')))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [brandId, remoteOn, remoteBrandId]);

  useEffect(() => {
    let cancelled = false;
    if (!brandCode || !scriptId) return;
    void fetchScriptLibraryItem(brandCode, scriptId)
      .then((item) => !cancelled && setPublish(item.publish ?? null))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [brandCode, scriptId]);

  const orient = film?.stillOrientation ?? 'tall';
  const runOf = (side: 'wide' | 'tall') => (orient === side ? film?.motion : film?.shelf?.orientation === side ? film.shelf.motion : undefined);
  const wideRun = runOf('wide');
  const tallRun = runOf('tall');

  // Kịch bản có câu [ngắn]: video dài phải là bản đã bỏ các câu đó, không đăng thẳng bản dựng đầy đủ.
  const scriptText = film?.script || state.scriptDraft || state.packDraft || '';
  const shortOnly = useMemo(() => shortOnlyLines(scriptText.split(/\r?\n/), spokenLine, isShortOnlyLine), [scriptText]);
  const scriptKey = filmScriptHash(scriptText);
  const longKey = (side: 'wide' | 'tall') => `${scriptKey}|${(side === 'wide' ? wideRun : tallRun)?.videoPath ?? ''}`;
  const longOf = (side: 'wide' | 'tall') => {
    const found = film?.longCut?.[side];
    return found && found.key === longKey(side) ? found : undefined;
  };
  const needLong = (side: 'wide' | 'tall') => shortOnly.length > 0 && !longOf(side);
  const masterLines = (side: 'wide' | 'tall') => {
    const set = orient === side ? film?.stills : film?.shelf?.orientation === side ? film.shelf.stills : undefined;
    return set?.length ? set.map((item) => item.text) : scriptLines;
  };
  const makeLong = async () => {
    if (!film) return;
    setBusy('longcut');
    setError('');
    try {
      const next: NonNullable<typeof film.longCut> = { ...(film.longCut ?? {}) };
      for (const side of (needsWide ? ['wide', 'tall'] : ['tall']) as ('wide' | 'tall')[]) {
        if (!ready(side === 'wide' ? wideRun : tallRun)) continue;
        setProgress(side === 'wide' ? 'Ghép video dài ngang…' : 'Ghép video dài dọc…');
        const result = await renderNovixaStockMotion({
          buildId,
          lines: masterLines(side),
          phase: 'longcut',
          allowVoice: false,
          orientation: side,
          shortOnly,
        });
        if (result.status !== 'ready' || !result.folder) throw new Error(result.message || 'Không ghép được video dài.');
        next[side] = { folder: result.folder.split(/[\\/]/).pop() || '', seconds: (result as { seconds?: number }).seconds ?? 0, key: longKey(side) };
      }
      onCommit({ ...state, filmOrchestration: { ...film, longCut: next } });
      message.success('Đã ghép video dài, bỏ các câu [ngắn]. Không tốn thêm tiền giọng.');
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không ghép được video dài.'));
    } finally {
      setBusy('');
      setProgress('');
    }
  };

  const rows = useMemo(() => {
    const long = publish?.long;
    const list: Row[] = [];
    // Kịch bản chưa có file .dang.json: tự soạn cho video dài từ lời đọc (bỏ các câu [ngắn]), giống video ngắn.
    const longLines = scriptLines.filter((_, index) => !shortOnly.includes(index));
    const own = longLines.length ? draftShortPublish({ lines: longLines, headline: null, filmTitle, long, brandCode, whole: true }) : null;
    if (needsWide) {
      list.push({
        key: 'wide',
        label: 'Bản dài ngang 16:9',
        channels: hasFanpage ? ['youtube_channel', 'facebook_page'] : ['youtube_channel'],
        kind: 'long',
        seq: 0,
        defaults: {
          title: long?.title || filmTitle,
          youtube: long?.youtubeDescription || own?.youtube || '',
          tiktok: '',
          facebook: long?.facebookCaption || own?.facebook || '',
          fbComment: long?.facebookComment || own?.fbComment || '',
        },
        ready: ready(wideRun) && !needLong('wide'),
        warnings: needLong('wide') ? ['video dài còn câu [ngắn]: bấm "Ghép video dài" trước'] : undefined,
        load: () => fetchNovixaVideo(buildId, longOf('wide')?.folder ?? folderOf(wideRun?.videoPath)),
      });
    }
    list.push({
      key: 'tall',
      label: needsWide ? 'Bản dài dọc 9:16' : 'Video dọc 9:16',
      channels: needsWide
        ? ['tiktok_channel']
        : hasFanpage
          ? ['youtube_channel', 'tiktok_channel', 'facebook_page']
          : ['youtube_channel', 'tiktok_channel'],
      kind: needsWide ? 'long' : 'short',
      seq: 1,
      defaults: {
        title: long?.title || filmTitle,
        youtube: needsWide ? '' : long?.youtubeDescription || own?.youtube || '',
        tiktok: long?.tiktokCaption || own?.tiktok || '',
        facebook: needsWide ? '' : long?.facebookCaption || own?.facebook || '',
        fbComment: needsWide ? '' : long?.facebookComment || own?.fbComment || '',
      },
      ready: ready(tallRun) && !(needsWide && needLong('tall')),
      warnings: needsWide && needLong('tall') ? ['video dài còn câu [ngắn]: bấm "Ghép video dài" trước'] : undefined,
      load: () => fetchNovixaVideo(buildId, (needsWide ? longOf('tall')?.folder : undefined) ?? folderOf(tallRun?.videoPath)),
    });
    for (const item of shorts?.items ?? []) {
      const parts = (item as { parts?: { from: number; to: number }[] | null }).parts;
      const spans = parts?.length ? parts : [{ from: (item as { from?: number }).from ?? 0, to: (item as { to?: number }).to ?? 0 }];
      const clipLines = spans.flatMap((part) => scriptLines.slice(part.from, part.to + 1));
      // Tự soạn từ chính các câu trong clip (không dùng nội dung viết tay theo số thứ tự, vì clip tự chia khác clip viết tay).
      const draft = clipLines.length
        ? draftShortPublish({ lines: clipLines, headline: item.title, filmTitle, long, brandCode })
        : null;
      const text = draft ? { title: draft.title, youtubeDescription: draft.youtube, tiktokCaption: draft.tiktok, facebookCaption: draft.facebook, facebookComment: draft.fbComment } : publish?.shorts?.find((s) => s.index === item.index);
      list.push({
        key: `short-${item.index}`,
        label: `Video ngắn ${item.index} · ${item.seconds}s`,
        channels: hasFanpage ? ['youtube_channel', 'tiktok_channel', 'facebook_page'] : ['youtube_channel', 'tiktok_channel'],
        kind: 'short',
        seq: 1 + item.index,
        defaults: {
          title: text?.title || (item.title ? `${item.title} | ${filmTitle}` : `${filmTitle} – video ngắn ${item.index}`),
          youtube: text?.youtubeDescription || '',
          tiktok: text?.tiktokCaption || '',
          facebook: text?.facebookCaption || '',
          fbComment: text?.facebookComment || '',
        },
        ready: true,
        warnings: draft?.warnings,
        load: () => fetchNovixaShort(buildId, shorts!.folder, item.index),
      });
    }
    return list;
  }, [buildId, brandCode, filmTitle, hasFanpage, needsWide, publish, scriptLines, shorts, tallRun, wideRun, film?.longCut, shortOnly, scriptKey]);

  const shapeOf = (row: Row): CoverShape => (row.key === 'wide' ? 'wide' : 'tall');
  const videoOf = async (row: Row) => {
    const hit = videoCache.current.get(row.key);
    if (hit) return hit;
    const blob = await row.load();
    videoCache.current.clear(); // giữ một video trong bộ nhớ là đủ
    videoCache.current.set(row.key, blob);
    return blob;
  };
  const keepCover = (key: string, cover: Cover) => {
    coverUrls.current.add(cover.url);
    setCovers((cur) => {
      const old = cur[key];
      if (old && old.url !== cover.url) {
        URL.revokeObjectURL(old.url);
        coverUrls.current.delete(old.url);
      }
      return { ...cur, [key]: cover };
    });
  };
  const buildCover = async (row: Row, video?: Blob): Promise<Cover> => {
    const opt = coverOpts[row.key] ?? {};
    const made = await makeCover(video ?? (await videoOf(row)), {
      shape: shapeOf(row),
      title: String(valueOf(row, 'title')),
      atSeconds: opt.atSeconds ?? undefined,
      withTitle: opt.withTitle !== false,
    });
    const cover: Cover = { blob: made.blob, url: URL.createObjectURL(made.blob), atSeconds: made.atSeconds };
    keepCover(row.key, cover);
    setCoverOpts((cur) => ({ ...cur, [row.key]: { ...cur[row.key], atSeconds: Math.round(made.atSeconds * 10) / 10 } }));
    return cover;
  };
  const makeRowCover = async (row: Row) => {
    setCoverBusy(row.key);
    setError('');
    try {
      await buildCover(row);
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không tạo được ảnh bìa từ video.'));
    } finally {
      setCoverBusy('');
    }
  };
  const pickRowCover = async (row: Row, file: File) => {
    setCoverBusy(row.key);
    setError('');
    try {
      const blob = await fitImage(file, shapeOf(row));
      const atSeconds = coverOpts[row.key]?.atSeconds ?? covers[row.key]?.atSeconds ?? 1;
      keepCover(row.key, { blob, url: URL.createObjectURL(blob), atSeconds, custom: true });
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không đọc được ảnh đã chọn (chỉ nhận JPG, PNG).'));
    } finally {
      setCoverBusy('');
    }
  };

  const plannedShorts = film?.shortPlans?.length ?? 0;
  const missingShorts = needsWide ? Math.max(0, plannedShorts - (shorts?.items.length ?? 0)) : 0;

  const valueOf = (row: Row, field: keyof Draft) => drafts[row.key]?.[field] ?? (field === 'title' ? exports[row.key]?.title : undefined) ?? row.defaults[field];
  const setField = (row: Row, field: keyof Draft, value: string) =>
    setDrafts((cur) => ({ ...cur, [row.key]: { ...cur[row.key], [field]: value } }));
  const scheduled = (row: Row) => Boolean(recOf(row.key)?.schedule?.length);
  const legacy = (row: Row) => !remoteOn && Boolean(exports[row.key]?.jobId && !exports[row.key]?.topicId);
  const pending = rows.filter((row) => row.ready && !scheduled(row) && !legacy(row) && !unchecked[row.key]);

  const problems = (row: Row) => {
    const list: string[] = [];
    if (!String(valueOf(row, 'title')).trim()) list.push('thiếu tiêu đề');
    list.push(...(row.warnings ?? []));
    if (row.channels.includes('youtube_channel')) {
      const text = String(valueOf(row, 'youtube'));
      if (!text.trim()) list.push('thiếu mô tả YouTube');
      else if (finance && !text.includes('không phải lời khuyên đầu tư')) list.push('mô tả YouTube thiếu câu miễn trừ');
    }
    if (row.channels.includes('facebook_page')) list.push(...checkFacebookPost(String(valueOf(row, 'facebook')), finance ? undefined : ''));
    if (finance && row.channels.includes('tiktok_channel') && String(valueOf(row, 'tiktok')).trim() && !String(valueOf(row, 'tiktok')).includes('không phải lời khuyên đầu tư')) {
      list.push('caption TikTok thiếu câu miễn trừ');
    }
    return list;
  };
  const blocking = pending.flatMap((row) => problems(row).map((p) => `${row.label}: ${p}`));

  const approveAll = async () => {
    if (!pending.length || blocking.length) return;
    if (remoteOn && !remoteReady) return;
    setBusy('approve');
    setError('');
    setSkipped([]);
    setWarnings([]);
    const acc: Record<string, FilmExportRecord> = { ...(state.filmOrchestration?.exports ?? {}) };
    const save = () => {
      const live = state.filmOrchestration;
      if (live) onCommit({ ...state, filmOrchestration: { ...live, exports: { ...acc } } });
    };
    // Bản ghi theo nơi đăng: máy chủ đăng bài (remote) hoặc API của máy này. Ghi lại sau mỗi bước để bấm lại làm tiếp, không tạo trùng.
    type Rec = { topicId?: string; uploaded?: boolean; extras?: boolean; schedule?: FilmExportRecord['schedule'] };
    const getRec = (key: string): Rec | undefined => (remoteOn ? acc[key]?.remote : acc[key]);
    const setRec = (key: string, title: string, patch: Rec) => {
      const base: FilmExportRecord = acc[key] ?? { title, at: new Date().toISOString() };
      acc[key] = remoteOn
        ? { ...base, remote: { ...base.remote, ...patch, brandId: remoteBrandId, at: new Date().toISOString() } }
        : { ...base, ...patch };
      save();
    };
    const targetBrand = remoteOn ? remoteBrandId : brandId;
    const where = remoteOn ? 'máy chủ' : 'lịch';
    const plans: TopicPublishPlanInput[] = [];
    const notes: string[] = [];
    try {
      for (const [i, row] of pending.entries()) {
        const title = String(valueOf(row, 'title')).trim();
        const step = `(${i + 1}/${pending.length})`;
        if (!getRec(row.key)?.topicId) {
          setProgress(`${step} Tạo bài «${title}» trên ${where}…`);
          const body = { brandId: targetBrand, title, status: 'Review', bodyOutline: `Video Studio · ${filmTitle} · ${row.label}` };
          const topic = remoteOn ? await remoteCreateTopic(body) : await createContentTopic(body);
          setRec(row.key, title, { topicId: topic.id });
        }
        const topicId = getRec(row.key)!.topicId!;
        let videoBlob: Blob | undefined;
        if (!getRec(row.key)?.uploaded) {
          const variants: ImportVariantItem[] = [];
          const youtube = String(valueOf(row, 'youtube')).trim();
          const tiktok = String(valueOf(row, 'tiktok')).trim();
          if (row.channels.includes('youtube_channel') && youtube) variants.push({ kind: 'youtube', title, bodyMarkdown: youtube });
          if (row.channels.includes('tiktok_channel') && tiktok) variants.push({ kind: 'video_caption', title, bodyMarkdown: tiktok });
          const facebook = String(valueOf(row, 'facebook')).trim();
          if (row.channels.includes('facebook_page') && facebook)
            variants.push({ kind: 'fb_page', title, bodyMarkdown: joinFacebookPost(facebook, String(valueOf(row, 'fbComment'))) });
          if (variants.length) {
            if (remoteOn) await remoteImportVariants(topicId, variants, 'Review');
            else await importTopicVariants(topicId, variants, 'Review');
          }
          setProgress(`${step} Lấy video «${title}» trên máy này…`);
          const blob = await videoOf(row);
          videoBlob = blob;
          const file = new File([blob], `${buildId}-${row.key}.mp4`, { type: 'video/mp4' });
          if (remoteOn) {
            const mb = Math.max(1, Math.round(blob.size / 1024 / 1024));
            setProgress(`${step} Gửi video «${title}» lên máy chủ (${mb} MB)…`);
            await remoteUploadVideo(topicId, file, (pct) => setProgress(`${step} Gửi video «${title}» lên máy chủ (${mb} MB): ${pct}%`));
          } else {
            setProgress(`${step} Đính video «${title}»…`);
            await uploadTopicVideo(topicId, file);
          }
          setRec(row.key, title, { uploaded: true });
        }
        if (!getRec(row.key)?.extras) {
          // Ảnh bìa (ngang cho YouTube/video Fanpage, dọc cho Shorts/Reels), danh sách phát, khung bìa TikTok.
          // Lỗi ở bước này không chặn đăng: video vẫn lên lịch, chỉ thiếu ảnh bìa.
          setProgress(`${step} Ảnh bìa và danh sách phát «${title}»…`);
          let cover: Cover | undefined = covers[row.key];
          if (!cover) {
            try {
              cover = await buildCover(row, videoBlob);
            } catch (reason) {
              notes.push(`${row.label}: chưa tạo được ảnh bìa (${apiErrorMessage(reason, 'lỗi đọc video')}), nền tảng sẽ tự lấy khung hình.`);
            }
          }
          const shape = shapeOf(row);
          const input = {
            thumbnail: cover && shape === 'wide' ? cover.blob : undefined,
            cover: cover && shape === 'tall' ? cover.blob : undefined,
            youtubePlaylist: row.channels.includes('youtube_channel') ? playlist.trim() : undefined,
            coverTimestampMs: Math.round((cover?.atSeconds ?? coverOpts[row.key]?.atSeconds ?? 1) * 1000),
          };
          try {
            if (remoteOn) await remotePublishExtras(topicId, input);
            else await savePublishExtras(topicId, input);
            setRec(row.key, title, { extras: true });
          } catch (reason) {
            notes.push(`${row.label}: chưa gửi được ảnh bìa/danh sách phát (${apiErrorMessage(reason, 'lỗi máy chủ')}). Video vẫn vào lịch.`);
          }
        }
        plans.push({ topicId, kind: row.kind, connectors: row.channels, seq: row.seq, sourceRef: `${buildId}:${row.key}` });
      }
      setProgress(remoteOn ? 'Máy chủ đang xếp lịch theo cài đặt…' : 'Agent đang xếp lịch theo cài đặt…');
      const result = remoteOn ? await remoteScheduleTopics(targetBrand, plans) : await scheduleTopicsWithAgent(brandId, plans);
      for (const row of pending) {
        const topicId = getRec(row.key)?.topicId;
        const slots = result.scheduled
          .filter((p) => p.topicId === topicId)
          .map((p) => ({ publishAt: p.publishAt, target: p.targetLabel, status: p.status }))
          .sort((a, b) => a.publishAt.localeCompare(b.publishAt));
        if (topicId && slots.length) setRec(row.key, acc[row.key]?.title ?? row.label, { schedule: slots });
      }
      save();
      setSkipped(result.skipped);
      setWarnings([...notes, ...result.warnings]);
      const count = result.scheduled.length;
      if (count) message.success(remoteOn ? `Đã đồng bộ lên máy chủ và xếp ${count} lượt đăng.` : `Đã xếp ${count} lượt đăng vào lịch của Agent.`);
      else message.warning('Chưa xếp được lượt nào. Xem lý do bên dưới.');
    } catch (reason) {
      save();
      setError(apiErrorMessage(reason, remoteOn
        ? 'Không đồng bộ được lên máy chủ. Bấm lại để làm tiếp từ chỗ dừng (phần đã gửi không gửi lại).'
        : 'Không đưa được phim vào lịch đăng. Bấm lại để làm tiếp từ chỗ dừng.'));
    } finally {
      setBusy('');
      setProgress('');
    }
  };

  const download = async (row: Row) => {
    setBusy(`dl-${row.key}`);
    try {
      const blob = await row.load();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${row.key}.mp4`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      setError('Không tải được video.');
    } finally {
      setBusy('');
    }
  };

  const statusTag = (row: Row) => {
    if (scheduled(row)) return <Tag color="green">{remoteOn ? 'Đã đồng bộ, đã vào lịch máy chủ' : 'Đã vào lịch'}</Tag>;
    if (legacy(row)) return <Tag>Đã đưa sang Video thành phẩm (cách cũ)</Tag>;
    if (recOf(row.key)?.uploaded) return <Tag color="orange">{remoteOn ? 'Đã gửi video, chưa xếp lịch' : 'Đã đính video, chưa xếp lịch'}</Tag>;
    if (recOf(row.key)?.topicId) return <Tag color="orange">{remoteOn ? 'Đã tạo bài trên máy chủ, chưa gửi video' : 'Đã tạo bài, chưa xếp lịch'}</Tag>;
    return row.ready ? <Tag color="blue">Sẵn sàng</Tag> : <Tag>Chưa dựng</Tag>;
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>5. Xuất &amp; đăng</Typography.Title>
        <Typography.Text type="secondary">
          Kiểm tra tiêu đề, mô tả, caption, ảnh bìa rồi bấm duyệt. Đây là lần duyệt cuối: Agent xếp các video vào khung trống
          tiếp theo theo cài đặt lịch của thương hiệu và duyệt luôn. Đến giờ, Agent đăng kèm tiêu đề, mô tả, thẻ, ảnh bìa,
          danh sách phát (YouTube), bình luận đầu (Fanpage). TikTok đăng thẳng kèm caption khi máy chủ đã bật đăng thẳng,
          chưa bật thì vào hộp nháp để anh dán caption.
        </Typography.Text>
      </div>
      {publish ? null : (
        <Alert type="info" showIcon message={'Kịch bản chưa có file nội dung đăng (.dang.json): các ô được tự soạn từ lời đọc của phim, không có nguồn riêng. Đọc lại trước khi duyệt, và thêm dòng "Nguồn: …, ngày …" nếu có số liệu.'} />
      )}
      {needsWide && shortOnly.length ? (
        needLong('wide') || needLong('tall') ? (
          <Alert
            type="warning"
            showIcon
            message={`Kịch bản có ${shortOnly.length} câu chỉ dành cho video ngắn ([ngắn]). Video dài chưa bỏ các câu này nên chưa đăng được.`}
            description="Ghép video dài từ bản đã dựng: chỉ cắt và nối, không tạo giọng mới nên không tốn thêm tiền giọng."
            action={<Button type="primary" loading={busy === 'longcut'} disabled={Boolean(busy)} onClick={() => void makeLong()}>Ghép video dài</Button>}
          />
        ) : (
          <Alert
            type="success"
            showIcon
            message={`Video dài đã bỏ ${shortOnly.length} câu [ngắn]${longOf('tall') ? `, dài ${Math.round(longOf('tall')!.seconds)} giây (bản dọc)` : ''}.`}
            action={<Button size="small" loading={busy === 'longcut'} disabled={Boolean(busy)} onClick={() => void makeLong()}>Ghép lại</Button>}
          />
        )
      ) : null}
      {missingShorts ? <Alert type="info" showIcon message={`Còn ${missingShorts} video ngắn chưa xuất (bước 4).`} /> : null}
      {remoteOn && remote ? <RemotePublishServerCard status={remote} /> : null}
      {rows.some((row) => row.channels.includes('youtube_channel')) ? (
        <Input
          addonBefore="Danh sách phát YouTube"
          placeholder="Để trống: Agent tự chọn danh sách hợp chủ đề hoặc tạo mới"
          maxLength={150}
          value={playlist}
          disabled={Boolean(busy)}
          onChange={(e) => {
            setPlaylist(e.target.value);
            writePlaylist(brandCode, e.target.value);
          }}
        />
      ) : null}
      {remoteOn && remoteBrandError ? <Alert type="error" showIcon message={remoteBrandError} /> : null}
      {error ? <Alert type="error" showIcon message={error} /> : null}

      {rows.map((row) => {
        const done = scheduled(row) || legacy(row);
        const locked = done || Boolean(busy);
        const rowProblems = row.ready && !done ? problems(row) : [];
        return (
          <Card
            key={row.key}
            size="small"
            title={
              <Space wrap>
                {!done ? (
                  <Checkbox
                    checked={row.ready && !unchecked[row.key]}
                    disabled={!row.ready || Boolean(busy)}
                    onChange={(e) => setUnchecked((cur) => ({ ...cur, [row.key]: !e.target.checked }))}
                  />
                ) : null}
                <span>{row.label}</span>
                {row.channels.map((c) => <Tag key={c}>{CHANNEL_TEXT[c]}</Tag>)}
                <Tag>{row.kind === 'long' ? 'Video dài' : 'Video ngắn'}</Tag>
                {statusTag(row)}
              </Space>
            }
            extra={<Button size="small" disabled={!row.ready || Boolean(busy)} loading={busy === `dl-${row.key}`} onClick={() => void download(row)}>Tải về</Button>}
          >
            <Space direction="vertical" size={8} style={{ width: '100%' }}>
              <Input addonBefore="Tiêu đề" maxLength={100} showCount value={String(valueOf(row, 'title'))} disabled={locked || Boolean(recOf(row.key)?.topicId)} onChange={(e) => setField(row, 'title', e.target.value)} />
              {row.channels.includes('youtube_channel') ? (
                <Input.TextArea
                  autoSize={{ minRows: 3, maxRows: 10 }}
                  placeholder={finance ? `Mô tả YouTube (kết bằng: ${DISCLAIMER})` : 'Mô tả YouTube'}
                  value={String(valueOf(row, 'youtube'))}
                  disabled={locked || Boolean(recOf(row.key)?.uploaded)}
                  onChange={(e) => setField(row, 'youtube', e.target.value)}
                />
              ) : null}
              {row.channels.includes('tiktok_channel') ? (
                <Input.TextArea
                  autoSize={{ minRows: 2, maxRows: 6 }}
                  placeholder="Caption TikTok"
                  value={String(valueOf(row, 'tiktok'))}
                  disabled={locked || Boolean(recOf(row.key)?.uploaded)}
                  onChange={(e) => setField(row, 'tiktok', e.target.value)}
                />
              ) : null}
              {row.channels.includes('facebook_page') ? (
                <>
                  <Input.TextArea
                    autoSize={{ minRows: 3, maxRows: 10 }}
                    placeholder="Nội dung Fanpage: dòng đầu là câu móc (≤125 ký tự), đoạn ngắn, câu hỏi cuối bài, 3–5 hashtag, câu miễn trừ. Không để link."
                    value={String(valueOf(row, 'facebook'))}
                    disabled={locked || Boolean(recOf(row.key)?.uploaded)}
                    onChange={(e) => setField(row, 'facebook', e.target.value)}
                  />
                  <Input.TextArea
                    autoSize={{ minRows: 1, maxRows: 4 }}
                    placeholder="Bình luận đầu trên Fanpage (nguồn, link bản đầy đủ). Không bắt buộc."
                    value={String(valueOf(row, 'fbComment'))}
                    disabled={locked || Boolean(recOf(row.key)?.uploaded)}
                    onChange={(e) => setField(row, 'fbComment', e.target.value)}
                  />
                </>
              ) : null}
              {row.ready ? (
                <Space wrap align="start">
                  {covers[row.key] ? (
                    <img
                      src={covers[row.key].url}
                      alt="Ảnh bìa"
                      style={{ height: shapeOf(row) === 'wide' ? 90 : 160, borderRadius: 6, border: '1px solid rgba(128,128,128,0.35)' }}
                    />
                  ) : null}
                  {!done && !recOf(row.key)?.extras ? (
                    <Space direction="vertical" size={4}>
                      <Typography.Text type="secondary">
                        {shapeOf(row) === 'wide' ? 'Ảnh bìa ngang (YouTube, video Fanpage)' : 'Ảnh bìa dọc (Shorts, Reels); giây lấy khung cũng là ảnh bìa TikTok'}
                        {covers[row.key] ? '' : '. Chưa tạo: khi duyệt sẽ tự lấy khung khoảng 1/3 video và chèn tiêu đề.'}
                      </Typography.Text>
                      <Space wrap>
                        <InputNumber
                          size="small"
                          min={0}
                          step={0.5}
                          addonBefore="Khung giây"
                          placeholder="tự chọn"
                          value={coverOpts[row.key]?.atSeconds ?? null}
                          disabled={Boolean(busy) || Boolean(coverBusy)}
                          onChange={(v) => setCoverOpts((cur) => ({ ...cur, [row.key]: { ...cur[row.key], atSeconds: typeof v === 'number' ? v : null } }))}
                        />
                        <Checkbox
                          checked={coverOpts[row.key]?.withTitle !== false}
                          disabled={Boolean(busy) || Boolean(coverBusy)}
                          onChange={(e) => setCoverOpts((cur) => ({ ...cur, [row.key]: { ...cur[row.key], withTitle: e.target.checked } }))}
                        >
                          Chèn tiêu đề
                        </Checkbox>
                        <Button size="small" loading={coverBusy === row.key} disabled={Boolean(busy) || (Boolean(coverBusy) && coverBusy !== row.key)} onClick={() => void makeRowCover(row)}>
                          {covers[row.key] ? 'Tạo lại ảnh bìa' : 'Tạo ảnh bìa'}
                        </Button>
                        <Upload
                          accept="image/jpeg,image/png"
                          showUploadList={false}
                          disabled={Boolean(busy) || Boolean(coverBusy)}
                          beforeUpload={(file) => {
                            void pickRowCover(row, file);
                            return false;
                          }}
                        >
                          <Button size="small" disabled={Boolean(busy) || Boolean(coverBusy)}>Dùng ảnh có sẵn</Button>
                        </Upload>
                      </Space>
                    </Space>
                  ) : recOf(row.key)?.extras ? (
                    <Tag color="green">Đã gửi ảnh bìa</Tag>
                  ) : null}
                </Space>
              ) : null}
              {rowProblems.length ? <Typography.Text type="danger">Cần sửa: {rowProblems.join('; ')}</Typography.Text> : null}
              {recOf(row.key)?.schedule?.length ? (
                <Space wrap>
                  {recOf(row.key)!.schedule!.map((slot) => (
                    <Tag key={`${slot.target}-${slot.publishAt}`} color={slot.status === 'Approved' ? 'green' : 'gold'}>
                      {vnTime(slot.publishAt)} · {slot.target}
                    </Tag>
                  ))}
                </Space>
              ) : null}
            </Space>
          </Card>
        );
      })}

      {skipped.length ? (
        <Alert
          type="warning"
          showIcon
          message="Một số video chưa xếp được"
          description={<ul style={{ margin: 0, paddingLeft: 18 }}>{skipped.map((s) => <li key={s.topicId}><strong>{s.title}</strong>: {s.reason}</li>)}</ul>}
        />
      ) : null}
      {warnings.map((w) => <Alert key={w} type="info" showIcon message={w} />)}
      {blocking.length ? <Alert type="warning" showIcon message="Chưa duyệt được" description={blocking.join(' · ')} /> : null}

      <Space wrap>
        <Popconfirm
          title={remoteOn ? 'Duyệt và đồng bộ lên máy chủ?' : 'Duyệt và đưa vào lịch đăng?'}
          description={
            remoteOn
              ? `${pending.length} video thành phẩm và nội dung đăng sẽ được gửi lên máy chủ; máy chủ xếp lịch, duyệt luôn và tự đăng đúng giờ.`
              : `${pending.length} video sẽ được tạo bài, đính video và Agent xếp lịch, duyệt luôn. YouTube tự đăng đúng giờ.`
          }
          okText="Duyệt"
          cancelText="Thôi"
          disabled={!pending.length || Boolean(blocking.length) || Boolean(busy) || (remoteOn && !remoteReady)}
          onConfirm={() => void approveAll()}
        >
          <Button
            type="primary"
            disabled={!pending.length || Boolean(blocking.length) || Boolean(busy) || (remoteOn && !remoteReady)}
            loading={busy === 'approve'}
          >
            {remoteOn ? `Duyệt và đồng bộ lên máy chủ (${pending.length})` : `Duyệt và đưa vào lịch đăng (${pending.length})`}
          </Button>
        </Popconfirm>
        {progress ? <Typography.Text type="secondary">{progress}</Typography.Text> : null}
        {remoteOn && remote?.baseUrl ? (
          <Typography.Link href={`${remote.baseUrl}/content/calendar?tab=duyet${remoteBrandId ? `&brand=${remoteBrandId}` : ''}`} target="_blank">
            Xem lịch trên máy chủ
          </Typography.Link>
        ) : (
          <Typography.Link href={`/content/calendar?tab=duyet&brand=${brandId}`}>Xem trong Lịch đăng</Typography.Link>
        )}
      </Space>
    </Space>
  );
}
