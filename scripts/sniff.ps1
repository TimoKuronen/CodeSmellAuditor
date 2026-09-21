param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Target,

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

if ($null -eq $Target -or $Target.Count -eq 0) {
    Write-Error "At least one -Target .cs path is required."
    exit 1
}

$absoluteFiles = @()
foreach ($item in $Target) {
    $absoluteFile = $item
    if (-not [System.IO.Path]::IsPathRooted($absoluteFile)) {
        $absoluteFile = Join-Path (Get-Location) $item
    }
    $absoluteFiles += [System.IO.Path]::GetFullPath($absoluteFile)
}

Set-Location -LiteralPath $repoRoot

$cliArgs = @("sniff") + $absoluteFiles
if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $cliArgs += @("--model", $Model)
}
if (-not [string]::IsNullOrWhiteSpace($Storage)) {
    $cliArgs += @("--storage", $Storage)
}

& dotnet run --project $cliProject -- @cliArgs
exit $LASTEXITCODE
