import shutil
from pathlib import Path

import chromadb
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter

from config import (
    CHROMA_DIR,
    CHUNK_OVERLAP,
    CHUNK_SIZE,
    COLLECTION_NAME,
    DOCS_DIR,
)
from vectorizer import get_vectorizer

def load_documents(docs_dir: Path) -> list:
    documents = []
    for pdf_path in docs_dir.glob("*.pdf"):
        loader = PyPDFLoader(str(pdf_path))
        documents.extend(loader.load())
    return documents


def main() -> int:
    docs_dir = Path(DOCS_DIR)
    if not docs_dir.exists():
        raise SystemExit(f"Docs folder not found: {docs_dir}")

    db_dir = Path(CHROMA_DIR)
    if db_dir.exists():
        shutil.rmtree(db_dir)

    documents = load_documents(docs_dir)
    if not documents:
        raise SystemExit("No PDF files found to load.")

    splitter = RecursiveCharacterTextSplitter(
        chunk_size=CHUNK_SIZE,
        chunk_overlap=CHUNK_OVERLAP,
    )
    chunks = splitter.split_documents(documents)

    vectorizer = get_vectorizer()
    vectors = vectorizer.embed_documents([chunk.page_content for chunk in chunks])

    if not vectors:
        raise SystemExit("No vectors generated from documents.")

    client = chromadb.PersistentClient(path=CHROMA_DIR)
    try:
        client.delete_collection(COLLECTION_NAME)
    except Exception:
        pass

    collection = client.get_or_create_collection(COLLECTION_NAME)

    ids = [f"doc_{idx}" for idx in range(len(chunks))]
    documents = [chunk.page_content for chunk in chunks]
    metadatas = [
        {
            "source": chunk.metadata.get("source", ""),
            "page": int(chunk.metadata.get("page", -1) or -1),
        }
        for chunk in chunks
    ]

    collection.add(ids=ids, documents=documents, embeddings=vectors, metadatas=metadatas)

    print(f"Indexed {len(chunks)} chunks into ChromaDB.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
