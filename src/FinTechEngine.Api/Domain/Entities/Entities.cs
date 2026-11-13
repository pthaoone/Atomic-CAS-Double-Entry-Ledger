using FinTechEngine.Api.Domain.Enums;

namespace FinTechEngine.Api.Domain.Entities;

public class Account
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Customer;
    public decimal CurrentBalance { get; set; }
    
    /// <summary>
    /// CAS Version (Optimistic Concurrency Control token).
    /// Incremented monotonically on every successful atomic update.
    /// </summary>
    public long Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TransactionRecord
{
    public string TxId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? SourceAccountId { get; set; }
    public string? DestinationAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }

    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}

public class LedgerEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
    public string TxId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public EntryDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransactionRecord? Transaction { get; set; }
    public Account? Account { get; set; }
}
