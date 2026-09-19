namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Trạng thái của chứng từ kế toán ghi kép.
/// </summary>
public enum JournalStatus
{
    /// <summary>Chứng từ đã được ghi sổ thành công và có hiệu lực.</summary>
    POSTED = 1,

    /// <summary>Chứng từ đã bị đảo ngược/hủy bỏ bằng một chứng từ đối ứng.</summary>
    REVERSED = 2
}
