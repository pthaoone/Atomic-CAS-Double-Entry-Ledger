using FinTechEngine.Api.Domain.Exceptions;
using FinTechEngine.Api.Infrastructure.Data;
using FinTechEngine.Api.Infrastructure.Services;
using FinTechEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinTechEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionEngine _engine;
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionEngine engine,
        AppDbContext context,
        ILogger<TransactionsController> logger)
    {
        _engine = engine;
        _context = context;
        _logger = logger;
    }

    [HttpPost("deposit")]
    public async Task<ActionResult<TransactionResponseDto>> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var result = await _engine.DepositAsync(request);
            return Ok(result);
        }
        catch (AccountNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidTransactionException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return StatusCode(409, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing deposit.");
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpPost("withdraw")]
    public async Task<ActionResult<TransactionResponseDto>> Withdraw([FromBody] WithdrawRequest request)
    {
        try
        {
            var result = await _engine.WithdrawAsync(request);
            return Ok(result);
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new { 
                error = ex.Message, 
                code = "INSUFFICIENT_FUNDS",
                accountId = ex.AccountId,
                currentBalance = ex.CurrentBalance,
                requestedAmount = ex.RequestedAmount
            });
        }
        catch (AccountNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidTransactionException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return StatusCode(409, new { error = ex.Message, code = "CAS_CONFLICT_LIMIT_EXCEEDED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing withdrawal.");
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<TransactionResponseDto>> Transfer([FromBody] TransferRequest request)
    {
        try
        {
            var result = await _engine.TransferAsync(request);
            return Ok(result);
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new { 
                error = ex.Message, 
                code = "INSUFFICIENT_FUNDS",
                accountId = ex.AccountId,
                currentBalance = ex.CurrentBalance,
                requestedAmount = ex.RequestedAmount
            });
        }
        catch (AccountNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidTransactionException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return StatusCode(409, new { error = ex.Message, code = "CAS_CONFLICT_LIMIT_EXCEEDED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing transfer.");
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetTransactions([FromQuery] int limit = 30)
    {
        var txs = await _context.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .Select(t => new
            {
                t.TxId,
                t.Timestamp,
                Type = t.Type.ToString(),
                t.Amount,
                Status = t.Status.ToString(),
                t.SourceAccountId,
                t.DestinationAccountId,
                t.Description,
                t.RetryCount,
                t.LatencyMs,
                t.ErrorMessage
            })
            .ToListAsync();

        return Ok(txs);
    }
}
