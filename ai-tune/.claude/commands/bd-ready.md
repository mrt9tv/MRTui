---
name: bd-ready
description: Show all unblocked bd tasks ready to work on, prioritized by importance
---

You will show all tasks that have no open blockers and can be started immediately.

## Process

1. **Get ready tasks**: `bd ready --json`
2. **Parse and display** with priorities and context
3. **Suggest best task** to start based on:
   - Priority (P0 > P1 > P2 > P3 > P4)
   - Dependencies (prefer tasks that unblock others)
   - Context (related to recent work)

## Output Format

```markdown
# Ready Tasks

## High Priority (P0-P1)
1. **bd-xxxx** - [Task Title]
   - Priority: P0
   - Parent: bd-yyyy (Epic: [Epic Name])
   - Files: path/to/file1.ts, path/to/file2.ts
   - Unblocks: 2 tasks

2. **bd-zzzz** - [Task Title]
   - Priority: P1
   - Parent: bd-yyyy
   - Files: path/to/file3.ts

## Medium Priority (P2-P3)
[Similar format]

## Recommendation
**Start with: bd-xxxx** (P0, unblocks other work)

Use: `/bd-work bd-xxxx` to begin

---
Total ready: X tasks
```

## Integration

- After completion with `/bd-complete`, this automatically shows next work
- Use at session start to resume work
- Use after `bd sync` to see if new tasks are available

## When No Tasks Ready

```markdown
# No Ready Tasks

All tasks are either:
- Completed
- In progress
- Blocked by dependencies

## Current Status
- Open: X tasks
- In Progress: Y tasks
- Blocked: Z tasks

Run `bd blocked` to see what's blocking progress.
```
