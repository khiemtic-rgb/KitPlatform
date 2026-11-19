import { Tooltip, Typography } from 'antd';
import type { CategoryScore } from '@/shared/api/assessment.api';
import { normalizeVietnamese } from '@/shared/score/vn-normalize';

const { Text } = Typography;

/** Quy đổi % trưởng thành (0–100) từ thang gốc 1–4. */
export function toScore100(scorePct: number): number {
  return Math.round(Math.max(0, Math.min(100, scorePct)));
}

export function getMaturityLevel(score100: number): { label: string; color: string } {
  if (score100 >= 80) return { label: 'Xuất sắc', color: '#047857' };
  if (score100 >= 60) return { label: 'Khá tốt', color: '#0f766e' };
  if (score100 >= 40) return { label: 'Cơ bản', color: '#b45309' };
  return { label: 'Cần cải thiện', color: '#b91c1c' };
}

/** Chú thích nhóm có thuật ngữ kỹ thuật. */
const CATEGORY_HINTS: Record<string, string> = {
  CUSTOMER: 'Quản lý khách hàng, lịch sử mua hàng, chăm sóc sau bán',
  OPERATIONS: 'Quy trình làm việc, phân quyền, SOP (Quy trình vận hành chuẩn)',
  INVENTORY: 'Tồn kho, hạn dùng, nhập hàng, kiểm kê',
  BUSINESS: 'Doanh thu, khuyến mại, mục tiêu kinh doanh',
  TECH: 'Phần mềm, dữ liệu, Dashboard (bảng điều khiển), công nghệ thông tin',
  GROWTH: 'Kế hoạch mở rộng và phát triển dài hạn',
};

function CategoryLabel({ code, name }: { code: string; name: string }) {
  const hint = CATEGORY_HINTS[code];
  if (!hint) return <span>{name}</span>;
  return (
    <Tooltip title={hint}>
      <span className="score-label-text">{name}</span>
    </Tooltip>
  );
}

export function OverallScoreHero({ scorePct }: { scorePct: number }) {
  const score100 = toScore100(scorePct);
  const level = getMaturityLevel(score100);

  return (
    <div className="score-hero">
      <Text type="secondary" className="score-hero-caption">
        Điểm trưởng thành tổng (thang 100)
      </Text>
      <div className="score-hero-value">
        <span className="score-hero-number">{score100}</span>
        <span className="score-hero-max">/ 100</span>
      </div>
      <span className="score-hero-badge" style={{ background: `${level.color}18`, color: level.color }}>
        {level.label}
      </span>
      <Text type="secondary" className="score-hero-footnote">
        0 = sơ khai · 100 = trưởng thành cao nhất
      </Text>
    </div>
  );
}

export function CategoryScoreTable({ items }: { items: CategoryScore[] }) {
  const sorted = [...items].sort((a, b) => {
    if (a.insufficientData !== b.insufficientData) return a.insufficientData ? 1 : -1;
    return b.scorePct - a.scorePct;
  });

  return (
    <div className="score-table">
      <div className="score-table-head">
        <span>Nhóm năng lực</span>
        <span>Mức độ</span>
        <span>Điểm</span>
      </div>
      {sorted.map((item) => {
        if (item.insufficientData) {
          return (
            <div key={item.code} className="score-table-row">
              <div className="score-table-name">
                <CategoryLabel code={item.code} name={item.name} />
              </div>
              <div className="score-table-bar-wrap" />
              <div className="score-table-score">
                <strong>Chưa đủ dữ liệu</strong>
              </div>
            </div>
          );
        }
        const score100 = toScore100(item.scorePct);
        const level = getMaturityLevel(score100);
        return (
          <div key={item.code} className="score-table-row">
            <div className="score-table-name">
              <CategoryLabel code={item.code} name={item.name} />
            </div>
            <div className="score-table-bar-wrap">
              <div className="score-table-bar">
                <span style={{ width: `${score100}%`, background: level.color }} />
              </div>
            </div>
            <div className="score-table-score">
              <strong>{score100}</strong>
              <Text type="secondary" className="score-table-score-sub">
                {level.label}
              </Text>
            </div>
          </div>
        );
      })}
    </div>
  );
}

/** Chú thích từ viết tắt / tiếng Anh + chuẩn hóa thiếu dấu trong nội dung insight. */
export function annotateInsightText(text: string): string {
  return normalizeVietnamese(text);
}
