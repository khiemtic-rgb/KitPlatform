import type { ReactNode } from 'react';
import { Typography } from 'antd';

/**
 * Đầu trang chung cho mọi trang của mục Content: tiêu đề trùng tên menu, một dòng mô tả,
 * nút thao tác bên phải. Mục có tab (Bài viết, Lịch đăng, Cấu hình) đặt tab ngay dưới.
 */
export function ContentPageHeader({ title, description, extra }: { title: ReactNode; description?: ReactNode; extra?: ReactNode }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap', marginBottom: 12 }}>
      <div style={{ minWidth: 0, flex: '1 1 320px' }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {title}
        </Typography.Title>
        {description ? (
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
            {description}
          </Typography.Text>
        ) : null}
      </div>
      {extra ? <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', flexShrink: 0 }}>{extra}</div> : null}
    </div>
  );
}
