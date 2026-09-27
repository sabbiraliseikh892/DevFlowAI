# DevFlow AI

AI-powered developer workflow assistant built for the IBM Bob 2.0 Hackathon.

## Overview

DevFlow AI helps developers understand and review GitHub repositories using
AI-powered repository analysis and code review.

It combines a React frontend, ASP.NET Core backend, GitHub integration, and
IBM watsonx.ai to identify actionable software issues.

## Features

- GitHub repository analysis
- AI-powered code review
- Security analysis
- Code quality analysis
- Performance analysis
- Maintainability analysis
- Reliability and error-handling analysis
- Severity-based findings
- File and line-level findings
- AI-generated recommendations
- IBM watsonx.ai integration
- React-based developer dashboard

## Technology Stack

### Frontend

- React
- TypeScript
- Vite
- CSS

### Backend

- C#
- ASP.NET Core
- REST API
- GitHub REST API

### AI

- IBM watsonx.ai
- Mistral Small 3.1 24B Instruct

## Architecture

```text
User
  |
  v
React / TypeScript Frontend
  |
  v
ASP.NET Core Backend
  |
  +------> GitHub REST API
  |             |
  |             v
  |       Repository Context
  |
  v
IBM watsonx.ai
  |
  v
Structured AI Findings
  |
  v
Code Review Dashboard
Code Review Workflow
User enters a public GitHub repository URL.
DevFlow AI validates the repository.
The backend retrieves repository metadata and source files.
Relevant repository context is prepared for AI analysis.
IBM watsonx.ai analyzes the supplied code.
Findings are returned in structured JSON.
DevFlow AI displays severity, category, file, line, description,
and recommendation.
Project Structure
DevFlowAI/
│
├── backend/
│   └── ASP.NET Core API
│
├── frontend/
│   └── React + TypeScript + Vite
│
├── bob_sessions/
│   └── Bob IDE task session summaries
│
└── README.md
Configuration

The following environment variables are required by the backend:

IBM_WATSONX_URL
IBM_WATSONX_API_KEY
IBM_WATSONX_PROJECT_ID
IBM_WATSONX_MODEL_ID
AnalysisProvider

Example:

IBM_WATSONX_URL=https://eu-de.ml.cloud.ibm.com
IBM_WATSONX_PROJECT_ID=<your-project-id>
IBM_WATSONX_MODEL_ID=mistralai/mistral-small-3-1-24b-instruct-2503
AnalysisProvider=Watsonx

Do not commit API keys or other secrets to GitHub.

Run Locally
Backend
cd backend
dotnet restore
dotnet build
dotnet run

Backend:

http://127.0.0.1:5032
Frontend
cd frontend
npm install
npm run build
npm run dev

Frontend:

http://localhost:5173
IBM Bob Usage

IBM Bob was used during development of DevFlow AI for implementation,
debugging, frontend development, backend development, code review
functionality, and UI refinement.

Bob IDE task session-summary screenshots are included in:

bob_sessions/
Security
IBM watsonx.ai credentials are kept on the backend.
API keys are supplied through environment variables.
Credentials are not stored in the React frontend.
The application accepts public GitHub repositories for analysis.
Hackathon

Built for the IBM Bob 2.0 Hackathon.

License

This project is provided for hackathon/demo purposes.
```
