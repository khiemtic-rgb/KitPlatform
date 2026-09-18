import { Descriptions, Drawer, Spin, Tag, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import dayjs from 'dayjs';
import type { SalesShiftDetail, SalesShiftSummary } from '@/shared/api/sales.types';
import { SALES_SHIFT_STATUSES } from '@/shared/api/sales.types';
import { ShiftSummaryPanel } from '@/modules/sales/shift-summary-ui';
import { formatDisplayMoney } from '@/shared/utils/money';

type SheetProps = {
  shift: SalesShiftDetail;
  loading?: boolean;
};

export function ShiftCloseSheet({ shift, loading }: SheetProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftReport' });
  const closed = shift.status === SALES_SHIFT_STATUSES.Closed;

  return (
    <Spin spinning={loading}>
      <Descriptions size="small" bordered column={2} style={{ marginBottom: 16 }}>
        <Descriptions.Item label={t('currentShift.descriptions.shiftNumber')}>
          {shift.shiftNumber}
        </Descriptions.Item>
        <Descriptions.Item label={t('currentShift.descriptions.warehouse')}>
          {shift.warehouseName}
        </Descriptions.Item>
        <Descriptions.Item label={t('currentShift.descriptions.openedBy')}>
          {shift.openedByUserName}
        </Descriptions.Item>
        <Descriptions.Item label={t('currentShift.descriptions.openedAt')}>
          {dayjs(shift.openedAt).format('DD-MM-YYYY HH:mm')}
        </Descriptions.Item>
        <Descriptions.Item label={t('currentShift.descriptions.openingCash')}>
          {formatDisplayMoney(shift.openingCash)}
        </Descriptions.Item>
        <Descriptions.Item label={t('currentShift.descriptions.status')}>
          <Tag color={closed ? 'default' : 'processing'}>
            {closed ? t('currentShift.statusClosed') : t('currentShift.statusOpen')}
          </Tag>
        </Descriptions.Item>
        {shift.closedByUserName ? (
          <Descriptions.Item label={t('sheet.closedBy')}>{shift.closedByUserName}</Descriptions.Item>
        ) : null}
        {shift.closedAt ? (
          <Descriptions.Item label={t('sheet.closedAt')}>
            {dayjs(shift.closedAt).format('DD-MM-YYYY HH:mm')}
          </Descriptions.Item>
        ) : null}
        {shift.closingCash != null ? (
          <Descriptions.Item label={t('sheet.closingCash')}>
            {formatDisplayMoney(shift.closingCash)}
          </Descriptions.Item>
        ) : null}
        {shift.closeNotes ? (
          <Descriptions.Item label={t('sheet.closeNotes')} span={2}>
            {shift.closeNotes}
          </Descriptions.Item>
        ) : null}
      </Descriptions>
      <ShiftSummaryPanel summary={shift.summary} showCashReconciliation loading={loading} />
    </Spin>
  );
}

type DrawerProps = {
  open: boolean;
  loading?: boolean;
  shift: SalesShiftDetail | null;
  fallbackSummary?: SalesShiftSummary | null;
  fallbackTitle?: string;
  onClose: () => void;
};

export function ShiftCloseSheetDrawer({
  open,
  loading,
  shift,
  fallbackSummary,
  fallbackTitle,
  onClose,
}: DrawerProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftReport' });
  const title = shift
    ? t('sheet.title', { number: shift.shiftNumber })
    : fallbackTitle || t('sheet.titleFallback');

  return (
    <Drawer
      title={title}
      width={720}
      open={open}
      onClose={onClose}
      destroyOnClose
    >
      {shift ? (
        <ShiftCloseSheet shift={shift} loading={loading} />
      ) : fallbackSummary ? (
        <>
          <Typography.Paragraph type="secondary">{t('sheet.noShiftHint')}</Typography.Paragraph>
          <ShiftSummaryPanel summary={fallbackSummary} loading={loading} />
        </>
      ) : (
        <Spin spinning={loading}>
          <Typography.Paragraph type="secondary">{t('sheet.empty')}</Typography.Paragraph>
        </Spin>
      )}
    </Drawer>
  );
}
