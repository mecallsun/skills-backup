---
name: init
description: "Use when user wants to initialize a new CLAUDE.md file with codebase documentation - starting documentation for a new project."
---

# Init Skill

Initialize a new CLAUDE.md file with comprehensive codebase documentation.

## When to Use

- Starting documentation for a new project
- Creating project-specific instructions
- Setting up Claude Code for a new codebase
- First-time project setup

## Usage

```
/init
```

## What It Does

Scans the codebase and creates `CLAUDE.md` with:

### Project Overview
- Project type and purpose
- Key technologies used
- Directory structure

### Architecture
- Main components
- How pieces connect
- Entry points

### Development Patterns
- Coding conventions
- Common commands
- Testing approach

### Important Notes
- Project-specific behaviors
- Gotchas and caveats
- Configuration details

## Process

1. Explore project structure
2. Identify key files and patterns
3. Analyze architecture
4. Generate documentation
5. Create CLAUDE.md

## Output

Creates `CLAUDE.md` in project root with:
- Project overview
- Architecture explanation
- Development guidelines
- Important patterns and notes

## Notes

- Only creates new files, doesn't modify existing code
- Asks for user confirmation before writing
- Can be customized with specific requirements