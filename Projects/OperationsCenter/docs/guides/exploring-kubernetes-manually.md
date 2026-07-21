# Exploring a Kubernetes cluster manually

A hands-on companion to
[kubernetes-manifests-setup.md](kubernetes-manifests-setup.md): now that you
have a real cluster (Rancher Desktop) and, once you apply them, a real set of
running Pods, this is how to actually look around inside it with `kubectl` —
the same instinct as `docker ps` / `docker logs` / `docker exec` against a
Compose stack, just spread across more kinds of objects.

Nothing here changes anything — every command below is read-only. Run them
freely, in any order, as many times as you like.

If you haven't applied `infra/k8s/` yet, Part 1 still works against the bare
cluster (Rancher Desktop's own system Pods) — a good way to get comfortable
with the commands before there's an application to look at. Part 2 onward
assumes the `operations-center` namespace exists.

---

## Part 0 — The one command you'll run more than any other

```bash
kubectl get pods -n operations-center
```

`get` lists objects. `-n <namespace>` scopes the query. This single pattern —
`kubectl get <kind> -n <namespace>` — is 80% of manual exploration. Everything
below is a variation on it, or a deeper look at one object it showed you.

A quick note on **`-A`**: add it instead of `-n <namespace>` to see every
namespace at once (`kubectl get pods -A`). Useful for "is anything, anywhere,
unhealthy" sweeps; too noisy for day-to-day work in one project.

---

## Part 1 — The cluster itself

```bash
kubectl cluster-info
kubectl get nodes -o wide
kubectl get namespaces
kubectl top nodes        # requires metrics-server — Rancher Desktop ships one
```

What you're looking for: the node's `STATUS` is `Ready`, and `top nodes`
returns numbers at all (if it errors, metrics-server isn't running — not
fatal, just means resource-usage commands won't work).

```bash
kubectl get storageclass
kubectl get pods -n kube-system
```

`kube-system` is where the cluster's own machinery lives — CoreDNS, the
storage provisioner, and (on Rancher Desktop) Traefik. Everything here should
already show `Running`/`Completed` before you apply anything of your own; if
something in `kube-system` is unhealthy, fix that first; nothing you deploy
will work reliably until it is.

---

## Part 2 — What's running, and is it actually healthy

```bash
kubectl get pods -n operations-center
```

Read the columns left to right:

| Column | What "healthy" looks like | What a problem looks like |
| --- | --- | --- |
| `READY` | `1/1` (or `N/N` matching how many containers the Pod has) | `0/1` — the container isn't passing its readiness probe |
| `STATUS` | `Running` or `Completed` (Jobs) | `CrashLoopBackOff`, `ImagePullBackOff`, `Pending`, `Error` |
| `RESTARTS` | `0`, or a small number that stopped climbing | A number that keeps climbing every time you re-run the command |
| `AGE` | — | A Pod stuck at a few seconds of age for a long time usually means it's being repeatedly restarted |

**`Running` is not the same as healthy.** A container can be `Running` and
still fail its readiness probe forever — always check `READY`, not just
`STATUS`.

```bash
kubectl get pods -n operations-center -o wide
```

Adds the Pod's internal IP and which Node it landed on — useful once you have
more than one Node, not very interesting on a single-node local cluster, but
good to know it's there.

```bash
kubectl get deployments,statefulsets,jobs -n operations-center
```

One level up from Pods: a `Deployment` shows `READY 1/1` when its target
replica count matches how many Pods are actually healthy. A `Job` shows
`COMPLETIONS 1/1` once it's finished — a Job stuck at `0/1` for a while is
either still running or stuck failing (check its Pod for why).

---

## Part 3 — Zooming into one Pod

Pick a Pod name from `kubectl get pods -n operations-center` and go deeper.

### `describe` — the single most useful troubleshooting command

```bash
kubectl describe pod <pod-name> -n operations-center
```

This prints far more than `get` does: which image it's running, its resource
requests/limits, its probes and their current status, and — critically, at
the very bottom — an **Events** table showing everything that's happened to
this Pod recently (scheduled, pulled image, started container, probe
failures, restarts). If a Pod won't start or keeps restarting, the Events
table almost always says why in plain English before you need to look at logs
at all.

### `logs` — what the application itself is saying

```bash
kubectl logs <pod-name> -n operations-center
kubectl logs <pod-name> -n operations-center --follow      # stream, like `tail -f`
kubectl logs <pod-name> -n operations-center --previous    # the crashed container BEFORE the current restart
kubectl logs <pod-name> -n operations-center --tail=50
```

`--previous` is the one people forget: if a container just crashed and
restarted, `kubectl logs` (no flag) shows the **new**, just-started
container's logs — usually nothing useful yet. `--previous` shows the logs
from the container that just died, which is where the actual error is.

If a Pod has more than one container (rare in this project, but common in
general), add `-c <container-name>` to any `logs`/`exec` command to pick
which one.

### `exec` — a shell inside the container

```bash
kubectl exec -it <pod-name> -n operations-center -- sh
```

Drops you into an interactive shell inside the running container (use `sh`,
not `bash` — most of the small images here, like `postgres:17-alpine`, don't
have `bash`). Useful for checking "is this file actually where I think it
is," or running a tool that's inside the image but not exposed any other way
— e.g., checking Postgres directly:

```bash
kubectl exec -it deployment/operations-center-postgres -n operations-center -- \
  psql -U operations_center -d operations_center -c '\dt'
```

You can target `deployment/<name>` directly instead of a specific Pod name —
Kubernetes picks one of that Deployment's Pods for you, which is convenient
when there's only one replica anyway.

### `port-forward` — reach it from your own machine

```bash
kubectl port-forward -n operations-center svc/operations-center-api 5000:8080
```

Opens a tunnel from `localhost:5000` on your machine straight to the
Service's port 8080, for as long as the command keeps running (`Ctrl+C` to
stop it). This is how you actually open the frontend/Grafana/Seq/Prometheus
in a browser without needing Ingress at all — see `infra/k8s/README.md`'s
access table for the exact command per service.

---

## Part 4 — Networking: how things find each other

```bash
kubectl get services -n operations-center
kubectl get endpoints -n operations-center
```

A **Service** has a stable `ClusterIP`. Its matching **Endpoints** object
lists the actual Pod IPs currently behind it — this is the single best way to
answer "is this Service actually routing to anything?" If a Service exists
but its Endpoints list is empty, its `selector` doesn't match any Pod's
labels — a very common copy-paste typo to hunt for.

```bash
kubectl get pods -n operations-center --show-labels
```

Shows every Pod's actual labels, so you can compare them directly against a
Service's `selector` (visible via `kubectl describe service <name> -n
operations-center`) when Endpoints comes up empty.

```bash
kubectl get ingress -n operations-center
kubectl describe ingress operations-center -n operations-center
```

Lists the hostnames/paths an Ingress is configured to route, and (in
`describe`) whether it's actually bound to a controller (an `ADDRESS` value
means yes).

---

## Part 5 — Storage and config

```bash
kubectl get pvc -n operations-center
kubectl get pv
```

A `PersistentVolumeClaim` should show `STATUS: Bound` — anything else
(`Pending`) means no `PersistentVolume` was provisioned for it yet, usually a
StorageClass problem. `pv` (no namespace — PersistentVolumes are
cluster-wide) shows the actual underlying storage each PVC is bound to.

```bash
kubectl get configmap,secret -n operations-center
kubectl get configmap otel-collector-config -n operations-center -o yaml
```

Dumping a ConfigMap's full YAML is a normal, safe thing to do — it's
non-sensitive by definition. **Don't do the same for a Secret** — `kubectl
get secret <name> -o yaml` prints its values base64-**encoded**, not
encrypted, so it's one `base64 -d` away from plaintext. If you need to
confirm a Secret's value made it into a running container correctly, check it
indirectly instead:

```bash
kubectl exec <pod-name> -n operations-center -- env | grep -i password
```

(Still visible in your own terminal, but at least it's not sitting in your
shell history via a manifest dump, and it confirms the value the *container*
actually sees, not just what's stored in the API.)

---

## Part 6 — Resource usage

```bash
kubectl top pods -n operations-center
```

Live CPU/memory per Pod, compared against the `requests`/`limits` you'd find
in `kubectl describe pod`. Requires metrics-server (Rancher Desktop ships one
— see Part 1). Useful for answering "is anything actually close to its memory
limit," which `get`/`describe` alone won't show you.

---

## Part 7 — Watching things change live

Add `--watch` (or `-w`) to almost any `get` command to keep it streaming
updates instead of printing once and exiting:

```bash
kubectl get pods -n operations-center --watch
```

Genuinely the best way to watch a rollout happen in real time — e.g. run this
in one terminal while applying a changed Deployment in another, and watch the
old Pod terminate as the new one becomes `Ready`.

```bash
kubectl get events -n operations-center --sort-by=.lastTimestamp
```

The namespace-wide event stream (the same Events table `describe` shows for
one Pod, but for everything at once, in time order) — the fastest way to see
"what just happened here" across every resource without guessing which Pod to
`describe`.

---

## Checklist

A straight-through pass for getting familiar with a cluster you haven't
looked at before, or sanity-checking one you have.

### Cluster level (works even with nothing applied yet)

- [ ] `kubectl cluster-info` returns a control-plane URL, not an error.
- [ ] `kubectl get nodes` shows every node `Ready`.
- [ ] `kubectl get storageclass` shows a default (marked `(default)`).
- [ ] `kubectl get pods -n kube-system` shows everything `Running` or
      `Completed` — nothing `CrashLoopBackOff` or `Pending`.

### Namespace / workload level

- [ ] `kubectl get pods -n operations-center` — every Pod's `READY` column
      matches its container count (`1/1`, not `0/1`), not just `STATUS:
      Running`.
- [ ] `kubectl get deployments,jobs -n operations-center` — Deployments show
      full `READY` counts; Jobs show `COMPLETIONS 1/1`.
- [ ] For any Pod that isn't healthy: `kubectl describe pod <name> -n
      operations-center`, read the Events table at the bottom before doing
      anything else.
- [ ] For any Pod that's restarting: `kubectl logs <name> -n
      operations-center --previous` to see the crash itself, not the fresh
      restart's near-empty log.

### Networking

- [ ] `kubectl get svc -n operations-center` — every Service you expect
      exists.
- [ ] `kubectl get endpoints -n operations-center` — every Service has at
      least one IP listed (an empty list means its selector matches nothing).
- [ ] If using Ingress: `kubectl describe ingress -n operations-center` shows
      an `ADDRESS`, confirming a controller picked it up.

### Storage and config

- [ ] `kubectl get pvc -n operations-center` — every PVC shows `Bound`, none
      `Pending`.
- [ ] `kubectl get configmap,secret -n operations-center` — every object you
      expect from `infra/k8s/config` and `infra/k8s/secrets` is present.
- [ ] Never dumped a Secret's YAML just to "take a look" — checked values
      indirectly (via `kubectl exec ... env`) when actually needed.

### Access

- [ ] Reached at least one Service via `kubectl port-forward` and confirmed
      it actually responds (not just that the command didn't error).
- [ ] `kubectl top pods -n operations-center` returns numbers (confirms
      metrics-server is working, useful before you ever need it for real
      troubleshooting).

### Cleanup habit

- [ ] Every `port-forward` you started got `Ctrl+C`'d when you were done with
      it — they don't clean themselves up, and a stale one silently holds a
      local port open.
