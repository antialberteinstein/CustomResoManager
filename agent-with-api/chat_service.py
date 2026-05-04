import os
from typing import Optional

import requests
from dotenv import load_dotenv
from langchain_community.embeddings import HuggingFaceEmbeddings
import chromadb

from config import (
    API_KEY,
    COLLECTION_NAME,
    CHROMA_DIR,
    EMBEDDING_MODEL,
    MODEL_NAME,
    TIMEOUT_SECONDS,
    TOP_K,
)


class ChatService:
    def __init__(self) -> None:
        self.api_key = API_KEY
        self.model_name = MODEL_NAME
        self.timeout_seconds = TIMEOUT_SECONDS
        self._session: Optional[requests.Session] = None
        self._embeddings: Optional[HuggingFaceEmbeddings] = None
        self._client: Optional[chromadb.PersistentClient] = None
        self._collection = None

    def load(self) -> None:
        load_dotenv()
        env_key = os.environ.get("GEMINI_API_KEY", "").strip()
        if env_key:
            self.api_key = env_key

        if not self.api_key:
            raise RuntimeError("Missing Gemini API key (set GEMINI_API_KEY in .env)")
        self._session = requests.Session()
        self._embeddings = HuggingFaceEmbeddings(model_name=EMBEDDING_MODEL)
        self._client = chromadb.PersistentClient(path=CHROMA_DIR)
        self._collection = self._client.get_or_create_collection(COLLECTION_NAME)

    def generate_reply(self, message: str) -> str:
        if self._session is None:
            raise RuntimeError("Client not initialized")

        if not message.strip():
            return ""

        if self._client is None or self._embeddings is None or self._collection is None:
            raise RuntimeError("Vector store not initialized")

        query_vector = self._embeddings.embed_query(message.strip())
        results = self._collection.query(
            query_embeddings=[query_vector],
            n_results=TOP_K,
            include=["documents", "metadatas"],
        )

        documents = results.get("documents", [])
        if not documents or not documents[0]:
            return "Khong tim thay thong tin phu hop trong tai lieu."

        context = "\n\n".join(documents[0])
        prompt = (
            "Bạn là trợ lý hướng dẫn sử dụng hệ thống. "
            "Chỉ trả lời dựa trên tài liệu được cung cấp. "
            "Nếu không có thông tin, hãy nói rõ là không tìm thấy.\n\n"
            f"Tài liệu:\n{context}\n\n"
            f"Câu hỏi: {message.strip()}\n"
            "Trả lời:"
        )

        payload = {
            "contents": [
                {
                    "role": "user",
                    "parts": [{"text": prompt}],
                }
            ]
        }

        url = (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"{self.model_name}:generateContent?key={self.api_key}"
        )
        response = self._session.post(url, json=payload, timeout=self.timeout_seconds)
        if not response.ok:
            raise RuntimeError(
                f"Gemini API error {response.status_code}: {response.text}"
            )
        data = response.json()
        candidates = data.get("candidates", [])
        if not candidates:
            return ""

        parts = candidates[0].get("content", {}).get("parts", [])
        if not parts:
            return ""

        return parts[0].get("text", "").strip()

    def _resolve_models(self) -> list[str]:
        return [self.model_name]
