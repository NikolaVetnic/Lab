# Operations Center

[![CI](https://github.com/NikolaVetnic/Lab/actions/workflows/ci.yml/badge.svg)](https://github.com/NikolaVetnic/Lab/actions/workflows/ci.yml)

Operations Center er et porteføljeprosjekt for en moderne plattform som hjelper organisasjoner med å håndtere hendelser, oppgaver, varsler og sanntidsinformasjon.

Prosjektet bygges som en modulær monolitt først, med tydelige modulgrenser som gjør det mulig å trekke ut separate mikrotjenester senere når det er et reelt behov.

## Mål

Prosjektet skal demonstrere erfaring med:

- .NET og C#
- REST API-er og SignalR
- PostgreSQL og EF Core
- JWT-autentisering og rollebasert autorisasjon
- Bakgrunnsjobber og event-driven kommunikasjon
- Docker og Docker Compose
- GitHub Actions
- Observability med OpenTelemetry, Prometheus og Grafana
- Kubernetes, Helm og Terraform
- Produksjonsrettet sikkerhet, logging og revisjonsspor

## Continuous Integration

CI-workflowen kjører på push til `main` og pull requests mot `main` når endringer treffer dette prosjektets filer (`apps/api/operations-center/**`, `apps/web/**`, `NuGet.config` eller selve workflow-filen), samt manuelt via `workflow_dispatch`.

Den validerer:

- backend-formattering, build og tester
- frontend-formattering, linting og produksjonsbuild
- Docker-build av API- og frontend-images uten publisering

Pull requests skal passere CI før de merges.

## Planlagt funksjonalitet

Operations Center vil etter hvert inneholde støtte for:

- Registrering og behandling av incidents
- Oppgaver knyttet til incidents
- Sanntidsdashboard
- Varslinger via ulike kanaler
- Vedlegg og dokumenthåndtering
- Revisjonsspor
- Søk og filtrering
- Brukere, roller og tilgangsstyring

## Arkitektur

Prosjektet starter som en modulær monolitt.

Backend-moduler skal ha tydelige ansvarsområder og ikke aksessere hverandres databasetabeller direkte. Kommunikasjon mellom moduler skal skje gjennom eksplisitte kontrakter og events.

Eksempler på planlagte moduler:

- Identity
- Incidents
- Tasks
- Notifications
- Files
- Audit
- Dashboard
- Search

Når en modul har et tydelig selvstendig ansvar og reelt behov for separat deploy og skalering, kan den trekkes ut til en egen tjeneste under services/.

## Repository-struktur

.
├── AGENTS.md
├── README.md
├── .github/
│ ├── copilot-instructions.md
│ ├── instructions/
│ └── workflows/
├── apps/
│ ├── api/
│ │ ├── operations-center/
│ │ │ ├── AGENTS.md
│ │ │ ├── src/
│ │ │ │ ├── BuildingBlocks/
│ │ │ │ └── OperationsCenter/
│ │ │ └── tests/
│ │ │ └── OperationsCenter/
│ └── web/
│ └── AGENTS.md
├── docs/
│ └── adr/
├── infra/
└── services/

## Teknologistack

Foreløpig plan:

| Område           | Teknologi                            |
| ---------------- | ------------------------------------ |
| Backend          | NET / C#                             |
| Frontend         | React + TypeScript                   |
| Database         | PostgreSQL                           |
| ORM              | Entity Framework Core                |
| Autentisering    | JWT                                  |
| Sanntid          | SignalR                              |
| Meldingskø       | RabbitMQ, senere                     |
| Cache            | Redis, senere                        |
| Containerisering | Docker og Docker Compose             |
| CI /CD           | GitHub Actions                       |
| Observability    | OpenTelemetry, Prometheus og Grafana |
| Infrastruktur    | Kubernetes, Helm og Terraform        |

## Nåværende fase

Prosjektet er i oppstartsfasen.

Første mål er å etablere en minimal backend-løsning med:

- Health endpoint
- Swagger / OpenAPI
- Global feilhåndtering med ProblemDetails
- Unit- og integration-testprosjekter
- Tydelig prosjektstruktur og avhengighetsretning

Databaser, autentisering, Docker, frontend, SignalR og meldingskøer introduseres gradvis etter at grunnmuren fungerer.

## Lokalt oppsett

Krav:

- Docker Desktop eller tilsvarende for containerisert kjøring
- .NET SDK 10 for lokal backend-kjøring uten containere
- Node.js 20+ for lokal frontend-kjøring uten containere

## Containerisert lokal miljø

Hele løsningen kan startes lokalt med én kommando.

Opprett lokal miljøfil:

```bash
cp .env.example .env
```

Start full stack:

```bash
docker compose up --build
```

Kjør i bakgrunnen:

```bash
docker compose up --build -d
```

Stopp containere:

```bash
docker compose down
```

Stopp og fjern databasen volum bevisst:

```bash
docker compose down -v
```

Bygg images på nytt uten å starte:

```bash
docker compose build
```

Vis containerstatus:

```bash
docker compose ps
```

Vis logger:

```bash
docker compose logs -f operations-center-api
docker compose logs -f operations-center-web
docker compose logs -f operations-center-migrations
```

Kjør en enkel Compose smoke test:

```bash
npm run smoke:compose
```

Smoke-testen bygger og starter stacken, verifiserer health/readiness, sjekker at migreringscontaineren fullfører, logger inn med seed-brukeren via frontend-originet, leser incident-listen, oppretter en incident, oppdaterer status, verifiserer audit-loggen, tester SignalR negotiate via `/hubs/operations`, og bekrefter at SPA deep-link fallback fungerer.

For å beholde containerne oppe etter testen ved feilsøking:

```bash
SMOKE_KEEP_STACK=1 npm run smoke:compose
```

Eksponerte lokale adresser i Compose-oppsettet:

- Frontend: `http://localhost:8080`
- API health: `http://localhost:5000/health`
- API readiness: `http://localhost:5000/ready`
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/openapi/v1.json`
- PostgreSQL: `localhost:5432`

Compose starter disse tjenestene:

- `operations-center-postgres`
- `operations-center-migrations`
- `operations-center-api`
- `operations-center-web`

Oppstartsrekkefølge:

1. PostgreSQL blir healthy.
2. `operations-center-migrations` kjører eksisterende Development seed-modus, som først anvender EF Core-migrasjoner og deretter seed-er demo-brukere og incidents idempotent.
3. API-et starter etter at seed/migrasjonstjenesten er ferdig.
4. Frontenden starter etter at API-et er healthy.

Browser-trafikk går mot frontend-originet på `http://localhost:8080`. Nginx i web-containeren reverse proxy-er `/api/*` og `/hubs/*` til API-containeren, inkludert WebSocket-oppgraderinger for SignalR. Dette gjør at frontend kan bruke relative URL-er i containerisert kjøring uten å bake inn `localhost`-avhengigheter i produksjonsbygget.

Compose-oppsettet er kun for lokal utvikling og porteføljedemo. Secrets ligger utenfor images og source control: Dockerfiles inneholder ingen connection strings eller nøkler, og Compose leser lokale verdier fra `.env`.

Standard seed-brukere i Compose:

- `admin@operations-center.local`
- `operator@operations-center.local`
- `viewer@operations-center.local`

Passord styres av `DEV_SEED_ADMIN_PASSWORD`, `DEV_SEED_OPERATOR_PASSWORD` og `DEV_SEED_VIEWER_PASSWORD` i `.env`.

## Lokal kjøring uten containere

Connection string-navn som brukes av API-et:

- `ConnectionStrings:OperationsCenterDatabase`

Eksempel på miljøvariabler for lokal kjøring ligger i `.env.example`.

Start kun PostgreSQL i Docker:

```bash
docker compose up -d operations-center-postgres
```

Start API lokalt med migrasjoner:

```bash
./scripts/start-api.sh
```

Start frontend og API lokalt:

```bash
./scripts/start-web.sh
```

Direktekommandoer er fortsatt tilgjengelige:

```bash
cd apps/api/operations-center
dotnet run --project src/OperationsCenter/OperationsCenter.Api

cd apps/web
npm run dev
```

Når API-et kjører lokalt i Development, er standard HTTP-port `http://localhost:5000` i `launchSettings.json`.

## Identity, JWT og roller

API-et har en minimal Identity-modul med JWT-basert autentisering og rollebasert autorisasjon.

Login-endpoint:

- `POST /auth/login`

Ved gyldig login returneres et bearer token som brukes i `Authorization: Bearer <token>`.

Incidents- og audit-endpoints krever autentisering:

- `Incidents.Read`: Admin, Operator, Viewer
- `Incidents.Write`: Admin, Operator

OpenAPI (`/openapi/v1.json`) inkluderer bearer security scheme og markerer autoriserte endpoints.

## Demo seed data for Incidents (Development only)

Incident-modulen har en utviklings-seeder med realistiske demo-hendelser for lokal testing og manuell utforskning av API-flyt (create/list/get/status).

Seed-data kjøres kun eksplisitt og kun i Development. Dette forhindrer utilsiktede dataendringer i andre miljøer.

Kjør seed-script fra repository-roten:

```bash
./scripts/seed-dev-data.sh
```

Scriptet:

- starter lokal PostgreSQL-container (`operations-center-postgres`) om nødvendig
- venter til databasen er klar
- kjører API i eksplisitt seed-modus: `dotnet run --project src/OperationsCenter.Api -- --seed`

Det samme seed-løpet brukes av `operations-center-migrations` i Docker Compose for å gjøre en ren lokal database innloggingsklar og demo-klar uten ekstra manuelle steg.

I seed-modus opprettes også idempotente utviklingsbrukere:

- `admin@operations-center.local` (Admin)
- `operator@operations-center.local` (Operator)
- `viewer@operations-center.local` (Viewer)

Passord kan overstyres med miljøvariabler:

- `DEV_SEED_ADMIN_PASSWORD`
- `DEV_SEED_OPERATOR_PASSWORD`
- `DEV_SEED_VIEWER_PASSWORD`

Viktig for tester:

- Integration-tester må opprette egne testdata
- tester skal ikke være avhengige av seedede incidents

## Agentinstruksjoner

Prosjektet inneholder instruksjoner for AI-agenter og GitHub Copilot:

- AGENTS.md inneholder prosjektets overordnede utviklingsregler.
- .github/copilot-instructions.md inneholder generelle Copilot-regler.
- .github/instructions/ inneholder teknologispesifikke regler.
- Lokale AGENTS.md-filer under apps/api og apps/web inneholder regler for henholdsvis backend og frontend.

Disse filene skal leses før større endringer gjøres i prosjektet.

## Arkitekturbeslutninger

Viktige og langvarige tekniske beslutninger dokumenteres som Architecture Decision Records i: `docs/adr/`

Første dokumenterte ADR:

- `0001-internal-mediator-cqrs.md` beskriver intern CQRS mediator-implementasjon uten ekstern MediatR-avhengighet.

Backend-kode er nå gruppert slik:

- `apps/api/operations-center/src/OperationsCenter/` for applikasjonsprosjekter
- `apps/api/operations-center/src/BuildingBlocks/` for delte interne building blocks
- `apps/api/operations-center/tests/OperationsCenter/` for testprosjekter

## Status

Under aktiv utvikling.
