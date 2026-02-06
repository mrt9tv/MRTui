---
name: bd-complete
description: Complete a bd task, run verification, and find next work
---

You will complete a task, verify the work, close the issue, and identify the next task to work on.

## Process

1. **Run verification**:
   - Tests pass
   - Linters pass
   - Code compiles/builds
   - Functionality works

2. **Close task**: `bd close <id> -m "Completion message with details"`

3. **Check for unblocked work**: `bd ready`

4. **Update session memory**: Log what was learned

5. **Identify next task**: Show first ready task

## Verification Checklist

```bash
# Run tests
npm test  # or appropriate test command

# Run linter
npm run lint  # or eslint/pylint

# Build check
npm run build  # or tsc/cargo build

# Git status
git status
```

## Completion Message Format

```bash
bd close <id> -m "$(cat <<'EOF'
Completed: [What was done]

Changes:
- path/to/file1.ts: Added X
- path/to/file2.ts: Modified Y

Tests: All passing
Build: Success
Verified: [How verification was done]
EOF
)"
```

## Output Format

```markdown
# Completed: bd-xxxx

## What Was Done
[Summary of implementation]

## Changes Made
- File 1: Changes
- File 2: Changes

## Verification
✓ Tests passing
✓ Linter clean
✓ Build successful
✓ Functionality verified

## Next Ready Tasks
1. bd-yyyy - [Task Name] (P2)
2. bd-zzzz - [Task Name] (P3)

## Recommendation
Start next with: `bd show bd-yyyy`

---
Task closed. Ready for next work.
```

## After Completion

- **Session continues**: Use `/bd-work <next-id>` for next task
- **Session ending**: Use `/sessions` to save context
- **Break point**: Run `bd sync` to push changes to git
