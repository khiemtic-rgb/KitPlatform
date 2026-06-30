import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Steps, Typography } from 'antd';
import {
  getInventoryCountWorkflowSteps,
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
  const { i18n } = useTranslation();
  const steps = useMemo(() => getInventoryCountWorkflowSteps(), [i18n.language]);
  const current = resolveCountWorkflowStep({ status, entryCount, canApprove });

  return (
    <div>
      <Steps
        size="small"
        direction={compact ? 'vertical' : 'horizontal'}
        current={current}
        items={steps.map((step) => ({
          title: step.title,
          description: compact ? undefined : step.description,
        }))}
      />
      {compact && steps[current] && (
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
          {steps[current].description}
        </Typography.Paragraph>
      )}
    </div>
  );
}
