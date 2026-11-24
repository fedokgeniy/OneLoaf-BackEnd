using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>EF Core implementation of <see cref="IConversationRepository"/>.</summary>
public sealed class ConversationRepository(OneLoafDbContext db) : IConversationRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationDto>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
        (await db.ChatConversations.AsNoTracking().Where(c => c.OwnerId == ownerId).OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    /// <inheritdoc />
    public async Task<ConversationDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var conversation = await db.ChatConversations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.OwnerId == ownerId && c.Id == id, cancellationToken);
        return conversation is null ? null : ToDto(conversation);
    }

    /// <inheritdoc />
    public async Task<ConversationDto> CreateAsync(Guid ownerId, string? title, CancellationToken cancellationToken)
    {
        var entity = new ChatConversation { OwnerId = ownerId, Title = title };
        db.ChatConversations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return (await GetAsync(ownerId, entity.Id, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.ChatConversations.Where(c => c.OwnerId == ownerId && c.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid conversationId, bool includeTools, CancellationToken cancellationToken)
    {
        var messages = db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (!includeTools)
        {
            messages = messages.Where(m => m.Role != ChatRole.Tool);
        }

        return (await messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MessageDto> AddMessageAsync(Guid conversationId, NewMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var entity = new ChatMessage
        {
            ConversationId = conversationId,
            Role = message.Role,
            Content = message.Content,
            ToolName = message.ToolName,
            ToolPayload = message.ToolPayload,
            InputTokens = message.InputTokens,
            OutputTokens = message.OutputTokens,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatMessages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.ChatConversations.Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken);
        db.ChangeTracker.Clear();
        return ToDto(entity);
    }

    /// <inheritdoc />
    public Task SetTitleIfEmptyAsync(Guid conversationId, string title, CancellationToken cancellationToken) =>
        db.ChatConversations.Where(c => c.Id == conversationId && c.Title == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, title), cancellationToken);

    private static ConversationDto ToDto(ChatConversation c) =>
        new(c.Id, c.Title, Utc(c.CreatedAt), Utc(c.UpdatedAt));

    private static MessageDto ToDto(ChatMessage m) => new(m.Id, m.Role, m.Content, m.ToolName,
        m.ToolPayload is null ? null : JsonDocument.Parse(m.ToolPayload).RootElement.Clone(), Utc(m.CreatedAt));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
