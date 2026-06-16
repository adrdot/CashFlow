# C4 — Nível 2: Containers

Decomposição interna do sistema Cash Flow.

```mermaid
C4Container
    title Diagrama de Containers — Cash Flow

    Person(user, "Usuário", "Comerciante")

    Container_Boundary(cashflow, "Cash Flow") {
        Container(web, "CashFlow.Web", "Blazor", "UI: login, lançamentos, relatórios")
        Container(auth, "CashFlow.Auth.Api", "ASP.NET Core", "Login, OAuth2, JWT, MFA")
        Container(tx_api, "CashFlow.Transactions.Api", "ASP.NET Core", "POST /api/transactions → EventStore")
        Container(tx_relay, "CashFlow.Transactions.Relay", ".NET Worker", "Persistent sub → SNS")
        Container(rpt_api, "CashFlow.Reporting.Api", "ASP.NET Core", "GET /api/reports/daily, exportação")
        Container(rpt_worker, "CashFlow.Reporting.Worker", ".NET Worker", "SQS → projeção SQL + invalidação cache")
    }

    ContainerDb(es, "EventStoreDB", "Armazenamento de eventos", "Streams por usuário")
    ContainerDb(sns, "Amazon SNS", "Tópico", "Fan-out TransactionRecorded")
    ContainerDb(sqs, "Amazon SQS", "Fila", "Consumo pelo reporting worker")
    ContainerDb(sql, "SQL Server", "reporting-db", "DailySummaries, ProjectedTransactions")
    ContainerDb(redis, "Redis", "Cache", "report:{userId}:{date}")
    Container_Ext(cognito, "Cognito", "IdP")

    Rel(user, web, "HTTPS")
    Rel(web, auth, "JSON/HTTPS")
    Rel(web, tx_api, "JSON/HTTPS")
    Rel(web, rpt_api, "JSON/HTTPS")
    Rel(auth, cognito, "OAuth / Admin API")
    Rel(tx_api, es, "Append evento", "HTTP/gRPC")
    Rel(tx_relay, es, "Persistent subscription")
    Rel(tx_relay, sns, "Publish")
    Rel(sns, sqs, "Subscription")
    Rel(rpt_worker, sqs, "Poll")
    Rel(rpt_worker, sql, "UPSERT projeção")
    Rel(rpt_worker, redis, "Invalida cache")
    Rel(rpt_api, sql, "Leitura O(1) DailySummaries")
    Rel(rpt_api, redis, "Cache-aside")
```

## Escalabilidade horizontal (local / produção)

| Container | Estratégia |
|-----------|------------|
| Transactions API | Stateless; réplicas atrás de load balancer |
| Transactions Relay | `WithReplicas(N)` no AppHost; subscription compartilhada |
| Reporting API | Stateless; cache Redis compartilhado |
| Reporting Worker | `WithReplicas(N)`; competição na fila SQS |

Ver [`../roadmap.md`](../roadmap.md) para Kubernetes e ALB.
