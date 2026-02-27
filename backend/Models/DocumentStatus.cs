namespace backend.Models;

public enum DocumentStatus
{
    PendingReviewer = 0,
    PendingManager = 1,
    PendingFinance = 2,
    Approved = 3,
    Rejected = 4
}