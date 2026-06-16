# ADR 000: Governança de decisões arquiteturais

## Status

**Aceito** (2026-06-16, revisado na consolidação)

## Contexto

O projeto Cash Flow documenta decisões em ADRs. A [constituição](../constitution.md) exige que **todas as decisões arquiteturais** sejam registradas. Este documento define **quando** registrar, **como** estruturar e **como** ligar decisões técnicas a objetivos de negócio.

### Princípio central

> Tudo que é arquitetura afeta objetivos de negócio. Nem todo design de sistema é arquitetura; **todo design torna-se arquitetura documentada** quando sustenta um objetivo de negócio ou pilar NFR mensurável.

---

## Catálogo atual (3 categorias)

| ADR | Categoria | Escopo |
|-----|-----------|--------|
| [001](001-arquitetura-estrutural-e-dados.md) | **Arquitetural** | Estrutura, padrões, modelo de dados, NFR-01 |
| [002](002-infraestrutura-stack-recursos.md) | **Infraestrutura** | EventStore, mensageria, relay, SQL, Redis |
| [003](003-seguranca-cognito-jwt.md) | **Segurança** | Cognito, JWT, isolamento por usuário |

Documentos substituídos: [`archive/`](archive/README.md).

### Quando criar nova ADR

Nova ADR de decisão **somente** se:

1. **Novo domínio** não cabível nas 3 categorias (ex.: deploy Kubernetes com HPA/ingress).
2. **Decisão irreversível** com alternativas credíveis e impacto em RN/NFR.

Caso contrário: **nova seção** na ADR existente da categoria, ou documentação operacional (SLO, C4, `messaging-pipeline-observability.md`).

**Teto recomendado:** 3 ADRs de decisão + este 000, até novo domínio arquitetural.

---

## O que é arquitetura (neste projeto)

| Critério | Exemplo Cash Flow |
|----------|-------------------|
| Capacidade de negócio | RN-01 lançamentos; RN-02 consolidado |
| NFR crítico | NFR-01 isolamento; NFR-02 50 RPS |
| Pilar da constituição | Performance, Safe by Default, Maintainability |
| Fronteira entre sistemas | Transactions ↔ Reporting via mensageria |
| Forma estrutural | Microsserviços, CQRS |

**Não exige ADR própria:** detalhe coberto por ADR pai; refatoração sem mudança de NFR; métricas operacionais (usar SLOs e docs de observabilidade).

---

## Rastreabilidade obrigatória por ADR

Referência: [`rastreabilidade-gaps-checklist.md`](../rastreabilidade-gaps-checklist.md).

| ID | Descrição |
|----|-----------|
| **RN-01** | Serviço de controle de lançamentos |
| **RN-02** | Serviço de consolidado diário |
| **NFR-01** | Lançamentos não caem se reporting cair |
| **NFR-02** | Consolidado: 50 req/s, ≤ 5% perda |

Pilares NFR: performance, disponibilidade, confiabilidade, manutenibilidade, escalabilidade, evolvabilidade.

---

## Processo

1. **Propor** — [`template.md`](template.md); status `Proposto`.
2. **Classificar** — Arquitetural | Infraestrutura | Segurança.
3. **Aceitar** — alinhamento com constituição e SLOs.
4. **Evoluir** — preferir seção nova na ADR da categoria vs. novo arquivo.
5. **Arquivar** — ADR substituída → stub em `archive/`.

---

## Formato

- **Numeração:** `NNN-titulo-kebab-case.md`
- **Categoria:** campo obrigatório no cabeçalho (exceto 000)
- **Idioma:** português
- **Links:** relativos; cruzar ADRs 001–003, C4, SLOs

---

## Referências

- [Template ADR](template.md)
- [Índice](../README.md)
- [Arquivadas](archive/README.md)

## Histórico

| Versão | Data | Nota |
|--------|------|------|
| 1.0 | 2026-06-16 | Aceito: governança inicial |
| 2.0 | 2026-06-16 | Consolidação em 3 categorias (001–003); arquivo 001–009 |
