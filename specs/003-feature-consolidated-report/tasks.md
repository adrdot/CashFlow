---

description: "Lista de tarefas para implementação de relatório consolidado e dashboards"
---

# Tarefas: Relatório consolidado e dashboards

**Entrada**: Documentos de design em `/specs/003-feature-consolidated-report/`

**Pré-requisitos**: plan.md, spec.md

**Testes**: Incluir testes unitários, de integração e de contrato porque a constituição exige validação automatizada e a feature depende de agregação com suporte SQL Server, cache Redis e confiabilidade de exportação.

**Organização**: As tarefas estão agrupadas por história de usuário para que cada capacidade de reporting possa ser implementada e verificada independentemente.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: Pode executar em paralelo ao tocar arquivos diferentes sem dependência bloqueante
- **[Story]**: Mapeia a tarefa a uma história de usuário (`US1`, `US2`, `US3`)
- Cada tarefa inclui um caminho de destino exato

## Fase 1: Setup (infraestrutura compartilhada)

**Propósito**: Criar a estrutura da fatia de reporting e conectá-la à solução existente hospedada no Aspire.

- [ ] T001 Criar pastas da solução de reporting em `src/` e `tests/` para `CashFlow.Reporting.Api`, `CashFlow.Reporting.Application`, `CashFlow.Reporting.Domain`, `CashFlow.Reporting.Infrastructure`, `CashFlow.Reporting.UnitTests`, `CashFlow.Reporting.IntegrationTests` e `CashFlow.Reporting.ContractTests`
- [ ] T002 Adicionar projetos de reporting à solução e ao grafo de referências em `Aspire.CashFlow.slnx`
- [ ] T003 [P] Registrar a API de reporting mais a configuração de dependência Redis em `Aspire.CashFlow.AppHost/AppHost.cs`
- [ ] T004 [P] Estender os service defaults compartilhados para health checks de reporting, políticas de resiliência e métricas em `Aspire.CashFlow.ServiceDefaults/Extensions.cs`

---

## Fase 2: Fundacional (pré-requisitos bloqueantes)

**Propósito**: Construir o domínio de reporting, contratos, abstrações de acesso a dados, abstrações de cache, scaffolding de exportação e configuração necessários por todas as histórias de usuário.

**⚠️ CRÍTICO**: Nenhum trabalho de história de usuário deve começar até esta fase estar completa.

- [ ] T005 Definir entidades de domínio e value objects de reporting em `src/CashFlow.Reporting.Domain/Entities/DailyConsolidatedReport.cs`, `src/CashFlow.Reporting.Domain/Entities/DashboardDataset.cs`, `src/CashFlow.Reporting.Domain/Entities/ReportCacheEntry.cs` e `src/CashFlow.Reporting.Domain/ValueObjects/ReportDate.cs`
- [ ] T006 [P] Definir contratos de requisição, resposta e exportação de relatório em `src/CashFlow.Reporting.Application/Contracts/GetDailyReportRequest.cs`, `src/CashFlow.Reporting.Application/Contracts/DailyReportResult.cs` e `src/CashFlow.Reporting.Application/Contracts/ExportReportResult.cs`
- [ ] T007 [P] Definir abstrações de agregação, cache e exportação em `src/CashFlow.Reporting.Application/Abstractions/IReportRepository.cs`, `src/CashFlow.Reporting.Application/Abstractions/IReportCache.cs` e `src/CashFlow.Reporting.Application/Abstractions/IReportExportService.cs`
- [ ] T008 Implementar acesso DbContext de agregação com suporte SQL e scaffolding de projeção de consulta em `src/CashFlow.Reporting.Infrastructure/Persistence/ReportingReadRepository.cs` e `src/CashFlow.Reporting.Infrastructure/Persistence/Queries/DailyReportProjection.cs`
- [ ] T009 [P] Implementar opções de cache Redis e scaffolding do provider em `src/CashFlow.Reporting.Infrastructure/Caching/ReportingCacheOptions.cs` e `src/CashFlow.Reporting.Infrastructure/Caching/RedisReportCache.cs`
- [ ] T010 [P] Implementar scaffolding do serviço de exportação CSV e PDF em `src/CashFlow.Reporting.Infrastructure/Exports/CsvReportExportService.cs` e `src/CashFlow.Reporting.Infrastructure/Exports/PdfReportExportService.cs`
- [ ] T011 Configurar registro de dependências da API de reporting, acesso SQL, Redis, autenticação e problem details em `src/CashFlow.Reporting.Api/Program.cs` e `src/CashFlow.Reporting.Api/appsettings.json`
- [ ] T012 [P] Adicionar definições de métricas e eventos de log de reporting em `src/CashFlow.Reporting.Infrastructure/Observability/ReportingMetrics.cs` e `src/CashFlow.Reporting.Infrastructure/Observability/ReportingLogEvents.cs`
- [ ] T013 Criar o documento de contrato da API de reporting em `specs/003-feature-consolidated-report/contracts/reporting.openapi.yaml`

**Checkpoint**: Fundação pronta. O trabalho das histórias de usuário de reporting pode prosseguir independentemente.

---

## Fase 3: História de usuário 1 - Visualizar relatório consolidado diário de saldo (Prioridade: P1) 🎯 MVP

**Objetivo**: Permitir que um usuário autenticado solicite um dia selecionado e visualize totais consolidados de débitos, créditos, saldo e volume de transações.

**Teste independente**: Solicitar um dia com transações existentes e um dia sem transações, depois verificar que o dashboard retorna totais precisos e uma resposta correta em estado zero.

### Testes para história de usuário 1

- [ ] T014 [P] [US1] Adicionar testes unitários para agregação diária, cálculo de saldo e comportamento em estado zero em `tests/CashFlow.Reporting.UnitTests/Application/GetDailyReportHandlerTests.cs`
- [ ] T015 [P] [US1] Adicionar testes de contrato para o endpoint de consulta de relatório diário em `tests/CashFlow.Reporting.ContractTests/GetDailyReportContractTests.cs`
- [ ] T016 [P] [US1] Adicionar testes de integração para geração de relatório diário com suporte SQL em `tests/CashFlow.Reporting.IntegrationTests/DailyReportFlowTests.cs`

### Implementação para história de usuário 1

- [ ] T017 [P] [US1] Implementar o caso de uso de consulta de relatório diário em `src/CashFlow.Reporting.Application/UseCases/GetDailyReportHandler.cs`
- [ ] T018 [P] [US1] Implementar o endpoint de relatório diário em `src/CashFlow.Reporting.Api/Endpoints/ReportingEndpoints.cs`
- [ ] T019 [US1] Adicionar uma página protegida de relatório consolidado em `src/CashFlow.Web/Components/Pages/Reports.razor`
- [ ] T020 [P] [US1] Implementar o cliente da API de reporting em `src/CashFlow.Web/Services/ReportingApiClient.cs`
- [ ] T021 [US1] Conectar navegação da área autenticada para a tela de reporting em `src/CashFlow.Web/Components/Pages/Dashboard.razor` e `src/CashFlow.Web/Components/Layout/MainLayout.razor`

**Checkpoint**: A história de usuário 1 deve estar totalmente funcional e demonstrável como MVP.

---

## Fase 4: História de usuário 2 - Analisar atividade diária por meio de gráficos (Prioridade: P2)

**Objetivo**: Apresentar o mesmo conjunto de dados consolidado por meio de gráficos de linha, pizza e barras mostrando proporção débito versus crédito e volume de transações.

**Teste independente**: Carregar um relatório com atividade mista e verificar que cada gráfico renderiza valores consistentes com os totais de resumo e entre si.

### Testes para história de usuário 2

- [ ] T022 [P] [US2] Adicionar testes unitários para mapeamento do conjunto de dados dos gráficos e cálculos de proporção em `tests/CashFlow.Reporting.UnitTests/Application/BuildDashboardDatasetTests.cs`
- [ ] T023 [P] [US2] Adicionar testes de componente ou de contrato para consistência do payload dos gráficos em `tests/CashFlow.Reporting.ContractTests/DashboardDatasetContractTests.cs`
- [ ] T024 [P] [US2] Adicionar testes de integração para cenários de visualização com débito e crédito mistos em `tests/CashFlow.Reporting.IntegrationTests/DashboardVisualizationTests.cs`

### Implementação para história de usuário 2

- [ ] T025 [US2] Implementar construção de séries dos gráficos em `src/CashFlow.Reporting.Application/UseCases/GetDailyReportHandler.cs`
- [ ] T026 [P] [US2] Implementar view models e adaptadores reutilizáveis de gráficos em `src/CashFlow.Web/Components/Charts/ConsolidatedLineChart.razor`, `src/CashFlow.Web/Components/Charts/DebitCreditPieChart.razor` e `src/CashFlow.Web/Components/Charts/TransactionVolumeBarChart.razor`
- [ ] T027 [US2] Vincular a página de relatório consolidado aos gráficos de linha, pizza e barras em `src/CashFlow.Web/Components/Pages/Reports.razor`
- [ ] T028 [P] [US2] Adicionar rótulos acessíveis dos gráficos, legendas e descrições de estado vazio em `src/CashFlow.Web/Components/Pages/Reports.razor` e `src/CashFlow.Web/Components/Charts/`

**Checkpoint**: As histórias de usuário 1 e 2 devem funcionar independentemente com análises de dashboard consistentes.

---

## Fase 5: História de usuário 3 - Exportar o relatório e beneficiar-se de leituras em cache (Prioridade: P3)

**Objetivo**: Atender requisições repetidas de relatório com eficiência por meio de cache Redis e permitir exportações CSV/PDF que correspondam aos totais exibidos.

**Teste independente**: Solicitar o mesmo relatório diário duas vezes para verificar reutilização de cache e fallback degradado quando o Redis estiver indisponível, depois exportar arquivos CSV e PDF que correspondam aos totais do relatório na tela.

### Testes para história de usuário 3

- [ ] T029 [P] [US3] Adicionar testes unitários para decisões de acerto/falha de cache e comportamento de invalidação em `tests/CashFlow.Reporting.UnitTests/Caching/ReportCacheTests.cs`
- [ ] T030 [P] [US3] Adicionar testes de integração para cache de relatório com suporte Redis e fallback com Redis indisponível em `tests/CashFlow.Reporting.IntegrationTests/RedisCachingTests.cs`
- [ ] T031 [P] [US3] Adicionar testes de contrato para endpoints de exportação CSV e PDF em `tests/CashFlow.Reporting.ContractTests/ReportExportContractTests.cs`
- [ ] T032 [P] [US3] Adicionar testes de integração para consistência de artefatos CSV/PDF em `tests/CashFlow.Reporting.IntegrationTests/ReportExportTests.cs`

### Implementação para história de usuário 3

- [ ] T033 [US3] Implementar consulta, população e lógica de atualização de cache de relatório com suporte Redis em `src/CashFlow.Reporting.Infrastructure/Caching/RedisReportCache.cs` e `src/CashFlow.Reporting.Application/UseCases/GetDailyReportHandler.cs`
- [ ] T034 [P] [US3] Implementar geração de relatório CSV e PDF em `src/CashFlow.Reporting.Infrastructure/Exports/CsvReportExportService.cs` e `src/CashFlow.Reporting.Infrastructure/Exports/PdfReportExportService.cs`
- [ ] T035 [P] [US3] Implementar endpoints de exportação para CSV e PDF em `src/CashFlow.Reporting.Api/Endpoints/ReportingExportEndpoints.cs`
- [ ] T036 [US3] Adicionar ações de exportação, mensagens de estado de cache e feedback de leitura degradada em `src/CashFlow.Web/Components/Pages/Reports.razor`
- [ ] T037 [P] [US3] Adicionar ganchos de invalidação de cache para mudanças de transação em `src/CashFlow.Transactions.Application/UseCases/CreateTransactionHandler.cs` e `src/CashFlow.Reporting.Infrastructure/Caching/RedisReportCache.cs`

**Checkpoint**: Todas as histórias de usuário de reporting devem estar funcionalmente independentes.

---

## Fase 6: Polimento e preocupações transversais

**Propósito**: Finalizar documentação, validação operacional e trabalho de qualidade que abrange toda a feature de reporting.

- [ ] T038 [P] Documentar setup de reporting, inicialização de dependências e verificação local em `specs/003-feature-consolidated-report/quickstart.md`
- [ ] T039 [P] Registrar decisões de cache e exportação de reporting em `docs/adr/0005-reporting-cache-strategy.md` e `docs/adr/0006-report-export-approach.md`
- [ ] T040 Adicionar documentação do modelo de dados de reporting em `specs/003-feature-consolidated-report/data-model.md`
- [ ] T041 Capturar decisões de TTL Redis, invalidação, biblioteca de gráficos e biblioteca PDF em `specs/003-feature-consolidated-report/research.md`
- [ ] T042 Atualizar instruções de setup do repositório e uso de reporting em `README.md`

---

## Dependências e ordem de execução

### Dependências entre fases

- **Fase 1: Setup**: Sem dependências; pode começar imediatamente.
- **Fase 2: Fundacional**: Depende da Fase 1 e bloqueia todo o trabalho de histórias de usuário.
- **Fase 3: História de usuário 1**: Depende da Fase 2 e entrega o MVP.
- **Fase 4: História de usuário 2**: Depende da Fase 2 e constrói sobre o payload de relatório definido na US1.
- **Fase 5: História de usuário 3**: Depende da Fase 2 e estende o caminho de leitura com comportamento de cache e exportação.
- **Fase 6: Polimento**: Depende das histórias de usuário desejadas estarem completas.

### Dependências entre histórias de usuário

- **US1 (P1)**: Sem dependência de outras histórias de usuário após a fase fundacional.
- **US2 (P2)**: Reutiliza o payload de relatório da US1, mas permanece testável independentemente por meio de verificações de consistência dos gráficos.
- **US3 (P3)**: Reutiliza o payload de relatório da US1 e adiciona comportamento de cache e exportação, permanecendo testável independentemente por meio de verificação de cache e artefatos.

### Oportunidades de paralelismo

- T003 e T004 podem executar em paralelo após T001 e T002.
- T006, T007, T009, T010 e T012 podem executar em paralelo durante a fase fundacional.
- Tarefas de teste dentro de cada história de usuário marcadas com `[P]` podem executar em paralelo.
- T017, T018 e T020 podem executar em paralelo assim que os contratos fundacionais estiverem completos.
- T026 e T028 podem executar em paralelo após o contrato do conjunto de dados do dashboard estar finalizado.
- T033, T034 e T035 podem executar em paralelo após o contrato do payload de relatório estar finalizado.

## Estratégia de implementação

### MVP primeiro

1. Completar Fase 1 e Fase 2.
2. Completar História de usuário 1.
3. Validar recuperação de relatório diário ponta a ponta antes de expandir o escopo.

### Entrega incremental

1. Entregar um relatório consolidado diário protegido com totais precisos e tratamento de estado vazio.
2. Adicionar visualizações consistentes de linha, pizza e barras derivadas do mesmo conjunto de dados.
3. Adicionar reutilização de cache Redis mais exportação CSV/PDF e comportamento de fallback degradado.
4. Finalizar documentação, ADRs e validação de dependências.

### Estratégia de equipe

1. Um desenvolvedor pode preparar contratos de reporting e lógica de agregação enquanto outro configura cache Redis e scaffolding de exportação.
2. Após a fase fundacional, o trabalho de API e dashboard web para US1 pode prosseguir em paralelo.
3. O trabalho de cache e exportação na US3 pode ser dividido assim que o contrato central do payload de relatório estiver estável.

## Notas

- Manter agregação, política de cache e geração de exportação fora dos componentes de UI.
- Garantir que o mesmo payload consolidado alimente totais de resumo, gráficos e artefatos de exportação.
- Indisponibilidade do Redis deve degradar para leituras da fonte de dados em vez de falhar a requisição de relatório por completo.
- Parar em cada checkpoint de história de usuário para verificar comportamento independente antes de continuar.
