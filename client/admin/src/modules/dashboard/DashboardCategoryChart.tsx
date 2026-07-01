import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Empty, Spin, Typography, message } from 'antd';
import { RightOutlined } from '@ant-design/icons';
import { runReport } from '@/shared/api/reports.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayMoney } from '@/shared/utils/money';
import {
  buildCategoryChartSlices,
  buildConicGradient,
  type CategoryChartSlice,
} from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  rollingDaysRangeIso,
  type RevenuePeriodDays,
} from '@/modules/dashboard/dashboard-revenue-range';

type DashboardCategoryChartProps = {
  enabled: boolean;
  periodDays: RevenuePeriodDays;
};

export function DashboardCategoryChart({ enabled, periodDays }: DashboardCategoryChartProps) {
  const { t } = useTranslation('dashboard');
  const [loading, setLoading] = useState(false);
  const [slices, setSlices] = useState<CategoryChartSlice[]>([]);
  const [periodTotal, setPeriodTotal] = useState(0);

  const load = useCallback(async () => {
    if (!enabled) return;
    setLoading(true);
    try {
      const range = rollingDaysRangeIso(periodDays);
      const result = await runReport('sales/revenue-by-category', {
        from: range.from,
        to: range.to,
      });
      const nextSlices = buildCategoryChartSlices(result.rows, t('categoryChart.other'));
      setSlices(nextSlices);
      const totalFromReport = result.totals ? readReportFieldNumber(result.totals, 'netAmount') : 0;
      const sliceTotal = nextSlices.reduce((sum, slice) => sum + slice.netAmount, 0);
      setPeriodTotal(totalFromReport > 0 ? totalFromReport : sliceTotal);
    } catch (error) {
      message.error(apiErrorMessage(error, t('categoryChart.loadFailed')));
      setSlices([]);
      setPeriodTotal(0);
    } finally {
      setLoading(false);
    }
  }, [enabled, periodDays, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const gradient = useMemo(() => buildConicGradient(slices), [slices]);

  if (!enabled) return null;

  return (
    <section className="dashboard-category-chart">
      <div className="dashboard-category-chart__head">
        <div>
          <Typography.Text className="dashboard-category-chart__eyebrow">
            {t('categoryChart.title')}
          </Typography.Text>
          <div className="dashboard-category-chart__total">{formatDisplayMoney(periodTotal)}</div>
          <Typography.Text type="secondary" className="dashboard-category-chart__subtitle">
            {t('categoryChart.subtitle', { days: periodDays })}
          </Typography.Text>
        </div>
        <Link to="/reports/sales/revenue-by-category" className="dashboard-category-chart__report-link">
          {t('categoryChart.fullReport')} <RightOutlined />
        </Link>
      </div>

      <Spin spinning={loading}>
        {slices.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={t('categoryChart.empty')}
            className="dashboard-category-chart__empty"
          />
        ) : (
          <div className="dashboard-category-chart__body">
            <div className="dashboard-category-chart__donut-wrap">
              <div
                className="dashboard-category-chart__donut"
                style={{ background: gradient }}
                role="img"
                aria-label={t('categoryChart.title')}
              >
                <div className="dashboard-category-chart__donut-hole">
                  <span className="dashboard-category-chart__donut-label">{t('categoryChart.topN', { count: 5 })}</span>
                </div>
              </div>
            </div>
            <ul className="dashboard-category-chart__legend">
              {slices.map((slice) => (
                <li key={slice.label} className="dashboard-category-chart__legend-item">
                  <span className="dashboard-category-chart__legend-name">
                    <span
                      className="dashboard-category-chart__legend-dot"
                      style={{ backgroundColor: slice.color }}
                    />
                    <span className="dashboard-category-chart__legend-text">{slice.label}</span>
                  </span>
                  <span className="dashboard-category-chart__legend-meta">
                    <strong>{slice.sharePercent}%</strong>
                    <span>{formatDisplayMoney(slice.netAmount)}</span>
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Spin>
    </section>
  );
}
