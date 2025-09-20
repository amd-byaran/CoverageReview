# PowerShell script to set up Git CLI environment and create remote repository
# Run this script to configure Git CLI paths and create the CodeCoverageHierarchyParser repository

Write-Host "🔧 Setting up Git CLI Environment" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Common Git CLI installation paths
$gitCliPaths = @(
    "${env:ProgramFiles}\GitHub CLI",
    "${env:ProgramFiles(x86)}\GitHub CLI", 
    "${env:LOCALAPPDATA}\GitHubCLI",
    "${env:USERPROFILE}\scoop\apps\gh\current",
    "${env:USERPROFILE}\.local\bin",
    "${env:ProgramFiles}\Git\cmd",
    "C:\tools\gh",
    "C:\gh"
)

# Function to add path to current session
function Add-ToPath {
    param([string]$NewPath)
    if (Test-Path $NewPath) {
        $currentPath = $env:PATH
        if ($currentPath -notlike "*$NewPath*") {
            $env:PATH = "$NewPath;$currentPath"
            Write-Host "✅ Added to PATH: $NewPath" -ForegroundColor Green
            return $true
        } else {
            Write-Host "ℹ️  Already in PATH: $NewPath" -ForegroundColor Yellow
            return $true
        }
    }
    return $false
}

# Function to add path permanently to user environment
function Add-ToUserPath {
    param([string]$NewPath)
    if (Test-Path $NewPath) {
        try {
            $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
            if ($userPath -notlike "*$NewPath*") {
                $newUserPath = "$userPath;$NewPath"
                [Environment]::SetEnvironmentVariable("PATH", $newUserPath, "User")
                Write-Host "✅ Added to permanent USER PATH: $NewPath" -ForegroundColor Green
            } else {
                Write-Host "ℹ️  Already in permanent PATH: $NewPath" -ForegroundColor Yellow
            }
        } catch {
            Write-Warning "Failed to update permanent PATH: $_"
        }
    }
}

# Search for Git CLI installation
Write-Host "`n🔍 Searching for Git CLI installation..." -ForegroundColor Yellow

$foundGitCli = $false
foreach ($path in $gitCliPaths) {
    $ghExe = Join-Path $path "gh.exe"
    if (Test-Path $ghExe) {
        Write-Host "📍 Found GitHub CLI at: $path" -ForegroundColor Green
        Add-ToPath $path
        Add-ToUserPath $path
        $foundGitCli = $true
        break
    }
}

# Also check if gh is already in PATH
try {
    $ghVersion = & gh --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GitHub CLI is already available in PATH" -ForegroundColor Green
        Write-Host "   Version: $($ghVersion -split "`n" | Select-Object -First 1)" -ForegroundColor Gray
        $foundGitCli = $true
    }
} catch {
    # gh not found in current PATH
}

if (-not $foundGitCli) {
    Write-Host "❌ GitHub CLI not found. Please install it from:" -ForegroundColor Red
    Write-Host "   https://cli.github.com/" -ForegroundColor Cyan
    Write-Host "   Or run: winget install GitHub.cli" -ForegroundColor Cyan
    exit 1
}

# Verify Git is available
Write-Host "`n🔍 Checking Git installation..." -ForegroundColor Yellow
try {
    $gitVersion = & git --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Git is available: $gitVersion" -ForegroundColor Green
    } else {
        throw "Git not found"
    }
} catch {
    Write-Host "❌ Git not found. Please install Git from:" -ForegroundColor Red
    Write-Host "   https://git-scm.com/download/windows" -ForegroundColor Cyan
    exit 1
}

# Configure Git user if not set
Write-Host "`n🔧 Checking Git configuration..." -ForegroundColor Yellow
try {
    $gitUser = & git config --global user.name 2>$null
    $gitEmail = & git config --global user.email 2>$null
    
    if ([string]::IsNullOrEmpty($gitUser)) {
        $userName = Read-Host "Enter your Git username"
        & git config --global user.name "$userName"
        Write-Host "✅ Set Git username: $userName" -ForegroundColor Green
    } else {
        Write-Host "✅ Git username: $gitUser" -ForegroundColor Green
    }
    
    if ([string]::IsNullOrEmpty($gitEmail)) {
        $userEmail = Read-Host "Enter your Git email"
        & git config --global user.email "$userEmail"
        Write-Host "✅ Set Git email: $userEmail" -ForegroundColor Green
    } else {
        Write-Host "✅ Git email: $gitEmail" -ForegroundColor Green
    }
} catch {
    Write-Warning "Failed to configure Git: $_"
}

# Check GitHub CLI authentication
Write-Host "`n🔐 Checking GitHub CLI authentication..." -ForegroundColor Yellow
try {
    $authStatus = & gh auth status 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GitHub CLI is authenticated" -ForegroundColor Green
    } else {
        Write-Host "❌ GitHub CLI not authenticated" -ForegroundColor Red
        Write-Host "   Run: gh auth login" -ForegroundColor Cyan
        
        $autoAuth = Read-Host "Do you want to authenticate now? (y/n)"
        if ($autoAuth -eq 'y' -or $autoAuth -eq 'Y') {
            & gh auth login
        }
    }
} catch {
    Write-Warning "Could not check GitHub CLI authentication: $_"
}

# Function to create remote repository
function New-GitHubRepository {
    param(
        [string]$RepoName = "CodeCoverageHierarchyParser",
        [string]$Description = "Ultra-fast C++ hierarchy parser with .NET assembly for Synopsys coverage analysis"
    )
    
    Write-Host "`n🚀 Creating GitHub repository: $RepoName" -ForegroundColor Cyan
    
    try {
        # Check if repository already exists
        $repoCheck = & gh repo view $RepoName 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "⚠️  Repository '$RepoName' already exists" -ForegroundColor Yellow
            $overwrite = Read-Host "Do you want to add it as remote anyway? (y/n)"
            if ($overwrite -ne 'y' -and $overwrite -ne 'Y') {
                return $false
            }
        } else {
            # Create new repository
            Write-Host "📝 Creating new repository..." -ForegroundColor Yellow
            & gh repo create $RepoName --public --description "$Description" --clone=false
            
            if ($LASTEXITCODE -ne 0) {
                Write-Host "❌ Failed to create repository" -ForegroundColor Red
                return $false
            }
            
            Write-Host "✅ Repository created successfully" -ForegroundColor Green
        }
        
        # Add remote origin
        Write-Host "🔗 Adding remote origin..." -ForegroundColor Yellow
        $username = & gh api user --jq .login
        $repoUrl = "https://github.com/$username/$RepoName.git"
        
        # Remove existing origin if it exists
        & git remote remove origin 2>$null
        
        # Add new origin
        & git remote add origin $repoUrl
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Remote origin added: $repoUrl" -ForegroundColor Green
            
            # Push to remote
            Write-Host "📤 Pushing code to remote repository..." -ForegroundColor Yellow
            & git push -u origin master
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Code pushed successfully!" -ForegroundColor Green
                Write-Host "🌐 Repository URL: https://github.com/$username/$RepoName" -ForegroundColor Cyan
                return $true
            } else {
                Write-Host "❌ Failed to push code" -ForegroundColor Red
                return $false
            }
        } else {
            Write-Host "❌ Failed to add remote origin" -ForegroundColor Red
            return $false
        }
        
    } catch {
        Write-Host "❌ Error creating repository: $_" -ForegroundColor Red
        return $false
    }
}

# Main execution
Write-Host "`n🎯 Environment setup complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# Ask if user wants to create the repository now
$createRepo = Read-Host "`nDo you want to create the GitHub repository now? (y/n)"
if ($createRepo -eq 'y' -or $createRepo -eq 'Y') {
    $success = New-GitHubRepository
    
    if ($success) {
        Write-Host "`n🎉 All done! Your code is now on GitHub!" -ForegroundColor Green
        Write-Host "`n📋 Next steps:" -ForegroundColor Cyan
        Write-Host "   - Visit your repository on GitHub" -ForegroundColor Gray
        Write-Host "   - Add a README badge or description" -ForegroundColor Gray
        Write-Host "   - Set up branch protection rules if needed" -ForegroundColor Gray
        Write-Host "   - Invite collaborators if this is a team project" -ForegroundColor Gray
    }
} else {
    Write-Host "`n📝 To create repository later, run:" -ForegroundColor Cyan
    Write-Host "   gh repo create CodeCoverageHierarchyParser --public" -ForegroundColor Gray
    Write-Host "   git remote add origin https://github.com/USERNAME/CodeCoverageHierarchyParser.git" -ForegroundColor Gray
    Write-Host "   git push -u origin master" -ForegroundColor Gray
}

Write-Host "`n✨ Git CLI environment is ready!" -ForegroundColor Green