using System;
using System.Collections.Generic;

namespace OpenWiki.Api.Models;

public class AiConversation
{
    public Guid Id { get; set; }
    public Guid? RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public string Mode { get; set; } = "fast";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
