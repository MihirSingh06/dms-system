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

    public DocumentsController(
        AppDbContext context,
        FileService fileService,
        OcrService ocrService)
    {
        _context = context;
        _fileService = fileService;
        _ocrService = ocrService;
    }

   // =========================
// UPLOAD DOCUMENT (AI AUTO PROCESSING)
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

    // =========================
    // DUPLICATE CHECK (HASH)
    // =========================
    var hashDuplicate = await _context.Documents
        .FirstOrDefaultAsync(d => d.FileHash == hash);

    if (hashDuplicate != null)
        return Conflict("This exact file was already uploaded.");

    // =========================
    // AI TEXT EXTRACTION
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

    Console.WriteLine("===== OCR TEXT START =====");
    Console.WriteLine(extractedText);
    Console.WriteLine("===== OCR TEXT END =====");

    if (string.IsNullOrWhiteSpace(extractedText))
        return BadRequest("Unable to extract text from document.");

// =========================
// AI PARSING (STRUCTURED PATTERN EXTRACTION)
// =========================

// Vendor = first line
var vendorMatch = Regex.Match(extractedText, @"^(.*)", RegexOptions.Multiline);
var vendor = vendorMatch.Success
    ? vendorMatch.Groups[1].Value.Trim()
    : "Unknown";

// =========================
// INVOICE NUMBER (pattern based)
// =========================
var invoiceMatch = Regex.Match(
    extractedText,
    @"\b\d{2}-\d{4}\b");

var invoiceNumber = invoiceMatch.Success
    ? invoiceMatch.Value.Trim()
    : "";

// =========================
// INVOICE DATE (first Month-name date found)
// =========================
DateTime? invoiceDate = null;

var dateMatch = Regex.Match(
    extractedText,
    @"\b[A-Za-z]+\s+\d{1,2},\s+\d{4}\b");

if (dateMatch.Success &&
    DateTime.TryParse(dateMatch.Value, out var parsedDate))
{
    invoiceDate = parsedDate;
}
// =========================
// TOTAL AMOUNT (Smart Label Extraction)
// =========================
decimal? amount = null;

// Try to extract from "Amount Due"
var amountDueMatch = Regex.Match(
    extractedText,
    @"Amount\s*Due.*?\bR(\d{1,3}(?:,\d{3})*\.\d{2})",
    RegexOptions.IgnoreCase | RegexOptions.Singleline);

if (amountDueMatch.Success)
{
    var clean = amountDueMatch.Groups[1].Value.Replace(",", "");
    if (decimal.TryParse(clean, out var parsed))
        amount = parsed;
}

// If not found, fallback to "Total:"
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

    // =========================
    // DUPLICATE CHECK (Vendor + Invoice)
    // =========================
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
        FileHash = hash,
        UploadedByUserId = userId,
        Status = DocumentStatus.PendingReviewer
    };

    _context.Documents.Add(document);
    await _context.SaveChangesAsync();

    return Ok(document);
}

    // =========================
    // APPROVAL FLOW
    // =========================
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null) return NotFound();

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = int.Parse(User.FindFirst("id")!.Value);

        if (document.Status == DocumentStatus.PendingReviewer && role == "Reviewer")
            document.Status = DocumentStatus.PendingManager;
        else if (document.Status == DocumentStatus.PendingManager && role == "Manager")
            document.Status = DocumentStatus.PendingFinance;
        else if (document.Status == DocumentStatus.PendingFinance && role == "Finance")
            document.Status = DocumentStatus.Approved;
        else
            return Forbid();

        _context.ApprovalHistories.Add(new ApprovalHistory
        {
            DocumentId = document.Id,
            UserId = userId,
            Role = role!,
            Action = "Approved"
        });

        await _context.SaveChangesAsync();
        return Ok(document);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] string reason)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null) return NotFound();

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = int.Parse(User.FindFirst("id")!.Value);

        document.Status = DocumentStatus.Rejected;

        _context.ApprovalHistories.Add(new ApprovalHistory
        {
            DocumentId = document.Id,
            UserId = userId,
            Role = role!,
            Action = "Rejected",
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return Ok(document);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _context.Documents.ToListAsync());
    }

[HttpGet("{id}/history")]
public async Task<IActionResult> GetHistory(int id)
{
    var history = await _context.ApprovalHistories
        .Where(h => h.DocumentId == id)
        .OrderBy(h => h.Timestamp)
        .Select(h => new {
            h.Role,
            h.Action,
            h.Reason,
            h.Timestamp
        })
        .ToListAsync();

    return Ok(history);
}

}