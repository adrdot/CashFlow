# Especificação de feature: Segurança AWS e gestão de usuários

**Branch da feature**: `005-adicionar-seguran-front`

**Criado em**: 2026-06-12

**Status**: Rascunho

**Entrada**: Descrição do usuário: "crie uma spec para adicionar a segurança de meu front-end e das minhas api e gestão de usuário utilizando os recursos da aws, com os seguintes requisitos: Cognito para autenticação e gestão de usuários, JWT, API Gateway Authorizers, KMS, Secrets Manager, TLS, S3 encryption, WAF, Shield, throttling, CORS, CloudWatch, CloudTrail, GuardDuty, Security Hub e middleware de segurança no .NET"

## Cenários de usuário e testes *(obrigatório)*

### História de usuário 1 - Autenticar usuários com segurança no front-end e nas APIs (Prioridade: P1)

Como usuário da aplicação, posso entrar por um fluxo de identidade seguro com Cognito e usar a aplicação apenas enquanto mantiver uma sessão autenticada válida.

**Por que esta prioridade**: Identidade segura é o ponto de entrada de todo fluxo protegido. Sem autenticação e validação de token, o front-end e as APIs permanecem expostos ou inutilizáveis.

**Teste independente**: Pode ser testado por completo entrando via Cognito com MFA habilitado, obtendo um JWT, chamando uma API protegida pelo API Gateway e verificando que o acesso é concedido apenas enquanto o token for válido.

**Cenários de aceite**:

1. **Dado** que existe um usuário ativo registrado no repositório de identidade configurado, **Quando** o usuário entra pelo front-end com credenciais válidas e MFA obrigatório, **Então** o sistema autentica o usuário via Cognito, emite tokens JWT válidos e permite acesso protegido ao front-end e às APIs.
2. **Dado** que uma requisição aponta para um endpoint de API protegido, **Quando** a requisição contém um JWT ausente, expirado, malformado, revogado ou inválido, **Então** o authorizer do API Gateway rejeita a requisição antes de ela alcançar a API de backend.
3. **Dado** que o usuário possui uma sessão autenticada válida, **Quando** o front-end chama APIs downstream, **Então** cada requisição protegida é enviada via TLS e inclui o bearer token esperado para autorização.

---

### História de usuário 2 - Gerenciar usuários, papéis e integração de identidade corporativa (Prioridade: P2)

Como administrador de segurança, posso gerenciar contas de usuário, grupos, atribuições de papéis e configurações de federação para que as políticas de acesso sejam controladas centralmente e auditáveis.

**Por que esta prioridade**: Ciclo de vida de usuários e governança de papéis são necessários para ir além do login básico e manter a autorização alinhada às responsabilidades operacionais.

**Teste independente**: Pode ser testado por completo provisionando um usuário, atribuindo-o a um grupo Cognito ou papel mapeado, federando opcionalmente com Active Directory e verificando que as claims resultantes controlam permissões no front-end e nas APIs.

**Cenários de aceite**:

1. **Dado** que um administrador cria ou importa um usuário, **Quando** o usuário é atribuído a um grupo de segurança ou mapeamento de papel, **Então** os tokens emitidos contêm claims que refletem o contexto de autorização atribuído.
2. **Dado** que a federação corporativa está habilitada, **Quando** um usuário se autentica pelo provedor de diretório configurado, **Então** o Cognito aceita a identidade federada e aplica as permissões de aplicação mapeadas.
3. **Dado** que o acesso de um usuário é desabilitado ou revogado, **Quando** esse usuário tenta entrar ou reutilizar uma sessão existente além do comportamento de revogação permitido, **Então** o sistema nega o acesso protegido contínuo.

---

### História de usuário 3 - Proteger APIs, segredos e dados armazenados contra ataques comuns (Prioridade: P3)

Como proprietário da plataforma, preciso que o perímetro da aplicação, os segredos e os dados armazenados sejam endurecidos para reduzir ataques web comuns, exposição de chaves e transporte inseguro.

**Por que esta prioridade**: Autenticação sozinha é insuficiente se segredos forem expostos, o transporte for fraco ou a superfície da API puder ser abusada ou atacada diretamente.

**Teste independente**: Pode ser testado por completo verificando que APIs protegidas exigem HTTPS, regras WAF bloqueiam payloads maliciosos, throttling limita tráfego abusivo, origens confiáveis são aplicadas, segredos são resolvidos pelo Secrets Manager e recursos criptografados usam chaves gerenciadas.

**Cenários de aceite**:

1. **Dado** que uma requisição contém padrões de ataque de SQL injection ou XSS, **Quando** ela alcança o perímetro da API, **Então** o AWS WAF bloqueia a requisição conforme regras gerenciadas ou customizadas e registra o evento.
2. **Dado** que um componente da aplicação precisa de uma connection string de banco de dados ou chave de API, **Quando** ele carrega sua configuração em runtime, **Então** o segredo é obtido do AWS Secrets Manager em vez de arquivos de configuração versionados no código-fonte.
3. **Dado** que dados são armazenados em serviços persistentes suportados ou S3, **Quando** os dados são gravados em repouso, **Então** a criptografia está habilitada usando AES-256 gerenciado pela AWS ou chaves respaldadas pelo KMS conforme o tipo de recurso.

---

### História de usuário 4 - Detectar, auditar e responder a eventos relevantes de segurança (Prioridade: P4)

Como equipe de segurança e operações, podemos monitorar autenticação, abuso de API, mudanças de configuração e comportamento suspeito a partir de uma visão centralizada de segurança para que ameaças sejam detectadas e investigadas rapidamente.

**Por que esta prioridade**: Controles de segurança exigem visibilidade e auditabilidade; caso contrário, falhas, ataques ou misconfigurações permanecem indetectados.

**Teste independente**: Pode ser testado por completo gerando eventos de autenticação, requisições rejeitadas, mudanças de infraestrutura e padrões de acesso suspeitos, e verificando que logs, findings e alarmes aparecem nos serviços de monitoramento e segurança configurados.

**Cenários de aceite**:

1. **Dado** que APIs protegidas e serviços de identidade estão em uso, **Quando** ocorrem eventos de autenticação, autorização, throttling ou WAF, **Então** o sistema emite logs e métricas para o CloudWatch com detalhe suficiente para triagem operacional.
2. **Dado** que recursos AWS ou chamadas de API relevantes para segurança são alterados, **Quando** a ação é executada, **Então** o CloudTrail registra a mudança para auditoria posterior.
3. **Dado** que o GuardDuty ou Security Hub detecta atividade suspeita ou violação de política, **Quando** um finding é gerado, **Então** a equipe de operações pode visualizá-lo em um fluxo centralizado de postura de segurança e receber um alerta acionável.

### Casos extremos

- O que acontece quando o MFA é obrigatório, mas o usuário não consegue concluir o segundo fator a tempo?
- Como o sistema trata a expiração do JWT durante uma sessão ativa no front-end ou enquanto múltiplas chamadas de API estão em andamento?
- O que acontece quando o Cognito está temporariamente indisponível durante login, refresh de token ou provisionamento de usuário?
- Como o perímetro da API se comporta quando a origem confiável do front-end muda e a configuração de CORS ainda não foi atualizada?
- O que acontece quando uma regra gerenciada do WAF bloqueia uma requisição legítima e é necessária uma exceção ou revisão de regra customizada?
- Como o sistema responde quando a rotação do Secrets Manager falha ou um segredo rotacionado ainda não pode ser consumido por um serviço dependente?
- O que acontece quando permissões do KMS estão mal configuradas e uma aplicação deixa de conseguir descriptografar dados ou configuração em runtime?
- Como o sistema trata throttling para tráfego legítimo em rajada sem negar silenciosamente operações críticas de negócio?
- O que acontece quando um usuário é desabilitado após um token já ter sido emitido e janelas de revogação ou expiração de token ainda estão em vigor?
- Como a aplicação se comporta se o GuardDuty, Security Hub ou alarmes do CloudWatch produzem findings enquanto canais de notificação downstream estão indisponíveis?

## Requisitos *(obrigatório)*

### Requisitos funcionais

- **FR-001**: O sistema DEVE usar o Amazon Cognito como provedor de identidade gerenciado principal para autenticação da aplicação e gestão do ciclo de vida de usuários.
- **FR-002**: O sistema DEVE suportar login de usuário com Cognito usando nome de usuário ou e-mail e senha.
- **FR-003**: O sistema DEVE suportar MFA para usuários conforme a política de segurança definida.
- **FR-004**: O sistema DEVE suportar fluxos OAuth2 e OpenID Connect exigidos pela aplicação front-end.
- **FR-005**: O sistema DEVE suportar integração ou federação com Active Directory ou fonte de identidade corporativa equivalente quando habilitada para o ambiente.
- **FR-006**: O sistema DEVE emitir access tokens baseados em JWT para sessões autenticadas e usá-los na comunicação entre front-end e API.
- **FR-007**: O sistema DEVE garantir que APIs protegidas validem tokens JWT em toda requisição por meio de authorizers do API Gateway antes de encaminhar tráfego aos serviços de backend.
- **FR-008**: O sistema DEVE rejeitar requisições com tokens ausentes, inválidos, expirados, malformados ou não autorizados sem expor detalhes sensíveis de validação.
- **FR-009**: O sistema DEVE mapear usuários autenticados para papéis ou grupos de autorização da aplicação para que permissões possam ser aplicadas de forma consistente no front-end e nas APIs.
- **FR-010**: O sistema DEVE suportar operações administrativas de gestão de usuários para criar, desabilitar, reabilitar e atribuir usuários a grupos ou papéis de acesso.
- **FR-011**: O sistema DEVE fornecer comportamento definido para refresh de token, logout e tratamento de revogação no front-end e nas APIs protegidas.
- **FR-012**: O sistema DEVE exigir TLS ou HTTPS para todas as comunicações públicas cliente-para-borda e borda-para-serviço expostas pelo API Gateway.
- **FR-013**: O sistema DEVE aplicar regras de CORS que permitam apenas origens, métodos e cabeçalhos explicitamente confiáveis exigidos pelo front-end.
- **FR-014**: O sistema DEVE aplicar controles de throttling e quota do API Gateway para proteger APIs de taxas de requisição abusivas ou excessivas.
- **FR-015**: O sistema DEVE usar AWS WAF para inspecionar tráfego de entrada quanto a SQL injection, XSS e outros padrões maliciosos configurados.
- **FR-016**: O sistema DEVE suportar regras WAF customizadas ou exceções quando regras gerenciadas sozinhas forem insuficientes para o perfil de tráfego da aplicação.
- **FR-017**: O sistema DEVE depender do AWS Shield Standard ou equivalente configurado mais forte para proteção DDoS de base dos endpoints AWS expostos.
- **FR-018**: O sistema DEVE armazenar segredos da aplicação, connection strings, senhas, referências de material de assinatura e chaves de API no AWS Secrets Manager em vez de configuração de aplicação em texto puro.
- **FR-019**: O sistema DEVE suportar rotação automática para segredos elegíveis armazenados no AWS Secrets Manager.
- **FR-020**: O sistema DEVE usar AWS KMS para gerenciar chaves de criptografia em cenários suportados de criptografia de dados em repouso.
- **FR-021**: O sistema DEVE garantir que qualquer bucket S3 usado pela solução tenha criptografia server-side habilitada com AES-256 ou chaves gerenciadas pelo KMS.
- **FR-022**: O sistema DEVE garantir que componentes de aplicação e infraestrutura possam obter configuração e dados criptografados apenas por meio de acesso de menor privilégio.
- **FR-023**: O sistema DEVE enviar telemetria de autenticação, autorização, acesso à API, WAF e segurança operacional para logs e métricas do CloudWatch.
- **FR-024**: O sistema DEVE definir alarmes para falhas de autenticação, requisições negadas, picos anômalos de tráfego, eventos de throttling e outros indicadores críticos de segurança.
- **FR-025**: O sistema DEVE habilitar auditoria CloudTrail para atividade de API AWS relevante para segurança que afete o ambiente protegido.
- **FR-026**: O sistema DEVE habilitar Amazon GuardDuty para detecção de ameaças no ambiente AWS alvo.
- **FR-027**: O sistema DEVE agregar postura de segurança ou findings no AWS Security Hub para visibilidade centralizada e triagem.
- **FR-028**: O sistema DEVE validar e sanitizar entrada de API em .NET usando mecanismos de validação de modelo como validação de ModelState e DataAnnotations.
- **FR-029**: O sistema DEVE proteger envios de formulário no front-end contra CSRF onde a arquitetura da aplicação usa fluxos baseados em navegador com sessão ou cookies sensíveis.
- **FR-030**: O sistema DEVE prevenir XSS em caminhos de UI renderizados no servidor usando práticas seguras de codificação de saída do framework, como HtmlEncoder e padrões Razor quando aplicável.
- **FR-031**: O sistema DEVE aplicar rate limiting em nível de aplicação em .NET para caminhos de backend não totalmente protegidos por throttling na borda ou quando granularidade adicional de política for necessária.
- **FR-032**: O sistema DEVE garantir que logs de segurança não armazenem senhas em texto puro, valores brutos de segredos ou outros dados sensíveis proibidos.
- **FR-033**: O sistema DEVE incluir testes automatizados focados em segurança cobrindo fluxo de autenticação, validação de token, controle de acesso e validação de entrada.
- **FR-034**: O sistema DEVE incluir um processo para manter pacotes NuGet e dependências sensíveis à segurança atualizados em cadência regular.
- **FR-035**: O sistema DEVE documentar os limites de confiança exigidos e a divisão de responsabilidades entre front-end, API Gateway, Cognito, APIs de backend e serviços de segurança AWS.

### Entidades principais *(incluir se a feature envolver dados)*

- **Perfil de identidade**: Identidade de usuário gerenciada pela aplicação contendo estado de autenticação, status de MFA, fonte de federação, status do ciclo de vida e atribuições de grupo ou papel de autorização.
- **Sessão de access token**: Contexto de sessão autenticada contendo claims JWT, informações de expiração, metadados de emissor ou audience e estado de revogação ou refresh.
- **Mapeamento de política de autorização**: Relação entre grupos Cognito, papéis da aplicação, permissões de API e regras de acesso a rotas do front-end.
- **Segredo gerenciado**: Segredo de runtime protegido armazenado no Secrets Manager com versionamento, status de rotação e metadados de política de acesso.
- **Política de chave de criptografia**: Definição respaldada pelo KMS de quais principals e serviços podem criptografar ou descriptografar recursos protegidos específicos.
- **Registro de evento de segurança**: Representação normalizada de eventos de autenticação, requisições negadas, findings WAF, eventos de throttling ou mudanças de configuração enviados a sistemas de observabilidade e auditoria.
- **Finding de segurança**: Sinal de ameaça ou conformidade passível de triagem gerado pelo GuardDuty, Security Hub, análise do CloudTrail ou alarmes do CloudWatch.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: 100% dos endpoints de API protegidos rejeitam requisições que não apresentam um token válido aceito pelo authorizer configurado.
- **SC-002**: 95% dos logins interativos bem-sucedidos, incluindo MFA obrigatório, concluem em menos de 15 segundos em condições normais de operação.
- **SC-003**: 100% dos segredos de runtime usados pela aplicação são resolvidos a partir de armazenamento de segredos gerenciado em vez de configuração em texto puro versionada.
- **SC-004**: 100% do tráfego público da aplicação usa HTTPS sem caminho HTTP inseguro suportado para operações protegidas.
- **SC-005**: 100% das mudanças de control plane AWS relevantes para segurança são auditáveis por meio de logging CloudTrail habilitado para o ambiente alvo.
- **SC-006**: 95% das sondas de requisições maliciosas que correspondem às regras WAF habilitadas são bloqueadas na borda antes de alcançar APIs de backend.
- **SC-007**: 100% dos findings de segurança de alta severidade gerados pelos serviços de detecção AWS configurados aparecem no fluxo centralizado de monitoramento ou segurança em até 5 minutos.
- **SC-008**: 100% dos testes de regressão automatizados de segurança para autenticação, autorização e validação de entrada passam no pipeline de entrega antes do release.

## Premissas

- O front-end e as APIs da aplicação continuarão executando em infraestrutura integrada à AWS onde o API Gateway pode ser posicionado à frente das APIs protegidas.
- O Cognito é a plataforma de identidade estratégica alvo para novo trabalho de autenticação, enquanto qualquer repositório de identidade local legado é migrado ou delegado atrás do novo fluxo.
- A granularidade de autorização será baseada em papéis ou grupos na primeira versão desta feature; direitos finos por recurso estão fora do escopo, salvo inclusão posterior.
- O escopo inicial cobre controles de segurança para acesso ao front-end, exposição de API, tratamento de segredos, criptografia e monitoramento, mas não uma implementação completa de resposta a incidentes de SOC.
- Se a aplicação usar fluxos SPA puramente baseados em token sem formulários server-side, controles CSRF aplicam-se apenas às interações do navegador que ainda dependem de cookies ou credenciais equivalentes reutilizáveis.
- Períodos exatos de retenção para logs, trails do CloudTrail e findings de segurança seguirão padrões de governança do ambiente e não são especificados nesta feature.
