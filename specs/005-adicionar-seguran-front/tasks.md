---

description: "Lista de tarefas para implementação de segurança AWS e gestão de usuários"

---

# Tarefas: Segurança AWS e gestão de usuários

**Entrada**: Documentos de design de `/specs/005-adicionar-seguran-front/`

**Pré-requisitos**: plan.md, spec.md

**Testes**: Incluir testes unitários, de integração e de contrato porque a spec exige explicitamente testes automatizados de segurança e a constituição exige validação automatizada para mudanças críticas de arquitetura.

**Organização**: As tarefas estão agrupadas por história de usuário para que cada capacidade de segurança possa ser implementada e validada de forma independente.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: Pode executar em paralelo ao tocar arquivos diferentes sem dependência bloqueante
- **[Story]**: Mapeia a tarefa a uma história de usuário (`US1`, `US2`, `US3`, `US4`)
- Toda tarefa inclui um caminho de destino exato

## Fase 1: Setup (infraestrutura compartilhada)

**Propósito**: Preparar a solução, projetos de teste e workspace de infraestrutura AWS necessários pela feature.

- [ ] T001 Criar pastas de implementação de segurança em `tests/` e `infra/aws/` para `CashFlow.Auth.UnitTests`, `CashFlow.Auth.IntegrationTests`, `CashFlow.Auth.ContractTests` e `security/`
- [ ] T002 Adicionar os projetos de teste de autenticação em `AspireApp1.slnx`
- [ ] T003 [P] Registrar placeholders de configuração orientados à AWS para serviços de auth, reporting e transactions em `AspireApp1.AppHost/AppHost.cs`
- [ ] T004 [P] Estender service defaults compartilhados para correlação, cabeçalhos de segurança, health checks e primitivas de rate limiting em `AspireApp1.ServiceDefaults/Extensions.cs`

---

## Fase 2: Fundacional (pré-requisitos bloqueantes)

**Propósito**: Construir as abstrações compartilhadas de identidade, segredos, criptografia, perímetro e observabilidade das quais toda história de usuário depende.

**⚠️ CRÍTICO**: Nenhum trabalho de história de usuário deve começar até esta fase estar completa.

- [ ] T005 Definir entidades de domínio de segurança compartilhadas em `src/CashFlow.Auth.Domain/Entities/IdentityProfile.cs`, `src/CashFlow.Auth.Domain/Entities/AuthorizationPolicyMapping.cs` e `src/CashFlow.Auth.Domain/Entities/SecurityFinding.cs`
- [ ] T006 [P] Definir contratos de segurança compartilhados em `src/CashFlow.Auth.Application/Contracts/UserSummary.cs`, `src/CashFlow.Auth.Application/Contracts/UserAccessAssignment.cs` e `src/CashFlow.Auth.Application/Contracts/SecurityEventRecord.cs`
- [ ] T007 [P] Definir abstrações de Cognito, segredos e gestão de chaves em `src/CashFlow.Auth.Application/Abstractions/IIdentityProvider.cs`, `src/CashFlow.Auth.Application/Abstractions/IUserAdministrationService.cs`, `src/CashFlow.Auth.Application/Abstractions/ISecretProvider.cs` e `src/CashFlow.Auth.Application/Abstractions/IEncryptionPolicyService.cs`
- [ ] T008 [P] Adicionar modelos de opções de segurança AWS em `src/CashFlow.Auth.Infrastructure/Configuration/CognitoOptions.cs`, `src/CashFlow.Auth.Infrastructure/Configuration/SecretsManagerOptions.cs` e `src/CashFlow.Auth.Infrastructure/Configuration/KmsOptions.cs`
- [ ] T009 Implementar scaffolding de identidade, segredos e gestão de chaves AWS em `src/CashFlow.Auth.Infrastructure/Identity/CognitoIdentityProvider.cs`, `src/CashFlow.Auth.Infrastructure/Identity/CognitoUserAdministrationService.cs` (apenas stub — administração completa adiada, ver `deferred.md`), `src/CashFlow.Auth.Infrastructure/Security/SecretsManagerSecretProvider.cs` e `src/CashFlow.Auth.Infrastructure/Security/KmsEncryptionPolicyService.cs`
- [ ] T010 [P] Refatorar registro de dependências de autenticação e middleware de autorização em `src/CashFlow.Auth.Api/Program.cs`, `src/CashFlow.Reporting.Api/Program.cs` e `src/CashFlow.Transactions.Api/Program.cs`
- [ ] T011 [P] Adicionar primitivas de telemetria de segurança em `src/CashFlow.Auth.Infrastructure/Observability/AuthMetrics.cs`, `src/CashFlow.Auth.Infrastructure/Observability/AuthLogEvents.cs` e `src/CashFlow.Auth.Infrastructure/Observability/SecurityAuditService.cs`
- [ ] T012 Criar documentos de contrato de API de segurança compartilhados em `specs/005-adicionar-seguran-front/contracts/auth-security.openapi.yaml` e `specs/005-adicionar-seguran-front/contracts/user-administration.openapi.yaml`
- [ ] T013 Criar templates de infraestrutura AWS para identidade, perímetro, segredos e monitoramento em `infra/aws/security/cognito-user-pool.yaml`, `infra/aws/security/api-gateway-authorizers.yaml`, `infra/aws/security/waf-shield.yaml`, `infra/aws/security/secrets-kms.yaml` e `infra/aws/security/monitoring-security.yaml`

**Checkpoint**: Fundação pronta. A implementação das histórias de usuário de segurança pode prosseguir de forma independente.

---

## Fase 3: História de usuário 1 - Autenticar usuários com segurança no front-end e nas APIs (Prioridade: P1) 🎯 MVP

**Objetivo**: Permitir que usuários entrem com autenticação respaldada pelo Cognito, recebam tokens JWT válidos e acessem apenas caminhos protegidos do front-end e da API.

**Teste independente**: Entrar com usuário válido e MFA obrigatório, chamar um endpoint protegido com o token emitido e verificar que tokens inválidos ou expirados são rejeitados antes do processamento no backend.

### Testes da história de usuário 1

- [ ] T014 [P] [US1] Adicionar testes unitários para login com Cognito, validação de token e comportamento de refresh em `tests/CashFlow.Auth.UnitTests/Application/CognitoAuthenticationTests.cs` e `tests/CashFlow.Auth.UnitTests/Security/JwtValidationTests.cs`
- [ ] T015 [P] [US1] Adicionar testes de contrato para login, validação de sessão e respostas não autorizadas em `tests/CashFlow.Auth.ContractTests/AuthSecurityContractTests.cs`
- [ ] T016 [P] [US1] Adicionar testes de integração para login MFA bem-sucedido e acesso a API protegida em `tests/CashFlow.Auth.IntegrationTests/CognitoLoginFlowTests.cs`

### Implementação da história de usuário 1

- [ ] T017 [P] [US1] Refatorar o caso de uso de login para autenticação Cognito em `src/CashFlow.Auth.Application/UseCases/LoginUserHandler.cs`
- [ ] T018 [P] [US1] Implementar endpoints de login, sessão e logout respaldados pelo Cognito em `src/CashFlow.Auth.Api/Endpoints/LoginEndpoints.cs` e `src/CashFlow.Auth.Api/Endpoints/SessionEndpoints.cs`
- [ ] T019 [P] [US1] Atualizar o cliente de autenticação do front-end e o tratamento de estado de sessão em `src/CashFlow.Web/Services/AuthApiClient.cs` e `src/CashFlow.Web/Services/SessionStore.cs`
- [ ] T020 [US1] Implementar a experiência de usuário de login Cognito e MFA em `src/CashFlow.Web/Components/Pages/Login.razor`
- [ ] T021 [US1] Aplicar navegação autenticada e proteção de rotas com consciência de token em `src/CashFlow.Web/Components/Routes.razor`, `src/CashFlow.Web/Components/Layout/MainLayout.razor` e `src/CashFlow.Web/Program.cs`
- [ ] T022 [US1] Substituir registro de auth apenas em memória por provedores com suporte a Cognito em `src/CashFlow.Auth.Infrastructure/Identity/InMemoryUserAccountStore.cs` e `src/CashFlow.Auth.Api/Program.cs`

**Checkpoint**: A história de usuário 1 deve estar totalmente funcional e demonstrável como MVP.

---

## Fase 4: História de usuário 2 - Gerenciar usuários, papéis e integração de identidade corporativa (Prioridade: P2)

> **⏸ Adiado (2026-06-12)**: A administração de usuários não está planejada para a fase atual. Trabalho de admin Cognito, federação e UI administrativa está rastreado em `deferred.md`. US1 (auth + refresh) permanece o escopo ativo. Endpoints administrativos apenas em memória para dev podem ainda existir quando o Cognito estiver desabilitado.

**Objetivo**: Permitir que administradores gerenciem usuários, mapeamentos de papéis e configurações de federação corporativa com comportamento de acesso auditável.

**Teste independente**: Provisionar ou atualizar um usuário, atribuí-lo a um grupo Cognito ou papel mapeado, validar opcionalmente o mapeamento de federação e verificar que claims e acesso mudam de acordo.

### Testes da história de usuário 2

- [ ] ~~T023~~ **[ADIADO]** [US2] Adicionar testes unitários para mapeamento de papéis, mudanças no ciclo de vida de usuário e comportamento de usuário desabilitado em `tests/CashFlow.Auth.UnitTests/Application/UserAdministrationTests.cs`
- [ ] ~~T024~~ **[ADIADO]** [US2] Adicionar testes de contrato para endpoints de administração de usuários em `tests/CashFlow.Auth.ContractTests/UserAdministrationContractTests.cs`
- [ ] ~~T025~~ **[ADIADO]** [US2] Adicionar testes de integração para atribuição de grupo, desabilitação e mapeamento de federação em `tests/CashFlow.Auth.IntegrationTests/UserAdministrationFlowTests.cs`

### Implementação da história de usuário 2

- [ ] ~~T026~~ **[ADIADO]** [US2] Implementar fluxos de provisionamento de usuário, atribuição de grupo e desabilitar ou habilitar em `src/CashFlow.Auth.Application/UseCases/CreateUserHandler.cs`, `src/CashFlow.Auth.Application/UseCases/AssignUserAccessHandler.cs` e `src/CashFlow.Auth.Application/UseCases/DisableUserHandler.cs`
- [ ] ~~T027~~ **[ADIADO]** [US2] Implementar serviços de administração Cognito e mapeamento de federação de diretório em `src/CashFlow.Auth.Infrastructure/Identity/CognitoUserAdministrationService.cs` e `src/CashFlow.Auth.Infrastructure/Identity/DirectoryFederationMapper.cs`
- [ ] ~~T028~~ **[ADIADO]** [US2] Expor endpoints de administração de usuários em `src/CashFlow.Auth.Api/Endpoints/UserAdministrationEndpoints.cs`
- [ ] ~~T029~~ **[ADIADO]** [US2] Adicionar tela administrativa de gestão de usuários e cliente de API em `src/CashFlow.Web/Components/Pages/UserAdministration.razor` e `src/CashFlow.Web/Services/UserAdministrationApiClient.cs`
- [ ] ~~T030~~ **[ADIADO]** [US2] Implementar aplicação de sessão revogada e usuário desabilitado em `src/CashFlow.Auth.Api/Endpoints/SessionEndpoints.cs` e `src/CashFlow.Auth.Infrastructure/Identity/CognitoIdentityProvider.cs`

**Checkpoint**: As histórias de usuário 1 e 2 devem funcionar de forma independente com comportamento de acesso gerenciado centralmente.

---

## Fase 5: História de usuário 3 - Proteger APIs, segredos e dados armazenados contra ataques comuns (Prioridade: P3)

**Objetivo**: Proteger o perímetro da aplicação, segredos de runtime e armazenamento criptografado com controles nativos da AWS e middleware de segurança .NET.

**Teste independente**: Verificar que APIs protegidas exigem HTTPS e JWTs válidos, origens confiáveis são aplicadas, throttling e regras WAF bloqueiam requisições abusivas, segredos são resolvidos a partir de armazenamento gerenciado e recursos criptografados são configurados por meio de chaves gerenciadas.

### Testes da história de usuário 3

- [ ] T031 [P] [US3] Adicionar testes unitários para validação de entrada, resolução de segredos e decisões de rate limiting em `tests/CashFlow.Auth.UnitTests/Security/InputValidationTests.cs` e `tests/CashFlow.Auth.UnitTests/Security/SecretsResolutionTests.cs`
- [ ] T032 [P] [US3] Adicionar testes de contrato para comportamento de autorização de API protegida em `tests/CashFlow.Reporting.ContractTests/AuthorizationContractTests.cs` e `tests/CashFlow.Transactions.ContractTests/AuthorizationContractTests.cs`
- [ ] T033 [P] [US3] Adicionar testes de integração para CORS, throttling, antiforgery e comportamento de inicialização respaldado por segredos em `tests/CashFlow.Auth.IntegrationTests/PerimeterHardeningTests.cs`

### Implementação da história de usuário 3

- [ ] T034 [P] [US3] Configurar authorizers JWT do API Gateway, endpoints apenas TLS, throttling, quotas e origens CORS confiáveis em `infra/aws/security/api-gateway-authorizers.yaml`
- [ ] T035 [P] [US3] Configurar regras gerenciadas WAF, exceções customizadas e proteções Shield em `infra/aws/security/waf-shield.yaml`
- [ ] T036 [P] [US3] Mover definições de segredos de runtime e políticas de chaves para serviços AWS gerenciados em `infra/aws/security/secrets-kms.yaml`, `src/CashFlow.Auth.Api/appsettings.json` e `src/CashFlow.Web/appsettings.json`
- [ ] T037 [US3] Implementar carregamento de segredos em runtime e acesso a configuração criptografada em `src/CashFlow.Auth.Infrastructure/Security/SecretsManagerSecretProvider.cs`, `src/CashFlow.Auth.Api/Program.cs` e `src/CashFlow.Web/Program.cs`
- [ ] T038 [P] [US3] Adicionar proteções .NET de validação de requisição, antiforgery, codificação de saída e rate limiting em `src/CashFlow.Auth.Api/Program.cs`, `src/CashFlow.Reporting.Api/Program.cs`, `src/CashFlow.Transactions.Api/Program.cs` e `src/CashFlow.Web/Components/Pages/Login.razor`
- [ ] T039 [US3] Configurar políticas de armazenamento criptografado para recursos de arquivo e dados em `infra/aws/security/secrets-kms.yaml` e `infra/aws/security/cognito-user-pool.yaml`

**Checkpoint**: As histórias de usuário 1 a 3 devem estar funcionalmente independentes com perímetro endurecido e tratamento de segredos.

---

## Fase 6: História de usuário 4 - Detectar, auditar e responder a eventos relevantes de segurança (Prioridade: P4)

**Objetivo**: Centralizar logs, trilhas de auditoria, findings e alarmes de segurança para que atividade suspeita e misconfiguração sejam visíveis e acionáveis.

**Teste independente**: Disparar falhas de autenticação, requisições negadas e mudanças de configuração, depois verificar que a telemetria aparece no CloudWatch, CloudTrail, GuardDuty e Security Hub com alertas utilizáveis.

### Testes da história de usuário 4

- [ ] T040 [P] [US4] Adicionar testes unitários para criação sanitizada de eventos de segurança e mapeamento de findings em `tests/CashFlow.Auth.UnitTests/Observability/SecurityEventRecordTests.cs`
- [ ] T041 [P] [US4] Adicionar testes de integração para logging de auditoria e emissão de telemetria em `tests/CashFlow.Auth.IntegrationTests/SecurityObservabilityTests.cs`
- [ ] T042 [P] [US4] Adicionar testes de contrato para formato de payload de evento de segurança e respostas de auditoria administrativa em `tests/CashFlow.Auth.ContractTests/SecurityAuditContractTests.cs`

### Implementação da história de usuário 4

- [ ] T043 [P] [US4] Emitir eventos estruturados de autenticação, autorização e perímetro em `src/CashFlow.Auth.Infrastructure/Observability/SecurityAuditService.cs`, `src/CashFlow.Auth.Infrastructure/Observability/AuthMetrics.cs` e `src/CashFlow.Auth.Api/Program.cs`
- [ ] T044 [P] [US4] Configurar logs, alarmes e dashboards do CloudWatch em `infra/aws/security/monitoring-security.yaml`
- [ ] T045 [P] [US4] Configurar auditoria CloudTrail, GuardDuty e agregação do Security Hub em `infra/aws/security/monitoring-security.yaml`
- [ ] T046 [US4] Expor status operacional de segurança e acesso sanitizado a findings em `src/CashFlow.Auth.Api/Endpoints/SecurityOperationsEndpoints.cs`
- [ ] T047 [US4] Adicionar referências de roteamento de alertas e runbooks operacionais em `src/CashFlow.Auth.Infrastructure/Observability/SecurityAlertCatalog.cs` e `infra/aws/security/README.md`

**Checkpoint**: Todas as histórias de usuário de segurança devem estar funcionalmente independentes.

---

## Fase 7: Polimento e preocupações transversais

**Propósito**: Concluir a feature com documentação, decisões de arquitetura, higiene de dependências e validação ponta a ponta.

- [ ] T048 [P] Documentar setup de segurança AWS, variáveis de ambiente e passos de verificação local em `specs/005-adicionar-seguran-front/quickstart.md`
- [ ] T049 [P] Registrar decisões de identidade, authorizer e monitoramento em `docs/adr/0007-cognito-identity-strategy.md`, `docs/adr/0008-api-gateway-security-boundary.md` e `docs/adr/0009-aws-security-observability.md`
- [ ] T050 Adicionar o modelo de dados da feature em `specs/005-adicionar-seguran-front/data-model.md`
- [ ] T051 Capturar decisões de Cognito, WAF, rotação de segredos e alertas em `specs/005-adicionar-seguran-front/research.md`
- [ ] T052 [P] Atualizar versões de dependências e orientação de revisão de pacotes em `Directory.Packages.props` ou nos arquivos `*.csproj` afetados em `src/`
- [ ] T053 Atualizar setup do repositório e instruções operacionais de segurança AWS em `README.md`
- [ ] T054 Executar validação ponta a ponta da feature e capturar evidências em `specs/005-adicionar-seguran-front/quickstart.md`

---

## Dependências e ordem de execução

### Dependências entre fases

- **Fase 1: Setup**: Sem dependências; pode começar imediatamente.
- **Fase 2: Fundacional**: Depende da Fase 1 e bloqueia todo trabalho de história de usuário.
- **Fase 3: História de usuário 1**: Depende da Fase 2 e entrega o MVP.
- **Fase 4: História de usuário 2**: Depende da Fase 2 e reutiliza primitivas de identidade da US1.
- **Fase 5: História de usuário 3**: Depende da Fase 2 e estende a plataforma com controles de perímetro, segredos e criptografia.
- **Fase 6: História de usuário 4**: Depende da Fase 2 e se beneficia de histórias anteriores que produzem eventos de segurança.
- **Fase 7: Polimento**: Depende das histórias de usuário desejadas estarem completas.

### Dependências entre histórias de usuário

- **US1 (P1)**: Sem dependência de outras histórias após a fase fundacional.
- **US2 (P2)**: Reutiliza primitivas de identidade respaldadas pelo Cognito da US1, mas permanece testável de forma independente.
- **US3 (P3)**: Reutiliza fluxo de identidade protegido da US1 e aplica controles de perímetro e segredos de forma independente.
- **US4 (P4)**: Reutiliza fontes de eventos da US1 à US3, mas permanece testável de forma independente por meio de validação de auditoria e alertas.

### Oportunidades de paralelismo

- T003 e T004 podem executar em paralelo após T001 e T002.
- T006, T007, T008, T010, T011 e T013 podem executar em paralelo durante a fase fundacional.
- Tarefas de teste dentro de cada história de usuário marcadas com `[P]` podem executar em paralelo.
- T017, T018 e T019 podem executar em paralelo assim que os contratos fundacionais estiverem completos.
- T026, T027 e T028 podem executar em paralelo após os contratos de administração de usuários estarem finalizados.
- T034, T035 e T036 podem executar em paralelo enquanto T037 e T038 integram as mudanças de runtime.
- T043, T044 e T045 podem executar em paralelo assim que o schema de eventos estiver estável.

## Estratégia de implementação

### MVP primeiro

1. Completar Fase 1 e Fase 2.
2. Completar História de usuário 1.
3. Validar login Cognito, MFA e acesso a API protegida ponta a ponta antes de expandir o escopo.

### Entrega incremental

1. Entregar login respaldado pelo Cognito e acesso a rotas protegidas.
2. Adicionar gestão do ciclo de vida de usuários e mapeamento de papéis.
3. Adicionar authorizers do API Gateway, WAF, CORS, throttling, segredos e controles de criptografia.
4. Adicionar monitoramento nativo AWS, auditoria e agregação de findings.
5. Concluir documentação, ADRs, revisão de dependências e validação operacional.

### Estratégia de equipe

1. Um desenvolvedor pode preparar templates de infraestrutura AWS enquanto outro refatora abstrações de auth e testes.
2. Após a fase fundacional, trabalho de front-end e API de auth para US1 pode prosseguir em paralelo.
3. Trabalho de administração de usuários, endurecimento de perímetro e monitoramento pode ser dividido entre desenvolvedores separados assim que os contratos compartilhados estiverem estáveis.

## Notas

- Manter integrações Cognito, Secrets Manager e KMS atrás de abstrações de aplicação em vez de chamar SDKs AWS diretamente da UI ou orquestração de casos de uso.
- Garantir que logs e findings de segurança nunca exponham senhas em texto puro, tokens brutos ou valores de segredos.
- Verificar que os testes falham antes de implementar o comportamento correspondente.
- Parar em cada checkpoint de história de usuário para validar a história de forma independente antes de continuar.
