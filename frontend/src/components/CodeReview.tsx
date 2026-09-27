// frontend/src/components/CodeReview.tsx

import { useState } from "react";
import {
  runCodeReview,
  type CodeReviewFinding,
  type CodeReviewResponse,
} from "../services/codeReviewService";

export function CodeReview() {
  const [repositoryUrl, setRepositoryUrl] = useState("");
  const [branch, setBranch] = useState("main");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<CodeReviewResponse | null>(null);
  const [selectedFinding, setSelectedFinding] =
    useState<CodeReviewFinding | null>(null);

  async function handleReview() {
    if (!repositoryUrl.trim()) {
      setError("Please enter a public GitHub repository URL.");
      return;
    }

    setLoading(true);
    setError("");
    setResult(null);
    setSelectedFinding(null);

    try {
      const response = await runCodeReview({
        repositoryUrl: repositoryUrl.trim(),
        branch: branch.trim() || "main",
      });

      setResult(response);

      if (response.findings.length > 0) {
        setSelectedFinding(response.findings[0]);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Code review failed.");
    } finally {
      setLoading(false);
    }
  }

  const highCount =
    result?.findings.filter((x) => x.severity.toUpperCase() === "HIGH")
      .length ?? 0;

  const mediumCount =
    result?.findings.filter((x) => x.severity.toUpperCase() === "MEDIUM")
      .length ?? 0;

  const lowCount =
    result?.findings.filter((x) => x.severity.toUpperCase() === "LOW").length ??
    0;

  return (
    <section className="code-review-page">
      {/* HEADER */}
      <div className="code-review-header">
        <div>
          <div className="code-review-eyebrow">DEVFLOW AI</div>

          <h1 className="code-review-title">AI Code Review</h1>

          <p className="code-review-subtitle">
            Analyze any public GitHub repository with IBM watsonx.ai and
            discover actionable code-quality, security and performance issues.
          </p>
        </div>

        <div className="code-review-ai-badge">
          <span className="ai-status-dot" />
          IBM watsonx.ai
        </div>
      </div>

      {/* INPUT CARD */}
      <div className="code-review-input-card">
        <div className="input-card-heading">
          <div>
            <h2>Repository Analysis</h2>
            <p>
              Enter a public GitHub repository to start an AI-powered code
              review.
            </p>
          </div>
        </div>

        <div className="code-review-form">
          <div className="code-review-field repository-field">
            <label>GitHub Repository URL</label>

            <div className="input-with-icon">
              <span className="input-icon">◉</span>

              <input
                type="url"
                value={repositoryUrl}
                onChange={(e) => setRepositoryUrl(e.target.value)}
                placeholder="https://github.com/owner/repository"
                disabled={loading}
              />
            </div>
          </div>

          <div className="code-review-field branch-field">
            <label>Branch</label>

            <input
              type="text"
              value={branch}
              onChange={(e) => setBranch(e.target.value)}
              placeholder="main"
              disabled={loading}
            />
          </div>

          <button
            className="code-review-button"
            type="button"
            onClick={handleReview}
            disabled={loading}
          >
            {loading ? (
              <>
                <span className="review-spinner" />
                Analyzing...
              </>
            ) : (
              <>
                <span>✦</span>
                Start AI Review
              </>
            )}
          </button>
        </div>

        <div className="review-input-hint">
          <span>🔒</span>
          Your repository credentials are never stored. Public GitHub
          repositories only.
        </div>
      </div>

      {/* ERROR */}
      {error && (
        <div className="code-review-error">
          <span>!</span>
          <div>
            <strong>Review failed</strong>
            <p>{error}</p>
          </div>
        </div>
      )}

      {/* LOADING */}
      {loading && (
        <div className="code-review-loading">
          <div className="loading-animation">
            <span />
            <span />
            <span />
          </div>

          <h3>Watsonx.ai is reviewing your repository</h3>

          <p>
            Fetching repository files and analyzing security, quality and
            maintainability.
          </p>
        </div>
      )}

      {/* RESULTS */}
      {result && !loading && (
        <div className="code-review-results">
          {/* RESULT HEADER */}
          <div className="results-header">
            <div>
              <div className="results-label">REVIEW COMPLETE</div>

              <h2>{result.repository}</h2>

              <div className="repository-meta">
                <span>⎇ {result.branch}</span>

                <span className="meta-divider">•</span>

                <span>✦ {result.source}</span>
              </div>
            </div>

            <div className="review-complete">
              <span className="complete-check">✓</span>
              Complete
            </div>
          </div>

          {/* METRICS */}
          <div className="review-metrics">
            <div className="review-metric">
              <div className="metric-icon total">#</div>

              <div>
                <span>Total Findings</span>
                <strong>{result.findings.length}</strong>
              </div>
            </div>

            <div className="review-metric">
              <div className="metric-icon high">!</div>

              <div>
                <span>High Severity</span>
                <strong>{highCount}</strong>
              </div>
            </div>

            <div className="review-metric">
              <div className="metric-icon medium">!</div>

              <div>
                <span>Medium Severity</span>
                <strong>{mediumCount}</strong>
              </div>
            </div>

            <div className="review-metric">
              <div className="metric-icon low">↓</div>

              <div>
                <span>Low Severity</span>
                <strong>{lowCount}</strong>
              </div>
            </div>
          </div>

          {/* FINDINGS */}
          {result.findings.length === 0 ? (
            <div className="no-findings-card">
              <div className="no-findings-icon">✓</div>

              <h3>No structured findings returned</h3>

              <p>
                Watsonx.ai completed the review, but no structured findings were
                returned.
              </p>
            </div>
          ) : (
            <div className="findings-layout">
              {/* LEFT */}
              <div className="findings-list-panel">
                <div className="panel-heading">
                  <div>
                    <h3>Code Findings</h3>
                    <p>AI-detected issues requiring attention</p>
                  </div>

                  <span className="findings-count">
                    {result.findings.length}
                  </span>
                </div>

                <div className="findings-list">
                  {result.findings.map((finding, index) => {
                    const severity = finding.severity.toUpperCase();

                    const active = selectedFinding === finding;

                    return (
                      <button
                        key={`${finding.file}-${finding.title}-${index}`}
                        type="button"
                        className={`finding-item ${
                          active ? "finding-item-active" : ""
                        }`}
                        onClick={() => setSelectedFinding(finding)}
                      >
                        <div className="finding-item-top">
                          <span
                            className={`severity-badge severity-${severity.toLowerCase()}`}
                          >
                            {severity}
                          </span>

                          <span className="finding-category">
                            {finding.category}
                          </span>
                        </div>

                        <h4>{finding.title}</h4>

                        <div className="finding-file">
                          <span>▣</span>
                          {finding.file}

                          {finding.line > 0 && (
                            <>
                              <span>:</span>
                              {finding.line}
                            </>
                          )}
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* RIGHT */}
              {selectedFinding && (
                <div className="finding-details-panel">
                  <div className="details-heading">
                    <div>
                      <span className="details-label">FINDING DETAILS</span>

                      <h3>{selectedFinding.title}</h3>
                    </div>

                    <span
                      className={`severity-badge severity-${selectedFinding.severity.toLowerCase()}`}
                    >
                      {selectedFinding.severity.toUpperCase()}
                    </span>
                  </div>

                  <div className="details-file">
                    <span>▣</span>

                    <span>{selectedFinding.file}</span>

                    {selectedFinding.line > 0 && (
                      <span>:{selectedFinding.line}</span>
                    )}
                  </div>

                  <div className="details-section">
                    <h4>Description</h4>

                    <p>{selectedFinding.description}</p>
                  </div>

                  <div className="details-section recommendation-section">
                    <div className="recommendation-title">
                      <span>✦</span>
                      AI Recommendation
                    </div>

                    <p>{selectedFinding.recommendation}</p>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* RAW RESPONSE */}
          <details className="raw-response">
            <summary>
              <span>View raw Watsonx.ai response</span>

              <span>+</span>
            </summary>

            <pre>{result.review}</pre>
          </details>
        </div>
      )}
    </section>
  );
}
