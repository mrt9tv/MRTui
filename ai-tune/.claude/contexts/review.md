# Code Review Mode Context

You are in **code review mode** - focused on quality, security, and maintainability.

## Review Checklist

### Code Quality
- [ ] Functions are single-purpose and < 50 lines
- [ ] Variable/function names are clear and descriptive
- [ ] No code duplication (DRY principle)
- [ ] Proper error handling throughout
- [ ] Edge cases considered and handled
- [ ] Comments explain "why", not "what"

### Testing
- [ ] Unit tests cover new functionality
- [ ] Edge cases are tested
- [ ] Tests are readable and maintainable
- [ ] No flaky tests (consistent pass/fail)
- [ ] Test coverage meets project standards

### Security
- [ ] No hardcoded secrets or credentials
- [ ] Input validation on all user inputs
- [ ] SQL injection prevention (parameterized queries)
- [ ] XSS prevention (proper escaping)
- [ ] Authentication/authorization checks
- [ ] Dependency vulnerabilities checked

### Performance
- [ ] No unnecessary loops or database queries
- [ ] Efficient algorithms (consider Big-O)
- [ ] Proper indexing on database queries
- [ ] Resource cleanup (connections, files, etc.)
- [ ] No memory leaks

### Maintainability
- [ ] Follows project conventions
- [ ] Backward compatible (or migration plan exists)
- [ ] Documentation updated
- [ ] Dependencies justified and minimal
- [ ] Technical debt addressed or documented

## Review Workflow

1. **Understand context**: Read bd issue, PR description
2. **Check tests first**: Do they cover the requirements?
3. **Review implementation**: Follow checklist above
4. **Run locally**: Test the changes yourself
5. **Document findings**: Create bd issues for problems found
6. **Provide feedback**: Constructive, specific, actionable

## Quick Commands

- `/code-review` - Full review workflow
- `/security-review` - Security-focused review
- `/test-coverage` - Check test coverage

## Severity Levels

- **🔴 Critical**: Security vulnerabilities, data loss risks
- **🟠 Major**: Bugs, performance issues, broken functionality
- **🟡 Minor**: Code quality, maintainability concerns
- **🔵 Nit**: Style preferences, minor improvements

## Common Issues

### Code Smells
- God objects (classes that do too much)
- Long parameter lists (> 4 params)
- Feature envy (method uses another class more than its own)
- Primitive obsession (using primitives instead of objects)

### Anti-patterns
- Spaghetti code (no clear structure)
- Lava flow (dead code no one dares remove)
- Copy-paste programming
- Magic numbers/strings

## Feedback Format

```markdown
## File: path/to/file.ts

### 🔴 Critical: SQL Injection Vulnerability (Line 45)
**Issue**: User input directly concatenated into SQL query
**Fix**: Use parameterized queries
**Example**: 
\`\`\`typescript
// Bad
db.query(`SELECT * FROM users WHERE id = ${userId}`)
// Good
db.query('SELECT * FROM users WHERE id = ?', [userId])
\`\`\`

### 🟡 Minor: Function Complexity (Line 78)
**Issue**: Function is 65 lines with 5 levels of nesting
**Fix**: Extract smaller functions for each logical step
```
