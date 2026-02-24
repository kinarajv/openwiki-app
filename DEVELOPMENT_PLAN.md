# OpenWiki Technical Architecture & Development Plan (.NET 8 + React)

## 1. Project Setup & Foundational Architecture

**Goal:** Establish the monorepo structure with a .NET 8 backend, React frontend, and a unified PostgreSQL database.

*   **Action 1.1:** Initialize the solution structure in `/root/.openclaw/workspace/deepwiki-app`.
    *   Backend: `.NET 8 Web API` project.
    *   Frontend: `React` (via Vite) with TypeScript and Tailwind CSS.
*   **Action 1.2:** Set up Docker Compose for local development.
    *   Services: PostgreSQL (with `pgvector` extension) and Redis (optional, for SignalR backplane/caching).
*   **Action 1.3:** Configure Entity Framework Core with `Npgsql`.
    *   Define core entities (Users, Repositories, Documentations, ChatHistory).
    *   Configure `pgvector` for storing code and documentation embeddings natively in Postgres.
*   **Action 1.4:** Implement base environment configuration using `appsettings.json` and `.env`, integrating the local `cliproxy:8317`.

## 2. Core API & Integration Layer

**Goal:** Build the .NET services for GitHub ingestion and the AI Chat client.

*   **Action 2.1:** Implement `IGitHubService` using `HttpClient` to fetch metadata, directory trees, and file contents.
*   **Action 2.2:** Build `IAiClientService` supporting the standard OpenAI format, routing to the local `cliproxy` endpoint.
*   **Action 2.3:** Set up EF Core vector search queries using `pgvector` for semantic similarity matching.

## 3. Data Processing & Document Generation Pipeline

**Goal:** Implement background workers in .NET to analyze repositories and generate documentation.

*   **Action 3.1:** Create a .NET Hosted Service / Background Task for the ingestion pipeline (Fetch -> Parse -> Extract).
*   **Action 3.2:** Implement the documentation generation service utilizing the `cliproxy` model to create Markdown files.
*   **Action 3.3:** Build the embedding generator to store documentation chunks into the Postgres vector columns.

## 4. Search & Discovery Implementation

**Goal:** Build the React UI and .NET API for discovering repositories.

*   **Action 4.1:** Develop the Search API endpoint using Postgres Full-Text Search combined with `pgvector` semantic search (Hybrid Search).
*   **Action 4.2:** Build the React Homepage UI (Search bar, trending repos list).
*   **Action 4.3:** Create the Search Results component displaying metadata.

## 5. Repository Documentation UI

**Goal:** Render the AI-generated documentation for users in React.

*   **Action 5.1:** Build the React router setup for `/[owner]/[repo]` fetching data from the .NET Web API.
*   **Action 5.2:** Implement a robust Markdown renderer in React (using `react-markdown`, `rehype-highlight`, and `mermaid`).
*   **Action 5.3:** Build the floating Table of Contents and deep-linking system.

## 6. Interactive AI Chat via SignalR

**Goal:** Implement the conversational interface using `cliproxy` and real-time streaming.

*   **Action 6.1:** Set up an ASP.NET Core SignalR Hub (`ChatHub`) to handle real-time bidirectional communication.
*   **Action 6.2:** Build the React Chat UI component and connect it using `@microsoft/signalr`.
*   **Action 6.3:** Implement the "Fast Mode" execution flow, streaming the AI response back through SignalR.
*   **Action 6.4:** Implement the "Deep Mode" execution flow, streaming reasoning steps and the final answer via SignalR.

## 7. Security, Auth, & Polish

**Goal:** Secure the application.

*   **Action 7.1:** Implement GitHub OAuth using ASP.NET Core Authentication (`AddOAuth`).
*   **Action 7.2:** Apply rate limiting using .NET 8's native rate limiting middleware.
*   **Action 7.3:** Finalize responsive design in React.
*   **Action 7.4:** Add global exception handling middleware in .NET and Error Boundaries in React.