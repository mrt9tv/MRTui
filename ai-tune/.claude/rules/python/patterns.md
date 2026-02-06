# Python Patterns

> This file extends [common/patterns.md](../patterns.md) with Python specific content.

## Protocol (Duck Typing)

```python
from typing import Protocol

class Drawable(Protocol):
    def draw(self) -> None: ...

def render(item: Drawable) -> None:
    item.draw()
```

## Dataclasses as DTOs

```python
from dataclasses import dataclass, asdict

@dataclass
class CreateUserRequest:
    name: str
    email: str

    def to_dict(self) -> dict:
        return asdict(self)
```

## Context Managers & Generators

```python
from contextlib import contextmanager

@contextmanager
def database_transaction(db):
    tx = db.begin()
    try:
        yield tx
        tx.commit()
    except Exception:
        tx.rollback()
        raise
```

## Reference

See skill: `python-patterns` for comprehensive Python design patterns.
