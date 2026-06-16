# Contribuindo — o que versionar

## Artefatos do produto (commitar)

| Pasta | Conteúdo |
|-------|----------|
| `docs/` | ADRs, C4, SLOs, roadmap, constituição |
| `specs/` | Especificações e planos de features |
| `infra/` | Docker, observabilidade, templates AWS (exceto `generated/`) |
| `src/`, `tests/` | Código e testes |
| `scripts/` | Automação local e testes de carga |
| `AspireApp1.AppHost/`, `AspireApp1.ServiceDefaults/` | Orquestração Aspire |

## Fora do repositório (gitignored)

| Pasta | Motivo |
|-------|--------|
| `tools/spec-kit/` | Ferramenta Spec Kit — workflows `/speckit.*` locais |
| `**/bin/`, `**/obj/` | Build |
| `infra/**/generated/` | Config gerada em dev |
| `scripts/reports/` | Relatórios de carga |

## Spec Kit

Ver [`../tools/spec-kit/README.md`](../tools/spec-kit/README.md) para junction/symlink de `.specify` na raiz, se necessário.
