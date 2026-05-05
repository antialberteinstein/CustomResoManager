from typing import Optional

from sentence_transformers import SentenceTransformer

from config import EMBEDDING_MODEL


class Vectorizer:
    def __init__(self, model_name: str) -> None:
        self._model = SentenceTransformer(model_name, device="cpu")

    def embed_documents(self, texts: list[str]) -> list[list[float]]:
        vectors = self._model.encode(texts, convert_to_numpy=True)
        return [vector.tolist() for vector in vectors]

    def embed_query(self, text: str) -> list[float]:
        vector = self._model.encode([text], convert_to_numpy=True)[0]
        return vector.tolist()


_vectorizer: Optional[Vectorizer] = None


def get_vectorizer() -> Vectorizer:
    global _vectorizer
    if _vectorizer is None:
        _vectorizer = Vectorizer(EMBEDDING_MODEL)
    return _vectorizer
