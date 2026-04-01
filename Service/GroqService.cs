using Npgsql;
using PertCPMBot.Controllers;
using PertCPMBot.Models;
using RepoDb;
using RepoDb.Extensions;
using System.Text;
using System.Text.Json;

namespace PertCPMBot.Service;

public class GroqService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _connString;
    private readonly ILogger<GroqService> _logger;
    IConfiguration _config;

    private const string SYSTEM_PROMPT = @"
        Eres el Chatbot 5B, experto en PERT y CPM para gestión de proyectos.
        Ayudás con: construcción de redes, tiempos tempranos/tardíos (ES, EF, LS, LF),
        holguras y determinación de la ruta crítica.
        PERT: te = (O + 4M + P) / 6, varianza σ² = ((P-O)/6)²
        CPM: tiempo único determinístico.
        Holgura Total = LS - ES. Ruta crítica = actividades con holgura = 0.
        En WhatsApp usá texto plano, sin HTML. Usá * para énfasis y emojis para claridad.
        Respondé siempre en español.";

    public GroqService(IConfiguration config, ILogger<GroqService> logger)
    {
        _apiKey = config["Groq:ApiKey"]!;
        _connString = config.GetConnectionString("DefaultConnection")!;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        _logger = logger;
        _config = config;
    }

    public async Task<string> GetReplyAsync(string phoneNumber, string userMessage)
    {
        try
        {
            await using var db = new NpgsqlConnection(_connString);

            // 1. Guardar mensaje del usuario en DB
            await db.InsertAsync(new ConversationHistory
            {
                Phone = phoneNumber,
                Role = "user",
                Content = userMessage,
                CreatedAt = DateTime.UtcNow
            });

            // 2. Cargar historial 
            var historyRaw = (await db.QueryAsync<ConversationHistory>(
                e => e.Phone == phoneNumber,
                orderBy: new[] { OrderField.Descending<ConversationHistory>(e => e.CreatedAt) },
                top: 10 // Con 10 mensajes es suficiente para contexto
            )).OrderBy(e => e.CreatedAt).ToList();

            var messages = new List<object> {
            new { role = "system", content = SYSTEM_PROMPT }
        };

            foreach (var h in historyRaw)
            {
                messages.Add(new { role = h.Role, content = h.Content });
            }

            // 3. Llamar a Groq (Llama 3)
            var body = new
            {
                model = _config["Groq:Model"],
                messages = messages,
                temperature = 0.5 // Baja temperatura para cálculos más precisos -> 0.2 recomendado para PERT/CPM, pero 0.5 para respuestas más naturales
            };

            var json = JsonSerializer.Serialize(body);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            // La URL de Groq
            var response = await _http.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error Groq: {Body}", responseBody);
                return "Lo siento, el motor de cálculo está descansando. 😴";
            }

            using var doc = JsonDocument.Parse(responseBody);
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // 4. Guardar respuesta en DB
            await db.InsertAsync(new ConversationHistory
            {
                Phone = phoneNumber,
                Role = "assistant",
                Content = reply,
                CreatedAt = DateTime.UtcNow
            });

            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetReplyAsync");
            return "Hubo un error procesando tu consulta de PERT/CPM.";
        }
    }
}
