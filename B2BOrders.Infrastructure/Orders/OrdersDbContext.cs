using Microsoft.EntityFrameworkCore;

namespace B2BOrders.Infrastructure.Orders;

public class Order {
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
}

public class OrdersDbContext: DbContext {
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options): base(options) {}
    public DbSet<Order> Orders { get; set; }
}
