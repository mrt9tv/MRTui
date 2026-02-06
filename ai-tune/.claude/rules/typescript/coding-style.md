# TypeScript/JavaScript Coding Style

> This file extends [common/coding-style.md](../coding-style.md) with TypeScript/JavaScript specific content.

## Immutability

Use spread operators, never mutate directly:

```typescript
// ✅ CORRECT: Spread operator
const updated = { ...user, name: 'New Name' }
const newArray = [...items, newItem]

// ❌ WRONG: Direct mutation
user.name = 'New Name'
items.push(newItem)
```

## Error Handling

Always use async/await with try-catch:

```typescript
try {
  const result = await fetchData()
  return { success: true, data: result }
} catch (error) {
  console.error('Fetch failed:', error)
  return { success: false, error: 'User-friendly message' }
}
```

## Input Validation

Use Zod for runtime validation:

```typescript
import { z } from 'zod'

const UserSchema = z.object({
  name: z.string().min(1),
  email: z.string().email(),
  age: z.number().int().positive().optional(),
})

type User = z.infer<typeof UserSchema>
```

## Console.log

- **No `console.log`** in production code
- Use proper logging library (winston, pino)
- `console.error` is acceptable for error handlers only

## Reference

See skill: `coding-standards` for comprehensive TypeScript patterns.
