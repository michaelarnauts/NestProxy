<#
.SYNOPSIS
  Updates ignored-packages.json with dynamically discovered non-runtime (build/test/tooling) package IDs.

.DESCRIPTION
  Classifies packages into:
    - RuntimePackages: packages appearing in published .deps.json of runtime entrypoint projects (or fallback assets).
    - AllPackages: union of packages from project.assets.json of runtime + (optionally) test projects (and optionally dotnet list output).
    - Dynamic (ignored) = AllPackages - RuntimePackages plus forced ignore pattern matches.

  Handles:
    - Blazor WASM (.deps.json under wwwroot/_framework) via recursive search.
    - Razor Class Libraries (no .deps.json) -> silently skipped for runtime classification.
    - Central Package Management (library IDs gathered directly from assets).
    - Test-only packages (included in ignore via test project assets).

  Result: ignored-packages.json is a plain JSON string array (compatible with nuget-license).

.PARAMETER Solution
  Path to solution (default: NestProxy.sln)

.PARAMETER RuntimeProjects
  Explicit runtime entrypoint projects. If omitted, auto-detects all .csproj excluding test pattern and classifies:
     - Runtime candidates: non-test projects
     - Test projects: names containing .Tests. or ending with Tests.csproj

.PARAMETER IgnoredFile
  Output ignore file (JSON array). Created/overwritten unless -DryRun.

.PARAMETER PublishOutputRoot
  Temporary publish directory root.

.PARAMETER ForcedIgnorePatterns
  Regex patterns whose matches are always added to dynamic ignore.

.PARAMETER SkipCleanPublish
  Do not delete existing publish directory (faster iterative runs).

.PARAMETER DryRun
  Compute but do not write IgnoredFile.

.PARAMETER OutputDynamicFile
  Optional secondary file containing only dynamic (non-runtime) package IDs.

.PARAMETER VerboseReport
  Print classification summary.

.PARAMETER Diag
  Print diagnostic samples (first N packages of each set).

.PARAMETER NoTestDependencies
  Do not incorporate test project packages into AllPackages.

.PARAMETER SkipDotnetList
  Skip invoking 'dotnet list package' (rely solely on assets.json libraries).

.PARAMETER IncludeProjects
  Optional glob/regex patterns to force-include certain projects (post autodetect filtering).

.PARAMETER ExcludeProjects
  Optional glob/regex patterns to force-exclude certain projects.

.EXAMPLE
  ./Update-IgnoredPackages.ps1

.EXAMPLE
  ./Update-IgnoredPackages.ps1 -RuntimeProjects src/NestProxy.Server/NestProxy.Server.csproj

.EXAMPLE
  ./Update-IgnoredPackages.ps1 -NoTestDependencies -VerboseReport -Diag

.NOTES
  Requires .NET SDK.
#>

[CmdletBinding()]
param(
  [string]$Solution = "NestProxy.sln",
  [string[]]$RuntimeProjects,
  [string]$IgnoredFile = "ignored-packages.json",
  [string]$PublishOutputRoot = ".publish-temp/ignored-packages",
  [string[]]$ForcedIgnorePatterns = @(
    '^Microsoft\.TestPlatform\.',
    '^Microsoft\.NET\.Test\.Sdk$',
    '^Mono\.TextTemplating$',
    '^Microsoft\.NET\.ILLink\.Tasks$',
    '^Microsoft\.NET\.Sdk\.WebAssembly\.Pack$',
    '^Microsoft\.Build(\.|$)',
    '^Microsoft\.CodeAnalysis(\.|$)'
  ),
  [switch]$SkipCleanPublish,
  [switch]$DryRun,
  [string]$OutputDynamicFile,
  [switch]$VerboseReport,
  [switch]$Diag,
  [switch]$NoTestDependencies,
  [switch]$SkipDotnetList,
  [string[]]$IncludeProjects,
  [string[]]$ExcludeProjects
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$m) Write-Host "[INFO ] $m" -ForegroundColor Cyan }
function Write-Warn { param([string]$m) Write-Host "[WARN ] $m" -ForegroundColor Yellow }
function Write-Err  { param([string]$m) Write-Host "[ERROR] $m" -ForegroundColor Red }

function Get-JsonObjectProperties {
  param([object]$Obj)
  if ($null -eq $Obj) { return @() }
  if ($Obj -is [hashtable]) { return $Obj.GetEnumerator() }
  return $Obj.PSObject.Properties
}

function Expand-PossiblePattern {
  param(
    [string]$Pattern,
    [System.Collections.Generic.HashSet[string]]$AllPackages
  )
  $results = New-Object System.Collections.Generic.List[string]
  if (-not $AllPackages -or $AllPackages.Count -eq 0) { return $results }

  if ($Pattern -match '^\^.*\$$') {
    foreach ($pkg in $AllPackages) { if ($pkg -match $Pattern) { $results.Add($pkg) } }
    return $results
  }
  if ($Pattern -match '[\*\?]') {
    $escaped = [Regex]::Escape($Pattern) -replace '\\\*','.*' -replace '\\\?','.'
    $regex = '^' + $escaped + '$'
    foreach ($pkg in $AllPackages) { if ($pkg -match $regex) { $results.Add($pkg) } }
    return $results
  }
  if ($AllPackages.Contains($Pattern)) {
    $results.Add($Pattern)
  } else {
    Write-Warn "Exact ignore entry '$Pattern' not found among discovered packages (skipping)."
  }
  return $results
}

function Parse-ExistingIgnore {
  param(
    [string]$Path,
    [System.Collections.Generic.HashSet[string]]$AllPackages
  )
  $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  if (-not (Test-Path $Path)) { return $set }
  try {
    $raw = Get-Content $Path -Raw
    if (-not $raw.Trim()) { return $set }
    $json = $raw | ConvertFrom-Json
  } catch {
    Write-Warn "Failed to parse existing ignore file ($Path): $($_.Exception.Message)"
    return $set
  }
  # Array form
  if ($json -is [System.Collections.IEnumerable] -and -not ($json -is [string])) {
    foreach ($item in $json) {
      if ([string]::IsNullOrWhiteSpace($item)) { continue }
      foreach ($pkg in (Expand-PossiblePattern -Pattern $item -AllPackages $AllPackages)) {
        [void]$set.Add($pkg)
      }
    }
    return $set
  }
  # Object with packages/patterns
  $props = $json.PSObject.Properties.Name
  $packages = @()
  $patterns = @()
  if ($props -contains 'packages') { $packages = @($json.packages) }
  if ($props -contains 'patterns') { $patterns = @($json.patterns) }

  foreach ($p in $packages) {
    if ([string]::IsNullOrWhiteSpace($p)) { continue }
    foreach ($pkg in (Expand-PossiblePattern -Pattern $p -AllPackages $AllPackages)) {
      [void]$set.Add($pkg)
    }
  }
  foreach ($pat in $patterns) {
    if ([string]::IsNullOrWhiteSpace($pat)) { continue }
    $expanded = Expand-PossiblePattern -Pattern $pat -AllPackages $AllPackages
    if ($expanded.Count -eq 0) {
      Write-Warn "Pattern '$pat' from existing ignore file matched no packages."
    } else {
      foreach ($pkg in $expanded) { [void]$set.Add($pkg) }
    }
  }
  return $set
}

# Preconditions
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Write-Err ".NET SDK (dotnet) not found in PATH."
  exit 1
}
if (-not (Test-Path $Solution)) {
  Write-Err "Solution file not found: $Solution"
  exit 1
}

# Autodetect projects if runtime not supplied
$allCsproj = @()
$solutionDir = Split-Path -Parent (Resolve-Path $Solution)
$allCsproj = Get-ChildItem -Path $solutionDir -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName

# Apply include/exclude filters early if given
if ($IncludeProjects) {
  $includes = @()
  foreach ($inc in $IncludeProjects) {
    $regex = $inc -replace '\*','.*'
    $includes += ($allCsproj | Where-Object { $_ -match $regex })
  }
  $allCsproj = $allCsproj | Where-Object { $includes -contains $_ }
}
if ($ExcludeProjects) {
  foreach ($exc in $ExcludeProjects) {
    $regex = $exc -replace '\*','.*'
    $allCsproj = $allCsproj | Where-Object { $_ -notmatch $regex }
  }
}

$testPattern = '\.Tests\.|Tests\.csproj$'

$TestProjects = @()
if (-not $RuntimeProjects -or $RuntimeProjects.Count -eq 0) {
  $RuntimeProjects = $allCsproj | Where-Object { $_ -notmatch $testPattern }
  $TestProjects    = $allCsproj | Where-Object { $_ -match $testPattern }
  Write-Info "Auto-detecting runtime projects (excluding *Tests*)."
} else {
  $RuntimeProjects = $RuntimeProjects | ForEach-Object { (Resolve-Path $_).Path }
  # Also gather tests (for AllPackages) if not explicitly disabled
  $TestProjects = $allCsproj | Where-Object { $_ -match $testPattern }
}

if (-not $RuntimeProjects -or $RuntimeProjects.Count -eq 0) {
  Write-Err "No runtime projects determined. Provide -RuntimeProjects."
  exit 1
}

Write-Info "Runtime projects:`n  - " + ($RuntimeProjects -join "`n  - ")
if (-not $NoTestDependencies -and $TestProjects.Count -gt 0) {
  Write-Info "Test projects (for dependency enumeration only):`n  - " + ($TestProjects -join "`n  - ")
} elseif ($NoTestDependencies) {
  Write-Info "Test project dependencies explicitly excluded (-NoTestDependencies)."
}

# Prepare publish root
if ((Test-Path $PublishOutputRoot) -and -not $SkipCleanPublish) {
  Write-Info "Cleaning publish root: $PublishOutputRoot"
  Remove-Item -Recurse -Force $PublishOutputRoot
}
New-Item -ItemType Directory -Force -Path $PublishOutputRoot | Out-Null

$RuntimePackages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$AllPackages     = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

# Function: gather libraries from project.assets.json (libraries section)
function Add-Packages-From-Assets {
  param([string]$ProjectPath, [switch]$AsRuntime)
  $projDir = Split-Path -Parent $ProjectPath
  $assetsFile = Get-ChildItem -Path (Join-Path $projDir 'obj') -Recurse -Filter project.assets.json |
                  Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $assetsFile) { return }
  try {
    $assetsJson = Get-Content $assetsFile.FullName -Raw | ConvertFrom-Json
    $libs = Get-JsonObjectProperties -Obj $assetsJson.libraries
    foreach ($lib in $libs) {
      $key = $lib.Name
      $val = $lib.Value
      if ($val.type -eq 'package' -and $key -match '^([^/]+)/') {
        $id = $Matches[1]
        [void]$AllPackages.Add($id)
        if ($AsRuntime) { [void]$RuntimePackages.Add($id) }
      }
    }
  } catch {
    Write-Warn "Failed to parse assets for ${ProjectPath}: $($_.Exception.Message)"
  }
}

# Publish & extract runtime
foreach ($proj in $RuntimeProjects) {
  $projName       = [IO.Path]::GetFileNameWithoutExtension($proj)
  $projPublishDir = Join-Path $PublishOutputRoot $projName
  Write-Info "Publishing $proj -> $projPublishDir"
  dotnet publish $proj -c Release -o $projPublishDir | Out-Null

  $depsFile = Get-ChildItem -Path $projPublishDir -Filter *.deps.json -Recurse | Select-Object -First 1
  if ($depsFile) {
    $rel = $depsFile.FullName.Substring($projPublishDir.Length).TrimStart('\','/')
    Write-Info "Found deps file: $rel"
    $depsJson = Get-Content $depsFile.FullName -Raw | ConvertFrom-Json
    $libEntries = Get-JsonObjectProperties -Obj $depsJson.libraries
    foreach ($entry in $libEntries) {
      $k = $entry.Name
      $v = $entry.Value
      if ($v.type -eq 'package' -and $k -match '^([^/]+)/') {
        [void]$RuntimePackages.Add($Matches[1])
        [void]$AllPackages.Add($Matches[1])
      }
    }
    # Still parse assets to ensure AllPackages completeness
    Add-Packages-From-Assets -ProjectPath $proj
    continue
  }

  # Razor Class Library detection
  $projText = Get-Content $proj -Raw
  $isRazorSdk      = $projText -match 'Sdk\s*=\s*"Microsoft\.NET\.Sdk\.Razor"'
  $isBlazorWasmSdk = $projText -match 'Sdk\s*=\s*"Microsoft\.NET\.Sdk\.BlazorWebAssembly"'

  if ($isRazorSdk -and -not $isBlazorWasmSdk) {
    Write-Info "No .deps.json (Razor Class Library) - skipping runtime extraction for $proj."
    # We still pull packages into AllPackages (not runtime) for possible ignore classification
    Add-Packages-From-Assets -ProjectPath $proj
    continue
  }

  Write-Warn "No .deps.json for $proj – fallback to project.assets.json for runtime classification."
  Add-Packages-From-Assets -ProjectPath $proj -AsRuntime
}

# Add test project packages (if enabled)
if (-not $NoTestDependencies -and $TestProjects.Count -gt 0) {
  foreach ($tp in $TestProjects) {
    Add-Packages-From-Assets -ProjectPath $tp
  }
}

# Optional supplementary: dotnet list (can catch edge cases)
if (-not $SkipDotnetList) {
  foreach ($proj in $RuntimeProjects) {
    Write-Info "Supplementary 'dotnet list package' for $proj"
    $jsonRaw = dotnet list $proj package --include-transitive --format json
    try {
      $pkgJson = $jsonRaw | ConvertFrom-Json
      foreach ($projNode in $pkgJson.projects) {
        foreach ($fw in $projNode.frameworks) {
          foreach ($pkg in $fw.packages) {
            if ($pkg.id) { [void]$AllPackages.Add($pkg.id) }
          }
        }
      }
    } catch {
      Write-Warn "Failed to parse 'dotnet list package' JSON for ${proj}: $($_.Exception.Message)"
    }
  }
  if (-not $NoTestDependencies) {
    foreach ($tp in $TestProjects) {
      $jsonRaw = dotnet list $tp package --include-transitive --format json 2>$null
      try {
        if ($jsonRaw) {
          $pkgJson = $jsonRaw | ConvertFrom-Json
          foreach ($projNode in $pkgJson.projects) {
            foreach ($fw in $projNode.frameworks) {
              foreach ($pkg in $fw.packages) {
                if ($pkg.id) { [void]$AllPackages.Add($pkg.id) }
              }
            }
          }
        }
      } catch {
        # Non-fatal
      }
    }
  }
}

Write-Info "Runtime packages discovered: $($RuntimePackages.Count)"
Write-Info "All packages discovered: $($AllPackages.Count)"

# Dynamic set = All - Runtime
$DynamicSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($pkg in $AllPackages) {
  if (-not $RuntimePackages.Contains($pkg)) { [void]$DynamicSet.Add($pkg) }
}

# Apply forced regex patterns
$ForcedMatches = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($pat in $ForcedIgnorePatterns) {
  foreach ($pkg in $AllPackages) {
    if ($pkg -match $pat) { [void]$ForcedMatches.Add($pkg) }
  }
}
if ($ForcedMatches.Count -gt 0) {
  Write-Info "Forced matches: $($ForcedMatches -join ', ')"
  foreach ($fm in $ForcedMatches) { [void]$DynamicSet.Add($fm) }
}

$DynamicList = $DynamicSet | Sort-Object
Write-Info "Dynamic (non-runtime) package count: $($DynamicList.Count)"
foreach ($d in $DynamicList) { Write-Host "  IGNORE: $d" }

if ($OutputDynamicFile) {
  $DynamicList | ConvertTo-Json -Depth 3 | Set-Content $OutputDynamicFile -Encoding UTF8
  Write-Info "Wrote dynamic-only ignore list: $OutputDynamicFile"
}

# Parse existing ignore & merge (need AllPackages for pattern expansion)
$ExistingIgnoreSet = Parse-ExistingIgnore -Path $IgnoredFile -AllPackages $AllPackages
if ($ExistingIgnoreSet.Count -gt 0) {
  Write-Info "Existing ignore entries (expanded): $($ExistingIgnoreSet.Count)"
}

$MergedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($e in $ExistingIgnoreSet) { [void]$MergedSet.Add($e) }
foreach ($d in $DynamicList)      { [void]$MergedSet.Add($d) }
$MergedList = $MergedSet | Sort-Object

if ($VerboseReport) {
  $RuntimeOnly = ($RuntimePackages | Where-Object { -not $MergedSet.Contains($_) }) | Sort-Object
  Write-Host "`n---- SUMMARY ----" -ForegroundColor Cyan
  Write-Host "Runtime packages:            $($RuntimePackages.Count)"
  Write-Host "Ignored packages (final):    $($MergedList.Count)"
  Write-Host "Runtime NOT ignored:         $($RuntimeOnly.Count)"
  if ($Diag) {
    Write-Host "`nSample Runtime (up to 15):" -ForegroundColor Cyan
    $RuntimePackages | Sort-Object | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
    Write-Host "Sample Dynamic (up to 15):" -ForegroundColor Cyan
    $DynamicList | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
  }
}

if ($DryRun) {
  Write-Warn "DryRun specified; NOT writing $IgnoredFile."
  return
}

$MergedList | ConvertTo-Json -Depth 3 | Set-Content $IgnoredFile -Encoding UTF8
Write-Info "Updated ignore file: $IgnoredFile (array of $($MergedList.Count) packages)"