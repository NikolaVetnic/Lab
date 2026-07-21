# Setting up Kubernetes manifests from scratch

This is a beginner-friendly, no-assumed-knowledge walkthrough of how Operations
Center's first Kubernetes manifests (`infra/k8s/`) were built — Postgres, the
API, the web frontend, and the observability stack (OpenTelemetry Collector,
Prometheus, Grafana, Seq, Tempo) — and how you'd build the same kind of setup
for any other app. It explains _what_ each piece is, _why_ it exists, and _in
what order_ to add things so each step is verifiable before you add the next.

If you just want the short reference (exact commands, file paths, URLs for
_this_ repo), see [`infra/k8s/README.md`](../../infra/k8s/README.md). This
guide is the "why does any of this exist" companion to that — the same
relationship the
[observability stack setup guide](observability-stack-setup.md) has to the
README's Observability section. If you haven't read that guide yet and don't
know what OTLP/Collector/Prometheus/Seq/Tempo actually do, read it first —
this guide assumes you already know _what_ those five things are and focuses
on _how to run them in Kubernetes_ instead of Docker Compose.

---

## Part 1 — The concepts, explained like you've never seen this before

### What is Kubernetes, and why not just keep using Docker Compose?

Docker Compose runs a group of containers on **one machine**, described in
one file, using Compose's own built-in networking and DNS. It's simple and
it's exactly what you want for local development on its own — which is why
this repo still uses it day-to-day.

Kubernetes solves a bigger problem: running containers across a **cluster**
— one machine or many — while constantly working to keep reality matching a
description you wrote down. You don't tell Kubernetes "start this container
now." You tell it "I want 1 copy of this Pod running, always," and a
background process (a **controller**) keeps checking and fixing that on an
ongoing basis — if a Pod crashes, a controller notices and starts a
replacement without you doing anything. This is called the **declarative
model**: you declare the end state, not the steps to get there.

For local development you don't need "many machines." Rancher Desktop,
Docker Desktop, kind, and minikube all give you a **single-node cluster** —
one machine playing every role. From the manifests' point of view, a
single-node local cluster behaves the same way a real multi-node production
cluster would. That's the entire point of doing this locally first: the YAML
you write against your laptop works basically unchanged against a real
cluster later. That "later" step — packaging these manifests into a
configurable, reusable Helm chart — is intentionally **not** done yet; see
"Known limitations" in `infra/k8s/README.md`.

### The core building blocks

| Concept | Everyday analogy | Job |
| --- | --- | --- |
| **Cluster** | An entire office building | Everything: every machine, every running container, every piece of config |
| **Node** | One floor of the building | One machine (physical or virtual) that's part of the cluster and actually runs containers |
| **Pod** | One desk with everyone who works together sitting at it | The smallest deployable unit — one or more containers that always run together on the same Node, sharing a network address and (optionally) storage |
| **Deployment** | A staffing policy: "always keep 3 people at this desk" | A controller that keeps a target number of identical Pods running, and knows how to roll out a new version (new image/config) without just deleting everything at once |
| **Service** | A department's reception phone number, not any one employee's desk phone | A stable network name + address for a group of Pods — Pods themselves get replaced constantly and get a new internal IP every time, so nothing should ever be hardcoded to talk to a Pod directly |
| **Namespace** | A floor's own separate mail room, keyed to that floor only | A way to group related resources and give them their own name scope, so `postgres` in one namespace doesn't collide with `postgres` in another |
| **ConfigMap** | A shared printed memo pinned to the wall | Non-secret configuration values, injected into Pods as environment variables or mounted as files |
| **Secret** | The same memo, but in a locked drawer | The same idea as a ConfigMap, but for sensitive values — stored base64-encoded, access-controlled separately from ConfigMaps |
| **PersistentVolumeClaim (PVC)** | A rented storage locker that survives you moving desks | A request for durable disk storage that outlives any one Pod being deleted and recreated |
| **Job** | A one-off task, not a standing role | A workload that's meant to run to completion once (e.g. a database migration) and then stop — as opposed to a Deployment, which is meant to run forever |
| **Ingress** | The building's front desk, routing visitors by name to the right department | HTTP(S) routing rules from outside the cluster into Services inside it, based on hostname and/or path — requires an Ingress **controller** already installed to do anything |

### How a Pod actually gets reached: the ephemeral-IP problem

This is the single most important thing to internalize before any of the
manifests make sense. A Pod is disposable — a Deployment might replace it at
any time (a crash, a rollout, a Node running low on resources), and the
replacement gets a **brand-new internal IP address**. If anything else in
the cluster remembered the old IP, it would now be talking to nothing.

A **Service** exists specifically to solve this. It has a stable name and a
stable internal IP (called a `ClusterIP`) that never changes, and it
continuously tracks which Pods are currently healthy and routes to them.
Other Pods never talk to a Pod's IP directly — they talk to a Service's name,
and Kubernetes' own internal DNS system (**CoreDNS**) resolves that name to
the Service's stable `ClusterIP`. This is the direct equivalent of Docker
Compose's built-in DNS (where a container reaches `postgres` by service name,
not by IP) — same idea, different implementation.

### Key vocabulary you'll keep running into

- **`kubectl`** — the command-line tool you use to talk to a cluster: create
  things (`apply`), inspect things (`get`, `describe`), read logs (`logs`),
  delete things (`delete`).
- **Labels & selectors** — plain key/value tags attached to resources (e.g.
  `app: operations-center-api`), and a matching query used elsewhere (e.g. a
  Service's `selector`) to say "route to every Pod carrying this label." This
  is the entire mechanism Services and Deployments use to find "which Pods am
  I talking about" — there's no other link between them.
- **Readiness probe vs. liveness probe** — two different questions with two
  different consequences. A **readiness** probe asks "is this Pod ready to
  receive traffic _right now_?" — failing it just removes the Pod from a
  Service's routing until it passes again. A **liveness** probe asks "is this
  Pod stuck or dead?" — failing it gets the Pod **killed and restarted**.
  Using the wrong one (or too aggressive a threshold) for the wrong check can
  restart a perfectly healthy Pod that's just doing something slow, like a
  cold start.
- **Resource requests vs. limits** — a `request` is what a container is
  guaranteed when the cluster decides where to schedule it; a `limit` is the
  hard ceiling it's never allowed to exceed (CPU gets throttled, memory over
  the limit gets the container killed).
- **`ClusterIP` vs. port-forward vs. Ingress** — three different ways to
  actually reach something. `ClusterIP` (the default Service type) is
  internal-only — nothing outside the cluster can reach it directly.
  `kubectl port-forward` opens a temporary tunnel from your machine straight
  to a Service or Pod, with zero cluster-wide routing setup — the fastest way
  to check something works. **Ingress** is persistent, hostname/path-based
  HTTP routing that anyone on your network (or just you, via `/etc/hosts`)
  can use — but it does nothing without an Ingress **controller** (e.g.
  Traefik, ingress-nginx) already running in the cluster to act on it.
- **Immutable field** — some fields on some resource types can't be changed
  after creation. A Job's Pod template is one of them: re-applying an edited
  Job manifest over an existing Job fails with a "field is immutable" error.
  The fix is always "delete the old one, then create the new one" — never
  "edit in place."
- **`envFrom` vs. `env`** — `envFrom` bulk-imports every key from a whole
  ConfigMap or Secret as environment variables. `env` defines one variable at
  a time and can also **build one value out of others** using `$(VAR)`
  syntax, referencing anything defined earlier in that same container's `env`
  list — this is how you assemble something like a database connection
  string out of separately-sourced ConfigMap and Secret values without ever
  writing the password in plain text anywhere in the manifest.

---

## Part 2 — Building the manifests, step by step

Each step below only depends on the ones before it, and each has its own way
to verify it worked _before you move on_ — the same discipline as the
observability guide's Part 2, and for the same reason: every gotcha in Part 3
below was caught exactly because something was checked at the wrong layer.

```text
Namespace
   │
   ▼
ConfigMaps + Secrets  (nothing consumes these yet, so they can't fail loudly)
   │
   ▼
Postgres (Deployment + PVC + Service)
   │
   ▼
API migration Job  ──▶  API (Deployment + Service)  ──▶  Web frontend
                              │
                              ▼
                     OpenTelemetry Collector
                       │        │        │
                       ▼        ▼        ▼
                 Prometheus    Seq     Tempo
                       │                 │
                       └──────▶ Grafana ◀┘
                                  │
                                  ▼
                        Ingress (optional, last)
```

### Step 0 — Prerequisites

- Pick **one** local Kubernetes distribution to start with (Docker Desktop,
  Rancher Desktop, kind, or minikube — see `infra/k8s/README.md` for the
  differences that matter for image loading). Don't try to support all four
  before you've gotten it working on any one of them.
- Have `kubectl` installed and pointed at that cluster
  (`kubectl config current-context` should show it).
- Have the application's container images already buildable — Kubernetes
  never builds images for you, it only runs ones that already exist.

### Step 1 — Create the Namespace

A Namespace is the cheapest possible first manifest: one resource, no
dependencies, and it gives every later resource somewhere to live and a
shared scope for names.

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: operations-center
```

**Verify:** `kubectl get namespace operations-center` shows `Active`.

### Step 2 — ConfigMaps and Secrets

Add the non-sensitive configuration (database name, service ports, feature
flags, and — critically for this stack — the observability config files:
the Collector's pipeline config, Prometheus' scrape config, Tempo's config,
Grafana's datasource/dashboard provisioning files) as ConfigMaps, and the
sensitive values (database password, JWT signing key, admin passwords) as
Secrets.

Nothing consumes these yet, so this step can't fail in a visible way — that's
deliberate. Get the values right here, once, instead of debugging "is it the
config or the workload" later when both are new at the same time.

**Verify:** `kubectl get configmap,secret -n operations-center` lists
everything you expect, and `kubectl get configmap <name> -n operations-center
-o yaml` for one of them shows the content you meant to put there (typos in a
multi-line config block mounted as a file are easy to miss until something
downstream fails to parse it).

### Step 3 — Postgres

This is the first real workload, and it goes first because almost everything
else depends on it being reachable. Three pieces:

1. A **PersistentVolumeClaim** — without it, all data disappears the moment
   the Pod is replaced, which happens far more often than you'd expect even
   in a stable-looking local cluster.
2. A **Deployment** with `pg_isready`-based readiness/liveness probes, CPU/
   memory requests and limits, and the database name/user/password wired in
   from the ConfigMap/Secret from Step 2.
3. A **Service** so anything else in the cluster can reach it by a stable
   name, matching how you'd already reach it in Docker Compose.

**Verify:** `kubectl get pods -n operations-center` shows the Postgres Pod as
`1/1 Ready`, and `kubectl exec` into it to run `pg_isready` directly confirms
the same thing the probe is checking.

### Step 4 — The API: migration Job first, then the Deployment

Two separate concerns, two separate manifests — don't combine them:

1. A **Job** that runs the application's database-migration mode once and
   exits. This must never be something every API replica does on its own
   startup — that's how you get multiple Pods racing to apply the same
   migration concurrently. Run it once, wait for it to finish, then move on.
2. A **Deployment** for the API itself, with readiness/liveness probes
   against whatever health endpoints the application actually exposes, and a
   **Service** so the frontend (and anything else) can reach it by name.

**Verify:** `kubectl wait --for=condition=Complete job/<name>` returns
successfully before you even look at the API Deployment. Then confirm the API
Pod reaches `Ready`, and `kubectl port-forward` to its Service to hit its
health endpoint directly from your machine.

### Step 5 — The web frontend

A Deployment + Service for whatever serves the frontend (commonly an nginx
container serving a built single-page app). If this container's own
configuration already reverse-proxies API/WebSocket paths to the API (check
before assuming you need to reconfigure anything — see Part 3), this step
can be genuinely this simple.

**Verify:** port-forward to the web Service and confirm the page loads, and
that whatever it proxies to the API (login, data fetching) actually works
end-to-end — not just that the static HTML renders.

### Step 6 — The OpenTelemetry Collector

If you already have a working Collector config file (from building the
Docker Compose version of this stack — see the
[observability guide](observability-stack-setup.md)), this step is almost
entirely "wrap that same file in a ConfigMap and mount it," not "write new
config." The interesting part is what hostnames the config file talks about:
they need to resolve inside Kubernetes, which Step 6 onward all depend on —
see "Service DNS vs. Compose DNS" in Part 3.

**Verify:** exactly like the Compose version — generate traffic through the
app, then read the Collector Pod's logs and look for non-zero record counts.

### Step 7 — Prometheus

A **PersistentVolumeClaim**, a ConfigMap holding the scrape config (pointed
at the Collector's Service, not `localhost`), and a Deployment + Service.

**Verify:** port-forward to Prometheus's Service, open its own UI, go to
Status → Targets, confirm the Collector target shows `UP`.

### Step 8 — Grafana

A PVC (for its own settings/session state), ConfigMaps for provisioning
(datasources pointing at the Prometheus and Tempo Services by name, plus any
starter dashboards), a Secret for the admin password, and a Deployment +
Service.

**Verify:** port-forward and open Grafana; the data sources should already be
there without you clicking "Add data source" — that's what provisioning-via-
file means — and a dashboard panel should render real data.

### Step 9 — Seq

A PVC, a Deployment (EULA accepted via its documented environment variable,
same as the Compose version) + Service, and — back in the Collector's
config — an exporter pointed at Seq's Service by name.

**Verify:** port-forward to Seq, generate a request through the app, confirm
a structured log line shows up with real fields, not just a raw string.

### Step 10 — Tempo

A PVC, a ConfigMap for its config (single-binary mode, local disk storage
only — no object storage backend for a local/demo setup), a Deployment +
Service, and a second Grafana data source pointed at Tempo's Service.

**Verify:** generate a request through the app, then check Grafana's Explore
view with the Tempo data source selected — confirm you see more than one span
per trace (a root span plus at least one real child span).

### Step 11 — Ingress (optional, and last)

Add this only once everything above already works via port-forward. An
Ingress needs a controller already installed in the cluster to do anything
at all — if you're not sure your cluster has one, port-forward is the
guaranteed fallback and doesn't need this step at all.

**Verify:** `kubectl get ingress -n operations-center` shows the rules, and
(with the right `/etc/hosts` entries and a controller installed) each
hostname reaches the right Service in a browser.

### Step 12 — Confirm nothing earlier broke

Same rule as the observability guide's Step 7: every time you add a new
piece, re-check the ones already working. It's easy to break Prometheus
scraping while editing the same Collector ConfigMap for Tempo's sake — a
regression here is silent until you actually look.

---

## Part 3 — Gotchas worth knowing before you hit them

These are all things that actually came up while building this exact set of
manifests — not hypothetical warnings.

- **Docker Compose DNS and Kubernetes Service DNS are two different systems
  that can be made to look identical on purpose.** If an image or config file
  already has a hostname baked in (e.g. an nginx config built into a frontend
  image, or an observability config file written for Compose), the easiest
  fix isn't to rebuild the image or rewrite the config — it's to name the
  Kubernetes **Service** identically to the old Compose service name. Cluster
  DNS then resolves that name the same way Compose's DNS did, and nothing
  downstream needs to change at all.
- **A Job's Pod template is immutable — you can't `kubectl apply` an edited
  one over an existing Job.** You'll get a "field is immutable" error. Delete
  the old Job first (`kubectl delete job <name>`), then apply the new one.
  This matters most for anything migration-shaped that you expect to re-run.
- **Forcing every container to run as non-root breaks some official images on
  purpose, not by accident.** nginx's official image starts its master
  process as root specifically so it can bind port 80 (a privileged port)
  and then drops privilege internally for its worker processes — forcing
  `runAsNonRoot: true` on the Pod breaks that model outright. Postgres's
  official image similarly needs to start as root briefly to fix ownership of
  a fresh data directory before it drops privilege itself. Read what an
  image's own `USER`/entrypoint already does before adding a blanket
  `securityContext` — hardening things that already drop their own privilege
  correctly, versus things that generically run as root the whole time, are
  not the same problem and don't take the same fix.
- **A read-only root filesystem breaks anything that assumes it can write
  somewhere, even somewhere you wouldn't think of as "the app's data."**
  Application frameworks often try to write temp files, a cache directory, or
  something under a default "home" path (e.g. cryptographic key material) —
  none of that is your application data, but it still needs somewhere
  writable. The fix is a small `emptyDir` volume mounted at that one path
  (`/tmp`, or wherever the framework insists on writing), not abandoning
  `readOnlyRootFilesystem` altogether.
- **A PersistentVolumeClaim's on-disk files are owned by whatever user first
  wrote to them — which might not be the user your container runs as.**
  Several official images (Prometheus, Grafana, Tempo) run as a specific
  non-root UID by default. If a Pod's `securityContext.fsGroup` doesn't match
  that UID's group, the container can mount the volume but fail to actually
  write to it. Check what UID an image runs as (usually documented, or
  visible via `docker run --rm <image> id`) before assuming a plain PVC mount
  "just works."
- **`$(VAR)` interpolation inside `env:` only sees variables defined earlier
  in the same list, in a predictable way if you define them explicitly.** If
  you need to build one environment variable's value out of others (a
  connection string out of a host/user/password), define those pieces as
  explicit `env` entries (even if they're also available via `envFrom`)
  immediately before the value that references them — don't rely on the
  merge order between `envFrom` and `env` being obvious at a glance later.
- **Mounting a ConfigMap as a file via `subPath` means edits to that
  ConfigMap don't get picked up automatically.** Unlike a whole-directory
  ConfigMap mount (which does update live, just not always instantly),
  `subPath` mounts are effectively frozen at Pod start — you need to
  recreate the Pod (e.g. `kubectl rollout restart deployment/<name>`) after
  changing the underlying ConfigMap.
- **"The image built fine" doesn't mean the cluster can see it.** Local
  Kubernetes distributions differ wildly here: some (Docker Desktop, Rancher
  Desktop on the moby engine) share the same image store your `docker build`
  already used, so there's nothing extra to do. Others (kind, minikube, or
  Rancher Desktop on containerd) need an explicit "load this image into the
  cluster" step. Skipping it doesn't error at build time — it shows up later
  as `ImagePullBackOff`, which looks like a networking or registry problem
  and isn't one.
- **Not every backend has a documented HTTP health endpoint — don't invent
  one and hope.** Guessing a plausible-looking `/health` or `/ready` path
  that doesn't actually exist gives you a probe that fails 100% of the time,
  which reads as "this thing never becomes healthy" even though the process
  is running fine. If nothing in the image's own docs confirms a path,  use a
  `tcpSocket` probe against the port it's supposed to be listening on
  instead — it proves the process is up and accepting connections without
  guessing at application-level behavior you haven't verified exists.
- **A probe firing before an app can possibly be ready looks exactly like a
  crash, but isn't one.** `initialDelaySeconds` set too low for something
  with a genuinely slow cold start (a JVM, a database running its first-time
  setup) causes repeated probe failures and restarts that look identical to
  a real crash loop in `kubectl get pods`. Check `kubectl describe pod
  <name>` for the actual probe failure reason before assuming the
  application itself is broken.

---

## Checklist

Use this as a straight-through checklist when setting this kind of stack up
for the first time (or repeating it in a new project). Each box assumes the
ones above it are already checked and verified — tick a box because you
_observed_ the verification step pass, not because the manifest _looks_
right.

### Cluster and namespace

- [ ] A local Kubernetes distribution chosen and running, `kubectl` pointed at
      it.
- [ ] `Namespace` manifest applied and confirmed `Active`.
- [ ] Every later resource explicitly declares that namespace — nothing left
      to land in `default` by accident.

### Config and secrets

- [ ] Non-sensitive configuration in ConfigMaps; sensitive values (passwords,
      signing keys) in Secrets — never mixed into the same object.
- [ ] Example/demo secret values only, clearly marked as such, kept out of
      anything that would look like a real credential.
- [ ] A real secrets file with actual values is git-ignored, never committed.

### Postgres

- [ ] PersistentVolumeClaim in place before the Deployment that uses it.
- [ ] Readiness and liveness probes using the database's own health command
      (e.g. `pg_isready`), not a generic TCP check.
- [ ] Resource requests and limits set — not omitted, not so low the
      container gets killed under any real load.
- [ ] Verified: Pod reaches `Ready`, and a manual health command run inside
      the Pod agrees with what the probe reports.

### Application (API + migrations + frontend)

- [ ] Database migrations run via a dedicated Job, applied and confirmed
      `Complete` before relying on the API being fully functional.
- [ ] No API replica runs migrations on its own startup.
- [ ] API Deployment's readiness/liveness probes point at real, existing
      health endpoints — confirmed by reading the application's own routes,
      not guessed.
- [ ] Database connection details assembled from ConfigMap + Secret values,
      never a password written in plain text directly in the manifest.
- [ ] Frontend Deployment's own proxy/reverse-proxy configuration (if any)
      confirmed to already point at the right internal Service name, or
      updated if it doesn't.
- [ ] Verified: login, and whatever the frontend's core end-to-end flow is,
      works through a port-forward — not just that a health endpoint returns
      200.

### OpenTelemetry Collector

- [ ] Existing Collector config (if one already exists from a Docker Compose
      setup) reused via a ConfigMap, not rewritten from scratch.
- [ ] Any hostnames inside that config resolve inside Kubernetes — either
      because Services were deliberately named to match, or the config was
      updated to the new names.
- [ ] Verified: Collector logs show non-zero records after generating app
      traffic.

### Prometheus + Grafana

- [ ] Prometheus's scrape config points at the Collector's Service by name.
- [ ] PersistentVolumeClaim attached, with `fsGroup` set to match whatever UID
      the Prometheus image actually runs as.
- [ ] Verified: Prometheus's own Status → Targets page shows the Collector
      target as `UP`.
- [ ] Grafana's datasources and any starter dashboards provisioned via
      ConfigMap, not clicked together by hand.
- [ ] Admin password sourced from a Secret.
- [ ] Verified: a Grafana dashboard panel renders real data with zero manual
      setup after a fresh apply.

### Seq

- [ ] PersistentVolumeClaim attached.
- [ ] EULA accepted via the documented environment variable, for local/demo
      use only.
- [ ] Collector's logs pipeline exporter points at Seq's Service by name,
      using whatever ingestion path that Seq version actually documents.
- [ ] Verified: a real log line from an actual app request is retrievable
      from Seq with structured fields intact.

### Tempo

- [ ] PersistentVolumeClaim attached, single-binary mode, local disk storage
      only.
- [ ] `fsGroup` set to match whatever UID the Tempo image actually runs as.
- [ ] Collector's traces pipeline exporter points at Tempo's Service by name.
- [ ] Tempo added as a second Grafana data source.
- [ ] Verified: a real trace is retrievable through Grafana's Explore view,
      showing more than one span.

### Ingress (optional)

- [ ] Added only after every piece above already works via port-forward.
- [ ] No assumption baked in about which specific Ingress controller is
      installed, unless that's explicitly documented.
- [ ] `/etc/hosts` entries (or equivalent) documented for whoever uses it
      next.
- [ ] Verified, if a controller is actually installed: each hostname reaches
      the right Service in a browser.

### Whole-stack sanity pass

- [ ] Re-verified Postgres, the API, and every already-working observability
      backend still work _after_ adding the next piece — don't just verify
      the newest addition.
- [ ] `kubectl get pods -n <namespace>` shows every Pod `Ready`, not just
      `Running` (a container can be running and still failing its readiness
      probe).
- [ ] Confirmed which fields are immutable on which resources (Jobs, at
      minimum) and documented the delete-then-reapply step for re-running
      them.
- [ ] Documented, in whatever form your project uses, how to build/load
      images for the local Kubernetes distributions you actually support, how
      to apply everything in the right order, and how to reach each piece —
      so the next person (including future-you) doesn't have to reverse-
      engineer the manifests to understand the shape of the system.
