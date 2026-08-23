param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target
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
& dotnet run --project $cliProject -- sniff $absoluteFile
exit $LASTEXITCODE
