
using RepoDb;
using RepoDb.Enumerations;
using PertCPMBot.Models;

namespace PertCPMBot.Data;

public static class EntityMappings
{
    public static void Configure()
    {
        FluentMapper.Entity<ConversationHistory>()
            .Table("conversation_history")
            .Primary(e => e.Id, true)  
            .Column(e => e.Id, "id")
            .Column(e => e.Phone, "phone")
            .Column(e => e.Role, "role")
            .Column(e => e.Content, "content")
            .Column(e => e.CreatedAt, "created_at");
    }
}