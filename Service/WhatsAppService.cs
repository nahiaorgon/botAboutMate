
using System.Text;
using System.Text.Json;

namespace PertCPMBot.Service;

public class WhatsAppService
{
    private readonly HttpClient _http;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;

    public WhatsAppService(IConfiguration config)
    {
        _accessToken = config["WhatsApp:AccessToken"]!;
        _phoneNumberId = config["WhatsApp:PhoneNumberId"]!;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task SendMessageAsync(string toPhone, string message)
    {
        var url = $"https://graph.facebook.com/v22.0/{_phoneNumberId}/messages"; 

        var body = new
        {
            messaging_product = "whatsapp",
            to = toPhone,
            type = "text",
            text = new { body = message }
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
       // await _http.PostAsync(url, content);

        var response = await _http.PostAsync(url, content);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
}