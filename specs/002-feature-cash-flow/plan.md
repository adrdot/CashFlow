# Plano de Implementação: Tela de Fluxo de Caixa

**Branch**: `002-feature-cash-flow` | **Data**: 2026-06-12 | **Spec**: `/specs/002-feature-cash-flow/spec.md`

> **Implementação atual (supersedido):** persistência em **EventStoreDB** + relay **SNS** ([ADR 002](../../docs/adr/002-infraestrutura-stack-recursos.md)). SQL Server permanece apenas no modelo de leitura de reporting.

**Entrada**: Especificação da feature em `/specs/002-feature-cash-flow/spec.md`

**Nota**: Este plano cobre apenas o recorte de transações de fluxo de caixa. Assume que a feature de login estabelece o acesso autenticado antes de o usuário chegar a esta tela, e não inclui relatórios consolidados além dos contratos de evento e persistência que esta feature deve expor.

## Resumo

Implementar uma tela de fluxo de caixa que registra transações de débito e crédito com valor, descrição e data da transação, persiste transações aceitas no SQL Server e publica eventos de transação registrada no RabbitMQ. A implementação será construída sobre a solução existente hospedada no Aspire e preservará os requisitos da constituição para modularidade, observabilidade, resiliência e validação automatizada.

Esta feature deve estender a estrutura atual com autenticação em primeiro lugar, adicionando um domínio de transações, casos de uso de aplicação, adaptadores de persistência e mensageria na infraestrutura, e uma UI web protegida para entrada de transações. O design deve manter as gravações de transação confiáveis, evitar respostas falsas de sucesso e deixar um caminho claro de recuperação quando SQL Server ou RabbitMQ falharem.

## Contexto Técnico

**Linguagem/Versão**: C# com .NET 10

**Dependências Principais**: ASP.NET Core, .NET Aspire AppHost, EF Core com provider SQL Server, cliente RabbitMQ ou abstração de mensageria, Serilog, métricas compatíveis com Prometheus

**Armazenamento**: SQL Server para persistência de transações, RabbitMQ para publicação de eventos de transação registrada

**Testes**: xUnit, Moq, Testcontainers para testes de integração com SQL Server e RabbitMQ, testes de contrato para a API de transações

**Plataforma Alvo**: Aplicação web containerizada e API hospedadas pelo ambiente de desenvolvimento local baseado em Aspire

**Tipo de Projeto**: Aplicação web com API transacional de backend e experiência de entrada de dados autenticada no navegador

**Metas de Performance**: Manter os fluxos de envio de transação alinhados com a linha de base do sistema de latência média abaixo de 200 ms para os caminhos de requisição suportados, atendendo ao critério de sucesso da feature de registrar envios válidos em menos de 5 segundos de ponta a ponta

**Restrições**: Validações devem rejeitar requisições malformadas antes da persistência, respostas de sucesso devem ser retornadas somente após commit no SQL Server, falhas no RabbitMQ não devem descartar transações já persistidas, e a implementação deve suportar monitoramento e fluxos de retry ou recuperação

**Escala/Escopo**: Uma tela protegida de entrada de transações, uma API de envio de transação, um modelo de persistência, um contrato de evento de transação registrada e telemetria de falha de suporte

## Verificação da Constituição

*GATE: Deve passar antes da pesquisa da Fase 0. Reavaliar após o design da Fase 1.*

- **Marcação Semântica e Simples**: Aprovado. A feature pode ser implementada com um formulário focado de entrada de transação, uma API de escrita enxuta e artefatos de documentação bem delimitados.
- **Acessibilidade por Padrão**: Aprovado com requisito de implementação. O formulário de transação deve expor rótulos claros, feedback de validação e mensagens de sucesso ou falha acessíveis.
- **Seguro por Padrão**: Aprovado com controles obrigatórios. O acesso deve permanecer autenticado, a validação de transações deve ser rigorosa, e o tratamento de falhas deve evitar confirmações falsas ou perda silenciosa de dados.
- **Linha de Base de Performance**: Aprovado condicionado ao design. Gravações no SQL Server e publicação no RabbitMQ devem ser instrumentadas e resilientes o suficiente para permanecer dentro da linha de base sob carga esperada.
- **Manutenibilidade em Primeiro Lugar**: Aprovado. Domínio de transações, persistência e mensageria devem ser isolados atrás de limites de aplicação e infraestrutura para expansão futura de CQRS ou reporting.

**Gatilhos de reavaliação após o design da Fase 1**:

- Confirmar o schema do SQL Server e a estratégia de migração para persistência de transações.
- Confirmar as convenções de exchange, fila e envelope de evento no RabbitMQ.
- Confirmar a estratégia de idempotência ou envio duplicado apontada como não resolvida na especificação da feature.

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/002-feature-cash-flow/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Código-Fonte (raiz do repositório)

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
│   └── CashFlow.Web/
└── tests/
    ├── CashFlow.Transactions.UnitTests/
    ├── CashFlow.Transactions.IntegrationTests/
    └── CashFlow.Transactions.ContractTests/
```

**Decisão de Estrutura**: Preservar o recorte de autenticação existente e adicionar um recorte de transações separado em `src` com projetos dedicados de domínio, aplicação, infraestrutura e API. Reutilizar `CashFlow.Web` para a UI autenticada de entrada de transações em vez de introduzir um segundo front-end web. Isso mantém a feature modular e se encaixa no layout atual do repositório já estabelecido pela implementação de login.

## Fases de Entrega

### Fase 0 - Pesquisa e decisões

- Identificar o design de tabela no SQL Server, necessidades de indexação e abordagem de migração para registros de transação.
- Decidir o contrato de evento no RabbitMQ, padrão de exchange ou roteamento, e estratégia de retry ou registro de falha.
- Resolver o comportamento de idempotência e envio duplicado deixado em aberto na especificação da feature.

### Fase 1 - Design de transações

- Definir o modelo de domínio de transação incluindo classificação de débito ou crédito, valor, descrição, data da transação, metadados de persistência e resultado de publicação.
- Definir o contrato da API de envio de transação e regras de validação.
- Definir os requisitos de observabilidade para sucesso de persistência, falha de persistência, sucesso de publicação e falha de publicação.

### Fase 2 - Caminho de escrita no backend

- Implementar validação de transação e tratamento de comandos.
- Implementar persistência no SQL Server e criação de identificador de transação.
- Implementar publicação de evento no RabbitMQ após persistência bem-sucedida.
- Implementar tratamento seguro de falhas para falhas no SQL Server e falhas no RabbitMQ após persistência.

### Fase 3 - Tela de fluxo de caixa

- Adicionar uma página protegida de entrada de fluxo de caixa em `CashFlow.Web`.
- Implementar fluxo de entrada de débito e crédito com validação de valor, descrição e data.
- Implementar estados de sucesso e falha voltados ao usuário sem expor detalhes de infraestrutura.

### Fase 4 - Verificação e endurecimento

- Adicionar testes unitários para validação, tratamento de comandos e mapeamento de eventos.
- Adicionar testes de integração para persistência no SQL Server, publicação no RabbitMQ e cenários de dependência degradada.
- Validar acessibilidade, performance, tratamento de falhas e observabilidade contra a especificação da feature e os gates da constituição.

## Riscos e Mitigações

- **Tratamento ambíguo de envio duplicado**: Mitigar documentando e decidindo a estratégia de idempotência antes do início da implementação.
- **Acoplamento no caminho de escrita entre persistência e mensageria**: Mitigar isolando a publicação de eventos atrás de um limite de aplicação e registrando falhas de publicação explicitamente.
- **Fragilidade de infraestrutura no desenvolvimento local**: Mitigar usando o wiring de recursos do Aspire e testes de integração com Testcontainers para SQL Server e RabbitMQ.
- **Confirmações falsas de sucesso sob falha parcial**: Mitigar retornando sucesso somente após commit e expondo falhas de publicação pós-persistência através de observabilidade e estado de recuperação.

## Rastreamento de Complexidade

| Violação | Por Que É Necessária | Alternativa Mais Simples Rejeitada Porque |
|----------|----------------------|-------------------------------------------|
| Camadas separadas de aplicação e infraestrutura de transações | Necessária para isolar persistência e publicação no RabbitMQ das preocupações de domínio e UI e suportar evolução futura de reporting e CQRS | Implementar gravações de transação diretamente em controllers ou nos projetos de autenticação existentes acoplaria preocupações não relacionadas e reduziria a testabilidade |
