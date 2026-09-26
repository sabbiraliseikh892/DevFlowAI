# DevFlow AI

> AI-powered developer workflow assistant — IBM Bob 2.0 Hackathon MVP

---

## What is DevFlow AI?

DevFlow AI is a full-stack web application that brings AI-assisted insights directly into your development workflow. It connects to your GitHub repositories, surfaces pull-request summaries, and provides actionable code-review feedback — all in a single dashboard.

This repository contains the **Phase 1 scaffold**: a running frontend and backend with a health-check integration, ready for feature development.

---

## Technology Stack

| Layer     | Technology                                |
|-----------|-------------------------------------------|
| Frontend  | React 19, TypeScript, Vite 6              |
| Backend   | ASP.NET Core Web API, .NET 8, C#          |
| AI        | IBM watsonx *(planned — Phase 2)*         |
| GitHub    | GitHub REST API *(planned — Phase 2)*     |

---

## High-Level Architecture

```
┌─────────────────────────┐        HTTP / JSON        ┌──────────────────────────┐
│   Frontend (Vite:5173)  │ ◄───────────────────────► │  Backend API (:5031)     │
│   React + TypeScript    │                            │  ASP.NET Core Web API    │
│                         │     GET /api/health        │  Controllers / Services  │
└─────────────────────────┘                            └──────────────────────────┘
```

During development, Vite proxies `/api/*` requests to the backend so both can run
independently without CORS issues from the browser.

---

## Project Structure

```
DevFlowAI/
├── frontend/                  # React + TypeScript + Vite
│   ├── src/
│   │   ├── App.tsx            # Dashboard shell
│   │   └── App.css            # Application styles
│   └── vite.config.ts         # Proxy configuration
│
├── backend/                   # ASP.NET Core Web API
│   ├── Controllers/           # HTTP endpoint handlers
│   ├── Services/              # Business logic
│   ├── DTOs/                  # Data transfer objects
│   ├── Models/                # Domain models (future)
│   └── Program.cs             # Application entry point
│
├── docs/                      # Architecture & design notes
├── bob_sessions/              # Bob AI session artefacts
└── README.md
```

---

## Prerequisites

| Tool        | Minimum version | Install                        |
|-------------|-----------------|--------------------------------|
| Node.js     | 18+             | https://nodejs.org             |
| npm         | 9+              | bundled with Node.js           |
| .NET SDK    | 8.0             | https://dotnet.microsoft.com   |

---

## Running Locally

### 1. Backend

```bash
cd backend
dotnet run
# API is available at http://localhost:5031
# Health check: GET http://localhost:5031/api/health
```

### 2. Frontend

```bash
cd frontend
npm install
npm run dev
# Dashboard is available at http://localhost:5173
```

Open **http://localhost:5173** — the dashboard will call the backend health endpoint
and display **"DevFlow AI API is running"** when both services are up.

---

## Current MVP Features

- ✅ React dashboard shell
- ✅ ASP.NET Core Web API with structured controller/service/DTO layers
- ✅ `GET /api/health` endpoint
- ✅ Frontend → backend connectivity check
- ✅ Vite dev-server proxy (no CORS friction in development)

---

## Planned Features (Phase 2+)

- 🔲 GitHub OAuth & repository browser
- 🔲 Pull-request listing and diff viewer
- 🔲 IBM watsonx AI-powered PR summaries
- 🔲 Automated code-review suggestions
- 🔲 Developer activity dashboard

---

## License

MIT
