namespace TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

/// <summary>
/// Thông tin bảo mật của người dùng
/// </summary>
public class UserSecurityProfile : BaseEntity
{
    //Fields
    public Guid UserSecurityProfileId { get => Id; private set => Id = value; }

    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey  { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public DateTime KeyGeneratedAt { get; set; } = DateTime.UtcNow;

    public UserSecurityProfileStatus UserSecurityProfileStatus { get; set; } = UserSecurityProfileStatus.ACTIVE;


    //Foreign Key
    public Guid UserAccountId { get; set; }


    //Navigation Property
    public virtual UserAccount? UserAccount { get; set; }

}