---

description: "Lista de tarefas para implementação da tela de fluxo de caixa"
---

# Tarefas: Tela de Fluxo de Caixa

**Entrada**: Documentos de design em `/specs/002-feature-cash-flow/`

**Pré-requisitos**: plan.md, spec.md

**Testes**: Incluir testes unitários, de integração e de contrato porque a constituição exige validação automatizada e a feature depende da confiabilidade do SQL Server e do RabbitMQ.

**Organização**: As tarefas são agrupadas por história de usuário para que cada capacidade de transação possa ser implementada e verificada de forma independente.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: Pode ser executada em paralelo ao tocar arquivos diferentes sem dependência bloqueante
- **[Story]**: Mapeia a tarefa a uma história de usuário (`US1`, `US2`, `US3`)
- Cada tarefa inclui um caminho de destino exato

## Fase 1: Setup (Infraestrutura Compartilhada)

**Propósito**: Criar a estrutura do recorte de transações e conectá-la à solução existente hospedada no Aspire.

- [ ] T001 Criar pastas da solução de transações em `src/` e `tests/` para `CashFlow.Transactions.Api`, `CashFlow.Transactions.Application`, `CashFlow.Transactions.Domain`, `CashFlow.Transactions.Infrastructure`, `CashFlow.Transactions.UnitTests`, `CashFlow.Transactions.IntegrationTests` e `CashFlow.Transactions.ContractTests`
- [ ] T002 Adicionar projetos de transações à solução e ao grafo de referências em `AspireApp1.slnx`
- [ ] T003 [P] Registrar a API de transações e suas dependências de infraestrutura em `AspireApp1.AppHost/AppHost.cs`
- [ ] T004 [P] Estender os service defaults compartilhados para health checks de transações, políticas de resiliência e métricas em `AspireApp1.ServiceDefaults/Extensions.cs`

---

## Fase 2: Fundacional (Pré-requisitos Bloqueantes)

**Propósito**: Construir o domínio de transações, contratos, acesso a dados, abstrações de mensageria e configuração necessários por todas as histórias de usuário.

**⚠️ CRÍTICO**: Nenhum trabalho de história de usuário deve começar até esta fase estar concluída.

- [ ] T005 Definir entidades de domínio e value objects de transação em `src/CashFlow.Transactions.Domain/Entities/CashFlowTransaction.cs`, `src/CashFlow.Transactions.Domain/Entities/PersistenceOutcome.cs` e `src/CashFlow.Transactions.Domain/ValueObjects/TransactionType.cs`
- [ ] T006 [P] Definir contratos de requisição, resposta e evento de transação em `src/CashFlow.Transactions.Application/Contracts/CreateTransactionRequest.cs`, `src/CashFlow.Transactions.Application/Contracts/CreateTransactionResult.cs` e `src/CashFlow.Transactions.Application/Contracts/TransactionRecordedEvent.cs`
- [ ] T007 [P] Definir abstrações de repositório e publicador em `src/CashFlow.Transactions.Application/Abstractions/ITransactionRepository.cs` e `src/CashFlow.Transactions.Application/Abstractions/ITransactionEventPublisher.cs`
- [ ] T008 Implementar DbContext do SQL Server, mapeamento de entidades e setup de migração em `src/CashFlow.Transactions.Infrastructure/Persistence/TransactionsDbContext.cs`, `src/CashFlow.Transactions.Infrastructure/Persistence/Configurations/CashFlowTransactionConfiguration.cs` e `src/CashFlow.Transactions.Infrastructure/Persistence/Migrations/`
- [ ] T009 [P] Implementar opções de conexão RabbitMQ e scaffolding do publicador em `src/CashFlow.Transactions.Infrastructure/Messaging/RabbitMqOptions.cs` e `src/CashFlow.Transactions.Infrastructure/Messaging/RabbitMqTransactionEventPublisher.cs`
- [ ] T010 Configurar registro de dependências da API de transações, SQL Server, RabbitMQ, autenticação e problem details em `src/CashFlow.Transactions.Api/Program.cs` e `src/CashFlow.Transactions.Api/appsettings.json`
- [ ] T011 [P] Adicionar definições de métricas e eventos de log de transação em `src/CashFlow.Transactions.Infrastructure/Observability/TransactionMetrics.cs` e `src/CashFlow.Transactions.Infrastructure/Observability/TransactionLogEvents.cs`
- [ ] T012 Criar o documento de contrato da API de envio de transação em `specs/002-feature-cash-flow/contracts/transactions.openapi.yaml`

**Checkpoint**: Fundação pronta. O trabalho das histórias de usuário de transação pode prosseguir de forma independente.

---

## Fase 3: História de Usuário 1 - Registrar uma transação de fluxo de caixa (Prioridade: P1) 🎯 MVP

**Objetivo**: Permitir que um usuário autenticado envie transações de débito e crédito com valor, descrição e data.

**Teste Independente**: Enviar transações válidas de débito e crédito pela tela protegida de fluxo de caixa e verificar que cada uma é aceita e confirmada ao usuário.

### Testes para História de Usuário 1

- [ ] T013 [P] [US1] Adicionar testes unitários para validação de transação e caminhos de sucesso do comando em `tests/CashFlow.Transactions.UnitTests/Application/CreateTransactionHandlerTests.cs`
- [ ] T014 [P] [US1] Adicionar testes de contrato para o endpoint de criação de transação em `tests/CashFlow.Transactions.ContractTests/CreateTransactionContractTests.cs`
- [ ] T015 [P] [US1] Adicionar testes de integração para fluxos válidos de envio de débito e crédito em `tests/CashFlow.Transactions.IntegrationTests/CreateTransactionFlowTests.cs`

### Implementação para História de Usuário 1

- [ ] T016 [P] [US1] Implementar o caso de uso de criação de transação em `src/CashFlow.Transactions.Application/UseCases/CreateTransactionHandler.cs`
- [ ] T017 [P] [US1] Implementar o endpoint de criação de transação em `src/CashFlow.Transactions.Api/Endpoints/TransactionEndpoints.cs`
- [ ] T018 [US1] Adicionar uma página protegida de entrada de fluxo de caixa em `src/CashFlow.Web/Components/Pages/CashFlow.razor`
- [ ] T019 [P] [US1] Implementar o cliente da API de transações em `src/CashFlow.Web/Services/TransactionsApiClient.cs`
- [ ] T020 [US1] Conectar navegação da área autenticada para a tela de fluxo de caixa em `src/CashFlow.Web/Components/Pages/Dashboard.razor` e `src/CashFlow.Web/Components/Layout/MainLayout.razor`

**Checkpoint**: A História de Usuário 1 deve estar totalmente funcional e demonstrável como MVP.

---

## Fase 4: História de Usuário 2 - Persistir transações no SQL Server (Prioridade: P2)

**Objetivo**: Garantir que cada transação aceita seja armazenada de forma durável no SQL Server com os campos de negócio e identificadores necessários.

**Teste Independente**: Enviar uma transação válida e verificar que ela é confirmada no SQL Server com tipo, valor, descrição, data e valores de identificador intactos.

### Testes para História de Usuário 2

- [ ] T021 [P] [US2] Adicionar testes unitários para mapeamento de persistência e comportamento do repositório em `tests/CashFlow.Transactions.UnitTests/Infrastructure/TransactionRepositoryTests.cs`
- [ ] T022 [P] [US2] Adicionar testes de integração para persistência no SQL Server e tratamento de falhas em `tests/CashFlow.Transactions.IntegrationTests/SqlPersistenceTests.cs`
- [ ] T023 [P] [US2] Adicionar testes de migração e verificação de schema em `tests/CashFlow.Transactions.IntegrationTests/TransactionsSchemaTests.cs`

### Implementação para História de Usuário 2

- [ ] T024 [US2] Implementar o repositório SQL Server em `src/CashFlow.Transactions.Infrastructure/Persistence/SqlTransactionRepository.cs`
- [ ] T025 [P] [US2] Adicionar geração de identificador de transação e tratamento de metadados de criação em `src/CashFlow.Transactions.Application/UseCases/CreateTransactionHandler.cs`
- [ ] T026 [P] [US2] Adicionar validação do formulário de transação e mensagens de falha de persistência em `src/CashFlow.Web/Components/Pages/CashFlow.razor`
- [ ] T027 [US2] Gerar e aplicar a migração inicial de transações em `src/CashFlow.Transactions.Infrastructure/Persistence/Migrations/`

**Checkpoint**: As Histórias de Usuário 1 e 2 devem funcionar de forma independente com persistência SQL confirmada.

---

## Fase 5: História de Usuário 3 - Publicar eventos de transação no RabbitMQ (Prioridade: P3)

**Objetivo**: Publicar um evento de transação registrada após persistência bem-sucedida e preservar recuperabilidade quando a publicação falhar.

**Teste Independente**: Enviar uma transação válida e verificar que o registro persistido produz um evento RabbitMQ ou um registro de falha de publicação recuperável sem perda de dados.

### Testes para História de Usuário 3

- [ ] T028 [P] [US3] Adicionar testes unitários para mapeamento de payload de evento e comportamento do publicador em `tests/CashFlow.Transactions.UnitTests/Messaging/TransactionRecordedEventTests.cs`
- [ ] T029 [P] [US3] Adicionar testes de integração para publicação bem-sucedida no RabbitMQ em `tests/CashFlow.Transactions.IntegrationTests/RabbitMqPublicationTests.cs`
- [ ] T030 [P] [US3] Adicionar testes de integração em modo degradado para recuperação de falha de publicação em `tests/CashFlow.Transactions.IntegrationTests/PublicationFailureRecoveryTests.cs`

### Implementação para História de Usuário 3

- [ ] T031 [US3] Implementar criação de evento de transação registrada em `src/CashFlow.Transactions.Application/UseCases/CreateTransactionHandler.cs`
- [ ] T032 [P] [US3] Implementar o publicador de eventos de transação no RabbitMQ em `src/CashFlow.Transactions.Infrastructure/Messaging/RabbitMqTransactionEventPublisher.cs`
- [ ] T033 [P] [US3] Adicionar registro de resultado de publicação e observabilidade em `src/CashFlow.Transactions.Infrastructure/Observability/TransactionPublicationAuditService.cs`
- [ ] T034 [US3] Adicionar tratamento voltado ao usuário para avisos de publicação pós-persistência em `src/CashFlow.Web/Components/Pages/CashFlow.razor`

**Checkpoint**: Todas as histórias de usuário de transação devem estar funcionalmente independentes.

---

## Fase 6: Polimento e Preocupações Transversais

**Propósito**: Finalizar documentação, validação operacional e trabalho de qualidade que abrange toda a feature de fluxo de caixa.

- [ ] T035 [P] Documentar setup de transações, inicialização de dependências e verificação local em `specs/002-feature-cash-flow/quickstart.md`
- [ ] T036 [P] Registrar decisões de persistência e mensageria de transações em `docs/adr/0003-transaction-persistence-strategy.md` e `docs/adr/0004-rabbitmq-transaction-events.md`
- [ ] T037 Adicionar documentação do modelo de dados de transação em `specs/002-feature-cash-flow/data-model.md`
- [ ] T038 Capturar decisões de schema SQL, envelope RabbitMQ e idempotência em `specs/002-feature-cash-flow/research.md`
- [ ] T039 Atualizar instruções de setup do repositório e uso do fluxo de caixa em `README.md`

---

## Dependências e Ordem de Execução

### Dependências entre Fases

- **Fase 1: Setup**: Sem dependências; pode começar imediatamente.
- **Fase 2: Fundacional**: Depende da Fase 1 e bloqueia todo o trabalho de histórias de usuário.
- **Fase 3: História de Usuário 1**: Depende da Fase 2 e entrega o MVP.
- **Fase 4: História de Usuário 2**: Depende da Fase 2 e constrói sobre o caminho de escrita definido na US1.
- **Fase 5: História de Usuário 3**: Depende da Fase 2 e do caminho de persistência estabelecido na US2.
- **Fase 6: Polimento**: Depende das histórias de usuário desejadas estarem concluídas.

### Dependências entre Histórias de Usuário

- **US1 (P1)**: Sem dependência de outras histórias de usuário após a fase fundacional.
- **US2 (P2)**: Reutiliza o fluxo de criação de transação da US1, mas permanece testável de forma independente através de verificação SQL.
- **US3 (P3)**: Reutiliza o fluxo de persistência da US2, mas permanece testável de forma independente através de verificação de publicação de eventos e recuperação.

### Oportunidades de Paralelismo

- T003 e T004 podem ser executadas em paralelo após T001 e T002.
- T006, T007, T009 e T011 podem ser executadas em paralelo durante a fase fundacional.
- Tarefas de teste dentro de cada história de usuário marcadas com `[P]` podem ser executadas em paralelo.
- T016, T017 e T019 podem ser executadas em paralelo assim que os contratos fundacionais estiverem concluídos.
- T024 e T027 podem ser executadas em paralelo após o DbContext e os mapeamentos estarem estabelecidos.
- T032 e T033 podem ser executadas em paralelo após o contrato de evento estar finalizado.

## Estratégia de Implementação

### MVP Primeiro

1. Concluir Fase 1 e Fase 2.
2. Concluir História de Usuário 1.
3. Validar envio de transação de ponta a ponta antes de expandir o escopo.

### Entrega Incremental

1. Entregar entrada de transação com UI autenticada e validação no backend.
2. Adicionar persistência confirmada no SQL Server e geração de identificador.
3. Adicionar publicação de evento no RabbitMQ e tratamento de recuperação de falha de publicação.
4. Finalizar documentação, ADRs e validação de dependências.

### Estratégia de Equipe

1. Um desenvolvedor pode construir contratos e validação de transações enquanto outro prepara o scaffolding de testes SQL e RabbitMQ.
2. Após a fase fundacional, trabalho de UI e API para US1 pode prosseguir em paralelo.
3. US2 e US3 podem ser divididas entre trabalho de persistência e mensageria assim que o comando central de transação estiver no lugar.

## Notas

- Manter validação de transação e orquestração de escrita fora dos componentes de UI.
- Não retornar confirmação de sucesso antes da conclusão do commit no SQL Server.
- Garantir que falhas de publicação no RabbitMQ sejam observáveis e recuperáveis sem perder a transação persistida.
- Parar em cada checkpoint de história de usuário para verificar comportamento independente antes de continuar.
