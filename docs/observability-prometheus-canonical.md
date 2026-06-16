# Observabilidade — Prometheus como fonte canônica

## Decisão

**Prometheus + Grafana** (`infra/observability/`) é a fonte canônica de **métricas e alertas** para CashFlow.

Os alarmes CloudWatch customizados em `infra/localstack/setup-monitoring.ps1` nos namespaces `CashFlow/Reporting`, `CashFlow/Security` e `CashFlow/Http` **não estão conectados** às aplicações: o código publica logs (CloudWatch Logs) e métricas via OTLP/Prometheus, mas **não** chama `PutMetricData` para esses namespaces.

## O que usar

| Sinal | Fonte canônica | Notas |
|-------|----------------|-------|
| SLOs de domínio (cache, projeção, persistência) | `infra/observability/prometheus/alerts/*.yml` + `docs/*-slo.md` | Thresholds só em YAML/docs |
| HTTP 5xx / RPS | OTEL `http_server_request_duration_seconds` | Instrumentação ASP.NET Core em ServiceDefaults |
| Profundidade SQS | CloudWatch `AWS/SQS` via **YACE** → Prometheus | `aws_sqs_*_average` em `infra/observability/yace/`; LocalStack em Docker |
| DLQ com mensagens | Alarme `cashflow-dlq-messages-visible` em `setup-monitoring.ps1` | Namespace `AWS/SQS` nativo — **mantido** |
| Auth failures | Logs estruturados | Alarme CW `CashFlow/Security` é placeholder; usar Prometheus/log filters no futuro |

## Alarmes LocalStack a ignorar (placeholder)

Estes alarmes em `setup-monitoring.ps1` existem para demonstração LocalStack e **não disparam** com o stack atual:

- `cashflow-auth-failures-high` (`CashFlow/Security`)
- `cashflow-api-5xx-high` (`CashFlow/Http`)
- `cashflow-reporting-projection-failures` (`CashFlow/Reporting`)
- `cashflow-reporting-export-failures` (`CashFlow/Reporting`)
- `cashflow-reporting-read-latency-high` (`CashFlow/Reporting`)

Equivalentes operacionais: `reporting.yml`, `transactions.yml`, `pipeline.yml`.

## Conectar CloudWatch no futuro (opcional)

1. **Metric filters** em log groups para auth failures.
2. **ADOT / OTEL collector** exportando métricas de app para CloudWatch.
3. **YACE** (`cashflow-yace` no compose de observabilidade) para `AWS/SQS`, RDS, etc. → Prometheus — **já configurado** para LocalStack em dev.

## Documentos relacionados

- `docs/reporting-slo.md`
- `docs/transactions-slo.md`
- `docs/messaging-pipeline-observability.md`
