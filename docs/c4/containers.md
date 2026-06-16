# C4 — Nível 2: Containers

Decomposição interna do sistema Cash Flow.

```mermaid
flowchart TB
    user([Usuario])

    subgraph cashflow [Cash Flow]
        web[CashFlow.Web]
        auth[CashFlow.Auth.Api]
        tx_api[Transactions.Api]
        tx_relay[Transactions.Relay]
        rpt_api[Reporting.Api]
        rpt_worker[Reporting.Worker]
    end

    es[(EventStoreDB)]
    sns[Amazon SNS]
    sqs[Amazon SQS]
    sql[(SQL Server)]
    redis[(Redis)]
    cognito[Amazon Cognito]

    user --> web
    web --> auth
    web --> tx_api
    web --> rpt_api
    auth --> cognito
    tx_api --> es
    tx_relay --> es
    tx_relay --> sns
    sns --> sqs
    rpt_worker --> sqs
    rpt_worker --> sql
    rpt_worker --> redis
    rpt_api --> sql
    rpt_api --> redis
```

## Escalabilidade horizontal (local / produção)

| Container | Estratégia |
|-----------|------------|
| Transactions API | Stateless; réplicas atrás de load balancer |
| Transactions Relay | `WithReplicas(N)` no AppHost; subscription compartilhada |
| Reporting API | Stateless; cache Redis compartilhado |
| Reporting Worker | `WithReplicas(N)`; competição na fila SQS |

Em **dev**, escala via `WithReplicas(N)` no AppHost (.NET Aspire). Em **produção**, ver [`../roadmap.md`](../roadmap.md) (Kubernetes + ALB/Ingress).
