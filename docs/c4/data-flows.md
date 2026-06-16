# Fluxos de dados

## Caminho de escrita (lançamentos)

```text
POST /api/transactions
    │  JWT + validação domínio
    ▼
CreateTransactionHandler
    │  append TransactionRecorded (idempotency-key opcional)
    ▼
EventStoreDB ──► HTTP 200 (sucesso ao comerciante)
    │
    │  assíncrono — falha aqui NÃO reverte o lançamento
    ▼
Transactions.Relay (persistent subscription)
    ▼
Amazon SNS
    ▼
Amazon SQS (+ DLQ)
    ▼
Reporting.Worker
    │  INSERT ProjectedTransactions + UPSERT DailySummaries
    │  invalida Redis report:{userId}:{date}
    ▼
reporting-db
```

**Garantia NFR:** Transactions API não chama Reporting API/Worker no request síncrono. Ver [ADR 001 § Isolamento NFR-01](../adr/001-arquitetura-estrutural-e-dados.md#4-isolamento-nfr-01--decomposição-writeread).

## Caminho de leitura (consolidado diário)

```text
GET /api/reports/daily?date=YYYY-MM-DD
    │  JWT → UserId
    ▼
Reporting.Api
    ├─► Cache Redis (hit)? → resposta cacheada (&lt; 200 ms meta)
    └─► Cache miss / modo degradado → SQL DailySummaries (O(1))
            ▼
        JSON + gráficos (Web) / exportação CSV / PDF
```

## Observabilidade do pipeline

Métricas cruzadas: `transactions.created` → `transactions.events.published` → `reporting.messages.consumed`.

Detalhes: [`../messaging-pipeline-observability.md`](../messaging-pipeline-observability.md).
