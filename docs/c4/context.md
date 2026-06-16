# C4 — Nível 1: Contexto

Sistema de controle de fluxo de caixa para comerciantes registrarem lançamentos e consultarem consolidado diário.

```mermaid
flowchart TB
    merchant([Comerciante])
    cashflow[Cash Flow]

    cognito[Amazon Cognito]
    eventstore[(EventStoreDB)]
    aws_messaging[SNS / SQS]
    sql[(SQL Server)]
    redis[(Redis)]
    obs[Prometheus / Grafana]

    merchant --> cashflow
    cashflow --> cognito
    cashflow --> eventstore
    cashflow --> aws_messaging
    cashflow --> sql
    cashflow --> redis
    cashflow --> obs
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
