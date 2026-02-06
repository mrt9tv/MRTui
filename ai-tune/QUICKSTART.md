# Quick Start Guide

Get AI-Tune running in under 5 minutes.

## Prerequisites (2 minutes)

### 1. Install Beads

**Windows:**
```powershell
powershell -c "iwr https://raw.githubusercontent.com/steveyegge/beads/main/install.ps1 | iex"
```

**Mac/Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash
```

Verify:
```bash
bd --version
```

### 2. Have Claude Code

You need VS Code with Claude Code extension installed.

## Installation (2 minutes)

### Option A: Project-Local (Recommended)

```powershell
# Windows
cd path\to\your-project
powershell -ExecutionPolicy Bypass -File path\to\ai-tune\install.ps1

# Mac/Linux
cd path/to/your-project
bash path/to/ai-tune/install.sh
```

### Option B: Global

```powershell
# Windows
powershell -ExecutionPolicy Bypass -File path\to\ai-tune\install.ps1 -Global

# Mac/Linux
bash path/to/ai-tune/install.sh --global
```

Then in each project:
```bash
cd your-project
bd init
bd setup claude --project
```

## First Steps (1 minute)

### 1. Verify Installation

Open your project in Claude Code:
```
/bd-ready
```

Should respond with bd integration info. If no tasks yet, that's fine!

### 2. Create Your First Plan

```
/bd-plan "Add user authentication with JWT"
```

This will:
- Analyze the requirement
- Break into tasks
- Create bd issues
- Show dependency graph
- Display first ready task

### 3. Start Working

```
/bd-work bd-xxxx
```

This will:
- Claim the task (set status to in_progress)
- Show task details
- Load relevant context
- Begin implementation

### 4. Complete Task

```
/bd-complete bd-xxxx
```

This will:
- Run verification (tests, linter)
- Close the task
- Show newly unblocked tasks
- Suggest next task

## Common Commands

| Command | What It Does |
|---------|--------------|
| `/bd-ready` | Show all unblocked tasks |
| `/bd-plan "Feature"` | Plan and create bd issues |
| `/bd-work <id>` | Claim and start task |
| `/bd-complete <id>` | Finish task, find next |
| `/sessions` | View/create session summaries |
| `/tdd` | Test-driven development |
| `/code-review` | Quality review |
| `/refactor-clean` | Clean up code |

## Your First Workflow

```
# 1. Plan a feature
/bd-plan "Add user profile page"

# 2. See what's ready
/bd-ready

# 3. Start first task
/bd-work bd-xxxx.1

# [implement the task]

# 4. Complete it
/bd-complete bd-xxxx.1

# 5. Continue with next task
/bd-work bd-xxxx.2

# [and so on...]
```

## Session Continuity

### End of Session
```
/sessions
```
Creates a session summary with:
- What was accomplished
- What worked/failed
- Current blockers
- Next steps

### Next Session
```
/sessions  # Load previous session
/bd-ready  # Pick up where you left off
```

## Troubleshooting

### Command not found
- Ensure files are in `.claude/commands/` (project) or `~/.claude/commands/` (global)
- Restart VS Code

### BD command errors
```bash
# Verify bd is installed
bd --version

# Reinitialize if needed
bd init

# Reset hooks
bd setup claude --project
```

### Hooks not triggering
```bash
# Check hooks file exists
cat .claude/hooks/hooks.json

# Reinstall
bd setup claude --project
```

## What's Next?

### Explore Agents
- `/plan` - Planning (without bd)
- `/tdd` - Test-driven development
- `/code-review` - Code review
- `/security-review` - Security audit
- `/refactor-clean` - Code cleanup

### Context Modes
Load different contexts for different work:
- `.claude/contexts/dev.md` - Development focus
- `.claude/contexts/review.md` - Code review focus
- `.claude/contexts/research.md` - Research focus

### Custom Skills
Add project-specific patterns:
```
.claude/skills/my-project-patterns/
└── README.md
```

### Rules
Add project-wide guidelines:
```
.claude/rules/my-coding-standards.md
```

## Pro Tips

1. **Create bd issues for everything**: They survive context compaction
2. **Update task notes as you go**: Future you will thank you
3. **Use dependency graphs**: Make blocking relationships explicit
4. **Session summaries are gold**: Don't skip them
5. **Close tasks immediately**: Keep ready list accurate
6. **Sync often**: `bd sync` pushes to git

## Need Help?

- Read the full [README.md](README.md)
- Check [beads documentation](https://github.com/steveyegge/beads)
- Review [ECC guides](https://github.com/affaan-m/everything-claude-code)

---

**You're ready to go!** Start with `/bd-plan "Your first feature"` 🚀
