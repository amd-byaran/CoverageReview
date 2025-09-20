#!/usr/bin/env pwsh

# PowerShell Profile Setup for Visual Studio 2026 Insider
# This script sets up the development environment for C++ compilation

Write-Host "Setting up Visual Studio 2026 Insider environment..." -ForegroundColor Green

# Visual Studio 2026 Insider paths
$VS2026_BASE = "C:\Program Files\Microsoft Visual Studio\18\Insiders"
$VS2026_VCVARS = "$VS2026_BASE\VC\Auxiliary\Build\vcvarsall.bat"
$VS2026_TOOLS = "$VS2026_BASE\VC\Tools\MSVC"

# Check if Visual Studio 2026 Insider is installed
if (Test-Path $VS2026_BASE) {
    Write-Host "✓ Found Visual Studio 2026 Insider at: $VS2026_BASE" -ForegroundColor Green
    
    # Find the latest MSVC version
    if (Test-Path $VS2026_TOOLS) {
        $latestMSVC = Get-ChildItem $VS2026_TOOLS | Sort-Object Name -Descending | Select-Object -First 1
        if ($latestMSVC) {
            $MSVC_VERSION = $latestMSVC.Name
            $MSVC_BIN = "$VS2026_TOOLS\$MSVC_VERSION\bin\Hostx64\x64"
            
            Write-Host "✓ Found MSVC version: $MSVC_VERSION" -ForegroundColor Green
            Write-Host "✓ MSVC binaries at: $MSVC_BIN" -ForegroundColor Green
            
            # Add MSVC tools to PATH if not already there
            if ($env:PATH -notlike "*$MSVC_BIN*") {
                $env:PATH = "$MSVC_BIN;$env:PATH"
                Write-Host "✓ Added MSVC tools to PATH" -ForegroundColor Green
            } else {
                Write-Host "✓ MSVC tools already in PATH" -ForegroundColor Yellow
            }
        }
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
                Write-Host "✓ Found Windows SDK version: $SDK_VERSION" -ForegroundColor Green
                
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
                
                Write-Host "✓ Set up INCLUDE and LIB environment variables" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "⚠ Windows SDK not found. Some features may not work." -ForegroundColor Yellow
    }
    
} else {
    Write-Host "✗ Visual Studio 2026 Insider not found at expected location" -ForegroundColor Red
    Write-Host "Expected location: $VS2026_BASE" -ForegroundColor Red
    exit 1
}

# Verify that cl.exe is now available
try {
    $clPath = (Get-Command cl -ErrorAction Stop).Source
    Write-Host "✓ C++ compiler available at: $clPath" -ForegroundColor Green
    
    # Get compiler version
    $version = & cl 2>&1 | Select-String "Version" | Select-Object -First 1
    if ($version) {
        Write-Host "✓ Compiler version: $($version.Line.Trim())" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ C++ compiler (cl.exe) not found in PATH" -ForegroundColor Red
    Write-Host "You may need to run the Visual Studio Developer Command Prompt" -ForegroundColor Yellow
}

# Create helpful aliases
Set-Alias -Name build -Value "./build.ps1"
Set-Alias -Name clean -Value "Remove-Item -Recurse -Force build -ErrorAction SilentlyContinue"

Write-Host ""
Write-Host "=== Development Environment Ready ===" -ForegroundColor Cyan
Write-Host "Available commands:" -ForegroundColor White
Write-Host "  build      - Build the project" -ForegroundColor Gray
Write-Host "  clean      - Clean build artifacts" -ForegroundColor Gray
Write-Host "  cl         - Microsoft C++ Compiler" -ForegroundColor Gray
Write-Host "  link       - Microsoft Linker" -ForegroundColor Gray
Write-Host ""