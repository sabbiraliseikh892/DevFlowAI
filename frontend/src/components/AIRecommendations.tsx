import type { Recommendation } from '../types';

interface AIRecommendationsProps {
  recommendations: Recommendation[];
}

const SEVERITY_ICON: Record<Recommendation['severity'], string> = {
  high: '⬤',
  medium: '⬤',
  low: '⬤',
};

export function AIRecommendations({ recommendations }: AIRecommendationsProps) {
  if (recommendations.length === 0) {
    return (
      <section className="section" aria-labelledby="rec-title">
        <h2 id="rec-title" className="section__title">AI Recommendations</h2>
        <div className="empty-state">
          <p className="empty-state__message">No recommendations found. Your repository looks great!</p>
        </div>
      </section>
    );
  }

  return (
    <section className="section" aria-labelledby="rec-title">
      <h2 id="rec-title" className="section__title">
        AI Recommendations
        <span className="section__count">{recommendations.length}</span>
      </h2>
      <p className="section__subtitle">
        AI-generated insights to improve your codebase.
      </p>

      <ul className="rec-list" role="list">
        {recommendations.map((rec) => (
          <li key={rec.id} className={`rec-item rec-item--${rec.severity}`}>
            <div className="rec-item__header">
              <span className={`rec-severity rec-severity--${rec.severity}`} aria-label={`${rec.severity} severity`}>
                {SEVERITY_ICON[rec.severity]}
              </span>
              <span className="rec-category">{rec.category}</span>
              <span className={`rec-badge rec-badge--${rec.severity}`}>{rec.severity}</span>
            </div>
            <h3 className="rec-title">{rec.title}</h3>
            <p className="rec-description">{rec.description}</p>
            {rec.file && (
              <div className="rec-location">
                <code className="rec-file">{rec.file}{rec.line != null ? `:${rec.line}` : ''}</code>
              </div>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
