# Go Coding Style

> This file extends [common/coding-style.md](../coding-style.md) with Go specific content.

## Formatting

- **gofmt** and **goimports** are mandatory — no style debates

## Design Principles

- Accept interfaces, return structs
- Keep interfaces small (1-3 methods)

## Error Handling

Always wrap errors with context:

```go
if err != nil {
    return fmt.Errorf("failed to create user: %w", err)
}
```

## Naming

- Package names: short, lowercase, no underscores
- Exported functions: PascalCase with Godoc comments
- Error messages: lowercase, no punctuation

```go
// ProcessData transforms raw input into structured output.
// It returns an error if the input is malformed.
func ProcessData(input []byte) (*Data, error)
```

## Reference

See skill: `golang-patterns` for comprehensive Go idioms and patterns.
