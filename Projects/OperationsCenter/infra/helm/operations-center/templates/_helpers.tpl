{{/*
Chart name, honoring nameOverride.
*/}}
{{- define "operations-center.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Fully qualified resource name prefix. Collapses to just the release name when
the release name already contains the chart name — this is what makes
`helm install operations-center ...` (as documented throughout this chart)
produce names identical to infra/k8s/ (operations-center-api, etc.) and to
the hostname baked into the web image's nginx.conf. See values.yaml's header
comment and the README's "Known limitations".
*/}}
{{- define "operations-center.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := include "operations-center.name" . }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Chart name and version, for the "helm.sh/chart" label.
*/}}
{{- define "operations-center.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels applied to every resource.
*/}}
{{- define "operations-center.labels" -}}
helm.sh/chart: {{ include "operations-center.chart" . }}
app.kubernetes.io/name: {{ include "operations-center.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
environment: {{ .Values.global.environment }}
{{- with .Values.global.commonLabels }}
{{ toYaml . }}
{{- end }}
{{- end }}

{{/*
Selector labels. Deliberately a minimal, stable subset — never add anything
here that could change across upgrades (e.g. version), since Deployment
selectors are immutable once created.
*/}}
{{- define "operations-center.selectorLabels" -}}
app.kubernetes.io/name: {{ include "operations-center.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Secret name helpers — each resolves to an existingSecret override if set,
otherwise the chart's own templated Secret (see secret.example.yaml). Keeping
one helper per concern (not one big shared Secret) is what lets each be
overridden independently.
*/}}
{{- define "operations-center.postgres.secretName" -}}
{{- if .Values.postgres.auth.existingSecret }}
{{- .Values.postgres.auth.existingSecret }}
{{- else }}
{{- include "operations-center.fullname" . }}-postgres
{{- end }}
{{- end }}

{{- define "operations-center.jwt.secretName" -}}
{{- if .Values.api.jwt.existingSecret }}
{{- .Values.api.jwt.existingSecret }}
{{- else }}
{{- include "operations-center.fullname" . }}-jwt
{{- end }}
{{- end }}

{{- define "operations-center.grafana.secretName" -}}
{{- if .Values.observability.grafana.admin.existingSecret }}
{{- .Values.observability.grafana.admin.existingSecret }}
{{- else }}
{{- include "operations-center.fullname" . }}-grafana
{{- end }}
{{- end }}

{{- define "operations-center.seed.secretName" -}}
{{- if .Values.seedJob.seedPasswords.existingSecret }}
{{- .Values.seedJob.seedPasswords.existingSecret }}
{{- else }}
{{- include "operations-center.fullname" . }}-seed
{{- end }}
{{- end }}
