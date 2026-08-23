# CodeSmellAuditor

[![CI](https://github.com/TimoKuronen/CodeSmellAuditor/actions/workflows/ci.yml/badge.svg)](https://github.com/TimoKuronen/CodeSmellAuditor/actions/workflows/ci.yml)

Local-first CLI that reviews C# source against markdown governance rules using a local [Ollama](https://ollama.com/) model. It streams the critique live and saves a timestamped markdown report.

This is an **AI-assisted semantic code reviewer**, not a deterministic static analyzer. It does not use Roslyn or parse C# into a syntax tree; the LLM interprets plain source text against your rule packs.

Built while learning .NET architecture and agent-assisted workflows — the irony of using AI to audit AI-generated code is not lost on me.

![CodeSmellAuditor terminal output](docs/images/terminal-output.png)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) running locally with a pulled model (default: `qwen3.5:4b`)

## Run

Batch (files in Targets):

```powershell
dotnet run --project CodeSmellAuditor.Cli
```

Sniff a file in place (any path; no Targets copy):

```powershell
dotnet run --project CodeSmellAuditor.Cli -- sniff path\to\Program.cs
```

Drop `.cs` files into `WorkstationStorage/Targets/` for batch mode. Governance rules as `.mdc` go in `WorkstationStorage/Rules/`. Reports land in `WorkstationStorage/Reports/` for both batch and sniff. Sniff does not wait for Enter; exit code is `0` if the audit passed, `1` if review is required or the run failed.

## Configuration

Environment variables (all optional):

| Variable | Default | Purpose |
|----------|---------|---------|
| `CODESMELL_STORAGE` | `WorkstationStorage` under repo root | Root folder containing `Rules/`, `Targets/`, `Reports/` |
| `CODESMELL_MODEL` | `qwen3.5:4b` | Ollama model name |
| `CODESMELL_NUM_CTX` | `8192` | Model context window passed to Ollama |
| `CODESMELL_NUM_PREDICT` | `1200` | Max output tokens for the critique |

## How it works

1. `Program.cs` (composition root) parses args (batch vs `sniff`), resolves paths, and wires `MarkdownRuleRepository`, `OllamaAiOrchestrator`, `AuditEngine`, and `CliAuditHost`.
2. Core exposes a file-level API: load rules, audit one `.cs` file (budgeted prompt + Ollama stream), save a markdown report.
3. `CliAuditHost` owns Status wrapping for both Targets batch and path-based sniff.
4. Pass/fail is parsed from a `Status:` line in the report body.

Sample report format: [docs/sample-report-excerpt.md](docs/sample-report-excerpt.md)

More detail: [architecture](docs/architecture.md) · [known limitations](docs/known-limitations.md) · [testing](docs/testing.md)

## Tests

```powershell
dotnet test CodeSmellAuditor.slnx -c Release
```

Unit tests cover rule loading, prompt building, pass/fail parsing, and `AuditEngine` file/batch orchestration with fakes. Ollama HTTP streaming and Cli Status UX are exercised manually, not in CI.

## Current scope

- Single-file `.cs` audits against local markdown rule packs
- Path-based `sniff` entry for auditing a file where it already lives
- Local Ollama only (privacy-preserving; no cloud API)
- Interactive CLI with streaming terminal output
- Layered Core / Cli / Tests solution with constructor injection at the composition root

## Future implementation

- Full CLI arguments (`--model`, `--storage`, `--non-interactive`) beyond the `sniff` path entry
- Surface batch pass/fail summary in the CLI (engine already returns `AuditRunResult`; sniff already sets exit codes)
- Directory sniff (audit all `*.cs` under a folder)
- Roslyn-based deterministic pre-checks before the LLM pass
- Cloud model backend via a second `IAiOrchestrator` implementation

## License

MIT
