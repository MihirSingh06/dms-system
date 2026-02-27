using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Services;
using System.Text.RegularExpressions;

namespace backend.Controllers;

[ApiController]
[Route("api/ocr")]
[Authorize]
public class OcrController : ControllerBase
{
    private readonly OcrService _ocrService;

    public OcrController(OcrService ocrService)
    {
        _ocrService = ocrService;
    }

[HttpPost("extract")]
public async Task<IActionResult> Extract(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("File required.");

    var extension = Path.GetExtension(file.FileName).ToLower();

    if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
        return BadRequest("Only image files (PNG, JPG) supported for OCR.");

    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    Directory.CreateDirectory(uploadsFolder);

    var filePath = Path.Combine(uploadsFolder, file.FileName);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    var text = await _ocrService.ExtractTextAsync(file);
    Console.WriteLine("===== OCR TEXT START =====");
    Console.WriteLine(text);
    Console.WriteLine("===== OCR TEXT END =====");
    var result = ParseInvoiceText(text);

    return Ok(result);
}

 private object ParseInvoiceText(string text)
{
    // Vendor = first line of document
    var vendorMatch = Regex.Match(text, @"^(.*)", RegexOptions.Multiline);
    var vendor = vendorMatch.Success ? vendorMatch.Groups[1].Value.Trim() : "";

    // Invoice Number pattern like 20-3592
    var invoiceNumberMatch = Regex.Match(text, @"\b\d{2}-\d{4}\b");
    var invoiceNumber = invoiceNumberMatch.Success ? invoiceNumberMatch.Value : "";

    // Date like August 1, 2025
    var dateMatch = Regex.Match(text, @"[A-Za-z]+\s\d{1,2},\s\d{4}");
    var invoiceDate = dateMatch.Success ? dateMatch.Value : "";

    // Extract all currency values like R2,409.25
    var moneyMatches = Regex.Matches(text, @"R\d{1,3}(?:,\d{3})*\.\d{2}");

    string amount = "";
    string vatAmount = "";

    if (moneyMatches.Count > 0)
    {
        var values = moneyMatches
            .Select(m => m.Value.Replace("R", "").Replace(",", ""))
            .Select(v => decimal.TryParse(v, out var d) ? d : 0)
            .Where(v => v > 0)
            .ToList();

        if (values.Count > 0)
        {
            var total = values.Max();
            amount = total.ToString("0.00");

            // VAT usually between 5% and 25% of total
            var vatCandidate = values
                .Where(v => v < total && v > total * 0.05m && v < total * 0.25m)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (vatCandidate > 0)
            {
                vatAmount = vatCandidate.ToString("0.00");
            }
        }
    }

    return new
    {
        Vendor = vendor,
        InvoiceNumber = invoiceNumber,
        InvoiceDate = invoiceDate,
        Amount = amount,
        VatAmount = vatAmount
    };
}
}