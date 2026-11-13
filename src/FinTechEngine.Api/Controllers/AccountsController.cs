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
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITransactionEngine _engine;

    public AccountsController(AppDbContext context, ITransactionEngine engine)
    {
        _context = context;
        _engine = engine;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAllAccounts()
    {
        var accounts = await _context.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Type == AccountType.SystemVault ? 0 : 1)
            .ThenBy(a => a.AccountId)
            .Select(a => new AccountDto(
                a.AccountId,
                a.AccountName,
                a.Type.ToString(),
                a.CurrentBalance,
                a.Version,
                a.CreatedAt,
                a.UpdatedAt
            ))
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetAccountById(string id)
    {
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null)
            return NotFound(new { error = $"Account '{id}' not found." });

        return Ok(new AccountDto(
            account.AccountId,
            account.AccountName,
            account.Type.ToString(),
            account.CurrentBalance,
            account.Version,
            account.CreatedAt,
            account.UpdatedAt
        ));
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        if (await _context.Accounts.AnyAsync(a => a.AccountId == request.AccountId))
            return Conflict(new { error = $"Account with ID '{request.AccountId}' already exists." });

        var account = new Account
        {
            AccountId = request.AccountId,
            AccountName = request.AccountName,
            Type = request.Type,
            CurrentBalance = request.InitialBalance,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAccountById), new { id = account.AccountId }, new AccountDto(
            account.AccountId,
            account.AccountName,
            account.Type.ToString(),
            account.CurrentBalance,
            account.Version,
            account.CreatedAt,
            account.UpdatedAt
        ));
    }

    [HttpPost("{id}/reset")]
    public async Task<IActionResult> ResetAccount(string id, [FromQuery] decimal balance = 1000m)
    {
        await _engine.ResetAccountAsync(id, balance);
        return Ok(new { message = $"Account '{id}' balance reset to {balance:N2} VND." });
    }

    [HttpGet("{id}/ledger")]
    public async Task<ActionResult<List<LedgerEntryDto>>> GetAccountLedger(string id, [FromQuery] int limit = 50)
    {
        var entries = await _context.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new LedgerEntryDto(
                e.EntryId,
                e.TxId,
                e.AccountId,
                e.Direction.ToString(),
                e.Amount,
                e.BalanceAfter,
                e.CreatedAt
            ))
            .ToListAsync();

        return Ok(entries);
    }
}
