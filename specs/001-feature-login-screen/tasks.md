---

description: "Lista de tarefas para implementação da tela de login"
---

# Tarefas: Tela de Login

**Entrada**: Documentos de design de `/specs/001-feature-login-screen/`

**Pré-requisitos**: plan.md, spec.md

**Testes**: Incluir testes unitários, de integração e de contrato porque a constituição exige validação automatizada e a spec define histórias de usuário testáveis de forma independente.

**Organização**: As tarefas são agrupadas por história de usuário para que cada história possa ser implementada e validada de forma independente.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: Pode ser executada em paralelo ao tocar arquivos diferentes sem dependência bloqueante
- **[Story]**: Mapeia a tarefa a uma história de usuário (`US1`, `US2`, `US3`)
- Cada tarefa inclui um caminho de destino exato

## Fase 1: Configuração (Infraestrutura Compartilhada)

**Propósito**: Criar a estrutura da fatia de autenticação e a configuração base dos projetos necessária antes do trabalho específico da funcionalidade.

- [ ] T001 Criar pastas da solução de autenticação em `src/` e `tests/` para `CashFlow.Auth.Api`, `CashFlow.Auth.Application`, `CashFlow.Auth.Domain`, `CashFlow.Auth.Infrastructure`, `CashFlow.Web`, `CashFlow.Auth.UnitTests`, `CashFlow.Auth.IntegrationTests` e `CashFlow.Auth.ContractTests`
- [ ] T002 Adicionar projetos de autenticação à solução e ao grafo de referências em `Aspire.CashFlow.slnx`
- [ ] T003 [P] Registrar a API de autenticação e o cliente web em `Aspire.CashFlow.AppHost/AppHost.cs`
- [ ] T004 [P] Estender os service defaults compartilhados para logging, health checks e métricas em `Aspire.CashFlow.ServiceDefaults/Extensions.cs`

---

## Fase 2: Fundacional (Pré-requisitos Bloqueantes)

**Propósito**: Construir os contratos centrais de autenticação, modelo de domínio, configuração e middleware dos quais todas as histórias de usuário dependem.

**⚠️ CRÍTICO**: Nenhum trabalho de história de usuário DEVE começar até que esta fase esteja concluída.

- [ ] T005 Definir modelos de domínio de autenticação em `src/CashFlow.Auth.Domain/Entities/UserAccount.cs`, `src/CashFlow.Auth.Domain/Entities/LoginAttempt.cs` e `src/CashFlow.Auth.Domain/Entities/JwtSession.cs`
- [ ] T006 [P] Definir contratos de requisição e resposta de autenticação em `src/CashFlow.Auth.Application/Contracts/LoginRequest.cs`, `src/CashFlow.Auth.Application/Contracts/LoginResult.cs` e `src/CashFlow.Auth.Application/Contracts/SessionState.cs`
- [ ] T007 [P] Implementar configuração JWT e binding de opções em `src/CashFlow.Auth.Infrastructure/Configuration/JwtOptions.cs` e `src/CashFlow.Auth.Api/appsettings.json`
- [ ] T008 Implementar interfaces de validação de credenciais e serviço de token em `src/CashFlow.Auth.Application/Abstractions/IAuthenticationService.cs` e `src/CashFlow.Auth.Application/Abstractions/ITokenService.cs`
- [ ] T009 [P] Implementar serviços de criação e validação de JWT em `src/CashFlow.Auth.Infrastructure/Security/JwtTokenService.cs` e `src/CashFlow.Auth.Infrastructure/Security/PasswordVerifier.cs`
- [ ] T010 Configurar autenticação, autorização, tratamento de exceções e middleware de telemetria em `src/CashFlow.Auth.Api/Program.cs`
- [ ] T011 [P] Adicionar definições de métricas de autenticação e eventos de log estruturados em `src/CashFlow.Auth.Infrastructure/Observability/AuthMetrics.cs` e `src/CashFlow.Auth.Infrastructure/Observability/AuthLogEvents.cs`
- [ ] T012 Criar o documento de contrato da API de login em `specs/001-feature-login-screen/contracts/auth-login.openapi.yaml`

**Checkpoint**: Fundação pronta. A implementação das histórias de usuário pode prosseguir de forma independente.

---

## Fase 3: História de Usuário 1 - Autenticar com credenciais válidas (Prioridade: P1) 🎯 MVP

**Objetivo**: Permitir que um usuário registrado faça login com sucesso, receba um JWT e entre em uma sessão autenticada.

**Teste Independente**: Enviar credenciais válidas e verificar que a API retorna um JWT, o cliente armazena a sessão autenticada e o usuário alcança o estado protegido de destino.

### Testes para História de Usuário 1

- [ ] T013 [P] [US1] Adicionar testes unitários para emissão de token e validação bem-sucedida de credenciais em `tests/CashFlow.Auth.UnitTests/Security/JwtTokenServiceTests.cs` e `tests/CashFlow.Auth.UnitTests/Application/AuthenticationServiceTests.cs`
- [ ] T014 [P] [US1] Adicionar testes de contrato para o endpoint de login em `tests/CashFlow.Auth.ContractTests/AuthLoginContractTests.cs`
- [ ] T015 [P] [US1] Adicionar testes de integração para login bem-sucedido em `tests/CashFlow.Auth.IntegrationTests/LoginSuccessFlowTests.cs`

### Implementação para História de Usuário 1

- [ ] T016 [P] [US1] Implementar o caso de uso de autenticação em `src/CashFlow.Auth.Application/UseCases/LoginUserHandler.cs`
- [ ] T017 [P] [US1] Implementar o endpoint de login em `src/CashFlow.Auth.Api/Endpoints/LoginEndpoints.cs`
- [ ] T018 [US1] Implementar a UI do formulário de login em `src/CashFlow.Web/Pages/Login.razor`
- [ ] T019 [P] [US1] Implementar o cliente da API de login e o gravador de sessão em `src/CashFlow.Web/Services/AuthApiClient.cs` e `src/CashFlow.Web/Services/SessionStore.cs`
- [ ] T020 [US1] Conectar navegação pós-login bem-sucedido e bootstrap do shell autenticado em `src/CashFlow.Web/Program.cs` e `src/CashFlow.Web/Layout/MainLayout.razor`

**Checkpoint**: A História de Usuário 1 DEVE estar totalmente funcional e demonstrável como MVP.

---

## Fase 4: História de Usuário 2 - Rejeitar credenciais inválidas com segurança (Prioridade: P2)

**Objetivo**: Negar tentativas de login inválidas com tratamento de erro genérico e não sensível.

**Teste Independente**: Enviar credenciais incorretas e verificar que o acesso é negado, nenhum JWT é emitido e o usuário vê uma mensagem genérica de credenciais inválidas.

### Testes para História de Usuário 2

- [ ] T021 [P] [US2] Adicionar testes unitários para tratamento de credenciais inválidas e respostas genéricas de falha em `tests/CashFlow.Auth.UnitTests/Application/InvalidCredentialTests.cs`
- [ ] T022 [P] [US2] Adicionar testes de integração para fluxos de senha incorreta e e-mail desconhecido em `tests/CashFlow.Auth.IntegrationTests/LoginFailureFlowTests.cs`
- [ ] T023 [P] [US2] Adicionar testes de UI para renderização genérica de erros em `tests/CashFlow.Auth.IntegrationTests/LoginErrorRenderingTests.cs`

### Implementação para História de Usuário 2

- [ ] T024 [US2] Implementar mapeamento de resposta para credenciais inválidas em `src/CashFlow.Auth.Application/UseCases/LoginUserHandler.cs`
- [ ] T025 [P] [US2] Adicionar respostas genéricas de erro de autenticação e mapeamento de status em `src/CashFlow.Auth.Api/Endpoints/LoginEndpoints.cs`
- [ ] T026 [P] [US2] Adicionar validação de formulário no cliente e apresentação genérica de erros de autenticação em `src/CashFlow.Web/Pages/Login.razor`
- [ ] T027 [US2] Registrar tentativas de login falhas e telemetria segura em `src/CashFlow.Auth.Infrastructure/Observability/AuthAuditService.cs`

**Checkpoint**: As Histórias de Usuário 1 e 2 DEVEM funcionar de forma independente sem expor detalhes sensíveis de autenticação.

---

## Fase 5: História de Usuário 3 - Preservar e encerrar sessões autenticadas corretamente (Prioridade: P3)

**Objetivo**: Restaurar sessões válidas ao recarregar e limpá-las no logout ou na detecção de token inválido.

**Teste Independente**: Fazer login, recarregar a aplicação para verificar a restauração, depois encerrar sessão e confirmar que a sessão foi limpa e as rotas protegidas estão bloqueadas.

### Testes para História de Usuário 3

- [ ] T028 [P] [US3] Adicionar testes unitários para restauração de sessão e verificações de expiração de token em `tests/CashFlow.Auth.UnitTests/Application/SessionRestorationTests.cs`
- [ ] T029 [P] [US3] Adicionar testes de integração para fluxos de recarga, token expirado e logout em `tests/CashFlow.Auth.IntegrationTests/SessionLifecycleTests.cs`
- [ ] T030 [P] [US3] Adicionar testes de contrato para endpoints de bootstrap de sessão e logout, se expostos, em `tests/CashFlow.Auth.ContractTests/SessionContractTests.cs`

### Implementação para História de Usuário 3

- [ ] T031 [US3] Implementar fluxo de restauração de sessão e validação de token em `src/CashFlow.Web/Services/SessionBootstrapper.cs`
- [ ] T032 [P] [US3] Implementar comportamento de logout e limpeza de sessão em `src/CashFlow.Web/Services/SessionStore.cs` e `src/CashFlow.Web/Components/AuthLogoutButton.razor`
- [ ] T033 [P] [US3] Implementar imposição de rotas protegidas e fallback para token inválido em `src/CashFlow.Web/Program.cs` e `src/CashFlow.Web/Components/AuthRouteGuard.razor`
- [ ] T034 [US3] Adicionar tratamento de rejeição de tokens expirados, malformados e adulterados em `src/CashFlow.Auth.Infrastructure/Security/JwtTokenService.cs`

**Checkpoint**: Todas as histórias de usuário de login DEVEM estar funcionalmente independentes.

---

## Fase 6: Polimento e Preocupações Transversais

**Propósito**: Finalizar documentação, validação operacional e trabalho de qualidade que abrange toda a funcionalidade de login.

- [ ] T035 [P] Documentar configuração de autenticação, variáveis de ambiente e verificação de login local em `specs/001-feature-login-screen/quickstart.md`
- [ ] T036 [P] Registrar decisões de design de autenticação em `docs/adr/0001-authentication-strategy.md` e `docs/adr/0002-jwt-session-persistence.md`
- [ ] T037 Adicionar documentação do modelo de dados de autenticação em `specs/001-feature-login-screen/data-model.md`
- [ ] T038 Executar validação de performance e caminhos de falha para o fluxo de login e registrar notas em `specs/001-feature-login-screen/research.md`
- [ ] T039 Atualizar configuração do repositório e instruções de autenticação em `README.md`

---

## Dependências e Ordem de Execução

### Dependências entre Fases

- **Fase 1: Configuração**: Sem dependências; pode começar imediatamente.
- **Fase 2: Fundacional**: Depende da Fase 1 e bloqueia todo o trabalho de histórias de usuário.
- **Fase 3: História de Usuário 1**: Depende da Fase 2 e entrega o MVP.
- **Fase 4: História de Usuário 2**: Depende da Fase 2 e pode começar após o MVP ou em paralelo com ajustes posteriores da US1, se houver equipe disponível.
- **Fase 5: História de Usuário 3**: Depende da Fase 2 e das primitivas de sessão estabelecidas na US1.
- **Fase 6: Polimento**: Depende das histórias de usuário desejadas estarem concluídas.

### Dependências entre Histórias de Usuário

- **US1 (P1)**: Sem dependência de outras histórias de usuário após a fase fundacional.
- **US2 (P2)**: Reutiliza o endpoint de login e o serviço de autenticação da US1, mas permanece testável de forma independente.
- **US3 (P3)**: Reutiliza emissão de token e componentes de sessão do cliente da US1, mas permanece testável de forma independente.

### Oportunidades de Paralelismo

- T003 e T004 podem ser executadas em paralelo após T001 e T002.
- T006, T007, T009 e T011 podem ser executadas em paralelo durante a fase fundacional.
- Tarefas de teste dentro de cada história de usuário marcadas com `[P]` podem ser executadas em paralelo.
- T016, T017 e T019 podem ser executadas em paralelo assim que os contratos fundacionais estiverem concluídos.
- T025 e T026 podem ser executadas em paralelo após T024.
- T032 e T033 podem ser executadas em paralelo após T031 começar a expor o comportamento do ciclo de vida da sessão.

## Estratégia de Implementação

### MVP Primeiro

1. Concluir Fase 1 e Fase 2.
2. Concluir História de Usuário 1.
3. Validar login bem-sucedido de ponta a ponta antes de expandir o escopo.

### Entrega Incremental

1. Entregar login bem-sucedido e navegação autenticada.
2. Adicionar tratamento seguro de credenciais inválidas e telemetria.
3. Adicionar restauração de sessão, logout e endurecimento de token.
4. Finalizar documentação, ADRs e validação de performance.

### Estratégia de Equipe

1. Um desenvolvedor pode concluir os contratos fundacionais de autenticação enquanto outro prepara o scaffolding de testes.
2. Após a fase fundacional, tarefas de UI e API para US1 podem prosseguir em paralelo.
3. US2 e US3 podem ser divididas entre trabalho de backend e cliente assim que as primitivas compartilhadas de autenticação existirem.

## Notas

- Manter criação de token, validação e verificação de credenciais fora dos componentes de UI.
- Garantir que toda telemetria de falha de autenticação omita senhas em texto puro e outros segredos.
- Validar que os testes falham antes de implementar o comportamento correspondente.
- Parar em cada checkpoint de história de usuário para verificar o comportamento independente antes de continuar.
