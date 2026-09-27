// frontend/src/services/codeReviewService.ts

export interface CodeReviewRequest {
  repositoryUrl: string;
  branch: string;
}

export interface CodeReviewFinding {
  severity: "HIGH" | "MEDIUM" | "LOW" | string;
  category: string;
  title: string;
  file: string;
  line: number;
  description: string;
  recommendation: string;
}

export interface CodeReviewResponse {
  repository: string;
  branch: string;
  source: string;
  findings: CodeReviewFinding[];
  review: string;
}

const API_BASE_URL = "http://127.0.0.1:5032";

export async function runCodeReview(
  request: CodeReviewRequest,
): Promise<CodeReviewResponse> {
  const response = await fetch(`${API_BASE_URL}/api/code-review/review`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  const data = await response.json();

  if (!response.ok) {
    throw new Error(data?.message || data?.detail || "Code review failed.");
  }

  return data;
}
