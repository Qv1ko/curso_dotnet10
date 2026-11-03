using B2BOrders.Domain.Orders;

namespace B2BOrders.Application.Orders;

public interface IOrderRepository {
    Task AddAsync(Order Order, CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
