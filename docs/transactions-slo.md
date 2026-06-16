# Transactions API — SLO / SLI

Baseline alinhada ao spec `002-feature-cash-flow`. As metas de throughput da constituição (**50 RPS**, **5% de perda**) aplicam-se **apenas ao consolidado (reporting)** — ver `reporting-slo.md`.

## Pilares

| Pilar | Objetivo | Sinais primários |
|-------|----------|------------------|
| **Performance** | Escritas rápidas e previsíveis | `transactions.persistence.duration`, `transactions.end_to_end.duration` |
| **Disponibilidade** | API acessível; poucos erros de servidor | `http_server_request_duration_seconds`, health `/ready` |
| **Confiabilidade** | Escritas duráveis; mensageria se recupera | `transactions.persistence.failures`, contadores de publish |

## Indicadores de nível de serviço (SLI)

| SLI | Meta | Constante | Medição |
|-----|------|-----------|---------|
| Gravação ponta a ponta | **95%** &lt; **5 s** | `EndToEndRecordingPercentile`, `EndToEndRecordingSeconds` | `transactions.end_to_end.duration` |
| Latência de persistência | **p95** &lt; **200 ms** | `MaxPersistenceLatencyMs`, `PersistenceLatencyPercentile` | `transactions.persistence.duration{outcome="success"}` |
| Erros HTTP de servidor | **5xx** &lt; **1%** | `MaxServerErrorPercent` | `http_server_request_duration_seconds{http_response_status_code=~"5.."}` / total |
| Falhas de publish SNS | **0** sustentado &gt; **5 min** | `PublishFailureSustainedMinutes` | `transactions.events.publish_failures` |
| Replay idempotente | Mesma `Idempotency-Key` → mesma transação | — | Testes de integração + `transactions.idempotent_replays` |

> **Nota:** erros de validação do cliente (`4xx`) ficam **fora** do SLI de erro de servidor.

Thresholds: `docs/transactions-slo.md` (tabela acima), `TransactionLoadTestSloGates.cs` (benchmarks). Alertas: `infra/observability/prometheus/alerts/transactions.yml`.

## Catálogo de métricas customizadas

| Métrica | Tipo | Tags | Pilar |
|---------|------|------|-------|
| `http_server_request_duration_seconds` | Histogram (OTEL) | `http_response_status_code`, … | Disponibilidade |
| `transactions.created` | Counter | `type` | Confiabilidade |
| `transactions.persistence.failures` | Counter | `stage` | Confiabilidade |
| `transactions.idempotent_replays` | Counter | — | Confiabilidade |
| `transactions.events.published` | Counter | — | Confiabilidade |
| `transactions.events.publish_failures` | Counter | `error_type` | Confiabilidade |
| `transactions.persistence.duration` | Histogram | `outcome` | Performance |
| `transactions.eventstore.append.duration` | Histogram | `outcome` | Performance |
| `transactions.publish.duration` | Histogram | `outcome` | Performance |
| `transactions.end_to_end.duration` | Histogram | — | Performance |
| `transactions.relay.subscription_lag` | Gauge | — | Confiabilidade |
| `transactions.relay.subscription_in_flight` | Gauge | — | Confiabilidade |
| `transactions.relay.parked_messages` | Gauge | — | Confiabilidade |

## Alertas (Prometheus)

| Alerta | Severidade | Expressão (resumo) |
|--------|------------|-------------------|
| `TransactionsPersistenceP95High` | warning | p95 persistência &gt; 200 ms por 5 min |
| `TransactionsEndToEndP95High` | warning | p95 E2E &gt; 5 s por 5 min |
| `TransactionsServerErrorRateHigh` | critical | taxa 5xx &gt; 1% por 5 min |
| `TransactionsPublishFailuresSustained` | critical | falhas SNS sustentadas por 5 min |
| `TransactionsPublishFailureRateHigh` | warning | falhas SNS &gt; 1% por 5 min |
| `TransactionsPersistenceFailuresDetected` | warning | qualquer falha de persistência em 5 min |

Definições: `infra/observability/prometheus/alerts/transactions.yml`.

## Pipeline de exportação

- **Prometheus**: `GET /metrics` quando `Observability:PrometheusEnabled=true` (padrão).
- **OTLP**: habilitado quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido (CloudWatch/Grafana via collector).
- Meter customizado: `CashFlow.Transactions`.

## Objetivos de nível de serviço (SLO)

- **Disponibilidade**: `/ready` saudável quando EventStore e SNS estão acessíveis.
- **Durabilidade**: HTTP 200 somente após append durável no EventStore (`TransactionRecorded`).
- **Recuperabilidade**: relay SNS com retry via NACK na persistent subscription; Reporting idempotente por `TransactionId`.

## Stack de observabilidade local

Iniciada automaticamente por `scripts/run-full-local.ps1` (Prometheus + Grafana).

```powershell
# Stack local completa (inclui observabilidade)
.\scripts\run-full-local.ps1

# Sem Prometheus/Grafana
.\scripts\run-full-local.ps1 -SkipObservability

# Somente observabilidade (opcional)
.\infra\observability\start-observability.ps1 -MetricsTarget host.docker.internal:5100

# Scrape HTTPS (mesma URL dos testes de carga; cert dev ignorado no Prometheus)
.\infra\observability\start-observability.ps1 -MetricsTarget host.docker.internal:7093 -MetricsScheme https
```

- Prometheus: http://localhost:9090  
- Grafana: http://localhost:3000 (admin / admin) → dashboards **Transactions API** e **Messaging Pipeline (EventStore → SQS)**

Vazão do pipeline e profundidade SQS: ver `messaging-pipeline-observability.md`.

**Scrape Prometheus:** padrão HTTP `http://host.docker.internal:5100/metrics` (mais simples em dev). HTTPS em `https://host.docker.internal:7093/metrics` com `tls_config.insecure_skip_verify` (certificado dev não confiável no container). Use `-ObservabilityHttps` no `run-full-local.ps1`, ou `-MetricsScheme https` / `https://host.docker.internal:7093` no `start-observability.ps1`.

| Verificação | URL |
|-------------|-----|
| Saúde do target Prometheus | http://localhost:9090/targets |
| Métricas (HTTP, padrão) | http://localhost:5100/metrics |
| Métricas (HTTPS, opcional) | https://localhost:7093/metrics |

## Carga exploratória (não são gates de SLO)

```powershell
.\scripts\run-transactions-load-test.ps1
```

Defaults do benchmark: `TransactionLoadTestDefaults.cs` (sondagem de capacidade, não throughput de reporting).

## Runbook operacional (resumo)

| Sintoma | Verificar | Ação |
|---------|-----------|------|
| `TransactionsPublishFailuresSustained` | logs relay, `transactions.events.publish_failures` | Inspecionar SNS/subscription EventStore; parked messages |
| `TransactionsPersistenceP95High` | painéis latência EventStore | Escalar EventStore; rede |
| `TransactionsServerErrorRateHigh` | painel 5xx + traces | Dependências `/ready` |
| `TransactionsEndToEndP95High` | duração publish | Corrigir SNS; subscription lag |
| `MessagingPipelineRelayLagSustained` | `transactions.relay.subscription_lag` | Escalar relay; SNS / parked |
| `MessagingPipelineSqsBacklogSustained` | `aws_sqs_approximate_number_of_messages_visible_average` | Escalar consumer Reporting; erros projeção |
| `MessagingPipelineThroughputMismatch` | painel throughput | Ver `messaging-pipeline-observability.md` |
| Grafana vazio após teste de carga | targets Prometheus | `transactions-api` UP; HTTP `:5100` ou HTTPS `:7093` |
