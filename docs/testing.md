# Testing

## Run tests

```powershell
dotnet test CodeSmellAuditor.slnx -c Release
```

CI runs the same command on Ubuntu for every push to `main`.

## What is covered

| Area | What it proves |
|------|----------------|
| `MarkdownRuleRepository` | Loads `.mdc` rules only, strips YAML front matter, throws on missing folder |
| `AuditPromptBuilder` | System/user prompt shape, character budget rejection |
| `AuditReportParser` | Reads `Status: COMPLIANT` / `Status: REVIEW REQUIRED`, fallback heuristics |
| `AuditEngine` | `AuditFileAsync` writes reports; `RunBatchAsync` returns empty or multi-file results via fakes |

Engine tests use a fake `IRuleRepository` and fake `IAiOrchestrator` — no terminal UI and no network. Interactive `CliAuditHost` Status wrapping is not unit-tested.

## What is not covered

- Real Ollama HTTP streaming (`OllamaAiOrchestrator`)
- `CliAuditHost` Spectre Status / streaming layout
- End-to-end audit quality or model output consistency
- CLI path resolution or interactive Enter-to-exit behavior

## Manual smoke test

Requires Ollama running with the configured model pulled:

```powershell
ollama pull qwen3.5:4b
dotnet run --project CodeSmellAuditor.Cli
```

Expect a yellow spinner while waiting on the model, then streamed critique text. Inspect the newest file in `WorkstationStorage/Reports/`.
