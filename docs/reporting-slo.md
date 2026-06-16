# Reporting API — SLO / SLI

Baseline alinhada à constituição do projeto (throughput do **consolidado**) e ao spec `003-feature-consolidated-report`.

A meta de performance da constituição (50 req/s com até 5% de perda) aplica-se às **leituras do consolidado**, não à API de escrita de Transactions. Objetivos do caminho de escrita: `transactions-slo.md`.

## Pilares

| Pilar | Objetivo | Sinais primários |
|-------|----------|------------------|
| **Performance** | Leituras cacheadas rápidas; caminho sem cache limitado | `reporting.read.duration`, `reporting.projection.duration` |
| **Disponibilidade** | API acessível; poucos erros de servidor | `reporting.requests.total`, health `/ready` |
| **Confiabilidade** | Projeção acompanha carga; cache coerente | `reporting.messages.*`, `reporting.cache.*`, gauges SQS |

## Indicadores de nível de serviço (SLI)

| SLI | Meta | Constante | Medição |
|-----|------|-----------|---------|
| Throughput do consolidado | **50 RPS** sustentado | `TargetRequestsPerSecond` | Gate de carga / `reporting.requests.total` |
| Perda sob carga do consolidado | **≤ 5%** | `MaxFailurePercent` | Respostas falhas / total no gate |
| Latência com cache | **95%** &lt; **2 s** | `MaxCachedP95LatencyMs` | `reporting.read.duration{cache="hit"}` |
| Latência sem cache | **95%** &lt; **5 s** | `UncachedReportSeconds` | `reporting.read.duration{cache="miss"}` |
| Latência leitura cacheada (p50) | &lt; **200 ms** | `MaxCachedP50LatencyMs` | Gate após aquecimento do cache |
| Erros HTTP de servidor | **5xx** &lt; **1%** | `MaxServerErrorPercent` | `reporting.requests.total{status_class="5xx"}` |
| Consistência de exportação | **100%** iguais à UI | — | Testes de integração (spec 003 SC-003) |

Constantes em `ReportingSlo.cs`. Alertas e PromQL em `ReportingAlertCatalog.cs`.

## Catálogo de métricas customizadas

| Métrica | Serviço | Tipo | Tags | Pilar |
|---------|---------|------|------|-------|
| `reporting.requests.total` | reporting-api | Counter | `method`, `route`, `status_class` | Disponibilidade |
| `reporting.read.duration` | reporting-api | Histogram | `cache`, `outcome` | Performance |
| `reporting.cache.hits` | reporting-api | Counter | — | Performance |
| `reporting.cache.misses` | reporting-api | Counter | — | Performance |
| `reporting.cache.invalidations` | reporting-api | Counter | — | Confiabilidade |
| `reporting.export.successes` | reporting-api | Counter | `format` | Disponibilidade |
| `reporting.export.failures` | reporting-api | Counter | `format`, `error_type` | Disponibilidade |
| `reporting.messages.consumed` | reporting-worker | Counter | — | Confiabilidade |
| `reporting.messages.failures` | reporting-worker | Counter | `error_type` | Confiabilidade |
| `reporting.projection.duration` | reporting-worker | Histogram | `outcome` | Performance |
| `reporting.pipeline.duration` | reporting-worker | Histogram | — | Performance |
| `reporting.sqs.visible_messages` | reporting-worker | Gauge | — | Confiabilidade |
| `reporting.sqs.in_flight_messages` | reporting-worker | Gauge | — | Confiabilidade |

## Alertas (Prometheus)

| Alerta | Severidade | Componente | Expressão (resumo) |
|--------|------------|------------|----------------------|
| `ReportingServerErrorRateHigh` | critical | reporting-api | taxa 5xx &gt; 1% por 5 min |
| `ReportingCachedReadP95High` | warning | reporting-api | p95 cache hit &gt; 2 s por 5 min |
| `ReportingUncachedReadP95High` | warning | reporting-api | p95 cache miss &gt; 5 s por 5 min |
| `ReportingExportFailuresSustained` | warning | reporting-api | falhas CSV/PDF sustentadas por 5 min |
| `ReportingCacheMissRateHigh` | warning | reporting-api | taxa miss &gt; 50% com tráfego sustentado |
| `ReportingProjectionFailuresSustained` | critical | reporting-worker | falhas SQS sustentadas por 5 min |
| `ReportingProjectionP95High` | warning | reporting-worker | p95 projeção &gt; 1 s por 5 min |
| `ReportingPipelineP95High` | warning | reporting-worker | p95 pipeline &gt; 5 s por 5 min |

Alertas transversais do pipeline (`MessagingPipelineSqsBacklogSustained`, etc.) em `prometheus/alerts/pipeline.yml`.

Definições: `infra/observability/prometheus/alerts/reporting.yml`.

## Gate de teste de carga

Executar localmente (stack ativa):

```powershell
.\scripts\run-reporting-load-test.ps1
```

Gates automáticos: fail % ≤ 5%, **p50** &lt; 200 ms, **p95** &lt; 2000 ms (após aquecimento sequencial do cache).

**Teste local:** desabilitar rate limiting global na `reporting-api` (`Security:RateLimitingEnabled=false` em Development / AppHost).

Logs: `tests/CashFlow.Reporting.Benchmarks/reports/`.

## Pipeline de exportação

- **Prometheus**: `GET /metrics` na `reporting-api` quando `Observability:PrometheusEnabled=true`.
- **OTLP**: métricas do worker via `CASHFLOW_OTEL_COLLECTOR_ENDPOINT` → collector → Prometheus/Grafana.
- Meter: `CashFlow.Reporting`.
- Dashboard Grafana: `infra/observability/grafana/dashboards/reporting-api.json`.

## Objetivos de nível de serviço (SLO)

- **Disponibilidade**: `/ready` saudável quando SQL de reporting e Redis (se habilitado) estão acessíveis.
- **Leitura degradada**: Redis indisponível → fallback SQL sem totais obsoletos silenciosos.
- **Consistência**: gráficos, totais, CSV e PDF derivam do mesmo payload consolidado.

## Documentos relacionados

- Constituição: `docs/constitution.md`
- Spec: `specs/003-feature-consolidated-report/spec.md`
- Pipeline: `docs/messaging-pipeline-observability.md`
- SLO Transactions: `docs/transactions-slo.md`
