import { useState } from 'react';
import { Button, Input } from 'antd';

const IMAGE_CHECKS = [
  'Đúng với nội dung',
  'Đúng nhân vật',
  'Đúng bối cảnh',
  'Đúng cảm xúc',
  'Có thể dùng để tạo video',
];

const VIDEO_CHECKS = [
  'Nhân vật đúng',
  'Chuyển động phù hợp',
  'Bối cảnh đúng',
  'Không có lỗi hình ảnh',
  'Âm thanh / lời thoại đúng',
  'Video có thể sử dụng',
];

export function ContentFamixaApprovalPanel({
  kind,
  onPass,
  onFail,
}: {
  kind: 'image' | 'video';
  onPass: () => void;
  onFail: (reason: string) => void;
}) {
  const [fail, setFail] = useState(false);
  const [reason, setReason] = useState('');
  const checks = kind === 'image' ? IMAGE_CHECKS : VIDEO_CHECKS;
  return (
    <div className="fx-approve">
      <p className="fx-approve__q">{kind === 'image' ? 'Cảnh này có đúng không?' : 'Kiểm tra:'}</p>
      <ul className="fx-approve__checks">
        {checks.map((item) => (
          <li key={item}>✓ {item}</li>
        ))}
      </ul>
      {fail ? (
        <div className="fx-approve__fail">
          <label>
            Lý do cần chỉnh sửa
            <Input.TextArea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
          </label>
          <p className="fx-desk__note">Ghi chú cho người kỹ thuật. Máy không tự tạo lại từ đây.</p>
          <div className="fx-desk__btns">
            <Button onClick={() => setFail(false)}>Quay lại</Button>
            <Button danger type="primary" onClick={() => onFail(reason.trim())}>
              Gửi yêu cầu chỉnh sửa
            </Button>
          </div>
        </div>
      ) : (
        <div className="fx-desk__btns">
          <Button onClick={() => setFail(true)}>Không đạt</Button>
          <Button type="primary" onClick={onPass}>
            {kind === 'image' ? '✓ Duyệt ảnh' : '✓ Duyệt video'}
          </Button>
        </div>
      )}
    </div>
  );
}
