namespace B2BOrders.Domain.Orders;

public interface IOrderRepository {
    Task AddAsync(Order Order, CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
