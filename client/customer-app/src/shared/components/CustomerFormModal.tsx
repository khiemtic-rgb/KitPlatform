import type { ReactNode } from 'react';
import { Modal } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import './CustomerFormModal.css';

type CustomerFormModalProps = {
  open: boolean;
  onCancel: () => void;
  title: ReactNode;
  subtitle?: ReactNode;
  icon: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  width?: number;
};

export function CustomerFormModal({
  open,
  onCancel,
  title,
  subtitle,
  icon,
  children,
  footer,
  width = 420,
}: CustomerFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      className="cfm-modal"
      open={open}
      onCancel={onCancel}
      footer={null}
      closable={false}
      destroyOnClose
      centered
      width={width}
    >
      <div className="cfm">
        <div className="cfm-header">
          <div className="cfm-header-main">
            <span className="cfm-header-icon">{icon}</span>
            <div>
              <div className="cfm-title">{title}</div>
              {subtitle ? <div className="cfm-sub">{subtitle}</div> : null}
            </div>
          </div>
          <button
            type="button"
            className="cfm-close"
            aria-label={t('common.cancel')}
            onClick={onCancel}
          >
            <CloseOutlined />
          </button>
        </div>
        {children}
        {footer}
      </div>
    </Modal>
  );
}

export function FormModalLabel({
  icon,
  children,
  required,
}: {
  icon?: ReactNode;
  children: ReactNode;
  required?: boolean;
}) {
  return (
    <span className="cfm-label">
      {icon}
      {children}
      {required ? <em>*</em> : null}
    </span>
  );
}

export function FormModalTip({
  icon,
  title,
  subtitle,
  action,
}: {
  icon: ReactNode;
  title: ReactNode;
  subtitle?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="cfm-tip">
      <span className="cfm-tip-icon">{icon}</span>
      <div className="cfm-tip-copy">
        <div className="cfm-tip-title">{title}</div>
        {subtitle ? <div className="cfm-tip-sub">{subtitle}</div> : null}
      </div>
      {action}
    </div>
  );
}

export function FormModalFooter({
  onCancel,
  onOk,
  okText,
  cancelText,
  confirmLoading,
  okDisabled,
}: {
  onCancel: () => void;
  onOk: () => void;
  okText?: string;
  cancelText?: string;
  confirmLoading?: boolean;
  okDisabled?: boolean;
}) {
  const { t } = useTranslation();

  return (
    <div className="cfm-footer">
      <button type="button" className="cfm-btn cfm-btn--ghost" onClick={onCancel} disabled={confirmLoading}>
        {cancelText ?? t('common.cancel')}
      </button>
      <button
        type="button"
        className="cfm-btn cfm-btn--primary"
        onClick={onOk}
        disabled={confirmLoading || okDisabled}
      >
        {okText ?? t('common.save')}
      </button>
    </div>
  );
}
