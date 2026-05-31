from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
import uvicorn
from pydantic import BaseModel

from chat_service import ChatService

_chat_service = ChatService()


@asynccontextmanager
async def lifespan(app: FastAPI):
    await _chat_service.load()
    yield
    await _chat_service.close()


app = FastAPI(title="Local LLM Chat Server", lifespan=lifespan)


class ChatRequest(BaseModel):
    message: str


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/chat")
async def chat(request: ChatRequest) -> dict:
    try:
        print(f"[chat] request: {request.message!r}")
        reply = await _chat_service.chat(request.message)
        print("[chat] reply generated")
        return {"reply": reply}
    except RuntimeError as ex:
        raise HTTPException(status_code=500, detail=str(ex)) from ex


if __name__ == "__main__":
    from config import CHAT_HOST, CHAT_PORT

    uvicorn.run("main:app", host=CHAT_HOST, port=CHAT_PORT, reload=True)
