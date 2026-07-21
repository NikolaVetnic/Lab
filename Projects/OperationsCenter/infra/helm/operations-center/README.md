# operations-center Helm chart

A Helm chart for the same local/demo stack as [`infra/k8s/`](../../k8s/README.md)
— Postgres, the API, the web frontend, and the OpenTelemetry
Collector/Prometheus/Grafana/Seq/Tempo observability stack — packaged so it's
configurable and repeatable instead of hand-edited YAML. This is **not**
production-hardened; see "Known limitations" below.

## How this relates to `infra/k8s/`

`infra/k8s/` is left in place, unchanged — this chart doesn't replace it, it's
built from it. Every raw manifest maps to one template here (same file names,
same per-component directories), parameterized through `values.yaml` instead
of hardcoded. If you just want to see the stack running once without
thinking about Helm, `infra/k8s/` is still the simpler path. If you want to
install it more than once, with different settings, or eventually promote
it toward a real release process, use this chart.

## Prerequisites

- Everything `infra/k8s/README.md` requires: a local Kubernetes cluster
  (Docker Desktop, Rancher Desktop, kind, or minikube), `kubectl`, and a
  container engine to build the two images.
- `helm` v3+ (Rancher Desktop bundles it under `~/.rd/bin`; Docker Desktop
  and the other distros don't — install separately if needed).

## Building local images

Identical to `infra/k8s/`:

```bash
docker compose build operations-center-api operations-center-web
```

Then load the images into your cluster if your distro needs it (kind,
minikube, Rancher Desktop on containerd) — see `infra/k8s/README.md`'s
"Building images for local Kubernetes" table; nothing about that changes
with Helm.

## Installing the chart

```bash
helm upgrade --install operations-center infra/helm/operations-center \
  --namespace operations-center \
  --create-namespace \
  --values infra/helm/operations-center/values.local.yaml
```

Using `helm upgrade --install` (not `helm install`) from the start means the
exact same command works for both the first install and every update after
— nothing to remember differently.

**Install with release name `operations-center`, as shown above.** The
chart's `fullname` helper collapses to just the release name when it already
contains the chart name, which is what makes every generated Service name
identical to `infra/k8s/`'s (`operations-center-api`, `operations-center-postgres`,
...) — including the one that matters most: the web image's `nginx.conf` has
`operations-center-api` baked in at build time, and only resolves correctly
under Kubernetes DNS if the API Service is actually named that. A different
release name still installs and runs, but breaks that one baked-in proxy
target unless you also pass `--set fullnameOverride=operations-center`. See
"Known limitations".

### Namespace

The chart does **not** template a `Namespace` by default (`namespace.create`
is `false`) — use `--create-namespace` as shown above, which is Helm's own
mechanism for this and doesn't tie the namespace's lifecycle to the release.
If you specifically want the namespace deleted along with everything else on
`helm uninstall`, set `namespace.create: true` and drop `--create-namespace`
— but that also means a second, unrelated release can't share the namespace
without a manual `kubectl delete namespace` first, so most people should
leave this off.

## Upgrading

Same command as installing:

```bash
helm upgrade --install operations-center infra/helm/operations-center \
  --namespace operations-center \
  --values infra/helm/operations-center/values.local.yaml
```

Change values, re-run, done. `helm history operations-center -n
operations-center` shows every revision if you need to roll back
(`helm rollback operations-center <revision> -n operations-center`).

## Uninstalling

```bash
helm uninstall operations-center --namespace operations-center
```

Removes every resource this release created — **including the PVCs and
their data** (Postgres, Grafana, Seq, Tempo, Prometheus). If you left
`namespace.create` at its default `false`, the namespace itself survives;
delete it separately if you want it gone too:

```bash
kubectl delete namespace operations-center
```

## Secrets

Three sensitive values — the Postgres password, the JWT signing key, and the
Grafana admin password — are templated from values by `templates/secret.example.yaml`
(a fourth, the dev-seed passwords, only renders if `seedJob.enabled` is true).

`values.yaml` ships every one of them as an obviously-fake `CHANGE_ME`
placeholder. `values.local.yaml` carries the **same dev-only defaults already
checked into `.env.example`** and `infra/k8s/secrets/secrets.example.yaml` —
never real credentials, and that's why the install command above always
passes `--values .../values.local.yaml` explicitly rather than baking those
values into `values.yaml` itself.

For anything beyond local development, either:

1. Copy `values.local.yaml` (e.g. to `values.mine.yaml`, which is
   git-ignored) and put your own values in the copy, or
2. Set the matching `existingSecret` field instead (`postgres.auth.existingSecret`,
   `api.jwt.existingSecret`, `observability.grafana.admin.existingSecret`,
   `seedJob.seedPasswords.existingSecret`) to point at a Secret you created
   and manage separately — the chart then skips templating its own Secret
   for that concern entirely. Each expects a fixed key name (`POSTGRES_PASSWORD`,
   `Jwt__SigningKey`, `GF_SECURITY_ADMIN_PASSWORD`, and the three
   `DEV_SEED_*_PASSWORD` keys, respectively) — match those exactly.

Never put real secret values in this README or in `values.yaml`.

## Migrations

`templates/api/migration-job.yaml` runs the API image's `--migrate` mode
(migrates only — no `EnsureCreated()`, no dev-data seeding) as a **Helm hook**:

```yaml
helm.sh/hook: pre-install,pre-upgrade
helm.sh/hook-delete-policy: before-hook-creation,hook-succeeded
```

This means migrations run automatically on every `helm upgrade --install` —
no separate manual step, unlike `infra/k8s/`'s plain Job. Two problems come
with that convenience, both addressed directly:

- **A Job's Pod template is immutable**, so a plain Job would fail with a
  "field is immutable" error the moment its image tag changes on a second
  upgrade. `before-hook-creation` has Helm delete the previous run's Job
  right before creating the new one, every time — this is the actual reason
  a hook is used here instead of a plain templated Job.
- **Hooks run before this release's other templates are guaranteed ready**
  — on a first install, Postgres might not have a healthy Pod behind its
  Service yet when the hook fires. The Job's `initContainers` includes a
  small `wait-for-postgres` step that loops `pg_isready` until Postgres
  answers, before the actual migration container runs.

`hook-succeeded` (not `hook-failed`) is in the delete policy on purpose: a
**failed** migration Job is left in place so you can `kubectl logs`/`describe`
it — it only gets cleaned up by `before-hook-creation` on your next attempt.
Check its result explicitly:

```bash
kubectl get jobs -n operations-center -l app.kubernetes.io/component=migration
kubectl logs -n operations-center job/operations-center-api-migrate
```

Set `migration.enabled: false` to skip migrations entirely on a given
install/upgrade (e.g. if you know the schema hasn't changed and want a
faster iteration loop) — the API will not work against an unmigrated
database, so only do this once you've confirmed the schema is already
current.

### Optional: demo data

`seedJob.enabled` (default `false`) adds a second hook Job running `--seed`
instead of `--migrate` (migrates, then seeds idempotent demo users —
`admin@/operator@/viewer@operations-center.local` — and demo incidents),
using the same hook strategy at a higher `hook-weight` so it runs after the
migration hook if both happen to be enabled together (harmless either way,
since `--seed` migrates internally too):

```bash
helm upgrade --install operations-center infra/helm/operations-center \
  --namespace operations-center --create-namespace \
  --values infra/helm/operations-center/values.local.yaml \
  --set seedJob.enabled=true
```

## Accessing everything

Every Service is `ClusterIP` — nothing is exposed outside the cluster unless
you enable Ingress. `helm install`/`upgrade` prints the exact port-forward
commands for whatever you enabled (see `templates/NOTES.txt`), or run them
yourself:

```bash
kubectl port-forward -n operations-center svc/operations-center-web 8080:80
kubectl port-forward -n operations-center svc/operations-center-api 5000:8080
kubectl port-forward -n operations-center svc/operations-center-grafana 3000:3000
kubectl port-forward -n operations-center svc/operations-center-seq 5341:80
kubectl port-forward -n operations-center svc/operations-center-prometheus 9090:9090
```

| What | URL |
| --- | --- |
| Frontend | <http://localhost:8080> |
| API health | <http://localhost:5000/health>, <http://localhost:5000/ready> |
| Swagger / OpenAPI | <http://localhost:5000/swagger>, <http://localhost:5000/openapi/v1.json> |
| Grafana | <http://localhost:3000> |
| Seq | <http://localhost:5341> |
| Prometheus | <http://localhost:9090> |
| Tempo | through Grafana → Explore → Tempo datasource |

### Ingress (optional)

Disabled by default (`ingress.enabled: false`). If your cluster already has
an Ingress controller (Rancher Desktop ships Traefik; kind/minikube need one
installed separately):

```bash
helm upgrade --install operations-center infra/helm/operations-center \
  --namespace operations-center --create-namespace \
  --values infra/helm/operations-center/values.local.yaml \
  --set ingress.enabled=true
```

Same host-only routing as `infra/k8s/ingress/ingress.yaml`: no path
rewriting, so no controller-specific annotations are needed for `/hubs`
(SignalR) — the web pod's own `nginx.conf` still does that reverse-proxying
internally. `ingress.className` is left empty by default to use the
cluster's default IngressClass; set it explicitly if your cluster has more
than one or none marked default. Add to `/etc/hosts`:

```
127.0.0.1 operations-center.local api.operations-center.local grafana.operations-center.local seq.operations-center.local
```

(the Prometheus host is disabled by default — set `ingress.prometheus.enabled: true`
too if you want it). Use your cluster's actual ingress address instead of
`127.0.0.1` for kind/minikube — check with `kubectl get ingress -n operations-center`.
No TLS is configured; this is plain HTTP only, by design, for local/demo use.

## Overriding values

Anything in `values.yaml` can be overridden three ways, in increasing order
of how "permanent" the override is:

```bash
# one-off, on the command line
--set api.replicaCount=1

# a values file you maintain (values.local.yaml is one example of this)
--values my-values.yaml

# combine both — later flags win over earlier ones
helm upgrade --install operations-center infra/helm/operations-center \
  --namespace operations-center \
  --values infra/helm/operations-center/values.local.yaml \
  --values my-values.yaml \
  --set web.replicaCount=2
```

See `values.yaml` itself for every available field — each is commented
where its meaning isn't obvious from the name alone.

## Validating without installing

```bash
helm lint infra/helm/operations-center --values infra/helm/operations-center/values.local.yaml

helm template operations-center infra/helm/operations-center \
  --namespace operations-center \
  --values infra/helm/operations-center/values.local.yaml \
  > /tmp/operations-center-rendered.yaml

kubectl apply --dry-run=client --namespace operations-center -f /tmp/operations-center-rendered.yaml
```

The last command validates every rendered resource against your cluster's
actual API schema without creating anything.

## Known limitations

Same posture as `infra/k8s/README.md`, carried over unchanged by moving to
Helm:

- **Single API replica only.** SignalR connections are held in-memory by one
  API pod; there is no backplane (Redis or otherwise) for multiple replicas
  yet. Don't raise `api.replicaCount` past 1 without adding one first.
- **A non-default release name breaks the web image's baked-in API proxy
  target** unless you also set `fullnameOverride: operations-center` — see
  "Installing the chart" above. This is a real, Helm-specific limitation on
  top of everything `infra/k8s/` already has: the nginx image itself isn't
  templated, so it can't follow the release name.
- **No NetworkPolicies.** Every pod in the namespace can reach every other pod.
- **PVCs use the cluster's default StorageClass** with no explicit
  provisioner tuning — fine for Docker/Rancher Desktop, kind, and minikube;
  not meant for anything beyond local/demo use.
- **Secrets are demo values checked into git** (`values.local.yaml`). Never
  put real credentials in a file tracked by git; use `existingSecret` or a
  git-ignored personal values file instead.
- **No Ingress controller is installed by this chart** — `ingress.enabled`
  assumes one already exists in the cluster (or you fall back to port-forward).
- **No TLS anywhere.** Local HTTP only.
- **Production Postgres should be managed externally** — the templated
  Deployment+PVC here is a local/demo convenience only, with no backup,
  replication, or failover.
- **Swagger/OpenAPI requires `api.aspnetEnvironment: Development`** (the
  default) — the API only maps those endpoints in Development, matching
  Docker Compose's behavior.
- **Hook Jobs are slightly less visible than normal resources.** They don't
  show up in `helm get manifest` the same way regular templates do, and
  `helm history` won't reflect a hook re-running on an otherwise-unchanged
  upgrade. Use `kubectl get jobs -n operations-center -l app.kubernetes.io/component=migration`
  (or `=seed`) directly if you need to check what actually ran.
