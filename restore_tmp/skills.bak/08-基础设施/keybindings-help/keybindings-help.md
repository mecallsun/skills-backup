---
name: keybindings-help
description: "Use when user wants to customize keyboard shortcuts, rebind keys, add chord bindings, or modify keybindings.json."
---

# Keybindings Help Skill

Customize and manage Claude Code keyboard shortcuts.

## When to Use

- Rebinding existing shortcuts
- Adding new chord bindings (e.g., Ctrl+K Ctrl+C)
- Removing or modifying keybindings
- Configuring submit key or other input bindings
- Troubleshooting keybinding issues

## Usage

```
/keybindings-help
```

## Common Operations

### Rebind a Key
Tell me which key you want to rebind and to what.

Example: "rebind Ctrl+S" - I'll help you change the save shortcut.

### Add Chord Binding
Create multi-key shortcuts like Ctrl+K followed by Ctrl+C.

### View Current Bindings
I can show you the current keybindings.json structure.

### Customize Submit Key
Change which key submits input in the REPL.

## File Location

Keybindings are stored in: `~/.claude/keybindings.json`