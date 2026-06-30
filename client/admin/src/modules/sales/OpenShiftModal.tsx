import { useEffect, useState } from 'react';
import { InputNumber, Modal, message } from 'antd';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('sales');
  const { t: tc } = useTranslation('common');
  const [openingCash, setOpeningCash] = useState(0);

  useEffect(() => {
    if (open) setOpeningCash(0);
  }, [open]);

  const handleOk = () =>
    (async () => {
      const cash = Number(openingCash ?? 0);
      if (Number.isNaN(cash) || cash < 0) {
        message.warning(t('pos.shift.invalidOpeningCash'));
        throw new Error('invalid opening cash');
      }
      await onConfirm(cash);
    })();

  return (
    <Modal
      title={t('pos.shift.openTitle')}
      open={open}
      confirmLoading={loading}
      okText={t('pos.shift.openConfirm')}
      cancelText={tc('actions.cancel')}
      destroyOnClose
      maskClosable={false}
      onCancel={onCancel}
      onOk={handleOk}
    >
      {warehouseName && (
        <div style={{ marginBottom: 12 }}>
          <PosSummaryRow label={t('pos.shift.warehouse')} value={warehouseName} />
        </div>
      )}
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block', marginBottom: 6, fontWeight: 500 }}>
          {t('pos.shift.openingCashLabel')}
        </label>
        <InputNumber
          {...moneyInputNumberPropsAllowZero}
          style={{ ...moneyInputNumberStyle, width: '100%' }}
          value={openingCash}
          onChange={(value) => setOpeningCash(Number(value ?? 0))}
          placeholder="0"
        />
      </div>
      <div style={{ color: '#666', fontSize: 12 }}>{t('pos.shift.openingCashHint')}</div>
    </Modal>
  );
}
