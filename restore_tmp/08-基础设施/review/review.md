---
name: review
description: "Use when user wants to review a Pull Request - analyze changes, provide feedback, check code quality and security."
---

# Review Skill

Review Pull Requests for code quality, correctness, security, and best practices.

## When to Use

- Reviewing pending PRs
- Checking code changes
- Providing feedback on implementations
- Security vulnerability detection
- Performance analysis
- Style and convention compliance

## Usage

```
/review
```

Or specify a PR:
```
/review https://github.com/owner/repo/pull/123
```

## Review Focus Areas

### Code Quality
- Readability and maintainability
- Code structure and organization
- Naming conventions
- Comment quality

### Correctness
- Logic errors
- Edge case handling
- Input validation
- Error handling

### Security
- Vulnerability scanning
- Injection prevention
- Authentication/authorization
- Data protection

### Performance
- Algorithm efficiency
- Resource usage
- Scalability concerns

### Best Practices
- Design patterns
- DRY principles
- Testing coverage
- Documentation

## Output

Provides structured feedback:
- Summary of changes
- Key findings
- Recommendations
- Suggested improvements
- Security concerns (if any)

## Notes

- Can review local changes or fetch from GitHub
- Focuses on substantive feedback
- Highlights critical issues first
- Provides actionable suggestions