# DevFlow AI

## Overview

AI-powered developer workflow assistant for analyzing GitHub repositories,
performing AI code reviews, and identifying actionable software issues.

## Problem

Developers spend significant time reviewing unfamiliar codebases,
identifying quality issues, understanding repositories, and maintaining code.

## Solution

DevFlow AI combines GitHub repository analysis with IBM watsonx.ai
to provide automated repository insights and AI-powered code review.

## Features

- GitHub repository analysis
- AI-powered code review
- Security issue detection
- Code quality analysis
- Performance issue detection
- Maintainability analysis
- Reliability and error-handling analysis
- Finding severity classification
- File and line-level findings
- AI recommendations
- IBM watsonx.ai integration

## Technology Stack

### Frontend

- React
- TypeScript
- Vite
- CSS

### Backend

- ASP.NET Core
- C#
- REST API
- GitHub REST API

### AI

- IBM watsonx.ai
- Mistral Small 3.1 24B Instruct

## Architecture

User
↓
React Frontend
↓
ASP.NET Core API
↓
GitHub API
↓
Repository Context
↓
IBM watsonx.ai
↓
Structured Code Review
↓
React Findings Dashboard

## Code Review Workflow

1. User enters a public GitHub repository.
2. DevFlow AI validates the repository URL.
3. Backend retrieves repository metadata and source files.
4. Relevant files are provided to IBM watsonx.ai.
5. AI analyzes security, quality, performance,
   maintainability and reliability.
6. AI returns structured findings.
7. DevFlow AI displays severity, file, line,
   description and recommendation.

## Configuration

IBM watsonx.ai credentials are supplied through environment variables.

Required variables:

IBM_WATSONX_URL
IBM_WATSONX_API_KEY
IBM_WATSONX_PROJECT_ID
IBM_WATSONX_MODEL_ID
AnalysisProvider

## Run Locally

### Backend

cd backend
dotnet restore
dotnet run

Backend:
http://127.0.0.1:5032

### Frontend

cd frontend
npm install
npm run dev

Frontend:
http://localhost:5173

## IBM Bob Usage

IBM Bob was used during development to accelerate the implementation
and iteration of DevFlow AI, including repository analysis,
frontend development, backend API implementation, debugging,
code review functionality and project refinement.

Task session-summary screenshots are available in:

bob_sessions/

## Security

API credentials are not stored in the frontend.
Sensitive configuration is provided through environment variables.

## Hackathon

Built for the IBM Bob 2.0 Hackathon.
