export function Settings() {
  return (
    <section className="section" aria-labelledby="settings-title">
      <div className="code-review-header">
        <div>
          <h2 id="settings-title" className="section__title">
            Settings
          </h2>

          <p className="code-review-subtitle">
            Configure and review the AI analysis environment used by DevFlow AI.
          </p>
        </div>
      </div>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "20px",
        }}
      >
        {/* AI Provider */}
        <div className="review-details">
          <h3>AI Provider</h3>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Provider</span>

            <h3 style={{ marginTop: "6px" }}>IBM watsonx.ai</h3>
          </div>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Status</span>

            <p
              style={{
                marginTop: "8px",
                color: "#22c55e",
                fontWeight: 600,
              }}
            >
              ● Connected
            </p>
          </div>
        </div>

        {/* Model */}
        <div className="review-details">
          <h3>AI Model</h3>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Model</span>

            <p
              style={{
                marginTop: "8px",
                wordBreak: "break-word",
              }}
            >
              mistralai/mistral-small-3-1-24b-instruct-2503
            </p>
          </div>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Region</span>

            <p style={{ marginTop: "8px" }}>Frankfurt (eu-de)</p>
          </div>
        </div>

        {/* Project */}
        <div className="review-details">
          <h3>IBM Cloud Project</h3>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Project</span>

            <p style={{ marginTop: "8px" }}>DevFlow AI</p>
          </div>

          <div style={{ marginTop: "18px" }}>
            <span className="review-category">Analysis Engine</span>

            <p style={{ marginTop: "8px" }}>AI-powered repository analysis</p>
          </div>
        </div>

        {/* Analysis */}
        <div className="review-details">
          <h3>Analysis Features</h3>

          <div style={{ marginTop: "18px" }}>
            <p>✓ Code Quality Analysis</p>
            <p>✓ Security Analysis</p>
            <p>✓ Performance Analysis</p>
            <p>✓ Maintainability Analysis</p>
            <p>✓ AI Recommendations</p>
          </div>
        </div>
      </div>

      {/* Security notice */}
      <div
        style={{
          marginTop: "24px",
          padding: "18px",
          borderRadius: "10px",
          border: "1px solid rgba(99, 102, 241, 0.25)",
          background: "rgba(99, 102, 241, 0.08)",
        }}
      >
        <h3>Security</h3>

        <p style={{ marginTop: "8px" }}>
          IBM Cloud credentials are supplied through environment variables and
          are not stored in the frontend application.
        </p>
      </div>
    </section>
  );
}
