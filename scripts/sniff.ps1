param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target,

    [string]$Model,

    [string]$Storage
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliProject = Join-Path $repoRoot "CodeSmellAuditor.Cli"

if (-not (Test-Path -LiteralPath $cliProject)) {
    Write-Error "CodeSmellAuditor.Cli not found at $cliProject"
    exit 1
}

$absoluteFile = $Target
if (-not [System.IO.Path]::IsPathRooted($absoluteFile)) {
    $absoluteFile = Join-Path (Get-Location) $Target
}
$absoluteFile = [System.IO.Path]::GetFullPath($absoluteFile)

Set-Location -LiteralPath $repoRoot

$cliArgs = @("sniff", $absoluteFile)
if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $cliArgs += @("--model", $Model)
}
if (-not [string]::IsNullOrWhiteSpace($Storage)) {
    $cliArgs += @("--storage", $Storage)
}

& dotnet run --project $cliProject -- @cliArgs
exit $LASTEXITCODE
