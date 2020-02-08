<#
.SYNOPSIS
Builds the software and examples.
.DESCRIPTION
Builds the software with various settings and options.
.PARAMETER Configuration
Specifies to build in Release configuration. Default is buildign for 'Debug'.
.PARAMETER Example
Run example, implies building the software.
.PARAMETER Test
Run tests, implies building the software.
.PARAMETER Clean
Don't build, but just clean build artifacts.
.PARAMETER Tidy
Don't build, but just clean the working copy from intermediate files.
.PARAMETER Build
Explicitly specify to build the software. Use together with '-Tidy' or '-Clean'.
.PARAMETER Arguments
Additional arguments to pass directly to 'dotnet.exe build' invocations.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
  [string]$Configuration = 'Debug',
  [switch]$Tidy = $false,
  [switch]$Clean = $false,
  [switch]$Test = $false,
  [switch]$Example = $false,
  [switch]$Build = $false,
  [switch]$Loop = $false,
  [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments
)

$ErrorActionPreference = 'Stop';

function Write-Status($text) { Write-Host -ForegroundColor DarkCyan -BackgroundColor White $text }
function Write-Fail($text) { Write-Host -ForegroundColor Red $text }
function Exit-IfCommandError($action) {
  if ($LASTEXITCODE -ne 0) {
    Write-Fail "$action failed with code $LASTEXITCODE."
    Exit $LASTEXITCODE
  }
}

$LocalPackages = $PSScriptRoot + '\pkgs'
$LocalRestorePath = $PSScriptRoot + '\example\Packages'
$Locations = @('src', 'example')

if ($Loop) {
  $Tidy = $true
  $Build = $true
  $Test = $true
  $Example = $true
}

# -----------------------------------------------------------------------------
# Clean / Tidy
# -----------------------------------------------------------------------------

if ($Tidy) {
  $Clean = $true
}

if ($Clean) {
  foreach ($location in $Locations) {
    Write-Status "Cleaning $location"
    Push-Location $location
    try {
      dotnet.exe clean --nologo --configuration $Configuration -v m
      Exit-IfCommandError "Clean"
    }
    finally {
      Pop-Location
    }
  }
}

if ($Tidy) {
  Write-Status "Tidy"
  Write-Host "Cleaning bin/ and obj/ folders"
  foreach ($location in $Locations) {
    Get-ChildItem -Path $location -Include bin, obj -Recurse -Directory | ForEach-Object {
      Remove-Item "$($_.FullName)\*" -Recurse -Force -ErrorAction Continue
    }
  }

  Write-Host "Removing local packages from $LocalPackages"
  if (Test-Path "$LocalPacages") {
    Remove-Item "$LocalPackages\*.nupkg" -Force -ErrorAction Continue
  }
  Write-Host "Removing local restore cache from $LocalRestorePath"
  if (Test-Path "$LocalRestorePath") {
    Remove-Item "$LocalRestorePath\*" -Recurse -Force -ErrorAction Continue
  }
  Write-Host "Removing build logs"
  foreach ($location in $Locations) {
    Remove-Item "$PSScriptRoot\msbuild.$location.binlog" -Force -ErrorAction Continue 2> $null
  }
}

if (($Clean -or $Tidy) -And !$Build) {
  Exit 0
}

# -----------------------------------------------------------------------------
# Build
# -----------------------------------------------------------------------------

foreach ($location in $Locations) {
  Write-Status "Building $location"
  Push-Location $location
  try {
    dotnet.exe restore
    Exit-IfCommandError "Restore"

    dotnet.exe build --nologo --configuration $Configuration `
            /binaryLogger:"$PSScriptRoot\msbuild.$location.binlog;ProjectImports=Embed" `
            $Arguments
    Exit-IfCommandError "Build"
  }
  finally {
    Pop-Location
  }
}

# -----------------------------------------------------------------------------
# Test
# -----------------------------------------------------------------------------

if ($Test) {
  Write-Status "Test"
  Push-Location $PSScriptRoot\src
  try {
    dotnet.exe test
    Exit-IfCommandError "Test"
  } finally {
    Pop-Location
  }
}

# -----------------------------------------------------------------------------
# Example
# -----------------------------------------------------------------------------

if ($Example) {
  Write-Status "Example"
  Push-Location $PSScriptRoot\example\SampleHost
  try {
    dotnet.exe run
    Exit-IfCommandError "Run Example"
  } finally {
    Pop-Location
  }
}
