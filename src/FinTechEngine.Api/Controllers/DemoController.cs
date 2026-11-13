using FinTechEngine.Api.Domain.Entities;
using FinTechEngine.Api.Domain.Enums;
using FinTechEngine.Api.Infrastructure.Data;
using FinTechEngine.Api.Infrastructure.Services;
using FinTechEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinTechEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMetricsCollector _metrics;

    public DemoController(AppDbContext context, IMetricsCollector metrics)
    {
        _context = context;
        _metrics = metrics;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetDemo([FromBody] StressTestPresetRequest? preset = null)
    {
        preset ??= new StressTestPresetRequest();

        // Remove all previous transactions and ledger entries
        _context.LedgerEntries.RemoveRange(_context.LedgerEntries);
        _context.Transactions.RemoveRange(_context.Transactions);

        // Reset or create ACC_A
        var accA = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == "ACC_A");
        if (accA == null)
        {
            accA = new Account
            {
                AccountId = "ACC_A",
                AccountName = "Nguyen Van A (Primary Demo)",
                Type = AccountType.Customer,
                CurrentBalance = preset.InitialBalance,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(accA);
        }
        else
        {
            accA.CurrentBalance = preset.InitialBalance;
            accA.Version = 1;
            accA.UpdatedAt = DateTime.UtcNow;
        }

        // Reset or create ACC_B
        var accB = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == "ACC_B");
        if (accB == null)
        {
            accB = new Account
            {
                AccountId = "ACC_B",
                AccountName = "Tran Thi B (Transfer Target)",
                Type = AccountType.Customer,
                CurrentBalance = 500m,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(accB);
        }
        else
        {
            accB.CurrentBalance = 500m;
            accB.Version = 1;
            accB.UpdatedAt = DateTime.UtcNow;
        }

        // Reset or create Vault
        var vault = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == AppDbContext.SystemVaultAccountId);
        if (vault == null)
        {
            vault = new Account
            {
                AccountId = AppDbContext.SystemVaultAccountId,
                AccountName = "System Central Cash Vault / Clearing",
                Type = AccountType.SystemVault,
                CurrentBalance = 10_000_000_000m,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Accounts.Add(vault);
        }
        else
        {
            vault.CurrentBalance = 10_000_000_000m;
            vault.Version = 1;
            vault.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _metrics.Reset();

        return Ok(new
        {
            message = "Demo environment reset successfully.",
            targetAccount = "ACC_A",
            initialBalance = preset.InitialBalance,
            concurrentRequests = preset.ConcurrentRequests,
            amountPerRequest = preset.AmountPerRequest
        });
    }
}
