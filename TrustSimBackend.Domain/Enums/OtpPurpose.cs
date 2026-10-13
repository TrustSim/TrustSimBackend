namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Mục đích sử dụng của mã xác thực OTP trong hệ thống TrustSim.
/// </summary>
public enum OtpPurpose
{
    /// <summary>Xác thực số điện thoại liên kết tài khoản / KYC</summary>
    PhoneVerification = 1,

    /// <summary>Xác thực đăng ký tài khoản mới</summary>
    Registration = 2,

    /// <summary>Xác thực đặt lại mật khẩu / Quên mật khẩu</summary>
    PasswordReset = 3,

    /// <summary>Xác thực giao dịch tài chính (Rút tiền, Thanh toán, Khóa cọc)</summary>
    FinancialTransaction = 4,

    /// <summary>Xác thực phê duyệt chuyển nhượng SIM</summary>
    SimTransferApproval = 5,

    /// <summary>Xác thực quyền sở hữu SIM số đẹp</summary>
    SimOwnershipVerification = 6,

    /// <summary>Xác thực giao kết Hợp đồng chuyển nhượng số hóa 3 bên.</summary>
    ContractSigning = 7
}
