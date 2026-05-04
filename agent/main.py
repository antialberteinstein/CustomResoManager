from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from chat_service import ChatService

app = FastAPI(title="Local LLM Chat Server")
_chat_service = ChatService()


class ChatRequest(BaseModel):
    message: str


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.on_event("startup")
def load_model() -> None:
    _chat_service.load()


@app.post("/chat")
def chat(request: ChatRequest) -> dict:
    try:
        reply = _chat_service.generate_reply(request.message)
        return {"reply": reply}
    except RuntimeError as ex:
        raise HTTPException(status_code=500, detail=str(ex)) from ex
