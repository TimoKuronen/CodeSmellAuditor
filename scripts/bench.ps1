param(
    [string]$Label = "",

    [string]$Model,

    [int]$Repeats = 0,

    [string]$Only
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$evalProject = Join-Path $repoRoot "CodeSmellAuditor.Eval"

if (-not (Test-Path -LiteralPath $evalProject)) {
    Write-Error "CodeSmellAuditor.Eval not found at $evalProject"
    exit 1
}

$evalArgs = @("--root", $repoRoot)
if (-not [string]::IsNullOrWhiteSpace($Label)) {
    $evalArgs += @("--label", $Label)
}
if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $evalArgs += @("--model", $Model)
}
if ($Repeats -gt 0) {
    $evalArgs += @("--repeats", $Repeats)
}
if (-not [string]::IsNullOrWhiteSpace($Only)) {
    $evalArgs += @("--only", $Only)
}

& dotnet run --project $evalProject --configuration Release -- @evalArgs
exit $LASTEXITCODE
