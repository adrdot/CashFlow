# ADR 002: Infraestrutura e stack de recursos

**Categoria:** Infraestrutura

## Status

**Aceito** (2026-06-16). Consolida decisões de persistência, mensageria, relay e modelo de leitura.

## Data

2026-06-16

## Objetivos de negócio afetados

| ID | Objetivo | Impacto |
|----|----------|---------|
| RN-01 | Lançamentos | EventStore SSOT; relay assíncrono |
| RN-02 | Consolidado | Projeção SQL + Redis; consumer SQS |
| NFR-01 | Isolamento | Broker e reporting fora do POST |
| NFR-02 | 50 RPS no consolidado | `DailySummaries` O(1) + cache |

Forma estrutural: [ADR 001 — Arquitetura](001-arquitetura-estrutural-e-dados.md).

## Stack implementada

```text
Transactions.Api → EventStoreDB → HTTP 200
         ↓ (push, worker)
Transactions.Relay → SNS → SQS (+ DLQ) → Reporting.Worker → reporting-db
                                                              ↓ invalidate
Reporting.Api ← Redis cache-aside ← DailySummaries (SQL)
```

| Recurso | Papel | Ambiente |
|---------|-------|----------|
| EventStoreDB | SSOT lançamentos | Docker |
| Amazon SNS + SQS | Integração event-driven | LocalStack / AWS |
| SQL Server `reporting-db` | Projeções (modelo de leitura) | Docker |
| Redis | Cache consolidado | Docker |

---

## 1. Persistência na escrita — EventStore como SSOT

| Alternativa | Prós | Contras | Nota |
|-------------|------|---------|------|
| SQL como SSOT | Consultas ad hoc | UPDATE/DELETE; fan-out fraco | 2 |
| **EventStore + mensageria** | Append-only; desacoplamento | Ops cluster ES; consistência eventual | **5** |
| DynamoDB + Streams | Serverless | Imutabilidade por convenção | 3 |
| Kinesis + store auxiliar | Log escalável | Complexidade desproporcional | 2 |

**Decisão:** EventStoreDB como única fonte da verdade de lançamentos. SQL **apenas** em projeções.

---

## 2. Idempotência na escrita — `eventId` determinístico

Problema do modelo híbrido (legado): escrita dupla SQL + EventStore sem atomicidade; `transactions-db` bloqueava lançamentos.

| Opção | Decisão |
|-------|---------|
| SQL `(UserId, IdempotencyKey)` | **Rejeitado** — escrita dupla, +1 dependência |
| **`eventId` determinístico no EventStore** | **Adotado** — UUID v5 de `(userId, key)` |
| Stream idempotência separada | Rejeitado — 2 appends por transação |
| Scan da stream | Rejeitado — O(n) |

Regras: com `Idempotency-Key` → `eventId = f(userId, key)`; replay → `PersistenceOutcome.Replay`; sem key → `eventId = transactionId`. `transactions-db` **removido** da Transactions API.

---

## 3. Mensageria — SNS/SQS vs RabbitMQ vs Kafka

Spec original pedia RabbitMQ; constituição fixa SNS. Comparativo completo:

### Requisitos funcionais

| Requisito | SNS/SQS | RabbitMQ | Kafka |
|-----------|---------|----------|-------|
| Publicar `TransactionRecorded` | SNS topic | AMQP exchange | Kafka topic |
| Filtro de mensagem | Filter policies | Exchanges + routing | Topics/partições |
| Replay | DLQ redrive | DLX + requeue | Offset reset |
| Fan-out N consumers | Nativo | N filas + bindings | N consumer groups |
| Evoluibilidade | `ITransactionEventPublisher` | Novo adapter | Novo adapter |

### NFR por opção

| Dimensão | SNS + SQS | RabbitMQ | Kafka |
|----------|-----------|----------|-------|
| Ambiente dev/prod | LocalStack / AWS gerenciado | Container / cluster ops | KRaft pesado |
| Throughput (~50–100 RPS) | Suficiente | Suficiente | Overkill |
| Latência E2E → SNS | Baixa | Baixa | Baixa c/ overhead |
| Durabilidade | SQS + DLQ | Filas duráveis | Log commitado |
| Segurança | IAM / LocalStack keys | AMQP user/TLS | SASL/ACLs |
| Ops (back-pressure, replay) | SQS depth + ES subscription lag | Queue depth, DLX | Partitions, lag |
| Custo-benefício (volume atual) | **Alto** | Médio | Baixo |

### Placar ponderado

| Opção | Total |
|-------|-------|
| **SNS/SQS** | **~4,7** |
| RabbitMQ | ~3,5 |
| Kafka | ~3,3 |

**Decisão:** Amazon SNS + SQS. Publish via `SnsTransactionEventPublisher` no Relay; consume via `Reporting.Worker`; contrato `TransactionRecordedEvent` estável.

**Não adotar** RabbitMQ nem Kafka no escopo atual. EventStore permanece o log replayável.

---

## 4. Relay EventStore → SNS

| Caminho | Decisão |
|-------|---------|
| A — Outbox SQL (polling) | **Removido** — teto ~4 msg/s, duplicação payload |
| **A′ — Persistent subscription → SNS** | **Adotado** — push, ACK/NACK, alinhado ao SSOT |
| B — DynamoDB Streams | Rejeitado — muda SSOT |
| C — Kinesis | Rejeitado — custo/complexidade |

Subscription `cashflow-sns-relay`; filtro `cashflow-`; checkpoint no EventStore. `OutboxRelayBackgroundService` descontinuado.

---

## 5. Modelo de leitura (reporting) — SQL + Redis

Problema: `ProcessedProjections` + `AnyAsync` = 2 round-trips; agregação em memória inviável para 50 RPS.

| Aspecto | Decisão |
|---------|---------|
| Dedup projeção | PK `ProjectedTransactions.Id` + duplicate key catch |
| Pré-agregação | `DailySummaries` UPSERT na mesma transação SQL |
| Cache | Redis cache-aside; Worker invalida `report:{userId}:{date}` |
| Leitura | `DailySummaries` O(1); fallback SQL se Redis degraded |
| DynamoDB (spike) | **Rejeitado** no volume atual — ver benchmark `CashFlow.Reporting.Benchmarks` |

**Reavaliação DynamoDB:** backlog SQS sustentado + `reporting.projection.duration` p95 acima do SLO com N workers.

---

## Estado da implementação

| Componente | Estado |
|------------|--------|
| Append `TransactionRecorded` no EventStore | Implementado |
| Idempotência `eventId` determinístico | Implementado |
| Relay persistent subscription → SNS | Implementado |
| Consumer SQS → projeção SQL | Implementado |
| Redis cache + invalidação Worker | Implementado |
| `transactions-db` na Transactions API | Removido |

---

## Observabilidade

Métricas, PromQL, matriz de sintomas e alertas: [`messaging-pipeline-observability.md`](../messaging-pipeline-observability.md). SLOs: `transactions-slo.md`, `reporting-slo.md`.

---

## Consequências

### Positivas

- Stack coerente com constituição (EventStore + SNS)
- Comparativos documentados (broker, SSOT, relay, modelo de leitura)
- Adapter de mensageria permite troca futura sem reescrever domínio

### Negativas

- Consistência eventual EventStore → `reporting-db`
- Ops EventStore + LocalStack
- Replay de integração limitado a DLQ (histórico no EventStore)

---

## Critério de reavaliação

| Componente | Gatilho |
|------------|---------|
| Mensageria | &gt; 5.000 msg/s ou replay via broker obrigatório |
| Relay | — (decisão estável) |
| Modelo de leitura | Backlog SQS sustentado → DynamoDB só idempotência |
| SSOT | Requisito regulatório de SQL como única fonte |

---

## Referências

- [ADR 000 — Governança](000-governanca-decisoes-arquiteturais.md)
- [ADR 001 — Arquitetura](001-arquitetura-estrutural-e-dados.md)
- [`messaging-pipeline-observability.md`](../messaging-pipeline-observability.md)
- [`specs/002-feature-cash-flow/spec.md`](../../specs/002-feature-cash-flow/spec.md)
- `SnsTransactionEventPublisher`, `EventStoreTransactionRepository`, `EventStoreToSnsRelayBackgroundService`

## Histórico

| Versão | Data | Nota |
|--------|------|------|
| 1.0 | 2026-06-16 | Aceito; substitui ADRs arquivadas 001–005 (versões anteriores) |
