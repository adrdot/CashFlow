# Plano de implementação: Segurança AWS e gestão de usuários

**Branch**: `005-adicionar-seguran-front` | **Data**: 2026-06-12 | **Spec**: `/specs/005-adicionar-seguran-front/spec.md`

**Entrada**: Especificação de feature de `/specs/005-adicionar-seguran-front/spec.md`

## Resumo

Implementar uma linha de base de segurança para o front-end e as APIs do CashFlow usando identidade gerenciada pela AWS, proteção na borda, gestão de segredos, criptografia e monitoramento centralizado. A entrega substituirá o caminho atual de autenticação focado em JWT local por fluxos de identidade respaldados pelo Cognito, posicionará APIs protegidas atrás de authorizers do API Gateway, endurecerá segredos e criptografia com Secrets Manager e KMS, e introduzirá observabilidade em camadas usando CloudWatch, CloudTrail, GuardDuty e Security Hub, preservando os limites existentes da Clean Architecture.

## Contexto técnico

**Linguagem/Versão**: C# com .NET 10

**Dependências principais**: ASP.NET Core, componentes interativos Blazor Server, Microsoft.AspNetCore.Authentication.JwtBearer, Aspire ServiceDefaults, AWS Cognito, authorizers do API Gateway, AWS WAF, AWS Secrets Manager, AWS KMS, CloudWatch, CloudTrail, GuardDuty, Security Hub

**Armazenamento**: SQL Server para persistência transacional, Redis para cache de relatórios, Cognito User Pool para identidades, AWS Secrets Manager para segredos de runtime, criptografia gerenciada pelo KMS para recursos suportados, criptografia S3 se armazenamento de arquivos for introduzido

**Testes**: xUnit, Moq, testes de integração, testes de contrato, Testcontainers quando verificação respaldada por infraestrutura for necessária, além de testes automatizados de regressão de segurança para caminhos de authn/authz e validação

**Plataforma alvo**: Aplicação web distribuída hospedada na AWS composta por front-end Blazor e múltiplas APIs ASP.NET Core, com orquestração local via Aspire durante o desenvolvimento

**Tipo de projeto**: Aplicação web distribuída com front-end, API de autenticação, APIs de negócio e projetos de infraestrutura de suporte seguindo Clean Architecture

**Metas de performance**: Manter a linha de base da constituição de latência média abaixo de 200 ms para chamadas de API protegidas sob carga normal, manter throughput consolidado de relatórios em ou acima de 50 requisições por segundo quando aplicável, e atender a meta da spec de 95% dos logins MFA bem-sucedidos em menos de 15 segundos

**Restrições**: TLS em todos os pontos, acesso IAM de menor privilégio, sem segredos em texto puro na configuração, validação de token na borda antes da execução no backend, compatibilidade com a estrutura atual da solução .NET 10, e sem regressão dos fluxos existentes de relatórios ou transações

**Escala/Escopo**: Fluxo de segurança do front-end em `CashFlow.Web`, autenticação e orquestração de tokens em `CashFlow.Auth.*`, aplicação de perímetro para APIs de Reporting e Transactions, serviços centralizados de segurança AWS para o ambiente completo, e gestão do ciclo de vida de usuários/grupos para a primeira fatia de segurança pronta para produção

## Verificação da constituição

*GATE: Deve passar antes da pesquisa da Fase 0. Reavaliar após o design da Fase 1.*

- **Marcação semântica e simples**: Aprovado. A feature pode ser entregue com limites de segurança claros e artefatos de documentação focados.
- **Acessibilidade por padrão**: Aprovado com revisão necessária. MFA e UX de login devem permanecer utilizáveis no front-end Blazor e não devem criar barreiras de acesso inacessíveis no login.
- **Seguro por padrão**: Aprovado. Esta feature implementa diretamente o requisito da constituição de que autenticação, autorização, criptografia e proteção contra ataques sejam projetados desde o início.
- **Linha de base de performance**: Aprovado com requisito de monitoramento. A sobrecarga de authorizer, WAF e rate limiting deve ser medida para que chamadas de API protegidas permaneçam dentro da linha de base para tráfego normal.
- **Manutenibilidade em primeiro lugar**: Aprovado. O trabalho de integração AWS deve permanecer isolado atrás de abstrações de aplicação e infraestrutura, evitando acoplamento direto a serviços dentro de código de UI ou casos de uso.
- **Alinhamento de observabilidade**: Aprovado com nota de ampliação. A orientação existente de observabilidade cita Serilog, Prometheus e Grafana; esta feature adiciona CloudWatch, CloudTrail, GuardDuty e Security Hub para visibilidade de segurança nativa da AWS em vez de substituir a stack atual.

## Fases de implementação

### Fase 0 - Pesquisa e decisões

- Confirmar estratégia de user pool Cognito, identificadores de login, modo MFA, tempo de vida do token, política de refresh e requisitos de federação.
- Decidir se o `CashFlow.Auth.Api` atual se torna uma fachada de integração Cognito, uma API de gestão administrativa, ou ambos.
- Definir quais APIs são expostas pelo API Gateway primeiro e quais rotas permanecem apenas internas.
- Definir cobertura de rotação de segredos e modelo de propriedade de chaves KMS por ambiente.
- Registrar ADRs para estratégia de provedor de identidade, topologia de authorizers e divisão de observabilidade entre telemetria nativa AWS e existente.

### Fase 1 - Design e contratos

- Modelar identidade de usuário, mapeamentos grupo-para-papel, ciclo de vida de token/sessão e responsabilidades de acesso a segredos.
- Definir contratos de API para login, validação de sessão, logout, administração de usuários e comportamento de falha de autorização.
- Projetar fluxo de estado de autenticação no front-end para login Cognito, refresh de token, logout e recuperação de sessão expirada.
- Definir catálogo de alarmes CloudWatch, escopo de trail CloudTrail, habilitação do GuardDuty e expectativas de agregação do Security Hub.

### Fase 2 - Incremento 1: Fundação de identidade

- Integrar `CashFlow.Web` com fluxos de autenticação Cognito.
- Refatorar `CashFlow.Auth.Api` e `CashFlow.Auth.Infrastructure` para validar ou intermediar sessões respaldadas pelo Cognito em vez de depender apenas de identidade local em memória.
- Introduzir abstrações de mapeamento de papéis e grupos em `CashFlow.Auth.Application`.
- Adicionar logout seguro, refresh e tratamento de sessão revogada.

### Fase 3 - Incremento 2: Endurecimento do perímetro da API

- Posicionar APIs protegidas atrás do API Gateway.
- Configurar authorizers JWT e expectativas de autorização em nível de rota.
- Aplicar acesso apenas HTTPS, CORS restrito, throttling, quotas e regras WAF de base.
- Adicionar rate limiting em nível de aplicação onde controles na borda não oferecem granularidade suficiente.

### Fase 4 - Incremento 3: Segredos, criptografia e endurecimento em runtime

- Mover segredos de runtime de arquivos de configuração para o Secrets Manager.
- Introduzir políticas de criptografia respaldadas pelo KMS para stores suportados e S3 se armazenamento de arquivos for usado.
- Revisar validação de entrada .NET, antiforgery, codificação de saída e comportamento de logging sensível nos pontos de entrada afetados.

### Fase 5 - Incremento 4: Monitoramento, auditoria e operações de segurança

- Emitir logs e métricas relevantes para segurança no CloudWatch.
- Habilitar CloudTrail para atividade de API AWS relevante para segurança.
- Habilitar GuardDuty e agregar findings no Security Hub.
- Definir alarmes, dashboards e comportamento de roteamento de alertas para acesso negado, tráfego suspeito, picos de throttling e deriva de configuração.

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/005-adicionar-seguran-front/
├── plan.md
├── spec.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Código-fonte (raiz do repositório)

```text
src/
├── CashFlow.Web/
├── CashFlow.Auth.Api/
├── CashFlow.Auth.Application/
├── CashFlow.Auth.Domain/
├── CashFlow.Auth.Infrastructure/
├── CashFlow.Reporting.Api/
├── CashFlow.Reporting.Application/
├── CashFlow.Reporting.Domain/
├── CashFlow.Reporting.Infrastructure/
├── CashFlow.Transactions.Api/
├── CashFlow.Transactions.Application/
├── CashFlow.Transactions.Domain/
└── CashFlow.Transactions.Infrastructure/

tests/
├── CashFlow.Reporting.ContractTests/
├── CashFlow.Reporting.IntegrationTests/
├── CashFlow.Reporting.UnitTests/
├── CashFlow.Transactions.ContractTests/
├── CashFlow.Transactions.IntegrationTests/
└── CashFlow.Transactions.UnitTests/

AspireApp1.AppHost/
AspireApp1.ServiceDefaults/
```

**Decisão de estrutura**: Manter o layout multi-projeto existente da Clean Architecture. Concentrar abstrações de identidade e gestão de usuários em `CashFlow.Auth.Application` e `CashFlow.Auth.Domain`, adaptadores AWS em `CashFlow.Auth.Infrastructure`, endpoints de auth voltados à API em `CashFlow.Auth.Api`, e tratamento de sessão no front-end em `CashFlow.Web`. Aplicar segurança de perímetro de forma consistente às APIs de Reporting e Transactions sem colapsar bounded contexts. Adicionar testes automatizados focados em autenticação junto à estratégia de testes existente, criando projetos de teste Auth dedicados se a estrutura atual de cobertura se mostrar insuficiente.

## Fatias de entrega

1. Login e validação de sessão respaldados pelo Cognito para o front-end e a API de auth.
2. Mapeamento de papéis e grupos com endpoints ou fluxos de administração de usuários.
3. Authorizers do API Gateway, restrições CORS, throttling e regras WAF de base.
4. Adoção do Secrets Manager e KMS para configuração de runtime e criptografia.
5. Habilitação de CloudWatch, CloudTrail, GuardDuty e Security Hub com alarmes acionáveis.
6. Testes de regressão de segurança e fluxo de atualização de dependências.

## Riscos e mitigações

- **Risco**: A integração Cognito pode conflitar com a implementação atual de auth em memória e o fluxo de desenvolvimento local.
  **Mitigação**: Introduzir abstrações de provedor de auth e manter fallback local seguro para desenvolvimento apenas onde explicitamente permitido.
- **Risco**: Authorizers do API Gateway e regras WAF podem bloquear tráfego legítimo durante o rollout.
  **Mitigação**: Fazer rollout por rota ou ambiente, começar com observabilidade e ajuste de regras gerenciadas, e definir tratamento de exceções.
- **Risco**: Rotação de segredos pode quebrar serviços dependentes se o reload de configuração estiver incompleto.
  **Mitigação**: Definir expectativas de versionamento de segredos, validação na inicialização e smoke tests de rotação.
- **Risco**: Telemetria de segurança pode fragmentar entre stacks de monitoramento existentes e nativas da AWS.
  **Mitigação**: Documentar propriedade, roteamento de eventos e destinos de alerta em ADRs e runbooks operacionais.

## Rastreamento de complexidade

Nenhuma violação da constituição identificada no momento do plano.
