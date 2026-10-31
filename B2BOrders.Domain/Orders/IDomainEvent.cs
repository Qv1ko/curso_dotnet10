namespace B2BOrders.Domain.Orders;

public interface IDomainEvent {
    DateTime OccuredOn { get; }
}
