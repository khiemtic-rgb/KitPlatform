import { useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { Alert, Button, Checkbox, Segmented, Select, Space, Tag, Typography } from 'antd';
import {
  fetchContentSeriesVoicePreview,
  fetchContentSeriesVoices,
  fetchNovixaMotion,
  fetchNovixaStill,
  fetchNovixaVideo,
  renderNovixaGeminiStill,
  renderNovixaStockMotion,
  type ContentSeriesVoice,
} from '@/shared/api/content.api';
import type { SeriesPilotState } from './content-famixa-series';
import {
  NOVIXA_VOICE,
  adoptNovixaStatus,
  applyNovixaMotion,
  mergeNovixaStills,
  describeClientError,
  novixaClientOutcome,
  novixaInputKey,
  novixaStillBrandStale,
  novixaUnits,
  novixaVideoCurrent,
  novixaVideoGate,
  type NovixaBrandProfile,
  type NovixaMotionRun,
  type NovixaStill,
} from './film-production-orchestrator';
import type { ScriptScene } from './novixa-shorts';

function folderOf(path?: string | null) {
  const parts = (path || '').split(/[/\\]/).filter(Boolean);
  return parts.length > 1 ? parts[parts.length - 2] : '';
}

function scriptOf(state: SeriesPilotState) {
  return state.filmOrchestration?.script || state.scriptDraft || state.packDraft || '';
}

function pictureCards(stills: NovixaStill[], pictureKey: Record<number, string>) {
  const seen = new Set<string>();
  const cards: NovixaStill[][] = [];
  for (const still of stills) {
    const key = pictureKey[still.index];
    if (!key) {
      cards.push([still]);
      continue;
    }
    if (seen.has(key)) continue;
    seen.add(key);
    cards.push(stills.filter((item) => pictureKey[item.index] === key));
  }
  return cards;
}

export function ContentNovixaStages({
  buildId,
  state,
  steps,
  onCommit,
  brand,
  section = 'pictures',
  scenes = [],
}: {
  buildId: string;
  state: SeriesPilotState;
  steps: string[];
  onCommit: (next: SeriesPilotState) => void;
  brand?: NovixaBrandProfile | null;
  /** Bước 2 (Hình) hay bước 3 (Giọng & video). Cùng một component để giữ ảnh và tác vụ khi đổi bước. */
  section?: 'pictures' | 'video';
  scenes?: ScriptScene[];
}) {
  const stills = state.filmOrchestration?.stills ?? [];
  const voice = state.filmOrchestration?.voiceChoice;
  const motion = state.filmOrchestration?.motion;
  const [busy, setBusy] = useState('');
  const [pictures, setPictures] = useState<Record<number, string>>({});
  const [pictureKey, setPictureKey] = useState<Record<number, string>>({});
  const [videoUrl, setVideoUrl] = useState('');
  const [previewUrl, setPreviewUrl] = useState('');
  const [note, setNote] = useState(motion?.message || '');
  const [voices, setVoices] = useState<ContentSeriesVoice[]>([]);
  const brandVoice = brand?.voice?.voiceId ? brand.voice : null;
  const selectedVoiceId = voice?.voiceId || brandVoice?.voiceId || NOVIXA_VOICE.voiceId;
  // Settings follow the brand voice only while that voice is selected; another voice keeps the proven defaults.
  const voiceSettings = brandVoice && selectedVoiceId === brandVoice.voiceId
    ? { stability: brandVoice.stability, similarityBoost: brandVoice.similarityBoost, style: brandVoice.style, speed: brandVoice.speed }
    : null;
  const [changingVoice, setChangingVoice] = useState(false);
  // Giọng thương hiệu đã cố định: dùng luôn, không bắt xác nhận lại cho từng phim.
  const withBrandVoice = (next: SeriesPilotState): SeriesPilotState => {
    const film = next.filmOrchestration;
    if (!brandVoice?.voiceId || !film || film.voiceChoice?.voiceId) return next;
    return { ...next, filmOrchestration: { ...film, voiceChoice: { model: NOVIXA_VOICE.model, voiceId: brandVoice.voiceId, approved: true, previewPath: null } } };
  };
  const gate = novixaVideoGate(withBrandVoice(state));
  const loaded = useRef(false);
  const playerRef = useRef<HTMLDivElement>(null);
  const stillKey = stills.map((item) => `${item.index}:${item.path}`).join('|');

  const save = (raw: SeriesPilotState) => {
    const next = withBrandVoice(raw);
    const currentMotion = next.filmOrchestration?.motion;
    onCommit(currentMotion ? applyNovixaMotion(next, steps, currentMotion) : next);
  };

  const adopt = (incoming: NovixaMotionRun, withStills?: NovixaStill[]) => {
    const motionNext = adoptNovixaStatus(state.filmOrchestration?.motion, incoming);
    const merged = withStills ? mergeNovixaStills(stills, withStills) : stills;
    save(applyNovixaMotion({
      ...state,
      filmOrchestration: state.filmOrchestration
        ? { ...state.filmOrchestration, motion: motionNext, stills: withStills ? merged : state.filmOrchestration.stills }
        : state.filmOrchestration,
    }, steps, motionNext));
    setNote(motionNext.message);
  };

  useEffect(() => {
    let cancelled = false;
    void fetchContentSeriesVoices()
      .then((rows) => {
        if (!cancelled) setVoices(rows);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (loaded.current) return undefined;
    loaded.current = true;
    let cancelled = false;
    void fetchNovixaMotion(buildId).then((remote) => {
      if (cancelled || !remote || remote.status === 'job_missing') return;
      const incoming: NovixaMotionRun = {
        status: remote.status,
        message: remote.message,
        folder: remote.folder,
        videoPath: remote.videoPath,
        phase: remote.phase,
      };
      // Máy chủ trả bản dựng mới nhất của phim, không phân biệt khung ngang/dọc.
      // Chỉ nhận khi bản này đang chờ một tác vụ, hoặc phim cũ chưa từng tách hai khung.
      const film = state.filmOrchestration;
      const legacy = !film?.stillOrientation && !film?.shelf;
      const waiting = motion?.status === 'running';
      // Video đã dựng xong trên máy nhưng trang này chưa ghi nhận (hết thời gian chờ, tải lại trang): nhận lại nếu đúng khung hiện tại.
      const shape = /^(\d+)x(\d+)$/.exec(remote.format ?? '');
      const sameShape = Boolean(shape) && (Number(shape![1]) > Number(shape![2]) ? 'wide' : 'tall') === orientation;
      const finished = remote.status === 'ready' && Boolean(remote.videoPath) && motion?.status !== 'ready' && sameShape;
      if (!waiting && !legacy && !finished) return;
      const remoteStills = (remote.stills ?? []).map((item) => ({ ...item, approved: false }));
      adopt(incoming, legacy && !finished && remoteStills.length ? mergeNovixaStills(stills, remoteStills) : undefined);
    }).catch(() => undefined);
    return () => {
      cancelled = true;
    };
    // Load the existing task once. Later edits stay local until the user continues.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [buildId]);

  useEffect(() => {
    let cancelled = false;
    const urls: string[] = [];
    void (async () => {
      const next: Record<number, string> = {};
      const keys: Record<number, string> = {};
      for (const still of stills) {
        const folder = folderOf(still.path);
        if (!folder) continue;
        try {
          const file = still.path.split(/[/\\]/).pop();
          const blob = await fetchNovixaStill(buildId, still.index, folder, file);
          const bytes = new Uint8Array(await blob.arrayBuffer());
          let hash = 2166136261;
          for (let i = 0; i < bytes.length; i += 97) hash = Math.imul(hash ^ bytes[i], 16777619);
          keys[still.index] = `${blob.size}:${hash >>> 0}`;
          const url = URL.createObjectURL(new Blob([bytes], { type: blob.type || 'image/png' }));
          urls.push(url);
          next[still.index] = url;
        } catch {
          /* the file may still be on disk while the page reloads */
        }
      }
      if (!cancelled) {
        setPictures(next);
        setPictureKey(keys);
      }
    })();
    return () => {
      cancelled = true;
      urls.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [buildId, stillKey]);

  useEffect(() => {
    let cancelled = false;
    const folder = folderOf(motion?.videoPath);
    if (motion?.status !== 'ready' || !folder) return undefined;
    void fetchNovixaVideo(buildId, folder).then((blob) => {
      if (cancelled) return;
      setVideoUrl(URL.createObjectURL(blob));
    }).catch(() => {
      if (!cancelled) setNote('Video đã có trên máy nhưng trang chưa mở được file.');
    });
    return () => {
      cancelled = true;
    };
  }, [buildId, motion?.status, motion?.videoPath]);

  const fromScript = novixaUnits(scriptOf(state)).map((unit) => unit.text);
  const lines = fromScript.length ? fromScript : stills.map((item) => item.text);
  // Bộ ảnh đang hiển thị có hướng riêng. Chưa có ảnh: video dài (từ 12 câu) mặc định ngang 16:9, còn lại dọc 9:16.
  const orientation: 'wide' | 'tall' = state.filmOrchestration?.stillOrientation ?? (stills.length ? 'tall' : lines.length >= 12 ? 'wide' : 'tall');

  // Video đã dựng xong trên máy nhưng trang chưa ghi nhận (hết thời gian chờ, tải lại trang): nhận lại.
  // Chạy lại khi dữ liệu phim đã nạp xong hoặc đổi khung; không huỷ giữa chừng (StrictMode mount hai lần).
  const recoveredKey = useRef('');
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const hydrated = Boolean(state.filmOrchestration);
  useEffect(() => {
    if (!hydrated || motion?.status === 'ready' || motion?.status === 'running') return;
    const key = `${buildId}:${orientation}`;
    if (recoveredKey.current === key) return;
    recoveredKey.current = key;
    void fetchNovixaMotion(buildId).then((remote) => {
      if (!mounted.current || !remote || remote.status !== 'ready' || !remote.videoPath) return;
      const shape = /^(\d+)x(\d+)$/.exec(remote.format ?? '');
      if (!shape || (Number(shape[1]) > Number(shape[2]) ? 'wide' : 'tall') !== orientation) return;
      adopt({ status: 'ready', message: remote.message, folder: remote.folder, videoPath: remote.videoPath, phase: remote.phase });
    }).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [buildId, orientation, hydrated, motion?.status]);
  const shelf = state.filmOrchestration?.shelf;
  const nameOf = (value: 'wide' | 'tall') => (value === 'wide' ? 'ngang' : 'dọc');

  // Đổi hướng: cất bộ ảnh + video hiện tại, lấy bộ của hướng mới (nếu có). Không bao giờ ghi đè bộ đã cất.
  const switchOrientation = (next: 'wide' | 'tall') => {
    const film = state.filmOrchestration;
    if (!film || next === orientation) return;
    const restore = shelf && shelf.orientation === next ? shelf : null;
    save({
      ...state,
      filmOrchestration: {
        ...film,
        stillOrientation: next,
        stills: restore ? restore.stills : [],
        motion: restore ? restore.motion : undefined,
        shelf: stills.length || motion ? { orientation, stills, motion } : undefined,
      },
    });
    setVideoUrl('');
    setNote(restore ? `Đã mở lại bộ ảnh ${nameOf(next)} đã cất.` : `Bộ ảnh ${nameOf(next)} còn trống. Bộ ảnh ${nameOf(orientation)} vừa rồi được cất lại, không bị ghi đè.`);
  };

  const run = async (phase: 'stills' | 'video', extra?: { redrawIndex?: number; variant?: number; force?: boolean; resumeOnly?: boolean }) => {
    const jobLines = phase === 'stills' && extra?.redrawIndex == null && fromScript.length
      ? fromScript
      : (stills.length ? stills.map((item) => item.text) : fromScript);
    if (phase === 'stills' || phase === 'video') {
      let current: Awaited<ReturnType<typeof fetchNovixaMotion>> | null = null;
      try {
        current = await fetchNovixaMotion(buildId);
      } catch {
        if (phase === 'video') {
          adopt({ status: 'running', message: 'Chưa kiểm tra được tác vụ hiện có. Không tạo lượt giọng mới.' });
          return;
        }
      }
      if (current?.status === 'running') {
        adopt({ status: 'running', message: current.message || 'Đang chạy. Không tạo lượt mới.', folder: current.folder, videoPath: current.videoPath });
        return;
      }
      if (phase === 'video' && extra?.resumeOnly) {
        setNote(novixaVideoCurrent(withBrandVoice(state)) ? 'Video đã có. Không tạo lượt giọng mới.' : 'Ảnh hoặc giọng đã đổi. Bấm Tạo video để dựng lại.');
        return;
      }
      const readyVideo = Boolean(motion?.status === 'ready' && motion.videoPath);
      const sameTake = novixaVideoCurrent(withBrandVoice(state)) || (readyVideo && !motion?.inputKey);
      if (phase === 'video' && readyVideo && !extra?.force && (sameTake || gate)) {
        setNote('Video đã có. Không cần tạo lại ảnh, không tạo lượt giọng mới.');
        playerRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return;
      }
      if (phase === 'video' && gate) {
        setNote(gate);
        return;
      }
    }
    const redrawOne = extra?.redrawIndex != null;
    setBusy(redrawOne ? `still-${extra.redrawIndex}` : phase);
    // Bản đã dựng (hướng kia, hoặc lần trước của hướng này) cùng giọng và cùng lời: dùng lại giọng, không gọi ElevenLabs lần nữa.
    // Máy chủ kiểm tra lại lời, giọng và thông số giọng trước khi dùng.
    const voicePrefix = `${selectedVoiceId}\n${jobLines.join('\n')}\n`;
    const reuseFolder = phase === 'video'
      ? [shelf?.motion, motion].find((run) => run?.status === 'ready' && run.folder && run.inputKey?.startsWith(voicePrefix))?.folder ?? null
      : null;
    try {
      const result = await renderNovixaStockMotion({
        buildId,
        lines: jobLines,
        phase,
        allowVoice: phase !== 'stills',
        voiceId: selectedVoiceId,
        voiceSettings: phase === 'video' ? voiceSettings : undefined,
        stills: phase === 'video' ? stills : undefined,
        keep: extra?.redrawIndex == null ? stills : stills.filter((item) => item.index !== extra.redrawIndex),
        redrawIndex: extra?.redrawIndex,
        variant: extra?.variant,
        orientation,
        force: extra?.force,
        reuseFolder,
        brand,
      });
      if (phase === 'stills' && result.stills) {
        const fresh = extra?.redrawIndex == null && extra?.variant != null;
        const merged = mergeNovixaStills(stills, result.stills.map((item) => ({ ...item, approved: false })), extra?.redrawIndex, fresh);
        save({
          ...state,
          filmOrchestration: state.filmOrchestration ? { ...state.filmOrchestration, stills: merged, stillOrientation: orientation } : state.filmOrchestration,
        });
        setNote(redrawOne ? `Đã vẽ lại ảnh ${Number(extra?.redrawIndex) + 1}. Các ảnh khác giữ nguyên.` : result.message || 'Giữ ảnh đã có. Chỉ vẽ câu chưa có ảnh.');
        return;
      }
      adopt({
        status: result.status,
        message: result.message,
        folder: result.folder,
        videoPath: result.videoPath,
        phase: result.phase,
        inputKey: result.status === 'ready' ? novixaInputKey(jobLines, selectedVoiceId, stills.map((item) => item.path)) : undefined,
      });
    } catch (error) {
      const outcome = novixaClientOutcome(axios.isAxiosError(error) ? error.code : undefined, describeClientError(error));
      adopt(outcome);
    } finally {
      setBusy('');
    }
  };

  const drawGemini = async (still: NovixaStill) => {
    setBusy(`gemini-${still.index}`);
    try {
      const result = await renderNovixaGeminiStill(buildId, still.index, still.text, brand);
      save({
        ...state,
        filmOrchestration: state.filmOrchestration
          ? {
              ...state.filmOrchestration,
              stills: stills.map((item) => item.index === still.index ? { ...item, path: result.path, approved: false } : item),
            }
          : state.filmOrchestration,
      });
      setNote('Đã tạo ảnh Gemini cho câu này. Các ảnh khác giữ nguyên.');
    } catch (error) {
      const message = axios.isAxiosError(error) ? String((error.response?.data as { message?: string } | undefined)?.message || '') : '';
      setNote(message || 'Gemini không tạo được ảnh.');
    } finally {
      setBusy('');
    }
  };

  const total = lines.length;
  const approvedCount = stills.filter((item) => item.approved).length;
  const needsWide = total >= 12;
  const sideCount = (side: 'wide' | 'tall') => {
    const rows = side === orientation ? stills : shelf?.orientation === side ? shelf.stills : [];
    return rows.filter((item) => item.approved).length;
  };
  const sideRun = (side: 'wide' | 'tall') => (side === orientation ? motion : shelf?.orientation === side ? shelf.motion : undefined);
  const groups = (scenes.length ? scenes : [{ n: 0, title: '', from: 0, to: Math.max(0, total - 1) }]).map((scene) => ({
    scene,
    cards: pictureCards(stills.filter((item) => item.index >= scene.from && item.index <= scene.to), pictureKey),
  })).filter((group) => group.cards.length > 0);
  const busyAny = Boolean(busy);
  const noteBox = note ? <Alert type={motion?.status === 'ready' && motion.videoPath ? 'success' : motion?.status === 'running' ? 'info' : 'warning'} showIcon message={note} /> : null;

  if (section === 'pictures') {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', gap: 12, alignItems: 'center' }}>
          <div>
            <Typography.Title level={4} style={{ margin: 0 }}>2. Hình</Typography.Title>
            <Typography.Text type="secondary">Mỗi câu một ảnh, vẽ trên máy, không tốn tiền. Chữ lấy từ kịch bản; sửa chữ thì quay lại bước 1.</Typography.Text>
          </div>
          {needsWide || orientation === 'wide' ? (
            <Segmented
              value={orientation}
              disabled={busyAny}
              onChange={(value) => switchOrientation(value as 'wide' | 'tall')}
              options={[
                { value: 'wide', label: `Ngang · YouTube · ${sideCount('wide')}/${total}` },
                { value: 'tall', label: `Dọc · TikTok, Short · ${sideCount('tall')}/${total}` },
              ]}
            />
          ) : null}
        </div>
        {noteBox}
        {!stills.length ? (
          <div style={{ border: '1px dashed #b9c3d0', borderRadius: 12, padding: '40px 24px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, textAlign: 'center' }}>
            <Typography.Title level={5} style={{ margin: 0 }}>Chưa có ảnh {nameOf(orientation)} {orientation === 'wide' ? '16:9' : '9:16'}</Typography.Title>
            <Typography.Text type="secondary">
              {orientation === 'wide' ? 'Bản ngang dùng cho YouTube.' : 'Bản dọc dùng cho TikTok và video ngắn.'}
              {shelf?.stills.length ? ` Bộ ảnh ${nameOf(shelf.orientation)} được giữ nguyên.` : ''}
            </Typography.Text>
            <Button type="primary" loading={busy === 'stills'} disabled={!total || busyAny} onClick={() => void run('stills')}>
              Tạo {total} ảnh {nameOf(orientation)}
            </Button>
          </div>
        ) : (
          <>
            <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
              <Typography.Text type={approvedCount === stills.length ? 'success' : undefined}>
                Đã duyệt {approvedCount}/{stills.length} ảnh {nameOf(orientation)}
                {stills.length < total ? ` · còn ${total - stills.length} câu chưa có ảnh` : ''}
              </Typography.Text>
              <Space wrap>
                {stills.length < total ? (
                  <Button loading={busy === 'stills'} disabled={busyAny} onClick={() => void run('stills')}>Vẽ câu còn thiếu</Button>
                ) : null}
                <Button
                  loading={busy === 'stills'}
                  disabled={busyAny}
                  onClick={() => void run('stills', { variant: (Date.now() % 997) + 1 })}
                >Vẽ lại tất cả</Button>
                <Button
                  type={approvedCount < stills.length ? 'primary' : 'default'}
                  disabled={approvedCount === stills.length}
                  onClick={() => {
                    save({
                      ...state,
                      filmOrchestration: state.filmOrchestration
                        ? { ...state.filmOrchestration, stills: stills.map((item) => ({ ...item, approved: true })) }
                        : state.filmOrchestration,
                    });
                    setNote(`Đã duyệt ${stills.length} ảnh.`);
                  }}
                >Duyệt tất cả</Button>
              </Space>
            </Space>
            {groups.map(({ scene, cards }) => (
              <div key={`${scene.n}-${scene.from}`} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {scene.n > 0 ? (
                  <Typography.Text strong style={{ color: '#4a5565' }}>
                    Cảnh {scene.n}{scene.title ? ` · ${scene.title}` : ''} · câu {scene.from + 1}–{scene.to + 1}
                  </Typography.Text>
                ) : null}
                <div style={{ display: 'grid', gridTemplateColumns: `repeat(auto-fill, minmax(${orientation === 'wide' ? 240 : 160}px, 1fr))`, gap: 12 }}>
                  {cards.map((group) => {
                    const still = group[0];
                    const approved = group.every((item) => item.approved);
                    return (
                      <article key={still.id} style={{ border: `1px solid ${approved ? '#b8e3a6' : '#e5e7eb'}`, borderRadius: 8, padding: 8, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
                        {busy === 'stills' || busy === `still-${still.index}`
                          ? <Typography.Text type="secondary">{busy === 'stills' ? 'Đang vẽ' : 'Đang vẽ lại ảnh này'}</Typography.Text>
                          : pictures[still.index]
                            ? <img src={pictures[still.index]} alt={still.text} style={{ width: '100%', borderRadius: 6 }} />
                            : <Typography.Text type="secondary">Đang tải ảnh</Typography.Text>}
                        <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }} ellipsis={{ rows: 2, tooltip: group.map((item) => item.text).join(' ') }}>
                          <strong>Câu {group.map((item) => item.index + 1).join(', ')}.</strong> {group.map((item) => item.text).join(' ')}
                        </Typography.Paragraph>
                        {novixaStillBrandStale(still, brand?.id || '') ? <Typography.Text type="warning" style={{ fontSize: 12 }}>Ảnh chưa theo thương hiệu hiện tại.</Typography.Text> : null}
                        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 6 }}>
                          <Checkbox
                            checked={approved}
                            onChange={(event) => save({
                              ...state,
                              filmOrchestration: state.filmOrchestration
                                ? { ...state.filmOrchestration, stills: stills.map((item) => group.some((row) => row.index === item.index) ? { ...item, approved: event.target.checked } : item) }
                                : state.filmOrchestration,
                            })}
                          >Duyệt</Checkbox>
                          <Button size="small" loading={busy === `still-${still.index}`} disabled={busyAny && busy !== `still-${still.index}`} onClick={() => void run('stills', { redrawIndex: still.index, variant: (Date.now() % 997) + 1 })}>Vẽ lại</Button>
                          <Button size="small" type="text" loading={busy === `gemini-${still.index}`} disabled={busyAny && busy !== `gemini-${still.index}`} onClick={() => void drawGemini(still)}>Gemini</Button>
                        </div>
                      </article>
                    );
                  })}
                </div>
              </div>
            ))}
          </>
        )}
      </Space>
    );
  }

  const brandVoiceInUse = Boolean(brandVoice?.voiceId && selectedVoiceId === brandVoice.voiceId);
  const voiceName = brandVoiceInUse
    ? brandVoice?.voiceName || brandVoice?.voiceId
    : voices.find((item) => item.voiceId === selectedVoiceId)?.name || (selectedVoiceId === NOVIXA_VOICE.voiceId ? NOVIXA_VOICE.label : selectedVoiceId);
  const sides: Array<'wide' | 'tall'> = needsWide ? ['wide', 'tall'] : ['tall'];
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>3. Giọng &amp; video</Typography.Title>
        <Typography.Text type="secondary">Thu giọng một lần cho bản đầu tiên. Bản còn lại dùng lại đúng giọng đó, không gọi ElevenLabs lần nữa.</Typography.Text>
      </div>
      {noteBox}
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 12, background: '#f8f9fb', borderRadius: 10, padding: '14px 16px' }}>
        <div style={{ flex: '999 1 320px', minWidth: 0 }}>
          <Typography.Text strong>{brandVoiceInUse ? 'Giọng thương hiệu' : 'Giọng riêng cho phim này'}: {voiceName}</Typography.Text>
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              ElevenLabs {NOVIXA_VOICE.model}
              {voiceSettings ? ` · ổn định ${voiceSettings.stability ?? 0.5} · tương đồng ${voiceSettings.similarityBoost ?? 0.85} · phong cách ${voiceSettings.style ?? 0} · tốc độ ${voiceSettings.speed ?? 1}` : ' · thông số mặc định'}
            </Typography.Text>
          </div>
        </div>
        <Button
          loading={busy === 'preview'}
          onClick={() => {
            setBusy('preview');
            void fetchContentSeriesVoicePreview(selectedVoiceId)
              .then((blob) => setPreviewUrl(URL.createObjectURL(blob)))
              .catch(() => setNote('Giọng này chưa có file mẫu. Không tạo lượt đọc kịch bản.'))
              .finally(() => setBusy(''));
          }}
        >Nghe thử</Button>
        <Button type="link" onClick={() => setChangingVoice((value) => !value)}>{changingVoice ? 'Đóng' : 'Đổi giọng cho phim này'}</Button>
      </div>
      {previewUrl ? <audio controls src={previewUrl} /> : null}
      {changingVoice ? (
        <Space wrap>
          <Select
            showSearch
            style={{ minWidth: 320, maxWidth: '100%' }}
            value={selectedVoiceId}
            optionFilterProp="label"
            options={[
              ...(brandVoice?.voiceId ? [{ value: brandVoice.voiceId, label: `Giọng thương hiệu · ${brandVoice.voiceName || brandVoice.voiceId}` }] : []),
              ...(brandVoice?.voiceId === NOVIXA_VOICE.voiceId ? [] : [{ value: NOVIXA_VOICE.voiceId, label: `${NOVIXA_VOICE.label} · ${NOVIXA_VOICE.voiceId}` }]),
              ...voices
                .filter((item) => item.voiceId && item.voiceId !== NOVIXA_VOICE.voiceId && item.voiceId !== brandVoice?.voiceId)
                .map((item) => ({ value: item.voiceId, label: item.name || item.voiceId })),
            ]}
            onChange={(voiceId: string) => save({
              ...state,
              filmOrchestration: state.filmOrchestration
                ? { ...state.filmOrchestration, voiceChoice: { model: NOVIXA_VOICE.model, voiceId, approved: true, previewPath: null } }
                : state.filmOrchestration,
            })}
          />
          <Typography.Text type="secondary">Đổi giọng thì video phải dựng lại và thu giọng lượt mới.</Typography.Text>
        </Space>
      ) : null}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 12 }}>
        {sides.map((side) => {
          const current = side === orientation;
          const sideRunNow = sideRun(side);
          const ready = sideRunNow?.status === 'ready' && Boolean(sideRunNow.videoPath);
          const other = side === 'wide' ? 'tall' : 'wide';
          const otherReady = sideRun(other)?.status === 'ready';
          return (
            <article key={side} style={{ border: `1px solid ${ready ? '#b8e3a6' : '#e3e6eb'}`, borderRadius: 12, padding: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                <Typography.Title level={5} style={{ margin: 0 }}>Bản {nameOf(side)} {side === 'wide' ? '16:9' : '9:16'}</Typography.Title>
                {ready ? <Tag color="green">Đã dựng</Tag> : sideRunNow?.status === 'running' ? <Tag color="blue">Đang dựng</Tag> : <Tag>Chưa dựng</Tag>}
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                {side === 'wide' ? 'Dùng cho YouTube.' : needsWide ? 'Dùng cho TikTok và video ngắn.' : 'Dùng cho TikTok, YouTube Shorts.'}
                {!ready && otherReady ? ` Dùng lại giọng của bản ${nameOf(other)}, không thu lại.` : ''}
              </Typography.Text>
              {current ? (
                <>
                  {ready ? (
                    <div ref={playerRef}>
                      {videoUrl ? <video controls src={videoUrl} style={{ width: side === 'wide' ? '100%' : 'min(280px, 100%)', borderRadius: 8, background: '#000' }} /> : <Typography.Text type="secondary">Đang mở video đã dựng.</Typography.Text>}
                    </div>
                  ) : null}
                  <Space wrap>
                    {ready ? (
                      <Button loading={busy === 'video'} disabled={busyAny} onClick={() => void run('video', { force: true })}>Dựng lại</Button>
                    ) : (
                      <Button type="primary" loading={busy === 'video'} disabled={Boolean(gate) || busyAny} onClick={() => void run('video')}>Dựng video {nameOf(side)}</Button>
                    )}
                  </Space>
                  {!ready && gate ? <Typography.Text style={{ color: '#8a4b00', fontSize: 13 }}>{gate === 'Còn ảnh chưa duyệt.' || gate === 'Chưa có ảnh từ kịch bản.' ? `Cần duyệt đủ ảnh ${nameOf(side)} ở bước 2.` : gate}</Typography.Text> : null}
                </>
              ) : (
                <Space wrap>
                  <Button type={ready ? 'default' : 'primary'} disabled={busyAny} onClick={() => switchOrientation(side)}>
                    {ready ? `Mở bản ${nameOf(side)}` : `Chuyển sang khung ${nameOf(side)}`}
                  </Button>
                  {!ready ? <Typography.Text type="secondary" style={{ fontSize: 13 }}>Ảnh {nameOf(side)}: {sideCount(side)}/{total} đã duyệt</Typography.Text> : null}
                </Space>
              )}
            </article>
          );
        })}
      </div>
    </Space>
  );
}
