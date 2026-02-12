from typing import List
from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer

app = FastAPI()
model = SentenceTransformer("all-MiniLM-L6-v2")  # fast + solid

class EmbedRequest(BaseModel):
    text: str

class EmbedBatchRequest(BaseModel):
    texts: List[str]

@app.post("/embed")
def embed(req: EmbedRequest):
    vec = model.encode(req.text, normalize_embeddings=True).tolist()
    return {"embedding": vec}

@app.post("/embed_batch")
def embed_batch(req: EmbedBatchRequest):
    vecs = model.encode(req.texts, normalize_embeddings=True).tolist()
    return {"embeddings": vecs}
