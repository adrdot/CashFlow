# Observabilidade do pipeline de mensageria (EventStore → SNS → SQS → Reporting)

Como verificar se a vazão de escrita acompanha o consumo downstream. Decisões de stack: [ADR 002 — Infraestrutura](adr/002-infraestrutura-stack-recursos.md).

## Estágios do pipeline

```text
POST /api/transactions
    │  transactions.requests.total
    │  transactions.persistence.duration
    │  transactions.eventstore.append.duration
    ▼
EventStore append ───────────────────────── transactions.created
    │
    │  transactions.relay.subscription_lag
    │  transactions.relay.subscription_in_flight
    │  transactions.relay.parked_messages
    ▼
Relay → SNS ─────────────────────────────────── transactions.events.published
    │  transactions.publish.duration
    │  transactions.end_to_end.duration
    ▼
SNS → SQS ───────────────────────────────────── reporting.sqs.visible_messages
    │                                          reporting.sqs.in_flight_messages
    ▼
Reporting Worker ───────────────────────────── reporting.messages.consumed
    │  reporting.projection.duration
    │  reporting.pipeline.duration
    ▼
reporting-db
         ↑
   GET reports (reporting-api)
```

## Catálogo de métricas

### Transactions API — caminho de escrita

| Métrica | Tipo | Tags | Finalidade |
|---------|------|------|------------|
| `transactions.requests.total` | Counter | `method`, `route`, `status_class` | Carga HTTP; base para taxa de erro 5xx |
| `transactions.created` | Counter | `type` | **Entrada do pipeline** — eventos novos no EventStore (exclui replay idempotente) |
| `transactions.idempotent_replays` | Counter | — | Replay da mesma `Idempotency-Key` (sem evento novo) |
| `transactions.persistence.failures` | Counter | `stage` | Falhas no caminho de escrita (`eventstore`, etc.) |
| `transactions.persistence.duration` | Histogram | `outcome` | Latência até HTTP 200 |
| `transactions.eventstore.append.duration` | Histogram | `outcome` | Latência do append no EventStore |

### Transactions Relay

| Métrica | Tipo | Exportação | Finalidade |
|---------|------|------------|------------|
| `transactions.events.published` | Counter | OTLP | Eventos entregues ao SNS |
| `transactions.events.publish_failures` | Counter | OTLP | Falhas de publish no SNS (NACK Retry) |
| `transactions.publish.duration` | Histogram | OTLP | Latência do `PublishAsync` no SNS |
| `transactions.end_to_end.duration` | Histogram | OTLP | `CreatedAtUtc` → SNS OK |
| `transactions.relay.subscription_lag` | Gauge | OTLP | Eventos pendentes em `cashflow-sns-relay` |
| `transactions.relay.subscription_in_flight` | Gauge | OTLP | Em processamento antes do ACK |
| `transactions.relay.parked_messages` | Gauge | OTLP | Mensagens parked — intervenção manual |

### Reporting Worker

| Métrica | Tipo | Exportação | Finalidade |
|---------|------|------------|------------|
| `reporting.messages.consumed` | Counter | OTLP | **Saída do pipeline** — SQS projetada no SQL |
| `reporting.messages.failures` | Counter | OTLP | Falhas de projeção |
| `reporting.projection.duration` | Histogram | OTLP | Por mensagem SQS + SQL |
| `reporting.pipeline.duration` | Histogram | OTLP | `CreatedAtUtc` → reporting-db OK |
| `reporting.sqs.visible_messages` | Gauge | OTLP | Profundidade da fila |
| `reporting.sqs.in_flight_messages` | Gauge | OTLP | Visibility timeout / em voo |

### Reporting API

| Métrica | Tipo | Exportação | Finalidade |
|---------|------|------------|------------|
| `reporting.requests.total` | Counter | Prometheus | Carga HTTP |
| `reporting.read.duration` | Histogram | Prometheus | Latência de leitura (`cache`, `outcome`) |
| `reporting.cache.hits` / `misses` / `invalidations` | Counter | Prometheus/OTLP | Comportamento do cache |
| `reporting.export.successes` / `failures` | Counter | Prometheus | Exportação CSV/PDF |

Regras de alerta Prometheus: `infra/observability/prometheus/alerts/pipeline.yml`, `transactions.yml`, `reporting.yml`. Grafana: **Messaging Pipeline (EventStore → SQS)**.

Workers são hosts em background (sem HTTP). `Observability:PrometheusEnabled=false`; métricas via **OTLP**.

## PromQL (vazão)

| Sinal | Expressão |
|-------|-----------|
| Entrada EventStore | `sum(rate(transactions_created_total[1m]))` |
| Saída relay (SNS) | `sum(rate(transactions_events_published_total[1m]))` |
| Saída pipeline (Reporting) | `sum(rate(reporting_messages_consumed_total[1m]))` |
| Desbalanceamento | `sum(rate(transactions_created_total[1m])) - sum(rate(reporting_messages_consumed_total[1m]))` |
| Taxa de falha SNS | `sum(rate(transactions_events_publish_failures_total[5m]))` |
| Taxa de falha projeção | `sum(rate(reporting_messages_failures_total[5m]))` |

## SLIs e metas

| SLI | Meta | Sinal primário |
|-----|------|----------------|
| Persistência p95 | &lt; 200 ms | `transactions.persistence.duration` |
| API → SNS p95 | &lt; 5 s (SLO) | `transactions.end_to_end.duration` |
| E2E até reporting | p95 &lt; 5 s | `reporting.pipeline.duration` |
| Backlog relay | 0 sustentado &gt; 5 min | `transactions.relay.subscription_lag` |
| Falhas de publish | 0 sustentado &gt; 5 min | `transactions.events.publish_failures` |
| Mensagens parked | 0 sustentado | `transactions.relay.parked_messages` |

Detalhes: `transactions-slo.md`, `reporting-slo.md`.

## Guia de interpretação

Use **três taxas** (`created`, `published`, `consumed`) e **dois backlogs** (subscription lag, profundidade SQS) em conjunto.

### Matriz de sintomas (carga estável)

| `created` | `published` | `consumed` | Relay lag | SQS visible | Causa provável |
|-----------|-------------|------------|-----------|-------------|----------------|
| ≈ input | ≈ created | ≈ created | ~0 | ~0 | **Pipeline saudável** |
| ≈ input | **<** created | **<** created | **↑** | baixo | **Gargalo no relay ou SNS** |
| ≈ input | ≈ created | **<** created | ~0 | **↑** | **Gargalo no Reporting** |
| ≈ input | ≈ created | ≈ created | ~0 | **↑** in-flight | Consumer lento ou visibility presa |
| **<** input | ≈ created | ≈ created | ~0 | ~0 | **Gargalo no caminho de escrita** |
| **>** consumed, sem POSTs novos | variável | variável | variável | variável | **Drenagem de backlog** após restart |

### Estágio do relay

```text
rate(transactions_created)  ──►  rate(transactions_events_published)
         │                                    │
         └──────── subscription_lag ◄────────┘
```

| Relação observada | Relay lag | publish_failures | Diagnóstico |
|-------------------|-----------|------------------|-------------|
| `created` > `published` sustentado | ↑ | baixo | Capacidade do relay ou catch-up |
| `created` > `published` sustentado | ↑ | ↑ | SNS indisponível / throttling |
| `published` > `created`, sem POSTs | ↓ | baixo | Drenagem de backlog (normal) |
| `created` ≈ `published`, lag ~0 | ~0 | zero | Relay OK; se Reporting atrasa, gargalo é após o SNS |

### ACK/NACK

- **ACK** após SNS OK → incrementa `events.published`; reduz o lag.
- **NACK Retry** → não incrementa `published`; incrementa `publish_failures`; reentrega.
- Evento inválido → ACK sem `published` (gap benigno).

### Cenários específicos

1. **Teste de carga parou mas métricas sobem** — drenagem de backlog; confirmar `increase(...[5m])` → 0.
2. **Idempotência vs throughput** — `requests.total` alto + `created` baixo → ver `idempotent_replays`.
3. **Latência vs backlog** — `end_to_end.duration` alto + lag ↑ → relay/SNS; `pipeline.duration` alto + SQS ↑ → Reporting/SQL.
4. **Mensagens parked** sustentadas → intervenção manual no EventStore.

### Alertas do pipeline

| Alerta | Arquivo | Significado |
|--------|---------|-------------|
| `MessagingPipelineRelayLagSustained` | `pipeline.yml` | Backlog no relay &gt; 100 por 5 min |
| `MessagingPipelineSqsBacklogSustained` | `pipeline.yml` | SQS não drena por 5 min |
| `MessagingPipelineThroughputMismatch` | `pipeline.yml` | \|created − consumed\| &gt; 1/s por 5 min |
| `TransactionsEndToEndP95High` | `transactions.yml` | p95 create→SNS &gt; 5 s |
| `TransactionsPublishFailuresSustained` | `transactions.yml` | Falhas SNS sustentadas |

## Prometheus + Grafana local

A stack Docker inclui ponte **OTEL Collector**:

```text
AppHost services --OTLP--> localhost:4318 (collector) --+--> Prometheus :8889/metrics
                                                        +--> Aspire Dashboard
```

```powershell
$env:CASHFLOW_OTEL_COLLECTOR_ENDPOINT = "http://127.0.0.1:4318"
.\infra\observability\start-observability.ps1
```

| Caminho de exportação | Métricas |
|-----------------------|----------|
| OTLP → collector → Prometheus | relay, reporting-worker, APIs |
| Aspire Dashboard | mesmo fluxo OTLP |

## Escala horizontal (réplicas)

| Serviço | Variável de ambiente | Padrão | HTTP |
|---------|----------------------|--------|------|
| `transactions-api` | `CASHFLOW_API_REPLICAS` | 1 | Sim |
| `transactions-relay` | `CASHFLOW_RELAY_REPLICAS` | 3 | Não |
| `reporting-api` | `CASHFLOW_API_REPLICAS` | 1 | Sim |
| `reporting-worker` | `CASHFLOW_REPORTING_WORKER_REPLICAS` | 3 | Não |

## Validar o pipeline

```powershell
.\scripts\validate-messaging-pipeline.ps1 -SkipCertificateCheck
.\scripts\validate-messaging-pipeline.ps1 -UsePrometheus
```

## Documentos relacionados

- [ADR 002 — Infraestrutura](adr/002-infraestrutura-stack-recursos.md)
- [ADR 001 — Arquitetura](adr/001-arquitetura-estrutural-e-dados.md)
- `transactions-slo.md` / `reporting-slo.md`
