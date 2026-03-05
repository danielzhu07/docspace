Contributing to DocSpace
========================

Thanks for taking a look at DocSpace! This document outlines a lightweight workflow for developing and contributing to this project.

Project layout
--------------

- `DocSpace.Api/` – ASP.NET Core Web API (Postgres + EF Core)
- `embedding_service/` – FastAPI + SentenceTransformers embedding service
- `frontend/` – React + TypeScript + Vite client
- `docker-compose.yml` – Postgres database container

Local development
-----------------

1. **Clone and install**

   ```bash
   git clone <this-repo-url>
   cd docspace
   ```

2. **Start Postgres**

   ```bash
   docker compose up -d
   ```

3. **Run the embedding service**

   ```bash
   cd embedding_service
   python -m venv .venv
   # Windows:
   .venv\Scripts\activate
   # macOS/Linux:
   # source .venv/bin/activate
   pip install -r requirements.txt
   uvicorn app:app --reload --port 8001
   ```

4. **Run the .NET API**

   ```bash
   cd DocSpace.Api
   dotnet restore
   dotnet ef database update
   dotnet run
   ```

5. **Run the frontend**

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

Coding guidelines
-----------------

- **Style**
  - Follow the existing style in each subproject:
    - C# conventions in `DocSpace.Api`
    - TypeScript/React best practices in `frontend`
    - PEP 8 style in `embedding_service`
  - Prefer clear, self-describing names over comments that repeat what the code already says.

- **Testing / sanity checks**
  - For the frontend, at minimum run:
    - `npm run build`
    - `npm run lint`
  - For the API, ensure the app starts cleanly and migrations apply:
    - `dotnet ef database update`
    - `dotnet run`
  - For the embedding service, make a quick request against `/embed` or `/embed_batch` to confirm it’s working.

Branches & pull requests
------------------------

- Create feature branches from the main branch.
- Keep PRs small and focused; aim to solve one problem per PR.
- In your PR description, include:
  - **What** you changed
  - **Why** you changed it
  - **How** you tested it

Configuration & secrets
-----------------------

- Do not commit real secrets (API keys, production connection strings, etc.).
- Local development uses:
  - `DocSpace.Api/appsettings.Development.json` for connection strings and logging
  - `docker-compose.yml` for the local Postgres instance
- If you introduce new configuration, prefer environment variables or a separate development config file and document it in `README.md`.

Issue reporting
---------------

When filing an issue or bug, please include:

- What you tried to do
- What you expected to happen
- What actually happened (including logs or stack traces when relevant)
- Steps to reproduce, if possible

