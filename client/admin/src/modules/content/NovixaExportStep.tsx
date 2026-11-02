import { useEffect, useMemo, useState } from 'react';
import { Alert, App, Button, Card, Checkbox, Input, Popconfirm, Space, Tag, Typography } from 'antd';
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
import { importTopicVariants, uploadTopicVideo, type ImportVariantItem } from '@/shared/api/content-youtube.api';
import { scheduleTopicsWithAgent, type AgentSkipped, type TopicPublishPlanInput } from '@/shared/api/content-agent.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { SeriesPilotState } from './content-famixa-series';
import type { FilmExportRecord, NovixaMotionRun } from './film-production-orchestrator';
import { checkFacebookPost, joinFacebookPost } from './facebook-post';
import { draftShortPublish } from './novixa-publish-draft';
import { filmScriptHash, isShortOnlyLine, spokenLine } from './film-production-orchestrator';
import { shortOnlyLines } from './novixa-shorts';

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

const DISCLAIMER = 'Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.';
const CHANNEL_TEXT: Record<Channel, string> = { youtube_channel: 'YouTube', tiktok_channel: 'TikTok (hộp nháp)', facebook_page: 'Fanpage (đăng tay theo lịch)' };

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
    void fetchContentChannels(brandId)
      .then((ch) => !cancelled && setHasFanpage(ch.some((c) => c.isActive && c.channelType === 'facebook_page')))
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [brandId]);

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
    if (needsWide) {
      list.push({
        key: 'wide',
        label: 'Bản dài ngang 16:9',
        channels: hasFanpage ? ['youtube_channel', 'facebook_page'] : ['youtube_channel'],
        kind: 'long',
        seq: 0,
        defaults: {
          title: long?.title || filmTitle,
          youtube: long?.youtubeDescription || '',
          tiktok: '',
          facebook: long?.facebookCaption || '',
          fbComment: long?.facebookComment || '',
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
        youtube: needsWide ? '' : long?.youtubeDescription || '',
        tiktok: long?.tiktokCaption || '',
        facebook: needsWide ? '' : long?.facebookCaption || '',
        fbComment: needsWide ? '' : long?.facebookComment || '',
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
        ? draftShortPublish({ lines: clipLines, headline: item.title, filmTitle, long })
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
  }, [buildId, filmTitle, hasFanpage, needsWide, publish, scriptLines, shorts, tallRun, wideRun, film?.longCut, shortOnly, scriptKey]);

  const plannedShorts = film?.shortPlans?.length ?? 0;
  const missingShorts = needsWide ? Math.max(0, plannedShorts - (shorts?.items.length ?? 0)) : 0;

  const valueOf = (row: Row, field: keyof Draft) => drafts[row.key]?.[field] ?? (field === 'title' ? exports[row.key]?.title : undefined) ?? row.defaults[field];
  const setField = (row: Row, field: keyof Draft, value: string) =>
    setDrafts((cur) => ({ ...cur, [row.key]: { ...cur[row.key], [field]: value } }));
  const scheduled = (row: Row) => Boolean(exports[row.key]?.schedule?.length);
  const legacy = (row: Row) => Boolean(exports[row.key]?.jobId && !exports[row.key]?.topicId);
  const pending = rows.filter((row) => row.ready && !scheduled(row) && !legacy(row) && !unchecked[row.key]);

  const problems = (row: Row) => {
    const list: string[] = [];
    if (!String(valueOf(row, 'title')).trim()) list.push('thiếu tiêu đề');
    list.push(...(row.warnings ?? []));
    if (row.channels.includes('youtube_channel')) {
      const text = String(valueOf(row, 'youtube'));
      if (!text.trim()) list.push('thiếu mô tả YouTube');
      else if (!text.includes('không phải lời khuyên đầu tư')) list.push('mô tả YouTube thiếu câu miễn trừ');
    }
    if (row.channels.includes('facebook_page')) list.push(...checkFacebookPost(String(valueOf(row, 'facebook'))));
    if (row.channels.includes('tiktok_channel') && String(valueOf(row, 'tiktok')).trim() && !String(valueOf(row, 'tiktok')).includes('không phải lời khuyên đầu tư')) {
      list.push('caption TikTok thiếu câu miễn trừ');
    }
    return list;
  };
  const blocking = pending.flatMap((row) => problems(row).map((p) => `${row.label}: ${p}`));

  const approveAll = async () => {
    if (!pending.length || blocking.length) return;
    setBusy('approve');
    setError('');
    setSkipped([]);
    setWarnings([]);
    const acc: Record<string, FilmExportRecord> = { ...(state.filmOrchestration?.exports ?? {}) };
    const save = () => {
      const live = state.filmOrchestration;
      if (live) onCommit({ ...state, filmOrchestration: { ...live, exports: { ...acc } } });
    };
    const plans: TopicPublishPlanInput[] = [];
    try {
      for (const [i, row] of pending.entries()) {
        const title = String(valueOf(row, 'title')).trim();
        let record = acc[row.key];
        if (!record?.topicId) {
          setProgress(`(${i + 1}/${pending.length}) Tạo bài «${title}»…`);
          const topic = await createContentTopic({ brandId, title, status: 'Review', bodyOutline: `Video Studio · ${filmTitle} · ${row.label}` });
          record = { title, at: new Date().toISOString(), topicId: topic.id };
          acc[row.key] = record;
          save();
        }
        if (!record.uploaded) {
          const variants: ImportVariantItem[] = [];
          const youtube = String(valueOf(row, 'youtube')).trim();
          const tiktok = String(valueOf(row, 'tiktok')).trim();
          if (row.channels.includes('youtube_channel') && youtube) variants.push({ kind: 'youtube', title, bodyMarkdown: youtube });
          if (row.channels.includes('tiktok_channel') && tiktok) variants.push({ kind: 'video_caption', title, bodyMarkdown: tiktok });
          const facebook = String(valueOf(row, 'facebook')).trim();
          if (row.channels.includes('facebook_page') && facebook)
            variants.push({ kind: 'fb_page', title, bodyMarkdown: joinFacebookPost(facebook, String(valueOf(row, 'fbComment'))) });
          if (variants.length) await importTopicVariants(record.topicId!, variants, 'Review');
          setProgress(`(${i + 1}/${pending.length}) Đính video «${title}»…`);
          const blob = await row.load();
          await uploadTopicVideo(record.topicId!, new File([blob], `${buildId}-${row.key}.mp4`, { type: 'video/mp4' }));
          record = { ...record, uploaded: true };
          acc[row.key] = record;
          save();
        }
        plans.push({ topicId: record.topicId!, kind: row.kind, connectors: row.channels, seq: row.seq, sourceRef: `${buildId}:${row.key}` });
      }
      setProgress('Agent đang xếp lịch theo cài đặt…');
      const result = await scheduleTopicsWithAgent(brandId, plans);
      for (const row of pending) {
        const record = acc[row.key];
        const slots = result.scheduled
          .filter((p) => p.topicId === record?.topicId)
          .map((p) => ({ publishAt: p.publishAt, target: p.targetLabel, status: p.status }))
          .sort((a, b) => a.publishAt.localeCompare(b.publishAt));
        if (record && slots.length) acc[row.key] = { ...record, schedule: slots };
      }
      save();
      setSkipped(result.skipped);
      setWarnings(result.warnings);
      const count = result.scheduled.length;
      if (count) message.success(`Đã xếp ${count} lượt đăng vào lịch của Agent.`);
      else message.warning('Agent chưa xếp được lượt nào. Xem lý do bên dưới.');
    } catch (reason) {
      save();
      setError(apiErrorMessage(reason, 'Không đưa được phim vào lịch đăng. Bấm lại để làm tiếp từ chỗ dừng.'));
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
    if (scheduled(row)) return <Tag color="green">Đã vào lịch</Tag>;
    if (legacy(row)) return <Tag>Đã đưa sang Video thành phẩm (cách cũ)</Tag>;
    if (exports[row.key]?.topicId) return <Tag color="orange">Đã tạo bài, chưa xếp lịch</Tag>;
    return row.ready ? <Tag color="blue">Sẵn sàng</Tag> : <Tag>Chưa dựng</Tag>;
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>5. Xuất &amp; đăng</Typography.Title>
        <Typography.Text type="secondary">
          Kiểm tra tiêu đề, mô tả, caption rồi bấm "Duyệt và đưa vào lịch đăng". Đây là lần duyệt cuối: Agent xếp các video
          vào khung trống tiếp theo theo cài đặt lịch của thương hiệu (sau các bài đã có lịch) và duyệt luôn. YouTube tự đăng
          đúng giờ; TikTok vào hộp nháp, anh dán caption khi đăng.
        </Typography.Text>
      </div>
      {publish ? null : (
        <Alert type="info" showIcon message="Kịch bản chưa có file nội dung đăng (.dang.json), các ô được điền tạm từ tên phim. Nhớ thêm câu miễn trừ." />
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
              <Input addonBefore="Tiêu đề" maxLength={100} showCount value={String(valueOf(row, 'title'))} disabled={locked || Boolean(exports[row.key]?.topicId)} onChange={(e) => setField(row, 'title', e.target.value)} />
              {row.channels.includes('youtube_channel') ? (
                <Input.TextArea
                  autoSize={{ minRows: 3, maxRows: 10 }}
                  placeholder={`Mô tả YouTube (kết bằng: ${DISCLAIMER})`}
                  value={String(valueOf(row, 'youtube'))}
                  disabled={locked || Boolean(exports[row.key]?.uploaded)}
                  onChange={(e) => setField(row, 'youtube', e.target.value)}
                />
              ) : null}
              {row.channels.includes('tiktok_channel') ? (
                <Input.TextArea
                  autoSize={{ minRows: 2, maxRows: 6 }}
                  placeholder="Caption TikTok (để dán khi đăng từ hộp nháp)"
                  value={String(valueOf(row, 'tiktok'))}
                  disabled={locked || Boolean(exports[row.key]?.uploaded)}
                  onChange={(e) => setField(row, 'tiktok', e.target.value)}
                />
              ) : null}
              {row.channels.includes('facebook_page') ? (
                <>
                  <Input.TextArea
                    autoSize={{ minRows: 3, maxRows: 10 }}
                    placeholder="Nội dung Fanpage: dòng đầu là câu móc (≤125 ký tự), đoạn ngắn, câu hỏi cuối bài, 3–5 hashtag, câu miễn trừ. Không để link."
                    value={String(valueOf(row, 'facebook'))}
                    disabled={locked || Boolean(exports[row.key]?.uploaded)}
                    onChange={(e) => setField(row, 'facebook', e.target.value)}
                  />
                  <Input.TextArea
                    autoSize={{ minRows: 1, maxRows: 4 }}
                    placeholder="Bình luận đầu trên Fanpage (nguồn, link bản đầy đủ). Không bắt buộc."
                    value={String(valueOf(row, 'fbComment'))}
                    disabled={locked || Boolean(exports[row.key]?.uploaded)}
                    onChange={(e) => setField(row, 'fbComment', e.target.value)}
                  />
                </>
              ) : null}
              {rowProblems.length ? <Typography.Text type="danger">Cần sửa: {rowProblems.join('; ')}</Typography.Text> : null}
              {exports[row.key]?.schedule?.length ? (
                <Space wrap>
                  {exports[row.key]!.schedule!.map((slot) => (
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
          title="Duyệt và đưa vào lịch đăng?"
          description={`${pending.length} video sẽ được tạo bài, đính video và Agent xếp lịch, duyệt luôn. YouTube tự đăng đúng giờ.`}
          okText="Duyệt"
          cancelText="Thôi"
          disabled={!pending.length || Boolean(blocking.length) || Boolean(busy)}
          onConfirm={() => void approveAll()}
        >
          <Button type="primary" disabled={!pending.length || Boolean(blocking.length) || Boolean(busy)} loading={busy === 'approve'}>
            Duyệt và đưa vào lịch đăng ({pending.length})
          </Button>
        </Popconfirm>
        {progress ? <Typography.Text type="secondary">{progress}</Typography.Text> : null}
        <Typography.Link href={`/content/calendar?tab=duyet&brand=${brandId}`}>Xem trong Lịch đăng</Typography.Link>
      </Space>
    </Space>
  );
}
