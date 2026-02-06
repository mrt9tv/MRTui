# Python Coding Style

> This file extends [common/coding-style.md](../coding-style.md) with Python specific content.

## Standards

- Follow **PEP 8** style guide
- Use **type annotations** for all function signatures
- Use **docstrings** for all public functions

## Immutability

Prefer frozen dataclasses and NamedTuple:

```python
from dataclasses import dataclass

@dataclass(frozen=True)
class User:
    name: str
    email: str
    age: int | None = None
```

```python
from typing import NamedTuple

class Point(NamedTuple):
    x: float
    y: float
```

## Formatting

Use these tools (in order of preference):
- **black** — opinionated code formatter
- **isort** — import sorting
- **ruff** — fast linter and formatter (can replace both)

```bash
ruff format .
ruff check --fix .
```

## Reference

See skill: `python-patterns` for comprehensive Python idioms.
