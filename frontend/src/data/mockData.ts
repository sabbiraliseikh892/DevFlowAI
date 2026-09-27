import type { SummaryMetric, Recommendation, AnalysisRecord } from '../types';

export const summaryMetrics: SummaryMetric[] = [
  {
    id: 'code-quality',
    label: 'Code Quality',
    value: '87 / 100',
    score: 87,
    status: 'good',
    trend: 'up',
    trendLabel: '+3 since last scan',
  },
  {
    id: 'security',
    label: 'Security',
    value: '72 / 100',
    score: 72,
    status: 'warning',
    trend: 'stable',
    trendLabel: 'No change',
  },
  {
    id: 'performance',
    label: 'Performance',
    value: '94 / 100',
    score: 94,
    status: 'excellent',
    trend: 'up',
    trendLabel: '+6 since last scan',
  },
  {
    id: 'technical-debt',
    label: 'Technical Debt',
    value: '4.2 days',
    score: 68,
    status: 'warning',
    trend: 'down',
    trendLabel: '−0.8 days improved',
  },
];

export const recommendations: Recommendation[] = [
  {
    id: 'rec-1',
    severity: 'high',
    category: 'Security',
    title: 'Hardcoded API key detected',
    description:
      'A plaintext API key was found in the source tree. Move it to an environment variable or a secrets manager immediately.',
    file: 'src/services/apiClient.ts',
    line: 14,
  },
  {
    id: 'rec-2',
    severity: 'high',
    category: 'Security',
    title: 'Dependency with known CVE',
    description:
      'Package "axios@0.21.1" has a known server-side request forgery vulnerability (CVE-2021-3749). Upgrade to ≥ 0.21.4.',
    file: 'package.json',
  },
  {
    id: 'rec-3',
    severity: 'medium',
    category: 'Code Quality',
    title: 'Unused exported function',
    description:
      'Function `formatDateLegacy` is exported but never imported anywhere in the project. Consider removing it to reduce bundle size.',
    file: 'src/utils/dateHelpers.ts',
    line: 42,
  },
  {
    id: 'rec-4',
    severity: 'medium',
    category: 'Performance',
    title: 'Missing memoisation on expensive computation',
    description:
      'The `processReportData` function is called on every render without memoisation. Wrap it with `useMemo` to avoid redundant work.',
    file: 'src/components/ReportTable.tsx',
    line: 88,
  },
  {
    id: 'rec-5',
    severity: 'low',
    category: 'Technical Debt',
    title: 'TODO comment older than 60 days',
    description:
      'There are 7 TODO comments that have not been resolved in over 60 days. Review and either complete them or open tracked issues.',
    file: 'src/pages/Dashboard.tsx',
    line: 5,
  },
];

export const recentAnalyses: AnalysisRecord[] = [
  {
    id: 'ana-1',
    repository: 'acme-corp/frontend-app',
    branch: 'main',
    analyzedAt: '2025-07-20T09:14:00Z',
    overallScore: 84,
    status: 'completed',
    metrics: { codeQuality: 87, security: 72, performance: 94, technicalDebt: 68 },
  },
  {
    id: 'ana-2',
    repository: 'acme-corp/api-gateway',
    branch: 'develop',
    analyzedAt: '2025-07-19T17:30:00Z',
    overallScore: 76,
    status: 'completed',
    metrics: { codeQuality: 79, security: 65, performance: 88, technicalDebt: 70 },
  },
  {
    id: 'ana-3',
    repository: 'acme-corp/mobile-client',
    branch: 'feature/auth-refresh',
    analyzedAt: '2025-07-19T11:05:00Z',
    overallScore: 61,
    status: 'completed',
    metrics: { codeQuality: 63, security: 55, performance: 71, technicalDebt: 54 },
  },
  {
    id: 'ana-4',
    repository: 'acme-corp/data-pipeline',
    branch: 'main',
    analyzedAt: '2025-07-18T08:00:00Z',
    overallScore: 0,
    status: 'failed',
    metrics: { codeQuality: 0, security: 0, performance: 0, technicalDebt: 0 },
  },
];
