# Kubernetes manifests (local/demo)

Raw Kubernetes manifests for running the full Operations Center stack — Postgres,
API, web frontend, and the OpenTelemetry Collector/Prometheus/Grafana/Seq/Tempo
observability stack — in a local Kubernetes cluster. This is the step **before**
Helm: everything here is plain YAML, applied directly with `kubectl`.

Not included yet, deliberately: Helm, Kustomize, Terraform, cloud load balancers,
production TLS, an externally managed Postgres, RabbitMQ, Redis, autoscaling,
NetworkPolicies, the Prometheus Operator, Argo CD/Flux. See "Known limitations"
below and CLAUDE.md.

## Prerequisites

- A local Kubernetes cluster (see "Supported environments" below) and `kubectl`
  pointed at it (`kubectl config current-context`).
- Docker (or the container engine your local Kubernetes distribution uses) to build
  the `operations-center-api:local` and `operations-center-web:local` images.
- A default `StorageClass` available in the cluster for the PersistentVolumeClaims
  (Docker Desktop, Rancher Desktop, kind, and minikube all provide one out of the box).

## Supported environments

Tested against, in order of how this was written and verified:

- **Docker Desktop** (built-in Kubernetes) — images built with `docker build` are
  already visible to its Kubernetes cluster; no extra load step.
- **Rancher Desktop** (containerd or moby, your choice in settings) — if using the
  containerd engine, images must be imported with `nerdctl` (see below); if using
  the moby/dockerd engine, they're visible directly like Docker Desktop.
- **kind** — images must be loaded into the cluster with `kind load docker-image`.
- **minikube** — either build inside minikube's own Docker daemon
  (`eval $(minikube docker-env)`) or load with `minikube image load`.

## Building images for local Kubernetes

Build both images the same way the Docker Compose stack does:

```bash
docker compose build operations-center-api operations-center-web
```

This produces `operations-center-api:local` and `operations-center-web:local`. Then,
depending on your cluster:

```bash
# kind
kind load docker-image operations-center-api:local operations-center-web:local

# minikube (if you didn't build inside minikube's Docker daemon)
minikube image load operations-center-api:local
minikube image load operations-center-web:local

# Rancher Desktop on containerd
docker save operations-center-api:local | nerdctl -n k8s.io load
docker save operations-center-web:local | nerdctl -n k8s.io load

# Docker Desktop / Rancher Desktop on moby
# Nothing to do — the cluster already sees images in the local Docker image store.
```

All Deployments/Jobs use `imagePullPolicy: IfNotPresent` and no registry, so the
cluster never tries to pull these images from anywhere else.

## Kubernetes Service DNS vs. Docker Compose service names

Several images have hostnames baked in at build time or in mounted config that were
written for Docker Compose's DNS (`operations-center-api`, `operations-center-seq`,
`operations-center-tempo`, `operations-center-prometheus`,
`operations-center-otel-collector`):

- the web image's `nginx.conf` proxies `/api/` and `/hubs/` to
  `http://operations-center-api:8080`;
- `otel-collector-config.yml` exports logs to `operations-center-seq` and traces to
  `operations-center-tempo:4317`;
- `prometheus.yml` scrapes `operations-center-otel-collector:8889`;
- Grafana's datasource provisioning points at `operations-center-prometheus:9090` and
  `operations-center-tempo:3200`.

Rather than rebuilding the web image or rewriting these config files, every
Kubernetes **Service** in this directory is deliberately named identically to its
Docker Compose counterpart. Kubernetes' own cluster DNS (CoreDNS) resolves
`<service-name>` to the Service's ClusterIP within the same namespace — so all of the
above config works unchanged. This is genuine Kubernetes service DNS, not a
Compose-specific shortcut; the names just happen to match on purpose.

## Applying the manifests

```bash
kubectl apply -f infra/k8s/namespace.yaml

# Copy and edit the example secrets first (see "Secrets" below), then:
kubectl apply -f infra/k8s/secrets/secrets.local.yaml

kubectl apply -R -f infra/k8s/config
kubectl apply -R -f infra/k8s/postgres
kubectl apply -R -f infra/k8s/observability

# Wait for Postgres to be Ready before migrating:
kubectl wait -n operations-center --for=condition=Ready pod -l app=operations-center-postgres --timeout=120s

kubectl apply -f infra/k8s/api/api-migrations-job.yaml
kubectl wait -n operations-center --for=condition=Complete job/operations-center-api-migrate --timeout=120s

kubectl apply -f infra/k8s/api/api-deployment.yaml
kubectl apply -f infra/k8s/api/api-service.yaml
kubectl apply -R -f infra/k8s/web

# Optional, only if your cluster has an Ingress controller:
kubectl apply -f infra/k8s/ingress/ingress.yaml
```

## Secrets

`infra/k8s/secrets/secrets.example.yaml` holds the Postgres password, JWT signing
key, Grafana admin password, and dev-seed user passwords — all the **same dev-only
values already in `.env.example`** at the repo root, never real credentials.

```bash
cp infra/k8s/secrets/secrets.example.yaml infra/k8s/secrets/secrets.local.yaml
# edit secrets.local.yaml with your own values if you want something other than the
# checked-in demo defaults
kubectl apply -f infra/k8s/secrets/secrets.local.yaml
```

`secrets.local.yaml` is git-ignored — never commit it. The manifests use
`stringData` (plain text), which `kubectl`/the API server base64-encodes on apply, so
there's nothing to pre-encode by hand. If you'd rather commit pre-encoded `data:`
values instead, generate them with:

```bash
echo -n 'your-value' | base64
```

## Running migrations

`infra/k8s/api/api-migrations-job.yaml` runs the API image in `--migrate` mode
(`dotnet OperationsCenter.Api.dll --migrate`), which only calls
`dbContext.Database.MigrateAsync()` and exits — no `EnsureCreated()`, no dev-data
seeding, no destructive schema changes. Run it once after Postgres is ready and
before relying on the API being fully functional:

```bash
kubectl apply -f infra/k8s/api/api-migrations-job.yaml
kubectl wait -n operations-center --for=condition=Complete job/operations-center-api-migrate --timeout=120s
```

Safe to re-run against an already-migrated database (EF Core migrations are
idempotent), but a completed Job's spec is immutable, so re-applying the same file
fails with a "field is immutable" error unless you delete the old Job first:

```bash
kubectl delete job -n operations-center operations-center-api-migrate --ignore-not-found
kubectl apply -f infra/k8s/api/api-migrations-job.yaml
```

Only this one Job runs migrations — the API Deployment does **not** run
`--migrate` on startup, so scaling API replicas never triggers concurrent migration
attempts.

### Optional: demo data

`infra/k8s/api/api-seed-job.yaml` is **not applied by default**. It runs the API
image in `--seed` mode (migrates, then seeds idempotent demo users —
`admin@/operator@/viewer@operations-center.local` — and demo incidents), using the
passwords from `dev-seed-secret` in the secrets manifest:

```bash
kubectl apply -f infra/k8s/api/api-seed-job.yaml
kubectl wait -n operations-center --for=condition=Complete job/operations-center-api-seed --timeout=120s
```

## Accessing everything

Every Service is `ClusterIP` — nothing is exposed outside the cluster by default.
Two ways in:

### Port-forward (always works, no extra setup)

```bash
kubectl port-forward -n operations-center svc/operations-center-web 8080:80
kubectl port-forward -n operations-center svc/operations-center-api 5000:8080
kubectl port-forward -n operations-center svc/operations-center-grafana 3000:3000
kubectl port-forward -n operations-center svc/operations-center-seq 5341:80
kubectl port-forward -n operations-center svc/operations-center-prometheus 9090:9090
```

Then open:

| What | URL |
| --- | --- |
| Frontend | <http://localhost:8080> |
| API health | <http://localhost:5000/health>, <http://localhost:5000/ready> |
| Swagger / OpenAPI | <http://localhost:5000/swagger>, <http://localhost:5000/openapi/v1.json> |
| Grafana | <http://localhost:3000> (admin / value from `grafana-secret`) |
| Seq | <http://localhost:5341> |
| Prometheus | <http://localhost:9090> |
| Tempo | through Grafana → Explore → Tempo datasource (already provisioned) |

### Ingress (optional)

If your cluster already has an Ingress controller (Rancher Desktop ships Traefik;
kind/minikube typically need `ingress-nginx` installed separately), apply
`infra/k8s/ingress/ingress.yaml` and add to `/etc/hosts`:

```
127.0.0.1 operations-center.local api.operations-center.local grafana.operations-center.local seq.operations-center.local prometheus.operations-center.local
```

(Use your cluster's actual ingress address instead of `127.0.0.1` for kind/minikube —
check with `kubectl get ingress -n operations-center`.) Then the same table above
applies, minus the port numbers, using these hostnames. Swagger/API/SignalR routing
through the frontend host works exactly as in Docker Compose, since the Ingress
routes the whole `operations-center.local` host to the web Service, and the web
image's own `nginx.conf` still does the `/api` and `/hubs` reverse-proxying
internally — no Ingress-controller-specific rewrite rules or WebSocket annotations
are needed.

## Verifying the deployment

```bash
kubectl get pods -n operations-center
kubectl get svc -n operations-center
kubectl get ingress -n operations-center

kubectl logs -n operations-center deployment/operations-center-api
kubectl logs -n operations-center deployment/operations-center-web
kubectl logs -n operations-center job/operations-center-api-migrate
kubectl logs -n operations-center deployment/operations-center-otel-collector
```

Manual checklist once pods are `Running`/`Ready`:

- [ ] Postgres pod `Ready` (`pg_isready` probe passing)
- [ ] `operations-center-api-migrate` Job `Complete`
- [ ] API pod `Ready` (`/ready` returns 200)
- [ ] frontend pod `Ready`, loads at `/`
- [ ] login works (via seeded demo users, or your own if you skipped seeding)
- [ ] incident list loads, creating an incident works, status updates work
- [ ] SignalR live updates work (single API replica — see limitations)
- [ ] Grafana opens, "Operations Center Overview" dashboard shows data
- [ ] Seq opens, receives structured logs
- [ ] Prometheus **Status → Targets** shows the `otel-collector` target as `UP`
- [ ] Tempo receives traces (visible via Grafana Explore → Tempo)

## Deleting everything

```bash
kubectl delete namespace operations-center
```

Deletes every resource in this directory at once, including the PVCs (and their
data). There is no resource outside the `operations-center` namespace.

## Known limitations

- **Single API replica only.** SignalR connections are held in-memory by one API
  pod; there is no backplane (Redis or otherwise) for multiple replicas yet. Don't
  scale `operations-center-api` past 1 without adding one first.
- **No NetworkPolicies.** Every pod in the namespace can reach every other pod.
- **PVCs use the cluster's default StorageClass** with no explicit provisioner
  tuning — fine for Docker/Rancher Desktop, kind, and minikube; not meant for
  anything beyond local/demo use.
- **Secrets are demo values checked into git** (`secrets.example.yaml`). Never put
  real credentials in a file tracked by git; use `secrets.local.yaml` (git-ignored)
  or a proper secrets manager for anything beyond local development.
- **No Ingress controller is installed by these manifests** — `ingress.yaml` assumes
  one already exists in the cluster (or you fall back to port-forward).
- **No TLS anywhere.** Local HTTP only.
- **Production Postgres should be managed externally** (a managed service, or a
  carefully configured/backed-up cluster) — the Deployment+PVC here is a local/demo
  convenience only, with no backup, replication, or failover.
- **Swagger/OpenAPI requires `ASPNETCORE_ENVIRONMENT=Development`** (set in
  `app-config`), matching how it already behaves in Docker Compose — the API only
  maps those endpoints in Development.

## Recommended next step

Turn these working manifests into a Helm chart: template the image tags,
replica counts, resource requests/limits, and the Ingress hostnames as chart values,
so the same stack can be installed with different overrides per environment without
copy-pasting YAML.
