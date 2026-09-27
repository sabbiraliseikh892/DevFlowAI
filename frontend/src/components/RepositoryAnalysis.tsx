import { useState } from 'react';
import type { AnalysisFormState, AnalysisStatus } from '../types';

interface RepositoryAnalysisProps {
  onAnalyze: (form: AnalysisFormState) => void;
  status: AnalysisStatus;
  errorMessage?: string;
}

/** Heuristic: if the message mentions "backend" or "localhost" it is a network error. */
function isNetworkError(msg?: string): boolean {
  return !!msg && (
    msg.includes('backend') ||
    msg.includes('localhost') ||
    msg.includes('NetworkError') ||
    msg.includes('Failed to fetch')
  );
}

/** Client-side validation: must be a github.com HTTPS URL. */
function validateGitHubUrl(url: string): string | null {
  const trimmed = url.trim();
  if (!trimmed) return 'Repository URL is required.';

  let parsed: URL;
  try {
    parsed = new URL(trimmed);
  } catch {
    return 'Enter a valid URL, e.g. https://github.com/owner/repository';
  }

  if (parsed.protocol !== 'https:') {
    return 'Only HTTPS GitHub URLs are supported.';
  }

  if (!['github.com', 'www.github.com'].includes(parsed.hostname.toLowerCase())) {
    return 'Only public GitHub repositories are supported (github.com).';
  }

  const parts = parsed.pathname.replace(/^\//, '').replace(/\/$/, '').split('/');
  if (parts.length < 2 || !parts[0] || !parts[1]) {
    return 'The URL must include an owner and a repository name: https://github.com/owner/repository';
  }

  return null;
}

export function RepositoryAnalysis({ onAnalyze, status, errorMessage }: RepositoryAnalysisProps) {
  const [form, setForm] = useState<AnalysisFormState>({ repoUrl: '', branch: 'main' });
  const [clientError, setClientError] = useState<string | null>(null);
  const isLoading = status === 'loading';
  const networkError = status === 'error' && isNetworkError(errorMessage);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const validationError = validateGitHubUrl(form.repoUrl);
    if (validationError) {
      setClientError(validationError);
      return;
    }

    setClientError(null);
    onAnalyze(form);
  }

  function handleUrlChange(value: string) {
    setForm((f) => ({ ...f, repoUrl: value }));
    // Clear client-side error as user types
    if (clientError) setClientError(null);
  }

  const hasUrlError = !!clientError;
  const showServerError = status === 'error';

  return (
    <section className="section" aria-labelledby="repo-analysis-title">
      <h2 id="repo-analysis-title" className="section__title">Repository Analysis</h2>
      <p className="section__subtitle">
        Enter a public GitHub repository URL to run an AI-powered code quality, security and
        performance analysis.
      </p>

      <form className="repo-form" onSubmit={handleSubmit} noValidate>
        <div className="repo-form__fields">
          <div className={`form-group${hasUrlError ? ' form-group--error' : ''}`}>
            <label htmlFor="repo-url" className="form-label">
              GitHub Repository URL
            </label>
            <input
              id="repo-url"
              type="url"
              className={`form-input${hasUrlError ? ' form-input--error' : ''}`}
              placeholder="https://github.com/owner/repository"
              value={form.repoUrl}
              onChange={(e) => handleUrlChange(e.target.value)}
              disabled={isLoading}
              required
              aria-describedby={hasUrlError ? 'repo-url-error' : undefined}
              aria-invalid={hasUrlError}
            />
            {hasUrlError && (
              <p id="repo-url-error" className="form-error" role="alert">
                {clientError}
              </p>
            )}
          </div>

          <div className="form-group form-group--short">
            <label htmlFor="branch" className="form-label">Branch</label>
            <input
              id="branch"
              type="text"
              className="form-input"
              placeholder="main"
              value={form.branch}
              onChange={(e) => setForm((f) => ({ ...f, branch: e.target.value }))}
              disabled={isLoading}
            />
          </div>
        </div>

        <button
          type="submit"
          className="btn btn--primary"
          disabled={isLoading || !form.repoUrl.trim()}
        >
          {isLoading ? (
            <>
              <span className="spinner" aria-hidden="true" />
              Analyzing…
            </>
          ) : (
            'Analyze Repository'
          )}
        </button>
      </form>

      {showServerError && networkError && (
        <div className="alert alert--error alert--network" role="alert" aria-live="assertive">
          <strong className="alert__heading">⚠ Backend unavailable</strong>
          <p className="alert__body">{errorMessage}</p>
          <p className="alert__hint">
            Start the backend with <code>cd backend &amp;&amp; dotnet run</code>, then try again.
          </p>
        </div>
      )}

      {showServerError && !networkError && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          <strong>Analysis failed:</strong>{' '}
          {errorMessage ?? 'An unexpected error occurred. Please try again.'}
        </div>
      )}

      {status === 'loading' && (
        <div className="alert alert--info" role="status" aria-live="polite">
          <span className="spinner" aria-hidden="true" /> Fetching repository data from GitHub and running analysis…
        </div>
      )}

      {status === 'success' && (
        <div className="alert alert--success" role="status">
          Analysis complete. Results are shown below.
        </div>
      )}
    </section>
  );
}
