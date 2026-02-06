# Session Summary Template

Copy this template when creating session summaries.

## File Name Pattern
`.claude-sessions/YYYY-MM-DD-HH-MM.md`

Example: `.claude-sessions/2026-02-05-14-30.md`

## Template

```markdown
# Session: YYYY-MM-DD HH:MM

## Objective
[What we set out to accomplish this session]

## What Was Accomplished

### Completed Tasks
1. **bd-xxxx** - [Task Title] ✅
   - Changes: `path/to/file1.ts`, `path/to/file2.ts`
   - Evidence: Tests passing, linter clean, feature verified
   - Commits: abc123f, def456g
   
2. **bd-yyyy** - [Task Title] ✅
   - Changes: `path/to/file3.ts`
   - Evidence: Unit tests pass, integration tested

### Partial Progress
1. **bd-zzzz** - [Task Title] ⏳ (60% complete)
   - Completed: Setup, initial implementation
   - Remaining: Error handling, edge cases, tests
   - Current state: Compiles but not tested

## Approaches That Worked

### Approach 1: [Name]
- **What**: [Brief description]
- **Why it worked**: [Explanation]
- **Evidence**: [Test results, performance metrics]
- **When to use**: [Conditions/context]
- **Code reference**: `path/to/file.ts:45-67`

### Approach 2: [Name]
- **What**: [Description]
- **Why it worked**: [Explanation]
- **Pattern**: [Is this reusable?]

## Approaches That Failed

### Attempt 1: [Name]
- **What we tried**: [Description]
- **Why it failed**: [Root cause]
- **Error message**: 
  ```
  [Exact error]
  ```
- **Time spent**: [Duration]
- **Don't try again because**: [Reason]

### Attempt 2: [Name]
- **What we tried**: [Description]
- **Why it failed**: [Reason]
- **Alternative**: [What we did instead]

## Learnings & Patterns Discovered

### Pattern 1: [Name]
- **Description**: [What pattern was discovered]
- **When to use**: [Conditions]
- **Implementation**:
  ```typescript
  // Brief code sample
  ```
- **Save to skill?**: Yes/No - If yes, create in `.claude/skills/`

### Pattern 2: [Name]
- **Description**: [Details]
- **Project-specific?**: Yes/No
- **Add to rules?**: Yes/No - If yes, add to `.claude/rules/`

## Current Blockers

### Blocker 1: [Description]
- **Impact**: [What's blocked]
- **Blocked tasks**: bd-aaaa, bd-bbbb
- **Investigation needed**: [What to research]
- **Possible solutions**: [Ideas]

### Blocker 2: [Description]
- **Impact**: [What's blocked]
- **Waiting on**: [External dependency]

## Next Steps

### Immediate (Start Next Session)
1. [ ] Complete error handling in bd-zzzz
2. [ ] Add tests for bd-zzzz
3. [ ] Review and close bd-zzzz

### Ready Tasks (From `bd ready`)
1. **bd-aaaa** - [Task Title] (P1)
   - Depends on: Nothing (READY)
   - Files: `path/to/file.ts`
   
2. **bd-bbbb** - [Task Title] (P2)
   - Depends on: bd-zzzz completion
   - Files: `path/to/other.ts`

### Research Needed
- [ ] Research alternative to failed Approach 2
- [ ] Investigate performance of implementation X
- [ ] Check if library Y supports feature Z

## BD Status Snapshot

```bash
# Ready tasks (copy/paste output)
$ bd ready
bd-aaaa - Task title 1 (P1)
bd-cccc - Task title 2 (P2)

# In-progress tasks
$ bd list --status in_progress
bd-zzzz - Current task (P2) - 60% complete

# Blocked tasks
$ bd blocked
bd-dddd - Blocked by bd-zzzz
bd-eeee - Blocked by external API availability

# Stats
$ bd stats
Open: 12
In Progress: 1
Completed: 45
```

## Context for Next Session

### Key Files Modified
- `src/auth/jwt.ts` - JWT token generation
- `src/api/login.ts` - Login endpoint
- `tests/auth.test.ts` - Auth tests

### Important State
- Database: Migration 0012 applied
- Dependencies: Added `jsonwebtoken@9.0.0`
- Config: Added `JWT_SECRET` to `.env.example`
- Branch: `feature/auth` (pushed to origin)

### Environment Setup
```bash
# To reproduce environment
npm install
cp .env.example .env
# Add JWT_SECRET to .env
npm run migrate
npm test
```

### Mental Context
[Any additional context, thoughts, or observations that would help resume]

## Verification Commands

```bash
# To verify current state
npm test
npm run lint
npm run build
git status
bd ready
```

---

**Session Duration**: X hours
**Lines Changed**: +XXX -YYY
**Tests**: XX passing, YY added
**Commits**: Z
```

## Usage

1. Copy this template
2. Fill in all sections as you work
3. Update throughout the session (not just at the end)
4. Save before context compaction or session end
5. Reference in next session for continuity
