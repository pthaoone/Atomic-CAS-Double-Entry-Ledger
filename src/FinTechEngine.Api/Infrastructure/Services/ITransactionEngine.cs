using FinTechEngine.Api.Models;

namespace FinTechEngine.Api.Infrastructure.Services;

public interface ITransactionEngine
{
    Task<TransactionResponseDto> DepositAsync(DepositRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResponseDto> WithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResponseDto> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
    Task ResetAccountAsync(string accountId, decimal balance, CancellationToken cancellationToken = default);
}

public interface IUnsafeTransactionEngine
{
    Task<TransactionResponseDto> UnsafeWithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResponseDto> UnsafeTransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
}
