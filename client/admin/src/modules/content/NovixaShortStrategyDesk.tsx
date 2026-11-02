import { useState } from 'react';
import { suggestNovixaShortStrategy } from '@/shared/api/content.api';
import type { ProductionShotActions } from '@/shared/api/content.api';
import type { FamixaSeriesShot } from './content-famixa-series';
import {
  PICTURE_IMAGE_TO_VIDEO,
  PICTURE_MOTION_GRAPHICS,
  PICTURE_STATIC,
  SPEECH_LIP_SYNC,
  SPEECH_MOUTH_UNSYNCED,
  SPEECH_NONE,
  SPEECH_VOICE_OVER,
  strategyBlockReason,
  strategyEngaged,
} from './novixa-short-strategy';

const PICTURES = [
  { value: PICTURE_STATIC, label: 'Ảnh tĩnh' },
  { value: PICTURE_IMAGE_TO_VIDEO, label: 'Ảnh sang video' },
  { value: PICTURE_MOTION_GRAPHICS, label: 'Đồ họa chuyển động' },
];

const SPEECH = [
  { value: SPEECH_NONE, label: 'Không thoại' },
  { value: SPEECH_VOICE_OVER, label: 'Voice-over' },
  { value: SPEECH_MOUTH_UNSYNCED, label: 'Miệng không khớp chữ' },
  { value: SPEECH_LIP_SYNC, label: 'Khớp khẩu hình' },
];

const REASONS: Record<string, string> = {
  STRATEGY_CONFIRMATION_REQUIRED: 'Chưa xác nhận chiến lược. Hành động trả phí đang khóa.',
  STRATEGY_UNDECLARED: 'Chiến lược chưa khai báo hợp lệ.',
  STATIC_FORBIDS_PAID_VIDEO: 'Ảnh tĩnh không gọi tạo video.',
  MOTION_GRAPHICS_UNSUPPORTED: 'Đồ họa chuyển động chưa có renderer.',
  VOICE_OVER_FORBIDS_LIPSYNC: 'Voice-over không gọi khớp khẩu hình.',
  MOUTH_UNSYNCED_FORBIDS_LIPSYNC: 'Không gọi khớp khẩu hình. Chưa có hoạt ảnh miệng.',
  SPEECH_DOES_NOT_REQUIRE_LIPSYNC: 'Cách thể hiện này không cần khớp khẩu hình.',
  LIPSYNC_REQUIRES_IMAGE_TO_VIDEO: 'Khớp khẩu hình chỉ khi hình là ảnh sang video.',
  MISSING_KEYFRAME: 'Chưa có keyframe đã duyệt.',
  MISSING_VOICE_ARTIFACT: 'Chưa có voice-over đã duyệt.',
  MISSING_LIPSYNC_ARTIFACT: 'Chưa đủ take và voice cho khớp khẩu hình.',
};

export function NovixaShortStrategyDesk({
  shot,
  accepted,
  catalog,
  onCommit,
}: {
  shot: FamixaSeriesShot;
  accepted: boolean;
  catalog?: ProductionShotActions;
  onCommit: (patch: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery' | 'strategyConfirmed'>) => Promise<void>;
}) {
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState('');
  const engaged = strategyEngaged(shot);
  const picture = (shot.pictureStrategy || '').trim();
  const speech = (shot.speechDelivery || '').trim();
  const reason = strategyBlockReason(catalog);
  const reasonText = reason ? REASONS[reason] || reason : '';

  const commit = async (patch: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery' | 'strategyConfirmed'>) => {
    setBusy(true);
    setNote('');
    try {
      await onCommit(patch);
    } catch (error) {
      setNote(error instanceof Error ? error.message : 'Server chưa nhận chiến lược.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ margin: '4px 0 6px 28px', fontSize: 12 }}>
      <div style={{ color: '#64748b' }}>
        {engaged
          ? 'Chiến lược shot — hình và thoại tách riêng.'
          : 'Chưa khai báo chiến lược. Shot này giữ hợp đồng Famixa cũ.'}
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 4 }}>
        <label>
          Hình{' '}
          <select
            aria-label={`Hình ${shot.id}`}
            value={picture}
            disabled={busy}
            onChange={(event) => {
              void commit({
                pictureStrategy: event.target.value,
                speechDelivery: speech,
                strategyConfirmed: false,
              });
            }}
          >
            <option value="">Chưa khai báo</option>
            {PICTURES.map((row) => (
              <option key={row.value} value={row.value}>
                {row.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Thoại{' '}
          <select
            aria-label={`Thoại ${shot.id}`}
            value={speech}
            disabled={busy}
            onChange={(event) => {
              void commit({
                pictureStrategy: picture,
                speechDelivery: event.target.value,
                strategyConfirmed: false,
              });
            }}
          >
            <option value="">Chưa khai báo</option>
            {SPEECH.map((row) => (
              <option key={row.value} value={row.value}>
                {row.label}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          disabled={busy}
          onClick={() => {
            void (async () => {
              setBusy(true);
              setNote('');
              try {
                const plan = await suggestNovixaShortStrategy({
                  hasDialogue: (shot.dialogueSegmentIds?.length ?? 0) > 0,
                  speaksOnCamera: false,
                  needsPictureMotion: picture === PICTURE_IMAGE_TO_VIDEO,
                  wordLevelMouth: false,
                });
                if (!plan.picture || !plan.speech) {
                  setNote('Server không trả gợi ý.');
                  return;
                }
                setNote(plan.reason);
                await onCommit({
                  pictureStrategy: plan.picture,
                  speechDelivery: plan.speech,
                  strategyConfirmed: false,
                });
              } catch (error) {
                setNote(error instanceof Error ? error.message : 'Không đọc được gợi ý từ server.');
              } finally {
                setBusy(false);
              }
            })();
          }}
        >
          Xem gợi ý
        </button>
        <label>
          <input
            type="checkbox"
            aria-label={`Xác nhận chiến lược ${shot.id}`}
            checked={shot.strategyConfirmed === true}
            disabled={busy || !picture || !speech}
            onChange={(event) => {
              void commit({
                pictureStrategy: picture,
                speechDelivery: speech,
                strategyConfirmed: event.target.checked,
              });
            }}
          />{' '}
          Xác nhận chiến lược
        </label>
      </div>
      <div style={{ marginTop: 4 }}>
        {accepted ? (
          <span style={{ color: '#166534' }}>Server đã nhận xác nhận.</span>
        ) : shot.strategyConfirmed ? (
          <span style={{ color: '#9a3412' }}>Đã gửi — server chưa nhận xác nhận.</span>
        ) : (
          <span style={{ color: '#64748b' }}>Chưa xác nhận.</span>
        )}
        {reasonText ? <div style={{ color: '#9a3412' }}>{reason} — {reasonText}</div> : null}
        {note ? <div style={{ color: '#334155' }}>{note}</div> : null}
      </div>
    </div>
  );
}
