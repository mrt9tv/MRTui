# Rules

## Structure

Rules are organized into a **common** layer plus **language-specific** directories:

```
rules/
├── common (root-level files)
│   ├── coding-style.md    # Immutability, file organization
│   ├── git-workflow.md    # Commit format, PR process
│   ├── testing.md         # TDD, 80% coverage requirement
│   ├── performance.md     # Model selection, context management
│   ├── patterns.md        # Design patterns, skeleton projects
│   ├── hooks.md           # Hook architecture, TodoWrite
│   ├── agents.md          # When to delegate to subagents
│   └── security.md        # Mandatory security checks
├── typescript/            # TypeScript/JavaScript specific
│   ├── coding-style.md
│   ├── patterns.md
│   ├── security.md
│   ├── hooks.md
│   └── testing.md
├── python/                # Python specific
│   ├── coding-style.md
│   ├── patterns.md
│   ├── security.md
│   └── testing.md
└── golang/                # Go specific
    ├── coding-style.md
    ├── testing.md
    └── hooks.md
```

- **Common** (root-level) files contain universal principles — no language-specific code examples.
- **Language directories** extend the common rules with framework-specific patterns, tools, and code examples. Each file references its common counterpart.

## Installation

### Common Rules Only (Language-Agnostic)

```bash
cp ai-tune/.claude/rules/*.md ~/.claude/rules/
```

### Add Language-Specific Rules

```bash
# TypeScript/JavaScript
cp ai-tune/.claude/rules/typescript/*.md ~/.claude/rules/

# Python
cp ai-tune/.claude/rules/python/*.md ~/.claude/rules/

# Go
cp ai-tune/.claude/rules/golang/*.md ~/.claude/rules/
```

> **Note**: Language-specific files extend (not replace) common rules. Both coexist in `~/.claude/rules/`.

## Adding a New Language

1. Create a new directory under `rules/` (e.g., `rules/rust/`)
2. Add rule files that extend common counterparts:
   ```markdown
   # Rust Coding Style
   
   > This file extends [common/coding-style.md](../coding-style.md) with Rust specific content.
   
   ## Your Rust-specific rules here...
   ```
3. Reference relevant skills in each file
