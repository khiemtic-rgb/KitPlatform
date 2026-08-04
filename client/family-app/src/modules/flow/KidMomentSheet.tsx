import { useEffect, useRef, useState } from 'react';
import {
  createFamilyMemory,
  uploadFamilyMomentMedia,
  type FamilyMemoryEntry,
} from '@/shared/api/family-os.api';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';

type Props = {
  familyId: string;
  memberId: string;
  memberName?: string;
  flowDate: string;
  open: boolean;
  remainingToday: number;
  onClose: () => void;
  onCreated: (entry: FamilyMemoryEntry) => void;
};

type Mode = 'pick' | 'photo' | 'voice';

const MAX_VOICE_MS = 30_000;

export function KidMomentSheet({
  familyId,
  memberId,
  memberName,
  flowDate,
  open,
  remainingToday,
  onClose,
  onCreated,
}: Props) {
  const [mode, setMode] = useState<Mode>('pick');
  const [caption, setCaption] = useState('');
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [recording, setRecording] = useState(false);
  const [recordMs, setRecordMs] = useState(0);
  const mediaRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<number | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (!open) return;
    setMode('pick');
    setCaption('');
    setPreviewUrl(null);
    setFile(null);
    setBusy(false);
    setError(null);
    setRecording(false);
    setRecordMs(0);
    return () => stopRecording(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  if (!open) return null;

  const short = (memberName || 'Con').trim().split(/\s+/).pop() || 'Con';

  const stopRecording = (discard = false) => {
    if (timerRef.current != null) {
      window.clearInterval(timerRef.current);
      timerRef.current = null;
    }
    const rec = mediaRef.current;
    if (rec && rec.state !== 'inactive') {
      try {
        rec.stop();
      } catch {
        /* ignore */
      }
    }
    mediaRef.current = null;
    const stream = streamRef.current;
    if (stream) {
      for (const t of stream.getTracks()) t.stop();
      streamRef.current = null;
    }
    setRecording(false);
    if (discard) {
      chunksRef.current = [];
      setRecordMs(0);
    }
  };

  const startRecording = async () => {
    setError(null);
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      setError('Máy này chưa hỗ trợ ghi âm trong trình duyệt.');
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      const mime = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
        ? 'audio/webm;codecs=opus'
        : MediaRecorder.isTypeSupported('audio/webm')
          ? 'audio/webm'
          : '';
      const rec = mime ? new MediaRecorder(stream, { mimeType: mime }) : new MediaRecorder(stream);
      chunksRef.current = [];
      rec.ondataavailable = (ev) => {
        if (ev.data.size > 0) chunksRef.current.push(ev.data);
      };
      rec.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: rec.mimeType || 'audio/webm' });
        if (blob.size < 400) {
          setError('Giọng quá ngắn — giữ nút ghi khoảng 2 giây nhé.');
          return;
        }
        const f = new File([blob], `moment-${Date.now()}.webm`, {
          type: blob.type || 'audio/webm',
        });
        setFile(f);
        setPreviewUrl(URL.createObjectURL(blob));
        setMode('voice');
      };
      mediaRef.current = rec;
      rec.start(200);
      setRecording(true);
      setRecordMs(0);
      const started = Date.now();
      timerRef.current = window.setInterval(() => {
        const elapsed = Date.now() - started;
        setRecordMs(elapsed);
        if (elapsed >= MAX_VOICE_MS) stopRecording(false);
      }, 200);
    } catch {
      setError('Chưa mở được micro — cho phép quyền ghi âm rồi thử lại.');
    }
  };

  const onPickPhoto = (list: FileList | null) => {
    const f = list?.[0];
    if (!f) return;
    if (!/^image\/(jpeg|png|webp)$/i.test(f.type) && !/\.(jpe?g|png|webp)$/i.test(f.name)) {
      setError('Chỉ chọn ảnh JPG, PNG hoặc WebP.');
      return;
    }
    setError(null);
    setFile(f);
    setPreviewUrl(URL.createObjectURL(f));
    setMode('photo');
  };

  const submit = async () => {
    if (!file) {
      setError('Chọn ảnh hoặc ghi một câu ngắn trước nhé.');
      return;
    }
    if (remainingToday <= 0) {
      setError('Hôm nay đã gửi đủ 3 khoảnh khắc — mai gửi tiếp nhé.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const uploaded = await uploadFamilyMomentMedia(familyId, file, memberId);
      const isAudio = uploaded.mediaKind === 'audio';
      const created = await createFamilyMemory(familyId, {
        titleVi: isAudio ? `${short} gửi giọng nói` : `${short} gửi khoảnh khắc`,
        flowDate,
        memberId,
        kind: 'kid_moment',
        noteVi: caption.trim() || undefined,
        icon: isAudio ? '\u{1F3A4}' : '\u{1F4F7}',
        photoUrl: uploaded.url,
      });
      onCreated(created);
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Chưa gửi được. Thử lại nhé.';
      setError(msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="ph-sheet-backdrop" role="presentation" onClick={onClose}>
      <div
        className="ph-sheet km-sheet"
        role="dialog"
        aria-modal="true"
        aria-label="Gửi khoảnh khắc cho nhà"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>Gửi khoảnh khắc</h2>
          <button type="button" className="ph-sheet-close" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        <p className="ph-sheet-lead">
          Không phải bài tập — chỉ một ảnh hoặc câu nói nhỏ để bố mẹ thấy bạn. Còn{' '}
          <strong>{Math.max(0, remainingToday)}</strong>/3 hôm nay.
        </p>

        {mode === 'pick' ? (
          <div className="km-pick">
            <button
              type="button"
              className="km-pick-btn"
              onClick={() => fileInputRef.current?.click()}
            >
              <span aria-hidden>{'\u{1F4F7}'}</span>
              <strong>Chọn ảnh</strong>
              <em>Một khoảnh bạn muốn chia sẻ</em>
            </button>
            <button
              type="button"
              className="km-pick-btn"
              onClick={() => void startRecording()}
              disabled={recording}
            >
              <span aria-hidden>{'\u{1F3A4}'}</span>
              <strong>{recording ? 'Đang ghi…' : 'Ghi giọng ngắn'}</strong>
              <em>
                {recording
                  ? `${Math.ceil(recordMs / 1000)}s / 30s — bấm dừng bên dưới`
                  : 'Tối đa 30 giây'}
              </em>
            </button>
            {recording ? (
              <button type="button" className="ph-request-submit" onClick={() => stopRecording(false)}>
                Dừng ghi
              </button>
            ) : null}
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              onChange={(e) => onPickPhoto(e.target.files)}
            />
          </div>
        ) : (
          <div className="km-preview">
            {mode === 'photo' && previewUrl ? (
              <img src={previewUrl} alt="Xem trước khoảnh khắc" className="km-preview-img" />
            ) : null}
            {mode === 'voice' && previewUrl ? (
              <audio controls src={withEvidenceAuth(previewUrl) || previewUrl} className="km-preview-audio" />
            ) : null}
            <label className="ph-add-memory-field">
              <span>Lời chú thích (tuỳ chọn)</span>
              <input
                value={caption}
                onChange={(e) => setCaption(e.target.value)}
                placeholder="Vd: Con vừa xong bài / Con nhớ bố mẹ"
                maxLength={120}
              />
            </label>
            <div className="km-actions">
              <button
                type="button"
                className="km-secondary"
                disabled={busy}
                onClick={() => {
                  stopRecording(true);
                  setMode('pick');
                  setFile(null);
                  setPreviewUrl(null);
                }}
              >
                Chọn lại
              </button>
              <button
                type="button"
                className="ph-request-submit"
                disabled={busy || !file}
                onClick={() => void submit()}
              >
                {busy ? 'Đang gửi…' : 'Gửi cho nhà'}
              </button>
            </div>
          </div>
        )}

        {error ? <p className="ph-sheet-error">{error}</p> : null}
      </div>
    </div>
  );
}