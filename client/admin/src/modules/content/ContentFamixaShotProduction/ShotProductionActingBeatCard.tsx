import { useEffect, useRef, useState } from 'react';
import { Button, Input, Select } from 'antd';
import type { FamixaSeriesShot } from '../content-famixa-series';
import { computeShotTiming, deriveActingBeat, type ShotActingBeat } from '../famixa-shot-production-timing';
import {
  ACTING_ACTION_CHIPS,
  ACTING_BEAT_APPLY_VISUAL,
  ACTING_BEAT_INVALID_CONTEXT,
  ACTING_BEAT_SAVE_LABEL,
  ACTING_BEAT_SAVED_LABEL,
  ACTING_BEAT_UNSAVED_LABEL,
  ACTING_BEAT_UNSAVED_LEAVE,
  ACTING_BODY_CHIPS,
  ACTING_EMOTION_VI,
  ACTING_EMOTIONS,
  ACTING_GAZE_CHIPS,
  ACTING_PAPER_PROP_EN,
  ACTING_PAPER_PROP_VI,
  ACTING_PHONE_PROP_EN,
  ACTING_PHONE_PROP_VI,
  ACTING_ROOM_CHIPS,
  INVALID_CONTEXTUAL_BEAT,
  actingBeatContextStatus,
  actingBeatDirty,
  actingChipsForShot,
  actingEmotionOf,
  actingRoomStillForSpeaker,
  derivedActingProp,
  patchActingBeat,
  type ActingBeatEditorHandle,
  type ActingCatalogChip,
} from './ShotProductionActingBeat';

function applyChipValue(chip: ActingCatalogChip, who?: string) {
  if (chip.id === 'sitstill') return actingRoomStillForSpeaker(who);
  return chip.value;
}

export function ShotProductionActingBeatCard({
  shot,
  spoken,
  who = 'nhân vật',
  speechText,
  onChange,
  onReady,
}: {
  shot: FamixaSeriesShot;
  spoken: boolean;
  who?: string;
  speechText?: string;
  onChange: (next: ReturnType<typeof patchActingBeat>) => void;
  onReady?: (handle: ActingBeatEditorHandle) => void;
}) {
  const [draftShotId, setDraftShotId] = useState(shot.id);
  const [draft, setDraft] = useState<ShotActingBeat | undefined>(shot.actingBeat);
  if (shot.id !== draftShotId) {
    setDraftShotId(shot.id);
    setDraft(shot.actingBeat);
  }
  const beat = shot.id === draftShotId ? draft : shot.actingBeat;
  const dirty = actingBeatDirty(shot.actingBeat, beat);
  const emotion = actingEmotionOf({ ...shot, actingBeat: beat });
  const prop = derivedActingProp({ ...shot, actingBeat: beat });
  const showPaper = prop === ACTING_PAPER_PROP_EN;
  const showPhone = prop === ACTING_PHONE_PROP_EN;
  const contextStatus = actingBeatContextStatus(shot, beat);
  const actionChips = actingChipsForShot(shot, ACTING_ACTION_CHIPS);
  const bodyChips = actingChipsForShot(shot, ACTING_BODY_CHIPS);
  const gazeChips = actingChipsForShot(shot, ACTING_GAZE_CHIPS);
  const roomChips = actingChipsForShot(shot, ACTING_ROOM_CHIPS);
  const draftRef = useRef(beat);
  const dirtyRef = useRef(dirty);
  const onChangeRef = useRef(onChange);
  draftRef.current = beat;
  dirtyRef.current = dirty;
  onChangeRef.current = onChange;

  const apply = (next: ShotActingBeat) => {
    setDraft(next);
  };

  const save = () => {
    const next = draftRef.current;
    if (!next || !dirtyRef.current) return;
    onChangeRef.current(next);
  };

  useEffect(() => {
    onReady?.({
      dirty: () => dirtyRef.current,
      save,
    });
  }, [onReady]);

  useEffect(() => {
    if (!dirty) return;
    const onLeave = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = ACTING_BEAT_UNSAVED_LEAVE;
    };
    window.addEventListener('beforeunload', onLeave);
    return () => window.removeEventListener('beforeunload', onLeave);
  }, [dirty]);

  return (
    <div className="fx-media-card" data-acting-beat="director">
      <h3 style={{ margin: '0 0 4px' }}>Diễn xuất</h3>
      <p className="fx-desk__note" style={{ margin: '0 0 12px' }}>
        {who} sẽ diễn thế nào?
      </p>
      {contextStatus === INVALID_CONTEXTUAL_BEAT ? (
        <div className="fx-desk__note" data-acting-beat-context={INVALID_CONTEXTUAL_BEAT} style={{ margin: '0 0 12px' }}>
          <p style={{ margin: '0 0 8px' }}>{ACTING_BEAT_INVALID_CONTEXT}</p>
          <Button
            size="small"
            onClick={() =>
              apply(
                deriveActingBeat(
                  shot,
                  shot.timing ?? computeShotTiming({ voiceDurations: spoken ? [1] : [] }),
                  { emotion: beat?.during.emotion || 'neutral' },
                ),
              )
            }
          >
            {ACTING_BEAT_APPLY_VISUAL}
          </Button>
        </div>
      ) : null}
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Cảm xúc</p>
      <Select
        size="small"
        style={{ width: '100%', maxWidth: 280, marginBottom: 12 }}
        placeholder="Cảm xúc"
        value={emotion}
        options={ACTING_EMOTIONS.map((e) => ({ value: e, label: ACTING_EMOTION_VI[e] }))}
        onChange={(next) => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { emotion: next }))}
      />
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Trước khi nói</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {actionChips.map((chip) => (
          <Button
            key={chip.id}
            size="small"
            type={beat?.before.action === chip.value ? 'primary' : 'default'}
            onClick={() => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { action: chip.value }))}
          >
            {chip.label}
          </Button>
        ))}
      </div>
      <Input
        size="small"
        style={{ marginBottom: 12 }}
        placeholder="Tự viết hành động"
        value={beat?.before.action ?? ''}
        onChange={(e) => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { action: e.target.value }))}
      />
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Hành động cơ thể</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {bodyChips.map((chip) => (
          <Button
            key={chip.id}
            size="small"
            type={beat?.before.body === chip.value ? 'primary' : 'default'}
            onClick={() => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { body: chip.value }))}
          >
            {chip.label}
          </Button>
        ))}
      </div>
      <Input
        size="small"
        style={{ marginBottom: 12 }}
        placeholder="Tự viết cử chỉ"
        value={beat?.before.body ?? ''}
        onChange={(e) => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { body: e.target.value }))}
      />
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Ánh mắt</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {gazeChips.map((chip) => (
          <Button
            key={chip.id}
            size="small"
            type={beat?.before.gaze === chip.value ? 'primary' : 'default'}
            onClick={() => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { gaze: chip.value }))}
          >
            {chip.label}
          </Button>
        ))}
      </div>
      <Input
        size="small"
        style={{ marginBottom: 12 }}
        placeholder="Tự viết ánh mắt"
        value={beat?.before.gaze ?? ''}
        onChange={(e) => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { gaze: e.target.value }))}
      />
      {showPaper || showPhone ? (
        <>
          <p style={{ margin: '0 0 4px', fontWeight: 600 }}>Đạo cụ</p>
          <p className="fx-desk__note" style={{ margin: '0 0 12px' }}>
            {showPaper ? ACTING_PAPER_PROP_VI : ACTING_PHONE_PROP_VI}
          </p>
        </>
      ) : null}
      <p style={{ margin: '0 0 6px', fontWeight: 600 }}>Không gian phía sau</p>
      <div className="fx-desk__btns" style={{ margin: '0 0 8px' }}>
        {roomChips.map((chip) => {
          const value = applyChipValue(chip, who);
          return (
            <Button
              key={chip.id}
              size="small"
              type={beat?.before.room === value ? 'primary' : 'default'}
              onClick={() => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { room: value }))}
            >
              {chip.label}
            </Button>
          );
        })}
      </div>
      <Input
        size="small"
        style={{ marginBottom: 12 }}
        placeholder="Tự viết không gian"
        value={beat?.before.room ?? ''}
        onChange={(e) => apply(patchActingBeat({ ...shot, actingBeat: beat }, spoken, { room: e.target.value }))}
      />
      {spoken ? (
        <>
          <p style={{ margin: '0 0 4px', fontWeight: 600 }}>Thoại</p>
          <p className="fx-desk__note" style={{ margin: '0 0 12px' }}>
            {speechText ? `Nói ngắn gọn «${speechText}».` : 'Rồi nói'}
          </p>
        </>
      ) : null}
      <div className="fx-desk__btns" data-acting-beat-save="director">
        <p className="fx-desk__note" style={{ margin: 0, flex: 1 }}>
          {dirty ? ACTING_BEAT_UNSAVED_LABEL : ACTING_BEAT_SAVED_LABEL}
        </p>
        <Button type="primary" size="small" disabled={!dirty} onClick={save}>
          {ACTING_BEAT_SAVE_LABEL}
        </Button>
      </div>
    </div>
  );
}
