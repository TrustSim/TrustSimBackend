namespace TrustSimBackend.Domain.Base
{   
    public abstract record BaseDomainEvent: IDomainEvent
    {
        public DateTime OccurredOn { get; protected set; } = DateTime.UtcNow;
    }
}