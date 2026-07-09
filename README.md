# CodeSmellAuditor

CLI tool that runs a local Ollama model against C# source files and checks them against a set of markdown governance rules. Streams the critique live and saves a timestamped report.

Built as part of learning .NET architecture while exploring agent-assisted workflows — the irony of using AI to audit AI-generated code is not lost on me.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) running locally with a pulled model (default: `qwen3.5:4b`)

## Run

```powershell
dotnet run --project CodeSmellAuditor.Cli
```

Drop `.cs` files into `WorkstationStorage/Targets/`, governance rules as `.mdc` into `WorkstationStorage/Rules/`. Reports land in `WorkstationStorage/Reports/`.

Override the storage root or model via `CODESMELL_STORAGE` and `CODESMELL_MODEL` env vars.

## How it works

`CodeSmellAuditor.Core` loads compact rule files, builds a budgeted prompt, and streams the model response token by token to the terminal. Each audit saves a markdown report with YAML front matter.

More detail: [architecture](docs/architecture.md) · [known limitations](docs/known-limitations.md)

## What's next

- CLI args for model and path instead of env vars
- Roslyn-based deterministic pre-checks before the AI pass
- Exit codes for CI use

## License

MIT