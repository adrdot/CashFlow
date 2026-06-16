# Rastreabilidade de Gaps — Desafio Arquiteto de Software



Documento de acompanhamento para fechar lacunas entre o projeto **Cash Flow** e os critérios do desafio (requisitos obrigatórios, NFRs e pilares arquiteturais).



**Como usar:** marque cada item conforme concluir. A coluna **Evidência** deve apontar para arquivo, comando ou artefato verificável.



**Última revisão:** 2026-06-15 (validação automatizada + teste de carga NFR-02 + teste funcional RN-01/RN-02)



---



## Legenda de status



| Símbolo | Significado |

|---------|-------------|

| ✅ | Atende |

| ⚠️ | Parcial — ação necessária |

| ❌ | Não atende — bloqueante ou alto risco |

| 🔲 | Pendente de validação |



---



## 1. Requisitos técnicos obrigatórios (eliminatórios)



> Se algum item abaixo não for minimamente atendido, o teste pode ser **descartado**.



| ID | Requisito do desafio | Status | Gap / ação | Evidência esperada | Validação |

|----|----------------------|--------|------------|-------------------|-----------|

| RT-01 | Solução em **C#** | ✅ | — | `Aspire.CashFlow.slnx`, projetos `*.csproj` | ✅ `dotnet build Aspire.CashFlow.slnx -c Release` (2026-06-15) |

| RT-02 | **Testes** automatizados | ✅ | Integração Auth/Reporting exige Docker/Cognito/Redis | `tests/**` | ⚠️ Core verde; contrato Auth falha sem Cognito local |

| RT-03 | **Boas práticas** (SOLID, padrões) | ✅ | — | ADRs, camadas Application/Domain/Infrastructure | ✅ Revisão estrutural OK |

| RT-04 | **README** com instruções claras | ✅ | Publicar URL GitHub quando repo estiver no ar | [`README.md`](../README.md) | ✅ Links ADR/SLO/C4 existem |

| RT-05 | **Repositório público** (GitHub) | ⚠️ | **Ação externa:** publicar e atualizar URL no README | URL `https://github.com/<org>/<repo>` | ❌ README ainda com placeholder |

| RT-06 | **Documentação no repositório** | ✅ | — | `README.md`, `docs/**`, `docs/c4/**` | ✅ Índice e C4 verificados |



### Checklist RT-04 — README mínimo



- [x] Visão geral do problema (fluxo de caixa + consolidado diário)

- [x] Diagrama de arquitetura (ASCII ou Mermaid)

- [x] Pré-requisitos (Docker, .NET 10 SDK, PowerShell)

- [x] Como rodar localmente (`.\scripts\run-full-local.ps1`)

- [x] URLs dos serviços (Web, APIs, Aspire Dashboard, Grafana)

- [x] Credenciais demo (`admin@cashflow.docker` / `Pass@word1`)

- [x] Como executar testes (`dotnet test`)

- [x] Como executar teste de carga de reporting (50 RPS)

- [x] Links para ADRs (`docs/adr/`) e SLOs (`docs/*-slo.md`)

- [x] Seção **Evoluções futuras** (roadmap resumido)



---



## 2. Requisitos de negócio



| ID | Requisito | Status | Implementação | Validação |

|----|-----------|--------|---------------|-----------|

| RN-01 | Serviço de **controle de lançamentos** (débito/crédito) | ✅ | `CashFlow.Transactions.Api` | ✅ `CashFlowFunctionalFlowTests` — POST crédito/débito 200 |

| RN-02 | Serviço de **consolidado diário** | ✅ | `CashFlow.Reporting.Api`, `CashFlow.Reporting.Worker` | ✅ `CashFlowFunctionalFlowTests` — GET daily + exportação CSV/PDF |



**Validação funcional (API):**

- [x] Registrar crédito via `POST /api/transactions`

- [x] Registrar débito via `POST /api/transactions`

- [x] Consultar consolidado do dia com totais coerentes (`GET /api/reports/daily`)

- [x] Exportar CSV/PDF com totais iguais ao consolidado

- **Teste:** `tests/CashFlow.FunctionalTests/CashFlowFunctionalFlowTests.cs`

- **Comando:** `dotnet test tests/CashFlow.FunctionalTests --filter CashFlowFunctionalFlowTests`



---



## 3. Requisitos não-funcionais críticos



| ID | Requisito | Status | Gap / ação | Evidência esperada | Validação |

|----|-----------|--------|------------|-------------------|-----------|

| NFR-01 | Lançamentos **não caem** se reporting cair | ✅ | — | `ReportingAvailabilityIsolationTests.cs` | ✅ 3/3 testes (Release) |

| NFR-02 | Consolidado: **50 req/s**, **≤ 5% perda** | ✅ | Log commitado; gates p50/p95 | `load_50rps_2026-06-15_21.42.13.log` | ✅ 1500 OK, 0% fail, 50 RPS, p95 7 ms |



### Checklist NFR-01 — Isolamento de disponibilidade



- [x] Confirmar que `CreateTransactionHandler` não chama reporting (somente EventStore)

- [x] Confirmar que `Transactions.Api` usa `NullTransactionEventPublisher` (relay separado)

- [x] Confirmar `/ready` da Transactions depende só de EventStore

- [x] **Criar teste:** `ReportingAvailabilityIsolationTests`

- [x] Documentar cenário no README



### Checklist NFR-02 — Throughput consolidado



- [x] Stack local completa (`run-full-local.ps1`)

- [x] Dados seed ou transações pré-existentes para a data do teste (`2026-06-12`)

- [x] Executar `.\scripts\run-reporting-load-test.ps1`

- [x] Fail % ≤ 5% e p50 < 200 ms / p95 < 2000 ms

- [x] Salvar log em `tests/CashFlow.Reporting.Benchmarks/reports/`

- [x] Atualizar `docs/reporting-slo.md`

- [x] Referenciar comando no README



---



## 4. Pilares arquiteturais do desafio



| ID | Pilar | Status | Gap / ação | Artefato |

|----|-------|--------|------------|----------|

| ARQ-01 | **Escalabilidade** | ✅ | — | `docs/roadmap.md`, AppHost `WithReplicas` |

| ARQ-02 | **Resiliência** | ✅ | — | [ADR 002](adr/002-infraestrutura-stack-recursos.md), DLQ, idempotência |

| ARQ-03 | **Segurança** | ✅ | JWT prod no roadmap | [ADR 003](adr/003-seguranca-cognito-jwt.md), `docs/roadmap.md` |

| ARQ-04 | **Padrões arquiteturais** | ✅ | — | [ADR 001](adr/001-arquitetura-estrutural-e-dados.md) |

| ARQ-05 | **Integração** | ✅ | — | [ADR 002](adr/002-infraestrutura-stack-recursos.md) |

| ARQ-06 | **RNF + métricas** | ✅ | — | SLOs, `reporting.yml`, dashboard Grafana, CloudWatch local |

| ARQ-07 | **Documentação (ADRs, diagramas, fluxos)** | ✅ | — | `docs/c4/`, `docs/README.md`, ADRs 000–003 |



### Checklist ARQ-06 — Métricas e alarmes (reporting)

- [x] Métricas HTTP e leitura (`reporting.requests.total`, `reporting.read.duration`)
- [x] Catálogo de alertas — `infra/observability/prometheus/alerts/reporting.yml`
- [x] Regras Prometheus — `infra/observability/prometheus/alerts/reporting.yml`
- [x] Dashboard Grafana — `infra/observability/grafana/dashboards/reporting-api.json`
- [x] Alarmes CloudWatch local — `infra/localstack/setup-monitoring.ps1`
- [x] Documentação — `docs/reporting-slo.md`

### Checklist ARQ-07 — Documentação arquitetural



- [x] Diagrama **C4 Context** — `docs/c4/context.md`

- [x] Diagrama **C4 Container** — `docs/c4/containers.md`

- [x] Fluxo write/read — `docs/c4/data-flows.md`

- [x] Índice — `docs/README.md`



---



## 5. Gaps de consistência interna



| ID | Gap | Prioridade | Ação | Validação |

|----|-----|------------|------|-----------|

| CON-01 | Specs 002/003 citam SQL+RabbitMQ | Média | Nota supersession adicionada | ✅ |

| CON-02 | `reporting-slo.md` desatualizado | Baixa | Atualizado | ✅ |

| CON-03 | Relay/Worker fora do slnx | Média | Adicionados ao `Aspire.CashFlow.slnx` | ✅ `dotnet build -c Release` |

| CON-04 | CI GitHub Actions | Média | `.github/workflows/ci.yml` | ⚠️ Build Release OK; testes Auth/Redis precisam infra no runner |

| CON-05 | Diagramas C4 | Alta | `docs/c4/*` | ✅ |

| CON-06 | deferred espalhado | Baixa | `docs/roadmap.md` | ✅ |



---



## 6. Matriz de rastreabilidade — Desafio → Artefato



| Critério do desafio | Artefato principal | Status |

|---------------------|-------------------|--------|

| Serviço de lançamentos | `CashFlow.Transactions.Api`, ADR 002 | ✅ |

| Serviço consolidado | `CashFlow.Reporting.Api`, ADR 002 | ✅ |

| C# | `Aspire.CashFlow.slnx` | ✅ |

| Testes | `tests/*` | ✅ |

| SOLID / Clean Architecture | Pastas por camada em cada API | ✅ |

| README | `README.md` | ✅ |

| GitHub público | *(ação externa)* | ⚠️ |

| Isolamento Transactions ↔ Reporting | ADR 001, 002; testes isolamento | ✅ |

| 50 RPS / 5% loss | Benchmarks + script de teste de carga | ✅ |

| Escalabilidade horizontal | AppHost, Redis, DailySummaries | ✅ |

| Resiliência | DLQ, NACK, idempotência | ✅ |

| Segurança | JWT, Cognito, MFA | ✅ |

| Microsserviços | Auth, Transactions, Relay, Reporting | ✅ |

| Integração assíncrona | EventStore → SNS → SQS | ✅ |

| Métricas operacionais | SLO/SLI, Prometheus | ✅ |

| ADRs | `docs/adr/000`–`003` | ✅ |

| Diagramas | `docs/c4/` | ✅ |

| Evoluções futuras | `docs/roadmap.md` | ✅ |



---



## 7. Plano de execução — status



| Fase | IDs | Status |

|------|-----|--------|

| **1 — Bloqueantes** | RT-04, RT-06 | ✅ README/docs (RT-05 pendente externo) |

| **2 — NFR crítico** | NFR-01, NFR-02 | ✅ NFR-01 + NFR-02 validados (teste de carga 2026-06-15) |

| **3 — Documentação** | ARQ-07, CON-05 | ✅ |

| **4 — Consistência** | CON-01–04 | ✅ (CI validar no push) |

| **5 — Polish** | CON-06, ARQ-01 | ✅ |



---



## 8. Checklist final de submissão



### Obrigatórios



- [x] `README.md` na raiz

- [ ] Repositório GitHub **público** com URL no README

- [x] `dotnet build Aspire.CashFlow.slnx -c Release` passa localmente

- [x] `dotnet test Aspire.CashFlow.slnx` 100% verde — ✅ **81/81** (2026-06-15, stack local ativa)

- [X] Sistema sobe com `.\scripts\run-full-local.ps1`

- [x] Fluxo funcional RN-01/RN-02 via API — `CashFlowFunctionalFlowTests` (1/1)



### NFRs



- [x] Evidência de isolamento (testes automatizados)

- [x] Teste de carga reporting 50 RPS com fail % ≤ 5% (log: `load_50rps_2026-06-15_21.42.13.log`)



### Arquitetura



- [x] ADRs linkados no README (000–003)

- [x] Diagrama C4 (Context + Container)

- [x] SLOs linkados

- [x] Seção evoluções futuras



### Qualidade



- [x] Specs alinhadas às ADRs (nota supersession)

- [ ] CI verde no GitHub Actions (push pendente; build Release OK local)

- [x] Solução inclui Relay e Worker



---



## 9. Registro de progresso



| Data | ID(s) | Responsável | Notas |

|------|-------|-------------|-------|

| 2026-06-15 | RT-04, RT-06, ARQ-07, CON-01–06, NFR-01 | Agent | README, C4, roadmap, CI, slnx, testes isolamento, specs |

| 2026-06-15 | NFR-02, RT-01, RT-02, CON-03/04 | Agent | Teste de carga 50 RPS OK; build Release; fix NU1107 Auth.UnitTests; fix EventStore stats parser |
| 2026-06-15 | RN-01, RN-02 | Agent | `CashFlow.FunctionalTests` — fluxo API crédito/débito → relatório → CSV/PDF |
| 2026-06-15 | dotnet test 81/81 | Agent | `AuthWebApplicationFactory`, isolamento Redis env, fix flaky `ReportingMetricsTests` |

| | RT-05 | | Publicar GitHub e URL real no README |



---



## Referências rápidas



| Documento | Caminho |

|-----------|---------|

| README raiz | [`../README.md`](../README.md) |

| Índice docs | [`README.md`](README.md) |

| Constituição | [`constitution.md`](constitution.md) |

| ADR arquitetural | [`adr/001-arquitetura-estrutural-e-dados.md`](adr/001-arquitetura-estrutural-e-dados.md) |
| ADR infraestrutura | [`adr/002-infraestrutura-stack-recursos.md`](adr/002-infraestrutura-stack-recursos.md) |
| ADR segurança | [`adr/003-seguranca-cognito-jwt.md`](adr/003-seguranca-cognito-jwt.md) |
| ADR governança | [`adr/000-governanca-decisoes-arquiteturais.md`](adr/000-governanca-decisoes-arquiteturais.md) |

| SLO Reporting | [`reporting-slo.md`](reporting-slo.md) |

| Roadmap | [`roadmap.md`](roadmap.md) |

| Script local | [`../scripts/run-full-local.ps1`](../scripts/run-full-local.ps1) |

| Teste de carga reporting | [`../scripts/run-reporting-load-test.ps1`](../scripts/run-reporting-load-test.ps1) |

---

## 10. Resultado da validação (2026-06-15)

### Comandos executados

| Comando | Resultado |
|---------|-----------|
| `dotnet build Aspire.CashFlow.slnx -c Release` | ✅ Sucesso (após correções) |
| `ReportingAvailabilityIsolationTests` | ✅ 3/3 |
| `CashFlow.Reporting.UnitTests` | ✅ 10/10 |
| `CashFlow.Transactions.IntegrationTests` | ✅ 8/8 |
| `CashFlow.Transactions.ContractTests` | ✅ 6/6 |
| `run-reporting-load-test.ps1` | ✅ 1500 OK @ 50 RPS, p50 3.5 ms, p95 7 ms |
| `CashFlow.FunctionalTests` | ✅ 1/1 — crédito/débito → relatório → CSV/PDF |
| `dotnet test Aspire.CashFlow.slnx -c Release` | ✅ **81/81** (stack local ativa) |

### `dotnet test Aspire.CashFlow.slnx -c Release` (stack local ativa)

| Projeto | Resultado |
|---------|-----------|
| `CashFlow.Reporting.UnitTests` | ✅ 10/10 |
| `CashFlow.Auth.UnitTests` | ✅ 26/26 |
| `CashFlow.Reporting.IntegrationTests` | ✅ 2/2 |
| `CashFlow.Transactions.IntegrationTests` | ✅ 8/8 |
| `CashFlow.Transactions.UnitTests` | ✅ 20/20 |
| `CashFlow.FunctionalTests` | ✅ 1/1 |
| `CashFlow.Transactions.ContractTests` | ✅ 6/6 |
| `CashFlow.Reporting.ContractTests` | ✅ 2/2 |
| `CashFlow.Auth.IntegrationTests` | ✅ 2/2 |
| `CashFlow.Auth.ContractTests` | ✅ 4/4 |

### Correções aplicadas nesta validação

| Item | Correção |
|------|----------|
| NU1107 `CashFlow.Auth.UnitTests` | `PackageReference AWSSDK.Core 4.0.9.3` |
| `AuthWebApplicationFactory` | Host de teste Auth isolado (Cognito/CloudWatch off, MFA local) |
| `ReportingWebApplicationFactory` | `NullReportCache` + env `Reporting__Redis__*` zerado |
| `ReportingMetricsTests` | Snapshot thread-safe para evitar race no `MeterListener` |

### Pendências bloqueantes para submissão

| ID | Ação |
|----|------|
| **RT-05** | Publicar repositório no GitHub e substituir placeholder no README |
| **CI** | Push e validar workflow; considerar `services:` Docker no runner para testes de integração |


