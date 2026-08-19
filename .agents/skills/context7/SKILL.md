---
name: context7
description: Use when needing up-to-date documentation, version-specific API references, or verified code examples for libraries and frameworks
---

# Context7 (Upstash Context7) Integration

## Overview

Context7 is a real-time documentation retrieval engine by Upstash. It fetches live, official, version-specific documentation and code examples to prevent API hallucinations and outdated code usage in AI interactions.

## When to Use

- When using newly updated or recently changed libraries/frameworks.
- When encountering unknown or deprecated API methods in a library.
- When writing integrations for third-party packages or SDKs and exact, up-to-date code examples are required.

## Ways to Access Context7

### 1. Via CLI (`ctx7`)
You can use the Context7 CLI to query documentation directly using `run_command`:

```bash
# Query documentation or get specific library context
npx -y ctx7 search "library_or_topic"
```

### 2. Via Web Search / Scraped Context
If CLI is not configured or offline, fetch updated documentation from:
- `https://context7.com`
- Search queries targeting official doc sites or Context7 index via `search_web` and `read_url_content`.

### 3. Via MCP Server (if enabled in IDE)
If an MCP server for Context7 (`@upstash/context7-mcp` or `Upstash.context7-mcp` extension) is enabled in your environment, use the corresponding MCP tools to fetch library documentation snippets.

## Best Practices

- **Filter by version:** Specify exact library major/minor versions when querying context.
- **Slim mode:** Ask for concise API method signatures to avoid unnecessary context inflation.
- **Verify against local imports:** Always verify imported package versions in `package.json`, `composer.json`, or requirements files before querying.
