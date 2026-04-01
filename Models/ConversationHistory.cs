namespace PertCPMBot.Models
{
    public class ConversationHistory
    { 
        public int Id { get; set; }
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    }
}
