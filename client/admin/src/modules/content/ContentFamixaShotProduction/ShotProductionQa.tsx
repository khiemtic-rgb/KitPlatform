import { Button, Checkbox } from 'antd';
import type { SeriesShotRun } from '../content-famixa-series';

export function ShotProductionQa({
  shotQa,
  spoken,
  onChange,
  onDecision,
  reviewTarget,
  tone = 'staff',
  afterLipsync = false,
  afterMix = false,
}: {
  shotQa?: SeriesShotRun['shotQa'];
  spoken: boolean;
  onChange: (next: NonNullable<SeriesShotRun['shotQa']>) => void;
  onDecision?: (decision: 'ACCEPT' | 'REJECT') => void;
  reviewTarget?: string;
  tone?: 'director' | 'staff';
  afterLipsync?: boolean;
  afterMix?: boolean;
}) {
  const qa = shotQa ?? {};
  const set = (key: 'action' | 'continuity' | 'motion' | 'voiceFace' | 'lipsyncQuality' | 'finalAv', value: boolean) => {
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
      <div>
        <Checkbox checked={Boolean(qa.motion)} onChange={(e) => set('motion', e.target.checked)}>
          {director ? 'Chuyển động làm đúng nhịp chưa?' : 'Chuyển động thực hiện nhịp'}
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
      {onDecision ? (
        <div>
          <p>
            {reviewTarget
              ? `Quyết định cho ${reviewTarget}. Ô đánh dấu không ghi nhận duyệt.`
              : 'Chưa có đúng tệp đang phát. Ô đánh dấu không ghi nhận duyệt.'}
          </p>
          <Button type="primary" onClick={() => onDecision('ACCEPT')}>Chấp nhận tệp này</Button>
          <Button onClick={() => onDecision('REJECT')}>Từ chối tệp này</Button>
        </div>
      ) : null}
    </div>
  );
}
