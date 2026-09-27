import type { SummaryMetric, MetricStatus } from '../types';

interface SummaryCardProps {
  metric: SummaryMetric;
}

const STATUS_LABELS: Record<MetricStatus, string> = {
  excellent: 'Excellent',
  good: 'Good',
  warning: 'Needs Attention',
  critical: 'Critical',
};

const TREND_ICONS = {
  up: '↑',
  down: '↓',
  stable: '→',
};

export function SummaryCard({ metric }: SummaryCardProps) {
  const { label, value, score, status, trend, trendLabel } = metric;
  const circumference = 2 * Math.PI * 20; // r=20
  const dashOffset = circumference - (score / 100) * circumference;

  return (
    <div className={`summary-card summary-card--${status}`}>
      <div className="summary-card__header">
        <span className="summary-card__label">{label}</span>
        <span className={`summary-card__badge summary-card__badge--${status}`}>
          {STATUS_LABELS[status]}
        </span>
      </div>

      <div className="summary-card__body">
        <div className="summary-card__ring" aria-label={`${label} score: ${score} out of 100`}>
          <svg width="56" height="56" viewBox="0 0 56 56">
            <circle cx="28" cy="28" r="20" fill="none" strokeWidth="5" className="ring-track" />
            <circle
              cx="28"
              cy="28"
              r="20"
              fill="none"
              strokeWidth="5"
              strokeLinecap="round"
              strokeDasharray={circumference}
              strokeDashoffset={dashOffset}
              className={`ring-fill ring-fill--${status}`}
              transform="rotate(-90 28 28)"
            />
          </svg>
          <span className="ring-label">{score}</span>
        </div>

        <div className="summary-card__details">
          <span className="summary-card__value">{value}</span>
          <span className={`summary-card__trend summary-card__trend--${trend}`}>
            {TREND_ICONS[trend]} {trendLabel}
          </span>
        </div>
      </div>
    </div>
  );
}
