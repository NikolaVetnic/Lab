# Operations Center

[![Operations Center CI](https://github.com/NikolaVetnic/Lab/actions/workflows/operations-center-ci.yml/badge.svg)](https://github.com/NikolaVetnic/Lab/actions/workflows/operations-center-ci.yml)

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
- Prometheus UI: `http://localhost:9090`
- Collector Prometheus-metrikker: `http://localhost:8889/metrics`

Compose starter disse tjenestene:

- `operations-center-postgres`
- `operations-center-migrations`
- `operations-center-api`
- `operations-center-web`
- `operations-center-otel-collector`
- `operations-center-prometheus`

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

## Observability (OpenTelemetry)

Backend-et er instrumentert med OpenTelemetry som leverandørnøytral standard for traces, metrics og logg-korrelasjon. Telemetri eksporteres via OTLP mot en OpenTelemetry Collector som introduseres senere. Applikasjonskoden er ikke koblet mot Grafana, Prometheus, Jaeger, Application Insights eller andre leverandører.

### Hva instrumenteres

Automatisk instrumentering:

- innkommende HTTP-forespørsler (ASP.NET Core) — health-rutene `/health` og `/ready` er ekskludert fra traces for å redusere støy
- utgående HTTP-kall (`HttpClient`)
- PostgreSQL-kommandoer via Npgsql sin `ActivitySource` (parametriserte SQL-setninger uten parameterverdier eller connection strings)
- .NET runtime-metrikker (GC, threads, med mer)
- ASP.NET Core- og HttpClient-metrikker

Prosess-metrikker (`OpenTelemetry.Instrumentation.Process`) er utelatt inntil videre fordi kun en prerelease-pakke finnes, og prosjektet bruker bevisst ingen preview-avhengigheter.

Egendefinerte aktiviteter (én delt `ActivitySource` med navn `OperationsCenter`):

- `incident.create` — tagger: `incident.id`, `incident.severity`
- `incident.status_change` — tagger: `incident.id`, `incident.previous_status`, `incident.new_status`

Egendefinerte metrikker (én delt `Meter` med navn `OperationsCenter`):

- `operations_center.incidents.created` — teller, økes først etter at en incident er persistert; tag `severity`
- `operations_center.incidents.status_changes` — teller, økes først etter en vellykket statusovergang; tagger `previous_status` og `new_status`

Metrikk-tagger holdes bevisst lav-kardinale. Incident-ID-er, bruker-ID-er og e-poster brukes aldri som metrikk-labels.

### Logging

Eksisterende `ILogger<T>`-basert strukturert logging beholdes uendret. Når telemetri er aktivert legges OpenTelemetry sin logg-provider til slik at logger som skapes innenfor et aktivt trace får trace- og span-korrelasjon. Konsoll-provideren beholdes, og OpenTelemetry-loggene eksporteres kun via OTLP, så det oppstår ingen duplisert konsoll-utskrift. Sensitiv informasjon (tokens, passord, Authorization-headere, connection strings) logges aldri.

### Konfigurasjon

Telemetri styres av `OpenTelemetry`-seksjonen og kan overstyres med miljøvariabler:

```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "ServiceName": "operations-center-api",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://localhost:4317"
  }
}
```

Miljøvariabel-ekvivalenter:

```bash
OpenTelemetry__Enabled=true
OpenTelemetry__ServiceName=operations-center-api
OpenTelemetry__OtlpEndpoint=http://otel-collector:4317
```

Resource-attributter som settes: `service.name`, `service.version` og `deployment.environment` (avledet fra ASP.NET Core-miljøet).

### Aktiver eller deaktiver lokalt

- I Docker Compose og `.env.example` er telemetri **aktivert** som standard (`OpenTelemetry__Enabled=true`) og pushes til Collector-tjenesten over OTLP.
- For lokal kjøring uten containere er eksport som standard **deaktivert** i `appsettings.json` (`OpenTelemetry__Enabled=false`); sett `OpenTelemetry__Enabled=true` og pek `OpenTelemetry__OtlpEndpoint` mot en kjørende Collector for å aktivere eksport.
- API-et starter og betjener trafikk normalt uansett om telemetri er av eller på.
- En utilgjengelig Collector gjør **ikke** API-et eller health-rutene usunne; eksportfeil logges og forkastes uten å påvirke forespørsler.

### Observability-infrastruktur (Collector + Prometheus)

Docker Compose kjører nå den første observability-infrastrukturen:

- `operations-center-otel-collector` — OpenTelemetry Collector (contrib-image). Tar imot OTLP over gRPC (`4317`) og HTTP (`4318`), batcher, og eksponerer metrikker på et Prometheus-scrape-endepunkt (`8889`). Traces og logger sendes foreløpig kun til Collectorens debug-utskrift (ingen lagringsbackend enda).
- `operations-center-prometheus` — Prometheus, scraper Collectorens metrikk-endepunkt og eksponerer UI på `9090`.

Telemetriflyt:

```
OperationsCenter.Api
    ↓ OTLP gRPC :4317
operations-center-otel-collector
    ↓ Prometheus-eksportør :8889
operations-center-prometheus (UI :9090)
```

Prometheus scraper Collectoren, ikke API-et direkte: .NET-API-et pusher metrikker via OTLP og eksponerer ikke selv et Prometheus-`/metrics`-endepunkt. Collectoren konverterer mottatte OTLP-metrikker til et scrape-bart endepunkt, slik at applikasjonskoden forblir leverandørnøytral uten en Prometheus-eksportør.

Konfigurasjonsfiler:

- Collector: `infra/observability/otel-collector-config.yml`
- Prometheus: `infra/observability/prometheus.yml`

Eksponerte lokale adresser:

- Prometheus UI: `http://localhost:9090`
- Collector Prometheus-metrikker: `http://localhost:8889/metrics`
- OTLP gRPC: `localhost:4317`
- OTLP HTTP: `localhost:4318`

Start stacken:

```bash
docker compose up --build
docker compose ps
docker compose logs -f operations-center-otel-collector
docker compose logs -f operations-center-prometheus
```

Verifiser at Prometheus scraper Collectoren:

1. Start stacken (`docker compose up --build`).
2. Bruk appen eller kall API-endepunkter (f.eks. login og list/opprett incidents) slik at det genereres trafikk og metrikker.
3. Åpne Prometheus på `http://localhost:9090`.
4. Gå til `Status → Targets` og bekreft at `otel-collector`-målet er `UP`.
5. Kjør en spørring på en kjent metrikk som strømmer fra API-et, for eksempel den innebygde ASP.NET Core-metrikken `http_server_request_duration_seconds_count` eller en .NET runtime-metrikk som `dotnet_gc_collections_total`.

De automatiske metrikkene (ASP.NET Core, HttpClient, .NET runtime) er verifisert ende-til-ende: API → Collector → Prometheus.

Merk: OpenTelemetry-metrikknavn normaliseres av Prometheus-eksportøren (punktum blir understrek, tellere får `_total`-suffiks). Applikasjonens egendefinerte metrikker bruker `operations_center_*`-navngivning (for eksempel `operations_center_incidents_created_total`). Instrumenteringen for disse ligger i backend fra forrige steg; å få de egendefinerte metrikkene og traces til å strømme helt frem til Collectoren gjenstår som en oppfølging på applikasjonssiden.

### Ikke inkludert enda

Grafana og Seq er bevisst **ikke** lagt til i dette steget, og heller ikke Tempo, Loki, Jaeger, dashboards eller alerts. Traces og logger mottas av Collectoren men lagres ikke i en backend enda. Neste steg er Grafana-dashboards oppå Prometheus.

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
- `0002-opentelemetry-otlp-observability.md` beskriver bruk av OpenTelemetry med OTLP-eksport for leverandørnøytral observability.

Backend-kode er nå gruppert slik:

- `apps/api/operations-center/src/OperationsCenter/` for applikasjonsprosjekter
- `apps/api/operations-center/src/BuildingBlocks/` for delte interne building blocks
- `apps/api/operations-center/tests/OperationsCenter/` for testprosjekter

## Status

Under aktiv utvikling.
