using B2BOrders.Domain.Customers;
using B2BOrders.Application.Customers;
using B2BOrders.Infrastructure.Orders;
using Microsoft.EntityFrameworkCore;

namespace B2BOrders.Infrastructure.Customers;

public class EfCoreCustomerRepository: ICustomerRepository {
    private readonly OrdersDbContext _context;
    
    public EfCoreCustomerRepository(OrdersDbContext context) {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) {
        return await _context.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken) {
        await _context.Customers.AddAsync(customer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}