# OpenWiki - Full E2E Flow Documentation

## 1. The Core Architecture Principle
Yes, OpenWiki **does create a document**. That is the fundamental difference between OpenWiki and a standard AI chatbot. 

It generates a static, highly structured **Markdown Document** first, and *then* allows you to chat with it.

### The Flow Breakdown
When a user clicks "Index Repo", two distinct things happen in the backend:

1.  **The Generation Phase (Creates the Document)**
    *   The backend pulls the GitHub files.
    *   It passes the metadata and file tree to the AI.
    *   The AI generates a massive, structured Markdown string containing:
        *   `Overview`
        *   `Purpose and Scope`
        *   `Process Architecture`
        *   `Core Components`
    *   **Crucially:** This markdown is immediately saved to the PostgreSQL database in the `Documentations` table so it never has to be generated again.
    
2.  **The Reading Phase (Displaying the Document)**
    *   The React frontend renders this Markdown document exactly like a standard Wiki or README file. It includes syntax highlighting and a Table of Contents.

3.  **The Chat Phase (Talking to the Document)**
    *   The chat interface sits *alongside* or *below* this generated document.
    *   When you ask a question in the chat, the AI uses the **Document it just created** (plus the raw source code vectors) to answer your question.

## How it works in our `.NET` implementation
Look at how I structured the `AppDbContext.cs` database schema:

```csharp
public class Documentation
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "generating";
    
    // It creates these structured sections!
    public ICollection<DocSection> Sections { get; set; } = new List<DocSection>();
}

public class DocSection
{
    public int Level { get; set; } // H1, H2, H3
    public string Title { get; set; } = string.Empty;
    
    // THIS is the actual generated document text
    public string? ContentMarkdown { get; set; } 
}
```

The system doesn't just chat; it literally authors a Wiki page (`DocSection.ContentMarkdown`) and saves it to the database so anyone else who visits that repo URL later instantly sees the documentation without waiting. 

I can build out the React UI component that actually renders this Markdown document if you'd like to see it fully realized visually!