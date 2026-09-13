---
name: loop
description: "Use when user wants to run a task on a recurring interval - monitoring, repeated checks, scheduled tasks, polling for status."
---

# Loop Skill

Run a prompt or command on a recurring interval for monitoring, polling, or repeated tasks.

## When to Use

- Monitoring deployments
- Checking build status
- Polling for external changes
- Running scheduled tasks
- Keeping watch on processes
- Repeated status checks

## Usage

```
/loop <interval> <command>
```

### Examples

```
/loop 5m /check-deploy     # Check deploy every 5 minutes
/loop 10m /build-status     # Check build every 10 minutes
/loop 30s echo "alive"     # Run echo every 30 seconds
```

## Options

- **Interval**: `Xm` (minutes) or `Xs` (seconds)
- **Command**: Any slash command or bash command
- **Recurring**: `true` (default) or `false` for one-shot

## Schedule Types

### Recurring (default)
- Continues until manually stopped
- Auto-expires after 7 days
- Persists across sessions if durable=true

### One-Shot
- Fires once at next interval
- Auto-deletes after execution

## Managing Loops

- `/loop` - List active loops
- Stop loop with task ID

## Notes

- Uses cron expressions internally
- Ignores new prompts while waiting
- Jitter added to avoid exact timing collisions
- 7-day max lifetime for recurring jobs