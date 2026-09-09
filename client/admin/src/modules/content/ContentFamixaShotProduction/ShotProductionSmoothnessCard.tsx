import { Button } from 'antd';
import type { FamixaSeriesShot, SeriesPilotState } from '../content-famixa-series';
import { shotRunOf } from '../content-famixa-series';
import {
  coverageOf,
  lastFrameEligible,
  lastFrameFromUrlOf,
  previousShotOf,
  type CameraPath,
  type CoverageSize,
  type EditorialPreset,
  type EpisodeSmoothness,
  type ShotCoverage,
} from '../famixa-shot-smoothness-contract';
import { dialogueFloorOf, editorialDurationOf, isDirectorEditorial, performanceDurationOf } from '../famixa-shot-production-timing';

const SIZE_CHIPS: { id: CoverageSize; label: string }[] = [
  { id: 'WIDE', label: 'Wide' },
  { id: 'MS', label: 'Medium' },
  { id: 'MCU', label: 'Cận vừa' },
  { id: 'ECU', label: 'Cận mặt' },
  { id: 'OTS', label: 'Qua vai' },
];

const PATH_CHIPS: { id: CameraPath; label: string }[] = [
  { id: 'HOLD', label: 'Máy đứng' },
  { id: 'PUSH_IN', label: 'Đẩy vào' },
  { id: 'TRACK', label: 'Theo người' },
];

export function ShotProductionSmoothnessCard({
  state,
  shot,
  smoothness,
  onCoverage,
  onEditorial,
  onSmoothness,
}: {
  state: SeriesPilotState;
  shot: FamixaSeriesShot;
  smoothness: Required<EpisodeSmoothness>;
  onCoverage: (next: ShotCoverage) => void;
  onEditorial: (preset: EditorialPreset) => void;
  onSmoothness?: (next: EpisodeSmoothness) => void;
}) {
  const prev = previousShotOf(state, shot);
  const coverage = coverageOf(state, shot, prev);
  const performance = performanceDurationOf(state, shot);
  const editorial = editorialDurationOf(state, shot);
  const floor = dialogueFloorOf(state, shot);
  const lastUrl = lastFrameFromUrlOf(state, shot);
  const lastOk = lastFrameEligible({ coverage, previousTakeUrl: lastUrl });
  const directorCut = isDirectorEditorial(shot);

  return (
    <div className="fx-media-card" data-smoothness="director">
      <h3 style={{ margin: '0 0 4px' }}>Nhịp hình</h3>
      <p className="fx-desk__note" style={{ margin: '0 0 12px' }}>
        Đổi góc / cắt editorial trên graph. Take đang có không đổi cho đến khi Confirm chuyển động. Không photoreal.
      </p>
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Góc máy</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {SIZE_CHIPS.map((chip) => (
          <Button
            key={chip.id}
            size="small"
            type={coverage.size === chip.id ? 'primary' : 'default'}
            onClick={() => onCoverage({ ...coverage, size: chip.id, source: 'DIRECTOR' })}
          >
            {chip.label}
          </Button>
        ))}
      </div>
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Đường máy</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {PATH_CHIPS.map((chip) => (
          <Button
            key={chip.id}
            size="small"
            type={coverage.cameraPath === chip.id ? 'primary' : 'default'}
            onClick={() => onCoverage({ ...coverage, cameraPath: chip.id, source: 'DIRECTOR' })}
          >
            {chip.label}
          </Button>
        ))}
      </div>
      <p className="fx-desk__note">
        Cắt: {coverage.bridge === 'LAST_FRAME' ? 'nối last-frame (Confirm I2V mới)' : 'cắt cứng'}
        {lastOk ? ' · có take trước' : ''}
      </p>
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Cắt Mix</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        <Button size="small" type={!directorCut ? 'primary' : 'default'} onClick={() => onEditorial('FULL_TAKE')}>
          Nguyên take {performance ? `${performance}s` : ''}
        </Button>
        <Button
          size="small"
          type={directorCut ? 'primary' : 'default'}
          disabled={floor <= 0}
          onClick={() => onEditorial('DIALOGUE_FLOOR')}
        >
          Sát thoại {floor ? `${floor}s` : ''}
        </Button>
      </div>
      <p className="fx-desk__note">Mix đang giữ {editorial}s. Bấm Hoàn thiện lại sau khi đổi cắt — 0 cr.</p>
      {onSmoothness ? (
        <>
          <p style={{ margin: '12px 0 6px', fontWeight: 600 }}>Mix tập</p>
          <div className="fx-desk__btns">
            <Button
              size="small"
              type={smoothness.grade ? 'primary' : 'default'}
              onClick={() => onSmoothness({ ...smoothness, grade: !smoothness.grade })}
            >
              Grade 2.5D
            </Button>
            <Button
              size="small"
              type={smoothness.colorMatch ? 'primary' : 'default'}
              onClick={() => onSmoothness({ ...smoothness, colorMatch: !smoothness.colorMatch })}
            >
              Khớp màu
            </Button>
            <Button
              size="small"
              type={smoothness.interpolate ? 'primary' : 'default'}
              onClick={() => onSmoothness({ ...smoothness, interpolate: !smoothness.interpolate })}
            >
              Nội suy khung
            </Button>
          </div>
          <p className="fx-desk__note">Nhạc/phòng đã trong Mix. Nội suy khung chỉ khi bật — chậm, không thay take.</p>
        </>
      ) : null}
      <p className="fx-desk__note" data-transition={shotRunOf(state, shot).transitionType || ''}>
        {prev ? `Shot trước: ${prev.id}` : 'Shot đầu — không last-frame'}
      </p>
    </div>
  );
}
