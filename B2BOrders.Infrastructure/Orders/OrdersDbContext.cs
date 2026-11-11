using B2BOrders.Domain.Customers;
using B2BOrders.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace B2BOrders.Infrastructure.Orders;

public class OrdersDbContext: DbContext {
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options): base(options) {}
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
}
