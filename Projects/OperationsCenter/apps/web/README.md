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

## Routes currently available

- /login
- /incidents
- /incidents/new
- /incidents/:id

## Endpoints currently used

- `POST /auth/login`
- `GET /incidents`
- `POST /incidents`
- `GET /incidents/{id}`
- `PATCH /incidents/{id}/status`
- `GET /incidents/{id}/audit`

## Current scope

Included:

- login page
- session token storage (`sessionStorage`)
- authenticated API client calls with bearer token
- protected incidents routes (list, create, details)
- loading/empty/error handling for incident list
- create incident form with basic client-side validation
- incident details page with not-found/auth/error states
- incident details status updates with backend-enforced transitions
- incident details audit timeline with status-change metadata rendering
- SignalR live updates for incident create/status changes
- non-blocking live connection status indicator

Not included yet:

- refresh tokens
- dashboards/charts
- notifications
