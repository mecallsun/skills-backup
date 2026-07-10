---
name: cowork
description: Claude leads planning and execution; Codex and Gemini serve as advisors whose input Claude reads and integrates before making final decisions
level: 5
---

# cowork - Claude-Led Orchestration with Codex & Gemini Advisors

Claude owns the full lifecycle: initial plan, execution, and final output. Codex and Gemini are consulted as specialist advisors at the planning stage. Claude reads their advice, weighs it, and decides — advisors inform but never replace Claude's judgment.

Use this when output quality matters more than speed, and you want multi-perspective validation before Claude commits to an approach.

## Role Definitions

| Role | Model | Responsibility |
|------|-------|---------------|
| **Commander** | Claude | Plans, executes, makes all final decisions |
| **Tech Advisor** | Codex | Architecture, correctness, risks, test strategy, backend trade-offs |
| **Design Advisor** | Gemini | UX clarity, alternatives, edge-case usability, docs, readability |

## When to Use

- Complex tasks where blind spots could hurt quality
- Code review requiring both technical depth and readability judgment
- Feature design needing feasibility + user experience validation
- Any task where "getting it right" outweighs "getting it fast"

## Requirements

- **Codex CLI**: `npm install -g @openai/codex`
- **Gemini CLI**: `npm install -g @google/gemini-cli`
- `omc ask` command available
- If either CLI is unavailable, continue with whichever is available and note the gap

## How It Works

```text
Phase 1 — Claude Plans
  Claude reads the request and drafts:
  - Initial approach and key assumptions
  - Specific questions for each advisor (targeted, not generic)
  - What a good answer looks like

Phase 2 — Advisors Consult
  Claude invokes advisors with precise questions:
  - omc ask codex "<targeted technical question>"
  - omc ask gemini "<targeted design/clarity question>"
  Artifacts written to .omc/artifacts/ask/

Phase 3 — Claude Reads & Integrates
  Claude reads both advisor outputs and asks:
  - What did Codex flag that I hadn't considered?
  - What did Gemini flag that I hadn't considered?
  - Where do they agree? Where do they conflict?
  - Which advice changes my plan, and which do I consciously reject (with reason)?

Phase 4 — Claude Executes
  Claude executes the revised plan with full context.
  Final output is Claude's own — not a merge of three outputs.
```

## Execution Protocol

When invoked, Claude MUST follow this exact workflow:

### Phase 1: Claude Plans

Before consulting anyone, Claude produces:

1. **Task understanding** — restate the request in one sentence to confirm scope
2. **Initial approach** — draft the plan Claude would follow without advisors
3. **Advisor questions** — formulate specific questions, not generic prompts:
   - Codex question: focus on one concrete technical risk, trade-off, or correctness concern
   - Gemini question: focus on one concrete usability, clarity, or alternative-approach concern

> Good advisor questions are narrow and answerable. Bad: "What do you think about this?" Good: "Is this transaction isolation level safe under concurrent writes at 10k RPS?"

### Phase 2: Invoke Advisors

> **Note:** Skill nesting is not supported. Always use the Bash tool directly.

Run both advisors with the targeted questions from Phase 1:

```bash
omc ask codex "<targeted technical question>"
omc ask gemini "<targeted design/clarity question>"
```

### Phase 3: Read & Integrate Advisor Input

Read latest artifacts:

```text
.omc/artifacts/ask/codex-*.md
.omc/artifacts/ask/gemini-*.md
```

For each advisor output, Claude explicitly notes:

- **Adopted:** advice that changes the plan, and why
- **Rejected:** advice Claude disagrees with, and why
- **Noted:** advice that's valid but out of scope for this task

This step is mandatory — Claude must not silently ignore advisor input.

### Phase 4: Execute

Claude executes the final plan with all advisor input integrated.

Deliver the output as Claude's own unified response — not a "here's what Codex said / here's what Gemini said" summary. Advisors are inputs, not co-authors.

If advisors surfaced a significant risk or alternative that changed the direction, call it out in one line: `> Revised approach based on Codex/Gemini input: [what changed and why]`

## Fallbacks

If one advisor is unavailable:
- Continue with the available advisor + Claude's own judgment for the missing angle
- Note which perspective is absent and what risks that creates

If both advisors are unavailable:
- Fall back to Claude-only execution
- State that cowork external advisors were unavailable and proceed

## Invocation

```bash
/oh-my-claudecode:cowork <task description>
```

Example:

```bash
/oh-my-claudecode:cowork Review this PR — check transaction safety and whether the API surface is intuitive
```
