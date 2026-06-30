/** Quy trình kiểm kê chuẩn — 4 bước nghiệp vụ. */

import { inventoryT } from '@/shared/i18n';

const WORKFLOW_STEP_KEYS = ['prepare', 'count', 'reconcile', 'approve'] as const;
const COUNT_REASON_PRESET_VALUES = ['periodic', 'ad_hoc', 'incident', 'audit'] as const;
const APPROVE_CHECKLIST_INDICES = [0, 1, 2] as const;

export function getInventoryCountWorkflowSteps() {
  const t = inventoryT();
  return WORKFLOW_STEP_KEYS.map((key) => ({
    title: t(`countWorkflow.steps.${key}.title`),
    description: t(`countWorkflow.steps.${key}.description`),
  }));
}

export function getCountReasonPresets() {
  const t = inventoryT();
  return COUNT_REASON_PRESET_VALUES.map((value) => ({
    value,
    label: t(`countWorkflow.reasonPresets.${value}`),
  }));
}

export function buildCountReason(preset: string, note?: string): string {
  const t = inventoryT();
  const base = t(`countWorkflow.reasonBase.${preset}`, { defaultValue: preset });
  const trimmed = note?.trim();
  return trimmed ? `${base} — ${trimmed}` : base;
}

export function getApproveCountChecklist(): string[] {
  const t = inventoryT();
  return APPROVE_CHECKLIST_INDICES.map((index) => t(`countWorkflow.approveChecklist.${index}`));
}

export function resolveCountWorkflowStep(input: {
  status: number;
  entryCount: number;
  canApprove: boolean;
}): number {
  if (input.status === 3) return 4;
  if (input.status !== 2) return 0;
  if (input.canApprove) return 3;
  if (input.entryCount > 0) return 2;
  return 1;
}

export function countVarianceSummary(
  lines: Array<{ differenceQuantity: number }>,
): { totalLines: number; varianceLines: number; surplus: number; shortage: number } {
  let varianceLines = 0;
  let surplus = 0;
  let shortage = 0;
  for (const line of lines) {
    if (line.differenceQuantity === 0) continue;
    varianceLines += 1;
    if (line.differenceQuantity > 0) surplus += line.differenceQuantity;
    else shortage += Math.abs(line.differenceQuantity);
  }
  return { totalLines: lines.length, varianceLines, surplus, shortage };
}
