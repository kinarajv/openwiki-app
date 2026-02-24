# DeepWiki MVP

AI-powered documentation generator with multi-document architecture, Mermaid diagrams, and persistent indexing.

## Features

- **Real GitHub Ingestion** - Clones repos, scans all source files
- **Multi-Document Generation** - Overview, Architecture, Components, Models, APIs
- **Document Relations** - Visual mindmap showing connections
- **Mermaid Diagrams** - Architecture, Data Flow, Class diagrams
- **Persistent Indexing** - Caches to DB, instant reload on revisit
- **AI Chat** - Fast/Deep modes with streaming responses

## Tech Stack

- **Backend:** .NET 8, Entity Framework Core, SignalR
- **Frontend:** React, TypeScript, Tailwind CSS, Mermaid.js
- **Database:** InMemory (easily swappable to PostgreSQL)

## Quick Start

### Backend
```bash
cd backend/DeepWiki.Api
dotnet restore
dotnet run --urls "http://localhost:5000"
```

### Frontend
```bash
cd frontend
npm install
npm run dev
```

## API Endpoints

- `GET /api/health` - Health check
- `GET /api/repo/check?owner=X&repo=Y` - Check if repo is indexed
- `GET /api/repo/data?owner=X&repo=Y` - Get cached documentation
- `POST /api/ingest?owner=X&repo=Y` - Index a repository
- `GET /api/search?q=query` - Search GitHub repos

## Configuration

Set these environment variables or update `appsettings.json`:
- `Cliproxy:ApiUrl` - AI API endpoint
- `Cliproxy:FastModel` - Model for Fast mode (default: gpt-5-codex-mini)
- `Cliproxy:DeepModel` - Model for Deep mode (default: gpt-5-codex)

## License

MIT
