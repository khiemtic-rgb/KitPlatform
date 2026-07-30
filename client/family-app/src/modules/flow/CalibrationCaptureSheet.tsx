import { useState } from 'react';
import {
  captureFamilyCalibration,
  type FamilyDnaCard,
} from '@/shared/api/family-os.api';

const WORRY_OPTIONS = [
  {
    value: 'tu_ti',
    label: 'Con hay tự ti / ngại thử',
    hint: 'Gặp bạn giỏi hơn là muốn rút lui',
  },
  {
    value: 'thieu_phan_dau',
    label: 'Con chưa chịu cố / tưởng mình khá',
    hint: 'Hay lướt bài, né chỗ khó',
  },
  {
    value: 'de_hu',
    label: 'Sợ chiều quá / con dễ hư',
    hint: 'Thương nhiều nhưng thiếu khung nhẹ',
  },
  {
    value: 'balance_ok',
    label: 'Nhà đang giữ nhịp ổn',
    hint: 'Muốn giữ vừa ấm vừa có giới hạn vừa phải',
  },
] as const;

const RESOURCE_OPTIONS = [
  { value: 'tight', label: 'Ít thời gian / điều kiện eo hẹp' },
  { value: 'moderate', label: 'Vừa đủ — giữ được nhịp nhẹ' },
  { value: 'abundant', label: 'Thoải mái hơn về thời gian / điều kiện' },
] as const;

const SCHOOL_OPTIONS = [
  {
    value: 'bubble_easy',
    label: 'Ít gặp bạn trường khác / ít thi ngoài',
    hint: 'Dễ tưởng mình khá nếu ít được so với ngoài',
  },
  {
    value: 'mixed',
    label: 'Thỉnh thoảng có giao lưu / đề ngoài',
    hint: 'Có đối chiếu vừa phải',
  },
  {
    value: 'competitive',
    label: 'Hay thi / cạnh tranh với môi trường khó hơn',
    hint: 'Thường xuyên gặp bạn / đề ngoài',
  },
] as const;

const SELF_VIEW_OPTIONS = [
  { value: 'overestimates', label: 'Con hay tự đánh giá cao hơn thực lực' },
  { value: 'calibrated', label: 'Con đánh giá khá sát' },
  { value: 'underestimates', label: 'Con hay tự đánh giá thấp / thiếu tự tin' },
] as const;

const PEER_SHOCK_OPTIONS = [
  { value: 'none', label: 'Chưa thấy cú sốc' },
  { value: 'mild', label: 'Có chạnh lòng nhẹ khi gặp bạn ngoài' },
  { value: 'sharp', label: 'Sụp tự tin rõ sau khi gặp bạn / đề ngoài' },
] as const;

type Props = {
  familyId: string;
  open: boolean;
  onClose: () => void;
  onSaved: (dna: FamilyDnaCard) => void;
};

/** Light capture — Growth Balance + calibration (không form 8 lớp). */
export function CalibrationCaptureSheet({ familyId, open, onClose, onSaved }: Props) {
  const [worry, setWorry] = useState<string>('tu_ti');
  const [resource, setResource] = useState<string>('moderate');
  const [school, setSchool] = useState<string>('bubble_easy');
  const [selfView, setSelfView] = useState<string>('overestimates');
  const [peerShock, setPeerShock] = useState<string>('none');
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  if (!open) return null;

  const submit = async () => {
    setBusy(true);
    setErr(null);
    try {
      const dna = await captureFamilyCalibration(familyId, {
        primaryWorryCode: worry,
        resourceBandCode: resource,
        schoolContextCode: school,
        selfViewCode: selfView,
        peerShockCode: peerShock,
      });
      onSaved(dna);
      onClose();
    } catch {
      setErr('Chưa lưu được. Thử lại sau nhé.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="famixa-cal-sheet"
      role="dialog"
      aria-modal="true"
      aria-label="Điều bố mẹ đang quan tâm"
    >
      <button type="button" className="famixa-cal-backdrop" aria-label="Đóng" onClick={onClose} />
      <div className="famixa-cal-panel">
        <header>
          <strong>Điều bố mẹ đang quan tâm — đúng nhà mình</strong>
          <p>Không so sánh xếp hạng. Giúp tránh tự ti · thiếu cố gắng · dễ hư.</p>
        </header>

        <fieldset>
          <legend>Lo lớn nhất của nhà mình lúc này?</legend>
          {WORRY_OPTIONS.map((o) => (
            <label key={o.value} className={worry === o.value ? 'is-on' : undefined}>
              <input
                type="radio"
                name="worry"
                value={o.value}
                checked={worry === o.value}
                onChange={() => setWorry(o.value)}
              />
              <span>
                <b>{o.label}</b>
                <em>{o.hint}</em>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset>
          <legend>Thời gian / điều kiện nhà mình gần với đâu?</legend>
          {RESOURCE_OPTIONS.map((o) => (
            <label key={o.value} className={resource === o.value ? 'is-on' : undefined}>
              <input
                type="radio"
                name="resource"
                value={o.value}
                checked={resource === o.value}
                onChange={() => setResource(o.value)}
              />
              <span>
                <b>{o.label}</b>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset>
          <legend>Môi trường học của con?</legend>
          {SCHOOL_OPTIONS.map((o) => (
            <label key={o.value} className={school === o.value ? 'is-on' : undefined}>
              <input
                type="radio"
                name="school"
                value={o.value}
                checked={school === o.value}
                onChange={() => setSchool(o.value)}
              />
              <span>
                <b>{o.label}</b>
                <em>{o.hint}</em>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset>
          <legend>Con tự đánh giá năng lực thế nào?</legend>
          {SELF_VIEW_OPTIONS.map((o) => (
            <label key={o.value} className={selfView === o.value ? 'is-on' : undefined}>
              <input
                type="radio"
                name="selfView"
                value={o.value}
                checked={selfView === o.value}
                onChange={() => setSelfView(o.value)}
              />
              <span>
                <b>{o.label}</b>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset>
          <legend>Gần đây có cú sốc khi gặp bạn / đề ngoài không?</legend>
          {PEER_SHOCK_OPTIONS.map((o) => (
            <label key={o.value} className={peerShock === o.value ? 'is-on' : undefined}>
              <input
                type="radio"
                name="peerShock"
                value={o.value}
                checked={peerShock === o.value}
                onChange={() => setPeerShock(o.value)}
              />
              <span>
                <b>{o.label}</b>
              </span>
            </label>
          ))}
        </fieldset>

        {err ? <p className="famixa-cal-err">{err}</p> : null}

        <div className="famixa-cal-actions">
          <button type="button" className="is-ghost" onClick={onClose} disabled={busy}>
            Để sau
          </button>
          <button type="button" className="is-primary" onClick={() => void submit()} disabled={busy}>
            {busy ? 'Đang lưu…' : 'Lưu & xem bước tiếp'}
          </button>
        </div>
      </div>
    </div>
  );
}
