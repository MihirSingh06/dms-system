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
        .Replace("ZAR", "", StringComparison.OrdinalIgnoreCase)
        .Replace("R", "", StringComparison.OrdinalIgnoreCase)
        .Replace("$", "")
        .Replace(" ", "")
        .Trim();

    // Handle South African decimal comma
    if (value.Contains(",") && !value.Contains("."))
    {
        value = value.Replace(",", ".");
    }
    else
    {
        // If both comma and dot exist, comma is thousands separator
        value = value.Replace(",", "");
    }

    if (decimal.TryParse(
        value,
        System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture,
        out var result))
    {
        return result;
    }

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
        var shortenedText = extractedText.Length > 3000
            ? extractedText.Substring(0, 3000)
            : extractedText;

            Console.WriteLine("OCR TEXT:");
            Console.WriteLine(shortenedText);

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
  ""total"": """",
  ""vatAmount"": """"
}}

Rules:

Rules:

Vendor Rules:
- Extract the supplier/vendor name that issued the invoice.
- Do NOT use the customer's name, Bill To name, patient's name, account holder, address, or other customer information as the vendor.

Known suppliers:
- Ekukhanyeni Pharmacy
- Ekukhanyeni
- Alpha Pharm
- Alphapharm
- Bizana Square Pharmacy
- Bizana Square

- If one of these supplier names appears on the invoice, prioritize it as the vendor.
- OCR may contain spelling mistakes, missing spaces, broken words, or different capitalization.
- Recognize obvious OCR variations of these supplier names.

Standardize the vendor name as follows:
- Any clear variation of Ekukhanyeni should be returned as Ekukhanyeni Pharmacy.
- Any clear variation of Alpha Pharm or Alphapharm should be returned as Alpha Pharm.
- Any clear variation of Bizana Square Pharmacy or Bizana Square should be returned as Bizana Square Pharmacy.

- If none of the known suppliers can be confidently identified, return the supplier name exactly as it appears on the invoice.


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
- Return the TOTAL INCLUDING VAT only.
- The amount must be the final amount payable by the customer.

Priority from highest to lowest:
1. TOTAL INCL.
2. TOTAL INCL
3. TOTAL INCLUDING VAT
4. TOTAL PAYABLE
5. GRAND TOTAL
6. AMOUNT DUE
7. TOTAL

If the invoice contains:

Total Excl.
VAT @ 15%
Total Incl.

then:
- amount = Total Incl.
- vatAmount = VAT @ 15%

Never return:
- Total Excl.
- Net Amount
- Sales Subtotal
- Subtotal
- Taxable Amount
- Unit Price
- Line Total

The amount returned must include VAT.


VAT Rules:

VAT Rules:

- The invoice may contain VAT amounts for individual line items as well as a total invoice VAT amount.
- ALWAYS return the TOTAL VAT for the entire invoice.
- NEVER return VAT belonging to an individual product or line item.

- On Alpha Pharm, Ekukhanyeni Pharmacy, and Bizana Square invoices, the invoice summary may appear as:

  Total Excl.    Vat @15%    Total Incl.

- When these summary headings appear together, the three corresponding values belong to the invoice summary.

- If the OCR text contains a sequence of values corresponding to:

  Total Excl.    Vat @15%    Total Incl.

  identify the values in that order and use:
  - Total Excl. = first value
  - VAT = second value
  - Total Incl. = third value

- For example, if the OCR contains:

  Total Excl.    Vat @15%    Total Incl.
  938.68         140.80      1 079.48

  then:
  - vatAmount = 140.80
  - total = 1 079.48

- Ignore other VAT values appearing elsewhere in the invoice if they belong to individual line items.

- A VAT value appearing near an Item Description, Item Code, quantity, unit price, or individual product must NOT be used as the invoice vatAmount.

- If both a line-item VAT and a summary VAT are present, ALWAYS use the summary VAT.

- If the total invoice VAT cannot be confidently identified, return an empty string.

- VAT amount must be the TOTAL VAT amount for the entire invoice.
- Do NOT extract VAT from an individual item, product, line, or service.
- If individual line items contain VAT amounts, ignore them.

Prioritize VAT values labeled:
- VAT @ 15%
- VAT@15%
- VAT @15%
- VAT 15%
- VAT AMOUNT
- TOTAL VAT
- TAX TOTAL

If the invoice contains a summary section with:

Total Excl.
VAT @ 15%
Total Incl.

ALWAYS use the amount next to VAT @ 15% as the vatAmount.

Do NOT use:
- VAT from individual line items
- Item VAT
- Line VAT
- Product VAT
- Tax from individual items

If the total invoice VAT is not clearly visible, return an empty string.

The total and VAT values must refer to the invoice summary, not an individual line item.


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
            var total = ParseMoney(result.Total);
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

    public string? Total { get; set; }
}