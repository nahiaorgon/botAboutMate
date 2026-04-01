using Microsoft.AspNetCore.Mvc;
using PertCPMBot.Service;
using System.Text.Json;
using PertCPMBot.Models; 

namespace PertCPMBot.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly GroqService _anthropic;
    private readonly WhatsAppService _whatsapp;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        GroqService anthropic,
        WhatsAppService whatsapp,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _anthropic = anthropic;
        _whatsapp = whatsapp;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"];
        
        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verificado correctamente.");
            return Content(challenge, "text/plain"); 
    }

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement payload)
    {
        try
        {
            var entry = payload
                .GetProperty("entry")[0]
                .GetProperty("changes")[0]
                .GetProperty("value");

            if (!entry.TryGetProperty("messages", out var messages))
                return Ok(); // No es un mensaje, ignorar

            var message = messages[0];
            var from = message.GetProperty("from").GetString()!;
            var type = message.GetProperty("type").GetString();

            if (type != "text")
            {
                await _whatsapp.SendMessageAsync(from,
                    "Solo proceso mensajes de texto por ahora. " +
                    "Enviame tu consulta sobre PERT o CPM. ✍️");
                return Ok();
            }

            var userText = message
                .GetProperty("text")
                .GetProperty("body")
                .GetString()!;

            _logger.LogInformation("Mensaje de {From}: {Text}", from, userText);

            // Responder "escribiendo..." primero (opcional)
            var reply = await _anthropic.GetReplyAsync(from, userText);
            await _whatsapp.SendMessageAsync(from, reply);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando webhook");
            return Ok(); // Siempre devolver 200 a Meta
        }
    }
}