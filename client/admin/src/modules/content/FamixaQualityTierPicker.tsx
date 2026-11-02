import { Select, Typography } from 'antd';
import {
  FAMIXA_QUALITY_TIERS,
  qualityCapabilityLabel,
  qualityTierOf,
  readQualityTier,
  writeQualityTier,
  type FamixaQualityCapability,
} from './famixa-quality-tier';

export function FamixaQualityTierPicker({
  shotId,
  capability,
  value,
  onChange,
}: {
  shotId: string;
  capability: FamixaQualityCapability;
  value?: string;
  onChange?: (tierId: string) => void;
}) {
  const selected = value || readQualityTier(shotId, capability);
  const tier = qualityTierOf(capability, selected);
  return (
    <label data-quality-tier={capability} style={{ display: 'block', marginTop: 8 }}>
      <Typography.Text>
        {qualityCapabilityLabel(capability)} · {tier.label}
      </Typography.Text>
      <Select
        size="small"
        value={selected}
        style={{ display: 'block', width: '100%', maxWidth: 360, marginTop: 4 }}
        onChange={(next: string) => {
          writeQualityTier(shotId, capability, next);
          onChange?.(next);
        }}
        options={FAMIXA_QUALITY_TIERS[capability].map((row) => ({
          value: row.id,
          label: `${row.label} · ${row.detail}`,
          disabled: !row.live,
        }))}
      />
    </label>
  );
}
