using System.Net.Http.Headers;
using System.Text.Json;

namespace backend.Services;

public class OcrService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public OcrService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<string> ExtractTextAsync(IFormFile file)
    {
        var apiKey = _config["OcrApiKey"];

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new Exception("OCR API key not configured.");
}

        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream();

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        content.Add(fileContent, "file", file.FileName);
        content.Add(new StringContent(apiKey), "apikey");
        content.Add(new StringContent("eng"), "language");

        var response = await _httpClient.PostAsync("https://api.ocr.space/parse/image", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement
            .GetProperty("ParsedResults")[0]
            .GetProperty("ParsedText")
            .GetString();

        return text ?? "";
    }
}