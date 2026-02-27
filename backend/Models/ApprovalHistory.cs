namespace backend.Models;

public class ApprovalHistory
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int UserId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty; // Approved / Rejected
    public string? Reason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}