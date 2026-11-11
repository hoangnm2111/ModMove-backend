using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Payments.Commands.TopUpDeposit;
using ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;
using ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;
using ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;
using ModMove.Application.Features.Wallets.Queries.GetDriverWallet;
using ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;
using ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;

namespace ModMove.WebApi.Controllers.Driver;

/// <summary>
/// Driver only (RoleConvention): api/driver/wallet. Wallet balances, deposit top-up via PayOS, bank account,
/// earnings breakdown and manual withdrawal requests (UC-DRV-20..23; DECISIONS 5.1, 5.2, M1, GB-13, GB-16, GB-32).
/// </summary>
public sealed class WalletController : ApiControllerBase
{
    /// <summary>Overview of deposit &amp; earnings balances, wallet total, max withdrawable amount, order eligibility, and linked bank account (UC-DRV-20).</summary>
    [HttpGet]
    public Task<DriverWalletResponse> GetWallet(CancellationToken cancellationToken) =>
        Sender.Send(new GetDriverWalletQuery(), cancellationToken);

    /// <summary>Save or update the beneficiary bank account used for manual earnings payouts (UC-DRV-22).</summary>
    [HttpPut("bank-account")]
    public async Task<IActionResult> UpdateBankAccount(
        UpdateDriverBankAccountCommand command, CancellationToken cancellationToken)
    {
        await Sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Paginated wallet transaction ledger, filterable by <c>type</c> and <c>status</c>, newest first (UC-DRV-20).</summary>
    [HttpGet("transactions")]
    public Task<PagedResult<DriverWalletTransactionResponse>> GetTransactions(
        [FromQuery] GetDriverWalletTransactionsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>Paginated list of earnings withdrawal requests, newest first (UC-DRV-22).</summary>
    [HttpGet("withdrawals")]
    public Task<PagedResult<DriverWithdrawalResponse>> GetWithdrawals(
        [FromQuery] GetDriverWithdrawalsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>
    /// Request a wallet withdrawal with no minimum amount, up to the wallet total (earnings only while an active order is in progress) (UC-DRV-22, GB-32).
    /// Deducts from earnings first and from the deposit for the remainder, then holds the requested amount as Pending for Admin payout.
    /// </summary>
    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal(
        RequestWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Revenue summary and per-order earnings breakdown for completed trips in Vietnam time (UC-DRV-23, DECISIONS M1, 9.5).</summary>
    [HttpGet("earnings")]
    public Task<DriverEarningsResponse> GetEarnings(
        [FromQuery] GetDriverEarningsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>
    /// UC-DRV-21, GB-13, GB-32: creates a 10-minute PayOS payment link and QR code for a driver deposit top-up
    /// (any positive whole VND amount; at least the missing amount when deposit is below REQUIRED_DEPOSIT). Calling again replaces any still-Pending session.
    /// </summary>
    [HttpPost("topup")]
    [HttpPost("top-up")]
    public Task<TopUpDepositResponse> TopUpDeposit(
        TopUpDepositRequest request, CancellationToken cancellationToken) =>
        Sender.Send(new TopUpDepositCommand(request.Amount, request.Platform), cancellationToken);
}
