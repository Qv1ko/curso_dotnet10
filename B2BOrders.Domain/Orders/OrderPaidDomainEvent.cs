namespace B2BOrders.Domain.Orders;

public record OrderPaidDomainEvent(int OrderId, int PaymentId, Money Amount): IDomainEvent {
    public DateTime OccuredOn { get; } = DateTime.UtcNow;
}
