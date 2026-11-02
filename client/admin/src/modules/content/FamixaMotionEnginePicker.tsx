import { useState } from 'react';
import { Button, Select, Space, Typography } from 'antd';
import {
  FAMIXA_MOTION_ENGINE_OPTIONS,
  type FamixaMotionEngine,
} from './famixa-motion-engine';
import {
  DIRECTOR_VIDEO_MODEL_UNRESOLVED,
  formatDirectorVideoModelLabel,
  formatVideoTakeProvenanceLine,
  motionEngineFromProviderModel,
  type DirectorVideoTakeProvenance,
} from './famixa-director-video-model-display';
import { FamixaProductionCredentialStatus } from './FamixaProductionCredentialStatus';

export function FamixaMotionEnginePicker({
  engine: _engine,
  onEngine,
  keys: _keys,
  selectionProviderId,
  selectionModelId,
  takeProvenance,
  busy,
}: {
  engine?: FamixaMotionEngine | null;
  onEngine: (next: FamixaMotionEngine) => void;
  keys?: { runway?: boolean; fal?: boolean; veo?: boolean };
  selectionProviderId?: string | null;
  selectionModelId?: string | null;
  takeProvenance?: DirectorVideoTakeProvenance;
  busy?: boolean;
}) {
  void _engine;
  void _keys;
  const [editing, setEditing] = useState(false);
  const osEngine = motionEngineFromProviderModel(selectionProviderId, selectionModelId);
  const currentLabel = formatDirectorVideoModelLabel(selectionProviderId, selectionModelId);
  const takeLine = formatVideoTakeProvenanceLine(takeProvenance);

  return (
    <div data-motion-engine="1" data-video-model-display="1">
      <FamixaProductionCredentialStatus />
      {takeProvenance?.present ? (
        <div data-video-take-provenance="1" style={{ marginBottom: 8 }}>
          <Typography.Text>Video</Typography.Text>
          <div>✓ Đã tạo</div>
          {takeLine ? <Typography.Text type="secondary">{takeLine}</Typography.Text> : null}
        </div>
      ) : null}
      <Space style={{ marginBottom: 4 }} wrap>
        <Typography.Text type="secondary">Model video</Typography.Text>
        <Typography.Text data-current-video-model="1">{currentLabel}</Typography.Text>
        <Button type="link" size="small" disabled={busy} onClick={() => setEditing((v) => !v)}>
          Thay đổi
        </Button>
      </Space>
      {editing ? (
        <Select
          value={osEngine ?? undefined}
          placeholder={DIRECTOR_VIDEO_MODEL_UNRESOLVED}
          disabled={busy}
          loading={busy}
          onChange={(next) => {
            onEngine(next);
            setEditing(false);
          }}
          style={{ width: '100%', maxWidth: 360 }}
          options={[...FAMIXA_MOTION_ENGINE_OPTIONS]}
        />
      ) : null}
    </div>
  );
}
