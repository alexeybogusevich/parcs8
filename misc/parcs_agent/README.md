# PARCS Agent

LangChain middleware that connects any agent to a PARCS cluster through its MCP server. It works with
any harness that accepts LangChain middleware (`langchain.agents.create_agent`, deepagents, ...).

```python
from langchain.agents import create_agent
from parcs_agent import create_parcs_middleware

parcs = await create_parcs_middleware(
    "ptc",  # or "tool_calling"
    {"transport": "streamable_http", "url": "http://cluster:8080/"},
)
agent = create_agent(model, middleware=[parcs])
```

`create_parcs_middleware(mode, connection, **options)` connects to the MCP server (any
`langchain-mcp-adapters` connection) and returns the middleware for the chosen mode. It is async
because the tools are listed from the server up front.

## Modes

| `mode` | Middleware | The model... |
|---|---|---|
| `"tool_calling"` | `ParcsToolCallingMiddleware` | calls the PARCS tools itself, one tool call per step |
| `"ptc"` | `ParcsPTCMiddleware` | writes JavaScript for a QuickJS `eval` tool that calls them as `tools.runLayer({...})`; the PARCS tools are not direct tools |

Both add the same PARCS guidance to the system prompt; replace it with `guidance=` (`""` for none).
`options` for `"ptc"` otherwise go to `langchain_quickjs.CodeInterpreterMiddleware` (`timeout`,
`max_ptc_calls`, `mode`, ...). The middleware classes can also be built directly from a list of tools.

Other middleware, such as `ToolRetryMiddleware`, is composed by the caller.

## Development

```bash
uv sync
uv run pytest      # unit tests plus a real in-process MCP server
uv run ruff check . && uv run ruff format .
```

`src/parcs_agent/prompts.py` describes the behaviour of the `Parcs.Agent.Mcp` server. When that server's
tools or envelope change, update the prompts and bump the version in `pyproject.toml`.
