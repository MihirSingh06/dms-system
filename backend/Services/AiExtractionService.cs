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

    // =========================
    // MONEY CLEANUP + VALIDATION
    // =========================
    private decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value
            .Replace("R", "")
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        if (decimal.TryParse(value, out var result))
            return result;

        return null;
    }

    public async Task<InvoiceAiResult?> ExtractInvoiceData(string extractedText)
    {
        Console.WriteLine("AI METHOD ENTERED");

        var apiKey = _config["OpenAI:ApiKey"];

        Console.WriteLine(
            "API KEY PRESENT: " + (!string.IsNullOrEmpty(apiKey))
        );

        if (string.IsNullOrEmpty(apiKey))
            return null;

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            Console.WriteLine("NO EXTRACTED TEXT PROVIDED");
            return null;
        }

        // =========================
        // LIMIT OCR TEXT
        // =========================
        var shortenedText = extractedText.Length > 1500
            ? extractedText.Substring(0, 1500)
            : extractedText;

        // =========================
        // AI PROMPT
        // =========================
        var prompt = $@"
Extract the following fields from the invoice text.

Return JSON ONLY in this format:

{{
  ""vendor"": """",
  ""invoiceNumber"": """",
  ""invoiceDate"": """",
  ""amount"": """",
  ""vatAmount"": """"
}}

Rules:

Invoice Number Rules:
- Invoice number may appear as:
  DOCUMENT NO
  DOC NO
  DOCUMENT #
  INVOICE NO
  INVOICE #
  TAX INVOICE
  RECEIPT NO
  REF NO
  REFERENCE NO

- If multiple identifiers exist, prioritize:
  1. INVOICE NO
  2. DOCUMENT NO
  3. TAX INVOICE
  4. RECEIPT NO

- Never use VAT numbers, account numbers, customer IDs, or phone numbers as invoice numbers.

Invoice Date Rules:
- Invoice date may appear as:
  DATE
  DATE/TIME
  INVOICE DATE

Amount Rules:
- Amount must be the FINAL TOTAL payable amount.
- Prefer values labeled:
  TOTAL
  TOTAL DUE
  AMOUNT DUE
  GRAND TOTAL
  TOTAL(INCL)

- Never use:
  SUBTOTAL
  BALANCE
  CHANGE
  CASH
  PAID
  TENDERED

- If multiple totals exist, choose the final payable amount.

VAT Rules:
- VAT amount must only be the VAT/TAX amount.
- VAT may appear as:
  VAT
  TAX
  VAT@
  VAT AMOUNT

- If VAT is not visible, return an empty string.

Invoice Text:
{shortenedText}
";

        // =========================
        // OPENAI REQUEST
        // =========================
        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0,
            response_format = new
            {
                type = "json_object"
            }
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

        try
        {
            var result = JsonSerializer.Deserialize<InvoiceAiResult>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (result == null)
            {
                Console.WriteLine("DESERIALIZED RESULT IS NULL");
                return null;
            }

            // =========================
            // VALIDATION
            // =========================
            var total = ParseMoney(result.Amount);
            var vat = ParseMoney(result.VatAmount);

            // VAT cannot exceed total
            if (vat.HasValue &&
                total.HasValue &&
                vat > total)
            {
                Console.WriteLine("INVALID VAT > TOTAL");
                result.VatAmount = "";
            }

            // Prevent missing invoice numbers
            if (string.IsNullOrWhiteSpace(result.InvoiceNumber))
            {
                Console.WriteLine("MISSING INVOICE NUMBER");
                result.InvoiceNumber = "MANUAL_REVIEW";
            }

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