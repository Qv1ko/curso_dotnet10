using Grpc.Core;
using Tienda.Catalogo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHeaderPropagation(options => options.Headers.Add("X-Correlation-Id"));
builder.Services
    .AddGrpcClient<Catalogo.CatalogoClient>(options =>
        options.Address = new Uri(builder.Configuration["Catalogo:Url"]
            ?? throw new InvalidOperationException("Falta la configuración 'Catalogo:Url'.")))
    .AddHeaderPropagation();

var app = builder.Build();
app.UseHeaderPropagation();

app.MapGet("/api/productos/{sku}", async (string sku, Catalogo.CatalogoClient catalogo, CancellationToken ct) => {
    try {
        var reply = await catalogo.ObtenerProductoAsync(
            new ProductoRequest { Sku = sku },
            deadline: DateTime.UtcNow.AddSeconds(2),
            cancellationToken: ct);

        return Results.Ok(new {
            sku = reply.Sku,
            nombre = reply.Nombre,
            precio = reply.PrecioCentimos / 100m,
            disponible = reply.Stock > 0
        });
    } catch (RpcException ex) {
        var (status, title) = ex.StatusCode switch
        {
            StatusCode.NotFound => (StatusCodes.Status404NotFound, "Producto no encontrado"),
            StatusCode.DeadlineExceeded => (StatusCodes.Status504GatewayTimeout, "Catálogo sin respuesta"),
            StatusCode.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Catálogo no disponible"),
            _ => (StatusCodes.Status502BadGateway, "Error en el catálogo")
        };
        return Results.Problem(title: title, statusCode: status);
    }
});

app.Run();
