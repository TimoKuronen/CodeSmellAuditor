# Testing

## Run tests

```powershell
dotnet test CodeSmellAuditor.slnx -c Release
```

CI runs the same command on Ubuntu for every push to `main`.

## What is covered (15 tests)

| Area | What it proves |
|------|----------------|
| `MarkdownRuleRepository` | Loads `.mdc` rules only, strips YAML front matter, throws on missing folder |
| `AuditPromptBuilder` | System/user prompt shape, character budget rejection |
| `AuditReportParser` | Reads `Status: COMPLIANT` / `Status: REVIEW REQUIRED`, fallback heuristics |
| `AuditEngine` | Writes report files, returns per-file pass/fail via fakes; empty targets returns empty result |

Engine tests use `NullAuditProgressReporter`, a fake `IRuleRepository`, and a fake `IAiOrchestrator` — no terminal UI and no network.

## What is not covered

- Real Ollama HTTP streaming (`OllamaAiOrchestrator`)
- `SpectreAuditProgressReporter` terminal rendering
- End-to-end audit quality or model output consistency
- CLI path resolution or interactive Enter-to-exit behavior

## Manual smoke test

Requires Ollama running with the configured model pulled:

```powershell
ollama pull qwen3.5:4b
dotnet run --project CodeSmellAuditor.Cli
```

Inspect the newest file in `WorkstationStorage/Reports/`.
