import { readReportFieldNumber, readReportFieldString } from '@/modules/dashboard/dashboard-revenue-range';

export type CategoryChartSlice = {
  label: string;
  netAmount: number;
  sharePercent: number;
  color: string;
};

const SLICE_COLORS = ['#0d9488', '#0284c7', '#7c3aed', '#d97706', '#db2777', '#64748b'];

export function buildCategoryChartSlices(
  rows: Record<string, unknown>[],
  otherLabel: string,
  topN = 5,
): CategoryChartSlice[] {
  const parsed = rows
    .map((row) => ({
      label: readReportFieldString(row, 'categoryLabel') || otherLabel,
      netAmount: readReportFieldNumber(row, 'netAmount'),
    }))
    .filter((item) => item.netAmount > 0)
    .sort((a, b) => b.netAmount - a.netAmount);

  const total = parsed.reduce((sum, item) => sum + item.netAmount, 0);
  if (total <= 0) return [];

  const top = parsed.slice(0, topN);
  const otherAmount = parsed.slice(topN).reduce((sum, item) => sum + item.netAmount, 0);

  const slices: CategoryChartSlice[] = top.map((item, index) => ({
    label: item.label,
    netAmount: item.netAmount,
    sharePercent: Math.round((item.netAmount / total) * 1000) / 10,
    color: SLICE_COLORS[index % SLICE_COLORS.length],
  }));

  if (otherAmount > 0) {
    slices.push({
      label: otherLabel,
      netAmount: otherAmount,
      sharePercent: Math.round((otherAmount / total) * 1000) / 10,
      color: SLICE_COLORS[5],
    });
  }

  return slices;
}

export function buildConicGradient(slices: CategoryChartSlice[]): string {
  if (slices.length === 0) return '#f0f0f0';
  let cursor = 0;
  const stops = slices.map((slice) => {
    const start = cursor;
    cursor += slice.sharePercent;
    return `${slice.color} ${start}% ${cursor}%`;
  });
  return `conic-gradient(${stops.join(', ')})`;
}
