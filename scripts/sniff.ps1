param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Target,

    [string]$Model,

    [string]$Storage,

    [switch]$System,

    [string]$Manifest
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliProject = Join-Path $repoRoot "CodeSmellAuditor.Cli"
$callerCwd = Get-Location

if (-not (Test-Path -LiteralPath $cliProject)) {
    Write-Error "CodeSmellAuditor.Cli not found at $cliProject"
    exit 1
}

if ($null -eq $Target -or $Target.Count -eq 0) {
    Write-Error "At least one -Target .cs path is required."
    exit 1
}

if ($System -and $Target.Count -lt 2) {
    Write-Error "System audit requires at least two -Target .cs paths."
    exit 1
}

if (-not [string]::IsNullOrWhiteSpace($Manifest) -and -not $System) {
    Write-Error "-Manifest is only valid with -System."
    exit 1
}

$absoluteFiles = @()
foreach ($item in $Target) {
    $absoluteFile = $item
    if (-not [System.IO.Path]::IsPathRooted($absoluteFile)) {
        $absoluteFile = Join-Path $callerCwd $item
    }
    $absoluteFiles += [System.IO.Path]::GetFullPath($absoluteFile)
}

$absoluteManifest = $null
if (-not [string]::IsNullOrWhiteSpace($Manifest)) {
    $manifestPath = $Manifest
    if (-not [System.IO.Path]::IsPathRooted($manifestPath)) {
        $manifestPath = Join-Path $callerCwd $Manifest
    }
    $absoluteManifest = [System.IO.Path]::GetFullPath($manifestPath)
}

Set-Location -LiteralPath $repoRoot

$command = if ($System) { "sniff-system" } else { "sniff" }
$cliArgs = @($command) + $absoluteFiles
if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $cliArgs += @("--model", $Model)
}
if (-not [string]::IsNullOrWhiteSpace($Storage)) {
    $cliArgs += @("--storage", $Storage)
}
if (-not [string]::IsNullOrWhiteSpace($absoluteManifest)) {
    $cliArgs += @("--manifest", $absoluteManifest)
}

& dotnet run --project $cliProject -- @cliArgs
exit $LASTEXITCODE
