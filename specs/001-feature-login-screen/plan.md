# Plano de Implementação: Tela de Login

**Branch**: `001-feature-login-screen` | **Data**: 2026-06-12 | **Spec**: `/specs/001-feature-login-screen/spec.md`

**Entrada**: Especificação da funcionalidade em `/specs/001-feature-login-screen/spec.md`

**Nota**: Este plano cobre apenas a fatia de login seguro. Ele para intencionalmente em autenticação, emissão de JWT e tratamento de sessão, e não inclui funcionalidades de lançamento de transações ou relatórios.

## Resumo

Implementar um fluxo de login seguro que autentique usuários com e-mail e senha, emita JWTs em caso de autenticação bem-sucedida, restaure sessões persistidas válidas e rejeite estado de autenticação inválido ou expirado com segurança. A implementação estará alinhada à constituição do projeto ao usar .NET 10, limites modulares, observabilidade estruturada, padrões seguros por padrão e cobertura de testes automatizados.

A funcionalidade de login introduzirá o primeiro limite protegido da aplicação para o sistema mais amplo de fluxo de caixa. A implementação DEVE suportar as futuras funcionalidades de fluxo de caixa e relatórios ao estabelecer primitivas reutilizáveis de autenticação e autorização, sem acoplar preocupações de UI à validação de token ou à lógica de verificação de conta.

## Contexto Técnico

**Linguagem/Versão**: C# com .NET 10

**Dependências Principais**: ASP.NET Core, autenticação JWT bearer, serviço de identidade ou validação customizada de credenciais, .NET Aspire AppHost, Serilog, métricas compatíveis com Prometheus

**Armazenamento**: Repositório de contas de usuário existente para validação de credenciais; mecanismo de persistência de sessão no cliente para o JWT emitido e metadados de sessão

**Testes**: xUnit, Moq, testes de integração para endpoints de autenticação e comportamento de restauração de sessão

**Plataforma Alvo**: Aplicação web e API containerizadas, hospedadas no ambiente local de desenvolvimento baseado em Aspire

**Tipo de Projeto**: Aplicação web com API de autenticação no backend e experiência de login no cliente

**Metas de Performance**: Manter os fluxos de autenticação e validação de sessão alinhados à linha de base do sistema de latência média abaixo de 200 ms para os caminhos de requisição suportados

**Restrições**: Transporte seguro obrigatório, sem registro de senha em texto puro, respostas genéricas para credenciais inválidas, JWTs expirados ou malformados DEVEM ser rejeitados, restauração de sessão DEVE ocorrer apenas para tokens válidos

**Escala/Escopo**: Uma tela de login, endpoint de emissão de token, caminho de restauração de sessão persistida, caminho de encerramento de sessão e telemetria de autenticação de suporte

## Verificação da Constituição

*GATE: DEVE passar antes da pesquisa da Fase 0. Reavaliar após o design da Fase 1.*

- **Marcação Semântica e Simples**: Aprovado. A funcionalidade pode ser entregue com um fluxo de UI enxuto, superfície de API pequena e artefatos de documentação focados.
- **Acessibilidade por Padrão**: Aprovado com requisito de implementação. O formulário de login DEVE expor rótulos claros, mensagens de validação e envio e tratamento de erros acessíveis por teclado.
- **Seguro por Padrão**: Aprovado com controles obrigatórios. Emissão de JWT, validação de token, sigilo de senha, limites de autorização e falhas genéricas de autenticação são requisitos centrais.
- **Linha de Base de Performance**: Aprovado. O escopo da funcionalidade é pequeno, mas emissão e validação de token ainda DEVEM permanecer dentro da meta de latência do sistema.
- **Manutenibilidade em Primeiro Lugar**: Aprovado. A lógica de autenticação DEVE ficar isolada atrás de limites de aplicação e infraestrutura para que funcionalidades posteriores possam reutilizá-la de forma limpa.

**Gatilhos de reavaliação após o design da Fase 1**:

- Confirmar a origem das contas de usuário e o mecanismo de verificação de senha.
- Confirmar como segredos JWT, chaves de assinatura e tempos de vida de token são configurados por ambiente.
- Confirmar a tecnologia do cliente que persistirá e restaurará sessões autenticadas.

## Estrutura do Projeto

### Documentação (esta funcionalidade)

```text
specs/001-feature-login-screen/
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
│   └── CashFlow.Web/
└── tests/
    ├── CashFlow.Auth.UnitTests/
    ├── CashFlow.Auth.IntegrationTests/
    └── CashFlow.Auth.ContractTests/
```

**Decisão de Estrutura**: Manter os projetos host do Aspire na raiz do workspace e adicionar uma fatia focada de autenticação em ``, com domínio, aplicação, infraestrutura, API e web separados. Isso preserva limites de arquitetura limpa enquanto mantém a primeira funcionalidade pequena o suficiente para evoluir para o sistema mais amplo depois.

## Fases de Entrega

### Fase 0 - Pesquisa e decisões

- Identificar a origem das contas de usuário para validação de login e documentar se é persistência local, dados de teste semeados ou dependência externa de identidade.
- Decidir o conjunto de claims do JWT, estratégia de assinatura, política de expiração e expectativas de comportamento de refresh na primeira release.
- Decidir a abordagem de persistência de sessão no cliente e documentar suas compensações de segurança.

### Fase 1 - Design de autenticação

- Definir o modelo de domínio de autenticação, incluindo identidade da conta de usuário, resultado da tentativa de login e metadados da sessão JWT.
- Definir o contrato da API de autenticação para login, validação de token ou bootstrap de perfil e semântica de encerramento de sessão.
- Definir requisitos de observabilidade para caminhos de sucesso, falha e rejeição de token na autenticação.

### Fase 2 - Implementação do backend

- Implementar validação de credenciais e emissão de JWT.
- Implementar validação de token e infraestrutura de autorização para rotas protegidas.
- Implementar tratamento seguro de falhas para credenciais inválidas, tokens expirados e tokens malformados.
- Adicionar logging estruturado e métricas para tentativas de autenticação bem-sucedidas e falhas, sem expor senhas.

### Fase 3 - Fluxo de login no cliente

- Implementar o formulário de login com validação de e-mail e senha.
- Implementar persistência e restauração de sessão autenticada para sessões válidas.
- Implementar comportamento de logout que limpe o estado local de sessão e retorne o cliente a um estado não autenticado.

### Fase 4 - Verificação e endurecimento

- Adicionar testes unitários para validação, tratamento de credenciais, criação de token e lógica de rejeição de token.
- Adicionar testes de integração para login bem-sucedido, credenciais inválidas, sessões expiradas e comportamento de logout.
- Validar acessibilidade, performance e tratamento de falhas contra a spec da funcionalidade e os gates da constituição.

## Riscos e Mitigações

- **Origem de identidade pouco clara**: Mitigar decidindo cedo se esta release usa repositório local de contas ou uma fonte externa de usuários existente.
- **Escolha insegura de persistência de sessão**: Mitigar documentando a decisão de armazenamento no cliente e aplicando os padrões de segurança do projeto antes da implementação.
- **Fluxo de autenticação fortemente acoplado à UI**: Mitigar mantendo emissão e validação de token em serviços de backend com contratos estáveis consumidos pelo cliente.
- **Observabilidade fraca para falhas de autenticação**: Mitigar definindo logging e métricas de tentativas de login desde o início, sem armazenar entradas sensíveis.

## Rastreamento de Complexidade

| Violação | Por Que É Necessária | Alternativa Mais Simples Rejeitada Porque |
|----------|----------------------|-------------------------------------------|
| Camadas separadas de aplicação e infraestrutura de autenticação | Necessária para manter validação de credenciais, emissão de token e preocupações de entrega isoladas para reutilização por funcionalidades posteriores | Colocar lógica de autenticação diretamente em um controller ou na camada de UI tornaria a reutilização posterior de autorização frágil e mais difícil de testar |
