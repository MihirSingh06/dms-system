using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Document
{
    public int Id { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string DocumentType { get; set; } = string.Empty; // Invoice or CreditNote

    public string? Vendor { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? VatAmount { get; set; }
    

    public string FileHash { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.PendingReviewer;

    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}