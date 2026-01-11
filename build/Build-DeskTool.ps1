<#
.SYNOPSIS
    Build script for DeskTool Windows application.

.DESCRIPTION
    This script builds the DeskTool application, runs tests, and optionally creates an MSIX package.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default is Release.

.PARAMETER Platform
    Target platform: x64, x86, or ARM64. Default is x64.

.PARAMETER Package
    Create MSIX package after building.

.PARAMETER Test
    Run unit tests after building.

.PARAMETER Clean
    Clean build artifacts before building.

.EXAMPLE
    .\Build-DeskTool.ps1 -Configuration Release -Package
    
.EXAMPLE
    .\Build-DeskTool.ps1 -Test -Clean
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64",
    
    [switch]$Package,
    
    [switch]$Test,
    
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$SolutionFile = Join-Path $RootDir "DeskTool.sln"
$OutputDir = Join-Path $RootDir "artifacts"
$TessDataDir = Join-Path $RootDir "tessdata"

# Color output helpers
function Write-Header($message) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $message" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success($message) {
    Write-Host "✓ $message" -ForegroundColor Green
}

function Write-Info($message) {
    Write-Host "→ $message" -ForegroundColor Yellow
}

function Write-Error($message) {
    Write-Host "✗ $message" -ForegroundColor Red
}

# Check prerequisites
function Test-Prerequisites {
    Write-Header "Checking Prerequisites"
    
    # Check dotnet SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Success "dotnet SDK: $dotnetVersion"
    }
    catch {
        Write-Error "dotnet SDK not found. Please install .NET 8 SDK."
        exit 1
    }
    
    # Check Windows SDK
    $windowsSdkPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0"
    if (Test-Path $windowsSdkPath) {
        Write-Success "Windows SDK 10.0.22621.0 found"
    }
    else {
        Write-Info "Windows SDK 10.0.22621.0 not found at expected location"
    }
    
    # Check tessdata
    if (Test-Path $TessDataDir) {
        $trainedData = Get-ChildItem $TessDataDir -Filter "*.traineddata"
        Write-Success "tessdata folder found with $($trainedData.Count) language files"
    }
    else {
        Write-Info "tessdata folder not found. Creating..."
        New-Item -ItemType Directory -Path $TessDataDir -Force | Out-Null
        Write-Info "Download language files from https://github.com/tesseract-ocr/tessdata"
        Write-Info "Required: eng.traineddata, vie.traineddata, deu.traineddata"
    }
}

# Clean build artifacts
function Invoke-Clean {
    Write-Header "Cleaning Build Artifacts"
    
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
        Write-Success "Removed artifacts folder"
    }
    
    Get-ChildItem $RootDir -Include bin,obj -Directory -Recurse | ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force
        Write-Info "Removed: $($_.FullName)"
    }
    
    Write-Success "Clean complete"
}

# Restore NuGet packages
function Invoke-Restore {
    Write-Header "Restoring NuGet Packages"
    
    dotnet restore $SolutionFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Restore failed"
        exit 1
    }
    
    Write-Success "Restore complete"
}

# Build solution
function Invoke-Build {
    Write-Header "Building Solution ($Configuration | $Platform)"
    
    dotnet build $SolutionFile `
        --configuration $Configuration `
        --no-restore `
        -p:Platform=$Platform
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Success "Build complete"
}

# Run tests
function Invoke-Test {
    Write-Header "Running Unit Tests"
    
    $testProject = Join-Path $RootDir "src\DeskTool.Tests\DeskTool.Tests.csproj"
    
    dotnet test $testProject `
        --configuration $Configuration `
        --no-build `
        --logger "console;verbosity=detailed" `
        --results-directory (Join-Path $OutputDir "TestResults")
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit 1
    }
    
    Write-Success "All tests passed"
}

# Publish application
function Invoke-Publish {
    Write-Header "Publishing Application"
    
    $publishDir = Join-Path $OutputDir "publish\$Platform"
    $projectPath = Join-Path $RootDir "src\DeskTool\DeskTool.csproj"
    
    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime "win-$Platform" `
        --self-contained true `
        --output $publishDir `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed"
        exit 1
    }
    
    # Copy tessdata
    $destTessData = Join-Path $publishDir "tessdata"
    if (Test-Path $TessDataDir) {
        Copy-Item $TessDataDir $destTessData -Recurse -Force
        Write-Success "Copied tessdata to publish folder"
    }
    
    Write-Success "Published to: $publishDir"
    return $publishDir
}

# Create MSIX package
function New-MsixPackage {
    param([string]$PublishDir)
    
    Write-Header "Creating MSIX Package"
    
    $packageDir = Join-Path $OutputDir "package"
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    
    # Check for makeappx
    $makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if (-not $makeAppx) {
        $makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"
        if (-not (Test-Path $makeAppx)) {
            Write-Error "makeappx.exe not found. Install Windows SDK."
            return
        }
    }
    else {
        $makeAppx = $makeAppx.Source
    }
    
    # Create AppxManifest
    $manifestPath = Join-Path $PublishDir "AppxManifest.xml"
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity
    Name="DeskTool"
    Publisher="CN=DeskTool Developer"
    Version="1.0.0.0"
    ProcessorArchitecture="$Platform" />

  <Properties>
    <DisplayName>DeskTool</DisplayName>
    <PublisherDisplayName>DeskTool Developer</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us"/>
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="DeskTool.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="DeskTool"
        Description="OCR and PDF Tools"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png"/>
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
    
    $manifest | Out-File -FilePath $manifestPath -Encoding utf8
    
    # Create placeholder assets if not exist
    $assetsDir = Join-Path $PublishDir "Assets"
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
    
    # Create simple placeholder images (1x1 transparent PNG)
    $placeholderPng = [byte[]]@(137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82,0,0,0,1,0,0,0,1,8,6,0,0,0,31,21,196,137,0,0,0,10,73,68,65,84,120,156,99,0,1,0,0,5,0,1,13,10,45,180,0,0,0,0,73,69,78,68,174,66,96,130)
    
    @("StoreLogo.png", "Square150x150Logo.png", "Square44x44Logo.png", "Wide310x150Logo.png") | ForEach-Object {
        $assetPath = Join-Path $assetsDir $_
        if (-not (Test-Path $assetPath)) {
            [System.IO.File]::WriteAllBytes($assetPath, $placeholderPng)
        }
    }
    
    # Create MSIX
    $msixPath = Join-Path $packageDir "DeskTool_$Platform.msix"
    
    & $makeAppx pack /d $PublishDir /p $msixPath /o
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "MSIX creation failed"
        return
    }
    
    Write-Success "MSIX created: $msixPath"
    Write-Info "Note: MSIX is unsigned. Sign with signtool for distribution."
}

# Main execution
try {
    Write-Host "`n🔧 DeskTool Build Script" -ForegroundColor Magenta
    Write-Host "Configuration: $Configuration | Platform: $Platform`n"
    
    Test-Prerequisites
    
    if ($Clean) {
        Invoke-Clean
    }
    
    Invoke-Restore
    Invoke-Build
    
    if ($Test) {
        Invoke-Test
    }
    
    if ($Package) {
        $publishDir = Invoke-Publish
        New-MsixPackage -PublishDir $publishDir
    }
    
    Write-Header "Build Completed Successfully!"
    Write-Host "Output: $OutputDir`n" -ForegroundColor Green
}
catch {
    Write-Error "Build failed: $_"
    exit 1
}
