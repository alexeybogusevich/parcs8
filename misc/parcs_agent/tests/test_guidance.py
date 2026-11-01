from __future__ import annotations

from langchain_core.messages import SystemMessage

from parcs_agent import ParcsPTCMiddleware, ParcsToolCallingMiddleware
from parcs_agent.middleware._guidance import append_to_system_message


def test_append_to_existing_system_message():
    assert append_to_system_message(SystemMessage("base"), "extra").text == "base\n\nextra"


def test_append_without_system_message():
    assert append_to_system_message(None, "extra").text == "extra"


def test_default_guidance_per_mode(cluster_info_tool):
    tool_calling = ParcsToolCallingMiddleware([cluster_info_tool])._guidance
    ptc = ParcsPTCMiddleware([cluster_info_tool])._guidance

    assert "## PARCS cluster" in tool_calling and "## PARCS cluster" in ptc
    assert "Driving PARCS with tool calls" in tool_calling and "JSON.parse" not in tool_calling
    assert "Driving PARCS from code" in ptc and "JSON.parse" in ptc
    assert "{eval_tool}" not in ptc and "`eval`" in ptc


def test_custom_and_empty_guidance(cluster_info_tool):
    assert ParcsToolCallingMiddleware([cluster_info_tool], guidance="mine")._guidance == "mine"
    assert ParcsPTCMiddleware([cluster_info_tool], guidance="")._guidance == ""
