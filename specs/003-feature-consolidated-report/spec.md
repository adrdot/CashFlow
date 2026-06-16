# Especificação de feature: Relatório consolidado e dashboards

**Branch da feature**: `003-feature-consolidated-report`

**Criado em**: 2026-06-12

**Status**: Rascunho

> **Implementação atual:** modelo de leitura em **SQL Server (`reporting-db`)** alimentado por projeção SQS; cache **Redis**; caminho de escrita via EventStore. Ver [ADR 002](../../docs/adr/002-infraestrutura-stack-recursos.md).

**Entrada**: Descrição do usuário: "Feature: Consolidated Report & Dashboards User needs a daily consolidated balance report with charts (line, pie, bar) showing debit vs credit ratio and transaction volume. Data must be cached in Redis and exportable to CSV/PDF."

## Cenários de usuário e testes *(obrigatório)*

### História de usuário 1 - Visualizar relatório consolidado diário de saldo (Prioridade: P1)

Como usuário que monitora finanças, posso abrir um relatório consolidado diário que exibe o resumo de saldo de um dia selecionado para entender rapidamente a posição financeira do dia.

**Por que esta prioridade**: O relatório consolidado é o principal resultado de negócio. Sem a visualização diária de saldo, o dashboard e as exportações não oferecem suporte decisório significativo.

**Teste independente**: Pode ser testado por completo selecionando um dia com transações existentes e verificando que o sistema exibe o saldo consolidado diário junto com os totais subjacentes de débito e crédito.

**Cenários de aceite**:

1. **Dado** que existem transações para um dia selecionado, **Quando** o usuário abre o relatório consolidado desse dia, **Então** o sistema exibe o saldo consolidado diário, o total de débitos e o total de créditos.
2. **Dado** que não existem transações para um dia selecionado, **Quando** o usuário abre o relatório consolidado, **Então** o sistema exibe um relatório em estado vazio com totais zerados e sem dados enganosos nos gráficos.

---

### História de usuário 2 - Analisar atividade diária por meio de gráficos (Prioridade: P2)

Como usuário que revisa o comportamento do fluxo de caixa, posso ver gráficos de linha, pizza e barras que resumem a proporção débito versus crédito e o volume de transações para que tendências e distribuição sejam fáceis de interpretar.

**Por que esta prioridade**: A análise visual é a principal capacidade do dashboard que transforma valores consolidados brutos em insights acionáveis.

**Teste independente**: Pode ser testado por completo carregando dados de relatório para um dia com atividade mista de transações e verificando que os gráficos de linha, pizza e barras renderizam valores consistentes a partir do mesmo conjunto de dados diários.

**Cenários de aceite**:

1. **Dado** que existem dados de relatório para um dia selecionado, **Quando** o dashboard carrega, **Então** um gráfico de pizza exibe a proporção débito versus crédito usando os valores consolidados do dia.
2. **Dado** que existem dados de relatório para um dia selecionado, **Quando** o dashboard carrega, **Então** os gráficos de barras e linha exibem o volume de transações e os detalhamentos de valores diários derivados do mesmo conjunto de dados do relatório.

---

### História de usuário 3 - Exportar o relatório e beneficiar-se de leituras em cache (Prioridade: P3)

Como usuário que compartilha insights financeiros, posso exportar o relatório consolidado para CSV e PDF enquanto recebo carregamentos repetidos rápidos do relatório por meio de cache Redis.

**Por que esta prioridade**: Exportação e cache melhoram a utilidade operacional e o desempenho, especialmente para acesso repetido aos mesmos dados de relatório.

**Teste independente**: Pode ser testado por completo carregando o mesmo relatório duas vezes para verificar o comportamento de recuperação em cache e, em seguida, exportando o relatório exibido para CSV e PDF e confirmando que os arquivos exportados correspondem aos dados visíveis.

**Cenários de aceite**:

1. **Dado** que um relatório foi gerado para um dia selecionado, **Quando** o mesmo relatório é solicitado novamente dentro da vida útil do cache, **Então** o sistema atende o relatório a partir de dados em cache com suporte Redis ou fluxo de resposta em cache equivalente.
2. **Dado** que o usuário está visualizando um relatório consolidado, **Quando** o usuário o exporta como CSV ou PDF, **Então** o arquivo gerado contém os mesmos totais consolidados e métricas suportadas pelos gráficos exibidos no dashboard.

### Casos de borda

- O que acontece quando o dia selecionado tem apenas débitos ou apenas créditos, produzindo uma proporção unilateral?
- O que acontece quando não há transações para o dia selecionado?
- Como o sistema lida com volumes muito grandes de transações diárias que podem afetar a legibilidade dos gráficos ou o tamanho da exportação?
- Como o sistema se comporta quando o Redis está indisponível e o relatório precisa ser gerado sem suporte de cache?
- Como o sistema responde quando a geração de CSV ou PDF falha após os dados do relatório terem sido preparados?
- Como o sistema lida com transações que chegam tardiamente e alteram um relatório diário previamente em cache?

## Requisitos *(obrigatório)*

### Requisitos funcionais

- **FR-001**: O sistema DEVE fornecer um relatório consolidado diário de saldo para um dia selecionado pelo usuário.
- **FR-002**: O sistema DEVE calcular e exibir, no mínimo, o total de débitos, o total de créditos e o saldo consolidado resultante para o dia selecionado.
- **FR-003**: O sistema DEVE fornecer visualizações de dashboard incluindo gráfico de linha, gráfico de pizza e gráfico de barras para os dados do relatório selecionado.
- **FR-004**: O sistema DEVE exibir a proporção débito versus crédito nas visualizações do dashboard.
- **FR-005**: O sistema DEVE exibir o volume de transações para o período de relatório selecionado.
- **FR-006**: O sistema DEVE garantir que todos os gráficos exibidos sejam derivados do mesmo conjunto de dados consolidados diários mostrado no resumo do relatório.
- **FR-007**: O sistema DEVE gerar dados de relatório a partir de registros de transações persistidos.
- **FR-008**: O sistema DEVE armazenar em cache dados de relatório consolidado no Redis para melhorar o desempenho de recuperação repetida do relatório.
- **FR-009**: O sistema DEVE definir e aplicar uma estratégia de invalidação ou atualização de cache quando os dados de transação de origem para o dia selecionado mudarem. [NECESSITA ESCLARECIMENTO: TTL exato do cache e gatilho de invalidação não especificados]
- **FR-010**: O sistema DEVE continuar fornecendo o relatório quando o Redis estiver indisponível, usando a fonte de dados primária com um caminho de desempenho degradado apropriado.
- **FR-011**: O sistema DEVE permitir que o usuário exporte o relatório consolidado para CSV.
- **FR-012**: O sistema DEVE permitir que o usuário exporte o relatório consolidado para PDF.
- **FR-013**: O sistema DEVE garantir que as saídas exportadas em CSV e PDF contenham os mesmos totais consolidados exibidos no relatório na tela.
- **FR-014**: O sistema DEVE retornar uma resposta de falha clara quando a geração da exportação não puder ser concluída.
- **FR-015**: O sistema DEVE representar dias em estado vazio sem saldos ou gráficos enganosos.
- **FR-016**: O sistema DEVE registrar telemetria operacional para geração de relatório, acertos ou falhas de cache e falhas de exportação.

### Entidades principais *(incluir se a feature envolver dados)*

- **Relatório consolidado diário**: Resumo financeiro agregado para um dia específico incluindo total de débitos, total de créditos, saldo consolidado, métricas de proporção e volume de transações.
- **Conjunto de dados do dashboard**: Dados normalizados prontos para gráficos derivados do relatório consolidado diário e usados de forma consistente nos gráficos de linha, pizza e barras.
- **Entrada de cache de relatório**: Representação armazenada no Redis de um relatório diário gerado, incluindo escopo da chave de cache, payload do relatório e metadados de atualidade.
- **Artefato de exportação**: Representação gerada em CSV ou PDF do relatório consolidado preparada para download ou entrega.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: 95% das requisições de relatório diário para dados não em cache concluem e renderizam em menos de 5 segundos.
- **SC-002**: 95% das requisições repetidas para o mesmo relatório diário dentro da vida útil do cache concluem em menos de 2 segundos.
- **SC-003**: 100% dos arquivos CSV e PDF exportados correspondem aos totais consolidados exibidos na interface para a mesma requisição de relatório.
- **SC-004**: 100% dos dias sem transações exibem um relatório válido em estado zero sem erros de cálculo nos gráficos.
- **SC-005**: 100% das requisições de relatório permanecem disponíveis quando o Redis está indisponível, embora possam operar fora das metas de desempenho com cache.

## Premissas

- As transações de origem já estão persistidas e disponíveis no armazenamento transacional primário antes da geração do relatório.
- O período de relatório desta feature é apenas diário; relatórios semanais, mensais e de intervalo personalizado estão fora do escopo.
- As exportações em PDF conterão uma representação estática do relatório e dos gráficos, em vez de um dashboard interativo.
- Autenticação e autorização do usuário para visualizar relatórios financeiros são tratadas em outro lugar e não são introduzidas por esta feature.
