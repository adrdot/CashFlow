# Roadmap — Evoluções futuras

Consolidação de itens deferidos e evoluções arquiteturais além do escopo mínimo do desafio.

**Última atualização:** 2026-06-16

---

## Infraestrutura e escala (ARQ-01)

| Item | Descrição | Prioridade |
|------|-----------|------------|
| **Kubernetes** | Deploy de Auth, Transactions API, Relay, Reporting API/Worker com HPA | Alta |
| **Load balancer HTTP** | ALB / Ingress na Reporting API e Transactions API para 50+ RPS multi-instância | Alta |
| **EventStore cluster** | Cluster multi-node com backup e monitoramento de subscription lag | Média |
| **Redis cluster** | ElastiCache ou Redis Cluster para cache de relatórios | Média |

## Segurança (ARQ-03)

| Item | Descrição | Prioridade |
|------|-----------|------------|
| **JWT produção** | Remover `SigningKey` de `appsettings.json`; usar Secrets Manager | Alta |
| **Cognito Admin** | Implementar `AdminCreateUser`, disable/enable, grupos (hoje scaffolding) | Média |
| **Federação enterprise** | AD / SAML / OIDC via Cognito IdP | Baixa |
| **WAF / API Gateway** | Rate limit edge + proteção OWASP em produção AWS | Média |

Detalhes Cognito Admin: [`../specs/005-adicionar-seguran-front/deferred.md`](../specs/005-adicionar-seguran-front/deferred.md).

## Dados e performance

| Item | Descrição | Prioridade |
|------|-----------|------------|
| **DynamoDB idempotência** | Reavaliar se backlog SQS sustentado mesmo com N workers (critério ADR 002 §5) | Baixa |
| **Read replicas SQL** | Separar projeção (write) de leitura de relatórios em escala | Média |
| **Chaos testing** | Simular queda de Redis, SQS, EventStore em ambiente de staging | Média |

## Qualidade e entrega

| Item | Descrição | Prioridade |
|------|-----------|------------|
| **CI coverage gate** | Cobertura mínima + publicação de relatório | Média |
| **Contract tests cross-service** | Expandir Pact para reporting e auth | Baixa |
| **GitHub público** | ~~Publicar repositório e badge CI no README~~ **Concluído** — [github.com/adrdot/CashFlow](https://github.com/adrdot/CashFlow) | — |

## Já implementado (referência)

- Repositório público GitHub + CI Actions (2026-06-16)
- OAuth2 authorization-code / Hosted UI (2026-06-12)
- EventStore + SNS/SQS + projeção SQL (ADR 001–003)
- Observabilidade Prometheus/Grafana + OTEL pipeline
- Teste de carga NBomber com gates SLO (`CashFlow.Reporting.Benchmarks`)
