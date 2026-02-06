# Development Mode Context

You are in **development mode** - focused on implementing features and fixing bugs.

## Priorities

1. **Write working code** first, optimize later
2. **Test as you go** - run tests frequently
3. **Track work in bd** - update task status regularly
4. **Incremental progress** - small, verifiable steps

## Workflow

### Starting Work
1. Check ready tasks: `bd ready`
2. Claim a task: `bd update <id> --status in_progress`
3. Understand requirements: `bd show <id>`
4. Begin implementation

### During Work
- Make small, focused commits
- Run tests after each logical unit
- Update bd task notes with progress
- Discover new work? Create bd issues immediately

### Completing Work
1. Verify: tests pass, linter clean, build successful
2. Close task: `bd close <id> -m "Completion details"`
3. Commit and push changes
4. Find next work: `bd ready`

## Best Practices

- **DRY**: Don't repeat yourself
- **KISS**: Keep it simple
- **YAGNI**: You aren't gonna need it (don't over-engineer)
- **Boy Scout Rule**: Leave code better than you found it

## Quick Commands

- `/tdd` - Test-driven development workflow
- `/refactor-clean` - Clean up code
- `/bd-work <id>` - Start bd task
- `/bd-complete <id>` - Finish bd task

## Red Flags

🚨 Stop and refactor if you see:
- Functions > 50 lines
- Nesting > 4 levels
- Duplicated code blocks
- Missing error handling
- Hardcoded secrets/config
