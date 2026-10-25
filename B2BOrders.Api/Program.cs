using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using B2BOrders.Infrastructure.Orders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<OrdersDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health/architecture", () => {
    return new  {
        Status = "healthy",
        Architecture = "Modular Monolith / Hexagonal",
        Modules = new[]{"Customers", "Orders", "Billings"}
    };
});

app.MapGet("/orders", async (OrdersDbContext db, int page = 1, int pageSize = 20) => {
    var query = db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page -1) * pageSize)
        .Take(pageSize)
        .Select(o => new { o.Id, Date = o.CreatedAt, o.Total })
        
    return await query.ToListAsync();
});

// Seed Automático
using (var scope = app.Services.CreateScope()) {
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    dbContext.Database.EnsureCreated(); // Crea la base de datos y las tablas

    if (!dbContext.Orders.Any()) {
        Console.WriteLine("Seeding initial orders data...");

        var orders = new List<Order>();

        for (int i = 0; i < 10000; i++) {
            orders.Add(new Order{
                CustomerId = i % 100,
                CreatedAt = DateTime.Now.AddDays(-new Random().Next(0, 1000)),
                Total = (decimal)(new Random().NextDouble() * 1000)
            });
        }

        dbContext.Orders.AddRange(orders);
        dbContext.SaveChanges();

        Console.WriteLine("Seeding completed.");
    }
}

app.Run();
