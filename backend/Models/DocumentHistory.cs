using System;

namespace backend.Models;

public class DocumentHistory
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; }

    public string Role { get; set; }
    public string Action { get; set; }
    public string? Reason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}