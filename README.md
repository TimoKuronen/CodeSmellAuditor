# CodeSmellAuditor

Local-first CLI that reviews C# source against markdown governance rules using a local [Ollama](https://ollama.com/) model. It streams the critique live and saves a timestamped markdown report.

This is an **AI-assisted semantic code reviewer**, not a deterministic static analyzer. It does not use Roslyn or parse C# into a syntax tree; the LLM interprets plain source text against your rule packs.

[![CI](https://github.com/TimoKuronen/CodeSmellAuditor/actions/workflows/ci.yml/badge.svg)](https://github.com/TimoKuronen/CodeSmellAuditor/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

![CodeSmellAuditor terminal output](docs/images/terminal-output.png)

## Highlights

- Single-file `.cs` audits against local markdown rule packs (`.mdc`)
- Batch mode via `WorkstationStorage/Targets/` and path-based `sniff` for files in place
- Local Ollama only — source stays on your machine
- Streaming terminal output with Spectre Status spinner until first token
- Timestamped markdown reports under `WorkstationStorage/Reports/`
- Pass/fail parsed from a `Status:` line in the report; batch and sniff set process exit codes (`0`/`1`)
- Batch mode prints a pass/fail summary after all Targets files; waits for Enter unless `--non-interactive`
- CLI flags: `--model`, `--storage`, `--non-interactive` (env vars `CODESMELL_MODEL` / `CODESMELL_STORAGE` override flags when set)
- Layered Core / Cli / Tests solution with constructor injection at the composition root
- Compact rule excerpts with character-budget validation before each audit
- 34 xUnit tests for rule loading, prompt building, pass/fail parsing, CLI flags, summaries, and engine orchestration (fakes; no Ollama in CI)
- Ubuntu CI via GitHub Actions

## Architecture

```text
Program.cs (composition root)
  -> MarkdownRuleRepository / OllamaAiOrchestrator
  -> AuditEngine
  -> CliAuditHost (batch + sniff)
```

Two projects plus tests: `CodeSmellAuditor.Core` (engine, interfaces, Ollama HTTP), `CodeSmellAuditor.Cli` (composition root and interactive host), `CodeSmellAuditor.Core.Tests`.

Details: [docs/architecture.md](docs/architecture.md)

## Stack

- C# / .NET 10
- [Ollama](https://ollama.com/) local HTTP API
- [Spectre.Console](https://spectreconsole.net/) (Status + streaming)
- xUnit
- Markdown rule packs (`.mdc`)

## Docs

- [Architecture](docs/architecture.md)
- [Sample report excerpt](docs/sample-report-excerpt.md)

## License

[MIT](LICENSE)
