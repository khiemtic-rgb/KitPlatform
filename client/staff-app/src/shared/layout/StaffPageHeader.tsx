import { Button, Typography } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

type Props = {
  title: string;
  subtitle?: string;
  backTo?: string;
  onBack?: () => void;
  right?: React.ReactNode;
};

export function StaffPageHeader({ title, subtitle, backTo = '/', onBack, right }: Props) {
  const navigate = useNavigate();

  return (
    <header className="staff-header staff-page-header">
      <button
        type="button"
        className="staff-page-header__back"
        aria-label="Quay lại"
        onClick={() => (onBack ? onBack() : navigate(backTo))}
      >
        <ArrowLeftOutlined />
      </button>
      <div className="staff-page-header__titles">
        <Typography.Text className="staff-page-header__title">{title}</Typography.Text>
        {subtitle ? (
          <Typography.Text type="secondary" className="staff-page-header__subtitle">
            {subtitle}
          </Typography.Text>
        ) : null}
      </div>
      <div className="staff-page-header__right">{right ?? <span className="staff-page-header__right-spacer" />}</div>
    </header>
  );
}
