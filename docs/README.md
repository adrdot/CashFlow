# Documentação — Cash Flow

Índice da documentação arquitetural e operacional do projeto.

## Decisões arquiteturais (ADRs)

Governança: [ADR 000](adr/000-governanca-decisoes-arquiteturais.md) · [template](adr/template.md)

| ADR | Categoria | Título |
|-----|-----------|--------|
| [000](adr/000-governanca-decisoes-arquiteturais.md) | Governança | Critérios e escopo das ADRs |
| [001](adr/001-arquitetura-estrutural-e-dados.md) | **Arquitetural** | Microsserviços, Clean/Hexagonal, CQRS, NFR-01 |
| [002](adr/002-infraestrutura-stack-recursos.md) | **Infraestrutura** | EventStore, SNS/SQS, relay, SQL, Redis |
| [003](adr/003-seguranca-cognito-jwt.md) | **Segurança** | Cognito + JWT |

## SLO / SLI

| Documento | Escopo |
|-----------|--------|
| [transactions-slo.md](transactions-slo.md) | Caminho de escrita (Transactions API) |
| [reporting-slo.md](reporting-slo.md) | Caminho de leitura (consolidado diário) |
| [messaging-pipeline-observability.md](messaging-pipeline-observability.md) | Observabilidade do pipeline EventStore → SQS |
| [observability-prometheus-canonical.md](observability-prometheus-canonical.md) | Prometheus como fonte canônica de alertas (vs CloudWatch placeholder) |

## Diagramas C4

| Diagrama | Arquivo |
|----------|---------|
| Contexto | [c4/context.md](c4/context.md) |
| Containers | [c4/containers.md](c4/containers.md) |
| Fluxos de dados | [c4/data-flows.md](c4/data-flows.md) |

## Planejamento

| Documento | Descrição |
|-----------|-----------|
| [roadmap.md](roadmap.md) | Evoluções futuras consolidadas |

## Specs de features

Localizadas em [`../specs/`](../specs/) — cada `spec.md` descreve requisitos originais; specs 002/003 incluem nota de supersessão (implementação usa EventStore + SNS/SQS, não SQL/RabbitMQ no caminho de escrita).

## Contribuição

| Documento | Descrição |
|-----------|-----------|
| [CONTRIBUTING.md](CONTRIBUTING.md) | O que versionar, lint, formatação e auditoria de segurança |

## Infraestrutura

Scripts e manifests em [`../infra/`](../infra/) — LocalStack, Cognito local, stack de transações, observabilidade (Prometheus/Grafana) e templates AWS.

## Constituição do projeto

[`constitution.md`](constitution.md)
