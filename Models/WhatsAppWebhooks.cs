namespace PertCPMBot.Models
{
    public record WhatsAppWebhookPayload(
        string @object,
        List<WhatsAppEntry> entry
    );

    public record WhatsAppEntry(
        string id,
        List<WhatsAppChange> changes
    );

    public record WhatsAppChange(
        WhatsAppValue value,
        string field
    );

    public record WhatsAppValue(
        string messaging_product,
        List<WhatsAppContact>? contacts,
        List<WhatsAppMessage>? messages
    );

    public record WhatsAppContact(string wa_id, WhatsAppProfile profile);
    public record WhatsAppProfile(string name);

    public record WhatsAppMessage(
        string from,
        string id,
        string timestamp,
        string type,
        WhatsAppText? text
    );

    public record WhatsAppText(string body);
}
