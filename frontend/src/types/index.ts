// ── Existing Phase 1/2 types ──────────────────────────────────────────────────

export type MetricStatus = 'excellent' | 'good' | 'warning' | 'critical';

export interface SummaryMetric {
  id: string;
  label: string;
  value: string;
  score: number; // 0–100
  status: MetricStatus;
  trend: 'up' | 'down' | 'stable';
  trendLabel: string;
}

export interface Recommendation {
  id: string;
  severity: 'high' | 'medium' | 'low';
  category: string;
  title: string;
  description: string;
  file?: string;
  line?: number;
}

export interface AnalysisRecord {
  id: string;
  repository: string;
  branch: string;
  analyzedAt: string;
  overallScore: number;
  status: 'completed' | 'in-progress' | 'failed';
  metrics: {
    codeQuality: number;
    security: number;
    performance: number;
    technicalDebt: number;
  };
}

export interface AnalysisFormState {
  repoUrl: string;
  branch: string;
}

export type AnalysisStatus = 'idle' | 'loading' | 'success' | 'error';

// ── Phase 3: Repository Analysis API types ────────────────────────────────────

export interface AnalysisScores {
  overall: number;
  codeQuality: number;
  security: number;
  performance: number;
  testing: number;
  documentation: number;
}

export interface AnalysisFindings {
  critical: number;
  high: number;
  medium: number;
  low: number;
}

export interface AnalysisRecommendation {
  id: string;
  severity: 'critical' | 'high' | 'medium' | 'low';
  category: string;
  title: string;
  /** Why this matters. */
  explanation: string;
  /** Concrete evidence from the repository, or "Evidence unavailable". */
  evidence: string;
  /** Specific action the developer should take. */
  recommendedAction: string;
  /** Affected file or path, null/undefined when not applicable. */
  affectedPath?: string | null;
  line?: number | null;
}

export interface AnalysisNextAction {
  priority: number;
  action: string;
  rationale: string;
}

export interface RepositoryInfo {
  url: string;
  name: string;
  branch: string;
}

// ── Phase 6: Real repository metadata from GitHub ────────────────────────────

/** Present when the backend successfully inspected the repository on GitHub. */
export interface RepoMetadata {
  description?: string;
  primaryLanguage?: string;
  languageCounts: Record<string, number>;
  totalFileCount: number;
  sampledFileCount: number;
  hasReadme: boolean;
  hasTests: boolean;
  hasCiConfig: boolean;
  dependencyFiles: string[];
  configFiles: string[];
  topLevelDirectories: string[];
}

export interface AnalysisResult {
  repository: RepositoryInfo;
  scores: AnalysisScores;
  findings: AnalysisFindings;
  recommendations: AnalysisRecommendation[];
  nextActions: AnalysisNextAction[];
  analyzedAt: string;
  /** "Watsonx" when powered by IBM watsonx.ai; "Mock" when using the deterministic fallback. */
  analysisSource?: string;
  /** Null when GitHub inspection was skipped or failed (Mock mode). */
  repoMetadata?: RepoMetadata | null;
}
