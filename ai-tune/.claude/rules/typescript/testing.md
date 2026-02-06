# TypeScript/JavaScript Testing

> This file extends [common/testing.md](../testing.md) with TypeScript/JavaScript specific content.

## E2E Testing

Use **Playwright** as the E2E testing framework for critical user flows.

## Unit Testing

Use **Jest** or **Vitest** for unit and integration tests:

```typescript
describe('calculateTotal', () => {
  it('returns correct total for items', () => {
    const items = [{ price: 10 }, { price: 20 }]
    expect(calculateTotal(items)).toBe(30)
  })

  it('handles empty array', () => {
    expect(calculateTotal([])).toBe(0)
  })
})
```

## Coverage

```bash
npm test -- --coverage
# or
vitest run --coverage
```

## Agent Support

- **tdd-guide** - Use PROACTIVELY for new features, enforces write-tests-first
- **e2e-runner** - Playwright E2E testing specialist
