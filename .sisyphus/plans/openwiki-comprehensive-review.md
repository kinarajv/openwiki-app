# Plan: OpenWiki-App Comprehensive Review & Refactoring

## Metadata
- **Created**: 2026-02-26
- **Complexity**: Architecture-tier (40+ tasks across 7 waves)
- **Repository**: `C:\Users\Formulatrix\Documents\DeepWikiOpen\openwiki-app`
- **Solution**: `OpenWiki.sln` (backend: `OpenWiki.Api`, tests: `OpenWiki.Api.Tests`)
- **Frontend**: `frontend/` (React 19 + Vite + TypeScript + TailwindCSS)

## Objective
Transform the openwiki-app from a prototype-quality Minimal API application into a production-grade, fully-tested system following Controller-Service-Repository architecture, with Serilog logging, NUnit testing with Testcontainers, and a properly decomposed frontend.

## Success Criteria
1. All backend code follows Controller → Service → Repository layering
2. Serilog is configured as the sole logging provider with structured logging
3. All tests use NUnit (not xUnit) and Testcontainers.PostgreSql (not InMemoryDatabase)
4. All tests compile AND pass (current state: tests don't compile)
5. Frontend is decomposed into logical components with React Router
6. End-to-end DeepWiki flow works: search → clone → document → summarize → chat

## Current State Assessment

### CRITICAL ISSUES (Blocking)
| # | Issue | File | Severity |
|---|-------|------|----------|
| 1 | ALL routes in Program.cs (298 lines), NO Controllers | `Program.cs` | 🔴 CRITICAL |
| 2 | NO Repository layer — DbContext injected directly into routes | `Program.cs` | 🔴 CRITICAL |
| 3 | NO Serilog — uses Console.WriteLine() | `Program.cs`, `ChatHub.cs` | 🔴 CRITICAL |
| 4 | Tests use xUnit, requirement is NUnit | `Tests.csproj` | 🔴 CRITICAL |
| 5 | `AiClientServiceTests` calls `GenerateDocSectionAsync()` — method doesn't exist. Actual: `GenerateStructuredDocsAsync(string, string, string)` | `AiClientServiceTests.cs` L47,L83 | 🔴 WON'T COMPILE |
| 6 | `ChatHubTests` calls `new ChatHub(mockAiService.Object)` — actual constructor: `ChatHub(AiClientService, IServiceProvider)` | `ChatHubTests.cs` L44 | 🔴 WON'T COMPILE |
| 7 | `ApiIntegrationTests` expects `"queued"` in response — endpoint returns `"completed"` or `"cached"` | `ApiIntegrationTests.cs` L76 | 🔴 WILL FAIL |
| 8 | All 3 Playwright tests reference non-existent DOM IDs (`#repo-input`, `#ingest-button`, `#chat-log`) | `tests/*.spec.ts` | 🔴 WILL FAIL |
| 9 | Testcontainers.PostgreSql NuGet referenced but NEVER used | `Tests.csproj` | 🟡 HIGH |
| 10 | 8 entity models defined inline in `AppDbContext.cs` | `AppDbContext.cs` | 🟡 HIGH |
| 11 | No interfaces for any services | `Services/` | 🟡 HIGH |
| 12 | No DTOs — anonymous types for all API responses | `Program.cs` | 🟡 HIGH |
| 13 | `ChatHub.cs` hardcoded URL `http://localhost:8317` and key `sk-apikey` | `ChatHub.cs` L46,L48 | 🟡 SECURITY |
| 14 | `ChatHub.cs` creates `new HttpClient()` in method — socket exhaustion risk | `ChatHub.cs` L50 | 🟡 HIGH |
| 15 | InMemoryDatabase in production, PostgreSQL connection string unused | `Program.cs` L23 | 🟡 HIGH |
| 16 | `GitHubIngestService` (281 lines, core business logic) has ZERO tests | — | 🟡 HIGH |
| 17 | Monolithic 504-line `App.tsx` with ALL state/views/logic | `App.tsx` | 🟡 MEDIUM |
| 18 | `react-router-dom` installed but unused | `package.json` | 🟡 MEDIUM |
| 19 | No authentication/authorization | `Program.cs` | 🟠 FLAG |
| 20 | CORS allows ALL origins | `Program.cs` L15 | 🟠 FLAG |

## Architecture Target

### Backend Project Structure (After Refactoring)
```
backend/OpenWiki.Api/
├── Controllers/
│   ├── HealthController.cs
│   ├── RepositoryController.cs
│   └── SearchController.cs
├── Hubs/
│   └── ChatHub.cs (refactored)
├── Services/
│   ├── Interfaces/
│   │   ├── IGitHubService.cs
│   │   ├── IGitHubIngestService.cs
│   │   └── IAiClientService.cs
│   ├── GitHubService.cs
│   ├── GitHubIngestService.cs
│   └── AiClientService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IRepositoryRepo.cs
│   │   ├── IDocumentationRepo.cs
│   │   └── IDiagramRepo.cs
│   ├── RepositoryRepo.cs
│   ├── DocumentationRepo.cs
│   └── DiagramRepo.cs
├── Models/
│   ├── Repository.cs
│   ├── Documentation.cs
│   ├── DocSection.cs
│   ├── DocRelation.cs
│   ├── CodeFile.cs
│   ├── Diagram.cs
│   ├── AiConversation.cs
│   └── ChatMessage.cs
├── DTOs/
│   ├── Requests/
│   │   ├── IngestRequest.cs
│   │   ├── RepoCheckRequest.cs
│   │   └── SearchRequest.cs
│   └── Responses/
│       ├── HealthResponse.cs
│       ├── RepoCheckResponse.cs
│       ├── RepoDataResponse.cs
│       ├── IngestResponse.cs
│       └── SearchResponse.cs
├── Data/
│   └── AppDbContext.cs (DbSets only, models removed)
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── Program.cs (slim: DI + middleware + pipeline)
├── appsettings.json
└── appsettings.Development.json
```

### Test Project Structure (After Refactoring)
```
backend/OpenWiki.Api.Tests/
├── Infrastructure/
│   ├── IntegrationTestBase.cs (Testcontainers PostgreSQL)
│   └── WebAppFactory.cs (custom WebApplicationFactory)
├── Unit/
│   ├── Services/
│   │   ├── GitHubServiceTests.cs
│   │   ├── GitHubIngestServiceTests.cs
│   │   └── AiClientServiceTests.cs
│   └── Hubs/
│       └── ChatHubTests.cs
├── Integration/
│   ├── Repositories/
│   │   ├── RepositoryRepoTests.cs
│   │   └── DocumentationRepoTests.cs
│   ├── Controllers/
│   │   ├── HealthControllerTests.cs
│   │   ├── RepositoryControllerTests.cs
│   │   └── SearchControllerTests.cs
│   └── Database/
│       └── DbContextTests.cs
├── GlobalUsings.cs
└── OpenWiki.Api.Tests.csproj
```

### Frontend Structure (After Refactoring)
```
frontend/src/
├── components/
│   ├── layout/
│   │   ├── Header.tsx
│   │   └── Sidebar.tsx
│   ├── home/
│   │   ├── SearchBar.tsx
│   │   └── PopularRepos.tsx
│   ├── ingestion/
│   │   └── IngestProgress.tsx
│   ├── documentation/
│   │   ├── DocumentViewer.tsx
│   │   ├── DiagramRenderer.tsx
│   │   └── MindmapView.tsx
│   └── chat/
│       └── ChatPanel.tsx
├── hooks/
│   ├── useSignalR.ts
│   └── useApi.ts
├── pages/
│   ├── HomePage.tsx
│   └── DocumentationPage.tsx
├── types/
│   └── index.ts
├── config/
│   └── api.ts
├── App.tsx (routing shell only)
├── main.tsx
├── index.css
└── App.css
```

## Dependency Graph
```
Wave 1 (Parallel):
├── Task 1: Extract Models, DTOs, Interfaces ──────┐
└── Task 6: Frontend Decomposition & Routing ──────┤
                                                    │
Wave 2 (After Task 1):                              │
└── Task 2: NUnit + Testcontainers Setup ──────┐   │
                                                │   │
Wave 3 (After Task 2):                          │   │
└── Task 3: Repository Layer + DB Tests ────┐  │   │
                                             │  │   │
Wave 4 (After Task 3):                      │  │   │
└── Task 4: Service Refactor + Serilog ──┐  │  │   │
                                          │  │  │   │
Wave 5 (After Task 4):                   │  │  │   │
└── Task 5: Controllers + Hub Refactor ┐ │  │  │   │
                                        │ │  │  │   │
Wave 6 (After Task 5 + Task 6):        │ │  │  │   │
└── Task 7: E2E Playwright Fixes ───────┘─┘──┘──┘───┘
```

## Guardrails (from Metis Review)
1. **NO authentication/authorization changes** — flagged only, not in scope
2. **Frontend decomposition limited to exactly the structure above** — no infinite refactoring
3. **InMemoryDatabase removed entirely** — PostgreSQL + Testcontainers only
4. **All acceptance criteria must be machine-verifiable** — no "ensure it works" statements
5. **Tests MUST be written concurrently with implementation** — not after

## Decisions Made
| Decision | Choice | Rationale |
|----------|--------|-----------|
| DB Provider | PostgreSQL (remove InMemory) | docker-compose already has pgvector; InMemory can't test real queries |
| Test Framework | NUnit 3.x + FluentAssertions | User requirement; FluentAssertions already in project |
| Test Containers | Testcontainers.PostgreSql (already referenced) | Real DB testing over InMemory fakes |
| Logging | Serilog with Console + File sinks | User requirement; structured logging standard |
| Frontend Routing | react-router-dom (already installed) | Already a dependency; enables proper URL-based navigation |
| API Style | [ApiController] with attribute routing | Industry standard for C# Web APIs |
| Mocking | Moq (already referenced) | Already in project for unit tests |
| Repository Pattern | Specific repositories (not generic) | Domain has distinct query needs per entity |

---

## WAVE 1: Foundation Layer (Parallel Start)

### Task 1: Extract Domain Models, DTOs, and Interfaces
**Category**: `unspecified-high`
**Skills**: `senior-backend`, `senior-architect`
**Depends On**: None
**Blocks**: Task 2, Task 3, Task 4, Task 5

**Context**: Currently all 8 entity models are defined inline in `backend/OpenWiki.Api/Data/AppDbContext.cs` (lines 19-111). All API responses use anonymous types. No service interfaces exist.

**Instructions**:

1. **Create Models folder** at `backend/OpenWiki.Api/Models/` and extract each entity class from `AppDbContext.cs` into its own file:
   - `Repository.cs` — lines 19-32 of AppDbContext.cs. Keep all properties. Add namespace `OpenWiki.Api.Models`.
   - `Documentation.cs` — lines 34-43. Add namespace `OpenWiki.Api.Models`.
   - `DocSection.cs` — lines 45-58. Add namespace `OpenWiki.Api.Models`.
   - `DocRelation.cs` — lines 60-68. Add namespace `OpenWiki.Api.Models`.
   - `CodeFile.cs` — lines 70-80. Add namespace `OpenWiki.Api.Models`.
   - `Diagram.cs` — lines 82-91. Add namespace `OpenWiki.Api.Models`.
   - `AiConversation.cs` — lines 93-101. Add namespace `OpenWiki.Api.Models`.
   - `ChatMessage.cs` — lines 103-111. Add namespace `OpenWiki.Api.Models`.

2. **Update AppDbContext.cs** to only contain DbSet properties. Add `using OpenWiki.Api.Models;`. Remove all class definitions. Keep lines 1-17 (class + DbSets).

3. **Create DTOs folder** at `backend/OpenWiki.Api/DTOs/`:
   - `Requests/IngestRequest.cs`: `public record IngestRequest(string Owner, string Repo);`
   - `Requests/RepoCheckRequest.cs`: `public record RepoCheckRequest(string Owner, string Repo);`
   - `Requests/SearchRequest.cs`: `public record SearchRequest(string Query);`
   - `Responses/HealthResponse.cs`: `public record HealthResponse(string Status);`
   - `Responses/RepoCheckResponse.cs`: `public record RepoCheckResponse(bool Indexed, DateTime? IndexedAt = null, int SectionsCount = 0);`
   - `Responses/RepoDataResponse.cs`: Extract structure from Program.cs lines 95-123. Create a record with Owner, Repo, Document, Metadata (nested record), Sections (list), Relations (list), Diagrams (list).
   - `Responses/IngestResponse.cs`: Extract from Program.cs lines 277-286. Create record with Status, Owner, Repo, Document, Metadata, SectionsCount, RelationsCount, DiagramsCount.
   - `Responses/SearchResponse.cs`: `public record SearchResponse(List<SearchResultItem> Results);` with `public record SearchResultItem(string FullName, string? Description, int Stars, string? Language);`

4. **Create Service Interfaces** at `backend/OpenWiki.Api/Services/Interfaces/`:
   - `IGitHubService.cs`: Extract from `GitHubService.cs`. Methods: `Task<string> GetRepoMetadataAsync(string owner, string repo)`, `Task<List<SearchResultItem>> SearchReposAsync(string query)` — note: change return type from `List<object>` to `List<SearchResultItem>` using the DTO.
   - `IAiClientService.cs`: Extract from `AiClientService.cs`. Method: `Task<string> GenerateStructuredDocsAsync(string codeContext, string owner, string repo)`
   - `IGitHubIngestService.cs`: Extract from `GitHubIngestService.cs`. Method: `Task<IngestResult> ProcessRepositoryAsync(string owner, string repo)`

5. **Make services implement interfaces**: Add `: IGitHubService` to `GitHubService`, `: IAiClientService` to `AiClientService`, `: IGitHubIngestService` to `GitHubIngestService`. Update `using` statements.

6. **Create Repository Interfaces** at `backend/OpenWiki.Api/Repositories/Interfaces/`:
   - `IRepositoryRepo.cs`: Methods: `Task<Repository?> GetByFullNameAsync(string fullName)`, `Task<Repository> AddAsync(Repository entity)`, `Task SaveChangesAsync()`
   - `IDocumentationRepo.cs`: Methods: `Task<Documentation> AddAsync(Documentation entity)`, `Task AddSectionAsync(DocSection section)`, `Task AddRelationAsync(DocRelation relation)`, `Task<List<DocSection>> GetSectionsByRepoIdAsync(Guid repoId)`, `Task<List<DocRelation>> GetRelationsByRepoIdAsync(Guid repoId)`
   - `IDiagramRepo.cs`: Methods: `Task AddAsync(Diagram diagram)`, `Task<List<Diagram>> GetByRepoIdAsync(Guid repoId)`

7. **Move IngestResult classes** from `GitHubIngestService.cs` (lines 7-38: `IngestResult`, `SectionResult`, `RelationResult`, `DiagramResult`) into `Models/IngestResult.cs`. Update namespace to `OpenWiki.Api.Models`.

8. **Run `dotnet build`** from `backend/OpenWiki.Api/` directory and fix any compilation errors.

**QA Verification**:
```bash
cd backend && dotnet build OpenWiki.Api/OpenWiki.Api.csproj
# Must exit 0 with no errors
# Verify files exist:
ls OpenWiki.Api/Models/*.cs        # Should list 8+ files
ls OpenWiki.Api/DTOs/Requests/*.cs  # Should list 3 files
ls OpenWiki.Api/DTOs/Responses/*.cs # Should list 5+ files
ls OpenWiki.Api/Services/Interfaces/*.cs # Should list 3 files
ls OpenWiki.Api/Repositories/Interfaces/*.cs # Should list 3 files
```

---

### Task 6: Frontend Decomposition & Routing
**Category**: `visual-engineering`
**Skills**: `senior-frontend`, `frontend-ui-ux`
**Depends On**: None
**Blocks**: Task 7

**Context**: The entire frontend is a single 504-line `App.tsx` file containing ALL state management, API calls, SignalR connections, view rendering, and UI logic. `react-router-dom` is installed but unused. Backend URL is hardcoded as `window.location.hostname + ':5000'`.

**Instructions**:

1. **Create environment configuration** at `frontend/.env`:
   ```
   VITE_API_URL=http://localhost:5000
   ```
   Create `frontend/src/config/api.ts`:
   ```typescript
   export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';
   ```

2. **Extract TypeScript types** from App.tsx (lines 37-65) into `frontend/src/types/index.ts`:
   - `RepoSearchResult`, `DocSection`, `Diagram`, `Relation` interfaces
   - Export the `slugify` utility function

3. **Extract custom hooks** into `frontend/src/hooks/`:
   - `useSignalR.ts`: Extract SignalR connection logic (App.tsx lines 147-164). Returns `{ connection, chatLog, isTyping, sendQuestion, setChatLog }`.
   - `useApi.ts`: Extract all `fetch()` calls (handleSearch, handleRepoSelect, ingestRepository — App.tsx lines 166-260). Returns `{ searchRepos, checkRepo, ingestRepo, isSearching }`.

4. **Extract components** into `frontend/src/components/`:
   - `layout/Header.tsx`: The sticky header bar (lines 388-406 in doc view, lines 316-321 in home view). Accept props for `activeRepo`, `cachedStatus`, `showMindmap`, `onToggleMindmap`, `onGoHome`. 
   - `layout/Sidebar.tsx`: The documentation sidebar (lines 409-432). Accept props for `docSections`, `diagrams`, `activeSection`, `onSectionClick`.
   - `home/SearchBar.tsx`: The search form (lines 329-335). Accept props for `searchQuery`, `onQueryChange`, `onSearch`, `isSearching`.
   - `home/PopularRepos.tsx`: Popular repo list (lines 341-353). Accept props for `repos`, `onSelect`.
   - `ingestion/IngestProgress.tsx`: The loading stepper (lines 362-382). Accept props for `currentStep`, `steps`.
   - `documentation/DocumentViewer.tsx`: The main doc content area (lines 434-483). Accept props for `repoMetadata`, `generatedDoc`, `docSections`, `diagrams`, `showMindmap`, `mindmapCode`, relations, diagram refs.
   - `documentation/DiagramRenderer.tsx`: Mermaid diagram rendering logic (lines 468-482). Accept props for `diagrams`.
   - `documentation/MindmapView.tsx`: Mindmap overlay (lines 447-452). Accept props for `mindmapCode`, `show`.
   - `chat/ChatPanel.tsx`: The fixed-bottom chat input (lines 487-499). Accept props for `question`, `onQuestionChange`, `onSend`, `isTyping`, `mode`, `onModeToggle`, `activeRepo`.

5. **Create page components** in `frontend/src/pages/`:
   - `HomePage.tsx`: Composes Header + SearchBar + PopularRepos. Uses `useApi` hook.
   - `DocumentationPage.tsx`: Composes Header + Sidebar + DocumentViewer + ChatPanel. Uses `useSignalR` and `useApi` hooks.

6. **Setup React Router** in `frontend/src/App.tsx`:
   ```tsx
   import { BrowserRouter, Routes, Route } from 'react-router-dom';
   import HomePage from './pages/HomePage';
   import DocumentationPage from './pages/DocumentationPage';

   function App() {
     return (
       <BrowserRouter>
         <Routes>
           <Route path="/" element={<HomePage />} />
           <Route path="/wiki/:owner/:repo" element={<DocumentationPage />} />
         </Routes>
       </BrowserRouter>
     );
   }
   export default App;
   ```

7. **Add data-testid attributes** to key interactive elements for Playwright:
   - Search input: `data-testid="search-input"`
   - Popular repo buttons: `data-testid="repo-card-{fullName}"`
   - Mode toggle: `data-testid="mode-toggle"`
   - Chat input: `data-testid="chat-input"`
   - Chat send button: `data-testid="chat-send"`
   - Chat log container: `data-testid="chat-log"`
   - Document viewer: `data-testid="doc-viewer"`

8. **Preserve ALL existing Tailwind styling** — do NOT change any class names or visual design.

**QA Verification**:
```bash
cd frontend && npx tsc --noEmit && npm run build
# Must exit 0 with no TypeScript errors
# Must produce dist/ folder
# Verify component files exist:
ls src/components/layout/*.tsx      # Header.tsx, Sidebar.tsx
ls src/components/home/*.tsx        # SearchBar.tsx, PopularRepos.tsx
ls src/components/chat/*.tsx        # ChatPanel.tsx
ls src/pages/*.tsx                  # HomePage.tsx, DocumentationPage.tsx
ls src/hooks/*.ts                   # useSignalR.ts, useApi.ts
ls src/types/index.ts               # Type definitions
```

---

## WAVE 2: Testing Infrastructure

### Task 2: Migrate to NUnit & Setup Testcontainers
**Category**: `unspecified-high`
**Skills**: `senior-qa`, `senior-backend`
**Depends On**: Task 1
**Blocks**: Task 3

**Context**: Current test project uses xUnit (v2.4.2) with global using `global using Xunit;`. Testcontainers.PostgreSql v4.10.0 is referenced but never used. All tests use InMemoryDatabase. Tests don't compile due to method signature mismatches.

**Instructions**:

1. **Update `OpenWiki.Api.Tests.csproj`** — Replace xUnit packages with NUnit:
   - REMOVE: `xunit` (v2.4.2), `xunit.runner.visualstudio` (v2.4.5)
   - ADD: `NUnit` (v4.3.2), `NUnit3TestAdapter` (v4.6.0), `NUnit.Analyzers` (v4.6.0)
   - KEEP: `FluentAssertions` (v8.8.0), `Moq` (v4.20.72), `Testcontainers.PostgreSql` (v4.10.0), `Microsoft.AspNetCore.Mvc.Testing` (v8.0.4), `Microsoft.NET.Test.Sdk` (v17.6.0), `coverlet.collector` (v6.0.0)
   - KEEP: `Microsoft.EntityFrameworkCore.InMemory` (v8.0.6) — needed for lightweight unit tests only
   - ADD: `Npgsql.EntityFrameworkCore.PostgreSQL` (v8.0.4) — for Testcontainers-based integration tests

2. **Update `GlobalUsings.cs`**:
   ```csharp
   global using NUnit.Framework;
   global using FluentAssertions;
   ```

3. **Delete `UnitTest1.cs`** — empty placeholder, serves no purpose.

4. **Create `Infrastructure/IntegrationTestBase.cs`**:
   ```csharp
   using Testcontainers.PostgreSql;
   using Microsoft.EntityFrameworkCore;
   using OpenWiki.Api.Data;

   namespace OpenWiki.Api.Tests.Infrastructure;

   public abstract class IntegrationTestBase : IAsyncDisposable
   {
       private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
           .WithImage("ankane/pgvector:v0.5.1")
           .Build();

       protected AppDbContext DbContext { get; private set; } = null!;

       [OneTimeSetUp]
       public async Task OneTimeSetup()
       {
           await _postgres.StartAsync();
           var options = new DbContextOptionsBuilder<AppDbContext>()
               .UseNpgsql(_postgres.GetConnectionString())
               .Options;
           DbContext = new AppDbContext(options);
           await DbContext.Database.EnsureCreatedAsync();
       }

       [OneTimeTearDown]
       public async Task OneTimeTearDown()
       {
           await DbContext.DisposeAsync();
           await _postgres.DisposeAsync();
       }

       public async ValueTask DisposeAsync()
       {
           await _postgres.DisposeAsync();
           GC.SuppressFinalize(this);
       }
   }
   ```

5. **Create `Infrastructure/WebAppFactory.cs`**:
   ```csharp
   using Microsoft.AspNetCore.Hosting;
   using Microsoft.AspNetCore.Mvc.Testing;
   using Microsoft.EntityFrameworkCore;
   using Microsoft.Extensions.DependencyInjection;
   using OpenWiki.Api.Data;
   using Testcontainers.PostgreSql;

   namespace OpenWiki.Api.Tests.Infrastructure;

   public class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
   {
       private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
           .WithImage("ankane/pgvector:v0.5.1")
           .Build();

       protected override void ConfigureWebHost(IWebHostBuilder builder)
       {
           builder.ConfigureServices(services =>
           {
               var descriptor = services.SingleOrDefault(
                   d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
               if (descriptor != null) services.Remove(descriptor);

               services.AddDbContext<AppDbContext>(options =>
                   options.UseNpgsql(_postgres.GetConnectionString()));
           });
       }

       public async Task InitializeAsync() => await _postgres.StartAsync();
       public new async Task DisposeAsync() => await _postgres.DisposeAsync();
   }
   ```

6. **Create directory structure**:
   ```
   mkdir -p Infrastructure Unit/Services Unit/Hubs Integration/Repositories Integration/Controllers Integration/Database
   ```

7. **Run `dotnet build`** on the test project to verify NUnit packages resolve correctly.

**QA Verification**:
```bash
cd backend && dotnet build OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj
# Must exit 0
grep -c "NUnit" OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj
# Must return >= 2 (NUnit + NUnit3TestAdapter)
grep -c "xunit" OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj
# Must return 0
```

---

## WAVE 3: Data Access Layer

### Task 3: Implement Repository Layer & Database Configuration
**Category**: `unspecified-high`
**Skills**: `senior-backend`, `test-driven-development`
**Depends On**: Task 1, Task 2
**Blocks**: Task 4

**Context**: Currently all data access is performed directly via `AppDbContext` injected into Program.cs Minimal API routes. Need to implement the Repository interfaces defined in Task 1 and switch from InMemoryDatabase to PostgreSQL.

**Instructions**:

1. **Update `Program.cs` database registration** — Replace InMemoryDatabase with PostgreSQL:
   ```csharp
   builder.Services.AddDbContext<AppDbContext>(options =>
   {
       options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
   });
   ```
   Remove `Microsoft.EntityFrameworkCore.InMemory` NuGet from the API project (keep in test project for unit tests only).

2. **Implement Repository classes** at `backend/OpenWiki.Api/Repositories/`:
   - `RepositoryRepo.cs`:
     - Inject `AppDbContext`
     - Implement `IRepositoryRepo`
     - `GetByFullNameAsync`: Use `.Include(r => r.Documentations).ThenInclude(d => d.Sections)` — extract exact query from Program.cs lines 47-50
     - `AddAsync`: Add entity, return it
     - `SaveChangesAsync`: Delegate to `_context.SaveChangesAsync()`
   - `DocumentationRepo.cs`:
     - Inject `AppDbContext`
     - Implement `IDocumentationRepo`
     - `AddAsync`, `AddSectionAsync`, `AddRelationAsync`: Simple add operations
     - `GetSectionsByRepoIdAsync`: Extract query from Program.cs lines 153-156
     - `GetRelationsByRepoIdAsync`: Extract query from Program.cs lines 83-85
   - `DiagramRepo.cs`:
     - Inject `AppDbContext`
     - Implement `IDiagramRepo`
     - `AddAsync`, `GetByRepoIdAsync`: Simple CRUD

3. **Register repositories in DI** — Add to `Program.cs` (or better, create `Extensions/ServiceCollectionExtensions.cs`):
   ```csharp
   builder.Services.AddScoped<IRepositoryRepo, RepositoryRepo>();
   builder.Services.AddScoped<IDocumentationRepo, DocumentationRepo>();
   builder.Services.AddScoped<IDiagramRepo, DiagramRepo>();
   ```

4. **Write integration tests** at `Integration/Repositories/`:
   - `RepositoryRepoTests.cs` — Extend `IntegrationTestBase`. Test:
     - `GetByFullNameAsync_WhenExists_ReturnsWithIncludes`
     - `GetByFullNameAsync_WhenNotExists_ReturnsNull`
     - `AddAsync_SavesAndReturnsEntity`
   - `DocumentationRepoTests.cs` — Test:
     - `AddSectionAsync_PersistsSection`
     - `GetSectionsByRepoIdAsync_ReturnsOrderedSections`
   - `Integration/Database/DbContextTests.cs` — Rewrite the existing `DbIntegrationTests.cs` using `IntegrationTestBase` (Testcontainers, not InMemory):
     - `CanConnectToPostgres` — verify EnsureCreated works
     - `CanSaveAndRetrieveRepository`
     - `CascadeIncludesWork` — verify navigation properties

5. **Delete the old `DbIntegrationTests.cs`** — it's replaced by the new integration tests.

**QA Verification**:
```bash
cd backend && dotnet test OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj --filter "FullyQualifiedName~Integration.Repositories|FullyQualifiedName~Integration.Database" --logger "console;verbosity=detailed"
# All repository and DB tests must pass
```

---

## WAVE 4: Service Layer & Logging

### Task 4: Refactor Services & Add Serilog
**Category**: `unspecified-high`
**Skills**: `senior-backend`, `test-driven-development`
**Depends On**: Task 3
**Blocks**: Task 5

**Context**: Services exist but don't use interfaces or repositories. No Serilog. The `GitHubIngestService` (281 lines) is the core business logic with ZERO tests. `ChatHub.cs` has hardcoded URLs and creates `new HttpClient()`.

**Instructions**:

1. **Add Serilog NuGet packages** to `OpenWiki.Api.csproj`:
   ```xml
   <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
   <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
   <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
   ```

2. **Configure Serilog in `Program.cs`**:
   ```csharp
   using Serilog;

   Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Information()
       .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
       .WriteTo.File("logs/openwiki-.log", rollingInterval: RollingInterval.Day)
       .CreateLogger();

   builder.Host.UseSerilog();
   ```

3. **Update `appsettings.json`** — Add Serilog configuration section:
   ```json
   "Serilog": {
     "MinimumLevel": {
       "Default": "Information",
       "Override": {
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "WriteTo": [
       { "Name": "Console" },
       { "Name": "File", "Args": { "path": "logs/openwiki-.log", "rollingInterval": "Day" } }
     ]
   }
   ```

4. **Refactor `GitHubIngestService.cs`** — inject `IRepositoryRepo`, `IDocumentationRepo`, `IDiagramRepo` via constructor instead of direct DbContext access. Replace all `Console.WriteLine()` with `_logger.LogInformation()` / `_logger.LogError()`.

5. **Refactor `AiClientService.cs`** — Replace `Console.WriteLine()` calls (lines 163-165) with `_logger.LogError()`. Ensure the service implements `IAiClientService`.

6. **Refactor `GitHubService.cs`** — Add `ILogger<GitHubService>` injection. Replace bare `catch {}` (line 57) with `catch (Exception ex) { _logger.LogWarning(ex, "GitHub search failed"); }`. Implement `IGitHubService`. Change `SearchReposAsync` return type from `List<object>` to `List<SearchResultItem>` (using DTO).

7. **Refactor `ChatHub.cs`**:
   - Remove `IServiceProvider` from constructor — replace with `IHttpClientFactory`
   - Replace `new HttpClient()` (line 50) with `_httpClientFactory.CreateClient()`
   - Remove hardcoded URL `"http://localhost:8317"` — use `IConfiguration` to read `Cliproxy:ApiUrl`
   - Remove hardcoded API key `"sk-apikey"` — use `IConfiguration` to read `OPENCLAW_API_KEY`
   - Inject `ILogger<ChatHub>` and replace `Console.Error` usage

8. **Register `IHttpClientFactory`** in `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient();
   ```

9. **Write unit tests** at `Unit/Services/`:
   - `GitHubServiceTests.cs` — Rewrite using NUnit `[Test]` instead of `[Fact]`. Use Moq for `HttpMessageHandler`. Test:
     - `GetRepoMetadataAsync_ReturnsJsonString`
     - `GetRepoMetadataAsync_ThrowsOnHttpError`
     - `SearchReposAsync_ReturnsResults`
     - `SearchReposAsync_ReturnsEmptyOnError`
   - `AiClientServiceTests.cs` — Rewrite using NUnit. Fix the method name from `GenerateDocSectionAsync` to `GenerateStructuredDocsAsync`. Test:
     - `GenerateStructuredDocsAsync_ReturnsAiContent`
     - `GenerateStructuredDocsAsync_ThrowsOnHttpError`
     - `GenerateStructuredDocsAsync_ThrowsOnEmptyContent`
   - `GitHubIngestServiceTests.cs` — **NEW FILE** (currently has ZERO tests). Mock `IAiClientService`. Test:
     - `ProcessRepositoryAsync_ClonesAndProcessesRepo` (mock git clone via process abstraction)
     - `ParseAiResponse_ValidJson_ReturnsSections`
     - `ParseAiResponse_InvalidJson_ReturnsFallback`
     - `ParseAiResponse_EmptyResponse_ReturnsDefaultSection`
   - `Unit/Hubs/ChatHubTests.cs` — Rewrite using NUnit. Fix constructor to match `ChatHub(IAiClientService, IHttpClientFactory, IConfiguration, ILogger<ChatHub>)`. Test:
     - `AskQuestion_StreamsChunksAndCompletes`
     - `AskQuestion_HandlesAiFailureGracefully`

10. **Delete old test files**: Remove `GitHubServiceTests.cs`, `AiClientServiceTests.cs`, `ChatHubTests.cs` from root of test project — replaced by new files in `Unit/` subdirectories.

**QA Verification**:
```bash
cd backend && dotnet test OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj --filter "FullyQualifiedName~Unit" --logger "console;verbosity=detailed"
# All unit tests must pass
grep -c "Serilog" OpenWiki.Api/Program.cs
# Must return >= 1
grep -c "Console.WriteLine" OpenWiki.Api/Program.cs OpenWiki.Api/Services/*.cs OpenWiki.Api/Hubs/ChatHub.cs
# Must return 0 (all replaced with Serilog)
```

---

## WAVE 5: Web Layer (Controllers & Hub)

### Task 5: Convert Minimal APIs to Controllers & Final Hub Refactor
**Category**: `unspecified-high`
**Skills**: `senior-backend`, `senior-architect`
**Depends On**: Task 4
**Blocks**: Task 7

**Context**: Program.cs (298 lines) contains all API endpoint logic as Minimal API routes (`app.MapGet`, `app.MapPost`). Need to extract into proper `[ApiController]` classes while keeping Program.cs slim (DI registration + middleware pipeline only).

**Instructions**:

1. **Add `builder.Services.AddControllers()` and `app.MapControllers()`** to `Program.cs`.

2. **Create `Controllers/HealthController.cs`**:
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   public class HealthController : ControllerBase
   {
       [HttpGet]
       public IActionResult Get() => Ok(new HealthResponse("healthy"));
   }
   ```
   Extract from Program.cs line 41.

3. **Create `Controllers/RepositoryController.cs`**:
   - Inject: `IRepositoryRepo`, `IGitHubService`, `IGitHubIngestService`, `ILogger<RepositoryController>`
   - `[HttpGet("check")]` — Extract logic from Program.cs lines 44-62. Use `IRepositoryRepo.GetByFullNameAsync()`. Return `RepoCheckResponse` DTO.
   - `[HttpGet("data")]` — Extract from lines 65-124. Use repos. Return `RepoDataResponse` DTO.
   - `[HttpPost("ingest")]` — Extract from lines 141-293. This is the LARGEST extraction (~150 lines). Use `IRepositoryRepo`, `IDocumentationRepo`, `IDiagramRepo`. Return `IngestResponse` DTO.

4. **Create `Controllers/SearchController.cs`**:
   - Inject: `IGitHubService`, `ILogger<SearchController>`
   - `[HttpGet]` — Extract from lines 127-138. Return `SearchResponse` DTO.

5. **Create `Middleware/ExceptionHandlingMiddleware.cs`**:
   ```csharp
   public class ExceptionHandlingMiddleware
   {
       private readonly RequestDelegate _next;
       private readonly ILogger<ExceptionHandlingMiddleware> _logger;

       public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
       {
           _next = next;
           _logger = logger;
       }

       public async Task InvokeAsync(HttpContext context)
       {
           try { await _next(context); }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
               context.Response.StatusCode = 500;
               await context.Response.WriteAsJsonAsync(new { error = ex.Message });
           }
       }
   }
   ```
   Register in Program.cs: `app.UseMiddleware<ExceptionHandlingMiddleware>();`

6. **Create `Extensions/ServiceCollectionExtensions.cs`** — Move all DI registrations out of Program.cs:
   ```csharp
   public static class ServiceCollectionExtensions
   {
       public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
       {
           // DbContext
           services.AddDbContext<AppDbContext>(options =>
               options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

           // Repositories
           services.AddScoped<IRepositoryRepo, RepositoryRepo>();
           services.AddScoped<IDocumentationRepo, DocumentationRepo>();
           services.AddScoped<IDiagramRepo, DiagramRepo>();

           // Services
           services.AddHttpClient<IGitHubService, GitHubService>();
           services.AddHttpClient<IAiClientService, AiClientService>();
           services.AddScoped<IGitHubIngestService, GitHubIngestService>();
           services.AddHttpClient();

           return services;
       }
   }
   ```
   Call in Program.cs: `builder.Services.AddApplicationServices(builder.Configuration);`

7. **Slim down Program.cs** to approximately 30-40 lines:
   ```csharp
   using Serilog;
   using OpenWiki.Api.Extensions;
   using OpenWiki.Api.Hubs;
   using OpenWiki.Api.Middleware;

   Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Information()
       .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.File("logs/openwiki-.log", rollingInterval: RollingInterval.Day)
       .CreateLogger();

   var builder = WebApplication.CreateBuilder(args);
   builder.Host.UseSerilog();

   builder.Services.AddControllers();
   builder.Services.AddEndpointsApiExplorer();
   builder.Services.AddSwaggerGen();
   builder.Services.AddSignalR();
   builder.Services.AddApplicationServices(builder.Configuration);
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("AllowFrontend",
           b => b.SetIsOriginAllowed(_ => true)
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 .AllowCredentials());
   });

   var app = builder.Build();

   if (app.Environment.IsDevelopment())
   {
       app.UseSwagger();
       app.UseSwaggerUI();
   }

   app.UseMiddleware<ExceptionHandlingMiddleware>();
   app.UseCors("AllowFrontend");
   app.MapControllers();
   app.MapHub<ChatHub>("/chatHub");

   app.Run();
   public partial class Program { }
   ```

8. **Remove all Minimal API routes** from Program.cs — all `app.MapGet` and `app.MapPost` calls (lines 41-293).

9. **Write controller integration tests** at `Integration/Controllers/`:
   - `HealthControllerTests.cs` — Using `WebAppFactory`. Test:
     - `Get_ReturnsHealthyStatus`
   - `RepositoryControllerTests.cs` — Using `WebAppFactory`. Test:
     - `Check_WhenNotIndexed_ReturnsFalse`
     - `Check_WhenIndexed_ReturnsTrue`
     - `Data_WhenNotFound_Returns404`
     - `Ingest_ProcessesAndReturnsCompleted` (mock external services)
   - `SearchControllerTests.cs` — Test:
     - `Search_ReturnsResults`
     - `Search_EmptyQuery_ReturnsEmpty`

10. **Delete old `ApiIntegrationTests.cs`** — replaced by new controller tests.

**QA Verification**:
```bash
cd backend && dotnet build && dotnet test --logger "console;verbosity=detailed"
# ALL tests must pass (unit + integration)
# Verify Program.cs is slim:
wc -l OpenWiki.Api/Program.cs
# Should be ~35-45 lines
# Verify controllers exist:
ls OpenWiki.Api/Controllers/*.cs
# Should list: HealthController.cs, RepositoryController.cs, SearchController.cs
# Verify no Minimal API routes remain:
grep -c "app.MapGet\|app.MapPost" OpenWiki.Api/Program.cs
# Must return 0
```

---

## WAVE 6: End-to-End Verification

### Task 7: Fix Playwright E2E Tests & Final Verification
**Category**: `unspecified-high`
**Skills**: `senior-qa`, `playwright`
**Depends On**: Task 5, Task 6

**Context**: All 3 existing Playwright tests reference non-existent DOM IDs (`#repo-input`, `#ingest-button`, `#chat-log`, `#chat-input`, `#chat-send`). After frontend decomposition (Task 6), new `data-testid` attributes were added. Tests must be rewritten to match the new component structure and API routes.

**Instructions**:

1. **Rewrite `tests/App.spec.ts`**:
   ```typescript
   import { test, expect } from '@playwright/test';

   test('has title and search bar', async ({ page }) => {
     await page.goto('/');
     await expect(page.getByText('OpenWiki')).toBeVisible();
     await expect(page.getByText('Which repo would you like to understand?')).toBeVisible();
     await expect(page.getByTestId('search-input')).toBeVisible();
   });

   test('displays popular repositories', async ({ page }) => {
     await page.goto('/');
     await expect(page.getByText('Popular Repositories')).toBeVisible();
     await expect(page.getByText('microsoft/vscode')).toBeVisible();
   });
   ```

2. **Rewrite `tests/E2EFlow.spec.ts`** — Mock the backend API:
   ```typescript
   import { test, expect } from '@playwright/test';

   test.describe('OpenWiki End-to-End Flow', () => {
     test('Mock ingestion flow', async ({ page }) => {
       // Mock the check endpoint
       await page.route('**/api/repo/check*', async route => {
         await route.fulfill({ json: { indexed: false } });
       });

       // Mock the ingest endpoint with full response
       await page.route('**/api/ingest*', async route => {
         const json = {
           status: 'completed',
           owner: 'microsoft',
           repo: 'playwright',
           document: '# Overview\n\nPlaywright is a testing framework.',
           metadata: { stars: 68000, language: 'TypeScript', description: 'Playwright testing' },
           sections: 1,
           relations: 0,
           diagrams: 0
         };
         await route.fulfill({ json });
       });

       await page.goto('/');
       // Click on a popular repo card
       await page.getByText('microsoft/playwright').click();
       // Should transition to documentation view
       await expect(page.getByTestId('doc-viewer')).toBeVisible({ timeout: 15000 });
       await expect(page.getByText('Overview')).toBeVisible();
     });
   });
   ```

3. **Rewrite `tests/FullUrlFlow.spec.ts`** — Test GitHub URL parsing:
   ```typescript
   import { test, expect } from '@playwright/test';

   test('Search and select repository', async ({ page }) => {
     await page.goto('/');

     // Mock search
     await page.route('**/api/search*', async route => {
       await route.fulfill({
         json: { results: [{ fullName: 'router-for-me/CLIProxyAPI', description: 'CLI Proxy', stars: 100, language: 'TypeScript' }] }
       });
     });

     const searchInput = page.getByTestId('search-input');
     await searchInput.fill('CLIProxyAPI');
     await searchInput.press('Enter');

     // Should display search results
     await expect(page.getByText('router-for-me/CLIProxyAPI')).toBeVisible({ timeout: 10000 });
   });
   ```

4. **Update `playwright.config.ts`** — Ensure it matches the new frontend dev server setup (should already work if Vite config unchanged).

5. **Run the full test suite**:
   ```bash
   cd frontend && npx playwright test
   ```

6. **Final backend verification**:
   ```bash
   cd backend && dotnet test --logger "console;verbosity=detailed"
   ```

**QA Verification**:
```bash
# Backend: ALL tests pass
cd backend && dotnet test --logger "console;verbosity=detailed"
# Frontend: Build succeeds
cd frontend && npm run build
# Frontend: E2E tests pass
cd frontend && npx playwright test --reporter=list
# Architecture: Verify structure
find backend/OpenWiki.Api/Controllers -name "*.cs" | wc -l   # >= 3
find backend/OpenWiki.Api/Repositories -name "*.cs" | wc -l  # >= 6 (3 impl + 3 interface)
find backend/OpenWiki.Api/Models -name "*.cs" | wc -l        # >= 8
find backend/OpenWiki.Api/DTOs -name "*.cs" | wc -l          # >= 8
grep -c "Console.WriteLine" backend/OpenWiki.Api/**/*.cs     # 0
grep -c "Serilog" backend/OpenWiki.Api/Program.cs            # >= 1
grep -c "NUnit" backend/OpenWiki.Api.Tests/*.csproj          # >= 2
grep -c "xunit" backend/OpenWiki.Api.Tests/*.csproj          # 0
```

---

## Final Verification Wave

After all tasks complete, run this comprehensive check:

```bash
# 1. Backend builds clean
cd backend && dotnet build --no-restore 2>&1 | tail -5

# 2. All backend tests pass
dotnet test --logger "console;verbosity=detailed" 2>&1 | tail -20

# 3. Frontend builds clean
cd ../frontend && npm run build 2>&1 | tail -5

# 4. Frontend TypeScript check
npx tsc --noEmit 2>&1 | tail -5

# 5. Playwright E2E tests pass
npx playwright test --reporter=list 2>&1 | tail -10

# 6. Architecture compliance
echo "=== ARCHITECTURE CHECK ==="
echo "Controllers: $(find ../backend/OpenWiki.Api/Controllers -name '*.cs' 2>/dev/null | wc -l)"
echo "Services: $(find ../backend/OpenWiki.Api/Services -name '*.cs' 2>/dev/null | wc -l)"
echo "Repositories: $(find ../backend/OpenWiki.Api/Repositories -name '*.cs' 2>/dev/null | wc -l)"
echo "Models: $(find ../backend/OpenWiki.Api/Models -name '*.cs' 2>/dev/null | wc -l)"
echo "DTOs: $(find ../backend/OpenWiki.Api/DTOs -name '*.cs' 2>/dev/null | wc -l)"
echo "Serilog refs: $(grep -rc 'Serilog' ../backend/OpenWiki.Api/Program.cs)"
echo "Console.WriteLine: $(grep -rc 'Console.WriteLine' ../backend/OpenWiki.Api/Program.cs ../backend/OpenWiki.Api/Services/*.cs ../backend/OpenWiki.Api/Hubs/*.cs 2>/dev/null)"
echo "NUnit refs: $(grep -c 'NUnit' ../backend/OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj)"
echo "xUnit refs: $(grep -c 'xunit' ../backend/OpenWiki.Api.Tests/OpenWiki.Api.Tests.csproj)"
```

## Out of Scope (Explicitly Excluded)
- Authentication/Authorization — flagged as needed but not in this plan
- CI/CD pipeline creation — separate concern
- CORS policy tightening — requires knowing deployment URLs
- Dark mode implementation (frontend `Moon` button is non-functional)
- Performance optimization
- Docker containerization of the API itself
