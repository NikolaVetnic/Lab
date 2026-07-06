# Operations Center

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

- .NET SDK
- Node.js, når frontend opprettes
- Docker Desktop eller tilsvarende, når PostgreSQL og øvrige lokale tjenester introduseres

## Lokal PostgreSQL for backend

PostgreSQL for lokal utvikling ligger i docker-compose-fil på repository-roten.

Start database:

```bash
docker compose up -d
```

Stopp database:

```bash
docker compose down
```

Connection string-navn som brukes av API-et:

- `ConnectionStrings:OperationsCenterDatabase`

Standard lokal konfigurasjon er satt i `appsettings.Development.json` for API-prosjektet.

Eksempel på miljøvariabler for lokal kjøring ligger i `.env.example`.

For nåværende løsning under `apps/api/operations-center`:

```bash
cd apps/api/operations-center
dotnet run --project src/OperationsCenter/OperationsCenter.Api
```

Når API-et kjører i Development, er dokumentasjon tilgjengelig på:

- Swagger UI: `https://localhost:7xxx/swagger`
- OpenAPI JSON: `https://localhost:7xxx/openapi/v1.json`

Merk: Eksakt port vises i oppstart-logger og i `launchSettings.json` for API-prosjektet.

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
