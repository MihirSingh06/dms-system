using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace backend.Services;

public class AiExtractionService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public AiExtractionService(IConfiguration config)
    {
        _config = config;
        _httpClient = new HttpClient();
    }

    public async Task<InvoiceAiResult?> ExtractInvoiceData(string extractedText)
    {
        Console.WriteLine("AI METHOD ENTERED");

        var apiKey = _config["OpenAI:ApiKey"];
        Console.WriteLine("API KEY PRESENT: " + (!string.IsNullOrEmpty(apiKey)));

        if (string.IsNullOrEmpty(apiKey))
            return null;

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            Console.WriteLine("NO EXTRACTED TEXT PROVIDED");
            return null;
        }

        var prompt = $@"
Extract the following fields from this invoice text.
Return ONLY valid JSON. Do NOT wrap in markdown.

Fields:
- vendor
- invoiceNumber
- invoiceDate (ISO format yyyy-MM-dd)
- vatAmount
- amount

Invoice text:
{extractedText}
";

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0
        };

        Console.WriteLine("CALLING OPENAI API...");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/chat/completions"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.SendAsync(request);

        Console.WriteLine("OPENAI STATUS: " + response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("OPENAI ERROR BODY:");
            Console.WriteLine(responseBody);
            return null;
        }

        Console.WriteLine("OPENAI RAW RESPONSE:");
        Console.WriteLine(responseBody);

        using var doc = JsonDocument.Parse(responseBody);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            Console.WriteLine("AI RETURNED EMPTY CONTENT");
            return null;
        }

        content = content.Trim();

        // Remove markdown wrapping if present
        if (content.StartsWith("```"))
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start >= 0 && end >= 0)
                content = content.Substring(start, end - start + 1);
        }

        try
        {
            var result = JsonSerializer.Deserialize<InvoiceAiResult>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            Console.WriteLine("AI PARSED SUCCESSFULLY");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine("JSON PARSE ERROR:");
            Console.WriteLine(ex.Message);
            return null;
        }
    }
}

public class InvoiceAiResult
{
    public string? Vendor { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? VatAmount { get; set; }
    public string? Amount { get; set; }
}