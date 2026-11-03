using B2BOrders.Domain.Customers;

namespace B2BOrders.Application.Customers;

public interface ICustomerRepository {
    Task<Customer?> GetByIdAsync(int id, CancellationToken cantellationToken);
    Task AddAsync(Customer customer, CancellationToken cantellationToken);
}
