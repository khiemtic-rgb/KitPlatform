import { Steps, Typography } from 'antd';
import {
  INVENTORY_COUNT_WORKFLOW_STEPS,
  resolveCountWorkflowStep,
} from '@/modules/inventory/inventory-count-workflow';

interface InventoryCountWorkflowStepsProps {
  status: number;
  entryCount: number;
  canApprove: boolean;
  compact?: boolean;
}

export function InventoryCountWorkflowSteps({
  status,
  entryCount,
  canApprove,
  compact,
}: InventoryCountWorkflowStepsProps) {
  const current = resolveCountWorkflowStep({ status, entryCount, canApprove });

  return (
    <div>
      <Steps
        size="small"
        direction={compact ? 'vertical' : 'horizontal'}
        current={current}
        items={INVENTORY_COUNT_WORKFLOW_STEPS.map((step) => ({
          title: step.title,
          description: compact ? undefined : step.description,
        }))}
      />
      {compact && INVENTORY_COUNT_WORKFLOW_STEPS[current] && (
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
          {INVENTORY_COUNT_WORKFLOW_STEPS[current].description}
        </Typography.Paragraph>
      )}
    </div>
  );
}
