# AI-Tune Installation Script for Windows
# Run with: powershell -ExecutionPolicy Bypass -File install.ps1

param(
    [switch]$Global,
    [string]$ProjectPath = "."
)

$ErrorActionPreference = "Stop"

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "  AI-Tune Installation" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if beads is installed
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $bdVersion = bd --version 2>&1
    Write-Host "✓ Beads is installed: $bdVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Beads (bd) not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install beads first:" -ForegroundColor Yellow
    Write-Host "  powershell -c `"iwr https://raw.githubusercontent.com/steveyegge/beads/main/install.ps1 | iex`"" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Get script directory (where ai-tune is located)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceClaudeDir = Join-Path $ScriptDir ".claude"
$SourceSessionsDir = Join-Path $ScriptDir ".claude-sessions"

# Verify source directories exist
if (-not (Test-Path $SourceClaudeDir)) {
    Write-Host "✗ Source .claude directory not found at: $SourceClaudeDir" -ForegroundColor Red
    exit 1
}

if ($Global) {
    # Global installation
    Write-Host ""
    Write-Host "Installing AI-Tune globally..." -ForegroundColor Yellow
    
    $GlobalClaudeDir = Join-Path $env:USERPROFILE ".claude"
    
    # Create global directory if it doesn't exist
    if (-not (Test-Path $GlobalClaudeDir)) {
        New-Item -ItemType Directory -Path $GlobalClaudeDir -Force | Out-Null
        Write-Host "✓ Created global .claude directory" -ForegroundColor Green
    }
    
    # Copy agents
    Write-Host "  Copying agents..." -ForegroundColor White
    $GlobalAgentsDir = Join-Path $GlobalClaudeDir "agents"
    if (-not (Test-Path $GlobalAgentsDir)) {
        New-Item -ItemType Directory -Path $GlobalAgentsDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $SourceClaudeDir "agents\*") -Destination $GlobalAgentsDir -Recurse -Force
    Write-Host "  ✓ Agents installed" -ForegroundColor Green
    
    # Copy commands
    Write-Host "  Copying commands..." -ForegroundColor White
    $GlobalCommandsDir = Join-Path $GlobalClaudeDir "commands"
    if (-not (Test-Path $GlobalCommandsDir)) {
        New-Item -ItemType Directory -Path $GlobalCommandsDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $SourceClaudeDir "commands\*") -Destination $GlobalCommandsDir -Recurse -Force
    Write-Host "  ✓ Commands installed" -ForegroundColor Green
    
    # Copy skills
    Write-Host "  Copying skills..." -ForegroundColor White
    $GlobalSkillsDir = Join-Path $GlobalClaudeDir "skills"
    if (-not (Test-Path $GlobalSkillsDir)) {
        New-Item -ItemType Directory -Path $GlobalSkillsDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $SourceClaudeDir "skills\*") -Destination $GlobalSkillsDir -Recurse -Force
    Write-Host "  ✓ Skills installed" -ForegroundColor Green
    
    # Copy contexts
    Write-Host "  Copying contexts..." -ForegroundColor White
    $GlobalContextsDir = Join-Path $GlobalClaudeDir "contexts"
    if (-not (Test-Path $GlobalContextsDir)) {
        New-Item -ItemType Directory -Path $GlobalContextsDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $SourceClaudeDir "contexts\*") -Destination $GlobalContextsDir -Recurse -Force
    Write-Host "  ✓ Contexts installed" -ForegroundColor Green
    
    # Copy rules
    Write-Host "  Copying rules..." -ForegroundColor White
    $GlobalRulesDir = Join-Path $GlobalClaudeDir "rules"
    if (-not (Test-Path $GlobalRulesDir)) {
        New-Item -ItemType Directory -Path $GlobalRulesDir -Force | Out-Null
    }
    if (Test-Path (Join-Path $SourceClaudeDir "rules")) {
        Copy-Item -Path (Join-Path $SourceClaudeDir "rules\*") -Destination $GlobalRulesDir -Recurse -Force
        Write-Host "  ✓ Rules installed" -ForegroundColor Green
    }
    
    # Copy hooks (optional - user may want project-specific)
    Write-Host "  Copying hooks..." -ForegroundColor White
    $GlobalHooksDir = Join-Path $GlobalClaudeDir "hooks"
    if (-not (Test-Path $GlobalHooksDir)) {
        New-Item -ItemType Directory -Path $GlobalHooksDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $SourceClaudeDir "hooks\*") -Destination $GlobalHooksDir -Recurse -Force
    Write-Host "  ✓ Hooks installed" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "✓ Global installation complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. In each project: cd your-project && bd init" -ForegroundColor White
    Write-Host "  2. Set up beads-Claude integration: bd setup claude --project" -ForegroundColor White
    Write-Host "  3. Try it: /bd-ready" -ForegroundColor White
    
} else {
    # Project-local installation
    Write-Host ""
    Write-Host "Installing AI-Tune to project: $ProjectPath" -ForegroundColor Yellow
    
    $ProjectPath = Resolve-Path $ProjectPath
    $ProjectClaudeDir = Join-Path $ProjectPath ".claude"
    $ProjectSessionsDir = Join-Path $ProjectPath ".claude-sessions"
    
    # Create project .claude directory
    if (-not (Test-Path $ProjectClaudeDir)) {
        New-Item -ItemType Directory -Path $ProjectClaudeDir -Force | Out-Null
        Write-Host "✓ Created .claude directory" -ForegroundColor Green
    }
    
    # Copy everything
    Write-Host "  Copying configuration..." -ForegroundColor White
    Copy-Item -Path (Join-Path $SourceClaudeDir "*") -Destination $ProjectClaudeDir -Recurse -Force
    Write-Host "  ✓ .claude directory copied" -ForegroundColor Green
    
    # Create .claude-sessions directory
    if (-not (Test-Path $ProjectSessionsDir)) {
        New-Item -ItemType Directory -Path $ProjectSessionsDir -Force | Out-Null
        Write-Host "  ✓ Created .claude-sessions directory" -ForegroundColor Green
    }
    
    # Copy session template
    if (Test-Path $SourceSessionsDir) {
        Copy-Item -Path (Join-Path $SourceSessionsDir "*") -Destination $ProjectSessionsDir -Force
        Write-Host "  ✓ Session template copied" -ForegroundColor Green
    }
    
    # Initialize beads if not already initialized
    Push-Location $ProjectPath
    try {
        if (-not (Test-Path ".beads")) {
            Write-Host ""
            Write-Host "Initializing beads in project..." -ForegroundColor Yellow
            bd init
            Write-Host "✓ Beads initialized" -ForegroundColor Green
        } else {
            Write-Host "  ✓ Beads already initialized" -ForegroundColor Green
        }
        
        # Set up Claude Code integration
        Write-Host ""
        Write-Host "Setting up beads-Claude integration..." -ForegroundColor Yellow
        bd setup claude --project
        Write-Host "✓ Integration configured" -ForegroundColor Green
        
    } finally {
        Pop-Location
    }
    
    # Add .claude-sessions to .gitignore if git repo exists
    $GitIgnorePath = Join-Path $ProjectPath ".gitignore"
    if (Test-Path (Join-Path $ProjectPath ".git")) {
        if (Test-Path $GitIgnorePath) {
            $GitIgnoreContent = Get-Content $GitIgnorePath -Raw
            if ($GitIgnoreContent -notmatch "\.claude-sessions") {
                Add-Content -Path $GitIgnorePath -Value "`n# AI-Tune session memory (local only)`n.claude-sessions/"
                Write-Host "  ✓ Added .claude-sessions to .gitignore" -ForegroundColor Green
            }
        } else {
            Set-Content -Path $GitIgnorePath -Value "# AI-Tune session memory (local only)`n.claude-sessions/"
            Write-Host "  ✓ Created .gitignore with .claude-sessions" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "✓ Project installation complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Open your project in Claude Code (VS Code)" -ForegroundColor White
    Write-Host "  2. Try: /bd-ready" -ForegroundColor White
    Write-Host "  3. Create your first plan: /bd-plan `"Your feature`"" -ForegroundColor White
    Write-Host ""
    Write-Host "Quick Reference:" -ForegroundColor Yellow
    Write-Host "  /bd-plan       - Plan and create bd issues" -ForegroundColor White
    Write-Host "  /bd-ready      - Show unblocked tasks" -ForegroundColor White
    Write-Host "  /bd-work <id>  - Start working on task" -ForegroundColor White
    Write-Host "  /bd-complete   - Finish task and find next" -ForegroundColor White
    Write-Host "  /sessions      - Manage session memory" -ForegroundColor White
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "  Installation Complete!" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
