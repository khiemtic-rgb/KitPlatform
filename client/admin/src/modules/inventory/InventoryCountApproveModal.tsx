import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Checkbox, Modal, Typography } from 'antd';
import type { AdjustmentCountPreviewLine } from '@/shared/api/inventory.types';
import { countVarianceSummary, getApproveCountChecklist } from '@/modules/inventory/inventory-count-workflow';
import { formatDisplayQuantity } from '@/shared/utils/money';

interface InventoryCountApproveModalProps {
  open: boolean;
  loading: boolean;
  previewLines: AdjustmentCountPreviewLine[];
  onCancel: () => void;
  onConfirm: () => void;
}

export function InventoryCountApproveModal({
  open,
  loading,
  previewLines,
  onCancel,
  onConfirm,
}: InventoryCountApproveModalProps) {
  const { t, i18n } = useTranslation('inventory', { keyPrefix: 'inventoryCountApproveModal' });
  const checklist = useMemo(() => getApproveCountChecklist(), [i18n.language]);
  const [checked, setChecked] = useState<boolean[]>(() => checklist.map(() => false));
  const summary = countVarianceSummary(previewLines);
  const allChecked = checked.every(Boolean);

  useEffect(() => {
    if (open) setChecked(checklist.map(() => false));
  }, [open, checklist]);

  return (
    <Modal
      title={t('title')}
      open={open}
      onCancel={onCancel}
      onOk={onConfirm}
      okText={t('okText')}
      cancelText={t('cancelText')}
      okButtonProps={{ disabled: !allChecked, loading }}
      width={520}
    >
      <Typography.Paragraph style={{ marginTop: 0 }}>
        {t('summaryIntro', { totalLines: summary.totalLines })}
        {summary.varianceLines > 0 ? (
          <>
            {' '}
            {t('varianceIntro', { varianceLines: summary.varianceLines })}
            {summary.surplus > 0 && <> {t('surplus', { qty: formatDisplayQuantity(summary.surplus) })}</>}
            {summary.shortage > 0 && <> {t('shortage', { qty: formatDisplayQuantity(summary.shortage) })}</>}.
          </>
        ) : (
          t('allMatch')
        )}
      </Typography.Paragraph>

      {previewLines.slice(0, 5).map((line) => (
        <div key={`${line.productId}-${line.batchId}`} style={{ fontSize: 13, marginBottom: 4 }}>
          {t('lineVariance', {
            product: line.productName,
            batch: line.batchNumber ? t('batchSuffix', { number: line.batchNumber }) : '',
            variance: formatDisplayQuantity(line.differenceQuantity),
          })}
        </div>
      ))}
      {previewLines.length > 5 && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('moreGroups', { count: previewLines.length - 5 })}
        </Typography.Text>
      )}

      <Typography.Paragraph strong style={{ marginTop: 16, marginBottom: 8 }}>
        {t('confirmTitle')}
      </Typography.Paragraph>
      {checklist.map((label, index) => (
        <div key={label} style={{ marginBottom: 8 }}>
          <Checkbox
            checked={checked[index]}
            onChange={(e) =>
              setChecked((prev) => {
                const next = [...prev];
                next[index] = e.target.checked;
                return next;
              })
            }
          >
            {label}
          </Checkbox>
        </div>
      ))}
    </Modal>
  );
}
