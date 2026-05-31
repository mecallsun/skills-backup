---
name: update-config
description: "Use when user asks to configure Claude Code settings, modify settings.json, manage hooks, permissions, environment variables, or change configurations."
---

# Update Configuration Skill

Configure and manage Claude Code settings via `settings.json` and `settings.local.json`.

## When to Use

- Adding/removing permissions for Bash/MCP tools
- Configuring hooks (before/after commands)
- Setting environment variables
- Modifying Claude Code behavior through settings
- Troubleshooting hook configurations

## Usage

```
/update-config
```

Then describe what configuration change you need.

## Common Operations

### Permissions
- Allow/block specific commands: `allow npm`, `block rm`
- Move permissions between global/user settings

### Hooks
- Configure pre/post command hooks
- Set up triggers for specific events

### Environment
- Set environment variables: `set DEBUG=true`
- Configure API keys or secrets

### Settings
- Change theme, model preferences
- Modify tool behaviors
- Configure input/output preferences