---
name: bd-work
description: Claim a bd task, load its context, and start implementation
---

You will claim a bd task, analyze its requirements, and begin implementation.

## Process

1. **Show task details**: `bd show <id> --json`
2. **Update status**: `bd update <id> --status in_progress`
3. **Load context**: Read relevant files mentioned in task
4. **Check dependencies**: Verify all blocking tasks are complete
5. **Begin implementation**: Follow the task description
6. **Track progress**: Add notes with `bd update <id> --notes "Progress update"`

## Before Starting

```bash
# Verify task is ready
bd ready | grep <id>

# Check full context
bd show <id>

# See dependency graph
bd graph <id>
```

## During Work

```bash
# Add progress notes
bd update <id> --notes "Completed X, working on Y"

# If blocked, update status
bd update <id> --status blocked --notes "Blocked by: reason"

# Discover related work
bd create "Bug: Found issue in X" --discovered-from <id>
```

## Output Format

```markdown
# Working on: bd-xxxx

## Task Details
[Task description from bd]

## Files to Modify
- path/to/file1.ts
- path/to/file2.ts

## Implementation Plan
1. Step 1
2. Step 2
3. Step 3

## Testing Approach
[How to verify]

---
Task claimed and in progress. Proceeding with implementation...
```

## Integration

- Use `/bd-tdd <id>` if task requires test-driven development
- Use `/bd-verify <id>` to run verification checks
- Use `/bd-complete <id>` when done
