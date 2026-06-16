# ADR NNN: [Título da decisão]

**Categoria:** [Arquitetural | Infraestrutura | Segurança]

> Antes de criar nova ADR: verificar se a decisão cabe em [ADR 001](001-arquitetura-estrutural-e-dados.md), [ADR 002](002-infraestrutura-stack-recursos.md) ou [ADR 003](003-seguranca-cognito-jwt.md).

## Status

**[Proposto | Aceito | Depreciado | Substituído por ADR XXX]**

## Data

YYYY-MM-DD

## Objetivos de negócio afetados

| ID | Objetivo | Impacto desta decisão |
|----|----------|------------------------|
| RN-XX | [Requisito de negócio] | [Como a decisão suporta ou restringe] |
| NFR-XX | [Requisito não funcional] | [Como a decisão suporta ou restringe] |

## Pilares NFR

| Pilar | Relevância |
|-------|------------|
| Performance | [Alta / Média / Baixa] |
| Disponibilidade | |
| Confiabilidade | |
| Manutenibilidade | |
| Escalabilidade | |
| Evolvabilidade | |

## Contexto

[Descreva o problema, restrições da constituição, estado atual e por que uma decisão é necessária agora.]

## Requisitos funcionais

| Requisito | Descrição |
|-----------|-----------|
| RF-01 | |

## Requisitos não funcionais

| Dimensão | Meta / nota |
|----------|-------------|
| Ambiente | |
| Throughput | |
| Latência | |
| Durabilidade / storage | |
| Segurança / autenticação | |
| Operação (back-pressure, replay, carga ops) | |

---

## Opções consideradas

### Opção A — [Nome]

[Descrição breve]

#### Prós

| # | Benefício |
|---|-----------|
| 1 | |

#### Contras

| # | Risco / custo |
|---|---------------|
| 1 | |

---

### Opção B — [Nome] (**escolhida**)

[Descrição breve]

#### Prós

| # | Benefício |
|---|-----------|
| 1 | |

#### Contras

| # | Risco / custo |
|---|---------------|
| 1 | |

---

## Avaliação comparativa

Escala: **1** = fraco · **5** = forte.

### Pesos

| Critério | Peso | Justificativa |
|----------|------|---------------|
| | | |

### Matriz de pontuação

| Critério | Opção A | **Opção B** |
|----------|---------|-------------|
| | | |

### Placar ponderado (indicativo)

| Opção | Total |
|-------|-------|
| **Opção B** | |

---

## Decisão

[Declaração clara da opção escolhida e regras de implementação.]

## Consequências

### Positivas

-

### Negativas

-

### Neutras

-

## Critério de reavaliação

[Condições objetivas que disparam revisão desta ADR — ex.: backlog sustentado, mudança de volume, novo requisito regulatório.]

## Referências

- [ADR XXX](XXX-nome.md)
- [`../transactions-slo.md`](../transactions-slo.md)
- [Constituição](../constitution.md)

## Histórico

| Versão | Data | Nota |
|--------|------|------|
| 1.0 | YYYY-MM-DD | Aceito |
