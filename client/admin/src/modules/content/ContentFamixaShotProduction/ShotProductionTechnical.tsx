import type { SeriesShotRun } from '../content-famixa-series';
import type { ShotProductionSnapshot } from './ShotProductionState';
import { deriveShotExecutionState, executionDiagnosticLines } from './ShotProductionExecution';
import { nextShotProductionCommand } from './ShotProductionOrchestrator';

export function ShotProductionTechnical({
  snap,
  run,
  state,
  shot,
  ttsFiles,
}: {
  snap: ShotProductionSnapshot;
  run?: SeriesShotRun;
  state?: Parameters<typeof deriveShotExecutionState>[0]['state'];
  shot?: Parameters<typeof deriveShotExecutionState>[0]['shot'];
  ttsFiles?: Record<string, { url: string; fileName: string }>;
}) {
  const cmd = nextShotProductionCommand(snap);
  const exec =
    snap.execution ||
    (state && shot ? deriveShotExecutionState({ state, shot, ttsFiles, nextAction: cmd }) : undefined);
  const voiceId = snap.input.cues.map((c) => c.voiceId).filter(Boolean).join(', ') || '—';
  const lines = exec ? executionDiagnosticLines({ ...exec, nextAction: cmd }) : [];
  return (
    <details className="fx-desk__note">
      <summary>Chi tiết kỹ thuật</summary>
      <ul className="fx-finish__check">
        {lines.map((line) => (
          <li key={line}>{line}</li>
        ))}
        <li>command {cmd.type}</li>
        <li>stage {snap.stage} (compat only)</li>
        <li>nextAction {snap.nextAction}</li>
        <li>provider Runway / Fal / ElevenLabs</li>
        <li>model gen4_turbo / eleven_v3 / sync-lipsync</li>
        <li>voiceId {voiceId}</li>
        <li>kf {run?.keyframeFileName || run?.keyframePath || '—'}</li>
        <li>previewUrl {run?.previewUrl || '—'} (display only)</li>
        <li>take {run?.takeUrl || '—'}</li>
        <li>lipsync {run?.lipsyncUrl || '—'}</li>
        <li>voiceFp {snap.stamp?.voiceFp || '—'}</li>
        <li>kfFp {snap.stamp?.kfFp || '—'}</li>
        <li>motionFp {snap.stamp?.motionFp || '—'}</li>
        <li>lipsyncFp {snap.stamp?.lipsyncFp || '—'}</li>
        <li>assembleFp {snap.stamp?.assembleFp || '—'}</li>
        <li>retry {cmd.retryStage || '—'}</li>
        <li>turboError {run?.turboError || '—'}</li>
        <li>lipsyncError {run?.lipsyncError || '—'}</li>
      </ul>
    </details>
  );
}
