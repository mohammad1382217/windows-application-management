#requires -Version 5.1
<#
.SYNOPSIS
    Builds a versioned, self-contained MSI release of MilOps.

.DESCRIPTION
    Pipeline:
      1. dotnet publish (self-contained single-file, win-x64).
      2. Ensure WiX build assets (.ico, banner/dialog BMPs) exist.
      3. Build the WiX MSI, injecting the version and publish dir.
      4. (Optional) Code-sign the MSI with signtool when a cert is configured.

.PARAMETER Version
    Semantic version (default 1.0.0). Stamped into Product Version and file name.

.PARAMETER SkipSign
    Force-skip signing even if cert env vars are set.

.PARAMETER Configuration
    Build configuration (default Release).

.EXAMPLE
    .\build-release.ps1 -Version 1.2.0
    .\build-release.ps1 -Version 1.2.0 -SkipSign

  Code signing (optional) - set before running:
    $env:MOPS_SIGN_PFX  = "C:\path\to\cert.pfx"
    $env:MOPS_SIGN_PASS = "the-pfx-password"
    $env:MOPS_SIGN_TSA  = "http://timestamp.digicert.com"   # optional RFC3161 TSA
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [switch]$SkipSign
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "MilOps.sln"
$presentation = Join-Path $root "src\MilOps.Presentation\MilOps.Presentation.csproj"
$installer = Join-Path $root "installer\MilOps.Installer.wixproj"
$publishDir = Join-Path $root "src\MilOps.Presentation\bin\$Configuration\publish"
$artifacts = Join-Path $root "artifacts"

Write-Host "==> MilOps release build: v$Version ($Configuration)" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1. Restore + build solution
# ---------------------------------------------------------------------------
Write-Host "`n[1/5] Restoring & building solution..." -ForegroundColor Yellow
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }
dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }

# ---------------------------------------------------------------------------
# 2. Publish self-contained single-file
# ---------------------------------------------------------------------------
Write-Host "`n[2/5] Publishing self-contained single-file (win-x64)..." -ForegroundColor Yellow
# NOTE: no --no-build here. The solution build above compiles WITHOUT a RID,
# so the win-x64 outputs would be stale; publish must compile for win-x64 itself.
dotnet publish $presentation -c $Configuration `
    -p:PublishProfile=SelfContainedWinX64 -p:RuntimeIdentifier=win-x64
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exe = Join-Path $publishDir "MilOps.exe"
if (-not (Test-Path $exe)) { throw "Expected publish output not found: $exe" }
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "    Published MilOps.exe ($sizeMb MB) -> $publishDir"

# ---------------------------------------------------------------------------
# 3. Ensure WiX assets exist (icon + banner/dialog bitmaps)
# ---------------------------------------------------------------------------
Write-Host "`n[3/5] Ensuring WiX assets..." -ForegroundColor Yellow
& (Join-Path $root "installer\ensure-assets.ps1")
if ($LASTEXITCODE -ne 0) { throw "Asset generation failed." }

# ---------------------------------------------------------------------------
# 4. Build the WiX MSI
# ---------------------------------------------------------------------------
Write-Host "`n[4/5] Building WiX MSI..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$vswhere = "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = if (Test-Path $vswhere) {
    & $vswhere -latest -prerelease -property installationPath 2>$null
} else { $null }
$msbuildExe = if ($msbuild) { Join-Path $msbuild "MSBuild\Current\Bin\MSBuild.exe" } else { $null }

if ($msbuildExe -and (Test-Path $msbuildExe)) {
    Write-Host "    Using MSBuild: $msbuildExe"
    & $msbuildExe $installer /t:Restore /p:MilOpsVersion=$Version 2>$null
    & $msbuildExe $installer /t:Build /p:MilOpsVersion=$Version `
        /p:PublishDir="$publishDir\" /p:Configuration=$Configuration `
        /p:OutputPath="$artifacts\"
} else {
    Write-Host "    vswhere/MSBuild not found; falling back to dotnet build (WiX SDK)." -ForegroundColor DarkYellow
    dotnet build $installer /p:MilOpsVersion=$Version `
        /p:PublishDir="$publishDir\" /p:Configuration=$Configuration `
        /p:OutputPath="$artifacts\"
}
if ($LASTEXITCODE -ne 0) { throw "MSI build failed." }

$msi = Get-ChildItem $artifacts -Filter "MilOpsSetup*.msi" | Select-Object -First 1
if (-not $msi) { throw "MSI was not produced in $artifacts." }

# Rename to a versioned file name.
$versionedMsi = Join-Path $artifacts "MilOps-$Version.msi"
if ($msi.FullName -ne $versionedMsi) {
    Copy-Item $msi.FullName $versionedMsi -Force
}
Write-Host "    MSI -> $versionedMsi" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 5. Optional code signing
# ---------------------------------------------------------------------------
if (-not $SkipSign -and $env:MOPS_SIGN_PFX -and (Test-Path $env:MOPS_SIGN_PFX)) {
    Write-Host "`n[5/5] Code-signing MSI..." -ForegroundColor Yellow
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" `
        -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
    if (-not $signtool) {
        Write-Warning "signtool.exe not found in Windows Kits; skipping signing."
    } else {
        $args = @('sign', '/f', $env:MOPS_SIGN_PFX, '/p', $env:MOPS_SIGN_PASS, '/fd', 'sha256')
        if ($env:MOPS_SIGN_TSA) { $args += @('/tr', $env:MOPS_SIGN_TSA, '/td', 'sha256') }
        $args += $versionedMsi
        & $signtool.FullName @args
        if ($LASTEXITCODE -ne 0) { throw "Code signing failed." }
        Write-Host "    Signed -> $versionedMsi" -ForegroundColor Green
    }
} else {
    Write-Host "`n[5/5] Skipping code signing (no MOPS_SIGN_PFX or -SkipSign)." -ForegroundColor DarkYellow
    Write-Host "    To enable: set MOPS_SIGN_PFX / MOPS_SIGN_PASS / MOPS_SIGN_TSA env vars." -ForegroundColor DarkYellow
}

Write-Host "`n==> Release artifact: $versionedMsi" -ForegroundColor Cyan
Write-Host "    (unsigned - configure signing per README to sign future releases)" -ForegroundColor DarkGray
return $versionedMsi
