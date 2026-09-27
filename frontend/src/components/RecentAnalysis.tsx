import type { AnalysisRecord } from '../types';

interface RecentAnalysisProps {
  records: AnalysisRecord[];
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function ScoreChip({ value, dimmed }: { value: number; dimmed?: boolean }) {
  const cls =
    dimmed ? 'score-chip score-chip--na'
    : value >= 85 ? 'score-chip score-chip--excellent'
    : value >= 70 ? 'score-chip score-chip--good'
    : value >= 55 ? 'score-chip score-chip--warning'
    : 'score-chip score-chip--critical';

  return <span className={cls}>{dimmed ? '—' : value}</span>;
}

export function RecentAnalysis({ records }: RecentAnalysisProps) {
  if (records.length === 0) {
    return (
      <section className="section" aria-labelledby="recent-title">
        <h2 id="recent-title" className="section__title">Recent Analysis</h2>
        <div className="empty-state">
          <p className="empty-state__message">No analyses yet. Run your first analysis above.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="section" aria-labelledby="recent-title">
      <h2 id="recent-title" className="section__title">Recent Analysis</h2>
      <p className="section__subtitle">Latest repository scans across your projects.</p>

      <div className="table-wrapper" role="region" aria-label="Recent analyses" tabIndex={0}>
        <table className="analysis-table">
          <thead>
            <tr>
              <th>Repository</th>
              <th>Branch</th>
              <th>Date</th>
              <th className="th-center">Overall</th>
              <th className="th-center">Quality</th>
              <th className="th-center">Security</th>
              <th className="th-center">Perf.</th>
              <th className="th-center">Debt</th>
              <th className="th-center">Status</th>
            </tr>
          </thead>
          <tbody>
            {records.map((rec) => {
              const failed = rec.status === 'failed';
              return (
                <tr key={rec.id}>
                  <td className="td-repo">{rec.repository}</td>
                  <td><code className="branch-tag">{rec.branch}</code></td>
                  <td className="td-date">{formatDate(rec.analyzedAt)}</td>
                  <td className="td-center"><ScoreChip value={rec.overallScore} dimmed={failed} /></td>
                  <td className="td-center"><ScoreChip value={rec.metrics.codeQuality} dimmed={failed} /></td>
                  <td className="td-center"><ScoreChip value={rec.metrics.security} dimmed={failed} /></td>
                  <td className="td-center"><ScoreChip value={rec.metrics.performance} dimmed={failed} /></td>
                  <td className="td-center"><ScoreChip value={rec.metrics.technicalDebt} dimmed={failed} /></td>
                  <td className="td-center">
                    <span className={`status-pill status-pill--${rec.status}`}>
                      {rec.status === 'completed' ? 'Completed'
                        : rec.status === 'in-progress' ? 'In Progress'
                        : 'Failed'}
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}
