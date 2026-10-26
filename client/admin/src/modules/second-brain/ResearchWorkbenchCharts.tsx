import { Typography } from 'antd';
import type { CandleChartModel, DistributionBar, EquityModel } from './research-workbench';
import { formatAmount } from './research-workbench';

export function CandleChart({ model }: { model: CandleChartModel }) {
  const width = 720;
  const height = 240;
  const padLeft = 12;
  const padRight = 12;
  const padTop = 20;
  const padBottom = 24;
  const lows = model.bars.map((bar) => bar.low);
  const highs = model.bars.map((bar) => bar.high);
  let min = Math.min(...lows);
  let max = Math.max(...highs);
  if (min === max) {
    min -= 1;
    max += 1;
  }
  const plotWidth = width - padLeft - padRight;
  const plotHeight = height - padTop - padBottom;
  const slot = plotWidth / model.bars.length;
  const y = (value: number) => padTop + ((max - value) / (max - min)) * plotHeight;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Biểu đồ nến và tín hiệu" style={{ width: '100%', height: 'auto' }}>
      {model.bars.map((bar, index) => {
        const x = padLeft + index * slot + slot / 2;
        const bodyWidth = Math.max(2, slot * 0.55);
        const up = bar.close >= bar.open;
        const color = up ? '#389e0d' : '#cf1322';
        const top = y(Math.max(bar.open, bar.close));
        const bodyHeight = Math.max(1, Math.abs(y(bar.open) - y(bar.close)));
        return (
          <g key={bar.time}>
            <line x1={x} x2={x} y1={y(bar.high)} y2={y(bar.low)} stroke={color} strokeWidth="1" />
            <rect x={x - bodyWidth / 2} y={top} width={bodyWidth} height={bodyHeight} fill={color} />
            {bar.buy && <circle cx={x} cy={Math.max(8, y(bar.high) - 8)} r="3.5" fill="#1677ff" />}
            {bar.sell && <circle cx={x} cy={Math.min(height - 8, y(bar.low) + 8)} r="3.5" fill="#d48806" />}
          </g>
        );
      })}
    </svg>
  );
}

export function EquityChart({ model }: { model: EquityModel }) {
  const width = 720;
  const height = 200;
  const values = model.points.flatMap((point) => [point.equity, point.equity - point.drawdown]);
  let min = Math.min(0, ...values);
  let max = Math.max(0, ...values);
  if (min === max) {
    min -= 1;
    max += 1;
  }
  const x = (index: number) => 16 + (index / Math.max(1, model.points.length - 1)) * (width - 32);
  const y = (value: number) => 16 + ((max - value) / (max - min)) * (height - 32);
  const line = model.points.map((point, index) => `${x(index)},${y(point.equity)}`).join(' ');
  const drawdown = model.points.map((point, index) => `${x(index)},${y(point.drawdown)}`).join(' ');
  return (
    <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="PnL lũy kế và drawdown" style={{ width: '100%', height: 'auto' }}>
      <polyline fill="none" stroke="#1677ff" strokeWidth="2" points={line} />
      <polyline fill="none" stroke="#d48806" strokeWidth="2" points={drawdown} />
    </svg>
  );
}

export function DistributionChart({ bars }: { bars: DistributionBar[] }) {
  const max = Math.max(...bars.map((bar) => Math.abs(bar.net)), 0.00000001);
  return (
    <div>
      {bars.map((bar) => (
        <div key={bar.id} style={{ display: 'grid', gridTemplateColumns: 'minmax(120px, 180px) 1fr auto', gap: 8, alignItems: 'center', marginBottom: 6 }}>
          <Typography.Text ellipsis>{bar.label}</Typography.Text>
          <div style={{ background: '#f5f5f5', height: 8 }}>
            <div style={{ width: `${(Math.abs(bar.net) / max) * 100}%`, height: 8, background: bar.net > 0 ? '#389e0d' : bar.net < 0 ? '#cf1322' : '#8c8c8c' }} />
          </div>
          <Typography.Text>{formatAmount(String(bar.net))}</Typography.Text>
        </div>
      ))}
    </div>
  );
}
