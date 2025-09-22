# PowerShell script to manage the local NuGet feed

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("publish", "list", "install", "remove", "clear", "status")]
    [string]$Action = "status",
    
    [Parameter(Mandatory=$false)]
    [string]$PackagePath,
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = ".",
    
    [Parameter(Mandatory=$false)]
    [string]$Version
)

$LocalFeedPath = "C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\NugetFeed"
$LocalFeedName = "LocalNuGetFeed"

Write-Host "Local NuGet Feed Manager" -ForegroundColor Green
Write-Host "=======================" -ForegroundColor Green
Write-Host "Feed Location: $LocalFeedPath" -ForegroundColor Cyan
Write-Host ""

switch ($Action) {
    "status" {
        Write-Host "📊 Status Information:" -ForegroundColor Yellow
        
        # Check if feed directory exists
        if (Test-Path $LocalFeedPath) {
            Write-Host "✅ Feed directory exists" -ForegroundColor Green
            
            # List packages in feed (hierarchical structure)
            $packages = Get-ChildItem $LocalFeedPath -Recurse -Filter "*.nupkg"
            $symbolsPackages = Get-ChildItem $LocalFeedPath -Recurse -Filter "*.snupkg"
            Write-Host "📦 Main packages in feed: $($packages.Count)" -ForegroundColor Cyan
            Write-Host "🔍 Symbol packages in feed: $($symbolsPackages.Count)" -ForegroundColor Cyan
            
            # Group by package ID for better display
            $packageGroups = $packages | Group-Object { 
                if ($_.Name -match "^(.+?)\.(\d+\.\d+\.\d+)\.nupkg$") { 
                    $matches[1] 
                } else { 
                    "Unknown" 
                } 
            }
            
            foreach ($group in $packageGroups) {
                Write-Host "📦 $($group.Name):" -ForegroundColor Yellow
                foreach ($package in $group.Group) {
                    $size = [math]::Round($package.Length / 1KB, 2)
                    $relativePath = $package.FullName.Replace($LocalFeedPath, "").TrimStart('\')
                    Write-Host "   - $relativePath ($size KB)" -ForegroundColor Gray
                }
            }
            
            if ($symbolsPackages.Count -gt 0) {
                Write-Host "`n🔍 Symbol packages:" -ForegroundColor Yellow
                foreach ($symbolsPkg in $symbolsPackages) {
                    $size = [math]::Round($symbolsPkg.Length / 1KB, 2)
                    $relativePath = $symbolsPkg.FullName.Replace($LocalFeedPath, "").TrimStart('\')
                    Write-Host "   - $relativePath ($size KB) [symbols]" -ForegroundColor DarkGray
                }
            }
        } else {
            Write-Host "❌ Feed directory does not exist" -ForegroundColor Red
        }
        
        # Check if feed is registered
        $sources = dotnet nuget list source 2>&1
        if ($sources -match $LocalFeedName) {
            Write-Host "✅ Feed is registered as NuGet source" -ForegroundColor Green
        } else {
            Write-Host "❌ Feed is not registered as NuGet source" -ForegroundColor Red
        }
    }
    
    "publish" {
        if (-not $PackagePath) {
            # Default to the HvpHtmlParser package
            $PackagePath = "bin\Release\HvpHtmlParser.1.0.3.nupkg"
        }
        
        if (-not (Test-Path $PackagePath)) {
            Write-Error "Package not found: $PackagePath"
            exit 1
        }
        
        # Extract package info for hierarchical structure
        $packageFileName = Split-Path $PackagePath -Leaf
        if ($packageFileName -match "^(.+?)\.(\d+\.\d+\.\d+)\.nupkg$") {
            $packageId = $matches[1].ToLower()
            $version = $matches[2]
            
            # Create hierarchical directory structure
            $hierarchicalPath = Join-Path $LocalFeedPath "$packageId\$version"
            New-Item -ItemType Directory -Path $hierarchicalPath -Force | Out-Null
            
            Write-Host "📤 Publishing to hierarchical structure: $packageId/$version" -ForegroundColor Yellow
            
            # Copy main package
            Copy-Item $PackagePath $hierarchicalPath -Force
            Write-Host "✅ Main package copied to: $hierarchicalPath" -ForegroundColor Green
            
            # Check for and copy corresponding symbols package
            $symbolsPath = $PackagePath -replace '\.nupkg$', '.snupkg'
            if (Test-Path $symbolsPath) {
                Copy-Item $symbolsPath $hierarchicalPath -Force
                Write-Host "✅ Symbols package copied to: $hierarchicalPath" -ForegroundColor Green
            }
            
            # Copy relevant documentation
            $docFiles = @("HVPParser.md", "DOCUMENTATION-PUBLISHING.md", "README.md")
            foreach ($docFile in $docFiles) {
                if (Test-Path $docFile) {
                    Copy-Item $docFile $hierarchicalPath -Force
                    Write-Host "📄 Copied documentation: $docFile" -ForegroundColor Gray
                }
            }
        } else {
            Write-Error "Could not parse package name for hierarchical structure"
            exit 1
        }
        
        Write-Host "✅ Package published successfully to hierarchical feed" -ForegroundColor Green
    }
    
    "list" {
        Write-Host "📋 Packages in hierarchical local feed:" -ForegroundColor Yellow
        $packages = Get-ChildItem $LocalFeedPath -Recurse -Filter "*.nupkg"
        $symbolsPackages = Get-ChildItem $LocalFeedPath -Recurse -Filter "*.snupkg"
        
        # Group by package ID
        $packageGroups = $packages | Group-Object { 
            if ($_.Name -match "^(.+?)\.(\d+\.\d+\.\d+)\.nupkg$") { 
                $matches[1] 
            } else { 
                "Unknown" 
            } 
        }
        
        foreach ($group in $packageGroups) {
            Write-Host "`n📦 $($group.Name):" -ForegroundColor Cyan
            foreach ($package in $group.Group) {
                $size = [math]::Round($package.Length / 1KB, 2)
                $relativePath = $package.FullName.Replace($LocalFeedPath, "").TrimStart('\')
                Write-Host "   $relativePath - $size KB - $($package.LastWriteTime)" -ForegroundColor Gray
            }
        }
        
        if ($symbolsPackages.Count -gt 0) {
            Write-Host "`n🔍 Symbol packages:" -ForegroundColor Yellow
            foreach ($symbolsPkg in $symbolsPackages) {
                $size = [math]::Round($symbolsPkg.Length / 1KB, 2)
                $relativePath = $symbolsPkg.FullName.Replace($LocalFeedPath, "").TrimStart('\')
                Write-Host "   $relativePath - $size KB - $($symbolsPkg.LastWriteTime)" -ForegroundColor DarkGray
            }
        }
    }
    
    "install" {
        if (-not $Version) {
            Write-Host "📥 Installing HvpHtmlParser (latest) to project: $ProjectPath" -ForegroundColor Yellow
            dotnet add $ProjectPath package HvpHtmlParser --source $LocalFeedPath
        } else {
            Write-Host "📥 Installing HvpHtmlParser v$Version to project: $ProjectPath" -ForegroundColor Yellow
            dotnet add $ProjectPath package HvpHtmlParser --version $Version --source $LocalFeedPath
        }
    }
    
    "remove" {
        Write-Host "🗑️ Removing HvpHtmlParser from project: $ProjectPath" -ForegroundColor Yellow
        dotnet remove $ProjectPath package HvpHtmlParser
    }
    
    "clear" {
        Write-Host "🧹 Clearing all packages from local feed..." -ForegroundColor Yellow
        $packages = Get-ChildItem $LocalFeedPath -Filter "*.nupkg"
        $symbolsPackages = Get-ChildItem $LocalFeedPath -Filter "*.snupkg"
        $allPackages = $packages + $symbolsPackages
        
        foreach ($package in $allPackages) {
            Remove-Item $package.FullName -Force
            $type = if ($package.Name.EndsWith('.snupkg')) { '[symbols]' } else { '[main]' }
            Write-Host "   Removed: $($package.Name) $type" -ForegroundColor Gray
        }
        Write-Host "✅ Feed cleared (removed $($allPackages.Count) files)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Usage Examples:" -ForegroundColor White
Write-Host "  .\local-nuget.ps1                                    # Show status" -ForegroundColor Gray
Write-Host "  .\local-nuget.ps1 -Action publish                    # Publish default package" -ForegroundColor Gray
Write-Host "  .\local-nuget.ps1 -Action publish -PackagePath 'path/to/package.nupkg'" -ForegroundColor Gray
Write-Host "  .\local-nuget.ps1 -Action install -ProjectPath 'MyProject'" -ForegroundColor Gray
Write-Host "  .\local-nuget.ps1 -Action list                       # List all packages" -ForegroundColor Gray
Write-Host "  .\local-nuget.ps1 -Action clear                      # Clear all packages" -ForegroundColor Gray