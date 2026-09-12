using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    public class TransferProof: AuditableEntity
    {

        public string ImageUrl {get;set;}=string.Empty;
        public string? Description {get;set;}=string.Empty;

        public ProofStatus ProofStatus {get;set;}

        public DateTime VerifiedAt {get;set;}= DateTime.UtcNow;

        public Guid SimTransferId {get;set;}

        public virtual SimTransfer? SimTransfer { get; set; }

    }
}