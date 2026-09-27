import { useState } from "react";
import "./App.css";

import { Header } from "./components/Header";
import { SummaryCard } from "./components/SummaryCard";
import { RepositoryAnalysis } from "./components/RepositoryAnalysis";
import { AIRecommendations } from "./components/AIRecommendations";
import { RecentAnalysis } from "./components/RecentAnalysis";
import { AnalysisResultPanel } from "./components/AnalysisResultPanel";
import { CodeReview } from "./components/CodeReview";
import { Settings } from "./components/Settings";

import {
  summaryMetrics,
  recommendations,
  recentAnalyses,
} from "./data/mockData";

import {
  analyzeRepository,
  AnalysisNetworkError,
} from "./services/analysisService";

import type {
  AnalysisFormState,
  AnalysisStatus,
  AnalysisResult,
} from "./types";

type NavItem = "dashboard" | "repository-analysis" | "code-review" | "settings";

function App() {
  const [activeNav, setActiveNav] = useState<NavItem>("dashboard");

  const [analysisStatus, setAnalysisStatus] = useState<AnalysisStatus>("idle");

  const [analysisError, setAnalysisError] = useState<string | undefined>(
    undefined,
  );

  const [analysisResult, setAnalysisResult] = useState<
    AnalysisResult | undefined
  >(undefined);

  async function handleAnalyze(form: AnalysisFormState) {
    setAnalysisStatus("loading");
    setAnalysisError(undefined);
    setAnalysisResult(undefined);

    try {
      const result = await analyzeRepository({
        repositoryUrl: form.repoUrl,
        branch: form.branch,
      });

      setAnalysisResult(result);
      setAnalysisStatus("success");
    } catch (err) {
      let message: string;

      if (err instanceof AnalysisNetworkError) {
        message = err.message;
      } else if (err instanceof Error) {
        message = err.message;
      } else {
        message = "An unexpected error occurred.";
      }

      setAnalysisError(message);
      setAnalysisStatus("error");
    }
  }

  return (
    <div className="app">
      {/* Header / Navigation */}
      <Header activeNav={activeNav} onNavChange={setActiveNav} />

      <main className="app-main">
        {/* =====================================================
            CODE REVIEW
           ===================================================== */}
        {activeNav === "code-review" ? (
          <CodeReview />
        ) : activeNav === "settings" ? (
          /* ===================================================
             SETTINGS
             =================================================== */
          <Settings />
        ) : (
          /* ===================================================
             DASHBOARD / REPOSITORY ANALYSIS
             =================================================== */
          <>
            {/* Repository Analysis */}
            <RepositoryAnalysis
              onAnalyze={handleAnalyze}
              status={analysisStatus}
              errorMessage={analysisError}
            />

            {/* Live Analysis Results */}
            {analysisStatus === "success" && analysisResult && (
              <section className="section" aria-labelledby="ar-results-title">
                <h2 id="ar-results-title" className="section__title">
                  Analysis Results
                </h2>

                <AnalysisResultPanel
                  result={analysisResult}
                  onReanalyze={() =>
                    handleAnalyze({
                      repoUrl: analysisResult.repository.url,
                      branch: analysisResult.repository.branch,
                    })
                  }
                />
              </section>
            )}

            {/* Overview / Summary Cards */}
            <section className="section" aria-labelledby="summary-title">
              <h2 id="summary-title" className="section__title">
                Overview
              </h2>

              <div className="summary-grid">
                {summaryMetrics.map((metric) => (
                  <SummaryCard key={metric.id} metric={metric} />
                ))}
              </div>
            </section>

            {/* AI Recommendations */}
            <AIRecommendations recommendations={recommendations} />

            {/* Recent Analysis */}
            <RecentAnalysis records={recentAnalyses} />
          </>
        )}
      </main>

      {/* Footer */}
      <footer className="app-footer">
        <span>DevFlow AI &copy; {new Date().getFullYear()}</span>
      </footer>
    </div>
  );
}

export default App;
