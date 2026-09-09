import { Button } from 'antd';
import { directorRecoveryOf } from './ShotProductionDirector';
import { staffOrchMessage } from './ShotProductionErrors';
import { nextShotProductionCommand, staffCommandCta } from './ShotProductionOrchestrator';
import { ShotProductionProgress } from './ShotProductionProgress';
import { ShotProductionQa } from './ShotProductionQa';
import type { ShotProductionSnapshot } from './ShotProductionState';
import type { SeriesShotRun } from '../content-famixa-series';
import {
  directorPrimaryCta,
  shotQaMissing,
  shotStaleNotes,
  type StaffShotAction,
} from './ShotProductionCta';

export type { StaffShotAction } from './ShotProductionCta';

export function ShotProductionCard({
  label,
  title,
  snap,
  shotQa,
  busy,
  tone = 'staff',
  showIdentity = true,
  holdReason,
  holdHint,
  onAction,
  onRetry,
  onQa,
}: {
  label: string;
  title: string;
  snap: ShotProductionSnapshot;
  shotQa?: SeriesShotRun['shotQa'];
  busy?: boolean;
  tone?: 'director' | 'staff';
  showIdentity?: boolean;
  holdReason?: string;
  holdHint?: string;
  onAction: (action: StaffShotAction) => void;
  onRetry?: (stage: 'voice' | 'picture' | 'motion' | 'lipsync' | 'mix') => void;
  onQa?: (next: NonNullable<SeriesShotRun['shotQa']>) => void;
}) {
  const cmd = nextShotProductionCommand(snap);
  const cta = tone === 'director' ? directorPrimaryCta(snap) : staffCommandCta(cmd);
  const recovery = tone === 'director' ? directorRecoveryOf(snap) : undefined;
  const waiting = cmd.kind === 'WAIT' || snap.motionBusy || snap.lipsyncBusy || snap.mixBusy || Boolean(busy);
  const showQa =
    tone === 'director'
      ? cmd.type === 'LIPSYNC_QA_REQUIRED'
      : !snap.isSilent && (snap.motionReady || snap.motionUsable || cmd.type === 'LIPSYNC_QA_REQUIRED' || cmd.type === 'CONFIRM_LIPSYNC');
  const stale = tone === 'director' ? [] : shotStaleNotes(snap);
  const afterLipsync = snap.lipSyncReady;
  const afterMix = snap.mixReady;
  const qaMissing = shotQaMissing(shotQa, !snap.isSilent, { afterLipsync, afterMix });
  const directorMessage = recovery?.message || undefined;
  const primaryCommand = tone === 'director' ? recovery?.nextCommand ?? cmd.type : cmd.type;
  const extraActions = (recovery?.actions ?? []).filter((row) => row.command !== primaryCommand);
  return (
    <article className="fx-media-card">
      {showIdentity ? (
        <>
          <h3>{label}</h3>
          <p>{title || snap.intent.action || 'Cảnh đang sản xuất'}</p>
        </>
      ) : null}
      <ShotProductionProgress snap={snap} />
      {tone === 'staff' && snap.voiceDurationSec > 0 ? (
        <p className="fx-desk__note">Thời lượng thoại: {snap.voiceDurationSec.toFixed(1)}s</p>
      ) : null}
      {stale.map((note) => (
        <p key={note} className="fx-desk__note">{note}</p>
      ))}
      {holdReason ? (
        <p className="fx-desk__note">
          {holdReason}
          {holdHint ? ` ${holdHint}` : ''}
        </p>
      ) : null}
      {cmd.kind === 'BLOCK' && tone === 'staff' ? (
        <p className="fx-desk__note">{staffOrchMessage(cmd.code || '')}</p>
      ) : null}
      {cta.hint && cmd.kind !== 'BLOCK' && tone === 'staff' ? <p className="fx-desk__note">{staffOrchMessage(cta.hint)}</p> : null}
      {tone === 'director' && snap.pictureUnusable ? (
        <p className="fx-desk__note">Ảnh này thuộc pipeline cũ. Tạo hình mới — không duyệt ảnh này.</p>
      ) : null}
      {tone === 'director' && directorMessage ? (
        directorMessage.split('\n').map((line) => (
          <p key={line} className="fx-desk__note">{line}</p>
        ))
      ) : null}
      {showQa && onQa ? (
        <>
          {tone === 'director' && cmd.type === 'LIPSYNC_QA_REQUIRED' ? (
            <p>Kiểm tra video trước khi tiếp tục</p>
          ) : null}
          <ShotProductionQa
            shotQa={shotQa}
            spoken={!snap.isSilent}
            tone={tone}
            afterLipsync={afterLipsync}
            afterMix={afterMix}
            onChange={onQa}
          />
          {cmd.type === 'LIPSYNC_QA_REQUIRED' && qaMissing.length ? (
            <p className="fx-desk__note">Còn chưa đánh dấu: {qaMissing.join(', ')}.</p>
          ) : null}
        </>
      ) : null}
      <div className="fx-desk__btns">
        <Button
          type="primary"
          loading={waiting}
          disabled={cta.action === 'blocked' || waiting}
          onClick={() => onAction(cta.action)}
        >
          {waiting && cmd.kind === 'WAIT' ? cta.label : cta.label}
        </Button>
        {extraActions.map((row) => (
          <Button
            key={row.type}
            onClick={() => {
              if (row.type === 'ACCEPT_EXISTING') onAction('accept-existing');
              else if (row.type === 'EDIT_INPUT') onAction('edit-input');
              else if (row.type === 'ENSURE_PICTURE') onAction('picture');
              else if (row.type === 'RETRY_MOTION' && snap.retryLocked) onAction('motion');
              else if (row.type === 'RETRY_MOTION' || row.type === 'CONFIRM_MOTION') onAction('motion');
              else if (row.type === 'ENSURE_VOICE') onAction('produce');
              else if (row.type === 'QA') onAction('qa');
              else if (row.type === 'LIPSYNC') onAction('lipsync');
            }}
          >
            {row.label}
          </Button>
        ))}
        {onRetry && snap.stage === 'VOICE_REQUIRED' ? <Button onClick={() => onRetry('voice')}>Thử lại thoại</Button> : null}
        {onRetry && (snap.stage === 'NEEDS_VISUAL_REVIEW' || snap.keyframeStale) ? (
          <Button onClick={() => onRetry('picture')}>Thử lại hình</Button>
        ) : null}
        {onRetry && tone === 'staff' && (snap.stage === 'VIDEO_ERROR' || (snap.stage === 'VIDEO_CONFIRM' && snap.motionFailed)) && !snap.retryLocked ? (
          <Button onClick={() => onRetry('motion')}>Thử lại chuyển động</Button>
        ) : null}
        {onRetry && snap.stage === 'LIPSYNC_ERROR' ? <Button onClick={() => onRetry('lipsync')}>Thử lại Lip-sync</Button> : null}
        {onRetry && snap.stage === 'MIX_ERROR' ? <Button onClick={() => onRetry('mix')}>Thử lại hoàn thiện</Button> : null}
      </div>
    </article>
  );
}
