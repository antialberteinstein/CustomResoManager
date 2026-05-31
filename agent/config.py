from pathlib import Path

try:
    import yaml
except ImportError:  # pyyaml not installed -> fall back to defaults below
    yaml = None


def _load_yaml_config() -> dict:
    """Load config.yaml next to this file. Missing file / missing pyyaml / bad YAML
    all degrade to an empty dict so the defaults apply."""
    path = Path(__file__).with_name("config.yaml")
    if yaml is None or not path.exists():
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return yaml.safe_load(handle) or {}
    except Exception:
        return {}


_cfg = _load_yaml_config()
_chat = _cfg.get("chat") or {}
_mcp = _cfg.get("mcpServer") or {}


def _mcp_url(host: str, port: int, path: str) -> str:
    if not path.startswith("/"):
        path = "/" + path
    if not path.endswith("/"):
        path = path + "/"
    return f"http://{host}:{port}{path}"


GGUF_URL = "https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF/resolve/main/gemma-4-E2B-it-Q8_0.gguf"
GGUF_FILENAME = "gemma-4-E2B-it-Q8_0.gguf"
TEMPERATURE = 0.3

# Where this agent's chat API listens (the C# WPF app posts here). From config.yaml [chat].
CHAT_HOST = str(_chat.get("host", "127.0.0.1"))
CHAT_PORT = int(_chat.get("port", 8000))

MODEL_CONTEXT_TOKENS = 4096
MAX_TOKENS = 768

DOCS_DIR = "docs"
CHROMA_DIR = "chroma_db"
COLLECTION_NAME = "docs"
EMBEDDING_MODEL = "sentence-transformers/all-MiniLM-L6-v2"
CHUNK_SIZE = 800
CHUNK_OVERLAP = 200
TOP_K = 4

# The C# MCP tool server this agent calls. From config.yaml [mcpServer].
MCP_SERVER_URL = _mcp_url(
    str(_mcp.get("host", "127.0.0.1")),
    int(_mcp.get("port", 7777)),
    str(_mcp.get("path", "/mcp")),
)
