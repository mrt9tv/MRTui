# Python Security

> This file extends [common/security.md](../security.md) with Python specific content.

## Secret Management

```python
import os

# ✅ ALWAYS from environment
API_KEY = os.environ["API_KEY"]

# Or use dotenv for development
from dotenv import load_dotenv
load_dotenv()
API_KEY = os.getenv("API_KEY")
```

## Security Scanning

Use **bandit** for static security analysis:

```bash
pip install bandit
bandit -r src/
```

## Common Pitfalls

- Never use `eval()` or `exec()` with user input
- Use `secrets` module for cryptographic randomness, not `random`
- Always use parameterized queries with SQLAlchemy or similar
- Validate file paths to prevent directory traversal

## Reference

See skill: `django-security` for Django-specific security patterns.
