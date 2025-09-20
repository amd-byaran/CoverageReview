#!/usr/bin/env pwsh

# Script to add Visual Studio 2026 Insider environment setup to PowerShell profile

Write-Host "=== Setting up PowerShell Profile for VS 2026 Insider ===" -ForegroundColor Green
Write-Host ""

$profilePath = $PROFILE
$profileDir = Split-Path $profilePath -Parent

# Create profile directory if it doesn't exist
if (!(Test-Path $profileDir)) {
    New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
    Write-Host "✓ Created profile directory: $profileDir" -ForegroundColor Green
}

# Check if profile exists
$profileExists = Test-Path $profilePath
$backupCreated = $false

if ($profileExists) {
    # Create backup of existing profile
    $backupPath = "$profilePath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item $profilePath $backupPath
    Write-Host "✓ Created backup of existing profile: $backupPath" -ForegroundColor Yellow
    $backupCreated = $true
}

# VS 2026 Insider environment setup code
$vsSetupCode = @'

# === Visual Studio 2026 Insider Environment Setup ===
function Initialize-VS2026Environment {
    param([switch]$Verbose)
    
    if ($Verbose) { Write-Host "Setting up Visual Studio 2026 Insider environment..." -ForegroundColor Green }
    
    # Visual Studio 2026 Insider paths
    $VS2026_BASE = "C:\Program Files\Microsoft Visual Studio\18\Insiders"
    $VS2026_TOOLS = "$VS2026_BASE\VC\Tools\MSVC"
    
    if (Test-Path $VS2026_BASE) {
        # Find the latest MSVC version
        if (Test-Path $VS2026_TOOLS) {
            $latestMSVC = Get-ChildItem $VS2026_TOOLS | Sort-Object Name -Descending | Select-Object -First 1
            if ($latestMSVC) {
                $MSVC_VERSION = $latestMSVC.Name
                $MSVC_BIN = "$VS2026_TOOLS\$MSVC_VERSION\bin\Hostx64\x64"
                
                # Add MSVC tools to PATH if not already there
                if ($env:PATH -notlike "*$MSVC_BIN*") {
                    $env:PATH = "$MSVC_BIN;$env:PATH"
                    if ($Verbose) { Write-Host "✓ Added MSVC tools to PATH" -ForegroundColor Green }
                }
                
                # Set up Windows SDK paths
                $SDK_BASE = "C:\Program Files (x86)\Windows Kits\10"
                if (Test-Path $SDK_BASE) {
                    $SDK_INCLUDE = "$SDK_BASE\Include"
                    $SDK_LIB = "$SDK_BASE\Lib"
                    
                    # Find latest Windows SDK version
                    if (Test-Path $SDK_INCLUDE) {
                        $latestSDK = Get-ChildItem $SDK_INCLUDE | Where-Object { $_.Name -match "^\d+\." } | Sort-Object Name -Descending | Select-Object -First 1
                        if ($latestSDK) {
                            $SDK_VERSION = $latestSDK.Name
                            
                            # Set up include and lib paths
                            $SDK_INCLUDE_PATHS = @(
                                "$SDK_INCLUDE\$SDK_VERSION\um",
                                "$SDK_INCLUDE\$SDK_VERSION\shared", 
                                "$SDK_INCLUDE\$SDK_VERSION\ucrt"
                            )
                            
                            $SDK_LIB_PATHS = @(
                                "$SDK_LIB\$SDK_VERSION\um\x64",
                                "$SDK_LIB\$SDK_VERSION\ucrt\x64"
                            )
                            
                            # Set environment variables
                            $env:INCLUDE = ($SDK_INCLUDE_PATHS + "$VS2026_TOOLS\$MSVC_VERSION\include") -join ";"
                            $env:LIB = ($SDK_LIB_PATHS + "$VS2026_TOOLS\$MSVC_VERSION\lib\x64") -join ";"
                            
                            if ($Verbose) { Write-Host "✓ Set up INCLUDE and LIB environment variables" -ForegroundColor Green }
                        }
                    }
                }
                
                # Create helpful aliases for development
                Set-Alias -Name vs-build -Value "cl" -Scope Global
                Set-Alias -Name vs-link -Value "link" -Scope Global
                
                if ($Verbose) { Write-Host "✓ Visual Studio 2026 Insider environment ready" -ForegroundColor Green }
                return $true
            }
        }
    }
    
    if ($Verbose) { Write-Host "⚠ Visual Studio 2026 Insider not found or not properly configured" -ForegroundColor Yellow }
    return $false
}

# Automatically initialize VS environment when PowerShell starts
Initialize-VS2026Environment

# Add helpful development aliases
function Show-VSEnvironment {
    Write-Host "=== Visual Studio 2026 Insider Environment ===" -ForegroundColor Cyan
    Write-Host "Compiler: " -NoNewline; try { (Get-Command cl).Source } catch { "Not found" }
    Write-Host "Linker: " -NoNewline; try { (Get-Command link).Source } catch { "Not found" }
    Write-Host "INCLUDE: $env:INCLUDE"
    Write-Host "LIB: $env:LIB"
}

Set-Alias -Name vsenv -Value Show-VSEnvironment

# === End Visual Studio 2026 Insider Environment Setup ===

'@

# Read existing profile content if it exists
$existingContent = ""
if ($profileExists) {
    $existingContent = Get-Content $profilePath -Raw
}

# Check if VS setup code is already present
if ($existingContent -like "*Visual Studio 2026 Insider Environment Setup*") {
    Write-Host "✓ Visual Studio environment setup already present in profile" -ForegroundColor Yellow
} else {
    # Append VS setup code to profile
    $newContent = $existingContent + "`n" + $vsSetupCode
    Set-Content -Path $profilePath -Value $newContent -Encoding UTF8
    Write-Host "✓ Added Visual Studio 2026 Insider environment setup to profile" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Profile Setup Complete ===" -ForegroundColor Cyan
Write-Host "Profile location: $profilePath" -ForegroundColor White
if ($backupCreated) {
    Write-Host "Backup created: $backupPath" -ForegroundColor White
}
Write-Host ""
Write-Host "Available commands after restart:" -ForegroundColor White
Write-Host "  vsenv          - Show VS environment status" -ForegroundColor Gray
Write-Host "  vs-build       - Alias for cl.exe compiler" -ForegroundColor Gray
Write-Host "  vs-link        - Alias for link.exe linker" -ForegroundColor Gray
Write-Host ""
Write-Host "To apply changes immediately, run:" -ForegroundColor Yellow
Write-Host "  . `$PROFILE" -ForegroundColor Cyan
Write-Host ""
Write-Host "Or restart PowerShell to have the environment set up automatically." -ForegroundColor White