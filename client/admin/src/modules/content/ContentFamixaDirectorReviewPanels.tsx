import { Link } from 'react-router-dom';
import { Button, Collapse } from 'antd';
import type { IdentityConditionedCalibrationPackReview, IdentityConditionedCalibrationSubjectReview } from '@/shared/api/content.api';
import {
  DIRECTOR_REVIEW_BACK_CALIBRATION,
  DIRECTOR_REVIEW_COMPLETE_TITLE,
  DIRECTOR_REVIEW_COMPLETE_VI,
  DIRECTOR_REVIEW_CONTINUE_FOUNDATION,
  DIRECTOR_REVIEW_CONTINUE_FOUNDATION_VI,
  DIRECTOR_REVIEW_DECISIONS,
  DIRECTOR_REVIEW_EXIT_LABEL,
  DIRECTOR_REVIEW_FAIL_HINT,
  DIRECTOR_REVIEW_FAIL_REASONS,
  DIRECTOR_REVIEW_FOUNDATION_NEXT,
  DIRECTOR_REVIEW_ADVANCED_ENGINEERING,
  DIRECTOR_REVIEW_GATE_GROUPS,
  DIRECTOR_REVIEW_GATE_HELP,
  DIRECTOR_REVIEW_GATE_LABELS,
  DIRECTOR_REVIEW_GATES,
  DIRECTOR_REVIEW_IMAGES_BLOCKED_LINE,
  DIRECTOR_REVIEW_IMAGES_READY_LINE,
  DIRECTOR_REVIEW_READY_TITLE,
  DIRECTOR_REVIEW_GUIDE,
  DIRECTOR_REVIEW_GUIDE_VI,
  DIRECTOR_REVIEW_HISTORICAL_LABEL,
  DIRECTOR_REVIEW_FOUNDATION_DONE,
  DIRECTOR_REVIEW_FOUNDATION_READY,
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_VI,
  DIRECTOR_REVIEW_MARK_PASS_WARNING,
  DIRECTOR_REVIEW_NO_REGEN,
  DIRECTOR_REVIEW_NOTE_PLACEHOLDER,
  DIRECTOR_REVIEW_PACK_SUMMARY_LABEL,
  DIRECTOR_REVIEW_PACK_UNIVERSE_GATES,
  DIRECTOR_REVIEW_PASS_BLOCKED,
  DIRECTOR_REVIEW_PASSED_LABEL,
  DIRECTOR_REVIEW_PASSED_VI,
  DIRECTOR_REVIEW_REVIEW_FAILED,
  DIRECTOR_REVIEW_REVIEW_FAILED_VI,
  DIRECTOR_REVIEW_REVIEW_NEXT,
  DIRECTOR_REVIEW_REVIEW_NEXT_VI,
  DIRECTOR_REVIEW_REVIEW_REQUIRED_CTA,
  DIRECTOR_REVIEW_REVIEW_REQUIRED_VI,
  DIRECTOR_REVIEW_SAVE_LABEL,
  directorReviewMarkPassEnabled,
  directorReviewMarkPassReasons,
  directorReviewNoteRequired,
  directorReviewOverallLabel,
  directorReviewPackScopeStatus,
  directorReviewPassChecklist,
  directorReviewPendingCount,
  directorReviewNavStatus,
  directorReviewSubjectChipVi,
  directorReviewSubjectCue,
  directorReviewSubjectMark,
  directorReviewSubjectRoleLine,
  directorReviewSubjectScopeStatus,
  directorReviewSubjectStatusVi,
  directorReviewTechnicalBlockReasons,
  directorReviewWorkflowPhase,
  directorReviewSubjectsReviewed,
  directorReviewWorkspaceBadge,
  emptyDirectorReviewGates,
  type DirectorReviewDecision,
  type DirectorReviewGates,
  type DirectorReviewPackUniverseGates,
} from './kit-video-visual-calibration';

export function reviewStatusColor(value?: string) {
  if (value === 'PASS' || value === 'VISUAL CALIBRATION PASSED' || value === 'READY TO PASS') return 'green';
  if (value === 'FAIL' || value === 'REVIEW BLOCKED') return 'red';
  if (value === 'REVIEW_REQUIRED' || value === 'REVIEW IN PROGRESS') return 'orange';
  return 'default';
}

function badgeClass(value?: string | null) {
  const d = (value || 'PENDING').toUpperCase();
  if (d === 'PASS' || d === 'VISUAL CALIBRATION PASSED' || d === 'READY TO PASS') return 'is-pass';
  if (d === 'FAIL' || d === 'REVIEW BLOCKED') return 'is-fail';
  if (d === 'REVIEW_REQUIRED' || d === 'REVIEW IN PROGRESS') return 'is-review';
  return 'is-pending';
}

export function ContentFamixaDirectorReviewHeader({
  review,
  onExit,
  onOpenSummary,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  onExit: string;
  onOpenSummary?: () => void;
}) {
  const badge = directorReviewWorkspaceBadge(review);
  const pending = directorReviewPendingCount(review?.subjects);
  const reviewed = directorReviewSubjectsReviewed(review?.subjects);
  const packStatus = directorReviewPackScopeStatus(review);
  const technicalPass = (review?.technicalIntegrityValid ?? 0) === 24;
  return (
    <header className="fx-drws__bar">
      <div>
        <p className="fx-drws__brand">FAMIXA VISUAL CALIBRATION</p>
        <p className="fx-drws__kicker">DIRECTOR REVIEW</p>
        <h1>Director Review</h1>
        <p>Visual Calibration</p>
        <p>Character Review</p>
        <p>EP01 · Tập 01</p>
        <p className="fx-drws__human-progress">
          Progress: {review?.subjectPass ?? 0} / 6 characters approved
        </p>
      </div>
      <div className="fx-drws__stats" aria-label="Review summary">
        <span className={`fx-drws__badge ${badgeClass(badge)}`}>{badge}</span>
        <div>
          <em>STATUS</em>
          <b>DIRECTOR REVIEW</b>
        </div>
        <div>
          <em>PACK REVIEW</em>
          <b>{packStatus}</b>
        </div>
        <div>
          <em>PROGRESS</em>
          <b>{reviewed}/6 subjects reviewed</b>
        </div>
        <div>
          <em>SUBJECTS</em>
          <b>{review?.subjectExpected ?? 6} total</b>
        </div>
        <div>
          <em>PASS</em>
          <b>{review?.subjectPass ?? 0}/{review?.subjectExpected ?? 6}</b>
        </div>
        <div>
          <em>FAIL</em>
          <b>{review?.fail ?? 0}</b>
        </div>
        <div>
          <em>PENDING</em>
          <b>{pending}</b>
        </div>
        <div>
          <em>TECHNICAL</em>
          <b>
            {technicalPass ? '✓ PASS' : 'PENDING'}
            {' · '}
            {review?.technicalIntegrityValid ?? 0}/{review?.technicalIntegrityExpected ?? 24}
          </b>
        </div>
        <div>
          <em>VISUAL PASS</em>
          <b>{review?.visualPass ? 'TRUE' : 'FALSE'}</b>
        </div>
      </div>
      <div className="fx-drws__head-actions">
        {onOpenSummary ? (
          <Button onClick={onOpenSummary}>{DIRECTOR_REVIEW_PACK_SUMMARY_LABEL}</Button>
        ) : null}
        <Link to={onExit}>
          <Button type="primary">{DIRECTOR_REVIEW_BACK_CALIBRATION}</Button>
        </Link>
        <Link to={onExit}>
          <Button>Mở Visual Calibration</Button>
        </Link>
        <Link to={onExit}>
          <Button>{DIRECTOR_REVIEW_EXIT_LABEL}</Button>
        </Link>
      </div>
    </header>
  );
}

export function ContentFamixaDirectorPackOverview({
  review,
}: {
  review?: IdentityConditionedCalibrationPackReview;
}) {
  const reviewed = directorReviewSubjectsReviewed(review?.subjects);
  const technicalPass = (review?.technicalIntegrityValid ?? 0) === 24;
  const packStatus = directorReviewPackScopeStatus(review);
  const visualLabel = review?.visualPass
    ? `PASS · ${reviewed} / 6`
    : `${packStatus === 'PENDING' ? 'PENDING' : packStatus} · ${reviewed} / 6`;
  return (
    <section className="fx-drws__overview" aria-label="Pack overview">
      <h2>PACK OVERVIEW</h2>
      <p className="fx-drws__kicker">VISUAL FOUNDATION</p>
      <div className="fx-drws__overview-grid">
        <div>
          <em>Characters</em>
          <b>6 Characters</b>
        </div>
        <div>
          <em>Images</em>
          <b>24 Images</b>
        </div>
        <div>
          <em>Views / Character</em>
          <b>4 Views / Character</b>
        </div>
        <div>
          <em>Technical Integrity</em>
          <b>{technicalPass ? '✓ PASS' : 'PENDING'} · {review?.technicalIntegrityValid ?? 0} / 24</b>
        </div>
        <div>
          <em>Identity Anchors</em>
          <b>{review?.frontAnchorsValid ?? 0} / 6</b>
        </div>
        <div>
          <em>Identity-conditioned</em>
          <b>{review?.identityConditionedValid ?? 0} / 18</b>
        </div>
        <div>
          <em>Director Visual Review</em>
          <b>{visualLabel}</b>
        </div>
        <div>
          <em>Pack Decision</em>
          <b>{packStatus}</b>
        </div>
      </div>
    </section>
  );
}

export function ContentFamixaDirectorWorkflowBanner({
  review,
  busy,
  onReviewNext: _onReviewNext,
  onReviewFailed,
  onReviewRequired,
  onMark,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  busy?: boolean;
  onReviewNext: () => void;
  onReviewFailed: () => void;
  onReviewRequired: () => void;
  onMark: () => void;
}) {
  const phase = directorReviewWorkflowPhase(review);
  const passed = review?.subjectPass ?? 0;
  const remaining = 6 - passed;
  if (phase === 'PASSED') {
    return (
      <section className="fx-drws__phase is-pass" aria-label="Workflow state">
        <h2>VISUAL CALIBRATION PASSED</h2>
        <p>{DIRECTOR_REVIEW_PASSED_VI}</p>
        <p>6 / 6 CHARACTERS</p>
        <p>24 / 24 CALIBRATION IMAGES</p>
        <p>Director-approved visual calibration.</p>
        <p>Identity foundation: APPROVED</p>
        <p>Technical integrity: 24 / 24 PASS</p>
        <p>Director review: 6 / 6 PASS</p>
        <p>Visual Pass: TRUE</p>
        <p>Authority SHA: UNCHANGED</p>
        <p>{DIRECTOR_REVIEW_FOUNDATION_READY}</p>
        <p>{DIRECTOR_REVIEW_FOUNDATION_DONE}</p>
        <Link to="/content/videos">
          <Button type="primary">{DIRECTOR_REVIEW_CONTINUE_FOUNDATION}</Button>
        </Link>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_CONTINUE_FOUNDATION_VI}</p>
      </section>
    );
  }
  if (phase === 'TECHNICAL_BLOCK') {
    return (
      <section className="fx-drws__phase is-fail fx-drws__phase--compact" aria-label="Workflow state">
        <h2>TECHNICAL BLOCK</h2>
        <p>⚠ {DIRECTOR_REVIEW_IMAGES_BLOCKED_LINE}</p>
        <p>Review blocked. Calibration images could not be loaded.</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL} [DISABLED]</p>
      </section>
    );
  }
  if (phase === 'BLOCKED') {
    return (
      <section className="fx-drws__phase is-fail" aria-label="Workflow state">
        <h2>DIRECTOR REVIEW BLOCKED</h2>
        <p>{passed} / 6 characters approved</p>
        <p>1 character requires correction.</p>
        <Button type="primary" onClick={onReviewFailed}>
          {DIRECTOR_REVIEW_REVIEW_FAILED}
        </Button>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_REVIEW_FAILED_VI}</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL} [DISABLED]</p>
      </section>
    );
  }
  if (phase === 'INCOMPLETE') {
    return (
      <section className="fx-drws__phase is-review" aria-label="Workflow state">
        <h2>DIRECTOR REVIEW INCOMPLETE</h2>
        <p>{passed} / 6 characters approved</p>
        <p>1 character requires additional review.</p>
        <Button type="primary" onClick={onReviewRequired}>
          {DIRECTOR_REVIEW_REVIEW_REQUIRED_CTA}
        </Button>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_REVIEW_REQUIRED_VI}</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL} [DISABLED]</p>
      </section>
    );
  }
  if (phase === 'READY') {
    return (
      <section className="fx-drws__phase is-ready" aria-label="Workflow state">
        <h2>VISUAL CALIBRATION READY</h2>
        <p>{DIRECTOR_REVIEW_COMPLETE_TITLE}</p>
        <p>{DIRECTOR_REVIEW_COMPLETE_VI}</p>
        <p>✓ 6 / 6 characters approved</p>
        <p>✓ All required visual reviews completed</p>
        <p>✓ No character failed</p>
        <p>✓ Director decisions recorded</p>
        <p>✓ 6 / 6 CHARACTERS PASS</p>
        <p>✓ 24 / 24 IMAGES TECHNICALLY VALID</p>
        <p>✓ 24 / 24 IMAGES VISUALLY APPROVED</p>
        <p>✓ Technical integrity PASS</p>
        <p>✓ Identity anchors 6 / 6</p>
        <p>✓ Conditioned views 18 / 18</p>
        <p>The complete Visual Calibration pack is ready for Director confirmation.</p>
        <p>READY FOR FINAL DIRECTOR CONFIRMATION</p>
        <Button type="primary" size="large" loading={busy} onClick={onMark}>
          ĐÁNH DẤU VISUAL CALIBRATION PASS
        </Button>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_LABEL}</p>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_VI}</p>
      </section>
    );
  }
  return (
    <section className="fx-drws__phase is-progress fx-drws__phase--compact" aria-label="Workflow state">
      <h2>DIRECTOR REVIEW</h2>
      <p>Visual Calibration · 6 Characters</p>
      <p>{passed} / 6 CHARACTERS APPROVED</p>
      <p>{remaining} / 6 characters remaining.</p>
      <p className="fx-desk__note">{DIRECTOR_REVIEW_REVIEW_NEXT}</p>
      <p className="fx-desk__note">{DIRECTOR_REVIEW_REVIEW_NEXT_VI}</p>
    </section>
  );
}

export function ContentFamixaDirectorReviewGuide() {
  return (
    <div className="fx-drws__guide">
      <p>
        <b>Director Review</b>
        {' — '}
        {DIRECTOR_REVIEW_GUIDE}
      </p>
      <p>{DIRECTOR_REVIEW_GUIDE_VI}</p>
    </div>
  );
}

export function ContentFamixaDirectorPackSummaryView({
  review,
  busy,
  packGates,
  onSelect,
  onMark,
  onBack,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  busy: boolean;
  packGates?: DirectorReviewPackUniverseGates;
  onSelect: (subjectId: string) => void;
  onMark: () => void;
  onBack: () => void;
}) {
  const reasons = directorReviewMarkPassReasons(review, packGates);
  const ready = directorReviewMarkPassEnabled(review);
  return (
    <section className="fx-drws__pack" aria-label="DIRECTOR REVIEW SUMMARY">
      <div className="fx-drws__pack-head">
        <h2>PACK REVIEW</h2>
        <p className="fx-drws__kicker">DIRECTOR REVIEW SUMMARY</p>
        <Button onClick={onBack}>← Back to subject</Button>
      </div>
      <ContentFamixaDirectorReviewSummary review={review} packGates={packGates} />
      <div className="fx-drws__pack-universe">
        <p>Visual Universe <b>{packGates?.VisualUniverseGate || 'PENDING'}</b></p>
        <p>Stylization <b>{packGates?.StylizationGate || 'PENDING'}</b></p>
        <p>Cross-character <b>{packGates?.CrossCharacterGate || 'PENDING'}</b></p>
        <p>Photorealism <b>{packGates?.PhotorealismGate || 'PENDING'}</b></p>
      </div>
      <p className="fx-drws__pack-ready">
        Pack Decision:
        {' '}
        <b>{ready ? 'READY FOR VISUAL CALIBRATION PASS' : directorReviewPackScopeStatus(review)}</b>
      </p>
      <ol className="fx-drws__pack-list">
        {(review?.subjects ?? []).map((subject, index) => (
          <li key={subject.subjectId}>
            <button type="button" onClick={() => onSelect(subject.subjectId)}>
              <span>{String(index + 1).padStart(2, '0')} {subject.label}</span>
              <b>{directorReviewSubjectScopeStatus(subject.decision)}</b>
            </button>
          </li>
        ))}
      </ol>
      <ContentFamixaDirectorCompletionPanel
        review={review}
        busy={busy}
        reasons={reasons}
        packReady={ready}
        onMark={onMark}
      />
      <ContentFamixaDirectorHistoricalPanel count={review?.historical?.length ?? 0} />
    </section>
  );
}

export function ContentFamixaDirectorReviewSummary({
  review,
  packGates,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  packGates?: DirectorReviewPackUniverseGates;
}) {
  const technicalPass = (review?.technicalIntegrityValid ?? 0) === 24;
  const ready = directorReviewMarkPassEnabled(review);
  const universe = packGates?.VisualUniverseGate || 'PENDING';
  return (
    <aside className="fx-drws__summary" aria-label="Review summary">
      <h2>VISUAL CALIBRATION</h2>
      <p>Subject Review: <b>{review?.subjectPass ?? 0} / {review?.subjectExpected ?? 6} PASS</b></p>
      <p>Images <b>{review?.technicalIntegrityValid ?? 0} / {review?.technicalIntegrityExpected ?? 24} READY</b></p>
      <p>Identity Anchors: <b>{review?.frontAnchorsValid ?? 0} / {review?.frontAnchorsExpected ?? 6}</b></p>
      <p>Conditioned Views: <b>{review?.identityConditionedValid ?? 0} / {review?.identityConditionedExpected ?? 18}</b></p>
      <p>
        Technical
        {' '}
        <b>{technicalPass ? '✓ PASS' : 'PENDING'} · {review?.technicalIntegrityValid ?? 0} / {review?.technicalIntegrityExpected ?? 24}</b>
      </p>
      <p>Director Visual Review <b>{review?.visualPass ? 'PASS' : 'PENDING'} · {review?.subjectPass ?? 0} / {review?.subjectExpected ?? 6}</b></p>
      <p>
        Visual Pass
        {' '}
        <b className={review?.visualPass ? 'is-pass' : ready ? 'is-review' : 'is-fail'}>
          {review?.visualPass ? 'TRUE' : ready ? 'READY TO MARK PASS' : 'FALSE'}
        </b>
      </p>
      <p>Foundation <b>{review?.foundationComplete ? 'COMPLETE' : 'NOT READY'}</b></p>
      <p className="fx-drws__sr">Visual Universe {universe}</p>
      <p className="fx-drws__summary-note">
        SUBJECTS PASS <b>{review?.subjectPass ?? 0} / {review?.subjectExpected ?? 6}</b>
        {' · '}
        VISUAL PASS <b>{review?.visualPass ? 'TRUE' : 'FALSE'}</b>
        {' · '}
        PACK DECISION <b>{review?.packDecision || 'NOT_REVIEWED'}</b>
        {' · '}
        {directorReviewOverallLabel(review)}
      </p>
    </aside>
  );
}

export function ContentFamixaDirectorSubjectNavigator({
  subjects,
  selectedSubjectId,
  thumbs,
  onSelect,
}: {
  subjects: IdentityConditionedCalibrationSubjectReview[];
  selectedSubjectId?: string;
  thumbs: Record<string, string>;
  onSelect: (subjectId: string) => void;
}) {
  return (
    <nav className="fx-drws__nav" aria-label="Subject queue">
      <h3>CHARACTER OVERVIEW</h3>
      <p className="fx-drws__kicker">SUBJECT QUEUE</p>
      <ol>
        {subjects.map((subject) => {
          const active = subject.subjectId === selectedSubjectId;
          const status = directorReviewSubjectScopeStatus(subject.decision);
          const mark = directorReviewSubjectMark(subject.decision, subject.subjectReviewStatus);
          const thumb = thumbs[`${subject.subjectId}/FRONT`];
          return (
            <li key={subject.subjectId}>
              <button
                type="button"
                className={`fx-drws__nav-item${active ? ' is-on is-current' : ''}`}
                aria-current={active ? 'true' : undefined}
                aria-label={`${subject.label} · Subject Review: ${status}${active ? ' · CURRENT' : ''}`}
                onClick={() => onSelect(subject.subjectId)}
              >
                {thumb ? <img src={thumb} alt="" /> : <span className="fx-drws__avatar" />}
                <span className="fx-drws__card-copy">
                  {active ? <em className="fx-drws__current">CURRENT</em> : null}
                  <strong>{subject.label}</strong>
                  <small>{directorReviewSubjectRoleLine(subject) || subject.subjectType}</small>
                  <small>
                    <span className="fx-drws__mark" aria-hidden="true">{mark}</span>
                    {' '}
                    {directorReviewNavStatus(subject.decision)}
                    {' · '}
                    {status}
                    {' · '}
                    {directorReviewSubjectChipVi(subject.decision)}
                    {' · '}
                    {directorReviewSubjectStatusVi(status)}
                  </small>
                  <small>{directorReviewSubjectCue(subject.decision)}</small>
                  <em>Review · Duyệt nhân vật</em>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

export function ContentFamixaDirectorUniverseOverview({
  subjects,
  thumbs,
  packGates,
  readOnly,
  onChange,
  onOpen,
}: {
  subjects: IdentityConditionedCalibrationSubjectReview[];
  thumbs: Record<string, string>;
  packGates: DirectorReviewPackUniverseGates;
  readOnly?: boolean;
  onChange: (gate: (typeof DIRECTOR_REVIEW_PACK_UNIVERSE_GATES)[number], value: DirectorReviewDecision) => void;
  onOpen?: (subjectId: string) => void;
}) {
  return (
    <section className="fx-drws__universe" aria-label="Visual Universe Overview">
      <h2>VISUAL UNIVERSE OVERVIEW</h2>
      <p className="fx-drws__kicker">Compare all 6 FRONT identity anchors</p>
      <div className="fx-drws__universe-row">
        {subjects.map((subject) => {
          const src = thumbs[`${subject.subjectId}/FRONT`];
          return (
            <button
              key={subject.subjectId}
              type="button"
              className="fx-drws__universe-card"
              onClick={() => onOpen?.(subject.subjectId)}
            >
              {src ? <img src={src} alt={`${subject.label} FRONT identity anchor`} /> : <div className="fx-clib__ph" />}
              <strong>{subject.label}</strong>
              <em>FRONT · IDENTITY ANCHOR</em>
            </button>
          );
        })}
      </div>
      <h3>UNIVERSE CHECK</h3>
      <p className="fx-desk__note">These are PACK-LEVEL gates. Subject visual gates stay on each character.</p>
      {DIRECTOR_REVIEW_PACK_UNIVERSE_GATES.map((gate) => (
        <div key={gate} className="fx-drws__gate-row">
          <strong>{DIRECTOR_REVIEW_GATE_LABELS[gate]}</strong>
          <div className="fx-drws__pills" role="group" aria-label={DIRECTOR_REVIEW_GATE_LABELS[gate]}>
            {DIRECTOR_REVIEW_DECISIONS.filter((value) => value !== 'PENDING').map((value) => (
              <button
                key={value}
                type="button"
                className={`fx-drws__pill is-${value.toLowerCase()}${packGates[gate] === value ? ' is-on' : ''}`}
                aria-pressed={packGates[gate] === value}
                disabled={readOnly}
                onClick={() => onChange(gate, value)}
              >
                {value === 'REVIEW_REQUIRED' ? 'REVIEW REQUIRED' : value}
              </button>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
}

export function ContentFamixaDirectorStickyStrip({
  label,
  index,
  total,
  imagesReady,
  gatesCompleted,
  decision,
  busy,
  disabled,
  onSave,
}: {
  label: string;
  index: number;
  total: number;
  imagesReady: number;
  gatesCompleted: number;
  decision: string;
  busy: boolean;
  disabled?: boolean;
  onSave: () => void;
}) {
  return (
    <div className="fx-drws__sticky" aria-label="Sticky review summary">
      <div>
        <em>CURRENT CHARACTER</em>
        <b>{label} · Character {index} / {total}</b>
        <span>{imagesReady}/4 images · {gatesCompleted}/10 gates · Decision: {decision}</span>
      </div>
      <Button type="primary" loading={busy} disabled={disabled} onClick={onSave}>
        {DIRECTOR_REVIEW_SAVE_LABEL}
      </Button>
    </div>
  );
}

export function ContentFamixaDirectorGatePanel({
  gates,
  technicalStatus,
  readOnly,
  onChange,
}: {
  gates: DirectorReviewGates;
  technicalStatus?: string;
  readOnly?: boolean;
  onChange: (gate: (typeof DIRECTOR_REVIEW_GATES)[number], value: DirectorReviewDecision) => void;
}) {
  return (
    <section className="fx-drws__gates">
      <h3>SUBJECT VISUAL REVIEW</h3>
      <p className="fx-drws__kicker">VISUAL REVIEW</p>
      <p className="fx-drws__kicker">DIRECTOR GATES</p>
      <p className="fx-desk__note">{DIRECTOR_REVIEW_GUIDE} {DIRECTOR_REVIEW_GUIDE_VI}</p>
      <p className="fx-drws__sr">Technical {technicalStatus || 'PENDING'}</p>
      {DIRECTOR_REVIEW_GATE_GROUPS.map((group) => (
        <div key={group.id} className="fx-drws__gate-group">
          <h4>{group.title}</h4>
          {DIRECTOR_REVIEW_GATES.map((gate) => (
            group.gates.includes(gate) ? (
        <div key={gate} className="fx-drws__gate-row" title={DIRECTOR_REVIEW_GATE_HELP[gate]}>
          <div>
            <strong>{DIRECTOR_REVIEW_GATE_LABELS[gate]}</strong>
            <p>{DIRECTOR_REVIEW_GATE_HELP[gate]}</p>
            {gates[gate] === 'PENDING' ? <small>Default: PENDING</small> : null}
          </div>
          <div className="fx-drws__pills" role="group" aria-label={DIRECTOR_REVIEW_GATE_LABELS[gate]}>
            {DIRECTOR_REVIEW_DECISIONS.filter((value) => value !== 'PENDING').map((value) => (
              <button
                key={value}
                type="button"
                className={`fx-drws__pill is-${value.toLowerCase()}${gates[gate] === value ? ' is-on' : ''}`}
                aria-pressed={gates[gate] === value}
                disabled={readOnly}
                onClick={() => onChange(gate, value)}
              >
                {value === 'REVIEW_REQUIRED' ? 'REVIEW REQUIRED' : value}
              </button>
            ))}
          </div>
        </div>
            ) : null
          ))}
        </div>
      ))}
    </section>
  );
}

export function ContentFamixaDirectorDecisionPanel({
  decision,
  canPass,
  directorNote,
  failureReason,
  noteMissing,
  viewsComplete,
  technicalStatus,
  gates,
  readOnly,
  onDecision,
  onNote,
  onFailureReason,
}: {
  decision: DirectorReviewDecision;
  canPass: boolean;
  directorNote: string;
  failureReason: string;
  noteMissing: boolean;
  viewsComplete: boolean;
  technicalStatus?: string;
  gates: DirectorReviewGates;
  readOnly?: boolean;
  onDecision: (value: DirectorReviewDecision) => void;
  onNote: (value: string) => void;
  onFailureReason: (value: (typeof DIRECTOR_REVIEW_FAIL_REASONS)[number]) => void;
}) {
  const checks = directorReviewPassChecklist({
    viewsComplete,
    technicalPass: technicalStatus === 'PASS',
    gates: gates || emptyDirectorReviewGates(),
  });
  return (
    <section className="fx-drws__decision">
      <h3>DIRECTOR DECISION</h3>
      <p className="fx-drws__kicker">SUBJECT DECISION</p>
      <div className="fx-drws__pills">
        {DIRECTOR_REVIEW_DECISIONS.map((value) => (
          <button
            key={value}
            type="button"
            className={`fx-drws__pill is-${value.toLowerCase()}${decision === value ? ' is-on' : ''}`}
            aria-pressed={decision === value}
            disabled={readOnly || (value === 'PASS' && !canPass)}
            onClick={() => onDecision(value)}
          >
            {value === 'REVIEW_REQUIRED' ? 'REVIEW REQUIRED' : value}
          </button>
        ))}
      </div>
      {!canPass ? (
        <>
          <p className="fx-drws__blocked">{DIRECTOR_REVIEW_PASS_BLOCKED}</p>
          <ul className="fx-drws__checks">
            {checks.map((row) => (
              <li key={row.id} className={row.ok ? 'is-ok' : 'is-off'}>
                {row.ok ? '✓' : '○'} {row.label}
              </li>
            ))}
          </ul>
        </>
      ) : null}
      {decision === 'FAIL' ? (
        <>
          <p className="fx-drws__blocked">VISUAL FAIL</p>
          <p className="fx-desk__note">Reason required.</p>
          <p className="fx-desk__note">{DIRECTOR_REVIEW_NO_REGEN}</p>
          <p className="fx-desk__note">{DIRECTOR_REVIEW_FAIL_HINT}</p>
          <div className="fx-drws__reasons">
            {DIRECTOR_REVIEW_FAIL_REASONS.map((reason) => (
              <button
                key={reason}
                type="button"
                className={`fx-drws__pill${failureReason === reason ? ' is-on' : ''}`}
                onClick={() => onFailureReason(reason)}
              >
                {reason}
              </button>
            ))}
          </div>
        </>
      ) : null}
      <label>
        Director Note {directorReviewNoteRequired(decision) ? <em>(bắt buộc)</em> : null}
        <textarea
          value={directorNote}
          onChange={(e) => onNote(e.target.value)}
          rows={3}
          placeholder={DIRECTOR_REVIEW_NOTE_PLACEHOLDER}
          disabled={readOnly}
        />
      </label>
      {noteMissing ? <p className="fx-drws__missing">Note bắt buộc với FAIL / REVIEW REQUIRED.</p> : null}
    </section>
  );
}

export function ContentFamixaDirectorCompletionPanel({
  review,
  busy,
  reasons,
  packReady,
  onMark,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  busy: boolean;
  reasons?: string[];
  packReady?: boolean;
  onMark: () => void;
}) {
  const ready = packReady ?? directorReviewMarkPassEnabled(review);
  const blocked = reasons?.length ? reasons : ['Mark Visual Calibration Pass locked until 6/6 subjects PASS.'];
  if (review?.visualPass) {
    return (
      <div className="fx-drws__complete">
        <h2>VISUAL CALIBRATION</h2>
        <p>✓ PASS</p>
        <p>Approved by Director</p>
        <p>6 / 6 SUBJECTS</p>
        <p>24 / 24 IMAGES</p>
        <p>Director Review complete.</p>
        <p>{DIRECTOR_REVIEW_FOUNDATION_NEXT}</p>
        <p>Next workflow: Character Foundation / Lock &amp; Promotion</p>
        <Button disabled>{DIRECTOR_REVIEW_PASSED_LABEL}</Button>
        <Link to="/content/videos">
          <Button>{DIRECTOR_REVIEW_CONTINUE_FOUNDATION}</Button>
        </Link>
      </div>
    );
  }
  if (ready) {
    return (
      <div className="fx-drws__complete">
        <h2>{DIRECTOR_REVIEW_COMPLETE_TITLE}</h2>
        <p>{DIRECTOR_REVIEW_COMPLETE_VI}</p>
        <p>Technical Integrity ✓</p>
        <p>24 Images ✓</p>
        <p>6 Identity Anchors ✓</p>
        <p>18 Conditioned Images ✓</p>
        <p>6/6 Subject Reviews ✓</p>
        <p>All Visual Gates ✓</p>
        <p>READY FOR VISUAL CALIBRATION PASS</p>
        <Button type="primary" size="large" loading={busy} onClick={onMark}>
          ĐÁNH DẤU VISUAL CALIBRATION PASS
        </Button>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_LABEL}</p>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_VI}</p>
        <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_WARNING}</p>
      </div>
    );
  }
  return (
    <div className="fx-drws__locked">
      <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL}</p>
      <p>[DISABLED]</p>
      <p>Reasons:</p>
      <ul>
        {blocked.map((reason) => (
          <li key={reason}>• {reason}</li>
        ))}
      </ul>
      <Button disabled>{DIRECTOR_REVIEW_MARK_PASS_LABEL}</Button>
    </div>
  );
}

export function ContentFamixaDirectorImageStatus({
  loaded,
  expected = 4,
  loadDone,
}: {
  loaded: number;
  expected?: number;
  loadDone?: boolean;
}) {
  const ready = loaded >= expected;
  const blocked = Boolean(loadDone) && !ready;
  return (
    <p className={`fx-drws__techbar${blocked ? ' is-block' : ready ? ' is-ok' : ''}`}>
      {ready
        ? `✓ ${DIRECTOR_REVIEW_IMAGES_READY_LINE}`
        : blocked
          ? `⚠ ${DIRECTOR_REVIEW_IMAGES_BLOCKED_LINE}`
          : 'Loading calibration images…'}
      {blocked ? ' 4 calibration images could not be loaded.' : ''}
    </p>
  );
}

export function ContentFamixaDirectorNextStep({
  decision,
  label,
  approved,
  expected = 6,
  nextLabel,
  note,
  onNext,
  onAgain,
}: {
  decision: string;
  label?: string;
  approved: number;
  expected?: number;
  nextLabel?: string;
  note?: string;
  onNext?: () => void;
  onAgain?: () => void;
}) {
  const name = (label || 'Character').toUpperCase();
  if (decision === 'PASS') {
    return (
      <section className="fx-drws__next is-pass" aria-label="Next character">
        <p>✓ {name} — APPROVED</p>
        <p>{approved} / {expected} characters approved</p>
        {nextLabel ? (
          <Button type="primary" size="large" onClick={onNext}>
            Review next character →
          </Button>
        ) : (
          <p>{DIRECTOR_REVIEW_READY_TITLE}</p>
        )}
      </section>
    );
  }
  if (decision === 'FAIL') {
    return (
      <section className="fx-drws__next is-fail" aria-label="Failed character">
        <p>✕ {name} — FAILED</p>
        {note ? <p>Reason: {note}</p> : null}
        <div className="fx-drws__next-actions">
          <Button onClick={onAgain}>Review again</Button>
          {nextLabel ? <Button type="primary" onClick={onNext}>Go to next character</Button> : null}
        </div>
      </section>
    );
  }
  return null;
}

export function ContentFamixaDirectorHistoricalPanel({
  count,
}: {
  count: number;
}) {
  if (!count) return null;
  return (
    <Collapse
      className="fx-drws__historical"
      items={[{
        key: 'hist',
        label: `Historical Artifacts · ${count} · ${DIRECTOR_REVIEW_HISTORICAL_LABEL}`,
        children: (
          <p>{count} historical independent pixels. Không đưa vào current review grid.</p>
        ),
      }]}
    />
  );
}

export function ContentFamixaDirectorTechnicalDetails({
  review,
  subject,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  subject?: IdentityConditionedCalibrationSubjectReview;
}) {
  const reviewed = directorReviewSubjectsReviewed(review?.subjects);
  return (
    <Collapse
      ghost
      className="fx-director-tech"
      items={[{
        key: 'tech',
        label: 'Technical details ▾',
        children: (
          <div>
            <p>Pack {review?.packId || '—'}</p>
            <p>CalibrationRunId {review?.calibrationRunId || '—'}</p>
            <p>Subject {subject?.subjectId || '—'}</p>
            <p>Technical {subject?.technicalStatus || '—'}</p>
            <p>{DIRECTOR_REVIEW_ADVANCED_ENGINEERING}</p>
            {directorReviewTechnicalBlockReasons(review).map((reason) => (
              <p key={reason}>{reason}</p>
            ))}
            <p>Reviewed {reviewed} / {review?.subjectExpected ?? 6}</p>
            <p>Gemini called {review?.geminiCalled ? 'TRUE' : 'FALSE'}</p>
            <p>Generation executed {review?.generationExecuted ? 'TRUE' : 'FALSE'}</p>
            <p>Provider called {review?.providerCalled ? 'TRUE' : 'FALSE'}</p>
            <p>HistoricalArtifacts {review?.historical?.length ?? 0}</p>
            {subject?.views.map((view) => (
              <p key={view.view}>
                {view.view} SHA {view.artifactSha256 || '—'} · {view.artifactPath || '—'}
              </p>
            ))}
          </div>
        ),
      }]}
    />
  );
}
