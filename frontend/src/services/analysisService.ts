import type { AnalysisResult } from "../types";

const API_BASE = "/api";

export interface AnalyzeRequest {
  repositoryUrl: string;
  branch?: string;
}

/**
 * Thrown when the backend is reachable but returns a non-2xx response.
 * The `status` property carries the HTTP status code for callers that
 * want to render different messages for 400 vs 500 etc.
 */
export class AnalysisApiError extends Error {
  readonly status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = "AnalysisApiError";
    this.status = status;
  }
}

/**
 * Thrown when the fetch itself fails (backend unreachable, CORS, network down).
 */
export class AnalysisNetworkError extends Error {
  constructor(cause?: unknown) {
    super(
      "Cannot reach the analysis backend. " +
        "Make sure the backend is running on http://127.0.0.1:5032.",
    );
    this.name = "AnalysisNetworkError";
    if (cause instanceof Error) this.cause = cause;
  }
}

/**
 * POST /api/analysis/analyze
 * Submits a repository URL for AI-style analysis.
 * Throws {@link AnalysisNetworkError} if the backend is unreachable, or
 * {@link AnalysisApiError} if the server returns a non-2xx response.
 */
export async function analyzeRepository(
  req: AnalyzeRequest,
): Promise<AnalysisResult> {
  let response: Response;

  try {
    response = await fetch(`${API_BASE}/analysis/analyze`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        repositoryUrl: req.repositoryUrl,
        branch: req.branch ?? "main",
      }),
    });
  } catch (err) {
    throw new AnalysisNetworkError(err);
  }

  if (!response.ok) {
    let detail = `HTTP ${response.status}`;
    try {
      const problem = await response.json();
      if (problem?.detail) detail = problem.detail;
      else if (problem?.title) detail = problem.title;
    } catch {
      // ignore JSON parse failures on the error body
    }
    throw new AnalysisApiError(detail, response.status);
  }

  return response.json() as Promise<AnalysisResult>;
}
