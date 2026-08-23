/** Browser mux: Runway takes + TTS + burned Vietnamese subs. */

import { fetchContentSeriesTake } from '../../shared/api/content.api';
import type { AssembleClip } from './content-famixa-assemble';

export function triggerDownload(blob: Blob, fileName: string) {
  const a = document.createElement('a');
  const href = URL.createObjectURL(blob);
  a.href = href;
  a.download = fileName;
  a.click();
  window.setTimeout(() => URL.revokeObjectURL(href), 8_000);
}

export async function takeBlobFromUrl(url: string) {
  return fetchContentSeriesTake(url);
}

export function blobToBase64(blob: Blob) {
  return new Promise<string>((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => {
      const s = String(r.result || '');
      const i = s.indexOf(',');
      resolve(i >= 0 ? s.slice(i + 1) : s);
    };
    r.onerror = () => reject(new Error('Không đọc được file thoại.'));
    r.readAsDataURL(blob);
  });
}

function pickRecorderMime() {
  const types = [
    'video/webm;codecs=vp9,opus',
    'video/webm;codecs=vp8,opus',
    'video/webm;codecs=vp9,vorbis',
    'video/webm;codecs=vp8,vorbis',
  ];
  for (const t of types) {
    if (typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported(t)) return t;
  }
  return 'video/webm';
}

function waitVideoPlaying(el: HTMLVideoElement) {
  return new Promise<void>((resolve) => {
    if (!el.paused && el.readyState >= 2) {
      resolve();
      return;
    }
    const done = () => {
      el.removeEventListener('playing', done);
      el.removeEventListener('error', done);
      resolve();
    };
    el.addEventListener('playing', done, { once: true });
    el.addEventListener('error', done, { once: true });
    void el.play().catch(done);
  });
}

function drawSub(ctx: CanvasRenderingContext2D, w: number, h: number, name: string, text: string) {
  const pad = 28;
  const boxH = 92;
  ctx.fillStyle = 'rgba(0,0,0,0.55)';
  ctx.fillRect(0, h - boxH - 24, w, boxH + 24);
  ctx.textAlign = 'center';
  ctx.lineJoin = 'round';
  if (name) {
    ctx.font = '600 18px "Segoe UI", system-ui, sans-serif';
    ctx.strokeStyle = 'rgba(0,0,0,0.8)';
    ctx.lineWidth = 4;
    ctx.strokeText(name.toUpperCase(), w / 2, h - boxH + 8);
    ctx.fillStyle = '#ffe08a';
    ctx.fillText(name.toUpperCase(), w / 2, h - boxH + 8);
  }
  ctx.font = '600 28px "Segoe UI", system-ui, sans-serif';
  ctx.strokeStyle = 'rgba(0,0,0,0.85)';
  ctx.lineWidth = 6;
  const line = text.length > 42 ? `${text.slice(0, 40)}…` : text;
  ctx.strokeText(line, w / 2, h - pad);
  ctx.fillStyle = '#fff';
  ctx.fillText(line, w / 2, h - pad);
}

async function loadVideo(blob: Blob, muted = true) {
  const url = URL.createObjectURL(blob);
  const el = document.createElement('video');
  el.src = url;
  el.muted = muted;
  el.volume = muted ? 0 : 1;
  el.playsInline = true;
  await new Promise<void>((resolve, reject) => {
    el.onloadedmetadata = () => resolve();
    el.onerror = () => reject(new Error('Không đọc được file take.'));
  });
  return { el, url };
}

export async function recordAssembledCut(opts: {
  clips: AssembleClip[];
  videoOf: (shotId: string) => Promise<Blob>;
  audioOf: (lineId: string) => Promise<Blob | undefined>;
  onProgress?: (msg: string) => void;
}): Promise<Blob> {
  if (typeof MediaRecorder === 'undefined') {
    throw new Error('Trình duyệt không ghi được video. Dùng Chrome/Edge.');
  }
  const w = 1280;
  const h = 720;
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không mở được canvas ghép.');
  const ac = new AudioContext();
  if (ac.state === 'suspended') await ac.resume();
  const dest = ac.createMediaStreamDestination();
  const keepAlive = ac.createGain();
  keepAlive.gain.value = 0.0001;
  const osc = ac.createOscillator();
  osc.frequency.value = 20;
  osc.connect(keepAlive);
  keepAlive.connect(dest);
  osc.start();
  ctx.fillStyle = '#0b1220';
  ctx.fillRect(0, 0, w, h);
  const vStream = canvas.captureStream(30);
  const mixed = new MediaStream([...vStream.getVideoTracks(), ...dest.stream.getAudioTracks()]);
  const chunks: BlobPart[] = [];
  const rec = new MediaRecorder(mixed, { mimeType: pickRecorderMime(), videoBitsPerSecond: 4_000_000 });
  rec.ondataavailable = (e) => {
    if (e.data.size) chunks.push(e.data);
  };
  const stopped = new Promise<void>((resolve) => {
    rec.onstop = () => resolve();
  });
  rec.start(400);

  try {
    for (let i = 0; i < opts.clips.length; i++) {
      const clip = opts.clips[i]!;
      opts.onProgress?.(`${clip.code} · ${i + 1}/${opts.clips.length}`);
      const blob = await opts.videoOf(clip.shotId);
      const keepLip = Boolean(clip.useVideoAudio);
      const { el, url } = await loadVideo(blob, !keepLip);
      const audioBufs: { buf: AudioBuffer; start: number }[] = [];
      let local = 0;
      if (keepLip) {
        try {
          ac.createMediaElementSource(el).connect(dest);
        } catch {
          /* element already routed */
        }
        local = Number.isFinite(el.duration) ? el.duration : clip.seconds;
      } else {
        for (const cue of clip.cues) {
          const ab = await opts.audioOf(cue.lineId);
          if (!ab) {
            local += cue.endSec - cue.startSec;
            continue;
          }
          const raw = await ab.arrayBuffer();
          const buf = await ac.decodeAudioData(raw.slice(0));
          audioBufs.push({ buf, start: local });
          local += buf.duration;
        }
      }
      const dur = Math.max(clip.seconds, Number.isFinite(el.duration) ? el.duration : 0, local || 0, 1);
      const t0 = ac.currentTime + 0.08;
      for (const a of audioBufs) {
        const src = ac.createBufferSource();
        src.buffer = a.buf;
        src.connect(dest);
        src.start(t0 + a.start);
      }
      el.currentTime = 0;
      await waitVideoPlaying(el);
      const started = performance.now();
      await new Promise<void>((resolve) => {
        const tick = () => {
          if (performance.now() - started >= dur * 1000) {
            resolve();
            return;
          }
          if (el.readyState >= 2) {
            try {
              ctx.drawImage(el, 0, 0, w, h);
            } catch {
              /* decode lag — keep last frame, do not flash black */
            }
          }
          const elapsed = (performance.now() - started) / 1000;
          const absT = clip.startSec + elapsed;
          const cue = clip.cues.find((c) => absT >= c.startSec && absT < c.endSec + 0.05);
          if (cue?.text) drawSub(ctx, w, h, cue.name, cue.text);
          window.requestAnimationFrame(tick);
        };
        window.requestAnimationFrame(tick);
      });
      el.pause();
      URL.revokeObjectURL(url);
    }
  } finally {
    osc.stop();
    if (rec.state !== 'inactive') rec.stop();
    await stopped;
    await ac.close().catch(() => undefined);
  }
  if (!chunks.length) throw new Error('Ghép xong nhưng không có dữ liệu video.');
  return new Blob(chunks, { type: rec.mimeType || 'video/webm' });
}
