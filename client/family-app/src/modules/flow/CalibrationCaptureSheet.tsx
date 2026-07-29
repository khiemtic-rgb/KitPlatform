import { useState } from 'react';
import {
  captureFamilyCalibration,
  type FamilyDnaCard,
} from '@/shared/api/family-os.api';

const WORRY_OPTIONS = [
  {
    value: 'tu_ti',
    label: 'Con tự ti / sợ thử',
    hint: 'Rút lui sau khi thấy bạn giỏi hơn',
  },
  {
    value: 'thieu_phan_dau',
    label: 'Con thiếu phấn đấu / tưởng mình khá',
    hint: 'Lướt bài, né khó, ảo giác',
  },
  {
    value: 'de_hu',
    label: 'Chiều quá / sợ con dễ hư',
    hint: 'Thiếu khung, thương = chiều',
  },
  {
    value: 'balance_ok',
    label: 'Đang giữ cân bằng ổn',
    hint: 'Muốn giữ nhịp hiện diện + tiêu chuẩn vừa',
  },
] as const;

const RESOURCE_OPTIONS = [
  { value: 'tight', label: 'Ít thời gian / điều kiện eo hẹp' },
  { value: 'moderate', label: 'Trung bình — đủ giữ nhịp nhẹ' },
  { value: 'abundant', label: 'Dư thời gian / điều kiện hơn' },
] as const;

const SCHOOL_OPTIONS = [
  {
    value: 'bubble_easy',
    label: 'Ít gặp bạn trường khác / ít thi ngoài',
    hint: 'Dễ tạo “ảo giác khá” nếu ít đối chiếu',
  },
  {
    value: 'mixed',
    label: 'Thỉnh thoảng có đối chiếu ngoài',
    hint: 'Có giao lưu / đề ngoài mức vừa',
  },
  {
    value: 'competitive',
    label: 'Hay thi / cạnh tranh với môi trường khó hơn',
    hint: 'Đối chiếu ngoài thường xuyên',
  },
] as const;

const SELF_VIEW_OPTIONS = [
  { value: 'overestimates', label: 'Con thường tự đánh giá cao hơn thực lực' },
  { value: 'calibrated', label: 'Con đánh giá khá sát' },
  { value: 'underestimates', label: 'Con thường tự đánh giá thấp / thiếu tự tin' },
] as const;

const PEER_SHOCK_OPTIONS = [
  { value: 'none', label: 'Chưa thấy cú sốc' },
  { value: 'mild', label: 'Có chạnh lòng nhẹ khi gặp bạn ngoài' },
  { value: 'sharp', label: 'Sụp tự tin rõ sau khi đối chiếu ngoài' },
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
    <div className="famixa-cal-sheet" role="dialog" aria-modal="true" aria-label="Quan tâm có phương pháp">
      <button type="button" className="famixa-cal-backdrop" aria-label="Đóng" onClick={onClose} />
      <div className="famixa-cal-panel">
        <header>
          <strong>Quan tâm có phương pháp — đúng nhà bạn</strong>
          <p>Không xếp hạng. Giúp tránh tự ti · thiếu phấn đấu · dễ hư.</p>
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
