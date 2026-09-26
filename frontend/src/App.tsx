import { useEffect, useState } from 'react'
import './App.css'

interface HealthStatus {
  status: string
  message: string
  timestamp: string
}

function App() {
  const [health, setHealth] = useState<HealthStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetch('/api/health')
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        return res.json() as Promise<HealthStatus>
      })
      .then((data) => {
        setHealth(data)
        setLoading(false)
      })
      .catch((err: Error) => {
        setError(err.message)
        setLoading(false)
      })
  }, [])

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>DevFlow AI</h1>
        <p className="subtitle">AI-powered developer workflow assistant</p>
      </header>

      <main className="dashboard-main">
        <section className="card">
          <h2>API Status</h2>
          {loading && <p className="status loading">Connecting…</p>}
          {error && <p className="status error">⚠ Connection failed: {error}</p>}
          {health && (
            <p className="status ok">
              ✓ {health.message}
            </p>
          )}
        </section>

        <section className="card placeholder">
          <h2>GitHub Integration</h2>
          <p>Coming soon — connect your repositories and pull requests.</p>
        </section>

        <section className="card placeholder">
          <h2>AI Insights</h2>
          <p>Coming soon — automated code review and PR summaries.</p>
        </section>
      </main>
    </div>
  )
}

export default App
