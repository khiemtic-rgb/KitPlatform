import { useEffect, useState } from 'react';
import { InputNumber, Modal, message } from 'antd';
import { PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import {
  moneyInputNumberPropsAllowZero,
  moneyInputNumberStyle,
} from '@/shared/utils/money';

type Props = {
  open: boolean;
  loading?: boolean;
  warehouseName?: string;
  onCancel: () => void;
  onConfirm: (openingCash: number) => void | Promise<void>;
};

export function OpenShiftModal({ open, loading, warehouseName, onCancel, onConfirm }: Props) {
  const [openingCash, setOpeningCash] = useState(0);

  useEffect(() => {
    if (open) setOpeningCash(0);
  }, [open]);

  const handleOk = () =>
    (async () => {
      const cash = Number(openingCash ?? 0);
      if (Number.isNaN(cash) || cash < 0) {
        message.warning('Quỹ đầu ca không hợp lệ');
        throw new Error('invalid opening cash');
      }
      await onConfirm(cash);
    })();

  return (
    <Modal
      title="Mở ca làm việc"
      open={open}
      confirmLoading={loading}
      okText="Mở ca"
      destroyOnClose
      maskClosable={false}
      onCancel={onCancel}
      onOk={handleOk}
    >
      {warehouseName && (
        <div style={{ marginBottom: 12 }}>
          <PosSummaryRow label="Kho" value={warehouseName} />
        </div>
      )}
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block', marginBottom: 6, fontWeight: 500 }}>
          Quỹ đầu ca (tiền mặt)
        </label>
        <InputNumber
          {...moneyInputNumberPropsAllowZero}
          style={{ ...moneyInputNumberStyle, width: '100%' }}
          value={openingCash}
          onChange={(value) => setOpeningCash(Number(value ?? 0))}
          placeholder="0"
        />
      </div>
      <div style={{ color: '#666', fontSize: 12 }}>
        Quỹ đầu ca là số tiền mặt có sẵn trong két trước khi bán. Hệ thống sẽ yêu cầu mở ca trước khi
        thanh toán đơn.
      </div>
    </Modal>
  );
}
