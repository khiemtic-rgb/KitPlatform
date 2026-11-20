import { Alert, Button, Card, Typography } from 'antd';
import type { ProductionCredentialBackupGate } from '@/shared/api/production-ai-catalog.api';
import type { BackupGateLoadError } from './production-credential-backup-notice';
import { backupConditionLabel } from './production-credential-backup-notice';

export function ProductionCredentialSecurityPanel({
  gate,
  gateError,
  checking,
  secretHeld,
  onRecheck,
  onBack,
}: {
  gate: ProductionCredentialBackupGate | null;
  gateError: BackupGateLoadError | null;
  checking: boolean;
  secretHeld: boolean;
  onRecheck: () => void;
  onBack: () => void;
}) {
  const label = backupConditionLabel(gate, gateError);
  const verified = gate?.writeGate === 'OPEN' && gate.restoreVerification === 'VERIFIED';
  const writeOpen = gate?.writeGate === 'OPEN' && gate.restoreVerification === 'UNVERIFIED';
  const blocked = gate?.writeGate === 'BLOCKED';

  return (
    <Card>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Điều kiện bảo mật để lưu thông tin xác thực
      </Typography.Title>
      <Typography.Paragraph>
        Lần lưu vừa rồi không ghi credential. Đây là điều kiện bảo mật của hệ thống, không phải kết luận API Key sai.
      </Typography.Paragraph>

      {gateError === 'forbidden' && (
        <Alert
          type="warning"
          showIcon
          message="Cần quản trị viên"
          description="Tài khoản này không xem được trạng thái điều kiện bảo mật. Người có quyền quản trị nội dung của tenant cần mở Model AI và kiểm tra điều kiện này. Ứng dụng không có chức năng tự yêu cầu cấp quyền."
        />
      )}

      {gateError === 'unavailable' && (
        <Alert
          type="error"
          showIcon
          message="Không kiểm tra được"
          description="Chưa đọc được trạng thái từ máy chủ. Credential chưa được tính là đã lưu."
        />
      )}

      {label && gateError === null && (
        <Alert
          type={blocked ? 'warning' : 'info'}
          showIcon
          style={{ marginBottom: 16 }}
          message={`Trạng thái: ${label}`}
          description={
            verified
              ? 'Khóa đang dùng đã được niêm trên máy này, mở lại và khớp. Có thể quay lại form để lưu API Key.'
              : blocked
              ? 'Điều kiện ghi đang đóng. Bằng chứng khôi phục khóa mã hóa: chưa xác minh. Ứng dụng không có nút xác nhận, vì một nút như vậy không thay bằng chứng sao lưu và khôi phục.'
              : writeOpen
                ? 'Máy chủ đang cho phép ghi credential. Bằng chứng khôi phục vẫn chưa có, nên trạng thái này chưa phải đã xác minh.'
                : 'Chưa có đủ dữ liệu để kết luận điều kiện đã đạt.'
          }
        />
      )}

      {blocked && gate?.evidenceCode === 'KEY_ID_MISMATCH' && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message="Bản sao trên máy này không dùng được"
          description={`Khóa đang dùng là ${gate.encryptionKeyId}. Bản sao tìm thấy là ${gate.evidenceKeyId}. Kiểm tra lại không thể xác nhận điều kiện này.`}
        />
      )}
      {blocked && gate?.evidenceCode === 'SAME_MACHINE' && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message="Bản sao đang nằm trên chính máy này"
          description="Kiểm tra lại không xác nhận điều kiện. Cần bản sao ở một máy khác của đúng khóa đang dùng."
        />
      )}
      {blocked && (!gate?.evidenceCode || gate.evidenceCode === 'ABSENT') && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Chưa có bản sao để kiểm tra"
          description="Kiểm tra lại chưa thấy bằng chứng khôi phục của khóa đang dùng."
        />
      )}

      <Typography.Paragraph>
        {secretHeld
          ? 'Form cấu hình vẫn giữ API Key đã nhập. Quay lại form rồi bấm Lưu cấu hình nếu muốn gửi. Ứng dụng không tự gửi.'
          : 'API Key không còn trong form. Khi quay lại, cần nhập lại rồi bấm Lưu cấu hình. Hệ thống không khôi phục một API Key chưa lưu.'}
      </Typography.Paragraph>

      <div style={{ display: 'flex', gap: 8 }}>
        <Button onClick={onRecheck} loading={checking}>
          Kiểm tra lại
        </Button>
        <Button type="primary" onClick={onBack}>
          {verified || writeOpen ? 'Tiếp tục lưu thông tin xác thực' : 'Quay lại form'}
        </Button>
      </div>
    </Card>
  );
}
