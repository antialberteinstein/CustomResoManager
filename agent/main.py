from fastapi import FastAPI, HTTPException
import uvicorn
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
        print(f"[chat] request: {request.message!r}")
        reply = _chat_service.generate_reply(request.message)
        print("[chat] reply generated")
        return {"reply": reply}
    except RuntimeError as ex:
        raise HTTPException(status_code=500, detail=str(ex)) from ex


if __name__ == "__main__":
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=True)
