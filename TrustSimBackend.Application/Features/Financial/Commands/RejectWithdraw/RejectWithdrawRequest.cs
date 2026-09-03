namespace TrustSimBackend.Application.Features.Financial.Commands.RejectWithdraw;

/// <summary>
/// DTO Request từ chối rút tiền của Admin.
/// </summary>
public record RejectWithdrawRequest(
    string Reason
);
