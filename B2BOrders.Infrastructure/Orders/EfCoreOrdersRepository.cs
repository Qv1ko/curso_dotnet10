using B2BOrders.Application.Orders;
using B2BOrders.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace B2BOrders.Infrastructure.Orders;

public class EfCoreOrderRepository: IOrderRepository {
    private readonly OrdersDbContext _context;

    public EfCoreOrderRepository(OrdersDbContext context) {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken) {
        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken) {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }
}