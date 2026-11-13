using System.Diagnostics;
using FinTechEngine.Api.Domain.Entities;
using FinTechEngine.Api.Domain.Enums;
using FinTechEngine.Api.Domain.Exceptions;
using FinTechEngine.Api.Infrastructure.Data;
using FinTechEngine.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinTechEngine.Api.Infrastructure.Services;

/// <summary>
/// Unsafe Transaction Engine: Deliberately DOES NOT use CAS.
/// Simulates standard naive read-modify-write without concurrency control.
/// Used to demonstrate race conditions, double-spending, and balance corruption during stress tests.
/// </summary>
public class UnsafeTransactionEngine : IUnsafeTransactionEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMetricsCollector _metrics;

    public UnsafeTransactionEngine(IServiceScopeFactory scopeFactory, IMetricsCollector metrics)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
    }

    public async Task<TransactionResponseDto> UnsafeWithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Naive read without locking or version check
        var source = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException(request.AccountId);

        var vault = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == AppDbContext.SystemVaultAccountId, cancellationToken)
            ?? throw new AccountNotFoundException(AppDbContext.SystemVaultAccountId);

        if (source.CurrentBalance < request.Amount)
        {
            sw.Stop();
            _metrics.RecordFailure(sw.ElapsedMilliseconds);
            throw new InsufficientFundsException(request.AccountId, source.CurrentBalance, request.Amount);
        }

        // Simulate real-world asynchronous IO / payment processing delay (1-2 ms)
        // This widens the race condition window just like in production high-concurrency environments!
        await Task.Delay(Random.Shared.Next(1, 4), cancellationToken);

        decimal newSourceBal = source.CurrentBalance - request.Amount;
        decimal newVaultBal = vault.CurrentBalance + request.Amount;

        var txId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var txRecord = new TransactionRecord
        {
            TxId = txId,
            Timestamp = now,
            Type = TransactionType.Withdrawal,
            Amount = request.Amount,
            Status = TransactionStatus.Completed,
            SourceAccountId = request.AccountId,
            DestinationAccountId = AppDbContext.SystemVaultAccountId,
            Description = $"[UNSAFE NO-CAS] Withdraw {request.Amount} VND",
            RetryCount = 0,
            LatencyMs = sw.ElapsedMilliseconds
        };
        context.Transactions.Add(txRecord);

        context.LedgerEntries.Add(new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = request.AccountId,
            Direction = EntryDirection.Debit,
            Amount = request.Amount,
            BalanceAfter = newSourceBal,
            CreatedAt = now
        });

        context.LedgerEntries.Add(new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = AppDbContext.SystemVaultAccountId,
            Direction = EntryDirection.Credit,
            Amount = request.Amount,
            BalanceAfter = newVaultBal,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        // Direct blind UPDATE without CAS WHERE Version = oldVersion!
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Accounts SET CurrentBalance = {0}, UpdatedAt = {1} WHERE AccountId = {2}",
            newSourceBal, now, request.AccountId);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Accounts SET CurrentBalance = {0}, UpdatedAt = {1} WHERE AccountId = {2}",
            newVaultBal, now, AppDbContext.SystemVaultAccountId);

        sw.Stop();
        _metrics.RecordSuccess(sw.ElapsedMilliseconds, 0);

        return new TransactionResponseDto(
            TxId: txId,
            Status: "Completed",
            Type: "Withdrawal",
            Amount: request.Amount,
            SourceAccountId: request.AccountId,
            DestinationAccountId: AppDbContext.SystemVaultAccountId,
            SourceNewBalance: newSourceBal,
            DestinationNewBalance: newVaultBal,
            RetriesUsed: 0,
            LatencyMs: sw.ElapsedMilliseconds,
            Message: "WARNING: Processed via UNSAFE Non-CAS pipeline (Susceptible to Race Conditions).",
            LedgerEntries: new List<LedgerEntryDto>()
        );
    }

    public async Task<TransactionResponseDto> UnsafeTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var source = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == request.SourceAccountId, cancellationToken)
            ?? throw new AccountNotFoundException(request.SourceAccountId);

        var dest = await context.Accounts.FirstOrDefaultAsync(a => a.AccountId == request.DestinationAccountId, cancellationToken)
            ?? throw new AccountNotFoundException(request.DestinationAccountId);

        if (source.CurrentBalance < request.Amount)
        {
            sw.Stop();
            _metrics.RecordFailure(sw.ElapsedMilliseconds);
            throw new InsufficientFundsException(request.SourceAccountId, source.CurrentBalance, request.Amount);
        }

        await Task.Delay(Random.Shared.Next(1, 4), cancellationToken);

        decimal newSourceBal = source.CurrentBalance - request.Amount;
        decimal newDestBal = dest.CurrentBalance + request.Amount;

        var txId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var txRecord = new TransactionRecord
        {
            TxId = txId,
            Timestamp = now,
            Type = TransactionType.Transfer,
            Amount = request.Amount,
            Status = TransactionStatus.Completed,
            SourceAccountId = request.SourceAccountId,
            DestinationAccountId = request.DestinationAccountId,
            Description = $"[UNSAFE NO-CAS] Transfer {request.Amount} VND",
            RetryCount = 0,
            LatencyMs = sw.ElapsedMilliseconds
        };
        context.Transactions.Add(txRecord);

        context.LedgerEntries.Add(new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = request.SourceAccountId,
            Direction = EntryDirection.Debit,
            Amount = request.Amount,
            BalanceAfter = newSourceBal,
            CreatedAt = now
        });

        context.LedgerEntries.Add(new LedgerEntry
        {
            EntryId = Guid.NewGuid().ToString("N"),
            TxId = txId,
            AccountId = request.DestinationAccountId,
            Direction = EntryDirection.Credit,
            Amount = request.Amount,
            BalanceAfter = newDestBal,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Accounts SET CurrentBalance = {0}, UpdatedAt = {1} WHERE AccountId = {2}",
            newSourceBal, now, request.SourceAccountId);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Accounts SET CurrentBalance = {0}, UpdatedAt = {1} WHERE AccountId = {2}",
            newDestBal, now, request.DestinationAccountId);

        sw.Stop();
        _metrics.RecordSuccess(sw.ElapsedMilliseconds, 0);

        return new TransactionResponseDto(
            TxId: txId,
            Status: "Completed",
            Type: "Transfer",
            Amount: request.Amount,
            SourceAccountId: request.SourceAccountId,
            DestinationAccountId: request.DestinationAccountId,
            SourceNewBalance: newSourceBal,
            DestinationNewBalance: newDestBal,
            RetriesUsed: 0,
            LatencyMs: sw.ElapsedMilliseconds,
            Message: "WARNING: Processed via UNSAFE Non-CAS pipeline (Susceptible to Race Conditions).",
            LedgerEntries: new List<LedgerEntryDto>()
        );
    }
}
