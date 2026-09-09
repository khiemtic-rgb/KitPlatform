import { FAMIXA_VISUAL_MODE, visualModeLabel } from './kit-video-visual-mode';

export function ContentFamixaVisualModeBadge({
  compact,
}: {
  compact?: boolean;
}) {
  const mode = visualModeLabel(FAMIXA_VISUAL_MODE.visualMode);
  if (compact) {
    return (
      <p className="fx-vmode fx-vmode--compact">
        <span>PROJECT VISUAL MODE</span>
        <b>{mode}</b>
      </p>
    );
  }
  return (
    <aside className="fx-vmode">
      <p className="fx-vmode__kicker">PROJECT VISUAL MODE</p>
      <p>
        <span>Project</span>
        <b>{FAMIXA_VISUAL_MODE.projectId}</b>
      </p>
      <p>
        <span>Visual Mode</span>
        <b>● {mode}</b>
      </p>
      <p>
        <span>Visual Universe</span>
        <b>● {FAMIXA_VISUAL_MODE.visualUniverse}</b>
      </p>
      <p>
        <span>Visual Authority</span>
        <b>● PROJECT</b>
      </p>
    </aside>
  );
}
