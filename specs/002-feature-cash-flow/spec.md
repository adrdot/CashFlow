# Especificação da Feature: Tela de Fluxo de Caixa

**Branch da Feature**: `002-feature-cash-flow`

**Criado em**: 2026-06-12

**Status**: Rascunho

> **Implementação atual (supersedido):** lançamentos persistem em **EventStoreDB** (append-only) e eventos integram via **Amazon SNS/SQS** (LocalStack em dev). Ver [ADR 002](../../docs/adr/002-infraestrutura-stack-recursos.md). Menções a SQL Server/RabbitMQ abaixo refletem o requisito original da feature, não o código final.

**Entrada**: Descrição do usuário: "Feature: Cash Flow Screen User needs to record debit and credit transactions with amount, description, and date. Transactions must be persisted in SQL Server and publish events to RabbitMQ."

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Registrar uma transação de fluxo de caixa (Prioridade: P1)

Como usuário que gerencia o fluxo de caixa, posso registrar uma transação de débito ou crédito com valor, descrição e data, para que o movimento passe a fazer parte do histórico financeiro do sistema.

**Por que esta prioridade**: Capturar transações é a ação de negócio central. Sem registro confiável, a tela não oferece valor operacional.

**Teste Independente**: Pode ser totalmente testado ao enviar uma nova transação de débito e uma de crédito e verificar que cada uma é aceita, armazenada e exibida como movimento registrado com sucesso.

**Cenários de Aceitação**:

1. **Dado** que o usuário está na tela de fluxo de caixa, **Quando** o usuário envia uma transação de débito válida com valor, descrição e data, **Então** o sistema armazena a transação e confirma o registro com sucesso.
2. **Dado** que o usuário está na tela de fluxo de caixa, **Quando** o usuário envia uma transação de crédito válida com valor, descrição e data, **Então** o sistema armazena a transação e confirma o registro com sucesso.

---

### História de Usuário 2 - Persistir transações no SQL Server (Prioridade: P2)

Como usuário de negócio, preciso que as transações registradas sejam persistidas de forma durável, para que os dados de fluxo de caixa permaneçam disponíveis após a conclusão da requisição.

**Por que esta prioridade**: O registro de transações só é confiável se o sistema confirmar os dados no sistema de registro.

**Teste Independente**: Pode ser totalmente testado ao criar uma transação e verificar que o registro completo é confirmado no SQL Server com os valores enviados e um identificador estável.

**Cenários de Aceitação**:

1. **Dado** um envio de transação válido, **Quando** o sistema aceita a requisição, **Então** a transação é persistida no SQL Server com seu tipo, valor, descrição, data e metadados de criação.
2. **Dado** que a gravação no SQL Server falha, **Quando** o usuário envia uma transação, **Então** o sistema reporta a falha e não trata a transação como registrada com sucesso.

---

### História de Usuário 3 - Publicar eventos de transação no RabbitMQ (Prioridade: P3)

Como serviço integrador, preciso de um evento quando uma transação é registrada, para que processos downstream possam reagir a novos movimentos de fluxo de caixa.

**Por que esta prioridade**: A publicação de eventos atende aos requisitos orientados a eventos da arquitetura e permite consumidores downstream sem acoplá-los ao caminho de escrita.

**Teste Independente**: Pode ser totalmente testado ao registrar uma transação válida e verificar que um evento correspondente é publicado no RabbitMQ com os detalhes da transação persistida.

**Cenários de Aceitação**:

1. **Dado** que uma transação foi persistida com sucesso, **Quando** a operação de gravação é concluída, **Então** o sistema publica um evento de transação registrada no RabbitMQ contendo o identificador da transação e os campos de negócio principais.
2. **Dado** que a publicação do evento falha após a persistência ter sucesso, **Quando** a requisição de transação é concluída, **Então** o sistema registra a falha de publicação para recuperação e não perde o registro da transação persistida.

### Casos de Borda

- O que acontece quando o valor é zero, negativo onde não é permitido, ou excede os limites numéricos configurados?
- O que acontece quando a descrição está vazia, contém apenas espaços em branco, ou é maior que o comprimento máximo suportado?
- Como o sistema trata uma data de transação no futuro ou muito distante no passado?
- Como o sistema se comporta se o mesmo envio for reenviado devido a um timeout do cliente?
- Como o sistema responde quando o SQL Server está indisponível durante a criação da transação?
- Como o sistema responde quando o RabbitMQ está indisponível após a transação ter sido persistida?

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: O sistema DEVE fornecer uma tela de fluxo de caixa para criar transações de débito e crédito.
- **FR-002**: O sistema DEVE exigir um tipo de transação de débito ou crédito para cada nova transação.
- **FR-003**: O sistema DEVE exigir um valor para cada transação antes do envio.
- **FR-004**: O sistema DEVE exigir uma descrição para cada transação antes do envio.
- **FR-005**: O sistema DEVE exigir uma data de transação para cada transação antes do envio.
- **FR-006**: O sistema DEVE validar que o valor está expresso em um formato numérico compatível com moeda antes de persistir a transação.
- **FR-007**: O sistema DEVE rejeitar envios de transação que estejam sem campos obrigatórios ou contenham formatos de campo inválidos.
- **FR-008**: O sistema DEVE persistir cada transação aceita no SQL Server.
- **FR-009**: O sistema DEVE armazenar, no mínimo, o tipo de transação, valor, descrição, data da transação e um identificador único de transação.
- **FR-010**: O sistema DEVE confirmar o registro com sucesso somente após a transação ter sido confirmada no SQL Server.
- **FR-011**: O sistema DEVE publicar um evento de transação registrada no RabbitMQ para cada transação persistida com sucesso no SQL Server.
- **FR-012**: O sistema DEVE incluir o identificador da transação persistida, tipo, valor, descrição e data da transação no payload do evento publicado.
- **FR-013**: O sistema DEVE tratar falhas de persistência no SQL Server retornando uma resposta de falha e evitando uma confirmação falsa de sucesso.
- **FR-014**: O sistema DEVE tratar falhas de publicação no RabbitMQ sem descartar uma transação persistida com sucesso.
- **FR-015**: O sistema DEVE registrar detalhes operacionais suficientes sobre falhas de persistência e publicação para suportar monitoramento e fluxos de retry ou recuperação.
- **FR-016**: O sistema DEVE impedir a criação duplicada de transações causada por reenvio imediato do mesmo pedido pelo cliente dentro da janela de idempotência definida ou processo de recuperação. [NECESSITA ESCLARECIMENTO: regra exata de deduplicação e janela não especificadas]

### Entidades Principais *(incluir se a feature envolve dados)*

- **Transação de Fluxo de Caixa**: Um único movimento financeiro classificado como débito ou crédito com valor, descrição, data da transação, identificador único e status de persistência.
- **Evento de Transação**: A mensagem de integração emitida após uma gravação bem-sucedida da transação, contendo a identidade da transação e os campos de negócio para consumidores downstream.
- **Resultado de Persistência**: O resultado registrado das etapas de gravação no SQL Server e publicação no RabbitMQ, incluindo o estado de sucesso ou falha necessário para monitoramento e recuperação.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 95% dos envios de transação válidos são registrados com sucesso em menos de 5 segundos de ponta a ponta.
- **SC-002**: 100% das transações registradas com sucesso são persistidas no SQL Server com os valores de valor, descrição, tipo e data enviados intactos.
- **SC-003**: 100% das transações persistidas com sucesso produzem um evento RabbitMQ correspondente ou um registro de falha de publicação recuperável.
- **SC-004**: 100% dos envios inválidos são rejeitados com uma resposta clara de validação ou falha e não são persistidos como transações concluídas.
- **SC-005**: 0 registros de transação são perdidos silenciosamente quando a publicação no RabbitMQ falha após a persistência no SQL Server ter sucesso.

## Premissas

- O usuário já está autenticado e autorizado a criar transações de fluxo de caixa antes de acessar esta tela.
- A infraestrutura de SQL Server e RabbitMQ já existe e é acessível pela aplicação.
- Edição, exclusão e listagem de transações históricas estão fora do escopo desta feature, salvo se introduzidas por uma especificação posterior.
- Consumidores de eventos esperam semântica de entrega at-least-once para mensagens de transação registrada.
