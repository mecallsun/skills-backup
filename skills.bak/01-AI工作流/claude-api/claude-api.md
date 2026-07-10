---
name: claude-api
description: "Use when building, debugging, or optimizing Claude API/Anthropic SDK applications - includes prompt caching, model migration, and SDK integration."
trigger: "code imports anthropic or @anthropic-ai/sdk, or questions about Claude API, Anthropic SDK, prompt caching, cache hit rate, Managed Agents"
---

# Claude API Skill

Build, debug, and optimize applications using the Claude API / Anthropic SDK.

## When to Use

- Building apps with Anthropic SDK
- Questions about Claude API usage
- Implementing prompt caching
- Migrating between Claude models (4.5→4.6→4.7)
- Debugging API integrations
- Optimizing token usage
- Working with Managed Agents
- Questions about tool use, batch processing, files, citations

## Usage

```
/claude-api
```

## Key Features

### Prompt Caching
Always include in SDK apps for cost efficiency:
- Cache system prompts
- Cache long documents
- Monitor cache hit rates

### Model Migration
When upgrading models:
1. Update model ID in code
2. Check for deprecated parameters
3. Test tool use compatibility
4. Verify response formats

### Common Patterns

```python
# Basic API call
from anthropic import Anthropic
client = Anthropic()
response = client.messages.create(
    model="claude-opus-4-7",
    max_tokens=1024,
    messages=[{"role": "user", "content": "Hello"}]
)
```

```python
# With prompt caching
from anthropic import Anthropic, cache_control
client = Anthropic()
response = client.messages.create(
    model="claude-opus-4-7",
    max_tokens=1024,
    messages=[
        {
            "role": "user",
            "content": [
                {"type": "text", "text": "Context...", "cache_control": cache_control("ephemeral")}
            ]
        }
    ]
)
```

## Skip Triggers

Do NOT trigger for:
- OpenAI or other provider SDKs
- Files with `-openai.py` or `-generic.py`
- Provider-neutral code
- General programming questions