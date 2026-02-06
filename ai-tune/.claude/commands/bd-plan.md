---
name: bd-plan
description: Create a comprehensive implementation plan and store it as bd issues with proper dependencies
---

You will create a detailed implementation plan using the bd-planner agent, then immediately execute the bd commands to persist the plan.

## Process

1. **Invoke bd-planner agent** to analyze requirements and create plan
2. **Execute bd commands** from the plan to create issues
3. **Verify creation** with `bd list --parent <epic-id>`
4. **Show ready work** with `bd ready`
5. **Output summary** of created issues and next steps

## Output Format

```markdown
# Plan Created: [Feature Name]

## Epic
- ID: bd-xxxx
- Title: [Feature Name]
- Priority: P1

## Tasks Created
1. bd-xxxx.1 - [Task 1 Name] (READY)
2. bd-xxxx.2 - [Task 2 Name] (Blocked by bd-xxxx.1)
3. bd-xxxx.3 - [Task 3 Name] (Blocked by bd-xxxx.2)

## Next Steps
Start with: `bd show bd-xxxx.1`

## To work on this:
1. Run: `bd ready` to see available tasks
2. Pick a task: `bd update <id> --status in_progress`
3. Complete work
4. Close task: `bd close <id> -m "Completion message"`
```

## Example Usage

User: `/bd-plan "Add user authentication with JWT tokens"`

Response will:
1. Create epic for authentication feature
2. Break into tasks (setup, middleware, endpoints, tests)
3. Set up dependency graph
4. Show first ready task to start

## Integration with Other Commands

After planning, use:
- `/bd-work <id>` - Start working on a specific task
- `/bd-tdd <id>` - Implement task with TDD workflow
- `/bd-complete <id>` - Close task and find next work
