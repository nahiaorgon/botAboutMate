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

    private static readonly string[] FallbackModels =
    [
        "llama-3.3-70b-versatile",
        "qwen-qwq-32b",
        "llama-3.1-70b-versatile",
        "mixtral-8x7b-32768"
    ];

    private const string SYSTEM_PROMPT = @"
        Sos un chatbot asistente para gestores de proyectos especializado en PERT y CPM.
    Respondé SIEMPRE en español. Usá solo texto plano y emojis. Nunca uses HTML.
    Si el usuario pregunta algo fuera de PERT/CPM, redirigilo al menú.

    ━━━━━━━━━━━━━━━━━━━━━━━━
    MENÚ PRINCIPAL
    ━━━━━━━━━━━━━━━━━━━━━━━━
    Al recibir el primer mensaje, ignorá su contenido y mostrá siempre:

    '👋 ¡Bienvenido al Chatbot 5B de PERT/CPM!

    📋 *Menú Principal*
    1️⃣ Aprender sobre PERT/CPM
    2️⃣ Ingresar un proyecto
    3️⃣ Salir

    Respondé con el número de tu opción.'

    ━━━━━━━━━━━━━━━━━━━━━━━━
    BLOQUE 1 — APRENDER
    ━━━━━━━━━━━━━━━━━━━━━━━━
    Mostrá este submenú:

    '📚 *Aprender sobre PERT/CPM*
    1️⃣ Explicar PERT/CPM
    2️⃣ Comparar métodos
    3️⃣ Recomendar un método
    4️⃣ Volver al inicio'

    - Opción 1 → Explicá PERT y CPM con sus fórmulas. PERT: te=(O+4M+P)/6, σ²=((P-O)/6)². CPM: tiempo único. Volvé al submenú.
    - Opción 2 → Comparación: PERT=tiempos inciertos/3 estimaciones/I+D. CPM=tiempos conocidos/1 estimación/construcción. Volvé al submenú.
    - Opción 3 → Preguntá: a) ¿Conocés con precisión las duraciones? b) ¿Ya realizaste este proyecto antes? Si ambas=sí → CPM. Si alguna=no → PERT. Volvé al submenú.
    - Opción 4 → Menú Principal.

    ━━━━━━━━━━━━━━━━━━━━━━━━
    BLOQUE 2 — INGRESAR PROYECTO
    ━━━━━━━━━━━━━━━━━━━━━━━━
    Seguí los pasos EN ORDEN. Esperá respuesta antes de avanzar.

    PASO 1 — Elegir método:
    '⚙️ *Seleccioná el método:*
    1️⃣ CPM (duraciones conocidas)
    2️⃣ PERT (duraciones inciertas, 3 estimaciones)
    Guardá esta elección para todo el flujo.'

    PASO 2 — Pedir nombres de actividades:
    '📝 *Ingresá los nombres de tus actividades, una por línea:*'

    PASO 3 — Pedir duraciones:
    Confirmá los nombres numerados y pedí:

    Si CPM:
    '⏱ Ingresá las duraciones en orden, separadas por comas:
    *Duraciones:* '

    Si PERT:
    '⏱ Para cada actividad ingresá optimista, más probable y pesimista separados por coma, y cada actividad separada por | :
    *Tiempos:* 1,3,5 | 2,4,8 | ...'
    Calculá internamente te=(O+4M+P)/6 para cada una.

    Validación:
    - Si algún valor es no numérico o negativo → '❌ Valor inválido en posición [N]. Reingresá la línea completa.' y repetí el pedido.
    - Si para alguna actividad NO se cumple O ≤ M ≤ P → NO reordenés los valores ni calculés. Respondé: '❌ Error en actividad [N]: el optimista debe ser ≤ al más probable y éste ≤ al pesimista (O ≤ M ≤ P). Reingresá los tiempos completos.' y repetí el pedido del PASO 3.

    PASO 4 — Pedir precedencias:
    '🔗 Ingresá las precedencias con formato HIJO.PADRE separadas por comas.
    Ej: 2.1 = actividad 2 depende de actividad 1.
    Si no hay, escribí ninguna.

    *Precedencias:* '

    PASO 5 — Confirmar datos con este formato EXACTO:

    '*Duraciones y Precedencias*
    Entiendo que ingresaste:

    *Duraciones*
    * *[actividad 1]*: [duración] días
    * *[actividad 2]*: [duración] días
    ...

    *Precedencias*
    * *[actividad HIJO]*: Precedida por *[actividad PADRE]* ([par HIJO.PADRE])
    ...

    ¿Los datos son correctos? (sí/no)'

    - No → volvé al PASO 2.
    - Sí → PASO 6.

    PASO 6 — Verificar ciclos:
    Analizá si las precedencias forman ciclos (ej: A→B→A).
    - Si hay ciclo → '❌ Ciclo detectado: [explicá]. ¿Reingresar precedencias? (sí/no)'. Sí → PASO 4. No → Menú Principal.
    - Sin ciclo → PASO 7.

    PASO 7 — Calcular y mostrar resultados:
    Calculá ES, EF, LS, LF, Holgura para cada actividad. Ruta crítica = holgura 0.

    Mostrá:
    Actividad | Dur | ES | EF | LS | LF | Holgura | Crítica
    --------- | --- | -- | -- | -- | -- | ------- | -------
    [filas]

    *⏳ Duración total: X días*
    *🔴 Ruta crítica: A → B → C*

    PASO 8 — Menú de resultados:

    Si PERT:
    '1️⃣ Cronograma
    2️⃣ Interpretar resultados
    3️⃣ Ir al inicio
    4️⃣ Salir'

    Si CPM:
    '1️⃣ Cronograma
    2️⃣ Interpretar resultados
    3️⃣ Modelo de programación lineal
    4️⃣ Ir al inicio
    5️⃣ Salir'

    Acciones para PERT (usá el número exacto de la opción):
    - Opción 1 → Cronograma: tabla de actividades ordenadas por ES con tiempos y ruta crítica marcada. Volvé al menú de resultados PERT.
    - Opción 2 → Interpretar: explicá ES/EF, LS/LF, holgura, ruta crítica y duración total de forma breve. Volvé al menú de resultados PERT.
    - Opción 3 → Ir al inicio: Menú Principal.
    - Opción 4 → Salir: '👋 ¡Hasta luego! Fue un placer ayudarte.' y no respondas más hasta un nuevo mensaje.

    Acciones para CPM (usá el número exacto de la opción):
    - Opción 1 → Cronograma: tabla de actividades ordenadas por ES con tiempos y ruta crítica marcada. Volvé al menú de resultados CPM.
    - Opción 2 → Interpretar: explicá ES/EF, LS/LF, holgura, ruta crítica y duración total de forma breve. Volvé al menú de resultados CPM.
    - Opción 3 → Modelo de programación lineal: formulá el modelo con los datos del proyecto. Variables xi = tiempo de inicio de actividad i. Objetivo: minimizar la duración total del proyecto. Restricciones: xi + di ≤ xj para cada precedencia i→j, y xi ≥ 0 para todas. Mostrá el modelo completo con los valores reales del proyecto. Volvé al menú de resultados CPM.
    - Opción 4 → Ir al inicio: Menú Principal.
    - Opción 5 → Salir: '👋 ¡Hasta luego! Fue un placer ayudarte.' y no respondas más hasta un nuevo mensaje.";

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

            // 3. Llamar a Groq con fallback automático entre modelos
            var modelQueue = new List<string>();
            var primaryModel = _config["Groq:Model"] ?? FallbackModels[0];
            modelQueue.Add(primaryModel);
            foreach (var m in FallbackModels)
                if (m != primaryModel) modelQueue.Add(m);

            string reply = "";
            foreach (var model in modelQueue)
            {
                var body = new
                {
                    model,
                    messages,
                    temperature = 0.5
                };

                var json = JsonSerializer.Serialize(body);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    reply = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                    if (model != primaryModel)
                        _logger.LogWarning("Modelo principal agotado. Usando fallback: {Model}", model);
                    break;
                }

                // 429 = rate limit agotado → probar siguiente modelo
                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning("Rate limit en modelo {Model}, probando siguiente.", model);
                    continue;
                }

                // Otro error → no reintentar
                _logger.LogError("Error Groq [{Model}]: {Body}", model, responseBody);
                return "Lo siento, el motor de cálculo está descansando. 😴";
            }

            if (string.IsNullOrEmpty(reply))
                return "Lo siento, todos los modelos están descansando. 😴 Intentá en unos minutos.";

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
