import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { RightOutlined } from '@ant-design/icons';
import { Segmented, Spin, Typography, message } from 'antd';
import { runReport } from '@/shared/api/reports.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatDisplayMoney } from '@/shared/utils/money';
import {
  buildDailyRevenueChartPoints,
  readReportFieldNumber,
  rollingDaysRangeIso,
  sumNetAmount,
} from '@/modules/dashboard/dashboard-revenue-range';

export type RevenuePeriodDays = 7 | 14 | 30;

type DashboardRevenueChartProps = {
  enabled: boolean;
};

export function DashboardRevenueChart({ enabled }: DashboardRevenueChartProps) {
  const { t } = useTranslation('dashboard');
  const [periodDays, setPeriodDays] = useState<RevenuePeriodDays>(7);
  const [loading, setLoading] = useState(false);
  const [points, setPoints] = useState<ReturnType<typeof buildDailyRevenueChartPoints>>([]);
  const [periodTotal, setPeriodTotal] = useState(0);

  const load = useCallback(async () => {
    if (!enabled) return;
    setLoading(true);
    try {
      const range = rollingDaysRangeIso(periodDays);
      const result = await runReport('sales/revenue-by-period', {
        from: range.from,
        to: range.to,
        groupBy: 'day',
      });
      const chartPoints = buildDailyRevenueChartPoints(periodDays, result.rows);
      setPoints(chartPoints);
      const totalFromReport = result.totals ? readReportFieldNumber(result.totals, 'netAmount') : 0;
      setPeriodTotal(totalFromReport > 0 ? totalFromReport : sumNetAmount(chartPoints));
    } catch (error) {
      message.error(apiErrorMessage(error, t('revenueChart.loadFailed')));
      setPoints([]);
      setPeriodTotal(0);
    } finally {
      setLoading(false);
    }
  }, [enabled, periodDays, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const totalNet = periodTotal;
  const maxNet = useMemo(() => Math.max(...points.map((p) => p.netAmount), 1), [points]);

  const periodOptions = useMemo(
    () => [
      { label: t('revenueChart.period7'), value: 7 as RevenuePeriodDays },
      { label: t('revenueChart.period14'), value: 14 as RevenuePeriodDays },
      { label: t('revenueChart.period30'), value: 30 as RevenuePeriodDays },
    ],
    [t],
  );

  if (!enabled) return null;

  return (
    <section className="dashboard-revenue-chart">
      <div className="dashboard-revenue-chart__head">
        <div>
          <Typography.Text className="dashboard-revenue-chart__eyebrow">
            {t('revenueChart.title')}
          </Typography.Text>
          <div className="dashboard-revenue-chart__total">{formatDisplayMoney(totalNet)}</div>
          <Typography.Text type="secondary" className="dashboard-revenue-chart__subtitle">
            {t('revenueChart.subtitle', { days: periodDays })}
          </Typography.Text>
        </div>
        <div className="dashboard-revenue-chart__controls">
          <Segmented
            size="small"
            value={periodDays}
            options={periodOptions}
            onChange={(value) => setPeriodDays(value as RevenuePeriodDays)}
          />
          <Link to="/reports/sales/revenue-by-period" className="dashboard-revenue-chart__report-link">
            {t('revenueChart.fullReport')} <RightOutlined />
          </Link>
        </div>
      </div>

      <Spin spinning={loading}>
        <div
          className="dashboard-revenue-chart__plot"
          role="img"
          aria-label={t('revenueChart.title')}
          style={{ gridTemplateColumns: `repeat(${Math.max(points.length, 1)}, minmax(28px, 1fr))` }}
        >
          {points.map((point, index) => {
            const heightPct = point.netAmount <= 0 ? 2 : Math.max(8, (point.netAmount / maxNet) * 100);
            return (
              <div key={`${point.label}-${index}`} className="dashboard-revenue-chart__bar-wrap">
                <div
                  className={
                    point.netAmount <= 0
                      ? 'dashboard-revenue-chart__bar dashboard-revenue-chart__bar--empty'
                      : 'dashboard-revenue-chart__bar'
                  }
                  style={{ height: `${heightPct}%` }}
                  title={`${point.label}: ${formatDisplayMoney(point.netAmount)}`}
                />
                <span className="dashboard-revenue-chart__bar-label">{point.label}</span>
              </div>
            );
          })}
        </div>
      </Spin>
    </section>
  );
}
