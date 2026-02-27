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
        {
            return Conflict("This exact file was already uploaded.");
        }

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

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest("Unable to extract text from document.");

        // =========================
        // AI PARSING (REGEX INTELLIGENCE)
        // =========================

        // Vendor = first line
        var vendorMatch = Regex.Match(extractedText, @"^(.*)", RegexOptions.Multiline);
        var vendor = vendorMatch.Success ? vendorMatch.Groups[1].Value.Trim() : "Unknown";

        // Invoice number (pattern like 20-3592 or INV-1234)
var invoiceMatch = Regex.Match(
    extractedText,
    @"Invoice\s*(No|#)?[:\s]*([A-Z0-9\-]+)",
    RegexOptions.IgnoreCase);

var invoiceNumber = invoiceMatch.Success
    ? invoiceMatch.Groups[2].Value.Trim()
    : "";

        // Date like August 1, 2025
DateTime? invoiceDate = null;

// PRIORITY 1: Look specifically for "Invoice Date"
var labeledDateMatch = Regex.Match(
    extractedText,
    @"Invoice\s*Date[:\s]*([A-Za-z0-9,\-/ ]+)",
    RegexOptions.IgnoreCase);

if (labeledDateMatch.Success)
{
    var dateString = labeledDateMatch.Groups[1].Value.Trim();

    if (DateTime.TryParse(dateString, out var parsedDate))
    {
        invoiceDate = parsedDate;
    }
}

// PRIORITY 2: If still null, fallback to first standard date pattern
if (invoiceDate == null)
{
    var fallbackMatch = Regex.Match(
        extractedText,
        @"\b(\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4})\b");

    if (fallbackMatch.Success)
    {
        var dateString = fallbackMatch.Groups[1].Value.Trim();

        if (DateTime.TryParse(dateString, out var parsedDate))
        {
            invoiceDate = parsedDate;
        }
    }
}

        // Total Amount
        var amountMatch = Regex.Match(extractedText,
            @"Total:\s*\n?\s*R?(\d{1,3}(?:,\d{3})*\.\d{2})",
            RegexOptions.IgnoreCase);

        decimal? amount = null;
        if (amountMatch.Success)
        {
            var cleanAmount = amountMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(cleanAmount, out var parsedAmount))
                amount = parsedAmount;
        }

        // Auto detect document type
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
}