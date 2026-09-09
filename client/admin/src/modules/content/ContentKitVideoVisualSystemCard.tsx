import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Space, Tag, Typography } from 'antd';
import { fetchKitVideoVisualSystem, lockKitVideoVisualSystem, type KitVideoVisualSystemRow } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { FAMIXA_VISUAL_STYLE_SYSTEM_V1 } from './kit-video-visual-system';
import { lookStatusVi } from './content-famixa-look-status';

export function ContentKitVideoVisualSystemCard() {
  const [row, setRow] = useState<KitVideoVisualSystemRow>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setBusy(true);
    void fetchKitVideoVisualSystem('FAMIXA')
      .then((next) => {
        setRow(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa đọc được luật hình từ máy chủ — đang hiện bản local.')))
      .finally(() => setBusy(false));
  }, []);

  const lock = () => {
    if (!row?.id || row.status === 'locked') return;
    setBusy(true);
    void lockKitVideoVisualSystem(row.id)
      .then((next) => {
        setRow(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa khóa được luật hình.')))
      .finally(() => setBusy(false));
  };

  const status = lookStatusVi(row?.status || FAMIXA_VISUAL_STYLE_SYSTEM_V1.status);

  return (
    <Card className="fx-look__card" size="small" title="Bước 1 · Luật hình của thế giới Famixa">
      <p className="fx-look__help">
        Đây là <b>luật vẽ chung</b>: điện ảnh, có cảm xúc, stylized — không giả người thật, không hoạt hình, không quảng
        cáo. Mọi nhân vật (Minh, bố, mẹ…) phải cùng một thế giới. Khóa luật này <b>không</b> khóa mặt Minh.
      </p>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={status.color}>{status.label}</Tag>
        <Tag>Famixa</Tag>
        <Tag>V1</Tag>
      </Space>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <Alert type="info" showIcon style={{ marginBottom: 8 }} message={status.hint} description="Nhìn là biết Famixa, không chỉ biết “ảnh AI đẹp”." />
      <Collapse
        size="small"
        items={[
          {
            key: 'more',
            label: 'Xem luật chi tiết',
            children: (
              <ul style={{ margin: 0, paddingLeft: 18, color: '#334155', lineHeight: 1.6 }}>
                <li>Nhân vật: mặt, tóc, dáng, tuổi phải nhận ra được.</li>
                <li>Cảm xúc: mắt, vai, khoảng cách, khoảng trống cũng kể chuyện.</li>
                <li>Cấm đổi mặt / đổi tóc / già hơn / thêm người không có trong chuyện.</li>
                <li>Minh 11 → 16 → 23 vẫn là một người, không phải nhân vật mới.</li>
              </ul>
            ),
          },
        ]}
      />
      <Space wrap style={{ marginTop: 8 }}>
        <Button size="small" loading={busy} disabled={!row?.id || row.status === 'locked'} onClick={lock}>
          Khóa luật hình V1
        </Button>
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
        Nút này chỉ khóa phong cách thế giới. Không vẽ ảnh, không khóa Minh, không chạy máy quay.
      </Typography.Paragraph>
    </Card>
  );
}
