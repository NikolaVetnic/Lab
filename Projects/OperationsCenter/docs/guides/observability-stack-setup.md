# Setting up an observability stack from scratch

This is a beginner-friendly, no-assumed-knowledge walkthrough of how Operations
Center's observability stack (OpenTelemetry → Collector → Prometheus + Grafana +
Seq + Tempo) was built, and how you'd build the same kind of stack for any other
app. It explains _what_ each piece is, _why_ it exists, and _in what order_ to add
things so each step is verifiable before you add the next.

If you just want the short reference (URLs, exact file paths, commands for
_this_ repo), see the [README's Observability section](../../README.md). This
guide is the "why does any of this exist" companion to that.

---

## Part 1 — The concepts, explained like you've never seen this before

### What is "observability"?

When your app is running in production (or in a demo, or on your laptop), it's
a black box unless you build in ways to look inside it. Observability is the
umbrella term for "the ways I can find out what my running app is actually
doing, without attaching a debugger." It's usually broken into three kinds of
signal, often called the **three pillars**:

| Signal      | Answers the question                                                                  | Everyday analogy                                                   |
| ----------- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| **Metrics** | "How much / how many / how fast, over time?"                                          | A car's dashboard: speed, fuel level, engine temperature           |
| **Logs**    | "What exactly happened, in this one event?"                                           | A ship's logbook: timestamped, free-text entries                   |
| **Traces**  | "What path did this one request take through my system, and where did it spend time?" | A package tracking page: it shows every stop, not just "delivered" |

You need all three because they answer different questions. A metric can tell
you "error rate went up at 14:32," a log can tell you "why" for one specific
failed request, and a trace can tell you "which of the five services this
request touched was actually slow."

### What is OpenTelemetry (OTel)?

OpenTelemetry is a vendor-neutral standard (and a set of SDKs) for producing
metrics, logs and traces from your application code. "Vendor-neutral" is the
important word: your code calls the OpenTelemetry API, not "the Datadog API" or
"the Grafana API." Where the data actually _ends up_ (Prometheus? Datadog?
Seq? something else?) becomes a matter of configuration, not code. This is why
you can swap or add backends (like we did — Seq for logs, Tempo for traces)
without touching a single line of application code.

### What is OTLP?

OTLP (OpenTelemetry Protocol) is the wire format OpenTelemetry uses to _ship_
telemetry data from one place to another — from your app to a collector, or
from a collector to a backend. Think of it as "the shipping container format":
as long as both ends speak OTLP, it doesn't matter what's inside or what
backend is on the receiving end.

### What is the OpenTelemetry Collector?

Your application could, in theory, export telemetry directly to every backend
you care about (Prometheus, Seq, Tempo, ...). That would mean:

- your app needs network access to every backend;
- your app needs to know about every backend's protocol quirks;
- adding a new backend means redeploying the app.

The **Collector** solves this by sitting in the middle. Your app sends
everything (metrics, logs, traces) to _one place_ over OTLP. The Collector
then decides where each signal goes. Adding a new backend later (like we did
with Tempo, and before that Seq) means changing the Collector's configuration
file — never the application.

A Collector configuration always has the same three building blocks:

- **Receivers** — how data gets _in_ (in our case, always "OTLP over gRPC and
  HTTP").
- **Processors** — anything that happens to the data in transit (we only use
  `batch`, which groups records together instead of sending one network call
  per record — much more efficient).
- **Exporters** — how data goes _out_, and to where. There's one exporter per
  destination (Prometheus, Seq, Tempo, or just `debug` which prints to the
  Collector's own console for troubleshooting).

These get wired together into **pipelines** — one pipeline per signal type
(metrics, logs, traces), each listing which receiver(s), processor(s) and
exporter(s) it uses. A single Collector can — and in our case does — fan the
same incoming stream out to completely different backends per signal type:

```text
                               ┌──(metrics)──▶ Prometheus
Your App ──OTLP──▶ Collector ──┼──(logs)─────▶ Seq
                               └──(traces)───▶ Tempo
```

### What do Prometheus, Grafana, Seq and Tempo each actually do?

| Tool           | Signal                | Job                                                                                                                                                                                                      |
| -------------- | --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Prometheus** | Metrics               | Stores time-series numbers (counters, gauges, histograms) and lets you query them over time. It works by _scraping_ — it periodically pulls a snapshot from a URL, rather than having data pushed to it. |
| **Grafana**    | (all — it's a viewer) | A dashboard/visualization layer. It doesn't store telemetry itself — it queries other systems ("data sources") like Prometheus and Tempo and draws graphs/tables/panels from the results.                |
| **Seq**        | Logs                  | A structured log server: you can search, filter, and pivot on the actual fields of a log message (not just grep text), and it understands OpenTelemetry's log format natively.                           |
| **Tempo**      | Traces                | Stores full distributed traces (the parent/child span trees) and lets Grafana query and visualize "what happened during this one request, across every layer it touched."                                |

A useful mental shortcut: **Prometheus and Tempo are storage + query engines
for one signal type each. Grafana is the shared window you look through to
see both. Seq is a self-contained storage + query engine + UI, all in one, for
logs specifically** (it doesn't need Grafana at all).

### Key vocabulary you'll keep running into

- **Instrumentation** — the code (usually a library, "automatic
  instrumentation") that generates telemetry for you, e.g. one line that
  wires up "every incoming HTTP request automatically becomes a trace."
- **Span** — one timed unit of work inside a trace, e.g. "handling this HTTP
  request" or "running this SQL query." A trace is a tree of spans.
- **Exporter / Receiver** — see above; always paired with a protocol (here,
  always OTLP) and a destination or source.
- **Scrape** — Prometheus's pull model: it fetches `/metrics` from a target on
  a schedule, rather than the target pushing to it.
- **Data source** (Grafana term) — a system Grafana knows how to query
  (Prometheus, Tempo, etc.). Configuring one is what lets Grafana draw
  anything at all.
- **Provisioning** (Grafana term) — pre-configuring data sources/dashboards
  via files on disk, so they exist automatically when the container starts —
  no clicking through the UI by hand.

---

## Part 2 — Building the stack, step by step

Each step below only depends on the ones before it, and each has its own way
to verify it worked _before you move on_. Don't skip the verification — every
gotcha in Part 3 was caught exactly because something was checked at the wrong
layer.

### Step 0 — Prerequisites

- Docker (and Docker Compose) installed and the daemon actually running.
- Your application already runs and serves traffic without any observability
  code at all. Observability is something you _add_ to a working app, never a
  precondition for it to start.

### Step 1 — Instrument the application with the OpenTelemetry SDK

Add the OpenTelemetry SDK for your language/framework and turn on:

- automatic tracing for incoming requests and outgoing HTTP calls;
- automatic metrics for the framework and runtime;
- (optional but recommended) a couple of custom, business-meaningful spans and
  counters for the one or two flows you actually care about (here: incident
  created, incident status changed).

Point the exporter at an OTLP endpoint — even before a Collector exists,
you're building against "some OTLP receiver will be at this URL." Make this
**opt-in and fail-safe from day one**: telemetry export failing must never
break a real request or stop the app from starting. This is a one-way door if
you get it backwards — it's much harder to retrofit "don't let telemetry take
the app down with it" after the fact than to design for it up front.

**Verify:** run the app with telemetry export pointed at nothing (or a closed
port). Confirm the app still starts and answers requests normally, and that
you only see export-failure log lines, not crashes.

### Step 2 — Stand up the OpenTelemetry Collector

Add one container running the Collector image, with a config file that has:

- one `otlp` receiver (gRPC + HTTP);
- a `batch` processor;
- exactly one exporter to start: `debug` (prints everything it receives to its
  own console — nothing else, no real backend yet).
- three pipelines (metrics/traces/logs), all using `[otlp]` receiver, `[batch]`
  processor, `[debug]` exporter.

Point your app's OTLP endpoint at this container.

**Verify:** generate some traffic against your app, then read the Collector's
container logs. You should see periodic `"Logs"` / `"Traces"` / `"Metrics"`
entries with non-zero record counts. If you see nothing, telemetry isn't
reaching the Collector yet — don't move on until it does. This step, with
nothing but a `debug` exporter, is the cheapest possible way to prove "my app
→ Collector" wiring works before adding any real backend into the mix.

### Step 3 — Add Prometheus (metrics)

Two things need to happen, in order:

1. Add a `prometheus` exporter to the Collector's config, and add it to the
   metrics pipeline's exporters. This makes the Collector expose a
   `/metrics` HTTP endpoint that looks exactly like something Prometheus
   itself would scrape.
2. Add a Prometheus container with a config file telling it to scrape that
   Collector endpoint (`scrape_configs`), and give it a persistent volume (it
   stores its time-series data on disk).

**Verify:** open Prometheus's own UI, go to Status → Targets, and confirm your
Collector target shows as `UP`. Then run a query for a metric you know your
app/framework produces (something built-in like an HTTP request counter) and
confirm you get numbers back.

### Step 4 — Add Grafana (visualize the metrics)

Add a Grafana container. Rather than clicking "Add data source" by hand every
time you recreate the container, **provision** it: mount a small YAML file
that pre-declares the Prometheus data source (pointing at Prometheus's
_Docker service name_, not `localhost` — Grafana runs inside its own
container and has to reach Prometheus over the Docker network, not your
machine's loopback address). Optionally provision a starter dashboard JSON
file the same way.

**Verify:** open Grafana, confirm the Prometheus data source is already there
(you shouldn't have had to configure anything by hand), and confirm a
dashboard panel actually renders data.

### Step 5 — Add Seq (logs)

1. Add a Seq container. Give it a persistent volume, accept its EULA via the
   documented environment variable, and don't configure authentication for a
   local/demo setup (only set an admin password if you actually want auth).
2. Check whether your Seq version supports **native OTLP log ingestion**
   (recent versions do, at a documented HTTP path). If it does, you don't need
   anything Seq-specific in the Collector — just point a generic `otlphttp`
   exporter's base URL at Seq's OTLP ingestion path, and add it to the logs
   pipeline. If your version doesn't support OTLP natively, you'd need a
   different bridge (e.g. an OTLP→Seq-API shim) — check this before you
   assume the "just point an exporter at it" shortcut works.

**Verify:** generate a request through your app, then either browse Seq's UI
or hit its events API directly, filtering for your service's logs. Confirm
the fields you'd expect (message, level, and anything structured your code
logs) show up — not just a raw string blob.

### Step 6 — Add Tempo (traces)

1. Add a Tempo container running in **single-binary local mode** (one process
   doing everything — ingestion, storage, querying — no cluster). Give it a
   config file that uses **local disk storage only** (no S3/GCS/Azure), and
   an OTLP receiver.
2. Add an `otlp` exporter to the Collector pointed at Tempo's OTLP receiver,
   and add it to the traces pipeline. Since this is all internal,
   unauthenticated container-to-container traffic, plain gRPC without TLS is
   fine (mark it explicitly as insecure in config — don't leave it
   ambiguous).
3. Add Tempo as a second Grafana data source (again: Docker service name, not
   `localhost`), alongside Prometheus.

**Verify:** generate a request through your app, then either query Tempo's
own API directly by service name, or use Grafana's Explore view with the
Tempo data source selected. Open one trace and confirm you see more than just
a single span — you want to see the parent HTTP-request span _and_ the child
spans underneath it (e.g. your custom business span, a database call). A
trace with only one span usually means child spans aren't being created or
correlated, not that tracing is "basically working."

### Step 7 — Confirm nothing earlier broke

Every time you add a new piece, re-check the _previous_ ones. It's easy to
accidentally break Prometheus scraping while editing the Collector config for
Tempo, since they're all in the same YAML file. A regression here is silent
until you look.

---

## Part 3 — Gotchas worth knowing before you hit them

These are all things that actually came up while building this exact stack —
not hypothetical warnings.

- **A container only picks up config-file changes on recreate, not on
  restart, and not automatically on "up."** If you mount a config file (like
  the Collector's or Tempo's YAML) and then edit that file on your host,
  running `docker compose up -d` again often won't recreate the container,
  because Compose only compares the _service definition_, not the _contents_
  of files it bind-mounts. After editing a mounted config file, force it with
  `docker compose up -d --force-recreate <name-of-service>`. Don't assume "the
  logs look the same" means "nothing changed" — check for the setting you
  actually edited.
- **A stray local process can silently steal your container's traffic.** If
  something else on your machine (an old `dotnet run`, a leftover dev server)
  is bound to the same host port you're publishing for your containerized
  app, requests can get routed to the _wrong_ process without any error —
  you'll get real, valid-looking responses that never touch the container you
  think you're testing. If telemetry mysteriously "isn't reaching" a Collector
  even though everything looks configured correctly, check what's actually
  listening on that port on the host before you doubt your config.
- **Collector exporter type names get renamed/deprecated between versions.**
  E.g. the generic HTTP exporter's alias was renamed from `otlphttp` to
  `otlp_http`, and the generic gRPC one from `otlp` to `otlp_grpc`. The
  Collector still accepts the old alias but logs a deprecation warning — don't
  ignore that warning, since aliases do eventually get removed.
- **Prometheus's `increase()`/`rate()` functions can under-report a brand-new
  counter.** If a counter only just started existing and has only ever held
  one value, `increase()` over a window can show `0` even though the metric
  is definitely being exported correctly — it's measuring the _change between
  samples in the window_, not the raw value. For "what's the total right
  now," query the counter directly (`sum(my_counter)`), not its increase.
- **A persisted data volume keeps its own state independent of your
  environment variables.** Grafana (and Seq) only apply "initial admin
  password" environment variables the _first_ time they initialize their data
  volume. If you recreate the container later with a different password
  environment variable but keep the same named volume, the old password is
  still what's stored — the environment variable is silently ignored on that
  restart. If you get locked out, that's usually why.
- **Binary string encoding can make you doubt correct code.** If you ever
  grep a compiled binary for a string literal and don't find it, don't
  immediately conclude the code is missing — some runtimes store string
  literals in a different encoding (e.g. UTF-16) than your grep expects
  (UTF-8/ASCII). Check both before concluding anything about what's in a
  built artifact.
- **A metric only counting your own manual test traffic doesn't confirm
  end-to-end.** Query the same data through every layer you claim is working
  — the raw backend directly, and the actual UI/proxy path a real user would
  use (e.g. Grafana's own datasource proxy, not just Prometheus's API
  directly) — since something can work at one layer and quietly not at
  another.

---

## Checklist

Use this as a straight-through checklist when setting this kind of stack up
for the first time (or repeating it in a new project). Each box assumes the
ones above it are already checked and verified — don't tick a box just
because the config _looks_ right; tick it because you _observed_ the
verification step pass.

### Application instrumentation

- [ ] OpenTelemetry SDK added for traces, metrics and logs.
- [ ] Automatic instrumentation enabled for incoming requests, outgoing HTTP,
      and the runtime.
- [ ] At least one custom, business-meaningful span and counter added for a
      flow you actually care about.
- [ ] Telemetry export is opt-in/configurable, and is fail-safe: pointing it
      at nothing does not stop the app from starting or serving requests.
- [ ] Sensitive data (tokens, passwords, connection strings, full request/
      response bodies) is never logged or attached as telemetry attributes.

### Collector

- [ ] Collector container running, with an `otlp` receiver (gRPC + HTTP).
- [ ] App's OTLP endpoint points at the Collector, not at any backend
      directly.
- [ ] `batch` processor used on every pipeline.
- [ ] Verified: Collector logs show non-zero records after generating app
      traffic, using nothing but a `debug` exporter, before adding any real
      backend.

### Prometheus + Grafana (metrics)

- [ ] `prometheus` exporter added to the Collector's metrics pipeline.
- [ ] Prometheus container scraping the Collector's Prometheus endpoint, with
      a persistent volume.
- [ ] Verified: Prometheus's own Status → Targets page shows the Collector
      target as `UP`.
- [ ] Grafana container running, with the Prometheus data source
      _provisioned via file_, not clicked together by hand.
- [ ] Verified: a Grafana dashboard panel renders real data without any
      manual data-source setup after a fresh `docker compose up`.

### Seq (logs)

- [ ] Seq container running, EULA accepted via its documented environment
      variable, persistent volume attached.
- [ ] Confirmed your Seq version's OTLP ingestion path before wiring the
      exporter (don't assume).
- [ ] Generic OTLP exporter added to the Collector's logs pipeline, pointed
      at Seq.
- [ ] Verified: a real log line from your app, generated by an actual request,
      is retrievable from Seq with structured fields intact (not just a raw
      string).
- [ ] Verified: log entries carry trace/span correlation fields where your
      app is inside an active trace.

### Tempo (traces)

- [ ] Tempo container running in single-binary mode, local disk storage only.
- [ ] `otlp` exporter added to the Collector's traces pipeline, pointed at
      Tempo, explicitly marked insecure (no ambiguous TLS state).
- [ ] Tempo added as a second Grafana data source (Docker service name, not
      `localhost`), Prometheus still present and still default.
- [ ] Verified: a real trace from an actual request is retrievable both
      directly from Tempo's API and through Grafana's datasource proxy.
- [ ] Verified: the retrieved trace shows more than one span — the request's
      root span plus at least one meaningful child span.

### Whole-stack sanity pass

- [ ] Re-verified Prometheus and Seq still work _after_ adding Tempo (or any
      later addition) — don't just verify the newest piece.
- [ ] Confirmed no host port collisions between any two services (including
      anything you might be running outside Docker).
- [ ] Confirmed every backend is genuinely optional at runtime: stopping any
      one of Prometheus/Grafana/Seq/Tempo does not break the application
      itself, only the observability view into it.
- [ ] Documented, in whatever form your project uses, which service does
      what, how signals flow between them, and which manual steps (if any)
      are still required — so the next person (including future-you) doesn't
      have to reverse-engineer the config to understand the shape of the
      system.
