import { useEffect, useState } from 'react';
import { Checkbox, Modal, Typography } from 'antd';
import type { AdjustmentCountPreviewLine } from '@/shared/api/inventory.types';
import { APPROVE_COUNT_CHECKLIST, countVarianceSummary } from '@/modules/inventory/inventory-count-workflow';
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
  const [checked, setChecked] = useState<boolean[]>(() => APPROVE_COUNT_CHECKLIST.map(() => false));
  const summary = countVarianceSummary(previewLines);
  const allChecked = checked.every(Boolean);

  useEffect(() => {
    if (open) setChecked(APPROVE_COUNT_CHECKLIST.map(() => false));
  }, [open]);

  return (
    <Modal
      title="Bước 4 — Duyệt phiên kiểm kê"
      open={open}
      onCancel={onCancel}
      onOk={onConfirm}
      okText="Duyệt và cập nhật tồn"
      cancelText="Quay lại đối chiếu"
      okButtonProps={{ disabled: !allChecked, loading }}
      width={520}
    >
      <Typography.Paragraph style={{ marginTop: 0 }}>
        Hệ thống sẽ cập nhật tồn theo <strong>{summary.totalLines}</strong> nhóm (SP + lô).
        {summary.varianceLines > 0 ? (
          <>
            {' '}
            Có <strong>{summary.varianceLines}</strong> nhóm lệch
            {summary.surplus > 0 && <> (dư {formatDisplayQuantity(summary.surplus)})</>}
            {summary.shortage > 0 && <> (thiếu {formatDisplayQuantity(summary.shortage)})</>}.
          </>
        ) : (
          ' Tất cả nhóm khớp tồn hệ thống.'
        )}
      </Typography.Paragraph>

      {previewLines.slice(0, 5).map((line) => (
        <div key={`${line.productId}-${line.batchId}`} style={{ fontSize: 13, marginBottom: 4 }}>
          · {line.productName}
          {line.batchNumber ? ` · lô ${line.batchNumber}` : ''}: lệch{' '}
          <strong>{formatDisplayQuantity(line.differenceQuantity)}</strong>
        </div>
      ))}
      {previewLines.length > 5 && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          … và {previewLines.length - 5} nhóm khác
        </Typography.Text>
      )}

      <Typography.Paragraph strong style={{ marginTop: 16, marginBottom: 8 }}>
        Xác nhận trước khi duyệt:
      </Typography.Paragraph>
      {APPROVE_COUNT_CHECKLIST.map((label, index) => (
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
