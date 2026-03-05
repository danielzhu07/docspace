DocSpace
========

DocSpace is a small, end‑to‑end document search playground. It lets you:

- Ingest documents into Postgres
- Chunk and embed them using a Python embedding service
- Run semantic search through a .NET API
- Explore results from a React frontend

This repository is a lightweight monorepo with three main parts:

- **`DocSpace.Api`** – ASP.NET Core Web API (Postgres + EF Core + semantic search endpoints)
- **`embedding_service`** – FastAPI service exposing a SentenceTransformers model
- **`frontend`** – React + TypeScript + Vite UI

---

Getting started
---------------

### Prerequisites

- **Node.js** 18+ (for the React frontend)
- **.NET SDK** 9.0+ (for `DocSpace.Api`)
- **Python** 3.10+ (for `embedding_service`)
- **PostgreSQL 16** (or Docker to run it via `docker-compose.yml`)

### 1. Start Postgres

The simplest way is via Docker using the provided `docker-compose.yml` at the repo root:

```bash
docker compose up -d
```

This starts a Postgres 16 container named `docspace-db` with:

- **DB name**: `docspace`
- **User**: `docspace`
- **Password**: `docspacepw`
- **Port**: `5432`

The .NET API is already configured to use this connection string in `appsettings.Development.json`.

### 2. Run the embedding service (Python)

From the `embedding_service` directory:

```bash
cd embedding_service
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
pip install -r requirements.txt

uvicorn app:app --host 0.0.0.0 --port 8001 --reload
```

This service exposes two endpoints:

- `POST /embed` – single text to embedding
- `POST /embed_batch` – batch text to embeddings

The .NET API calls this service via `http://localhost:8001`.

### 3. Run the .NET API (`DocSpace.Api`)

From the `DocSpace.Api` directory:

```bash
cd DocSpace.Api
dotnet restore
dotnet ef database update  # apply migrations
dotnet run
```

By default the API will:

- Connect to Postgres using `Default` connection string from `appsettings.Development.json`
- Expose controllers for:
  - document ingestion
  - semantic search
  - health checks
- Enable Swagger UI in development

### 4. Run the frontend

From the `frontend` directory:

```bash
cd frontend
npm install
npm run dev
```

Vite typically runs on `http://localhost:5173`. The API currently allows CORS from that origin.

---

Project structure
-----------------

At a high level:

- `DocSpace.Api/`
  - ASP.NET Core Web API
  - `Data/AppDbContext.cs` – EF Core context
  - `Models/` – `Document`, `DocumentChunk`, etc.
  - `Controllers/`
    - `DocumentsController` – document ingestion endpoints
    - `SemanticSearchController` – semantic search endpoints
    - `SearchController` / `HealthController` – misc search & health checks
  - `Services/`
    - `TextChunker` – splits documents into chunks before embedding
    - `SemanticSplitter` – additional semantic chunking logic
    - `EmbeddingClient` – HTTP client for the Python embedding service

- `embedding_service/`
  - `app.py` – FastAPI app using `sentence_transformers` (`all-MiniLM-L6-v2`)
  - `requirements.txt` – Python dependencies

- `frontend/`
  - React + TypeScript + Vite client
  - Entry point in `src/main.tsx` and `src/App.tsx`

- `docker-compose.yml`
  - Postgres 16 container with a persistent volume

---

Development notes
-----------------

- **Migrations**: The `DocSpace.Api/Migrations` folder contains EF Core migrations for the database schema (documents, chunks, embeddings).
- **CORS**: CORS is currently configured to only allow `http://localhost:5173`. If your frontend runs elsewhere, update `Program.cs`.
- **Configuration**: Local development connection strings live in `DocSpace.Api/appsettings.Development.json`. Do not commit secrets to version control for production usage.

---

Scripts & common commands
-------------------------

- **Frontend**
  - `npm run dev` – start Vite dev server
  - `npm run build` – type-check and build
  - `npm run lint` – run ESLint

- **Backend (.NET)**
  - `dotnet run` – start the API
  - `dotnet ef migrations add <Name>` – add a new migration
  - `dotnet ef database update` – apply migrations

- **Embedding service**
  - `uvicorn app:app --reload --port 8001` – dev server

---

Contributing
------------

See `CONTRIBUTING.md` for basic guidelines on how to work on this repo.

