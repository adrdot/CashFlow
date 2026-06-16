# Plano de implementação: Relatório consolidado e dashboards

**Branch**: `003-feature-consolidated-report` | **Data**: 2026-06-12 | **Spec**: `/specs/003-feature-consolidated-report/spec.md`

> **Implementação atual:** projeção via **Reporting.Worker** (SQS), pré-agregação `DailySummaries`, cache Redis ([ADR 002 §5](../../docs/adr/002-infraestrutura-stack-recursos.md#5-modelo-de-leitura-reporting--sql--redis)).

**Entrada**: Especificação de feature em `/specs/003-feature-consolidated-report/spec.md`

**Nota**: Este plano cobre apenas a fatia de relatórios consolidados. Assume acesso autenticado e registro de transações já existentes, e não redesenha o caminho de escrita de transações além dos modelos de leitura, ganchos de invalidação de cache e contratos de exportação necessários para reporting.

## Resumo

Implementar uma feature de relatório consolidado diário que agrega transações de fluxo de caixa persistidas em um relatório de saldo de um único dia, apresenta o resultado por meio de gráficos de linha, pizza e barras, armazena em cache payloads de relatório no Redis para acesso repetido rápido e suporta exportação em CSV e PDF. A implementação deve permanecer alinhada à constituição preservando arquitetura modular, comportamento de leitura degradada quando o Redis estiver indisponível e observabilidade forte em torno de geração de relatório e falhas de exportação.

Esta feature deve estender as fatias existentes de autenticação e transações adicionando um domínio de reporting e um fluxo de aplicação focado em agregação do lado de consulta em vez de escritas transacionais. O design deve manter o conjunto de dados dos gráficos, totais de resumo, entrada de cache e artefatos exportados derivados do mesmo payload de origem para que a interface, o cache e as exportações permaneçam consistentes.

## Contexto técnico

**Linguagem/Versão**: C# com .NET 10

**Dependências principais**: ASP.NET Core, .NET Aspire AppHost, biblioteca cliente Redis, acesso SQL Server sobre o armazenamento de transações existente, biblioteca de gráficos para UI Blazor, suporte a serialização CSV, biblioteca de geração de PDF, Serilog, métricas compatíveis com Prometheus

**Armazenamento**: SQL Server como fonte de verdade do sistema para transações; Redis para entradas de cache de relatório consolidado; geração transitória de arquivo ou stream para artefatos CSV e PDF

**Testes**: xUnit, Moq, testes de contrato para endpoints de relatório e exportação, testes de integração com Testcontainers para SQL Server e Redis, verificação em nível de UI para mapeamento do conjunto de dados dos gráficos quando prático

**Plataforma alvo**: Aplicação web e API containerizadas hospedadas pelo ambiente de desenvolvimento local baseado em Aspire

**Tipo de projeto**: Aplicação web com API de reporting no backend, modelo de leitura em cache, endpoints de exportação e experiência de dashboard autenticada

**Metas de desempenho**: Atender aos SLOs de consolidação em `docs/reporting-slo.md` (50 RPS, ≤5% de perda sob carga, latência média abaixo de 200 ms para leituras em cache), enquanto atende aos critérios de sucesso da feature de relatórios diários não em cache em menos de 5 segundos e carregamentos repetidos em cache em menos de 2 segundos

**Restrições**: Todos os gráficos devem ser alimentados pelo mesmo conjunto de dados consolidado, falhas do Redis devem degradar graciosamente para leituras da fonte de dados, saídas de exportação devem corresponder aos totais exibidos e o comportamento de atualização de cache deve ser determinístico quando os dados de transação mudarem

**Escala/Escopo**: Fluxo de relatório consolidado diário, geração de conjunto de dados pronto para gráficos, caminho de consulta e atualização de cache Redis, endpoints de exportação CSV/PDF e telemetria de suporte

## Verificação da constituição

*GATE: Deve passar antes da pesquisa da Fase 0. Reavaliar após o design da Fase 1.*

- **Marcação semântica e simples**: Aprovado. A feature pode ser entregue como uma tela de reporting focada, superfície estreita de API de consulta e artefatos de exportação bem delimitados.
- **Acessibilidade por padrão**: Aprovado com requisito de implementação. Gráficos e dados de resumo devem fornecer rótulos acessíveis, descrições alternativas e mensagens legíveis de estado vazio ou degradado.
- **Seguro por padrão**: Aprovado com controles obrigatórios. O acesso ao relatório deve permanecer autenticado, arquivos exportados devem conter apenas dados autorizados e o comportamento degradado de cache não deve expor saldos obsoletos ou incorretos silenciosamente.
- **Linha de base de desempenho**: Aprovado condicionado ao design. Cache com suporte Redis e agregação SQL eficiente são necessários para manter a recuperação repetida do relatório dentro da meta.
- **Manutenibilidade em primeiro lugar**: Aprovado. As preocupações de reporting devem permanecer isoladas das preocupações de escrita de transações para que o modelo de leitura, a lógica de exportação e a estratégia de cache possam evoluir independentemente.

**Gatilhos de reavaliação após o design da Fase 1**:

- Confirmar a chave de cache Redis, TTL e estratégia de invalidação para dados de relatório diário.
- Confirmar a biblioteca de gráficos e a abordagem de geração de PDF usadas pela fatia web/reporting.
- Confirmar se a geração de relatório lê diretamente do armazenamento de escrita de transações ou via uma abstração de repositório de reporting dedicada.

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/003-feature-consolidated-report/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Código-fonte (raiz do repositório)

```text
AspireApp1.AppHost/
AspireApp1.ServiceDefaults/

├── src/
│   ├── CashFlow.Auth.Api/
│   ├── CashFlow.Auth.Application/
│   ├── CashFlow.Auth.Domain/
│   ├── CashFlow.Auth.Infrastructure/
│   ├── CashFlow.Transactions.Api/
│   ├── CashFlow.Transactions.Application/
│   ├── CashFlow.Transactions.Domain/
│   ├── CashFlow.Transactions.Infrastructure/
│   ├── CashFlow.Reporting.Api/
│   ├── CashFlow.Reporting.Application/
│   ├── CashFlow.Reporting.Domain/
│   ├── CashFlow.Reporting.Infrastructure/
│   └── CashFlow.Web/
└── tests/
    ├── CashFlow.Reporting.UnitTests/
    ├── CashFlow.Reporting.IntegrationTests/
    └── CashFlow.Reporting.ContractTests/
```

**Decisão de estrutura**: Preservar as fatias existentes de auth e transações e adicionar uma fatia de reporting separada em `src` com projetos dedicados de domínio, aplicação, infraestrutura e API. Reutilizar `CashFlow.Web` para o dashboard de reporting autenticado para que a UI permaneça centralizada enquanto consultas de reporting, cache e exportação permanecem desacoplados das escritas de transações.

## Fases de entrega

### Fase 0 - Pesquisa e decisões

- Decidir a estrutura de chave de cache Redis, TTL e estratégia de invalidação para payloads de relatório diário.
- Decidir as bibliotecas de gráficos e geração de PDF que se encaixam na aplicação web Blazor existente.
- Decidir se a agregação de relatório lê diretamente do modelo de persistência de transações ou por meio de uma projeção de repositório de reporting dedicada.

### Fase 1 - Design de reporting

- Definir o modelo de domínio de reporting incluindo totais diários, saldo consolidado, proporção débito versus crédito, volume de transações, dados de séries dos gráficos e metadados de atualidade do cache.
- Definir os contratos de API de consulta e exportação de relatório para recuperação de relatório, download CSV e download PDF.
- Definir requisitos de observabilidade para geração de relatório, comportamento de acerto ou falha de cache, sucesso de exportação, falha de exportação e leituras degradadas do Redis.

### Fase 2 - Fluxo de reporting no backend

- Implementar agregação diária com suporte SQL e geração do conjunto de dados dos gráficos.
- Implementar consulta, população e invalidação ou atualização de cache Redis para payloads de relatório diário.
- Implementar geração de exportação CSV e PDF a partir do mesmo payload de relatório consolidado.
- Implementar comportamento degradado seguro quando o Redis estiver indisponível e tratamento explícito de falha quando a geração de exportação falhar.

### Fase 3 - Experiência do dashboard

- Adicionar uma página protegida de relatório consolidado em `CashFlow.Web`.
- Implementar a visualização do relatório diário com totais de resumo, tratamento de estado vazio e renderização consistente de gráficos de linha, pizza e barras.
- Implementar ações de exportação CSV e PDF com mensagens de sucesso e falha voltadas ao usuário que não exponham detalhes internos da infraestrutura.

### Fase 4 - Verificação e endurecimento

- Adicionar testes unitários para agregação diária, cálculos de proporção, mapeamento do conjunto de dados dos gráficos, decisões de cache e formatação de exportação.
- Adicionar testes de integração para geração de relatório com suporte SQL, comportamento de acerto ou falha de cache Redis, indisponibilidade degradada do Redis e endpoints de exportação.
- Validar acessibilidade, desempenho, consistência entre totais da interface e exportados e observabilidade contra a especificação da feature e os gates da constituição.

## Riscos e mitigações

- **Regras de atualidade do cache pouco claras**: Mitigar decidindo TTL e gatilhos de invalidação antes do início da implementação e documentando-os na pesquisa da feature e nos ADRs.
- **Divergência entre totais da interface, dados dos gráficos e exportações**: Mitigar gerando os três a partir de um único payload de relatório consolidado e verificando esse contrato em testes.
- **Consultas pesadas de agregação diária sob carga**: Mitigar adicionando projeções de consulta eficientes, indexação SQL direcionada e cache de leituras repetidas com suporte Redis.
- **Complexidade de integração de biblioteca de exportação ou gráficos**: Mitigar selecionando bibliotecas cedo e restringindo a primeira versão a exportações estáticas diárias com layouts determinísticos.

## Rastreamento de complexidade

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|----------|----------------------|-------------------------------------------|
| Camadas separadas de aplicação e infraestrutura de reporting | Necessária para isolar agregação SQL, cache Redis, geração de exportação e orquestração de consultas das preocupações de UI e escrita de transações | Construir relatórios diretamente dentro de controllers ou na fatia de transações existente acoplaria demais a evolução do modelo de leitura, política de cache e lógica de exportação a preocupações não relacionadas |
