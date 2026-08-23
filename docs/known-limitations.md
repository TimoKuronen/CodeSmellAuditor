# Known Limitations

## Product model

- **Not static analysis.** The tool sends plain source text to an LLM; it does not use Roslyn or deterministic rule evaluation.
- Model output is non-deterministic; the same file may produce different critiques between runs.
- Pass/fail is parsed from a `Status:` line in the report body. `sniff` sets process exit codes from that result; interactive batch still does not print a summary or set exit codes.

## AI dependency

- Requires Ollama running locally; no cloud fallback in the current version.
- Reasoning models may produce verbose output; a bounded report template and `num_predict` cap mitigate this.

## Token budget

- Full rule manuals plus full source files can exceed the model context window.
- Mitigation: compact `.mdc` rules at runtime; reference `.md` manuals are excluded from prompts.
- `AuditPromptBuilder.ValidateBudget` rejects oversized inputs before calling Ollama.
- Large single files may still exceed context even with compact rules.

## Scope

- Audits individual `.cs` files, not full solution context or cross-file dependencies.
- Rules are plain markdown files; pack version is noted inside each `.mdc` (RulePackVersion) but the tool does not enforce version checks.

## UX

- Interactive batch waits for Enter before exit; `sniff` exits immediately with a pass/fail exit code.
- Configuration is mostly environment-variable based; CLI args currently cover `sniff <path>` only (not `--model` / `--storage`).

## Security

- Sends full source code to the local Ollama instance only (not cloud by default).
- No secret scanning in target files before audit.

## Future implementation

See [README](../README.md#future-implementation) for fuller CLI args, batch summary display, directory sniff, and optional Roslyn pre-checks.
