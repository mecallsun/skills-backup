---
name: security-review
description: "Use when user wants to perform a security review of pending changes on current branch - OWASP top 10, vulnerabilities, secure coding practices."
---

# Security Review Skill

Perform comprehensive security review of pending code changes.

## When to Use

- Before merging security-sensitive changes
- Checking for OWASP Top 10 vulnerabilities
- Auditing authentication/authorization code
- Reviewing data handling
- Checking for injection vulnerabilities
- Secure coding practice compliance

## Usage

```
/security-review
```

## Security Focus Areas

### OWASP Top 10
1. **Injection** - SQL, NoSQL, Command injection
2. **Broken Authentication** - Session management issues
3. **Sensitive Data Exposure** - Encryption, storage
4. **XML External Entities (XXE)** - XML parsing
5. **Broken Access Control** - Authorization bypass
6. **Security Misconfiguration** - Defaults, errors
7. **XSS** - Cross-site scripting
8. **Insecure Deserialization** - Data handling
9. **Using Components with Known Vulnerabilities** - Dependencies
10. **Insufficient Logging** - Audit trails

### Common Vulnerabilities
- Command injection
- Path traversal
- Race conditions
- Credential exposure
- Secrets in code
- Weak cryptography

## Process

1. Scan changed files for security issues
2. Analyze data flow and user inputs
3. Check for vulnerable patterns
4. Review dependency changes
5. Provide findings with severity levels

## Output

Security report with:
- Critical findings (immediate action)
- High findings (soon)
- Medium findings (planned)
- Low findings (consider)
- Recommendations with fixes

## Notes

- Prioritizes by severity
- Provides specific fix suggestions
- References relevant CVEs for dependencies
- Includes secure alternatives