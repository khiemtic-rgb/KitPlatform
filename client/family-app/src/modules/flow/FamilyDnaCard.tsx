import { useState } from 'react';
import type { FamilyDnaCard } from '@/shared/api/family-os.api';
import { CalibrationCaptureSheet } from '@/modules/flow/CalibrationCaptureSheet';

type Props = {
  familyId: string;
  dna: FamilyDnaCard | null;
  loading?: boolean;
  onUpgrade?: () => void;
  onSetup?: () => void;
  onDnaChange?: (dna: FamilyDnaCard) => void;
};

/** Family DNA snapshot — 4 lines + calibration coach tip. */
export function FamilyDnaCardView({
  familyId,
  dna,
  loading,
  onUpgrade,
  onSetup,
  onDnaChange,
}: Props) {
  const [calOpen, setCalOpen] = useState(false);

  if (loading && !dna) {
    return (
      <section className="famixa-dna" aria-busy="true" aria-label="DNA gia đình">
        <header className="famixa-dna-head">
          <span aria-hidden>🧬</span>
          <strong>DNA nhà bạn</strong>
        </header>
        <p className="famixa-dna-muted">Đang lấy hồ sơ nhà…</p>
      </section>
    );
  }

  if (!dna?.hasBlueprint) {
    return (
      <>
        <section className="famixa-dna is-empty" aria-label="DNA gia đình">
          <header className="famixa-dna-head">
            <span aria-hidden>🧬</span>
            <strong>DNA nhà bạn</strong>
          </header>
          <p className="famixa-dna-muted">
            Hoàn tất Setup Wizard để Famixa hiểu giai đoạn, giá trị và mục tiêu nhà bạn.
          </p>
          <div className="famixa-dna-actions">
            {onSetup ? (
              <button type="button" className="famixa-dna-cta" onClick={onSetup}>
                Thiết lập nhanh ›
              </button>
            ) : null}
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              Thêm tín hiệu học / tự tin ›
            </button>
          </div>
        </section>
        <CalibrationCaptureSheet
          familyId={familyId}
          open={calOpen}
          onClose={() => setCalOpen(false)}
          onSaved={(d) => onDnaChange?.(d)}
        />
      </>
    );
  }

  const values = dna.valuesLabelsVi.filter(Boolean);
  const focus = dna.focusLabelsVi.filter(Boolean);
  const showCalCta =
    dna.needsCalibrationCapture ||
    dna.calibrationPhaseCode === 'needs_capture' ||
    dna.calibrationPhaseCode === 'bubble_risk' ||
    dna.calibrationPhaseCode === 'peer_shock';

  return (
    <>
      <section
        className={`famixa-dna${dna.isTeaser ? ' is-teaser' : ''}${
          dna.calibrationPhaseCode === 'peer_shock' ? ' is-shock' : ''
        }`}
        aria-label="DNA gia đình"
      >
        <header className="famixa-dna-head">
          <span aria-hidden>🧬</span>
          <strong>DNA nhà bạn</strong>
          {dna.growthBalanceLabelVi ? <em>{dna.growthBalanceLabelVi}</em> : null}
          {!dna.growthBalanceLabelVi && dna.calibrationLabelVi ? (
            <em>{dna.calibrationLabelVi}</em>
          ) : null}
          {dna.isTeaser && !dna.growthBalanceLabelVi && !dna.calibrationLabelVi ? (
            <em>Bản rút gọn</em>
          ) : null}
        </header>

        {dna.careValueVi ? <p className="famixa-dna-care">{dna.careValueVi}</p> : null}

        <dl className="famixa-dna-grid">
          {dna.stageLabelVi ? (
            <div>
              <dt>Stage</dt>
              <dd>{dna.stageLabelVi}</dd>
            </div>
          ) : null}
          {values.length > 0 ? (
            <div>
              <dt>Values</dt>
              <dd>{values.join(' · ')}</dd>
            </div>
          ) : null}
          {focus.length > 0 ? (
            <div>
              <dt>Focus</dt>
              <dd>{focus.join(' · ')}</dd>
            </div>
          ) : null}
          {dna.nextStepVi ? (
            <div className="is-next">
              <dt>Next step</dt>
              <dd>{dna.nextStepVi}</dd>
            </div>
          ) : null}
        </dl>

        {dna.coachTipVi ? <p className="famixa-dna-tip">{dna.coachTipVi}</p> : null}

        <div className="famixa-dna-actions">
          {showCalCta ? (
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              {dna.needsCalibrationCapture
                ? 'Chọn lo lớn nhất của nhà ›'
                : 'Cập nhật quan tâm có phương pháp ›'}
            </button>
          ) : (
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              Điều chỉnh tín hiệu ›
            </button>
          )}
        </div>

        {dna.isTeaser ? (
          <div className="famixa-dna-lock">
            <p>{dna.upgradeHintVi || 'Mở Peace Plan để xem Focus & bước tiếp theo.'}</p>
            {onUpgrade ? (
              <button type="button" className="famixa-dna-cta" onClick={onUpgrade}>
                Xem Peace Plan ›
              </button>
            ) : null}
          </div>
        ) : null}
      </section>

      <CalibrationCaptureSheet
        familyId={familyId}
        open={calOpen}
        onClose={() => setCalOpen(false)}
        onSaved={(d) => onDnaChange?.(d)}
      />
    </>
  );
}
