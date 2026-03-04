using backend;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace backend.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Reviewer,Manager,Finance")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly OcrService _ocrService;
    private readonly AiExtractionService _aiService;

    public DocumentsController(
        AppDbContext context,
        FileService fileService,
        OcrService ocrService,
        AiExtractionService aiService)
    {
        _context = context;
        _fileService = fileService;
        _ocrService = ocrService;
        _aiService = aiService;
    }

    // =========================
    // UPLOAD DOCUMENT (AI + FALLBACK)
    // =========================
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File required.");

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("File exceeds 10MB limit.");

        var extension = Path.GetExtension(file.FileName).ToLower();

        if (extension != ".pdf" &&
            extension != ".png" &&
            extension != ".jpg" &&
            extension != ".jpeg" &&
            extension != ".xls" &&
            extension != ".xlsx")
        {
            return BadRequest("Unsupported file type.");
        }

        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);

        var (path, hash) = await _fileService.SaveFileAsync(file);

        // Duplicate hash check
        var hashDuplicate = await _context.Documents
            .FirstOrDefaultAsync(d => d.FileHash == hash);

        if (hashDuplicate != null)
            return Conflict("This exact file was already uploaded.");

        // =========================
        // TEXT EXTRACTION
        // =========================
        string extractedText = "";

        if (extension == ".pdf" ||
            extension == ".png" ||
            extension == ".jpg" ||
            extension == ".jpeg")
        {
            extractedText = await _ocrService.ExtractTextAsync(file);
        }
        else if (extension == ".xls" || extension == ".xlsx")
        {
            extractedText = await _fileService.ExtractExcelTextAsync(file);
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest("Unable to extract text from document.");

        // =========================
        // AI EXTRACTION
        // =========================
        var aiResult = await _aiService.ExtractInvoiceData(extractedText);

        string vendor = "";
        string invoiceNumber = "";
        DateTime? invoiceDate = null;
        decimal? amount = null;
        decimal? vatAmount = null;

        if (aiResult != null)
        {
            vendor = aiResult.Vendor ?? "";
            invoiceNumber = aiResult.InvoiceNumber ?? "";

            // Date
            if (!string.IsNullOrWhiteSpace(aiResult.InvoiceDate) &&
                DateTime.TryParse(aiResult.InvoiceDate, out var parsedDate))
            {
                invoiceDate = parsedDate;
            }

            // Amount (clean R and commas)
            if (!string.IsNullOrWhiteSpace(aiResult.Amount))
            {
                var cleaned = aiResult.Amount
                    .Replace("R", "")
                    .Replace(",", "")
                    .Trim();

                if (decimal.TryParse(cleaned, out var parsedAmount))
                    amount = parsedAmount;
            }

            // VAT (clean R and commas)
            if (!string.IsNullOrWhiteSpace(aiResult.VatAmount))
            {
                var cleanedVat = aiResult.VatAmount
                    .Replace("R", "")
                    .Replace(",", "")
                    .Trim();

                if (decimal.TryParse(cleanedVat, out var parsedVat))
                    vatAmount = parsedVat;
            }
        }

        // =========================
        // REGEX FALLBACKS
        // =========================

        if (string.IsNullOrWhiteSpace(vendor))
        {
            var vendorMatch = Regex.Match(extractedText, @"^(.*)", RegexOptions.Multiline);
            vendor = vendorMatch.Success
                ? vendorMatch.Groups[1].Value.Trim()
                : "Unknown";
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var invoiceMatch = Regex.Match(extractedText, @"\b\d{2}-\d{4}\b");
            if (invoiceMatch.Success)
                invoiceNumber = invoiceMatch.Value.Trim();
        }

        if (invoiceDate == null)
        {
            var dateMatch = Regex.Match(
                extractedText,
                @"\b[A-Za-z]+\s+\d{1,2},\s+\d{4}\b");

            if (dateMatch.Success &&
                DateTime.TryParse(dateMatch.Value, out var parsedDate))
            {
                invoiceDate = parsedDate;
            }
        }

        if (amount == null)
        {
            var totalMatch = Regex.Match(
                extractedText,
                @"Total.*?\bR(\d{1,3}(?:,\d{3})*\.\d{2})",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (totalMatch.Success)
            {
                var clean = totalMatch.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(clean, out var parsed))
                    amount = parsed;
            }
        }

        string documentType = extractedText.ToLower().Contains("credit")
            ? "CreditNote"
            : "Invoice";

        // Duplicate vendor + invoice check
        if (!string.IsNullOrWhiteSpace(vendor) &&
            !string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var duplicate = await _context.Documents
                .FirstOrDefaultAsync(d =>
                    d.Vendor == vendor &&
                    d.InvoiceNumber == invoiceNumber);

            if (duplicate != null)
                return Conflict("Duplicate invoice detected.");
        }

        // =========================
        // SAVE DOCUMENT
        // =========================
        var document = new Document
        {
            FileName = file.FileName,
            FilePath = path,
            DocumentType = documentType,
            Vendor = vendor,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            Amount = amount,
            VatAmount = vatAmount,
            FileHash = hash,
            UploadedByUserId = userId,
            Status = DocumentStatus.PendingReviewer
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return Ok(document);
    }

    // =========================
// GET ALL DOCUMENTS
// =========================
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var documents = await _context.Documents
        .OrderByDescending(d => d.UploadedAt)
        .ToListAsync();

    return Ok(documents);
}

[HttpPost("extract")]
public async Task<IActionResult> ExtractOnly(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("File required.");

    string extractedText = await _ocrService.ExtractTextAsync(file);

    if (string.IsNullOrWhiteSpace(extractedText))
        return BadRequest("Unable to extract text.");

    var aiResult = await _aiService.ExtractInvoiceData(extractedText);

    return Ok(aiResult);
}

[HttpGet("{id}/history")]
public async Task<IActionResult> GetHistory(int id)
{
    var history = await _context.DocumentHistories
        .Where(h => h.DocumentId == id)
        .OrderBy(h => h.Timestamp)
        .Select(h => new
        {
            role = h.Role,
            action = h.Action,
            reason = h.Reason,
            timestamp = h.Timestamp
        })
        .ToListAsync();

    return Ok(history);
}

[HttpPost("{id}/approve")]
public async Task<IActionResult> Approve(int id)
{
    var document = await _context.Documents.FindAsync(id);
    if (document == null)
        return NotFound();

    var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    string action = "";

    if (document.Status == DocumentStatus.PendingReviewer && role == "Reviewer")
    {
        document.Status = DocumentStatus.PendingManager;
        action = "Approved by Reviewer";
    }
    else if (document.Status == DocumentStatus.PendingManager && role == "Manager")
    {
        document.Status = DocumentStatus.PendingFinance;
        action = "Approved by Manager";
    }
    else if (document.Status == DocumentStatus.PendingFinance && role == "Finance")
    {
        document.Status = DocumentStatus.Approved;
        action = "Approved by Finance";
    }
    else
    {
        return BadRequest("Not allowed to approve this document.");
    }

    _context.DocumentHistories.Add(new DocumentHistory
    {
        DocumentId = document.Id,
        Role = role,
        Action = action,
        Timestamp = DateTime.UtcNow
    });

    await _context.SaveChangesAsync();

    return Ok();
}

[HttpPost("{id}/reject")]
public async Task<IActionResult> Reject(int id, [FromBody] string reason)
{
    var document = await _context.Documents.FindAsync(id);
    if (document == null)
        return NotFound();

    var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    document.Status = DocumentStatus.Rejected;

    _context.DocumentHistories.Add(new DocumentHistory
    {
        DocumentId = document.Id,
        Role = role,
        Action = "Rejected",
        Reason = reason,
        Timestamp = DateTime.UtcNow
    });

    await _context.SaveChangesAsync();

    return Ok();
}

[HttpGet("{id}/file")]
public async Task<IActionResult> DownloadFile(int id)
{
    var document = await _context.Documents.FindAsync(id);
    if (document == null)
        return NotFound();

    if (!System.IO.File.Exists(document.FilePath))
        return NotFound();

    var bytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);

    return File(bytes, "application/octet-stream", document.FileName);
}

}