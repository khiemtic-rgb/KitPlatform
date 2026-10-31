import { useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { Alert, Button, Card, Checkbox, Input, Select, Space, Typography } from 'antd';
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
}: {
  buildId: string;
  state: SeriesPilotState;
  steps: string[];
  onCommit: (next: SeriesPilotState) => void;
  brand?: NovixaBrandProfile | null;
}) {
  const stills = state.filmOrchestration?.stills ?? [];
  const voice = state.filmOrchestration?.voiceChoice;
  const motion = state.filmOrchestration?.motion;
  const gate = novixaVideoGate(state);
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
  const loaded = useRef(false);
  const playerRef = useRef<HTMLDivElement>(null);
  const stillKey = stills.map((item) => `${item.index}:${item.path}`).join('|');

  const save = (next: SeriesPilotState) => {
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
      const remoteStills = (remote.stills ?? []).map((item) => ({ ...item, approved: false }));
      adopt(incoming, remoteStills.length ? mergeNovixaStills(stills, remoteStills) : undefined);
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
        setNote(novixaVideoCurrent(state) ? 'Video đã có. Không tạo lượt giọng mới.' : 'Ảnh hoặc giọng đã đổi. Bấm Tạo video để dựng lại.');
        return;
      }
      const readyVideo = Boolean(motion?.status === 'ready' && motion.videoPath);
      const sameTake = novixaVideoCurrent(state) || (readyVideo && !motion?.inputKey);
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
      const outcome = novixaClientOutcome(axios.isAxiosError(error) ? error.code : undefined);
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

  return (
    <Card size="small" title="Ảnh, giọng, video" style={{ marginTop: 12 }}>
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        {note ? <Alert type={motion?.status === 'ready' && motion.videoPath ? 'success' : motion?.status === 'running' ? 'info' : 'warning'} showIcon message={note} /> : null}
        {motion?.status === 'ready' && motion.videoPath ? (
          <div ref={playerRef}>
            {videoUrl ? <video controls src={videoUrl} style={{ width: orientation === 'wide' ? 'min(640px, 100%)' : 'min(360px, 100%)' }} /> : <Typography.Text>Đang mở video đã dựng.</Typography.Text>}
          </div>
        ) : null}
        <Space wrap>
          <Select
            size="middle"
            style={{ width: 170 }}
            value={orientation}
            disabled={Boolean(busy)}
            onChange={(value) => switchOrientation(value)}
            options={[{ value: 'tall', label: 'Dọc 9:16 (video ngắn)' }, { value: 'wide', label: 'Ngang 16:9 (video dài)' }]}
          />
          {shelf && shelf.stills.length ? <Typography.Text type="secondary">Đã cất {shelf.stills.length} ảnh {nameOf(shelf.orientation)}</Typography.Text> : null}
          <Button
            type="primary"
            loading={busy === 'stills'}
            disabled={!lines.length || busy.startsWith('still-') || busy.startsWith('gemini-')}
            onClick={() => void run('stills', stills.length ? { variant: (Date.now() % 997) + 1 } : undefined)}
          >{stills.length ? 'Tạo lại tất cả' : 'Tạo ảnh'}</Button>
          <Button disabled={!stills.length} onClick={() => {
            save({
              ...state,
              filmOrchestration: state.filmOrchestration
                ? { ...state.filmOrchestration, stills: stills.map((item) => ({ ...item, approved: true })) }
                : state.filmOrchestration,
            });
            setNote(`Đã duyệt ${stills.length} ảnh.`);
          }}>Duyệt tất cả ảnh</Button>
        </Space>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 12 }}>
          {pictureCards(stills, pictureKey).map((group) => {
            const still = group[0];
            return (
            <article key={still.id} style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 8, minWidth: 0 }}>
              {busy === 'stills' || busy === `still-${still.index}` ? <Typography.Text type="secondary">{busy === 'stills' ? 'Đang vẽ lại tất cả' : 'Đang vẽ lại ảnh này'}</Typography.Text> : pictures[still.index] ? <img src={pictures[still.index]} alt={still.text} style={{ width: '100%', borderRadius: 6 }} /> : <Typography.Text type="secondary">Đang tải ảnh</Typography.Text>}
              {novixaStillBrandStale(still, brand?.id || '') ? <Typography.Text type="warning">Ảnh chưa theo thương hiệu hiện tại. Bấm Tạo lại ảnh. Ảnh đã duyệt không bị thay.</Typography.Text> : null}
              {group.map((item) => (
                <Input.TextArea
                  key={item.id}
                  style={{ marginTop: 8 }}
                  value={item.text}
                  autoSize
                  onChange={(event) => {
                    const text = event.target.value;
                    save({
                      ...state,
                      filmOrchestration: state.filmOrchestration
                        ? { ...state.filmOrchestration, stills: stills.map((row) => row.index === item.index ? { ...row, text, approved: false } : row) }
                        : state.filmOrchestration,
                    });
                  }}
                />
              ))}
              <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', alignItems: 'stretch', gap: 8 }}>
                <Checkbox
                  checked={group.every((item) => item.approved)}
                  onChange={(event) => save({
                    ...state,
                    filmOrchestration: state.filmOrchestration
                      ? { ...state.filmOrchestration, stills: stills.map((item) => group.some((row) => row.index === item.index) ? { ...item, approved: event.target.checked } : item) }
                      : state.filmOrchestration,
                  })}
                >Duyệt</Checkbox>
                <Button block size="small" loading={busy === `still-${still.index}`} disabled={Boolean(busy) && busy !== `still-${still.index}`} onClick={() => void run('stills', { redrawIndex: still.index, variant: (Date.now() % 997) + 1 })}>Tạo lại ảnh</Button>
                <Button block size="small" loading={busy === `gemini-${still.index}`} disabled={Boolean(busy) && busy !== `gemini-${still.index}`} onClick={() => void drawGemini(still)}>Tạo ảnh Gemini</Button>
              </div>
            </article>
            );
          })}
        </div>
        <Typography.Text>
          Giọng ElevenLabs · {NOVIXA_VOICE.model}.{' '}
          {brandVoice
            ? `Mặc định là giọng của thương hiệu: ${brandVoice.voiceName || brandVoice.voiceId}${voiceSettings ? ` · độ ổn định ${voiceSettings.stability ?? 0.5} · tốc độ ${voiceSettings.speed ?? 1}` : ''}.`
            : 'Mặc định là giọng đã kiểm thử. Đặt giọng cố định trong Thương hiệu → Giọng đọc.'}
        </Typography.Text>
        <Select
          showSearch
          style={{ minWidth: 320, maxWidth: '100%' }}
          value={selectedVoiceId}
          optionFilterProp="label"
          options={[
            ...(brandVoice?.voiceId && brandVoice.voiceId !== NOVIXA_VOICE.voiceId
              ? [{ value: brandVoice.voiceId, label: `Giọng thương hiệu · ${brandVoice.voiceName || brandVoice.voiceId}` }]
              : []),
            { value: NOVIXA_VOICE.voiceId, label: `${NOVIXA_VOICE.label} · ${NOVIXA_VOICE.voiceId}` },
            ...voices
              .filter((item) => item.voiceId && item.voiceId !== NOVIXA_VOICE.voiceId && item.voiceId !== brandVoice?.voiceId)
              .map((item) => ({ value: item.voiceId, label: item.name || item.voiceId })),
          ]}
          onChange={(voiceId: string) => save({
            ...state,
            filmOrchestration: state.filmOrchestration
              ? { ...state.filmOrchestration, voiceChoice: { model: NOVIXA_VOICE.model, voiceId, approved: false, previewPath: null } }
              : state.filmOrchestration,
          })}
        />
        <Space wrap>
          <Button
            onClick={() => save({
              ...state,
              filmOrchestration: state.filmOrchestration
                ? { ...state.filmOrchestration, voiceChoice: { model: NOVIXA_VOICE.model, voiceId: selectedVoiceId, approved: true, previewPath: voice?.previewPath } }
                : state.filmOrchestration,
            })}
          >{voice?.approved && voice.voiceId === selectedVoiceId ? 'Đã xác nhận giọng' : 'Xác nhận giọng'}</Button>
          <Button
            loading={busy === 'preview'}
            onClick={() => {
              setBusy('preview');
              void fetchContentSeriesVoicePreview(selectedVoiceId)
                .then((blob) => setPreviewUrl(URL.createObjectURL(blob)))
                .catch(() => setNote('Giọng này chưa có file mẫu thư viện. Không tạo lượt đọc kịch bản.'))
                .finally(() => setBusy(''));
            }}
          >Nghe thử</Button>
        </Space>
        {previewUrl ? <audio controls src={previewUrl} /> : null}
        <Space wrap>
          <Button type="primary" loading={busy === 'video'} onClick={() => void run('video')}>Tạo video</Button>
          <Button loading={busy === 'video'} onClick={() => void run('video', { force: false, resumeOnly: true })}>Tiếp tục</Button>
        </Space>
        {gate ? <Typography.Text type="secondary">{gate}{motion?.status === 'ready' && motion.videoPath ? ' Video đã dựng vẫn xem được phía trên.' : ''}</Typography.Text> : null}
      </Space>
    </Card>
  );
}
