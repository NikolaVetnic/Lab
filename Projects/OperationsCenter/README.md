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
│ │ ├── AGENTS.md
│ │ ├── src/
│ │ └── tests/
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

## Backend vil senere kunne startes fra

cd apps/api
dotnet run --project src/OperationsCenter.Api

## Agentinstruksjoner

Prosjektet inneholder instruksjoner for AI-agenter og GitHub Copilot:

- AGENTS.md inneholder prosjektets overordnede utviklingsregler.
- .github/copilot-instructions.md inneholder generelle Copilot-regler.
- .github/instructions/ inneholder teknologispesifikke regler.
- Lokale AGENTS.md-filer under apps/api og apps/web inneholder regler for henholdsvis backend og frontend.

Disse filene skal leses før større endringer gjøres i prosjektet.

## Arkitekturbeslutninger

Viktige og langvarige tekniske beslutninger dokumenteres som Architecture Decision Records i: `docs/adr/`

## Status

Under aktiv utvikling.
