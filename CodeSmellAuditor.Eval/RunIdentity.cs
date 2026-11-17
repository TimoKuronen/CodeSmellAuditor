namespace CodeSmellAuditor.Eval;

/// <summary>
/// Who and what a run is: the label, model, git state and content hashes that let a later
/// reader attribute a change in results to prompt, rules, code, corpus or model.
/// </summary>
public sealed record RunIdentity(
    string RunId,
    DateTime DateUtc,
    string Label,
    string Model,
    string GitSha,
    bool Dirty,
    string CorpusHash,
    string PromptHash,
    string RulesHash,
    string CoreHash);
