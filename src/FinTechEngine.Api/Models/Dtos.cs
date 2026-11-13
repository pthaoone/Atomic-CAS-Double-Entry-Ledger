using FinTechEngine.Api.Domain.Enums;

namespace FinTechEngine.Api.Models;

public record CreateAccountRequest(
    string AccountId,
    string AccountName,
    decimal InitialBalance = 0,
    AccountType Type = AccountType.Customer
);

public record DepositRequest(
    string AccountId,
    decimal Amount,
    string? Description = null,
    int MaxRetries = 10
);

public record WithdrawRequest(
    string AccountId,
    decimal Amount,
    string? Description = null,
    int MaxRetries = 10
);

public record TransferRequest(
    string SourceAccountId,
    string DestinationAccountId,
    decimal Amount,
    string? Description = null,
    int MaxRetries = 10
);

public record AccountDto(
    string AccountId,
    string AccountName,
    string Type,
    decimal CurrentBalance,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record LedgerEntryDto(
    string EntryId,
    string TxId,
    string AccountId,
    string Direction,
    decimal Amount,
    decimal BalanceAfter,
    DateTime CreatedAt
);

public record TransactionResponseDto(
    string TxId,
    string Status,
    string Type,
    decimal Amount,
    string? SourceAccountId,
    string? DestinationAccountId,
    decimal? SourceNewBalance,
    decimal? DestinationNewBalance,
    int RetriesUsed,
    long LatencyMs,
    string? Message,
    List<LedgerEntryDto> LedgerEntries
);

public record AccountAuditDetail(
    string AccountId,
    string AccountName,
    decimal StoredBalance,
    decimal ComputedLedgerBalance,
    decimal Discrepancy,
    bool IsConsistent,
    int TotalEntries
);

public record LedgerAuditReportDto(
    DateTime AuditTimestamp,
    bool IsOverallBalanced,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal ImbalanceDelta,
    int TotalTransactions,
    int TotalLedgerEntries,
    bool AnyNegativeBalances,
    List<AccountAuditDetail> Accounts,
    string Summary
);

public record EngineMetricsDto(
    long TotalRequests,
    long SuccessfulTransactions,
    long FailedTransactions,
    long CasConflictCount,
    long TotalRetries,
    double AverageLatencyMs,
    double CurrentTps,
    int ActiveConnections
);

public record StressTestPresetRequest(
    string AccountId = "ACC_STRESS_TEST",
    decimal InitialBalance = 1000m,
    int ConcurrentRequests = 100,
    decimal AmountPerRequest = 10m
);
