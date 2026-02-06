# Example Project CLAUDE.md

This is an example project-level CLAUDE.md file. Place at your project root.

## Project Overview

- **Name**: My Project
- **Type**: Full-stack web application
- **Stack**: Next.js, TypeScript, PostgreSQL, Tailwind CSS

## Key Rules

- No emojis in code, comments, or documentation
- Prefer immutability — never mutate objects or arrays
- Many small files over few large files (200-400 lines, 800 max)
- Always use TypeScript strict mode
- TDD: Write tests before implementation

## Code Organization

```
src/
|-- app/              # Next.js app router
|-- components/       # Reusable UI components
|-- hooks/            # Custom React hooks
|-- lib/              # Utility libraries
|-- types/            # TypeScript definitions
```

## Code Style

- **Immutability**: Always use spread operators, never mutate
- **Error handling**: Try-catch with user-friendly messages
- **Validation**: Zod schemas for all inputs
- **File size**: Max 800 lines, prefer 200-400

## Key Patterns

### API Response Format

```typescript
interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: string
}
```

### Error Handling

```typescript
try {
  const result = await operation()
  return { success: true, data: result }
} catch (error) {
  console.error('Operation failed:', error)
  return { success: false, error: 'User-friendly message' }
}
```

## Environment Variables

```bash
# Required
DATABASE_URL=
API_KEY=

# Optional
DEBUG=false
```

## Available Commands

- `/tdd` - Test-driven development workflow
- `/plan` - Create implementation plan
- `/code-review` - Review code quality
- `/build-fix` - Fix build errors
- `/e2e` - Generate E2E tests

## Testing

- **Unit tests**: Jest/Vitest for individual functions
- **Integration tests**: API endpoint testing
- **E2E tests**: Playwright for critical user flows
- **Coverage**: 80% minimum, 100% for critical business logic

## Security

- No hardcoded secrets (use env vars)
- Input validation on all endpoints
- SQL injection prevention (parameterized queries)
- XSS prevention (sanitize outputs)

## Git Workflow

- Conventional commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`
- Always test locally before committing
- Small, focused commits
