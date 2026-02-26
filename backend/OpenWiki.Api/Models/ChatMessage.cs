using System;

namespace OpenWiki.Api.Models;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public AiConversation? Conversation { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
