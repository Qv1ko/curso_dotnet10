using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore; // SOLO PRUEBAS

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<InventarioDbContext>(o =>   // SOLO PRUEBAS
    o.UseSqlServer(builder.Configuration.GetConnectionString("Inventario")));
builder.Services.AddHostedService<PublicadorInventario>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())   // SOLO PRUEBAS: crea la base y un producto
{
    var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
    db.Database.EnsureCreated();
    if (!db.Productos.Any()) { db.Productos.Add(new Producto { Id = "P-1", Stock = 10 }); db.SaveChanges(); }
}

app.MapPost("/api/reservas", async Task<Results<Created<Reserva>, ProblemHttpResult>> (
    SolicitudReserva solicitud, InventarioDbContext db, CancellationToken ct) =>
{
    var producto = await db.Productos.FindAsync([solicitud.ProductoId], ct);
    if (producto is null)
        return TypedResults.Problem(title: "Producto no encontrado", statusCode: StatusCodes.Status404NotFound);
    if (solicitud.Unidades > producto.Stock)
        return TypedResults.Problem(title: "Stock insuficiente", statusCode: StatusCodes.Status409Conflict);

    producto.Stock -= solicitud.Unidades;
    var reserva = new Reserva
    {
        Id = Guid.NewGuid(),
        PedidoId = solicitud.PedidoId,
        ProductoId = solicitud.ProductoId,
        Unidades = solicitud.Unidades
    };
    db.Reservas.Add(reserva);
    db.Outbox.Add(new MensajeOutbox
    {
        Id = Guid.NewGuid(),
        Tipo = "stock.reservado",
        Contenido = JsonSerializer.Serialize(new StockReservado(
            solicitud.PedidoId, solicitud.ProductoId, solicitud.Unidades, producto.Stock)),
        OcurridoEn = DateTime.UtcNow
    });
    await db.SaveChangesAsync(ct);
    return TypedResults.Created($"/api/reservas/{reserva.Id}", reserva);
});

app.Run();