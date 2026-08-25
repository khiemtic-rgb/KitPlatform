import { Button, Tag, Tooltip } from 'antd';
import { MedicineBoxOutlined } from '@ant-design/icons';
import {
  consultationSafetyAlertType,
  consultationSafetyLevelLabel,
} from '@/shared/api/pharmacy-consultation.api';

function consultationSafetyTagColor(level: string): string {
  switch (consultationSafetyAlertType(level)) {
    case 'error':
      return 'error';
    case 'warning':
      return 'warning';
    case 'info':
      return 'processing';
    default:
      return 'success';
  }
}

type Props = {
  safetyLevel: string;
  onReopen: () => void;
  onUnlink: () => void;
};

export function PosConsultationSessionChip({ safetyLevel, onReopen, onUnlink }: Props) {
  const safetyLabel = consultationSafetyLevelLabel(safetyLevel);

  return (
    <div className="pos-consultation-chip">
      <Tooltip title="Phiên tư vấn đã lưu. Chốt đơn sẽ tự liên kết với hóa đơn.">
        <Tag
          color={consultationSafetyTagColor(safetyLevel)}
          icon={<MedicineBoxOutlined />}
          className="pos-consultation-chip__tag"
        >
          Đã lưu phiên tư vấn
        </Tag>
      </Tooltip>
      <span className="pos-consultation-chip__level">{safetyLabel}</span>
      <Button type="link" size="small" className="pos-consultation-chip__action" onClick={onReopen}>
        Mở lại
      </Button>
      <Button type="link" size="small" className="pos-consultation-chip__action" onClick={onUnlink}>
        Bỏ liên kết
      </Button>
    </div>
  );
}
