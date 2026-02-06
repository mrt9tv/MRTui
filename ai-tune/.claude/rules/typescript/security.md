# TypeScript/JavaScript Security

> This file extends [common/security.md](../security.md) with TypeScript/JavaScript specific content.

## Secret Management

```typescript
// ❌ NEVER hardcoded
const API_KEY = 'sk-abc123...'

// ✅ ALWAYS from environment
const API_KEY = process.env.API_KEY
if (!API_KEY) throw new Error('API_KEY required')
```

## Input Sanitization

- Always validate with Zod or similar before processing
- Use parameterized queries for database access
- Sanitize HTML output to prevent XSS

## Agent Support

Use `security-reviewer` skill for comprehensive vulnerability analysis.

## Reference

See skill: `security-review` for detailed security checklists.
