"""MCP client wrapper for the mock tooling server (streamable-HTTP transport).

Connects to an independently running MCP server over HTTP. The server must be
started separately, e.g.:

    python mock_tooling_mcp_server/server.py

The session is kept alive for the lifetime of the FastAPI app / CLI.
"""
import asyncio
import json
from contextlib import AsyncExitStack
from typing import Any, Optional

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

from config import MCP_SERVER_URL


class MCPUnavailableError(RuntimeError):
    """Raised when the MCP tooling server is unreachable."""


class MCPToolingClient:
    def __init__(self, url: str = MCP_SERVER_URL) -> None:
        self._url = url
        self._session: Optional[ClientSession] = None
        self._stack: Optional[AsyncExitStack] = None

    @property
    def is_available(self) -> bool:
        return self._session is not None

    async def start(self) -> None:
        if self._session is not None:
            return

        self._stack = AsyncExitStack()
        try:
            await asyncio.wait_for(self._do_connect(), timeout=3.0)
        except (asyncio.TimeoutError, Exception) as ex:
            try:
                await self._stack.aclose()
            except Exception:
                pass
            self._stack = None
            self._session = None
            print(
                f"[mcp-client] WARNING: could not connect to {self._url}: {ex}. "
                "Tool calls will fail gracefully until the server is reachable. "
                "Start it with: python mock_tooling_mcp_server/server.py",
                flush=True,
            )
            return

        tools = await self._session.list_tools()
        names = [tool.name for tool in tools.tools]
        print(
            f"[mcp-client] connected to {self._url}, tools={names}",
            flush=True,
        )

    async def _do_connect(self) -> None:
        assert self._stack is not None
        transport = await self._stack.enter_async_context(
            streamablehttp_client(self._url)
        )
        read, write, _ = transport
        self._session = await self._stack.enter_async_context(
            ClientSession(read, write)
        )
        await self._session.initialize()

    async def call_tool(self, name: str, args: dict[str, Any]) -> Any:
        if self._session is None:
            await self.start()
        if self._session is None:
            raise MCPUnavailableError(
                f"MCP server at {self._url} is unreachable"
            )
        print(f"[mcp-client] call_tool({name}, {args})", flush=True)
        try:
            result = await asyncio.wait_for(
                self._session.call_tool(name, args),
                timeout=5.0,
            )
        except asyncio.TimeoutError as ex:
            self._session = None
            raise MCPUnavailableError(
                f"MCP tool call timed out after 5s: {name}"
            ) from ex
        except Exception as ex:
            raise MCPUnavailableError(f"MCP tool call failed: {ex}") from ex

        structured = getattr(result, "structuredContent", None)
        if structured is not None:
            if (
                isinstance(structured, dict)
                and len(structured) == 1
                and "result" in structured
            ):
                return structured["result"]
            return structured

        if not result.content:
            return None

        parsed = []
        for block in result.content:
            text = getattr(block, "text", None)
            if text is None:
                continue
            try:
                parsed.append(json.loads(text))
            except json.JSONDecodeError:
                parsed.append(text)

        if not parsed:
            return None
        if len(parsed) == 1:
            return parsed[0]
        return parsed

    async def close(self) -> None:
        if self._stack is None:
            return
        try:
            await self._stack.aclose()
        except Exception as ex:
            print(f"[mcp-client] close error: {ex}", flush=True)
        finally:
            self._stack = None
            self._session = None
