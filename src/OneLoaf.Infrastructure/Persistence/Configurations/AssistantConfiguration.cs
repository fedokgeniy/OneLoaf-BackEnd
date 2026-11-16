using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence.Configurations;

public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> b)
    {
        b.ToTable("chat_conversations", t => t.HasComment("E8. Conversations with the AI assistant."));
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200);
        b.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("chat_messages", t =>
        {
            t.HasComment("E8, US-34-36. Messages of the user, the assistant and tool results.");
            t.HasCheckConstraint("ck_chat_messages_tokens", "input_tokens >= 0 AND output_tokens >= 0");
            t.HasCheckConstraint("ck_chat_messages_tool_name", "role <> 'tool' OR tool_name IS NOT NULL");
            t.HasCheckConstraint("ck_chat_messages_tool_payload",
                "tool_payload IS NULL OR (tool_name IS NOT NULL AND jsonb_typeof(tool_payload) = 'object')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.ToolName).HasMaxLength(100);
        b.Property(x => x.ToolPayload).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        b.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
