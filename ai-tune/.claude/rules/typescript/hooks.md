# TypeScript/JavaScript Hooks

> This file extends [common/hooks.md](../hooks.md) with TypeScript/JavaScript specific content.

## PostToolUse Hooks

Configure in `~/.claude/settings.json`:

- **Prettier**: Auto-format `.ts`, `.tsx`, `.js`, `.jsx` files after edit
- **TypeScript check**: Run `tsc --noEmit` after editing `.ts` files
- **Console.log warning**: Flag `console.log` additions in production code

## Stop Hooks

- **Console.log audit**: Check for any `console.log` statements before session ends
- **Type check**: Run full type check before finalizing changes

## Reference

See skill: `coding-standards` for TypeScript-specific automation patterns.
