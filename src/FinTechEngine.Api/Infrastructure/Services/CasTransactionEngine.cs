using System.Diagnostics;
using FinTechEngine.Api.Domain.Entities;
using FinTechEngine.Api.Domain.Enums;
using FinTechEngine.Api.Domain.Exceptions;
using FinTechEngine.Api.Infrastructure.Data;
using FinTechEngine.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinTechEngine.Api.Infrastructure.Services;

public class CasTransactionEngine : ITransactionEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<CasTransactionEngine> _logger;

    public CasTransactionEngine(
        IServiceScopeFactory scopeFactory,
        IMetricsCollector metrics,
        ILogger<CasTransactionEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<TransactionResponseDto> DepositAsync(DepositRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new InvalidTransactionException("Deposit amount must be strictly greater than 0.");

        return await ExecuteWithRetryAsync(
            type: TransactionType.Deposit,
            sourceAccountId: AppDbContext.SystemVaultAccountId,
            destAccountId: request.AccountId,
            amount: request.Amount,
            description: request.Description ?? $"Deposit {request.Amount:N2} VND to {request.AccountId}",
            maxRetries: request.MaxRetries,
            cancellationToken: cancellationToken);
    }

    public async Task<TransactionResponseDto> WithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new InvalidTransactionException("Withdrawal amount must be strictly greater than 0.");

        return await ExecuteWithRetryAsync(
            type: TransactionType.Withdrawal,
            sourceAccountId: request.AccountId,
            destAccountId: AppDbContext.SystemVaultAccountId,
            amount: request.Amount,
            description: request.Description ?? $"Withdraw {request.Amount:N2} VND from {request.AccountId}",
            maxRetries: request.MaxRetries,
            cancellationToken: cancellationToken);
    }

    public async Task<TransactionResponseDto> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new InvalidTransactionException("Transfer amount must be strictly greater than 0.");

        if (string.Equals(request.SourceAccountId, request.DestinationAccountId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidTransactionException("Source and destination accounts cannot be identical.");

        return await ExecuteWithRetryAsync(
            type: TransactionType.Transfer,
            sourceAccountId: request.SourceAccountId,
            destAccountId: request.DestinationAccountId,
            amount: request.Amount,
            description: request.Description ?? $"Transfer {request.Amount:N2} VND from {request.SourceAccountId} to {request.DestinationAccountId}",
            maxRetries: request.MaxRetries,
            cancellationToken: cancellationToken);
    }

    public async Task ResetAccountAsync(string accountId, decimal balance, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
        if (account == null)
        {
            account = new Account
            {
                AccountId = accountId,
                AccountName = $"Account {accountId}",
                CurrentBalance = balance,
                Version = 1,
                Type = AccountType.Customer,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Accounts.Add(account);
        }
        else
        {
            account.CurrentBalance = balance;
            account.Version += 1;
            account.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<TransactionResponseDto> ExecuteWithRetryAsync(
        TransactionType type,
        string sourceAccountId,
        string destAccountId,
        decimal amount,
        string description,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        _metrics.RecordRequest();
        var stopwatch = Stopwatch.StartNew();
        int currentAttempt = 0;

        while (true)
        {
            currentAttempt++;
            try
            {
                var result = await ExecuteSingleAttemptAsync(
                    type,
                    sourceAccountId,
                    destAccountId,
                    amount,
                    description,
                    currentAttempt - 1,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken);

                stopwatch.Stop();
                _metrics.RecordSuccess(stopwatch.ElapsedMilliseconds, currentAttempt - 1);
                return result;
            }
            catch (ConcurrencyConflictException ex)
            {
                _metrics.RecordConflict();

                if (currentAttempt > maxRetries)
                {
                    stopwatch.Stop();
                    _metrics.RecordFailure(stopwatch.ElapsedMilliseconds);
                    _logger.LogWarning("CAS conflict exhausted {MaxRetries} retries for Tx. Error: {Message}", maxRetries, ex.Message);
                    
                    // Record failed transaction for audit trail
                    await RecordFailedTransactionAsync(type, sourceAccountId, destAccountId, amount, description, currentAttempt - 1, stopwatch.ElapsedMilliseconds, "Max retries exceeded due to concurrent conflicts.");
                    
                    throw new ConcurrencyConflictException($"Transaction aborted after {maxRetries} CAS conflict retries. Contention was too high.");
                }

                _metrics.RecordRetry();

                // Exponential backoff with random jitter: (5 * 1.5^(attempt-1)) + jitter
                int baseBackoffMs = (int)(8 * Math.Pow(1.4, currentAttempt - 1));
                int jitterMs = Random.Shared.Next(2, 15);
                int totalDelay = Math.Min(250, baseBackoffMs + jitterMs);

                await Task.Delay(totalDelay, cancellationToken);
            }
            catch (Exception)
            {
                stopwatch.Stop();
                _metrics.RecordFailure(stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    private async Task<TransactionResponseDto> ExecuteSingleAttemptAsync(
        TransactionType type,
        string sourceAccountId,
        string destAccountId,
        decimal amount,
        string description,
        int retriesUsed,
        long elapsedSoFar,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Fetch fresh account versions and balances (AsNoTracking to ensure no cached state)
        var source = await context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == sourceAccountId, cancellationToken)
            ?? throw new AccountNotFoundException(sourceAccountId);

        var dest = await context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == destAccountId, cancellationToken)
            ?? throw new AccountNotFoundException(destAccountId);

        // 2. Business validation: Overdraft check (Customer accounts cannot have negative balance)
        if (source.Type == AccountType.Customer && source.CurrentBalance < amount)
        {
            throw new InsufficientFundsException(sourceAccountId, source.CurrentBalance, amount);
        }

        // 3. Compute prospective balances
        decimal newSourceBalance = source.CurrentBalance - amount;
        decimal newDestBalance = dest.CurrentBalance + amount;
        long oldSourceVersion = source.Version;
        long oldDestVersion = dest.Version;

        // 4. Double-Entry Ledger and CAS in a single atomic DB transaction
        using var dbTransaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        var txId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        // Create Transaction Record
        var txRecord = new TransactionRecord
        {
            TxId = txId,
            Timestamp = now,
            Type = type,
            Amount = amount,
            Status = TransactionStatus.Completed,
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destAccountId,
            Description = description,
            RetryCount = retriesUsed,
            LatencyMs = elapsedSoFar
        };
        context.Transactions.Add(txRecord);

        // Create Double-Entry Ledger entries:
        // Entry 1: Debit Source Account
        var debitEntry = new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = sourceAccountId,
            Direction = EntryDirection.Debit,
            Amount = amount,
            BalanceAfter = newSourceBalance,
            CreatedAt = now
        };
        context.LedgerEntries.Add(debitEntry);

        // Entry 2: Credit Destination Account
        var creditEntry = new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = destAccountId,
            Direction = EntryDirection.Credit,
            Amount = amount,
            BalanceAfter = newDestBalance,
            CreatedAt = now
        };
        context.LedgerEntries.Add(creditEntry);

        // Save new ledger entries and transaction record first
        await context.SaveChangesAsync(cancellationToken);

        // 5. Execute Atomic CAS updates with Version condition
        // Determine deterministic order to eliminate potential lock contention
        var isSourceFirst = string.CompareOrdinal(sourceAccountId, destAccountId) < 0;
        var firstId = isSourceFirst ? sourceAccountId : destAccountId;
        var firstNewBal = isSourceFirst ? newSourceBalance : newDestBalance;
        var firstOldVer = isSourceFirst ? oldSourceVersion : oldDestVersion;

        var secondId = isSourceFirst ? destAccountId : sourceAccountId;
        var secondNewBal = isSourceFirst ? newDestBalance : newSourceBalance;
        var secondOldVer = isSourceFirst ? oldDestVersion : oldSourceVersion;

        const string casSql = @"
            UPDATE Accounts
            SET CurrentBalance = {0}, Version = Version + 1, UpdatedAt = {1}
            WHERE AccountId = {2} AND Version = {3};";

        int rowsAffected1 = await context.Database.ExecuteSqlRawAsync(
            casSql,
            firstNewBal,
            now,
            firstId,
            firstOldVer);

        if (rowsAffected1 == 0)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException(firstId, firstOldVer);
        }

        int rowsAffected2 = await context.Database.ExecuteSqlRawAsync(
            casSql,
            secondNewBal,
            now,
            secondId,
            secondOldVer);

        if (rowsAffected2 == 0)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException(secondId, secondOldVer);
        }

        // Both updates succeeded atomically! Commit transaction!
        await dbTransaction.CommitAsync(cancellationToken);

        return new TransactionResponseDto(
            TxId: txId,
            Status: "Completed",
            Type: type.ToString(),
            Amount: amount,
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destAccountId,
            SourceNewBalance: newSourceBalance,
            DestinationNewBalance: newDestBalance,
            RetriesUsed: retriesUsed,
            LatencyMs: elapsedSoFar,
            Message: "Transaction processed successfully via Atomic CAS and Double-Entry Ledger.",
            LedgerEntries: new List<LedgerEntryDto>
            {
                new(debitEntry.EntryId, txId, sourceAccountId, EntryDirection.Debit.ToString(), amount, newSourceBalance, now),
                new(creditEntry.EntryId, txId, destAccountId, EntryDirection.Credit.ToString(), amount, newDestBalance, now)
            }
        );
    }

    private async Task RecordFailedTransactionAsync(
        TransactionType type,
        string sourceAccountId,
        string destAccountId,
        decimal amount,
        string description,
        int retries,
        long latencyMs,
        string errorMessage)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var failedTx = new TransactionRecord
            {
                TxId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                Type = type,
                Amount = amount,
                Status = TransactionStatus.Failed,
                SourceAccountId = sourceAccountId,
                DestinationAccountId = destAccountId,
                Description = description,
                RetryCount = retries,
                LatencyMs = latencyMs,
                ErrorMessage = errorMessage
            };

            context.Transactions.Add(failedTx);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist failed transaction log.");
        }
    }
}
