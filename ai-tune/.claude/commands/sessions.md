---
name: sessions
description: Manage session memory - view, create, and load session summaries for cross-session continuity
---

You will help manage session memory files that preserve context across conversations.

## Commands

### View Recent Sessions
```bash
# List recent session files (Windows)
Get-ChildItem .claude-sessions\*.md | Sort-Object LastWriteTime -Descending | Select-Object -First 5

# Show latest session
Get-Content .claude-sessions\$(Get-ChildItem .claude-sessions\*.md | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty Name)
```

### Create Session Summary
Use template: `.claude-sessions/template.md`

Create file: `.claude-sessions/YYYY-MM-DD-HH-MM.md`

### Load Previous Session
Simply reference the file path and I'll read and incorporate the context.

## Integration with BD

Session summaries should:
- Reference bd task IDs for all work
- Include `bd ready` output for next session
- Document why tasks are blocked (with bd issue links)
- Track discovered work (new bd issues created)

## When to Create

1. Before context compaction (PreCompact hook)
2. End of work session (Stop hook)
3. After completing major milestone
4. When switching between major tasks
