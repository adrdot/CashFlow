# C4 — Nível 1: Contexto

Sistema de controle de fluxo de caixa para comerciantes registrarem lançamentos e consultarem consolidado diário.

```mermaid
C4Context
    title Diagrama de Contexto — Cash Flow

    Person(merchant, "Comerciante", "Registra débitos/créditos e consulta saldo diário")
    System(cashflow, "Cash Flow", "Microsserviços C# com caminho de escrita isolado do reporting")

    System_Ext(cognito, "Amazon Cognito", "Autenticação OAuth2 / MFA (LocalStack/Cognito Local em dev)")
    System_Ext(eventstore, "EventStoreDB", "Fonte da verdade append-only dos lançamentos")
    System_Ext(aws_messaging, "SNS / SQS", "Integração assíncrona (LocalStack em dev)")
    System_Ext(sql, "SQL Server", "Modelo de leitura reporting-db")
    System_Ext(redis, "Redis", "Cache de relatórios consolidados")
    System_Ext(obs, "Prometheus / Grafana", "Métricas e dashboards operacionais")

    Rel(merchant, cashflow, "Usa via browser", "HTTPS")
    Rel(cashflow, cognito, "Autentica usuários")
    Rel(cashflow, eventstore, "Append TransactionRecorded")
    Rel(cashflow, aws_messaging, "Publica/consome eventos")
    Rel(cashflow, sql, "Projeções e leitura")
    Rel(cashflow, redis, "Cache-aside relatórios")
    Rel(cashflow, obs, "Exporta métricas OTLP/Prometheus")
```

## Responsabilidades externas

| Sistema | Papel |
|---------|-------|
| Cognito | Identidade, MFA, tokens JWT |
| EventStoreDB | Durabilidade e imutabilidade dos lançamentos |
| SNS/SQS | Desacoplamento temporal entre escrita e modelo de leitura |
| SQL Server | `DailySummaries` e detalhes projetados |
| Redis | Aceleração de `GET /api/reports/daily` |
| Prometheus/Grafana | SLI/SLO operacionais |
