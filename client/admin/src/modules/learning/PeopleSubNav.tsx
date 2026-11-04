import { Link, useLocation } from 'react-router-dom';
import { Badge, Menu } from 'antd';
import {
  DashboardOutlined,
  MailOutlined,
  CheckSquareOutlined,
  ReadOutlined,
  RiseOutlined,
  StarOutlined,
  TrophyOutlined,
} from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { useCanLearningWrite } from '@/shared/auth/usePermission';
import { fetchLearningMailUnreadCount } from '@/shared/api/learning.api';

/**
 * Thanh điều hướng phân hệ — cùng cấp.
 * - Quản lý: Tổng quan · Đào tạo · Đánh giá tháng · Phát triển nghề · Ghi nhận · Hộp thư
 * - Nhân viên: Trang chính · Học bài · Hồ sơ năng lực · Thư
 */
export function PeopleSubNav() {
  const location = useLocation();
  const canWrite = useCanLearningWrite();
  const [mailUnread, setMailUnread] = useState(0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const n = await fetchLearningMailUnreadCount();
        if (!cancelled) setMailUnread(n);
      } catch {
        if (!cancelled) setMailUnread(0);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [location.pathname]);

  const mailLabel = (
    <Link to="/people/mail">
      <Badge count={mailUnread} size="small" offset={[8, 0]}>
        {canWrite ? 'Hộp thư' : 'Thư'}
      </Badge>
    </Link>
  );

  const navItems = canWrite
    ? [
        {
          key: '/people',
          icon: <DashboardOutlined />,
          label: <Link to="/people">Tổng quan</Link>,
        },
        {
          key: '/people/learn',
          icon: <ReadOutlined />,
          label: <Link to="/people/learn">Đào tạo</Link>,
        },
        {
          key: '/people/tasks',
          icon: <CheckSquareOutlined />,
          label: <Link to="/people/tasks">Việc trong ca</Link>,
        },
        {
          key: '/people/evaluations',
          icon: <StarOutlined />,
          label: <Link to="/people/evaluations">Đánh giá tháng</Link>,
        },
        {
          key: '/people/grow',
          icon: <RiseOutlined />,
          label: <Link to="/people/grow">Phát triển nghề</Link>,
        },
        {
          key: '/people/recognize',
          icon: <TrophyOutlined />,
          label: <Link to="/people/recognize">Ghi nhận</Link>,
        },
        {
          key: '/people/mail',
          icon: <MailOutlined />,
          label: mailLabel,
        },
      ]
    : [
        {
          key: '/people',
          icon: <DashboardOutlined />,
          label: <Link to="/people">Trang chính</Link>,
        },
        {
          key: '/people/learn',
          icon: <ReadOutlined />,
          label: <Link to="/people/learn">Học bài</Link>,
        },
        {
          key: '/people/tasks',
          icon: <CheckSquareOutlined />,
          label: <Link to="/people/tasks">Việc của tôi</Link>,
        },
        {
          key: '/people/recognize',
          icon: <TrophyOutlined />,
          label: <Link to="/people/recognize">Hồ sơ năng lực</Link>,
        },
        {
          key: '/people/mail',
          icon: <MailOutlined />,
          label: mailLabel,
        },
      ];

  const path = location.pathname;
  let selected = '/people';
  if (path.startsWith('/people/learn') || path.startsWith('/people/enrollments')) {
    selected = '/people/learn';
  } else if (path.startsWith('/people/tasks')) selected = '/people/tasks';
  else if (path.startsWith('/people/evaluations')) selected = '/people/evaluations';
  else if (path.startsWith('/people/grow')) selected = '/people/grow';
  else if (path.startsWith('/people/recognize')) selected = '/people/recognize';
  else if (path.startsWith('/people/mail')) selected = '/people/mail';
  else if (path === '/people' || path === '/people/' || path.startsWith('/people/dashboard')) {
    selected = '/people';
  }

  return (
    <Menu
      mode="horizontal"
      selectedKeys={[selected]}
      items={navItems}
      style={{
        marginBottom: 20,
        borderInline: 'none',
        background: 'transparent',
        borderBottom: '1px solid #f0f0f0',
        lineHeight: '46px',
      }}
    />
  );
}
