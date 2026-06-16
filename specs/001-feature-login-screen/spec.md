# Especificação de feature: Tela de login

**Branch da feature**: `001-feature-login-screen`

**Criado em**: 2026-06-12

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Feature: Login Screen User needs a secure login screen with email/password authentication, JWT token issuance, error handling for invalid credentials, and session persistence."

## Cenários de usuário e testes *(obrigatório)*

### História de usuário 1 - Autenticar com credenciais válidas (Prioridade: P1)

Como usuário registrado, posso entrar com meu e-mail e senha para acessar as partes protegidas da aplicação.

**Por que esta prioridade**: Este é o ponto de entrada principal da aplicação protegida. Sem autenticação bem-sucedida, nenhum fluxo protegido fica utilizável.

**Teste independente**: Pode ser testado por completo enviando credenciais válidas de uma conta ativa e verificando que o sistema concede acesso, emite um JWT e exibe o estado autenticado.

**Cenários de aceite**:

1. **Dado** um usuário registrado com conta ativa, **Quando** o usuário envia e-mail e senha válidos, **Então** o sistema autentica o usuário, emite um JWT válido, cria uma sessão persistida e encaminha o usuário para a página inicial autenticada.
2. **Dado** um usuário já autenticado com sessão persistida válida, **Quando** o usuário reabre a aplicação, **Então** o sistema restaura a sessão sem exigir novo login até a sessão expirar ou o usuário sair.

---

### História de usuário 2 - Rejeitar credenciais inválidas com segurança (Prioridade: P2)

Como usuário que digitou credenciais incorretas, recebo uma mensagem de erro clara sem expor se o e-mail ou a senha estava errado.

**Por que esta prioridade**: O tratamento de login inválido é necessário para segurança e usabilidade. Evita vazamento de informação e ajuda o usuário a corrigir erros comuns de digitação.

**Teste independente**: Pode ser testado por completo enviando senha incorreta ou e-mail desconhecido e verificando que o acesso é negado, nenhum JWT é emitido e a interface exibe erro genérico de autenticação.

**Cenários de aceite**:

1. **Dado** um usuário que informa senha incorreta para uma conta existente, **Quando** a requisição de login é enviada, **Então** o sistema nega o acesso, não retorna JWT, preserva o estado não autenticado e exibe erro genérico de credenciais inválidas.
2. **Dado** um usuário que informa um e-mail que não corresponde a nenhuma conta, **Quando** a requisição de login é enviada, **Então** o sistema retorna o mesmo erro genérico de credenciais inválidas usado para senhas incorretas.

---

### História de usuário 3 - Preservar e encerrar sessões autenticadas corretamente (Prioridade: P3)

Como usuário autenticado, permaneço logado após atualizar a aplicação e posso encerrar minha sessão explicitamente quando quiser.

**Por que esta prioridade**: A persistência de sessão melhora a usabilidade; o logout explícito é necessário para manter o estado de autenticação sob controle do usuário.

**Teste independente**: Pode ser testado por completo fazendo login, atualizando ou reiniciando o cliente, verificando que a sessão persiste, saindo e confirmando que a sessão persistida foi limpa.

**Cenários de aceite**:

1. **Dado** um usuário com sessão autenticada ativa, **Quando** a aplicação é recarregada, **Então** a sessão é restaurada do estado persistido e as telas protegidas permanecem acessíveis.
2. **Dado** um usuário autenticado, **Quando** o usuário faz logout, **Então** o sistema limpa a sessão persistida e o JWT e exige autenticação para futuras requisições protegidas.

### Casos extremos

- O que acontece quando o usuário envia o formulário de login com e-mail vazio, senha vazia ou ambos os campos ausentes?
- O que acontece quando o formato do e-mail é inválido antes do envio da requisição?
- Como o sistema responde quando o JWT expirou mas ainda existe uma sessão persistida localmente?
- Como o sistema responde quando o armazenamento de sessão contém um token malformado ou adulterado?
- Como o sistema se comporta quando o serviço de autenticação está temporariamente indisponível?
- Como o sistema trata tentativas repetidas de login com falha em um curto período?

## Requisitos *(obrigatório)*

### Requisitos funcionais

- **FR-001**: O sistema DEVE fornecer uma tela de login com campos de e-mail e senha e uma ação de envio.
- **FR-002**: O sistema DEVE validar que e-mail e senha estão presentes antes de tentar autenticar.
- **FR-003**: O sistema DEVE validar que o campo de e-mail segue um formato válido antes de enviar a requisição.
- **FR-004**: O sistema DEVE autenticar usuários com credenciais e-mail/senha por um canal de transporte seguro.
- **FR-005**: O sistema DEVE emitir um JWT após autenticação bem-sucedida.
- **FR-006**: O sistema DEVE incluir a identidade do usuário autenticado e as claims de autorização no JWT emitido.
- **FR-007**: O sistema DEVE persistir o estado da sessão autenticada para que uma sessão válida sobreviva a atualização ou reinício da aplicação.
- **FR-008**: O sistema DEVE restaurar a sessão autenticada do estado persistido somente quando o token armazenado permanecer válido.
- **FR-009**: O sistema DEVE rejeitar credenciais inválidas sem indicar se o e-mail ou a senha estava incorreto.
- **FR-010**: O sistema DEVE exibir um erro de autenticação amigável quando o login falhar por credenciais inválidas.
- **FR-011**: O sistema DEVE impedir acesso a áreas protegidas da aplicação quando não existir sessão autenticada válida.
- **FR-012**: O sistema DEVE limpar o JWT e o estado da sessão persistida no logout explícito.
- **FR-013**: O sistema DEVE detectar JWTs expirados, malformados ou adulterados e tratar a sessão como não autenticada.
- **FR-014**: O sistema DEVE retornar ou exibir uma resposta de falha não sensível quando o serviço de autenticação estiver indisponível.
- **FR-015**: O sistema DEVE registrar eventos de sucesso e falha de autenticação para monitoramento de segurança sem registrar senhas em texto puro.
- **FR-016**: O sistema DEVE proteger a entrada de senha para que não seja exibida em texto puro na interface.

### Entidades principais *(incluir se a feature envolver dados)*

- **Conta de usuário**: Identidade registrada que pode autenticar com e-mail e senha; inclui status da conta e contexto de autorização.
- **Tentativa de login**: Um envio de autenticação contendo o e-mail informado, timestamp, resultado e categoria do motivo da falha.
- **Sessão JWT**: Estado de sessão autenticada contendo o token emitido, expiração, metadados da sessão persistida e status atual de autenticação.

## Critérios de sucesso *(obrigatório)*

### Resultados mensuráveis

- **SC-001**: 95% dos usuários com credenciais válidas concluem o login e chegam à página inicial autenticada em menos de 30 segundos.
- **SC-002**: 100% das tentativas de login inválidas são rejeitadas sem revelar se o e-mail existe no sistema.
- **SC-003**: 100% dos logins bem-sucedidos emitem um JWT utilizável imediatamente para requisições autorizadas.
- **SC-004**: 95% das sessões persistidas válidas são restauradas com sucesso após atualização ou reinício da aplicação quando o token não expirou.
- **SC-005**: 100% das ações de logout explícito removem o estado de autenticação persistido antes da próxima requisição protegida.

## Premissas

- Contas de usuário já existem e fluxos de registro de senha estão fora do escopo desta feature.
- MFA, redefinição de senha e recuperação de conta estão fora do escopo da primeira versão da tela de login.
- Assinatura, regras de validação e gestão de segredos do JWT serão fornecidas pelo serviço de autenticação do backend.
- A persistência de sessão usará um mecanismo de armazenamento no cliente adequado ao tipo final da aplicação, com tokens armazenados conforme os padrões de segurança do projeto.
