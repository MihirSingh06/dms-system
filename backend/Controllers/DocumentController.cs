using backend;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Reviewer,Manager,Finance")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;

    public DocumentsController(AppDbContext context, FileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    // =========================
    // UPLOAD DOCUMENT
    // =========================
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? vendor,
        [FromForm] string? invoiceNumber,
        [FromForm] DateTime? invoiceDate,
        [FromForm] decimal? amount)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File required.");

        if (!string.Equals(documentType, "Invoice", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(documentType, "CreditNote", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid document type.");
        }

        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null)
            return Unauthorized("Invalid token.");

        var userId = int.Parse(userIdClaim.Value);

        var (path, hash) = await _fileService.SaveFileAsync(file);

        // =========================
        // DUPLICATE DETECTION
        // =========================

        // 1️⃣ Exact file duplicate (hash)
        var hashDuplicate = await _context.Documents
            .FirstOrDefaultAsync(d => d.FileHash == hash);

        if (hashDuplicate != null)
        {
            return Conflict(new
            {
                Error = "DuplicateFile",
                Message = "This exact file has already been uploaded."
            });
        }

        // Normalize strings for safe comparison
        var normalizedVendor = vendor?.Trim().ToLower();
        var normalizedInvoice = invoiceNumber?.Trim().ToLower();

        // 2️⃣ Vendor + Invoice duplicate
        if (!string.IsNullOrWhiteSpace(normalizedVendor) &&
            !string.IsNullOrWhiteSpace(normalizedInvoice))
        {
            var invoiceDuplicate = await _context.Documents
                .FirstOrDefaultAsync(d =>
                    d.Vendor != null &&
                    d.InvoiceNumber != null &&
                    d.Vendor.ToLower() == normalizedVendor &&
                    d.InvoiceNumber.ToLower() == normalizedInvoice);

            if (invoiceDuplicate != null)
            {
                return Conflict(new
                {
                    Error = "DuplicateInvoice",
                    Message = "An invoice with this number already exists for this vendor."
                });
            }
        }

        // 3️⃣ Vendor + Amount duplicate (secondary rule)
        if (!string.IsNullOrWhiteSpace(normalizedVendor) && amount.HasValue)
        {
            var vendorAmountDuplicate = await _context.Documents
                .FirstOrDefaultAsync(d =>
                    d.Vendor != null &&
                    d.Amount.HasValue &&
                    d.Vendor.ToLower() == normalizedVendor &&
                    d.Amount.Value == amount.Value);

            if (vendorAmountDuplicate != null)
            {
                return Conflict(new
                {
                    Error = "DuplicateVendorAmount",
                    Message = "A document with the same vendor and amount already exists."
                });
            }
        }

        // =========================
        // CREATE DOCUMENT
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
    // APPROVE DOCUMENT
    // =========================
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (document.Status == DocumentStatus.Approved ||
            document.Status == DocumentStatus.Rejected)
            return BadRequest("Document already finalized.");

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst("id");

        if (role == null || userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);

        if (document.Status == DocumentStatus.PendingReviewer && role == "Reviewer")
            document.Status = DocumentStatus.PendingManager;
        else if (document.Status == DocumentStatus.PendingManager && role == "Manager")
            document.Status = DocumentStatus.PendingFinance;
        else if (document.Status == DocumentStatus.PendingFinance && role == "Finance")
            document.Status = DocumentStatus.Approved;
        else
            return Forbid();

        var history = new ApprovalHistory
        {
            DocumentId = document.Id,
            UserId = userId,
            Role = role,
            Action = "Approved"
        };

        _context.ApprovalHistories.Add(history);
        await _context.SaveChangesAsync();

        return Ok(document);
    }

    // =========================
    // REJECT DOCUMENT
    // =========================
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] string reason)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (document.Status == DocumentStatus.Approved ||
            document.Status == DocumentStatus.Rejected)
            return BadRequest("Document already finalized.");

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst("id");

        if (role == null || userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);

        document.Status = DocumentStatus.Rejected;

        var history = new ApprovalHistory
        {
            DocumentId = document.Id,
            UserId = userId,
            Role = role,
            Action = "Rejected",
            Reason = reason
        };

        _context.ApprovalHistories.Add(history);
        await _context.SaveChangesAsync();

        return Ok(document);
    }

    // =========================
    // GET HISTORY
    // =========================
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var history = await _context.ApprovalHistories
            .Where(h => h.DocumentId == id)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();

        return Ok(history);
    }

    // =========================
    // GET ALL DOCUMENTS
    // =========================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var documents = await _context.Documents.ToListAsync();
        return Ok(documents);
    }
}