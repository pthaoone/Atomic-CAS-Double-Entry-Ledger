namespace FinTechEngine.Api.Domain.Exceptions;

public class ConcurrencyConflictException : Exception
{
    public string AccountId { get; }
    public long ExpectedVersion { get; }

    public ConcurrencyConflictException(string accountId, long expectedVersion)
        : base($"Optimistic concurrency CAS conflict detected on Account '{accountId}'. Expected version: {expectedVersion}. Another transaction modified this account concurrently.")
    {
        AccountId = accountId;
        ExpectedVersion = expectedVersion;
    }

    public ConcurrencyConflictException(string message) : base(message)
    {
        AccountId = string.Empty;
        ExpectedVersion = -1;
    }
}

public class InsufficientFundsException : Exception
{
    public string AccountId { get; }
    public decimal CurrentBalance { get; }
    public decimal RequestedAmount { get; }

    public InsufficientFundsException(string accountId, decimal currentBalance, decimal requestedAmount)
        : base($"Insufficient funds in account '{accountId}'. Current balance: {currentBalance:N2}, Requested: {requestedAmount:N2}.")
    {
        AccountId = accountId;
        CurrentBalance = currentBalance;
        RequestedAmount = requestedAmount;
    }
}

public class AccountNotFoundException : Exception
{
    public string AccountId { get; }

    public AccountNotFoundException(string accountId)
        : base($"Account '{accountId}' was not found in the ledger database.")
    {
        AccountId = accountId;
    }
}

public class InvalidTransactionException : Exception
{
    public InvalidTransactionException(string message) : base(message)
    {
    }
}
