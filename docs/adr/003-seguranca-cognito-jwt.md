# ADR 003: Segurança — Cognito + JWT

**Categoria:** Segurança

## Status

**Aceito** (2026-06-16). Dev local com JWT/Cognito LocalStack; produção parcialmente no [roadmap](../roadmap.md).

## Data

2026-06-16

## Objetivos de negócio afetados

| ID | Objetivo | Impacto |
|----|----------|---------|
| RN-01 | Lançamentos | `UserId` do JWT escopa streams |
| RN-02 | Consolidado | Relatórios filtrados por `UserId` |
| — | Safe by Default (constituição) | APIs protegidas; sem acesso anônimo |

## Contexto

Usuários acessam via Web Blazor. Dados financeiros são **por comerciante**. Spec [`005-adicionar-seguran-front`](../../specs/005-adicionar-seguran-front/spec.md) cobre perímetro AWS (WAF, KMS); esta ADR foca **identidade**: emissor de tokens e validação nas APIs.

Arquitetura estrutural: [ADR 001](001-arquitetura-estrutural-e-dados.md) (Auth BC isolado).

---

## Requisitos funcionais

| Requisito | Descrição |
|-----------|-----------|
| RF-01 | Login e-mail/senha (+ MFA demo local) |
| RF-02 | OAuth2 authorization-code (Web) |
| RF-03 | JWT bearer em Transactions e Reporting |
| RF-04 | Claim `UserId` / `sub` — isolamento de dados |
| RF-05 | (Roadmap) Federação AD/SAML via Cognito |

## Requisitos não funcionais

| Dimensão | Meta |
|----------|------|
| Segurança | TLS; credenciais fora de query string |
| Dev | Cognito/LocalStack ou emissor JWT local |
| Prod | Cognito User Pool; secrets em Secrets Manager |
| Disponibilidade | Falha Auth bloqueia login; tokens válidos até exp |

---

## Opções consideradas

### JWT auto-assinado (sem IdP)

Prós: simplicidade dev. Contras: sem MFA/enterprise; chave em config. **Apenas demo** — insuficiente para prod.

### ASP.NET Identity + cookies

Prós: familiar em MVC. Contras: incompatível com APIs stateless. **Rejeitado.**

### Amazon Cognito + JWT (**escolhida**)

```text
User → Web → Auth.Api → Cognito
              ↓
Web → Transactions.Api / Reporting.Api (Bearer)
```

Prós: IdP gerenciado, OAuth2/OIDC, MFA, federação, APIs stateless. Contras: complexidade dev; Admin API scaffolding; rotação JWKS.

### Auth0 / Keycloak

Prós: DX madura. Contras: desvia eixo AWS. **Rejeitado.**

### Avaliação

| Critério | Peso | JWT local | Cookies | **Cognito+JWT** |
|----------|------|-----------|---------|-----------------|
| Safe by Default | 25% | 2 | 3 | **5** |
| Microsserviços stateless | 20% | 4 | 1 | **5** |
| Evolução enterprise | 20% | 1 | 3 | **5** |
| Alinhamento AWS / spec 005 | 15% | 2 | 2 | **5** |
| Simplicidade dev | 20% | **5** | 4 | 3 |
| **Total** | | ~2,8 | ~2,5 | **~4,5** |

---

## Decisão

1. **`CashFlow.Auth.Api`** — autenticação; integração Cognito (ou compatível em dev).
2. **JWT bearer** — validação em cada request nas APIs protegidas.
3. **Isolamento** — handlers usam `UserId` do claim; sem cross-tenant.
4. **Web** — OAuth2 authorization-code.
5. **Prod (roadmap)** — `SigningKey` em Secrets Manager; API Gateway authorizer opcional.

**Fora desta ADR:** WAF, KMS, GuardDuty — perímetro, não identidade core.

---

## Consequências

### Positivas

- OIDC escalável; Auth BC isolado
- Testes: `AuthWebApplicationFactory`, JWT de teste

### Negativas

- Cognito Admin scaffolding
- Integração completa exige Docker em dev

---

## Critério de reavaliação

- Multi-tenant B2B → claims ABAC
- Migração off-AWS → Keycloak com mesma interface OIDC

---

## Referências

- [ADR 001 — Arquitetura](001-arquitetura-estrutural-e-dados.md)
- [`specs/005-adicionar-seguran-front/spec.md`](../../specs/005-adicionar-seguran-front/spec.md)
- [`roadmap.md`](../roadmap.md)
- `CashFlow.Auth.Api`, `AuthWebApplicationFactory`

## Histórico

| Versão | Data | Nota |
|--------|------|------|
| 1.0 | 2026-06-16 | Aceito; substitui ADR arquivada 008 |
