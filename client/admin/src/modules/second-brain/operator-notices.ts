import type { TradingSummary } from './trading.api';

export type OperatorNotice = {
  id: string;
  action: string;
  reason: string;
  priority: 'high' | 'normal';
  blocker: string | null;
  status: 'open' | 'informational';
  href: string;
};

export function operatorNotices(summary: TradingSummary | null): OperatorNotice[] {
  if (!summary) {
    return [{
      id: 'summary-missing',
      action: 'Tải lại tình hình',
      reason: 'API summary chưa trả dữ liệu.',
      priority: 'high',
      blocker: 'Không có payload summary.',
      status: 'open',
      href: '/second-brain/overview',
    }];
  }
  const items: OperatorNotice[] = [];
  if (summary.tradeCount === 0) {
    items.push({
      id: 'no-trades',
      action: 'Vào phiên làm việc để nối sàn hoặc nhập CSV',
      reason: 'Chưa có giao dịch được lưu.',
      priority: 'high',
      blocker: null,
      status: 'open',
      href: '/second-brain/session',
    });
  }
  if (summary.onlyFixtureData) {
    items.push({
      id: 'fixture-only',
      action: 'Đồng bộ lịch sử sàn hoặc nhập CSV của người dùng',
      reason: 'Toàn bộ giao dịch đang là dữ liệu kiểm thử.',
      priority: 'high',
      blocker: 'Không dùng các số này như kết quả giao dịch thật.',
      status: 'open',
      href: '/second-brain/session',
    });
  }
  if (summary.riskVetoCount > 0) {
    items.push({
      id: 'risk-veto',
      action: 'Xem các lần risk từ chối',
      reason: `${summary.riskVetoCount} lần risk đã từ chối trong dữ liệu đã lưu.`,
      priority: 'normal',
      blocker: null,
      status: 'open',
      href: '/second-brain/risk',
    });
  }
  if (summary.paperExecutionCount > 0 && summary.paperExecutionCount < 30) {
    items.push({
      id: 'paper-sample',
      action: 'Đọc paper như mô phỏng đã lưu',
      reason: `Có ${summary.paperExecutionCount} bản ghi paper. Mẫu dưới 30 không đủ để kết luận.`,
      priority: 'normal',
      blocker: 'Summary không có kết quả out-of-sample.',
      status: 'informational',
      href: '/second-brain/risk',
    });
  }
  return items;
}
