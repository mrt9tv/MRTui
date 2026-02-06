---
name: configure-ai-tune
description: Interactive installer and configurator for the ai-tune Claude Code configuration package. Guides through skill selection, rule setup, and verification.
---

# Configure AI-Tune

Interactive setup wizard for the ai-tune unified Claude Code configuration package.

## When to Activate

- First-time setup of ai-tune
- Adding new skills or rules to an existing installation
- Troubleshooting configuration issues
- Updating after pulling new changes

## Prerequisites

- Claude Code installed and working
- ai-tune repository cloned or downloaded

## Setup Steps

### Step 1: Verify ai-tune Location

Confirm the ai-tune directory exists and contains the expected structure:

```bash
# Verify structure
ls ai-tune/.claude/agents/
ls ai-tune/.claude/commands/
ls ai-tune/.claude/skills/
ls ai-tune/.claude/rules/
```

### Step 2: Install Core Components

Copy agents, commands, and contexts to your Claude config:

```bash
# Agents (all 15)
cp ai-tune/.claude/agents/*.md ~/.claude/agents/

# Commands (all 61)
cp ai-tune/.claude/commands/*.md ~/.claude/commands/

# Contexts
cp ai-tune/.claude/contexts/*.md ~/.claude/contexts/
```

### Step 3: Select Rules

Install common (language-agnostic) rules, then add language-specific rules:

```bash
# Always install common rules
cp ai-tune/.claude/rules/*.md ~/.claude/rules/

# Then pick your language(s):
# TypeScript/JavaScript
cp -r ai-tune/.claude/rules/typescript/* ~/.claude/rules/

# Python
cp -r ai-tune/.claude/rules/python/* ~/.claude/rules/

# Go
cp -r ai-tune/.claude/rules/golang/* ~/.claude/rules/
```

> **Note**: Language-specific rules extend common rules. If you use multiple languages, the language-specific files will coexist alongside the common ones.

### Step 4: Select Skills

Choose skills relevant to your project. Skills are organized by category:

**General Development:**
| Skill | Description |
|-------|-------------|
| coding-standards | Language-agnostic best practices |
| tdd-workflow | Test-driven development methodology |
| security-review | Security vulnerability checklist |
| verification-loop | Continuous verification patterns |
| eval-harness | Evaluation-driven development |

**Frontend:**
| Skill | Description |
|-------|-------------|
| frontend-patterns | React, Next.js patterns |

**Backend:**
| Skill | Description |
|-------|-------------|
| backend-patterns | API, database, caching patterns |
| postgres-patterns | PostgreSQL-specific patterns |
| clickhouse-io | ClickHouse analytics patterns |

**Python Stack:**
| Skill | Description |
|-------|-------------|
| python-patterns | Idiomatic Python patterns |
| python-testing | pytest, TDD, fixtures |
| django-patterns | Django web framework |
| django-security | Django security best practices |
| django-tdd | Django testing with pytest-django |
| django-verification | Django verification workflows |

**Go Stack:**
| Skill | Description |
|-------|-------------|
| golang-patterns | Idiomatic Go patterns |
| golang-testing | Go testing, TDD, benchmarks |

**Java/Spring Stack:**
| Skill | Description |
|-------|-------------|
| java-coding-standards | Java conventions |
| jpa-patterns | JPA/Hibernate patterns |
| springboot-patterns | Spring Boot patterns |
| springboot-security | Spring Security config |
| springboot-tdd | Spring Boot testing |
| springboot-verification | Spring verification workflows |

**AI/Learning:**
| Skill | Description |
|-------|-------------|
| continuous-learning | Auto-extract patterns from sessions |
| continuous-learning-v2 | Instinct-based learning |
| iterative-retrieval | Progressive context refinement |
| strategic-compact | Manual compaction strategies |

**Workflow:**
| Skill | Description |
|-------|-------------|
| project-guidelines-example | Template for project skills |

**Beads Integration:**
| Skill | Description |
|-------|-------------|
| beads | bd issue tracker integration |

Install selected skills:

```bash
# Copy specific skills
cp -r ai-tune/.claude/skills/tdd-workflow ~/.claude/skills/
cp -r ai-tune/.claude/skills/security-review ~/.claude/skills/
# ... add more as needed

# Or copy all skills
cp -r ai-tune/.claude/skills/* ~/.claude/skills/
```

### Step 5: Configure Hooks

Copy the hooks configuration:

```bash
cp ai-tune/.claude/hooks/hooks.json ~/.claude/hooks/hooks.json
```

Review and customize hooks for your environment. The default hooks include:
- **SessionStart**: Load session context
- **PreCompact**: Save state before compaction
- **PreToolUse**: Validate operations (large writes, dev server blocking, test verification)
- **PostToolUse**: Post-edit checks
- **Stop**: Session summary and learning extraction

### Step 6: Set Up Session Memory (Optional)

Create the session memory directory:

```bash
mkdir -p ~/.claude/sessions
```

### Step 7: Verify Installation

Run verification checks:

```bash
# Count installed components
echo "Agents: $(ls ~/.claude/agents/*.md 2>/dev/null | wc -l)"
echo "Commands: $(ls ~/.claude/commands/*.md 2>/dev/null | wc -l)"
echo "Skills: $(ls -d ~/.claude/skills/*/ 2>/dev/null | wc -l)"
echo "Rules: $(ls ~/.claude/rules/*.md 2>/dev/null | wc -l)"
echo "Hooks: $(test -f ~/.claude/hooks/hooks.json && echo 'installed' || echo 'missing')"
```

## Troubleshooting

### Hooks Not Firing
1. Verify `hooks.json` is valid JSON: `cat ~/.claude/hooks/hooks.json | python -m json.tool`
2. Check hook types are valid: `command`, `prompt`, or `agent`
3. Ensure matchers are valid regex patterns

### Skills Not Found
1. Verify each skill directory has a `SKILL.md` file
2. Check file permissions
3. Restart Claude Code after adding skills

### Commands Not Appearing
1. Commands need YAML frontmatter with `description:` field
2. Verify files end in `.md` extension
3. Check file permissions

## Updating

To update ai-tune after pulling new changes:

```bash
# Re-copy updated components
cp ai-tune/.claude/agents/*.md ~/.claude/agents/
cp ai-tune/.claude/commands/*.md ~/.claude/commands/
cp ai-tune/.claude/rules/*.md ~/.claude/rules/
```

## Uninstalling

To remove ai-tune components:

```bash
# Remove agents added by ai-tune
# (review list first - don't delete your custom agents)
ls ~/.claude/agents/

# Remove the hooks file
rm ~/.claude/hooks/hooks.json

# Remove skills
rm -rf ~/.claude/skills/beads
# ... etc
```
