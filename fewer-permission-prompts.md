---
name: fewer-permission-prompts
description: "Use when user wants to reduce permission prompts by adding allowlists to settings.json - trust common read-only Bash and MCP commands."
---

# Fewer Permission Prompts Skill

Scan conversation history and add commonly-used read-only commands to project allowlist to reduce permission prompts.

## When to Use

- User wants fewer "Allow this command?" prompts
- Frequently running the same read-only commands
- Adding trust for common development tools
- Managing permission configurations

## Usage

```
/fewer-permission-prompts
```

## Process

1. **Scan** - Analyze recent tool calls in conversation:
   - Identify read-only Bash commands (ls, cat, git status, etc.)
   - Identify commonly used MCP tool calls
   - Find patterns in approved commands

2. **Prioritize** - Create allowlist entries for:
   - High-frequency read-only commands
   - Common development tools
   - Non-destructive operations

3. **Apply** - Add to project `settings.json`:
   ```json
   {
     "allowedCommands": {
       "bash": ["ls", "git status", "..."],
       "mcp": ["..."]
     }
   }
   ```

## Notes

- Only adds read-only/safe commands
- Never adds destructive commands (rm -rf, etc.)
- Respects user's existing permissions
- Can be customized per project