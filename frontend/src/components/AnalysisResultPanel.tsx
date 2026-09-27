import { type AnalysisResult, type AnalysisRecommendation, type RepoMetadata } from '../types';

interface AnalysisResultPanelProps {
  result: AnalysisResult;
  onReanalyze?: () => void;
}

// ── helpers ────────────────────────────────────────────────────────────────────

function scoreStatus(score: number): 'excellent' | 'good' | 'warning' | 'critical' {
  if (score >= 85) return 'excellent';
  if (score >= 70) return 'good';
  if (score >= 50) return 'warning';
  return 'critical';
}

function ScoreRing({ label, score }: { label: string; score: number }) {
  const status = scoreStatus(score);
  const r = 20;
  const circ = 2 * Math.PI * r;
  const offset = circ - (score / 100) * circ;

  return (
    <div className="ar-score-card">
      <div
        className="summary-card__ring"
        aria-label={`${label}: ${score} out of 100`}
      >
        <svg width="56" height="56" viewBox="0 0 56 56">
          <circle cx="28" cy="28" r={r} fill="none" strokeWidth="5" className="ring-track" />
          <circle
            cx="28"
            cy="28"
            r={r}
            fill="none"
            strokeWidth="5"
            strokeLinecap="round"
            strokeDasharray={circ}
            strokeDashoffset={offset}
            className={`ring-fill ring-fill--${status}`}
            transform="rotate(-90 28 28)"
          />
        </svg>
        <span className="ring-label">{score}</span>
      </div>
      <span className="ar-score-label">{label}</span>
    </div>
  );
}

function FindingBadge({ count, severity }: { count: number; severity: string }) {
  const cls =
    severity === 'critical' ? 'rec-badge rec-badge--high'
    : severity === 'high'   ? 'rec-badge rec-badge--high'
    : severity === 'medium' ? 'rec-badge rec-badge--medium'
    : 'rec-badge rec-badge--low';
  return (
    <span className={cls} style={{ marginRight: '0.5rem', fontSize: '0.8rem' }}>
      {count} {severity}
    </span>
  );
}

function RecommendationItem({ rec }: { rec: AnalysisRecommendation }) {
  const sev = rec.severity === 'critical' ? 'high' : rec.severity;
  const evidenceUnavailable = !rec.evidence || rec.evidence === 'Evidence unavailable';

  return (
    <li className={`rec-item rec-item--${sev}`}>
      <div className="rec-item__header">
        <span className={`rec-severity rec-severity--${sev}`} aria-label={`${rec.severity} severity`}>⬤</span>
        <span className="rec-category">{rec.category}</span>
        <span className={`rec-badge rec-badge--${sev}`}>{rec.severity}</span>
      </div>
      <h4 className="rec-title">{rec.title}</h4>

      {/* Explanation — why this matters */}
      <p className="rec-explanation">{rec.explanation}</p>

      {/* Evidence — concrete evidence from the repo */}
      <div className="rec-evidence-block">
        <span className="rec-evidence-label">Evidence</span>
        {evidenceUnavailable ? (
          <span className="rec-evidence-unavailable">{rec.evidence || 'Evidence unavailable'}</span>
        ) : (
          <span className="rec-evidence-text">{rec.evidence}</span>
        )}
      </div>

      {/* Recommended action */}
      {rec.recommendedAction && (
        <div className="rec-action-block">
          <span className="rec-action-label">Recommended action</span>
          <span className="rec-action-text">{rec.recommendedAction}</span>
        </div>
      )}

      {/* Affected path */}
      {rec.affectedPath && (
        <div className="rec-location">
          <code className="rec-file">
            {rec.affectedPath}{rec.line != null ? `:${rec.line}` : ''}
          </code>
        </div>
      )}
    </li>
  );
}

/** Badge row helper */
function TagBadge({ text }: { text: string }) {
  return (
    <span className="repo-tag" title={text}>
      {text}
    </span>
  );
}

function BoolIndicator({ value, trueLabel, falseLabel }: {
  value: boolean;
  trueLabel: string;
  falseLabel: string;
}) {
  return (
    <span className={`repo-indicator ${value ? 'repo-indicator--yes' : 'repo-indicator--no'}`}>
      {value ? `✓ ${trueLabel}` : `✗ ${falseLabel}`}
    </span>
  );
}

function RepoMetadataPanel({ meta }: { meta: RepoMetadata }) {
  const topLangs = Object.entries(meta.languageCounts)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 6);

  return (
    <section className="ar-section ar-meta-panel" aria-labelledby="ar-repo-meta-title">
      <h3 id="ar-repo-meta-title" className="ar-section-title">
        Repository Snapshot
        <span className="ar-meta-badge ar-meta-badge--live">live from GitHub</span>
      </h3>

      {meta.description && (
        <p className="ar-meta-description">{meta.description}</p>
      )}

      <div className="ar-meta-grid">
        {/* Languages */}
        {topLangs.length > 0 && (
          <div className="ar-meta-cell">
            <span className="ar-meta-label">Languages</span>
            <div className="ar-tags">
              {topLangs.map(([lang, count]) => (
                <TagBadge key={lang} text={`${lang} (${count})`} />
              ))}
            </div>
          </div>
        )}

        {/* File count */}
        <div className="ar-meta-cell">
          <span className="ar-meta-label">Files</span>
          <span className="ar-meta-value">
            {meta.totalFileCount} total, {meta.sampledFileCount} sampled
          </span>
        </div>

        {/* Indicators */}
        <div className="ar-meta-cell">
          <span className="ar-meta-label">Health Indicators</span>
          <div className="ar-indicators">
            <BoolIndicator value={meta.hasReadme} trueLabel="README" falseLabel="No README" />
            <BoolIndicator value={meta.hasTests} trueLabel="Tests found" falseLabel="No tests" />
            <BoolIndicator value={meta.hasCiConfig} trueLabel="CI configured" falseLabel="No CI" />
          </div>
        </div>

        {/* Dependency files */}
        {meta.dependencyFiles.length > 0 && (
          <div className="ar-meta-cell">
            <span className="ar-meta-label">Dependency manifests</span>
            <div className="ar-tags">
              {meta.dependencyFiles.map((f) => <TagBadge key={f} text={f} />)}
            </div>
          </div>
        )}

        {/* Top-level directories */}
        {meta.topLevelDirectories.length > 0 && (
          <div className="ar-meta-cell">
            <span className="ar-meta-label">Structure</span>
            <div className="ar-tags">
              {meta.topLevelDirectories.slice(0, 10).map((d) => (
                <TagBadge key={d} text={d + '/'} />
              ))}
              {meta.topLevelDirectories.length > 10 && (
                <span className="ar-tags-more">
                  +{meta.topLevelDirectories.length - 10} more
                </span>
              )}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

/** Provider badge shown in the panel header. */
function AnalysisSourceBadge({ source }: { source?: string }) {
  const isWatsonx = source === 'Watsonx';
  return (
    <span
      className={`ar-source-badge ${isWatsonx ? 'ar-source-badge--watsonx' : 'ar-source-badge--mock'}`}
      title={isWatsonx
        ? 'Analysis powered by IBM watsonx.ai with live repository data'
        : 'Analysis produced by the deterministic mock service (offline mode)'}
    >
      {isWatsonx ? '⬡ IBM watsonx.ai' : '⬡ Mock analysis'}
    </span>
  );
}

// ── Grouped findings ──────────────────────────────────────────────────────────

const SEVERITY_ORDER: Array<AnalysisRecommendation['severity']> = ['critical', 'high', 'medium', 'low'];

function FindingGroup({
  severity,
  items,
}: {
  severity: AnalysisRecommendation['severity'];
  items: AnalysisRecommendation[];
}) {
  if (items.length === 0) return null;
  const sev = severity === 'critical' ? 'high' : severity;
  return (
    <div className="ar-finding-group">
      <div className="ar-finding-group__header">
        <span className={`rec-severity rec-severity--${sev}`} aria-hidden="true">⬤</span>
        <span className="ar-finding-group__label">
          {severity.charAt(0).toUpperCase() + severity.slice(1)}
        </span>
        <span className="section__count">{items.length}</span>
      </div>
      <ul className="rec-list" role="list">
        {items.map((rec) => (
          <RecommendationItem key={rec.id} rec={rec} />
        ))}
      </ul>
    </div>
  );
}

// ── Safe accessor helpers ─────────────────────────────────────────────────────

/** Safely reads a numeric score, defaulting to 0 when the field is absent. */
function safeScore(score: number | undefined | null): number {
  return typeof score === 'number' ? Math.max(0, Math.min(100, score)) : 0;
}

// ── main component ─────────────────────────────────────────────────────────────

export function AnalysisResultPanel({ result, onReanalyze }: AnalysisResultPanelProps) {
  // Safe destructuring with defaults for malformed / partial API responses
  const repository    = result.repository    ?? { url: '', name: 'Unknown', branch: 'unknown' };
  const scores        = result.scores        ?? { overall: 0, codeQuality: 0, security: 0, performance: 0, testing: 0, documentation: 0 };
  const findings      = result.findings      ?? { critical: 0, high: 0, medium: 0, low: 0 };
  const recommendations = Array.isArray(result.recommendations) ? result.recommendations : [];
  const nextActions   = Array.isArray(result.nextActions)   ? result.nextActions   : [];
  const analysisSource = result.analysisSource;

  const analyzedDate = result.analyzedAt
    ? new Date(result.analyzedAt).toLocaleString('en-US', {
        month: 'short', day: 'numeric', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
      })
    : 'Unknown time';

  const totalFindings =
    (findings.critical ?? 0) + (findings.high ?? 0) + (findings.medium ?? 0) + (findings.low ?? 0);

  // Group findings by severity
  const grouped = SEVERITY_ORDER.reduce<Record<string, AnalysisRecommendation[]>>((acc, sev) => {
    acc[sev] = recommendations.filter((r) => r.severity === sev);
    return acc;
  }, {} as Record<string, AnalysisRecommendation[]>);

  return (
    <div className="ar-panel">
      {/* Header */}
      <div className="ar-header">
        <div>
          <h3 className="ar-repo-name">{repository.name}</h3>
          <p className="ar-meta">
            <code className="branch-tag">{repository.branch}</code>
            <a
              href={repository.url}
              target="_blank"
              rel="noopener noreferrer"
              className="ar-url"
              title={`Open ${repository.url} in a new tab`}
            >
              {repository.url}
            </a>
          </p>
        </div>
        <div className="ar-header-right">
          <AnalysisSourceBadge source={analysisSource} />
          <div className="ar-timestamp">Analyzed {analyzedDate}</div>
          {onReanalyze && (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={onReanalyze}
              aria-label="Re-analyze this repository"
            >
              ↺ Re-analyze
            </button>
          )}
        </div>
      </div>

      {/* Real repository metadata panel */}
      {result.repoMetadata && <RepoMetadataPanel meta={result.repoMetadata} />}

      {/* Scores */}
      <section className="ar-section" aria-labelledby="ar-scores-title">
        <h3 id="ar-scores-title" className="ar-section-title">Health Scores</h3>
        <div className="ar-scores-grid">
          <ScoreRing label="Overall"       score={safeScore(scores.overall)} />
          <ScoreRing label="Code Quality"  score={safeScore(scores.codeQuality)} />
          <ScoreRing label="Security"      score={safeScore(scores.security)} />
          <ScoreRing label="Performance"   score={safeScore(scores.performance)} />
          <ScoreRing label="Testing"       score={safeScore(scores.testing)} />
          <ScoreRing label="Documentation" score={safeScore(scores.documentation)} />
        </div>
      </section>

      {/* Findings summary */}
      <section className="ar-section" aria-labelledby="ar-findings-title">
        <h3 id="ar-findings-title" className="ar-section-title">
          Findings
          <span className="section__count">{totalFindings}</span>
        </h3>
        <div className="ar-findings-row">
          {(findings.critical ?? 0) > 0 && <FindingBadge count={findings.critical} severity="critical" />}
          {(findings.high     ?? 0) > 0 && <FindingBadge count={findings.high}     severity="high" />}
          {(findings.medium   ?? 0) > 0 && <FindingBadge count={findings.medium}   severity="medium" />}
          {(findings.low      ?? 0) > 0 && <FindingBadge count={findings.low}      severity="low" />}
          {totalFindings === 0 && (
            <span className="ar-no-findings">No findings — repository looks healthy!</span>
          )}
        </div>
      </section>

      {/* Findings grouped by severity */}
      {recommendations.length > 0 && (
        <section className="ar-section" aria-labelledby="ar-recs-title">
          <h3 id="ar-recs-title" className="ar-section-title">
            {analysisSource === 'Watsonx' ? 'AI-Powered Findings' : 'Findings'}
            <span className="section__count">{recommendations.length}</span>
          </h3>
          {SEVERITY_ORDER.map((sev) => (
            <FindingGroup key={sev} severity={sev} items={grouped[sev] ?? []} />
          ))}
        </section>
      )}

      {recommendations.length === 0 && (
        <section className="ar-section" aria-labelledby="ar-empty-recs-title">
          <h3 id="ar-empty-recs-title" className="ar-section-title">Findings</h3>
          <div className="ar-empty-state">
            No actionable findings — this repository looks in great shape!
          </div>
        </section>
      )}

      {/* Next Actions */}
      {nextActions.length > 0 && (
        <section className="ar-section" aria-labelledby="ar-actions-title">
          <h3 id="ar-actions-title" className="ar-section-title">Suggested Next Actions</h3>
          <ol className="ar-actions-list">
            {nextActions.map((a) => (
              <li key={a.priority} className="ar-action-item">
                <span className="ar-action-num">{a.priority}</span>
                <div>
                  <strong className="ar-action-title">{a.action}</strong>
                  <p className="ar-action-rationale">{a.rationale}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>
      )}
    </div>
  );
}
