# Contribuindo — o que versionar

## Artefatos do produto (commitar)

| Pasta | Conteúdo |
|-------|----------|
| `docs/` | ADRs, C4, SLOs, roadmap, constituição |
| `specs/` | Especificações e planos de features |
| `infra/` | Docker, observabilidade, templates AWS (exceto `generated/`) |
| `src/`, `tests/` | Código e testes |
| `scripts/` | Automação local e testes de carga |
| `Aspire.CashFlow.AppHost/`, `Aspire.CashFlow.ServiceDefaults/` | Orquestração Aspire |

## Fora do repositório (gitignored)

| Pasta | Motivo |
|-------|--------|
| `tools/spec-kit/` | Ferramenta Spec Kit — workflows `/speckit.*` locais |
| `**/bin/`, `**/obj/` | Build |
| `infra/**/generated/` | Config gerada em dev |
| `scripts/reports/` | Relatórios de carga |

## Spec Kit

Ver [`../tools/spec-kit/README.md`](../tools/spec-kit/README.md) para junction/symlink de `.specify` na raiz, se necessário.

## Lint e formatação

| Comando | Ação |
|---------|------|
| `./scripts/lint.ps1` | Valida CSharpier e compila com analisadores |
| `./scripts/lint.ps1 -Fix` | Aplica analisadores + formata com CSharpier |

Ferramentas: [CSharpier](https://csharpier.com/) (manifesto em [`dotnet-tools.json`](../dotnet-tools.json)), NetAnalyzers via build.

Regras em [`.editorconfig`](../.editorconfig), [`.csharpierrc.json`](../.csharpierrc.json) e [`Directory.Build.props`](../Directory.Build.props).

## Segurança

| Comando | Ação |
|---------|------|
| `./scripts/security-audit.ps1` | Lista pacotes vulneráveis (NuGet) + build com analisadores de segurança |
| `./scripts/security-audit.ps1 -ReportOnly` | Apenas relatório, sem falhar o pipeline |
| `./scripts/security-audit.ps1 -FailOnPackageSeverity high` | Falha apenas em vulnerabilidades altas/críticas |

Ferramentas integradas:

- **NuGet Audit** — vulnerabilidades em pacotes diretos e transitivos (`NU1901`–`NU1904`)
- **SecurityCodeScan** — análise estática de código (injeção SQL, criptografia fraca, etc.)
- **NetAnalyzers** — regras `CA3xxx` de segurança no build
- **Dependabot** — PRs automáticos para atualizar pacotes (`.github/dependabot.yml`)

No Cursor/VS Code: extensão [CSharpier](https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode) + format on save (ver `.vscode/settings.json`).
