import { Checkbox } from 'antd';
import type { SeriesShotRun } from '../content-famixa-series';

export function ShotProductionQa({
  shotQa,
  spoken,
  onChange,
  tone = 'staff',
  afterLipsync = false,
  afterMix = false,
}: {
  shotQa?: SeriesShotRun['shotQa'];
  spoken: boolean;
  onChange: (next: NonNullable<SeriesShotRun['shotQa']>) => void;
  tone?: 'director' | 'staff';
  afterLipsync?: boolean;
  afterMix?: boolean;
}) {
  const qa = shotQa ?? {};
  const set = (key: 'action' | 'continuity' | 'voiceFace' | 'lipsyncQuality' | 'finalAv', value: boolean) => {
    onChange({ ...qa, [key]: value });
  };
  const director = tone === 'director';
  const showQuality = spoken && (tone !== 'director' || afterLipsync);
  const showFinal = spoken && (tone !== 'director' || afterMix || afterLipsync);
  return (
    <div className="fx-desk__note">
      <p>{director ? 'Video đã ổn chưa?' : 'Kiểm tra cảnh'}</p>
      <div>
        <Checkbox checked={Boolean(qa.action)} onChange={(e) => set('action', e.target.checked)}>
          {director ? 'Hành động đúng chưa?' : 'Hành động phù hợp'}
        </Checkbox>
      </div>
      <div>
        <Checkbox checked={Boolean(qa.continuity)} onChange={(e) => set('continuity', e.target.checked)}>
          {director ? 'Cảnh có liền mạch không?' : 'Continuity ổn'}
        </Checkbox>
      </div>
      {spoken ? (
        <div>
          <Checkbox checked={Boolean(qa.voiceFace)} onChange={(e) => set('voiceFace', e.target.checked)}>
            {director ? 'Giọng và mặt khớp không?' : 'Giọng / khuôn mặt phù hợp'}
          </Checkbox>
        </div>
      ) : null}
      {showQuality ? (
        <div>
          <Checkbox checked={Boolean(qa.lipsyncQuality)} onChange={(e) => set('lipsyncQuality', e.target.checked)}>
            {director ? 'Môi khớp thoại chưa?' : 'Lip-sync đạt (không chỉ vì đã có file)'}
          </Checkbox>
        </div>
      ) : null}
      {showFinal ? (
        <div>
          <Checkbox checked={Boolean(qa.finalAv)} onChange={(e) => set('finalAv', e.target.checked)}>
            {director ? 'Hình và tiếng ổn chưa?' : 'Final A/V đạt'}
          </Checkbox>
        </div>
      ) : null}
    </div>
  );
}
