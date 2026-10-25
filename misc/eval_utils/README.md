# eval-utils

Configuration shared by the PARCS evaluation harnesses (`parcs_agent_cli`, …):

- `EvalSettings` — base settings with `llm`, `mcp` (PARCS MCP server) and `langsmith` sections; subclass it and set `model_config["env_file"]`.
- `create_model(settings.llm)` — chat model for the `openai`, `vertexai`, `google` or `anthropic` provider.
- `ThreadNonceMiddleware` — per-thread system-prompt nonce so runs never share a provider prompt-cache prefix.

Provider packages are optional extras: `eval-utils[vertexai,google,anthropic]`.
