# AI-Tune

The ultimate Claude Code configuration system combining persistent task tracking with battle-tested workflows.

## What's This?

A ready-to-use package that merges:
- **Beads**: Git-backed issue tracking that survives context compaction
- **Everything Claude Code**: Production-tested agents, skills, and workflows

Result: A context-efficient, long-horizon AI coding system with cross-session memory.

## Quick Install

```powershell
# Windows (in your project)
powershell -ExecutionPolicy Bypass -File install.ps1

# Mac/Linux (in your project)
bash install.sh
```

See [QUICKSTART.md](QUICKSTART.md) for complete setup guide.

## Key Features

- **Persistent Memory**: Tasks tracked in git survive everything
- **15+ Agents**: Specialized for planning, coding, review
- **30+ Skills**: Battle-tested patterns (TDD, security, refactoring)
- **Session Continuity**: Resume work weeks later
- **Token Optimized**: ~11-22k tokens vs 50k+ for MCP-heavy setups

## Core Commands

```
/bd-plan "Feature"    - Plan and create bd issues
/bd-ready             - Show unblocked tasks
/bd-work <id>         - Start task
/bd-complete <id>     - Finish and find next
/sessions             - Manage session memory
```

## Documentation

- **[README.md](README.md)** - Full documentation
- **[QUICKSTART.md](QUICKSTART.md)** - 5-minute setup guide
- **[.claude/](claude/)** - All configurations

## Structure

```
ai-tune/
├── .claude/          # All Claude Code configs
│   ├── agents/       # 15+ specialized agents
│   ├── commands/     # Quick workflows
│   ├── skills/       # 30+ patterns
│   ├── hooks/        # Automations
│   └── contexts/     # Mode switching
├── .claude-sessions/ # Session memory
├── install.ps1       # Windows installer
├── install.sh        # Mac/Linux installer
├── README.md         # Full docs
└── QUICKSTART.md     # This file

Your project after install:
├── .beads/           # Task tracking
├── .claude/          # Configs (copied)
└── .claude-sessions/ # Memory (local)
```

## Workflow Example

```
/bd-plan "Add auth"     # Creates epic + tasks with dependencies
/bd-ready               # Shows: bd-xxx.1 (ready)
/bd-work bd-xxx.1       # Claims task, loads context
[implement]
/bd-complete bd-xxx.1   # Closes, shows bd-xxx.2 (now ready)
/bd-work bd-xxx.2       # Continue...
```

## Support

- Beads: https://github.com/steveyegge/beads
- ECC: https://github.com/affaan-m/everything-claude-code
- Issues: Customize for your needs!

---

Ready to level up your AI-assisted development? Run install script and start with `/bd-plan`! 🚀
