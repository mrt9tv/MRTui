# Python Testing

> This file extends [common/testing.md](../testing.md) with Python specific content.

## Framework

Use **pytest** as the testing framework.

## Coverage

```bash
pytest --cov=src --cov-report=term-missing
```

## Test Organization

Use `pytest.mark` for test categorization:

```python
import pytest

@pytest.mark.unit
def test_calculate_total():
    assert calculate_total([10, 20, 30]) == 60

@pytest.mark.integration
def test_database_connection():
    db = get_database()
    assert db.is_connected()

@pytest.mark.e2e
def test_user_registration_flow():
    ...
```

## Fixtures

```python
@pytest.fixture
def sample_user():
    return User(name="Alice", email="alice@example.com")

def test_user_display(sample_user):
    assert sample_user.name == "Alice"
```

## Reference

See skill: `python-testing` for detailed pytest patterns and fixtures.
