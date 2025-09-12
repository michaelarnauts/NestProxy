<#
.SYNOPSIS
  Regenerates THIRD_PARTY_NOTICES.md and the licenses/ directory using nuget-license.

.DESCRIPTION
  Runs the nuget-license tool with:
    - include transitive dependencies
    - markdown output
    - allowed license policy
    - ignored package patterns
    - downloaded license texts

  Optionally shows git diff after regeneration and can fail if differences exist
  (useful in CI to detect that a dependency update forgot to regenerate notices).

.PARAMETER Solution
  Path to the solution (default: NestProxy.sln)

.PARAMETER Allowed
  Path to allowed-licenses.json (default: allowed-licenses.json)

.PARAMETER Ignored
  Path to ignored-packages.json (default: ignored-packages.json)

.PARAMETER NoticesFile
  Output notice file (default: THIRD_PARTY_NOTICES.md)

.PARAMETER LicensesDir
  Directory to hold downloaded license texts (default: licenses)

.PARAMETER ShowDiff
  If set, shows git diff for changed files after generation.

.PARAMETER FailOnChange
  If set, exits with code 2 if the notice file or licenses directory changed (after generation).

.EXAMPLE
  ./Update-Licenses.ps1

.EXAMPLE
  ./Update-Licenses.ps1 -ShowDiff -FailOnChange

.NOTES
  Requires:
    - Local tool manifest with nuget-license installed, or a global install.
    - PowerShell 5+ or PowerShell Core.
#>

[CmdletBinding()]
param(
  [string]$Solution = "NestProxy.sln",
  [string]$Allowed = "allowed-licenses.json",
  [string]$Ignored = "ignored-packages.json",
  [string]$NoticesFile = "THIRD_PARTY_NOTICES.md",
  [string]$LicensesDir = "licenses",
  [switch]$ShowDiff,
  [switch]$FailOnChange
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$m) Write-Host "[INFO ] $m" -ForegroundColor Cyan }
function Write-Warn { param([string]$m) Write-Host "[WARN ] $m" -ForegroundColor Yellow }
function Write-Err  { param([string]$m) Write-Host "[ERROR] $m" -ForegroundColor Red }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Write-Err ".NET SDK (dotnet) not found in PATH."
  exit 1
}

if (-not (Test-Path $Solution)) {
  Write-Err "Solution file not found: $Solution"
  exit 1
}

if (-not (Test-Path $Allowed)) {
  Write-Warn "Allowed licenses file not found: $Allowed (tool may report more)."
}
if (-not (Test-Path $Ignored)) {
  Write-Warn "Ignored packages file not found: $Ignored (no packages will be ignored)."
}

# Restore local tools if a manifest exists
if (Test-Path ".config/dotnet-tools.json") {
  Write-Info "Restoring local dotnet tools..."
  dotnet tool restore | Out-Null
} else {
  Write-Warn "No local tool manifest detected (.config/dotnet-tools.json). Assuming nuget-license is globally installed."
}

# Record pre-change state (for optional diff detection)
$preHash = @{}
if (Test-Path $NoticesFile) {
  $preHash["__NOTICE__"] = (Get-FileHash $NoticesFile -Algorithm SHA256).Hash
}
if (Test-Path $LicensesDir) {
  Get-ChildItem -Recurse -File $LicensesDir | ForEach-Object {
    $preHash[$_.FullName] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
  }
}

Write-Info "Cleaning licenses directory: $LicensesDir"
if (Test-Path $LicensesDir) {
  Remove-Item -Recurse -Force $LicensesDir
}
New-Item -ItemType Directory -Force -Path $LicensesDir | Out-Null

$cmd = @(
  "dotnet", "tool", "run", "nuget-license", "--",
  "-i", $Solution,
  "-t",
  "-o", "Markdown",
  "-fo", $NoticesFile,
  "-d", $LicensesDir
)

if (Test-Path $Ignored) { $cmd += @("-ignore", $Ignored) }
if (Test-Path $Allowed) { $cmd += @("-a", $Allowed) }

Write-Info "Executing: $($cmd -join ' ')"
$process = Start-Process -NoNewWindow -FilePath $cmd[0] -ArgumentList $cmd[1..($cmd.Length-1)] -PassThru -Wait

if ($process.ExitCode -ne 0) {
  Write-Err "nuget-license exited with code $($process.ExitCode)"
  exit $process.ExitCode
}

Write-Info "Generation complete."

# Post-change hashing
$changed = $false
$changedFiles = @()

if (Test-Path $NoticesFile) {
  $newHash = (Get-FileHash $NoticesFile -Algorithm SHA256).Hash
  if (-not $preHash.ContainsKey("__NOTICE__") -or $preHash["__NOTICE__"] -ne $newHash) {
    $changed = $true
    $changedFiles += $NoticesFile
  }
}

if (Test-Path $LicensesDir) {
  Get-ChildItem -Recurse -File $LicensesDir | ForEach-Object {
    $newHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    if (-not $preHash.ContainsKey($_.FullName) -or $preHash[$_.FullName] -ne $newHash) {
      $changed = $true
      $changedFiles += $_.FullName
    }
  }

  # Detect removed files (present before, gone now)
  foreach ($prev in $preHash.Keys | Where-Object { $_ -ne "__NOTICE__" }) {
    if ($prev -like "$LicensesDir*") {
      if (-not (Test-Path $prev)) {
        $changed = $true
        $changedFiles += "$prev (deleted)"
      }
    }
  }
}

if ($ShowDiff -and (Get-Command git -ErrorAction SilentlyContinue)) {
  Write-Info "Showing git diff for $NoticesFile (if tracked):"
  git diff -- $NoticesFile 2>$null | Write-Host
}

if ($changed) {
  Write-Info "License artifacts changed:"
  $changedFiles | Sort-Object | ForEach-Object { Write-Host " - $_" }
  if ($FailOnChange) {
    Write-Err "Changes detected; exiting with code 2 (FailOnChange)."
    exit 2
  }
} else {
  Write-Info "No changes to license artifacts."
}

Write-Info "DONE."