# Adiado / trabalho futuro

> **Consolidado em:** [`../../docs/roadmap.md`](../../docs/roadmap.md) — use o roadmap como fonte principal de evoluções futuras.

Itens intencionalmente fora do escopo da fase atual de implementação. Revisitar quando as prioridades do produto exigirem gestão centralizada do ciclo de vida de usuários.

**Última atualização**: 2026-06-12

## Admin Cognito (`CognitoUserAdministrationService`)

**Status**: Futuro — não planejado para o curto prazo.

**Comportamento atual**:

- `CognitoUserAdministrationService` é apenas scaffolding (sem chamadas ao Cognito Admin SDK).
- Com Cognito habilitado, operações administrativas não provisionam nem gerenciam usuários reais no Cognito.
- Endpoints administrativos em memória apenas para desenvolvimento permanecem em `/api/auth/admin` quando o Cognito está desabilitado (`Program.cs`).

**Escopo futuro** (quando priorizado):

- Implementar APIs Admin do Cognito: `AdminCreateUser`, `AdminDisableUser`, `AdminEnableUser`, `AdminAddUserToGroup`, `ListUsers`, etc.
- Conectar `UserAdministrationEndpoints` ao Cognito em vez de `InMemoryUserAccountStore`.
- Interface administrativa (`UserAdministration.razor`) e testes de contrato (tarefas US2 T023–T029).
- Validar contra Cognito Local e User Pool na AWS.

**Itens adiados relacionados**:

- Federação AD / SAML / OIDC (`DirectoryFederationMapper`, configuração de IdP no User Pool).

## OAuth2 authorization-code / Hosted UI

**Status**: Implementado (2026-06-12).

**Comportamento atual**:

- A Auth API expõe `/api/auth/oauth/authorize`, `/api/auth/oauth/login` (Hosted UI de dev) e `/api/auth/oauth/token`.
- Quando `Cognito:OAuth:Domain` está definido, authorize redireciona para o Cognito Hosted UI da AWS e a troca de token usa o endpoint `/oauth2/token` do Cognito.
- Quando o domínio está vazio (local/em memória), a auth API serve um Hosted UI de desenvolvimento para testes end-to-end do fluxo OAuth2 authorization-code.
- O CashFlow.Web adiciona **Entrar com Hosted UI (OAuth2)** na página de login e conclui o fluxo em `/auth/callback`.
- Login por senha e bearer/refresh permanecem disponíveis como fallback.

**Checklist de produção na AWS**:

- Configurar domínio do User Pool Cognito e segredo do app client.
- Definir `Cognito:OAuth:Domain`, `Cognito:OAuth:ClientSecret` e URLs de callback correspondentes.
