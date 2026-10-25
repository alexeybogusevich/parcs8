from __future__ import annotations

import pytest
from langchain.agents import create_agent

from parcs_agent import ParcsPTCMiddleware, ParcsToolCallingMiddleware, create_parcs_middleware

from .conftest import scripted_model


@pytest.fixture
def connection(mcp_url):
    return {"transport": "streamable_http", "url": mcp_url}


async def test_invalid_mode_fails_before_connecting():
    unreachable = {"transport": "streamable_http", "url": "http://127.0.0.1:1/"}

    with pytest.raises(ValueError, match="mode must be one of"):
        await create_parcs_middleware("nope", unreachable)  # type: ignore[arg-type]


async def test_tool_calling_mode_against_mcp_server(connection):
    middleware = await create_parcs_middleware("tool_calling", connection)
    assert isinstance(middleware, ParcsToolCallingMiddleware)
    assert [t.name for t in middleware.tools] == ["get_cluster_info"]

    model = scripted_model({"name": "get_cluster_info", "args": {}})
    result = await create_agent(model, middleware=[middleware]).ainvoke({"messages": [("user", "go")]})

    assert '"maxParallelism": 4' in result["messages"][2].text


async def test_ptc_mode_against_mcp_server(connection):
    middleware = await create_parcs_middleware("ptc", connection, max_ptc_calls=10)
    assert isinstance(middleware, ParcsPTCMiddleware)

    code = "const r = JSON.parse((await tools.getClusterInfo({}))[0].text); r.output.maxParallelism"
    model = scripted_model({"name": "eval", "args": {"code": code}})
    result = await create_agent(model, middleware=[middleware]).ainvoke({"messages": [("user", "go")]})

    assert result["messages"][2].text == "<result>4</result>"
