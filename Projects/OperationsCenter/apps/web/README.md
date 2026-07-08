# Operations Center Web (Initial Foundation)

This frontend is a minimal React + TypeScript app for validating API authentication and protected incident reads.

## Prerequisites

- Node.js 20+
- Running Operations Center API in Development

## Setup

From repository root:

```bash
cd apps/web
cp .env.example .env
npm install
```

Set backend URL in `.env`:

```bash
VITE_API_BASE_URL=http://localhost:5000
```

Use the API port shown by backend startup logs if it differs.

## Run locally

```bash
npm run dev
```

Open the local Vite URL (typically `http://localhost:5173`).

## Build

```bash
npm run build
```

## Endpoints currently used

- `POST /auth/login`
- `GET /incidents`

## Current scope

Included:

- login page
- session token storage (`sessionStorage`)
- authenticated API client calls with bearer token
- protected incidents route
- loading/empty/error handling for incident list

Not included yet:

- refresh tokens
- incident create/update UI
- SignalR or real-time updates
- audit timeline UI
- dashboards/charts
- notifications
