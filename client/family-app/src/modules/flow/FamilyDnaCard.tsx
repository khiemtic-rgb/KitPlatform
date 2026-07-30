import { useState } from 'react';
import type { FamilyDnaCard } from '@/shared/api/family-os.api';
import { CalibrationCaptureSheet } from '@/modules/flow/CalibrationCaptureSheet';
import { canonicalSchoolStageLabelVi } from '@/shared/onboarding/onboarding';

type Props = {
  familyId: string;
  dna: FamilyDnaCard | null;
  loading?: boolean;
  /** Khi nhà có nhiều con — DNA là hồ sơ cả gia đình, không gắn 1 đứa. */
  houseScopeNote?: string | null;
  onUpgrade?: () => void;
  onSetup?: () => void;
  onDnaChange?: (dna: FamilyDnaCard) => void;
};

/** Soft-rewrite câu DNA cũ trong DB — giữ nghĩa, bỏ từ khô. */
function warmDnaCopy(text: string | null | undefined): string {
  const raw = (text ?? '').trim();
  if (!raw) return '';
  return raw
    .replace(/quan tâm có phương pháp/gi, 'quan tâm đúng nhịp nhà mình')
    .replace(/quan tâm có giá trị khi/gi, 'quan tâm có ý nghĩa khi')
    .replace(/không theo nhà khác/gi, 'không chạy theo nhà khác')
    .replace(/đúng nhịp nhà bạn/gi, 'đúng nhịp nhà mình')
    .replace(/ảo giác khá/gi, 'tưởng mình khá')
    .replace(/thiếu phấn đấu/gi, 'chưa chịu cố')
    .replace(/như checklist/gi, 'như danh sách việc')
    .replace(/điểm cân bằng/gi, 'điểm ổn')
    .replace(/bằng chứng học thật/gi, 'việc học thật');
}

function DnaHead({
  houseScopeNote,
  growthBalanceLabelVi,
  calibrationLabelVi,
  isTeaser,
}: {
  houseScopeNote?: string | null;
  growthBalanceLabelVi?: string | null;
  calibrationLabelVi?: string | null;
  isTeaser?: boolean;
}) {
  return (
    <header className="famixa-dna-head">
      <span aria-hidden>🧬</span>
      <strong>DNA gia đình bạn</strong>
      {houseScopeNote ? <em>{houseScopeNote}</em> : null}
      {!houseScopeNote && growthBalanceLabelVi ? (
        <em>{warmDnaCopy(growthBalanceLabelVi)}</em>
      ) : null}
      {!houseScopeNote && !growthBalanceLabelVi && calibrationLabelVi ? (
        <em>{warmDnaCopy(calibrationLabelVi)}</em>
      ) : null}
      {!houseScopeNote && !growthBalanceLabelVi && !calibrationLabelVi && isTeaser ? (
        <em>Đang xem bản ngắn</em>
      ) : null}
    </header>
  );
}

/** Family DNA snapshot — 4 lines + calibration coach tip. */
export function FamilyDnaCardView({
  familyId,
  dna,
  loading,
  houseScopeNote,
  onUpgrade,
  onSetup,
  onDnaChange,
}: Props) {
  const [calOpen, setCalOpen] = useState(false);

  if (loading && !dna) {
    return (
      <section className="famixa-dna" aria-busy="true" aria-label="DNA gia đình">
        <DnaHead houseScopeNote={houseScopeNote} />
        <p className="famixa-dna-muted">Famixa đang nhớ lại nhịp nhà bạn…</p>
      </section>
    );
  }

  if (!dna?.hasBlueprint) {
    return (
      <>
        <section className="famixa-dna is-empty" id="famixa-dna" aria-label="DNA gia đình">
          <DnaHead houseScopeNote={houseScopeNote} />
          <p className="famixa-dna-muted">
            Trả lời vài câu ngắn để Famixa hiểu độ tuổi, điều nhà mình quý và việc muốn tập
            cùng con.
          </p>
          <div className="famixa-dna-actions">
            {onSetup ? (
              <button type="button" className="famixa-dna-cta" onClick={onSetup}>
                Bắt đầu cùng Famixa ›
              </button>
            ) : null}
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              Kể Famixa lo gì về học / tự tin ›
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
        id="famixa-dna"
        className={`famixa-dna${dna.isTeaser ? ' is-teaser' : ''}${
          dna.calibrationPhaseCode === 'peer_shock' ? ' is-shock' : ''
        }`}
        aria-label="DNA gia đình"
      >
        <DnaHead
          houseScopeNote={houseScopeNote}
          growthBalanceLabelVi={dna.growthBalanceLabelVi}
          calibrationLabelVi={dna.calibrationLabelVi}
          isTeaser={dna.isTeaser}
        />

        {dna.careValueVi ? (
          <p className="famixa-dna-care">{warmDnaCopy(dna.careValueVi)}</p>
        ) : null}

        <dl className="famixa-dna-grid">
          {(() => {
            const stage = canonicalSchoolStageLabelVi(dna.stageLabelVi);
            return stage ? (
              <div>
                <dt>Độ tuổi</dt>
                <dd>{stage}</dd>
              </div>
            ) : null;
          })()}
          {values.length > 0 ? (
            <div>
              <dt>Nhà mình quý</dt>
              <dd>{values.join(' · ')}</dd>
            </div>
          ) : null}
          {focus.length > 0 ? (
            <div>
              <dt>Đang tập cùng con</dt>
              <dd>{focus.join(' · ')}</dd>
            </div>
          ) : null}
          {dna.nextStepVi ? (
            <div className="is-next">
              <dt>Việc nhỏ tiếp theo</dt>
              <dd>{warmDnaCopy(dna.nextStepVi)}</dd>
            </div>
          ) : null}
        </dl>

        {dna.coachTipVi ? (
          <p className="famixa-dna-tip">{warmDnaCopy(dna.coachTipVi)}</p>
        ) : null}

        <div className="famixa-dna-actions">
          {showCalCta ? (
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              {dna.needsCalibrationCapture
                ? 'Kể Famixa lo lớn nhất của nhà ›'
                : 'Cập nhật điều bố mẹ đang quan tâm ›'}
            </button>
          ) : (
            <button type="button" className="famixa-dna-cta is-soft" onClick={() => setCalOpen(true)}>
              Điều chỉnh nhẹ cho đúng nhà mình ›
            </button>
          )}
        </div>

        {dna.isTeaser ? (
          <div className="famixa-dna-lock">
            <p>
              {warmDnaCopy(dna.upgradeHintVi) ||
                'Mở Peace Plan để xem việc nhà đang tập cùng con và bước nhỏ tiếp theo.'}
            </p>
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
