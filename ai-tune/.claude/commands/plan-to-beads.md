---
description: Convert a Claude Code plan file into beads epic + tasks for cross-session tracking.
---

# Convert Plan to Beads Tasks

Convert a Claude Code plan file into beads epic + tasks for cross-session tracking.

$ARGUMENTS

Use the Task tool to delegate this to a subagent:

## Agent Instructions

1. **Find the plan file**
   - If `$ARGUMENTS` specifies a path, use that
   - Otherwise, find the most recent plan in `~/.claude/plans/`
   - If no plans found, ask the user for the path

2. **Parse the plan structure**
   - Title: Extract from `# Plan:` heading
   - Description: Extract from `## Summary` section
   - Tasks: Extract from `### Phase N:` sections
   - Each phase becomes a beads task

3. **Create the epic**
   ```bash
   bd create "[Plan Title]" -t epic -p 1 -d "[summary]" --json
   ```
   Save the epic ID from the JSON output.

4. **Create tasks from phases**
   For each `### Phase N:` section:
   ```bash
   bd create "[Phase Title]" -t task -p 2 -d "[first paragraph]" --json
   ```

5. **Add sequential dependencies**
   Each phase depends on the previous one:
   ```bash
   bd dep add <phase2_id> <phase1_id>
   bd dep add <phase3_id> <phase2_id>
   ```

6. **Link tasks to epic**
   ```bash
   bd dep add <task_id> <epic_id>
   ```

7. **Return a concise summary**
   ```
   Created epic: [title] ([epic_id])
   ├── Phase 1: [title] ([task_id])
   ├── Phase 2: [title] ([task_id]) → depends on Phase 1
   └── Phase 3: [title] ([task_id]) → depends on Phase 2
   ```

## Notes

- The original plan file is preserved unchanged
- Task descriptions use the first paragraph of each phase only
- Sequential phases get automatic dependencies
- Priority defaults to 2 (medium) for tasks, 1 (high) for epic

## Installation

This command is part of the ai-tune package. Copy to `~/.claude/commands/` if installing manually.
