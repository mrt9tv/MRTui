# AI-Tune 🎯

**The Ultimate Claude Code Configuration System**

Integrated combination of **Beads** (persistent task tracking) + **Everything Claude Code** (production-tested workflows) into one cohesive AI-assisted development system.

## What is AI-Tune?

AI-Tune is a complete Claude Code configuration package that provides:
- **Persistent Memory**: Beads (bd) issue tracking survives context compaction
- **Smart Workflows**: 15 specialized agents for planning, coding, review
- **30 Skills**: Battle-tested patterns for TDD, security, refactoring
- **61 Commands**: Slash commands for every workflow need
- **Session Continuity**: Memory persistence across conversations
- **Token Optimization**: CLI-first approach (10-50x less context than MCP-heavy setups)

## Quick Start

### Prerequisites

1. **Claude Code** (VS Code with Claude)
2. **Beads CLI**:
   ```bash
   # Windows
   powershell -c "iwr https://raw.githubusercontent.com/steveyegge/beads/main/install.ps1 | iex"
   
   # Mac/Linux
   curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash
   ```

### Installation

#### Method 1: Copy to New Project
```bash
# Copy ai-tune to your project
xcopy /E /I "path\to\ai-tune\.claude" "your-project\.claude"
xcopy /E /I "path\to\ai-tune\.claude-sessions" "your-project\.claude-sessions"

# Initialize beads in your project
cd your-project
bd init

# Set up beads with Claude Code
bd setup claude --project
```

#### Method 2: Global Installation
```bash
# Copy to global Claude directory
xcopy /E /I "path\to\ai-tune\.claude\*" "%USERPROFILE%\.claude\"

# Any project will now have access
# Just initialize bd per-project:
cd your-project
bd init
bd setup claude --project
```

### Verify Installation

In Claude Code:
```
/bd-ready
```

Should show bd integration working. If no tasks exist yet, that's fine!

## Core Workflows

### 1. Planning & Task Creation
```
/bd-plan "Add user authentication"
```

Creates:
- Epic issue with full requirements
- Breakdown into sub-tasks
- Dependency graph
- Shows first ready task

### 2. Working on Tasks
```
/bd-ready          # Show unblocked tasks
/bd-work bd-xxxx   # Claim and start task
[... implement ...]
/bd-complete bd-xxxx  # Finish and find next work
```

### 3. Session Continuity
```
# Before ending session
/sessions          # Create session summary

# Next session
/sessions          # Load previous session
/bd-ready          # Pick up where you left off
```

### 4. Specialized Workflows
```
/tdd               # Test-driven development
/code-review       # Quality & security review
/refactor-clean    # Clean up code
/e2e               # End-to-end testing
```

## Directory Structure

```
your-project/
├── .beads/                      # Beads tracking (gitignored)
│   ├── issues.jsonl             # Git-tracked task database
│   └── beads.db                 # Local SQLite cache
│
├── .claude/                     # Claude Code configs
│   ├── agents/                  # 15 specialized agents
│   │   ├── bd-planner.md        # Plan + create bd issues
│   │   ├── planner.md           # Original ECC planner
│   │   ├── architect.md         # System design
│   │   ├── tdd-guide.md         # Test-driven development
│   │   ├── code-reviewer.md     # Code quality
│   │   ├── security-reviewer.md # Security audit
│   │   └── ... (15 total)
│   │
│   ├── commands/                # 61 slash commands
│   │   ├── bd-plan.md           # Plan with bd
│   │   ├── bd-work.md           # Start bd task
│   │   ├── bd-complete.md       # Finish bd task
│   │   ├── bd-ready.md          # Show ready tasks
│   │   ├── plan-to-beads.md     # Convert plans to bd tasks
│   │   ├── pm2.md               # PM2 service management
│   │   ├── sessions.md          # Session memory
│   │   ├── plan.md              # ECC planner
│   │   ├── tdd.md               # TDD workflow
│   │   └── ... (61 total)
│   │
│   ├── skills/                  # 30 workflow patterns
│   │   ├── beads/               # Beads integration skill
│   │   ├── configure-ai-tune/   # Setup wizard
│   │   ├── tdd-workflow/        # Test-driven dev
│   │   ├── security-review/     # Security checklist
│   │   ├── verification-loop/   # Quality gates
│   │   ├── golang-patterns/     # Language-specific
│   │   ├── python-patterns/
│   │   └── ... (30 total)
│   │
│   ├── hooks/                   # Automation
│   │   └── hooks.json           # SessionStart, PreCompact, Stop, etc.
│   │
│   ├── contexts/                # Workflow modes
│   │   ├── dev.md               # Development focus
│   │   ├── review.md            # Code review focus
│   │   └── research.md          # Exploration focus
│   │
│   └── rules/                   # Project-wide guidelines
│       ├── *.md                 # 8 common rules (language-agnostic)
│       ├── typescript/          # 5 TypeScript-specific rules
│       ├── python/              # 4 Python-specific rules
│       └── golang/              # 3 Go-specific rules
│
└── .claude-sessions/            # Session memory
    ├── template.md              # Session summary template
    └── YYYY-MM-DD-HH-MM.md      # Session history

├── examples/                    # Example configs
│   ├── CLAUDE.md                # Project-level CLAUDE.md template
│   └── user-CLAUDE.md           # User-level CLAUDE.md template

└── mcp-configs/                 # MCP server configs
    └── mcp-servers.json         # GitHub, Supabase, Vercel, etc.
```

## Key Features

### 1. Persistent Task Tracking (Beads)
- **Survives context compaction**: Tasks stored in git-tracked JSONL
- **Dependency graphs**: Track what blocks what
- **Hierarchical**: Epics → Tasks → Sub-tasks
- **Zero conflict**: Hash-based IDs prevent merge collisions
- **Cross-session**: Pick up work weeks later

### 2. Integrated Planner
`/bd-plan` creates both:
- Human-readable implementation plan
- BD issues with full dependency graph
- Ready tasks to start immediately

### 3. Session Memory
- Automatic prompts via hooks (PreCompact, Stop)
- Template-based session summaries
- Evidence-based progress tracking
- Cross-session context recovery

### 4. Smart Hooks
- **SessionStart**: Load bd context + previous session
- **PreCompact**: Save state before context loss
- **Stop**: Create session summary checklist
- **PreToolUse**: Reminders (tmux for dev servers, etc.)
- **PostToolUse**: Suggestions after bd operations

### 5. Token Optimization
- CLI-first approach (vs MCP-heavy)
- `bd prime`: ~1-2k tokens (vs 10-50k for MCP tools)
- On-demand skill loading
- Context-aware mode switching

### 6. Specialized Agents
- **bd-planner**: Plan + create bd issues
- **planner**: Pure planning (no bd)
- **architect**: System design
- **tdd-guide**: Test-driven development
- **code-reviewer**: Quality & security
- **refactor-cleaner**: Code cleanup
- And 10+ more...

## Workflow Modes

Switch contexts for different work:

```bash
# Development mode (default)
# Focuses on implementation, frequent testing

# Code review mode
# Use: Load .claude/contexts/review.md
# Focuses on quality, security, maintainability

# Research mode
# Use: Load .claude/contexts/research.md
# Focuses on exploration, documentation, decisions
```

## Common Patterns

### Starting a New Feature
```
1. /bd-plan "Feature: User profiles"
2. Review generated plan and bd issues
3. /bd-ready (shows first unblocked task)
4. /bd-work bd-xxxx.1
5. Implement the task
6. /bd-complete bd-xxxx.1
7. Repeat steps 3-6
```

### Resuming After Break
```
1. /sessions (shows recent sessions)
2. Reference latest session file
3. /bd-ready (shows current ready tasks)
4. /bd-work <id>
```

### Before Context Compaction
```
1. /sessions (create session summary)
2. Update bd task notes: bd update <id> --notes "Progress..."
3. Compact can now happen safely
```

### End of Day
```
1. Close completed tasks: bd close <id>
2. Update in-progress: bd update <id> --notes "State..."
3. Create session summary: /sessions
4. Sync: bd sync && git push
```

## Integration Examples

### With TDD Workflow
```
/bd-work bd-xxxx   # Claim task
/tdd               # Start TDD workflow
[... implement with tests ...]
/bd-complete bd-xxxx
```

### With Code Review
```
# After PR created
/code-review
[... review finds issues ...]
bd create "Bug: Issue found" --discovered-from bd-xxxx
bd dep add bd-yyyy bd-xxxx --type discovered-from
```

### With Refactoring
```
/bd-plan "Refactor: Extract auth service"
/bd-work bd-xxxx
/refactor-clean
/bd-complete bd-xxxx
```

## Customization

### Add Your Own Agents
Create `.claude/agents/your-agent.md`:
```markdown
---
name: your-agent
description: What this agent does
tools: ["Read", "Grep", "Bash"]
model: opus
---

Agent instructions here...
```

### Add Project-Specific Skills
Create `.claude/skills/your-skill/README.md`:
```markdown
# Your Skill

Skill content that teaches Claude patterns specific to your project.
```

### Add Custom Commands
Create `.claude/commands/your-command.md`:
```markdown
---
name: your-command
description: What this command does
---

Command workflow here...
```

## Troubleshooting

### BD not found
```bash
# Install beads
powershell -c "iwr https://raw.githubusercontent.com/steveyegge/beads/main/install.ps1 | iex"

# Verify
bd --version
```

### Hooks not working
```bash
# Check hooks file exists
cat .claude/hooks/hooks.json

# Reinstall hooks (if using project-local)
bd setup claude --project
```

### Commands not showing
Ensure files are in:
- `.claude/commands/` for project-local
- `~/.claude/commands/` for global

## What's Included

### From Beads
- Task tracking with bd CLI
- Claude Code plugin structure
- Beads skill with full documentation
- Git-based sync workflow

### From Everything Claude Code
- 15 production agents (including Go/Python reviewers)
- 30 battle-tested skills (29 ECC + configure-ai-tune)
- 61 workflow commands (including pm2, plan-to-beads)
- Hook system with automations
- Language-specific rules (TypeScript, Python, Go)
- Token optimization patterns
- Memory persistence framework
- Example CLAUDE.md configs
- MCP server templates

### New Integrated Features
- **bd-planner**: Planning agent that creates bd issues
- **bd-work/bd-complete**: Workflow commands
- **Unified hooks**: BD + ECC memory hooks
- **Context modes**: Dev/Review/Research contexts
- **Session management**: Cross-session continuity

## Architecture

### Token Budget
- Base Claude Code: ~5-10k tokens
- Beads Context (`bd prime`): ~1-2k tokens
- ECC Skills (on-demand): ~2-5k tokens
- Session Memory: ~3-5k tokens
- **Total**: ~11-22k tokens (vs 50k+ with heavy MCP)

### Data Flow
```
Planning → BD Issues → Ready Tasks → Implementation → Completion
   ↓           ↓            ↓              ↓             ↓
ECC Agent   JSONL       bd ready      ECC Skills    Session Memory
```

### Memory Layers
1. **Persistent** (survives everything): BD JSONL in git
2. **Session** (survives compaction): .claude-sessions/*.md
3. **Contextual** (current conversation): Loaded on-demand

## Best Practices

1. **Always use bd for multi-session work**: Context compaction can't erase it
2. **Create session summaries regularly**: Don't rely on memory alone
3. **Update bd task notes as you go**: Future you will thank you
4. **Use dependency graphs**: Make blocking relationships explicit
5. **Close tasks immediately**: Keep ready list accurate
6. **Sync often**: Push bd changes to git frequently
7. **Load appropriate context mode**: Dev vs Review vs Research

## Support

- **Beads Issues**: https://github.com/steveyegge/beads/issues
- **ECC Issues**: https://github.com/affaan-m/everything-claude-code/issues
- **AI-Tune**: This is a community integration - customize for your needs!

## License

- Beads: Check beads repository
- Everything Claude Code: MIT License
- This integration: Use as you wish

---

**Version**: 1.0.0  
**Last Updated**: 2026-02-05

Happy coding! 🚀
