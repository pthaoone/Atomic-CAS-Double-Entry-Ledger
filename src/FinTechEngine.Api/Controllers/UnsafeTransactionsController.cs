using FinTechEngine.Api.Domain.Exceptions;
using FinTechEngine.Api.Infrastructure.Services;
using FinTechEngine.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinTechEngine.Api.Controllers;

[ApiController]
[Route("api/unsafe")]
[Route("api/[controller]")]
public class UnsafeTransactionsController : ControllerBase
{
    private readonly IUnsafeTransactionEngine _unsafeEngine;

    public UnsafeTransactionsController(IUnsafeTransactionEngine unsafeEngine)
    {
        _unsafeEngine = unsafeEngine;
    }

    [HttpPost("withdraw")]
    public async Task<ActionResult<TransactionResponseDto>> UnsafeWithdraw([FromBody] WithdrawRequest request)
    {
        try
        {
            var result = await _unsafeEngine.UnsafeWithdrawAsync(request);
            return Ok(result);
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<TransactionResponseDto>> UnsafeTransfer([FromBody] TransferRequest request)
    {
        try
        {
            var result = await _unsafeEngine.UnsafeTransferAsync(request);
            return Ok(result);
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new { error = ex.Message, code = "INSUFFICIENT_FUNDS" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
